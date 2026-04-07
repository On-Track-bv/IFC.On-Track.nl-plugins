// Purpose: bSDD Search command - opens the bSDD search UI
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.Commands;

/// <summary>
/// Opens the bSDD Search and Link UI.
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
            var doc = uiApp.ActiveUIDocument?.Document;
            
            if (doc == null)
            {
                message = "Please open a document first.";
                return Result.Failed;
            }
            
            // Get selected elements
            var activeUIDoc = uiApp.ActiveUIDocument;
            var selection = activeUIDoc?.Selection.GetElementIds() 
                ?? Array.Empty<ElementId>();
            logger.LogInformation("Selected {Count} elements", selection.Count);
            
            // Create and show the bSDD window via DI
            var window = Host.GetService<UI.BsddWindow>();
            window.Initialize(uiApp, doc, selection);
            window.ShowDialog();
            
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
