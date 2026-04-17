// Purpose: ExternalEvent handler for the 3 selection modes in the dockable pane
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Revit.Model;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.Commands;

public enum SelectionMode { MakeSelection, SelectAll, SelectVisibleInView }

/// <summary>
/// Singleton wrapper that holds the ExternalEvent and its handler so they can be
/// injected into BsddSelectionView after being created during OnStartup.
/// </summary>
public sealed class SelectionEventManager
{
    public ExternalEvent? Event { get; internal set; }
    public SelectionEventHandler? Handler { get; internal set; }
}

/// <summary>
/// IExternalEventHandler for the 3 element-selection modes.
/// Must be raised via ExternalEvent so Revit API access runs on the main thread.
/// </summary>
public sealed class SelectionEventHandler : IExternalEventHandler
{
    private readonly ElementsManager _elementsManager;
    private readonly LastSelectionCache _cache;
    private readonly ILogger<SelectionEventHandler> _logger;

    public SelectionMode Mode { get; set; } = SelectionMode.SelectAll;

    /// <summary>Callback invoked (on Revit main thread) after selection is complete.</summary>
    public Action<List<IfcEntity>>? OnComplete { get; set; }

    public SelectionEventHandler(ElementsManager elementsManager, LastSelectionCache cache, ILogger<SelectionEventHandler> logger)
    {
        _elementsManager = elementsManager;
        _cache = cache;
        _logger = logger;
    }

    public void Execute(UIApplication app)
    {
        _logger.LogInformation("SelectionEventHandler.Execute: mode={Mode}", Mode);

        var uidoc = app.ActiveUIDocument;
        if (uidoc == null)
        {
            _logger.LogWarning("SelectionEventHandler.Execute: ActiveUIDocument is null");
            return;
        }

        var doc = uidoc.Document;
        _logger.LogDebug("SelectionEventHandler.Execute: document={Doc}", doc.PathName);

        var entities = Mode switch
        {
            SelectionMode.SelectAll          => _elementsManager.GetAllElementTypesAsIfcJson(doc),
            SelectionMode.SelectVisibleInView => _elementsManager.GetViewElementTypesAsIfcJson(doc, doc.ActiveView),
            SelectionMode.MakeSelection      => GetManualSelection(uidoc),
            _                                => new List<IfcEntity>()
        };

        _logger.LogInformation("SelectionEventHandler.Execute: collected {Count} entities", entities.Count);

        // Persist so document-switch can restore this selection
        if (!string.IsNullOrEmpty(doc.PathName))
            _cache.Set(doc.PathName, entities);

        OnComplete?.Invoke(entities);
    }

    private List<IfcEntity> GetManualSelection(UIDocument uidoc)
    {
        try
        {
            var existing = uidoc.Selection.GetElementIds();
            ICollection<ElementId> ids;

            if (existing.Count > 0)
            {
                ids = existing;
            }
            else
            {
                ids = uidoc.Selection.PickObjects(ObjectType.Element)
                    .Select(r => r.ElementId)
                    .ToList();
            }

            return _elementsManager.GetSelectedElementsAsIfcJson(uidoc.Document, ids);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return new List<IfcEntity>();
        }
    }

    public string GetName() => "IFC.On-Track.nl Selection";
}
