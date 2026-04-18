// Purpose: Revit plugin entry point
using System.IO;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Nice3point.Revit.Extensions.UI;
using Nice3point.Revit.Toolkit.Decorators;
using Nice3point.Revit.Toolkit.External;
using Nice3point.Revit.Toolkit.Options;
using IfcOnTrack.Core.Bridge;
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
    public static readonly Guid BsddSelectionPaneId = new("E8F5A3D7-9C1B-4E2A-8F7D-5C6B4A3E2D1F");

    public override void OnStartup()
    {
        Host.Start();
        SubscribeToDocumentEvents();
        CreateRibbon();

        // ExternalEvent.Create must be called in IExternalApplication context.
        // Must be done BEFORE RegisterDockablePane: if the panel was visible in the
        // previous Revit session, Revit will eagerly call FrameworkElementCreator
        // during registration, instantiating BsddSelectionView while Event is still null.
        var handler = Host.GetService<SelectionEventHandler>();
        var manager = Host.GetService<SelectionEventManager>();
        manager.Handler = handler;
        manager.Event = Autodesk.Revit.UI.ExternalEvent.Create(handler);

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
        // Remove cached selection for the closing document
        if (!string.IsNullOrEmpty(e.Document.PathName))
            Host.TryGetService<LastSelectionCache>()?.Remove(e.Document.PathName);
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

            var view = Host.TryGetService<BsddSelectionView>();
            if (view == null) return;

            // Push settings (C# → JS: window.updateSettings)
            view.Dispatcher.InvokeAsync(() => view.PushSettingsToJs(settings));

            // Restore last selection for this document, or empty list if none yet
            var cache = Host.TryGetService<LastSelectionCache>();
            var entities = cache?.Get(doc.PathName) ?? new List<IfcEntity>();
            view.Dispatcher.InvokeAsync(() => view.PushSelectionToJs(entities));
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

        // Toggle dockable pane (matches original "bSDD selection" button)
        panel.AddPushButton<ShowBsddPanelCommand>("bSDD\nselection")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/BsddIcon32.png")
            .SetToolTip("Show/hide bSDD Classification panel");

        // IFC Export with bSDD (matches original "IFC export" button)
        panel.AddPushButton<IfcExportCommand>("IFC\nexport")
            .SetImage("/IfcOnTrack.Revit;component/Resources/Icons/IfcExportIcon16.png")
            .SetLargeImage("/IfcOnTrack.Revit;component/Resources/Icons/IfcExportIcon32.png")
            .SetToolTip("Export to IFC with correct bSDD classification references");
    }

    // ─── Dockable pane ─────────────────────────────────────────────────────────

    // Static: persists for the Revit session AppDomain lifetime.
    // GetDockablePane() throws when the pane isn't registered yet, so it cannot
    // be used as a guard. A static flag is the only safe double-registration check.
    private static bool _dockablePaneRegistered;

    private void RegisterDockablePane()
    {
        if (_dockablePaneRegistered)
            return;

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
        _dockablePaneRegistered = true;
    }
}

/// <summary>
/// Marker attribute for implicitly used code (ReSharper/Rider).
/// </summary>
[AttributeUsage(AttributeTargets.All)]
internal sealed class UsedImplicitlyAttribute : Attribute { }
