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
/// </summary>
public class BsddSelectionBridge
{
    private readonly ILogger<BsddSelectionBridge> _logger;
    private readonly ElementsManager _elementsManager;
    private readonly SettingsManager _settingsManager;
    
    // Nice3point async event handlers for Revit API calls
    private readonly AsyncEventHandler _eventHandler = new();
    private readonly AsyncEventHandler<string> _eventHandlerWithResult = new();
    
    // Callback to open search window
    private Action<List<IfcEntity>>? _openSearchCallback;
    
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
    /// Sets callback for opening search window with selected elements.
    /// </summary>
    public void SetSearchCallback(Action<List<IfcEntity>> callback)
    {
        _openSearchCallback = callback;
    }

    /// <summary>
    /// Called from JavaScript to open bSDD Search panel with selected elements.
    /// </summary>
    /// <param name="ifcJsonData">JSON array of IfcEntity objects</param>
    /// <returns>JSON result</returns>
    public async Task<string> bsddSearch(string ifcJsonData)
    {
        _logger.LogInformation("bsddSearch called with {Length} chars", ifcJsonData?.Length ?? 0);
        
        try
        {
            var entities = JsonConvert.DeserializeObject<List<IfcEntity>>(ifcJsonData ?? "[]") 
                ?? new List<IfcEntity>();
            
            // Open the search window with the selected entities
            _openSearchCallback?.Invoke(entities);
            
            return JsonConvert.SerializeObject(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "bsddSearch failed");
            return JsonConvert.SerializeObject(new { success = false, error = ex.Message });
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "saveSettings failed");
        }
    }

    /// <summary>
    /// Called from JavaScript to load current bridge data (settings + empty IFC data).
    /// </summary>
    /// <returns>JSON of BsddBridgeData</returns>
    public async Task<string> loadBridgeData()
    {
        _logger.LogInformation("loadBridgeData called");
        
        try
        {
            return await _eventHandlerWithResult.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    return JsonConvert.SerializeObject(new BridgeData());
                }
                
                var settings = _settingsManager.LoadSettings(doc);
                var bridgeData = new BridgeData
                {
                    Settings = settings,
                    IfcData = new List<IfcEntity>() // Selection panel starts with empty data
                };
                
                return JsonConvert.SerializeObject(bridgeData);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "loadBridgeData failed");
            return JsonConvert.SerializeObject(new BridgeData());
        }
    }

    /// <summary>
    /// Called from JavaScript to load all element types in the document.
    /// </summary>
    /// <returns>JSON of BridgeData with all element types</returns>
    public async Task<string> loadAllElements()
    {
        _logger.LogInformation("loadAllElements called");
        
        try
        {
            return await _eventHandlerWithResult.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    return JsonConvert.SerializeObject(new BridgeData());
                }
                
                var settings = _settingsManager.LoadSettings(doc);
                var entities = _elementsManager.GetAllElementTypesAsIfcJson(doc);
                
                var bridgeData = new BridgeData
                {
                    Settings = settings,
                    IfcData = entities
                };
                
                return JsonConvert.SerializeObject(bridgeData);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "loadAllElements failed");
            return JsonConvert.SerializeObject(new BridgeData());
        }
    }

    /// <summary>
    /// Called from JavaScript to load elements in current view.
    /// </summary>
    /// <returns>JSON of BridgeData with view elements</returns>
    public async Task<string> loadViewElements()
    {
        _logger.LogInformation("loadViewElements called");
        
        try
        {
            return await _eventHandlerWithResult.RaiseAsync(app =>
            {
                var doc = app.ActiveUIDocument?.Document;
                var view = app.ActiveUIDocument?.ActiveView;
                
                if (doc == null || view == null)
                {
                    return JsonConvert.SerializeObject(new BridgeData());
                }
                
                var settings = _settingsManager.LoadSettings(doc);
                var entities = _elementsManager.GetViewElementTypesAsIfcJson(doc, view);
                
                var bridgeData = new BridgeData
                {
                    Settings = settings,
                    IfcData = entities
                };
                
                return JsonConvert.SerializeObject(bridgeData);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "loadViewElements failed");
            return JsonConvert.SerializeObject(new BridgeData());
        }
    }
}
