// Purpose: JavaScript bridge for bSDD Search modal window
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nice3point.Revit.Toolkit.External.Handlers;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Revit.Model;

namespace IfcOnTrack.Revit.Bridge;

/// <summary>
/// JavaScript bridge for the bSDD Search modal window.
/// Handles save/cancel operations and loads initial bridge data.
/// Uses Nice3point's AsyncEventHandler for thread-safe Revit API access.
/// </summary>
public class BsddSearchBridge
{
    private readonly ILogger<BsddSearchBridge> _logger;
    private readonly ElementsManager _elementsManager;
    private readonly SettingsManager _settingsManager;
    private readonly LastSelectionCache _lastSelectionCache;

    // Nice3point async event handlers
#pragma warning disable CS0618 // AsyncEventHandler is obsolete; migrate to AsyncExternalEvent when API stabilises
    private readonly AsyncEventHandler _eventHandler = new();
    private readonly AsyncEventHandler<string> _eventHandlerWithResult = new();
#pragma warning restore CS0618

    // Initial data passed to search window
    private BridgeData? _bridgeData;

    // Callbacks for window management
    private Action? _closeWindowCallback;
    private Action<List<IfcEntity>>? _refreshSelectionCallback;

    public BsddSearchBridge(
        ILogger<BsddSearchBridge> logger,
        ElementsManager elementsManager,
        SettingsManager settingsManager,
        LastSelectionCache lastSelectionCache)
    {
        _logger = logger;
        _elementsManager = elementsManager;
        _settingsManager = settingsManager;
        _lastSelectionCache = lastSelectionCache;
    }
    
    /// <summary>
    /// Initialize with data to display in search window.
    /// </summary>
    public void Initialize(BridgeData bridgeData)
    {
        _bridgeData = bridgeData;
    }
    
    /// <summary>
    /// Sets callback for closing the window.
    /// </summary>
    public void SetCloseCallback(Action callback)
    {
        _closeWindowCallback = callback;
    }
    
    /// <summary>
    /// Sets callback for refreshing selection panel after save.
    /// Receives the freshly-read entity list so the panel can push it via window.updateSelection.
    /// </summary>
    public void SetRefreshCallback(Action<List<IfcEntity>> callback)
    {
        _refreshSelectionCallback = callback;
    }

    /// <summary>
    /// Called from JavaScript to save bSDD data to Revit elements.
    /// After saving, reads back ONLY the edited entities (not all elements in the model)
    /// and passes them to the refresh callback so the selection panel updates only those items.
    /// </summary>
    /// <param name="ifcJsonData">JSON of BridgeData with updated IFC entities</param>
    /// <returns>JSON result</returns>
    public async Task<string> save(string ifcJsonData)
    {
        _logger.LogInformation("save called with {Length} chars", ifcJsonData?.Length ?? 0);

        try
        {
            var bridgeData = JsonConvert.DeserializeObject<BridgeData>(ifcJsonData ?? string.Empty);
            if (bridgeData == null)
                return JsonConvert.SerializeObject(new { success = false, error = "Invalid data" });

            List<IfcEntity>? refreshedEntities = null;

            await _eventHandlerWithResult.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) return string.Empty;

                // ApplyBridgeData opens its own sub-transactions internally
                _elementsManager.ApplyBridgeData(doc, bridgeData);

                // Persist settings if present
                if (bridgeData.Settings != null)
                {
                    using var settingsTx = new Transaction(doc, "Save bSDD Settings");
                    settingsTx.Start();
                    _settingsManager.SaveSettings(doc, bridgeData.Settings);
                    settingsTx.Commit();
                }

                // Restore the FULL original selection from cache (e.g., user selected 10, edited 2, restore all 10)
                // Priority: LastSelectionCache > _bridgeData > fallback to edited entities
                List<IfcEntity> originalSelection;
                if (!string.IsNullOrEmpty(doc.PathName))
                {
                    var cached = _lastSelectionCache.Get(doc.PathName);
                    if (cached != null && cached.Any())
                    {
                        originalSelection = cached;
                        _logger.LogInformation("Using {Count} entities from LastSelectionCache", cached.Count);
                    }
                    else if (_bridgeData?.IfcData != null && _bridgeData.IfcData.Any())
                    {
                        originalSelection = _bridgeData.IfcData;
                        _logger.LogInformation("Using {Count} entities from _bridgeData", _bridgeData.IfcData.Count);
                    }
                    else
                    {
                        originalSelection = bridgeData.IfcData ?? new List<IfcEntity>();
                        _logger.LogInformation("Fallback: using {Count} edited entities", originalSelection.Count);
                    }
                }
                else
                {
                    originalSelection = _bridgeData?.IfcData ?? bridgeData.IfcData ?? new List<IfcEntity>();
                }

                // Build a map of Tag → original entity order
                var originalTags = originalSelection
                    .Where(e => !string.IsNullOrEmpty(e.Tag))
                    .Select((e, index) => new { e.Tag, Index = index })
                    .ToDictionary(x => x.Tag, x => x.Index);

                // Fetch fresh data ONLY for the originally selected entities
                var allEntities = _elementsManager.GetAllElementTypesAsIfcJson(doc);
                refreshedEntities = allEntities
                    .Where(e => !string.IsNullOrEmpty(e.Tag) && originalTags.ContainsKey(e.Tag))
                    .OrderBy(e => originalTags.TryGetValue(e.Tag!, out var idx) ? idx : int.MaxValue)
                    .ToList();

                _logger.LogInformation("Refreshing {Count} originally selected entities (preserving order)", 
                    refreshedEntities.Count);

                return string.Empty;
            });

            // Push refreshed entities to selection panel (C# → JS via window.updateSelection)
            if (refreshedEntities != null)
                _refreshSelectionCallback?.Invoke(refreshedEntities);

            // Close search window
            _closeWindowCallback?.Invoke();

            return JsonConvert.SerializeObject(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "save failed");
            return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Called from JavaScript to cancel and close the search window.
    /// </summary>
    public void cancel()
    {
        _logger.LogInformation("cancel called");
        _closeWindowCallback?.Invoke();
    }

    /// <summary>
    /// Called from JavaScript to load the current bridge data.
    /// </summary>
    /// <returns>JSON of BridgeData</returns>
    public string loadBridgeData()
    {
        _logger.LogInformation("loadBridgeData called");
        
        if (_bridgeData != null)
        {
            return JsonConvert.SerializeObject(_bridgeData);
        }
        
        return JsonConvert.SerializeObject(new BridgeData());
    }

    /// <summary>
    /// Called from JavaScript to load settings only.
    /// Required by BsddBridgeInterface.ts contract alongside loadBridgeData.
    /// </summary>
    public string loadSettings()
    {
        _logger.LogInformation("loadSettings called");
        var settings = _bridgeData?.Settings ?? new BridgeSettings();
        return JsonConvert.SerializeObject(settings);
    }

    /// <summary>
    /// Called from JavaScript to save settings only.
    /// </summary>
    /// <param name="settingsJson">JSON of BridgeSettings</param>
    public async Task saveSettings(string settingsJson)
    {
        _logger.LogInformation("saveSettings called");
        
        try
        {
            var settings = JsonConvert.DeserializeObject<BridgeSettings>(settingsJson);
            if (settings == null) return;
            
            // Update local data
            if (_bridgeData != null)
            {
                _bridgeData.Settings = settings;
            }
            
            await _eventHandler.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) return;
                
                using var transaction = new Transaction(doc, "Save bSDD Settings");
                transaction.Start();
                _settingsManager.SaveSettings(doc, settings);
                transaction.Commit();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "saveSettings failed");
        }
    }
}
