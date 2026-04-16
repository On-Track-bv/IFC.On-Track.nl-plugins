// Purpose: JavaScript bridge for bSDD Selection dockable pane
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nice3point.Revit.Toolkit.External.Handlers;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Revit.Model;

namespace IfcOnTrack.Revit.Bridge;

/// <summary>
/// JavaScript bridge for the bSDD Selection dockable pane.
/// Exposes methods callable from the embedded browser UI.
/// Uses Nice3point's AsyncEventHandler for thread-safe Revit API access.
///
/// C# → JS push functions (set as window.xxx by useCefSharpBridge.ts):
///   window.updateSelection(entities)  — update the full element list
///   window.updateSettings(settings)   — update settings
/// JS → C# pull functions (called by useCefSharpBridge.ts on init):
///   bsddBridge.loadBridgeData()       — returns {settings, ifcData:[], propertyIsInstanceMap}
///   bsddBridge.loadSettings()         — returns settings JSON
/// JS → C# event functions:
///   bsddBridge.bsddSearch(entities)   — open search window
///   bsddBridge.bsddSelect(entities)   — select in Revit
///   bsddBridge.saveSettings(settings) — persist settings
/// </summary>
public class BsddSelectionBridge
{
    private readonly ILogger<BsddSelectionBridge> _logger;
    private readonly ElementsManager _elementsManager;
    private readonly SettingsManager _settingsManager;
    
    // Nice3point async event handlers for Revit API calls
#pragma warning disable CS0618 // AsyncEventHandler is obsolete; migrate to AsyncExternalEvent when API stabilises
    private readonly AsyncEventHandler _eventHandler = new();
    private readonly AsyncEventHandler<string> _eventHandlerWithResult = new();
#pragma warning restore CS0618
    
    /// <summary>Callback invoked (on UI thread) to open search window. Receives full BridgeData with entities + settings + propertyIsInstanceMap.</summary>
    private Action<BridgeData>? _openSearchCallback;
    
    /// <summary>Callback invoked (on UI thread) with refreshed entities after a search-window save.</summary>
    private Action<List<IfcEntity>>? _refreshCallback;

    /// <summary>Callback invoked (on UI thread) with new settings after saveSettings.</summary>
    private Action<BridgeSettings>? _pushSettingsCallback;
    
    public BsddSelectionBridge(
        ILogger<BsddSelectionBridge> logger,
        ElementsManager elementsManager,
        SettingsManager settingsManager)
    {
        _logger = logger;
        _elementsManager = elementsManager;
        _settingsManager = settingsManager;
    }
    
    /// <summary>
    /// Sets callback for opening search window. The callback receives a full BridgeData
    /// (entities + settings + propertyIsInstanceMap).
    /// </summary>
    public void SetSearchCallback(Action<BridgeData> callback)
    {
        _openSearchCallback = callback;
    }

    /// <summary>
    /// Sets callback (called on UI thread) with refreshed entities after a search-window save.
    /// </summary>
    public void SetRefreshCallback(Action<List<IfcEntity>> callback)
    {
        _refreshCallback = callback;
    }

    /// <summary>
    /// Sets callback (called on UI thread) with updated settings after saveSettings.
    /// </summary>
    public void SetPushSettingsCallback(Action<BridgeSettings> callback)
    {
        _pushSettingsCallback = callback;
    }

    /// <summary>
    /// Called by the search window after a successful save. Pushes fresh entities to the panel.
    /// </summary>
    public void TriggerRefresh(List<IfcEntity> entities)
    {
        _refreshCallback?.Invoke(entities);
    }

