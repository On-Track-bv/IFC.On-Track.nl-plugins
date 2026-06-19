// Purpose: Manages Revit IFC Classification data storage for multi-classification IFC export
// This is the bridge between bSDD dictionaries and Revit's built-in IFC export classification system.
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Revit.Model;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.IfcExport;

/// <summary>
/// Manages the Revit IFC Classification data stored in the document's DataStorage.
/// The schema GUID 9A5A28C2-DDAC-4828-8B8A-3EE97118017A matches Revit's own IFC exporter schema
/// so that classifications appear correctly in the IFC export dialog.
///
/// This supports multiple simultaneous classifications per element (e.g., IFC class + NL-SfB + OmniClass).
/// </summary>
public class IfcClassificationManager
{
    private readonly ILogger<IfcClassificationManager> _logger;
    private readonly SettingsManager _settingsManager;

    // This schema GUID is the one Revit's own IFC exporter uses – do not change it
    private static readonly Guid ClassificationSchemaId = new("9A5A28C2-DDAC-4828-8B8A-3EE97118017A");

    private const string FieldName = "ClassificationName";
    private const string FieldSource = "ClassificationSource";
    private const string FieldEdition = "ClassificationEdition";
    private const string FieldEditionDate_Day = "ClassificationEditionDate_Day";
    private const string FieldEditionDate_Month = "ClassificationEditionDate_Month";
    private const string FieldEditionDate_Year = "ClassificationEditionDate_Year";
    private const string FieldLocation = "ClassificationLocation";
    private const string FieldFieldName = "ClassificationFieldName";

