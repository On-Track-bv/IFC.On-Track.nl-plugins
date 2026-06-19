// Purpose: bSDD Search command - opens bSDD search and links classifications to selected elements
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Revit.Model;
using IfcOnTrack.Revit.UI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace IfcOnTrack.Revit.Commands;

/// <summary>
/// Opens the bSDD Search modal dialog for the currently selected elements.
/// If nothing is selected, loads all element types from the document.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class BsddCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var logger = Host.GetService<ILogger<BsddCommand>>();

        try
        {
            logger.LogInformation("BsddCommand executed");

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc?.Document;

            if (doc == null)
            {
                message = "Please open a document first.";
                return Result.Failed;
            }

            var elementsManager = Host.GetService<ElementsManager>();
            var settingsManager = Host.GetService<SettingsManager>();

            // Get selected or all elements
            var selectedIds = uiDoc?.Selection.GetElementIds() ?? Array.Empty<ElementId>();
            List<IfcEntity> entities;

            if (selectedIds.Count > 0)
            {
                logger.LogInformation("Opening bSDD Search with {Count} selected elements", selectedIds.Count);
                entities = elementsManager.GetSelectedElementsAsIfcJson(doc, selectedIds);
            }
            else
            {
                logger.LogInformation("Opening bSDD Search with all document element types");
                entities = elementsManager.GetAllElementTypesAsIfcJson(doc);
            }

            var settings = settingsManager.LoadSettings(doc);
            var propertyIsInstanceMap = elementsManager.GetProjectParameterTypes(doc);
            var bridgeData = new BridgeData
            {
                Settings = settings,
                IfcData = entities,
                PropertyIsInstanceMap = propertyIsInstanceMap
            };

            var searchView = Host.GetService<BsddSearchView>();

            // Wire the refresh callback so saving from the toolbar also updates the dockable panel
            var selectionView = Host.TryGetService<BsddSelectionView>();
            if (selectionView != null)
            {
                searchView.Bridge.SetRefreshCallback(
                    entities => selectionView.Dispatcher.InvokeAsync(
                        () => selectionView.PushSelectionToJs(entities)));
            }

            searchView.Initialize(bridgeData);
            searchView.ShowDialog();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BsddCommand failed");
            message = ex.Message;
            return Result.Failed;
        }
    }
}
