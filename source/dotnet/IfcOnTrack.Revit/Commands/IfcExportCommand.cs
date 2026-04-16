// Purpose: Command to export the model to IFC with bSDD classifications
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using IfcOnTrack.Revit.IfcExport;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.Commands;

/// <summary>
/// Exports the Revit model to IFC with correct bSDD classification references.
/// Workflow:
///   1. Stores active bSDD dictionaries as Revit IFC classifications
///   2. Generates UDPS property set mappings from bsdd/prop/ parameters
///   3. Runs IFC export
///   4. Post-processes output to fix classification reference URLs
///   5. Restores previous IFC classifications
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class IfcExportCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var logger = Host.GetService<ILogger<IfcExportCommand>>();

        try
        {
            logger.LogInformation("IfcExportCommand executed");

            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc?.Document;

            if (doc == null)
            {
                message = "Please open a document first.";
                return Result.Failed;
            }

            var activeViewId = uiDoc?.ActiveView?.Id ?? ElementId.InvalidElementId;

            var exportService = Host.GetService<IfcExportService>();
            exportService.ExportIfc(doc, activeViewId);

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IfcExportCommand failed");
            TaskDialog.Show("IFC.On-Track.nl Export Error", ex.Message);
            message = ex.Message;
            return Result.Failed;
        }
    }
}
