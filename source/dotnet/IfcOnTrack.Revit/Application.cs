// Purpose: Revit plugin entry point
using System.IO;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Nice3point.Revit.Extensions.UI;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Toolkit.Options;
using IfcOnTrack.Revit.Commands;
using IfcOnTrack.Revit.Model;
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
        SubscribeToDocumentEvents();
        CreateRibbon();
        RegisterDockablePane();
    }

    public override void OnShutdown()
    {
        UnsubscribeFromDocumentEvents();
        Host.Stop();
    }

    // ─── Document events ──────────────────────────────────────────────────────

    private void SubscribeToDocumentEvents()
    {
        Application.ControlledApplication.DocumentOpened += OnDocumentOpened;
        Application.ControlledApplication.DocumentCreated += OnDocumentCreated;
        Application.ControlledApplication.DocumentClosing += OnDocumentClosing;
        Application.ViewActivated += OnViewActivated;
    }

    private void UnsubscribeFromDocumentEvents()
    {
        Application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
        Application.ControlledApplication.DocumentCreated -= OnDocumentCreated;
        Application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
        Application.ViewActivated -= OnViewActivated;
    }

    private void OnDocumentOpened(object? sender, DocumentOpenedEventArgs e)
    {
        if (!e.Document.IsFamilyDocument)
            ReloadSettingsAndSelection(e.Document);
    }

    private void OnDocumentCreated(object? sender, DocumentCreatedEventArgs e)
    {
        if (!e.Document.IsFamilyDocument)
            ReloadSettingsAndSelection(e.Document);
    }

    private void OnDocumentClosing(object? sender, DocumentClosingEventArgs e)
    {
        // Clear any cached data for the closing document
        var settingsManager = Host.TryGetService<SettingsManager>();
        settingsManager?.ClearCache();
    }

    private void OnViewActivated(object? sender, ViewActivatedEventArgs e)
    {
        // Refresh when switching between documents
        try
        {
            var newDoc = e.CurrentActiveView?.Document;
            var oldDoc = e.PreviousActiveView?.Document;
            if (newDoc != null && newDoc.PathName != (oldDoc?.PathName ?? string.Empty))
                ReloadSettingsAndSelection(newDoc);
        }
        catch { /* ignore errors during view activation */ }
    }

    private void ReloadSettingsAndSelection(Autodesk.Revit.DB.Document doc)
    {
        try
        {
            var settingsManager = Host.TryGetService<SettingsManager>();
            if (settingsManager == null) return;

            var settings = settingsManager.LoadSettings(doc);

            // Push settings to the dockable panel browser (C# → JS: window.updateSettings)
            var view = Host.TryGetService<BsddSelectionView>();
            if (view != null)
            {
                view.Dispatcher.InvokeAsync(() => view.PushSettingsToJs(settings));

                // Also push all element types so the panel shows current data
                // (C# → JS: window.updateSelection)
                var elementsManager = Host.TryGetService<ElementsManager>();
                if (elementsManager != null)
                {
                    var entities = elementsManager.GetAllElementTypesAsIfcJson(doc);
                    view.Dispatcher.InvokeAsync(() => view.PushSelectionToJs(entities));
                }
            }
        }
        catch (Exception ex)
        {
            // Don't crash on startup events
            System.Diagnostics.Debug.WriteLine($"[IfcOnTrack] Error reloading settings: {ex.Message}");
        }
    }

    // ─── Ribbon ────────────────────────────────────────────────────────────────

    private void CreateRibbon()
    {
        var panel = Application.CreatePanel("bSDD", "IFC.On-Track.nl");

        // bSDD Panel toggle (dockable pane)
        panel.AddPushButton<ShowBsddPanelCommand>("bSDD\nPanel")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon32.png")
            .SetToolTip("Show/hide bSDD Classification panel");

        // bSDD Search command (modal window with selected elements)
        panel.AddPushButton<BsddCommand>("bSDD\nSearch")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon32.png")
            .SetToolTip("Open bSDD search and link classifications to selected elements");

        panel.AddSeparator();

        // IFC Export with bSDD
        panel.AddPushButton<IfcExportCommand>("IFC\nExport")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/IfcExportIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/IfcExportIcon32.png")
            .SetToolTip("Export to IFC with correct bSDD classification references");

        panel.AddSeparator();

        // IDS Validator
        panel.AddPushButton<IdsCommand>("IDS\nValidate")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/IdsIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/IdsIcon32.png")
            .SetToolTip("Validate model against IDS requirements");

        // Settings
        panel.AddPushButton<SettingsCommand>("Settings")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/SettingsIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/SettingsIcon32.png")
            .SetToolTip("Configure IFC.On-Track.nl settings");
    }

    // ─── Dockable pane ─────────────────────────────────────────────────────────

    private void RegisterDockablePane()
    {
        DockablePaneProvider
            .Register(Application, BsddSelectionPaneId, "bSDD Classification")
            .SetConfiguration(data =>
            {
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
