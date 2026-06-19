// Purpose: Command to toggle the bSDD Selection dockable pane
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;

namespace IfcOnTrack.Revit.Commands;

/// <summary>
/// Toggles visibility of the bSDD Selection dockable pane.
/// Uses Nice3point's ExternalCommand base class for simplified API access.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ShowBsddPanelCommand : ExternalCommand
{
    public override void Execute()
    {
        var paneId = new DockablePaneId(IfcOnTrack.Revit.Application.BsddSelectionPaneId);
        var pane = Application.GetDockablePane(paneId);
        
        if (pane == null)
        {
            ErrorMessage = "bSDD Selection pane is not registered.";
            Result = Autodesk.Revit.UI.Result.Failed;
            return;
        }
        
        // Toggle visibility
        if (pane.IsShown())
        {
            pane.Hide();
        }
        else
        {
            pane.Show();
        }
    }
}
