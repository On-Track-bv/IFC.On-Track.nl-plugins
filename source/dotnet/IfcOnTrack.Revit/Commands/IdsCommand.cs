// Purpose: IDS Validator command - opens the IDS validation UI
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.Commands;

/// <summary>
/// Opens the IDS Validator UI.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class IdsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var logger = Host.GetService<ILogger<IdsCommand>>();
        
        try
        {
            logger.LogInformation("IdsCommand executed");
            
            var uiApp = commandData.Application;
            var doc = uiApp.ActiveUIDocument?.Document;
            
            if (doc == null)
            {
                message = "Please open a document first.";
                return Result.Failed;
            }
            
            // TODO: Implement IDS validation window
            TaskDialog.Show("IFC.On-Track.nl", "IDS Validator - Coming soon!");
            
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "IdsCommand failed");
            message = ex.Message;
            return Result.Failed;
        }
    }
}
