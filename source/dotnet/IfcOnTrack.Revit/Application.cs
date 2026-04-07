// Purpose: Revit plugin entry point
using System.IO;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Toolkit.Options;
using IfcOnTrack.Revit.Commands;
using IfcOnTrack.Revit.UI;

namespace IfcOnTrack.Revit;

/// <summary>
/// IFC.On-Track.nl Revit plugin application entry point.
/// </summary>
[UsedImplicitly]
public class Application : ExternalApplication
{
    /// <summary>
    /// Plugin installation directory.
    /// </summary>
    public static string PluginDirectory => Path.GetDirectoryName(
        System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

    /// <summary>
    /// GUID for the bSDD Selection dockable pane.
    /// </summary>
    public static readonly Guid BsddSelectionPaneId = new("D7C963CE-B3CA-426A-8D51-6E8254D21158");

    public override void OnStartup()
    {
        Host.Start();
        CreateRibbon();
        RegisterDockablePane();
    }

    public override void OnShutdown()
    {
        Host.Stop();
    }

    private void CreateRibbon()
    {
        var panel = Application.CreatePanel("Commands", "IFC.On-Track.nl");

        // bSDD Panel toggle (dockable pane)
        panel.AddPushButton<ShowBsddPanelCommand>("bSDD\nPanel")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon32.png")
            .SetToolTip("Show/hide bSDD Selection panel");

        // bSDD Search command (modal window)
        panel.AddPushButton<BsddCommand>("bSDD\nSearch")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon32.png")
            .SetToolTip("Open bSDD search and link classifications to selected elements");

        panel.AddSeparator();

        // IDS Validator command
        panel.AddPushButton<IdsCommand>("IDS")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/IdsIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/IdsIcon32.png")
            .SetToolTip("Validate model against IDS requirements");

        panel.AddSeparator();

        // Settings command
        panel.AddPushButton<SettingsCommand>("Settings")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/SettingsIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/SettingsIcon32.png")
            .SetToolTip("Configure IFC.On-Track.nl settings");
    }

    private void RegisterDockablePane()
    {
        // Register dockable pane using Nice3point's DockablePaneProvider
        DockablePaneProvider
            .Register(Application, BsddSelectionPaneId, "bSDD Selection")
            .SetConfiguration(data =>
            {
                // Use DI service provider to create the selection view
                data.FrameworkElementCreator = new FrameworkElementCreator<BsddSelectionView>(
                    Host.ServiceProvider);
                data.InitialState = new DockablePaneState
                {
                    MinimumWidth = 400,
                    MinimumHeight = 300,
                    DockPosition = DockPosition.Right
                };
            });
    }
}

/// <summary>
/// Marker attribute for implicitly used code (ReSharper/Rider).
/// </summary>
[AttributeUsage(AttributeTargets.All)]
internal sealed class UsedImplicitlyAttribute : Attribute { }