    public IfcClassificationManager(ILogger<IfcClassificationManager> logger, SettingsManager settingsManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the full list of IFC classifications from the current bSDD settings.
    /// Includes main dictionary + all filter dictionaries.
    /// </summary>
    public List<RevitIfcClassification> GetAllIfcClassificationsFromSettings()
    {
        var settings = _settingsManager.GetSettings();
        var result = new List<RevitIfcClassification>();

        if (settings.MainDictionary?.IfcClassification != null)
            result.Add(ConvertToRevitIfcClassification(settings.MainDictionary.IfcClassification));

        if (settings.FilterDictionaries != null)
        {
            foreach (var dict in settings.FilterDictionaries)
            {
                if (dict.IfcClassification != null)
                    result.Add(ConvertToRevitIfcClassification(dict.IfcClassification));
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a map from classification location URL → Revit parameter mapping name.
    /// Used by the IFC export to write both the bsdd/class/ parameter AND the user-friendly mapped parameter.
    /// </summary>
    public Dictionary<string, string> GetClassificationParameterMap()
    {
        var map = new Dictionary<string, string>();
        var settings = _settingsManager.GetSettings();

        var allDicts = new List<BsddDictionary>();
        if (settings.MainDictionary != null) allDicts.Add(settings.MainDictionary);
        if (settings.FilterDictionaries != null) allDicts.AddRange(settings.FilterDictionaries);

        foreach (var dict in allDicts)
        {
            var location = dict.IfcClassification?.Location;
            if (!string.IsNullOrEmpty(location) && !string.IsNullOrEmpty(dict.ParameterMapping))
                map[location] = dict.ParameterMapping;
        }
        return map;
    }

    /// <summary>
    /// Returns classifications currently stored in the document's DataStorage.
    /// </summary>
    public List<RevitIfcClassification> GetStoredClassifications(Document document)
    {
        var schema = GetOrCreateSchema();
        return GetClassificationsInStorage(document, schema)
            .Select(ds => ReadClassification(ds, schema))
            .Where(c => c != null)
            .Cast<RevitIfcClassification>()
            .ToList();
    }

    /// <summary>
    /// Creates/updates document DataStorage to reflect the given classifications.
    /// Removes classifications not in the new list.
    /// Must be called inside a transaction.
    /// </summary>
    public void UpdateClassifications(
        Document document,
        List<RevitIfcClassification> classifications,
        bool isBsddExport,
        Dictionary<string, string>? bsddParameterMap = null)
    {
        var schema = GetOrCreateSchema();
        var stored = GetStoredClassifications(document);
        var newLocations = classifications
            .Where(c => c.Location != null)
            .Select(c => c.Location!)
            .ToHashSet();

        // Create or update
        foreach (var classification in classifications)
        {
            var existing = stored.FirstOrDefault(s => s.Location == classification.Location);

            if (existing == null)
            {
                // Create new
                var dataStorage = DataStorage.Create(document);
                dataStorage.SetEntity(BuildEntity(schema, classification, isBsddExport, bsddParameterMap));
                _logger.LogDebug("Created IFC classification: {Name} @ {Location}", classification.Name, classification.Location);
            }
            else
            {
                // Update if changed
                if (!classification.Equals(existing))
                {
                    var ds = GetClassificationDataStorage(document, existing);
                    if (ds != null)
                    {
                        ds.SetEntity(BuildEntity(schema, classification, isBsddExport, bsddParameterMap));
                        _logger.LogDebug("Updated IFC classification: {Name}", classification.Name);
                    }
                }
            }
        }

        // Remove stale classifications
        foreach (var old in stored)
        {
            if (old.Location != null && !newLocations.Contains(old.Location))
            {
                var ds = GetClassificationDataStorage(document, old);
                if (ds != null)
                {
                    document.Delete(ds.Id);
                    _logger.LogDebug("Removed stale IFC classification: {Name}", old.Name);
                }
            }
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private IList<DataStorage> GetClassificationsInStorage(Document doc, Schema schema)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>()
            .Where(ds =>
            {
                var e = ds.GetEntity(schema);
                return e != null && e.IsValid();
            })
            .Cast<DataStorage>()
            .ToList();
    }

    private DataStorage? GetClassificationDataStorage(Document doc, RevitIfcClassification classification)
    {
        var schema = GetOrCreateSchema();
        return GetClassificationsInStorage(doc, schema)
            .FirstOrDefault(ds => ReadClassification(ds, schema)?.Location == classification.Location);
    }

    private RevitIfcClassification? ReadClassification(DataStorage ds, Schema schema)
    {
        try
        {
            var e = ds.GetEntity(schema);
            if (!e.IsValid()) return null;

            return new RevitIfcClassification
            {
                Name = e.Get<string>(schema.GetField(FieldName)),
                Source = e.Get<string>(schema.GetField(FieldSource)),
                Edition = e.Get<string>(schema.GetField(FieldEdition)),
                Location = e.Get<string>(schema.GetField(FieldLocation)),
                FieldName = e.Get<string>(schema.GetField(FieldFieldName))
            };
        }
        catch
        {
            return null;
        }
    }

    private Entity BuildEntity(
        Schema schema,
        RevitIfcClassification classification,
        bool isBsddExport,
        Dictionary<string, string>? parameterMap)
    {
        var entity = new Entity(schema);
        entity.Set<string>(schema.GetField(FieldName), classification.Name ?? string.Empty);
        entity.Set<string>(schema.GetField(FieldSource), classification.Source ?? string.Empty);
        entity.Set<string>(schema.GetField(FieldEdition), classification.Edition ?? string.Empty);
        entity.Set<Int32>(schema.GetField(FieldEditionDate_Day), 1);
        entity.Set<Int32>(schema.GetField(FieldEditionDate_Month), 1);
        entity.Set<Int32>(schema.GetField(FieldEditionDate_Year), DateTime.Now.Year);
        entity.Set<string>(schema.GetField(FieldLocation), classification.Location ?? string.Empty);

        if (isBsddExport && !string.IsNullOrEmpty(classification.Location))
        {
            // Build the FieldName value: list of Revit parameters that carry classification data
            var fieldNames = new List<string>();
            if (Uri.TryCreate(classification.Location, UriKind.Absolute, out var uri))
            {
                var paramName = ParameterDataManagement.CreateParameterNameFromUri(uri);
                fieldNames.Add(paramName);
                fieldNames.Add(paramName + "[Instance]");
            }
            if (parameterMap?.TryGetValue(classification.Location, out var mapped) == true
                && !string.IsNullOrEmpty(mapped))
            {
                fieldNames.Add(mapped);
            }

            entity.Set<string>(schema.GetField(FieldFieldName), string.Join(",", fieldNames));
        }
        else
        {
            entity.Set<string>(schema.GetField(FieldFieldName), string.Empty);
        }

        return entity;
    }

    private static RevitIfcClassification ConvertToRevitIfcClassification(IfcClassification ifc)
        => new RevitIfcClassification
        {
            Name = ifc.Name,
            Source = ifc.Source,
            Edition = ifc.Edition,
            Location = ifc.Location,
        };

    private static Schema GetOrCreateSchema()
    {
        var existing = Schema.Lookup(ClassificationSchemaId);
        if (existing != null) return existing;

        var builder = new SchemaBuilder(ClassificationSchemaId);
        builder.SetSchemaName("IfcClassification");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField(FieldName, typeof(string));
        builder.AddSimpleField(FieldSource, typeof(string));
        builder.AddSimpleField(FieldEdition, typeof(string));
        builder.AddSimpleField(FieldEditionDate_Day, typeof(Int32));
        builder.AddSimpleField(FieldEditionDate_Month, typeof(Int32));
        builder.AddSimpleField(FieldEditionDate_Year, typeof(Int32));
        builder.AddSimpleField(FieldLocation, typeof(string));
        builder.AddSimpleField(FieldFieldName, typeof(string));
        return builder.Finish();
    }
}

/// <summary>
/// Simplified DTO for IFC Classification data as stored in Revit DataStorage.
/// </summary>
public class RevitIfcClassification
{
    public string? Name { get; set; }
    public string? Source { get; set; }
    public string? Edition { get; set; }
    public string? Location { get; set; }
    public string? FieldName { get; set; }

    public bool Equals(RevitIfcClassification other)
        => other != null
           && Name == other.Name
           && Source == other.Source
           && Edition == other.Edition
           && Location == other.Location
           && FieldName == other.FieldName;
}
