// Purpose: Settings command - opens plugin configuration
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.Commands;

/// <summary>
/// Opens the Settings UI.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class SettingsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var logger = Host.GetService<ILogger<SettingsCommand>>();
        
        try
        {
            logger.LogInformation("SettingsCommand executed");
            
            // TODO: Implement settings window
            TaskDialog.Show("IFC.On-Track.nl", "Settings - Coming soon!");
            
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SettingsCommand failed");
            message = ex.Message;
            return Result.Failed;
        }
    }
}
