// Purpose: Persists the last element selection per Revit document (keyed by document path).
// Mirrors GlobalSelection.LastSelectedElementsWithDocs from the original bSDD-Revit-plugin.
using IfcOnTrack.Core.Bridge;

namespace IfcOnTrack.Revit.Model;

/// <summary>
/// Stores the last pushed selection (as IfcEntities) per open Revit document path.
/// Matches the per-document selection memory from the original bSDD-Revit-plugin.
/// </summary>
public sealed class LastSelectionCache
{
    private readonly Dictionary<string, List<IfcEntity>> _cache = new();

    public void Set(string docPath, List<IfcEntity> entities)
    {
        _cache[docPath] = entities;
    }

    /// <returns>The cached selection, or null if the document has no prior selection.</returns>
    public List<IfcEntity>? Get(string docPath)
        => _cache.TryGetValue(docPath, out var entities) ? entities : null;

    public void Remove(string docPath)
        => _cache.Remove(docPath);
}