    /// <summary>
    /// Called from JavaScript to open bSDD Search panel with selected elements.
    /// Builds the full BridgeData (entities + settings + propertyIsInstanceMap) that the search
    /// window needs, then invokes the open-search callback on the UI thread.
    /// </summary>
    /// <param name="ifcJsonData">JSON array of IfcEntity objects</param>
    public async Task bsddSearch(string ifcJsonData)
    {
        _logger.LogInformation("bsddSearch called with {Length} chars", ifcJsonData?.Length ?? 0);
        
        try
        {
            var entities = JsonConvert.DeserializeObject<List<IfcEntity>>(ifcJsonData ?? "[]")
                ?? new List<IfcEntity>();

            // Build full BridgeData in the Revit API context so we get settings + propertyIsInstanceMap
            var bridgeDataJson = await _eventHandlerWithResult.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                var settings = doc != null ? _settingsManager.LoadSettings(doc) : new BridgeSettings();
                var propMap = doc != null ? _elementsManager.GetProjectParameterTypes(doc) : new Dictionary<string, bool>();
                return JsonConvert.SerializeObject(new BridgeData
                {
                    Settings = settings,
                    IfcData = entities,
                    PropertyIsInstanceMap = propMap
                });
            });
            var bridgeData = JsonConvert.DeserializeObject<BridgeData>(bridgeDataJson) ?? new BridgeData { IfcData = entities };

            _openSearchCallback?.Invoke(bridgeData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "bsddSearch failed");
        }
    }

    /// <summary>
    /// Called from JavaScript to select/highlight elements in the Revit model.
    /// </summary>
    /// <param name="ifcJsonData">JSON array of IfcEntity objects with Tag properties</param>
    public async Task bsddSelect(string ifcJsonData)
    {
        _logger.LogInformation("bsddSelect called");
        
        try
        {
            var entities = JsonConvert.DeserializeObject<List<IfcEntity>>(ifcJsonData ?? "[]") 
                ?? new List<IfcEntity>();
            
            await _eventHandler.RaiseAsync(app =>
            {
                _elementsManager.SelectElementsInModel(app, entities);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "bsddSelect failed");
        }
    }

    /// <summary>
    /// Called from JavaScript to save settings to the document.
    /// After save, pushes the new settings back to the browser panel via window.updateSettings.
    /// </summary>
    /// <param name="settingsJson">JSON of BridgeSettings</param>
    public async Task saveSettings(string settingsJson)
    {
        _logger.LogInformation("saveSettings called");
        
        try
        {
            var settings = JsonConvert.DeserializeObject<BridgeSettings>(settingsJson);
            if (settings == null) return;
            
            await _eventHandler.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null) return;
                
                using var transaction = new Transaction(doc, "Save bSDD Settings");
                transaction.Start();
                _settingsManager.SaveSettings(doc, settings);
                transaction.Commit();
            });

            // Push updated settings to the browser panel (C# → JS push)
            _pushSettingsCallback?.Invoke(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "saveSettings failed");
        }
    }

    /// <summary>
    /// Called from JavaScript (useCefSharpBridge) on panel init to load the initial bridge data.
    /// Returns {settings, ifcData: [], propertyIsInstanceMap} — the selection panel starts empty;
    /// the full element list is pushed separately via window.updateSelection([...]).
    /// </summary>
    public async Task<string> loadBridgeData()
    {
        _logger.LogInformation("loadBridgeData called");
        
        try
        {
            return await _eventHandlerWithResult.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                    return JsonConvert.SerializeObject(new BridgeData());
                
                var settings = _settingsManager.LoadSettings(doc);
                var propMap = _elementsManager.GetProjectParameterTypes(doc);

                return JsonConvert.SerializeObject(new BridgeData
                {
                    Settings = settings,
                    IfcData = new List<IfcEntity>(), // full list pushed via updateSelection
                    PropertyIsInstanceMap = propMap
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "loadBridgeData failed");
            return JsonConvert.SerializeObject(new BridgeData());
        }
    }

    /// <summary>
    /// Called from JavaScript to load settings only (without element data).
    /// Required by BsddBridgeInterface.ts contract alongside loadBridgeData.
    /// </summary>
    public async Task<string> loadSettings()
    {
        _logger.LogInformation("loadSettings called");

        try
        {
            return await _eventHandlerWithResult.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                var settings = doc != null ? _settingsManager.LoadSettings(doc) : new BridgeSettings();
                return JsonConvert.SerializeObject(settings);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "loadSettings failed");
            return JsonConvert.SerializeObject(new BridgeSettings());
        }
    }
}
