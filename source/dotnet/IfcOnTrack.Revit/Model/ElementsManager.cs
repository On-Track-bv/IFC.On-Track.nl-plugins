// Purpose: Manages Revit elements and converts them to/from IFC JSON (bSDD workflow)
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Revit.Utilities;

namespace IfcOnTrack.Revit.Model;

/// <summary>
/// Manages reading and writing bSDD data to/from Revit elements.
///
/// Data storage:
///   - IfcEntity associations (classifications) are stored in Revit ExtensibleStorage on the ElementType.
///   - Classifications and properties are additionally stored as Revit project parameters
///     so they can be exported via IFC.
///
/// Uses a unique schema GUID to avoid conflicts with the legacy bSDD-Revit-plugin.
/// </summary>
public class ElementsManager
{
    private readonly ILogger<ElementsManager> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly ParameterDataManagement _paramDataManagement;
    private readonly ParametersManager _parametersManager;

    // Schema GUID for IFC associations storage (unique for IFC.On-Track.nl plugin)
    // Different from old bSDD-Revit-plugin to avoid conflicts
    private static readonly Guid SchemaGuid = new("B7C4D8A2-5E3F-4A1B-9D6C-2F8E7A4B3C1D");
    private const string SchemaFieldName = "IfcClassificationData";

    public ElementsManager(
        ILogger<ElementsManager> logger,
        SettingsManager settingsManager,
        ParameterDataManagement paramDataManagement,
        ParametersManager parametersManager)
    {
        _logger = logger;
        _settingsManager = settingsManager;
        _paramDataManagement = paramDataManagement;
        _parametersManager = parametersManager;
    }

    // ─── Read: Revit → IFC JSON ───────────────────────────────────────────────

    /// <summary>
    /// Gets a map of parameter names → isInstance for all project parameters.
    /// Used to populate PropertyIsInstanceMap for the bSDD JS bridge.
    /// </summary>
    public Dictionary<string, bool> GetProjectParameterTypes(Document doc)
        => _paramDataManagement.GetProjectParameterTypes(doc);

    /// <summary>
    /// Get all ElementTypes in the document as IfcEntities.
    /// Includes Rooms and Area's as special entities.
    /// </summary>
    public List<IfcEntity> GetAllElementTypesAsIfcJson(Document doc)
    {
        var elementTypes = new FilteredElementCollector(doc)
            .OfClass(typeof(ElementType))
            .Cast<ElementType>()
            .ToList();

        return BuildIfcEntityList(doc, elementTypes);
    }

    /// <summary>
    /// Get ElementTypes whose instances are visible in the given view.
    /// </summary>
    public List<IfcEntity> GetViewElementTypesAsIfcJson(Document doc, View view)
    {
        var typeIds = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType()
            .Select(e => e.GetTypeId())
            .Where(id => id != ElementId.InvalidElementId)
            .Distinct()
            .ToList();

        var elementTypes = typeIds
            .Select(id => doc.GetElement(id) as ElementType)
            .Where(t => t != null)
            .Cast<ElementType>()
            .ToList();

        return BuildIfcEntityList(doc, elementTypes);
    }

    /// <summary>
    /// Get element types from a set of selected element IDs.
    /// </summary>
    public List<IfcEntity> GetSelectedElementsAsIfcJson(Document doc, ICollection<ElementId> selectedIds)
    {
        var typeIds = selectedIds
            .Select(id => doc.GetElement(id))
            .Where(e => e != null)
            .Select(e => e!.GetTypeId())
            .Where(id => id != ElementId.InvalidElementId)
            .Distinct()
            .ToList();

        var elementTypes = typeIds
            .Select(id => doc.GetElement(id) as ElementType)
            .Where(t => t != null)
            .Cast<ElementType>()
            .ToList();

        return BuildIfcEntityList(doc, elementTypes);
    }

    private List<IfcEntity> BuildIfcEntityList(Document doc, List<ElementType> elementTypes)
    {
        var entities = elementTypes.Select(et => CreateIfcEntity(et, doc)).ToList();

        // Add special Room and Area entities (matching bSDD-Revit-plugin behaviour)
        entities.Add(new IfcEntity
        {
            Name = ParameterDataManagement.RoomName,
            Type = "Rooms & Area's",
            Description = "Rooms & Area's",
            ObjectType = "Rooms & Area's",
            PredefinedType = "Rooms & Area's"
        });
        entities.Add(new IfcEntity
        {
            Name = ParameterDataManagement.AreaName,
            Type = "Rooms & Area's",
            Description = "Rooms & Area's",
            ObjectType = "Rooms & Area's",
            PredefinedType = "Rooms & Area's"
        });

        return entities;
    }

    // ─── Write: IFC JSON → Revit ──────────────────────────────────────────────

    /// <summary>
    /// Apply bridge data (from UI save) to Revit elements.
    /// Creates project parameters and writes values. Manages its own transactions internally.
    /// </summary>
    public void ApplyBridgeData(Document doc, BridgeData bridgeData)
    {
        if (bridgeData.IfcData == null) return;

        var propertyIsInstanceMap = bridgeData.PropertyIsInstanceMap ?? new Dictionary<string, bool>();
        var groupType = GroupTypeId.Ifc;

        foreach (var entity in bridgeData.IfcData)
        {
            try
            {
                ApplyIfcEntity(doc, entity, propertyIsInstanceMap, groupType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply entity '{Name}'", entity.Name);
            }
        }
    }

    private void ApplyIfcEntity(
        Document doc,
        IfcEntity entity,
        Dictionary<string, bool> propertyIsInstanceMap,
        ForgeTypeId groupType)
    {
        var dictionaryCollection = new HashSet<IfcClassification>();

        _paramDataManagement.GetParametersToCreateAndSet(
            doc, entity, dictionaryCollection, propertyIsInstanceMap,
            out var parametersToCreate, out var parametersToSet);

        bool isRoomOrArea = entity.Name == ParameterDataManagement.RoomName ||
                            entity.Name == ParameterDataManagement.AreaName;

        if (isRoomOrArea)
        {
            var builtInCat = entity.Name == ParameterDataManagement.AreaName
                ? BuiltInCategory.OST_Areas
                : BuiltInCategory.OST_Rooms;

            var categoryList = new List<Category> { doc.Settings.Categories.get_Item(builtInCat) };

            using (var createTx = new Transaction(doc, "Create bSDD parameters"))
            {
                createTx.Start();
                _parametersManager.CreateProjectParameters(doc, parametersToCreate, "bSDD", groupType, categoryList);
                createTx.Commit();
            }

            if (parametersToSet.Any())
            {
                var instances = new FilteredElementCollector(doc)
                    .OfCategory(builtInCat)
                    .WhereElementIsNotElementType()
                    .ToElements();
                using var setTx = new Transaction(doc, "Set bSDD parameter values");
                setTx.Start();
                _parametersManager.SetElementsParameters(instances, parametersToSet);
                setTx.Commit();
            }
        }
        else
        {
            if (!long.TryParse(entity.Tag, out var idLong)) return;
            var elementId = new ElementId(idLong);
            var elementType = doc.GetElement(elementId) as ElementType;
            if (elementType == null) return;

            // 1. Store associations in ExtensibleStorage
            using (var storageTx = new Transaction(doc, "Set bSDD entity storage"))
            {
                storageTx.Start();
                SaveAssociationsToStorage(elementType, entity.HasAssociations);
                storageTx.Commit();
            }

            // 2. Create parameters
            using (var createTx = new Transaction(doc, "Create bSDD parameters"))
            {
                createTx.Start();
                var categoryList = elementType.Category != null
                    ? new List<Category> { elementType.Category }
                    : null;
                _parametersManager.CreateProjectParameters(doc, parametersToCreate, "bSDD", groupType, categoryList);
                createTx.Commit();
            }

            // 3. Handle instance parameter settings
            if (parametersToCreate.Any(p => p.IsInstance))
            {
                using var instanceTx = new Transaction(doc, "Update instance parameters");
                instanceTx.Start();
                _parametersManager.SetInstanceParameterVaryBetweenGroups(doc, parametersToCreate, true);
                instanceTx.Commit();
            }

            // 4. Set values on the element type (instance parameter values are not yet supported)
            using (var setTx = new Transaction(doc, "Set bSDD parameter values"))
            {
                setTx.Start();
                _parametersManager.SetElementTypeParameters(elementType, parametersToSet);
                setTx.Commit();
            }
        }
    }

    // ─── Select in model ──────────────────────────────────────────────────────

    /// <summary>
    /// Highlight all instances of the element types described by the given entities.
    /// </summary>
    public void SelectElementsInModel(UIApplication uiApp, List<IfcEntity> ifcEntities)
    {
        var uiDoc = uiApp.ActiveUIDocument;
        var doc = uiDoc?.Document;
        if (doc == null || uiDoc == null) return;

        var allIds = new List<ElementId>();

        foreach (var entity in ifcEntities)
        {
            if (entity.Name == ParameterDataManagement.AreaName)
            {
                var areas = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Areas)
                    .WhereElementIsNotElementType()
                    .Select(e => e.Id);
                allIds.AddRange(areas);
            }
            else if (entity.Name == ParameterDataManagement.RoomName)
            {
                var rooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Select(e => e.Id);
                allIds.AddRange(rooms);
            }
            else if (!string.IsNullOrEmpty(entity.Tag) && long.TryParse(entity.Tag, out var idLong))
            {
                var typeId = new ElementId(idLong);
                var instances = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType()
                    .Where(e => e.GetTypeId() == typeId)
                    .Select(e => e.Id);
                allIds.AddRange(instances);
            }
        }

        if (allIds.Count > 0)
        {
            try
            {
                uiDoc.Selection.SetElementIds(allIds);
                _logger.LogInformation("Selected {Count} instances", allIds.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not set selection");
            }
        }
    }

    // ─── ExtensibleStorage ────────────────────────────────────────────────────

    private void SaveAssociationsToStorage(ElementType elementType, List<Association>? associations)
    {
        try
        {
            var schema = GetOrCreateSchema();
            var field = schema.GetField(SchemaFieldName);
            var entity = new Entity(schema);
            // Serialize only IfcClassificationReference items — LoadAssociationsFromStorage
            // deserializes as List<IfcClassificationReference>, so other subtypes would be lost anyway.
            var classRefs = associations?.OfType<IfcClassificationReference>().ToList()
                ?? new List<IfcClassificationReference>();
            entity.Set(field, JsonConvert.SerializeObject(classRefs));
            elementType.SetEntity(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save associations to storage for '{Name}'", elementType.Name);
        }
    }

    private List<IfcClassificationReference> LoadAssociationsFromStorage(ElementType elementType)
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema == null) return new();

        var storageEntity = elementType.GetEntity(schema);
        if (storageEntity.Schema == null) return new();

        var json = storageEntity.Get<string>(schema.GetField(SchemaFieldName));
        if (string.IsNullOrEmpty(json)) return new();

        try
        {
            return JsonConvert.DeserializeObject<List<IfcClassificationReference>>(json)
                ?? new List<IfcClassificationReference>();
        }
        catch
        {
            return new();
        }
    }

    private static Schema GetOrCreateSchema()
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema != null) return schema;

        var builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName("IfcOnTrackAssociations_v2");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField(SchemaFieldName, typeof(string));
        return builder.Finish();
    }

    // ─── IFC entity creation ──────────────────────────────────────────────────

    private IfcEntity CreateIfcEntity(ElementType elementType, Document doc)
    {
        var entity = new IfcEntity
        {
            Type = GetIfcType(doc, elementType),
            Name = GetFamilyTypeName(elementType),
            Tag = elementType.Id.ToString(),
            Description = GetTypeParamValue(elementType, "Description"),
            PredefinedType = elementType.get_Parameter(BuiltInParameter.IFC_EXPORT_PREDEFINEDTYPE_TYPE)?.AsString(),
            ObjectType = GetTypeParamValue(elementType, "IfcObjectType[Type]"),
        };

        // Load stored classification references from ExtensibleStorage
        var storedAssociations = LoadAssociationsFromStorage(elementType);

        // Also load from active settings (project parameters)
        var settingsAssociations = GetAssociationsFromSettings(elementType);

        // Merge: stored associations take precedence; settings associations fill in gaps
        var merged = MergeAssociations(storedAssociations, settingsAssociations);
        if (merged.Any())
            entity.HasAssociations = merged.Cast<Association>().ToList();

        // Load property definitions from bsdd/prop/ parameters
        var propertySets = GetPropertySetsFromParameters(elementType);
        if (propertySets.Any())
            entity.IsDefinedBy = propertySets;

        return entity;
    }

    private List<IfcClassificationReference> MergeAssociations(
        List<IfcClassificationReference> stored,
        Dictionary<string, (string? Identification, string? Name)> fromSettings)
    {
        // GroupBy first to handle items with null/empty Location without throwing on duplicate keys.
        var result = stored
            .GroupBy(r => r.ReferencedSource?.Location ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var kvp in fromSettings)
        {
            var location = kvp.Key;
            var identification = kvp.Value.Identification;
            var name = kvp.Value.Name;
            if (result.TryGetValue(location, out var existing))
            {
                existing.Identification = identification;
                existing.Name = name;
            }
            else if (!string.IsNullOrEmpty(identification) || !string.IsNullOrEmpty(name))
            {
                result[location] = new IfcClassificationReference
                {
                    Type = "IfcClassificationReference",
                    Identification = identification,
                    Name = name,
                    ReferencedSource = new IfcClassification
                    {
                        Type = "IfcClassification",
                        Location = location
                    }
                };
            }
        }

        return result.Values.ToList();
    }

    /// <summary>
    /// Gets classification data from the currently active dictionaries' mapping parameters.
    /// Returns a dictionary keyed by dictionary location URL.
    /// </summary>
    private Dictionary<string, (string? Identification, string? Name)> GetAssociationsFromSettings(ElementType elementType)
    {
        var result = new Dictionary<string, (string?, string?)>();
        var settings = _settingsManager.GetSettings();

        var activeDicts = new List<BsddDictionary>();
        if (settings.MainDictionary != null) activeDicts.Add(settings.MainDictionary);
        if (settings.FilterDictionaries != null) activeDicts.AddRange(settings.FilterDictionaries);

        foreach (var dict in activeDicts)
        {
            var locationStr = dict.IfcClassification?.Location;
            if (string.IsNullOrEmpty(locationStr)) continue;
            if (!Uri.TryCreate(locationStr, UriKind.Absolute, out var uri)) continue;

            string? identification = null, name = null;

            // bsdd/class/ parameter
            var bsddParamName = ParameterDataManagement.CreateParameterNameFromUri(uri);
            var bsddVal = GetTypeParamValue(elementType, bsddParamName);
            if (!string.IsNullOrEmpty(bsddVal))
            {
                var parts = bsddVal.Split(':');
                identification = parts[0];
                name = parts.Length > 1 ? parts[1] : parts[0];
            }

            // Mapped parameter (e.g. NL-SfB)
            if (!string.IsNullOrEmpty(dict.ParameterMapping))
            {
                var mappedVal = GetTypeParamValue(elementType, dict.ParameterMapping);
                if (!string.IsNullOrEmpty(mappedVal))
                {
                    var parts = mappedVal.Split(':');
                    identification ??= parts[0];
                    if (string.IsNullOrEmpty(name))
                        name = parts.Length > 1 ? parts[1] : parts[0];
                }
            }

            if (!string.IsNullOrEmpty(identification) || !string.IsNullOrEmpty(name))
                result[locationStr] = (identification, name);
        }

        return result;
    }

    private List<IfcPropertySet> GetPropertySetsFromParameters(ElementType elementType)
    {
        var psetMap = new Dictionary<string, List<IfcProperty>>();

        foreach (Parameter param in elementType.Parameters)
        {
            var paramName = param.Definition?.Name;
            if (string.IsNullOrEmpty(paramName)) continue;

            bool isBsddProp = paramName.StartsWith("bsdd/prop/", StringComparison.Ordinal);
            bool isIfcParam = !isBsddProp && IfcParameterMappings.IfcParameters.Contains(paramName, StringComparer.Ordinal);

            if (!isBsddProp && !isIfcParam) continue;
            if (!param.HasValue) continue;
            if (paramName == "Category" && param.IsReadOnly) continue;

            string psetName, propName;
            if (isBsddProp)
            {
                // bsdd/prop/{psetName}/{propName}
                var parts = paramName.Split('/');
                if (parts.Length < 4) continue;
                psetName = parts[2];
                propName = string.Join("/", parts.Skip(3));
            }
            else
            {
                // IFC standard property – group under empty pset name
                psetName = "";
                propName = paramName;
            }

            if (!psetMap.ContainsKey(psetName))
                psetMap[psetName] = new List<IfcProperty>();

            object? value = param.StorageType switch
            {
                StorageType.String => param.AsString(),
                StorageType.Integer => (object)param.AsInteger(),
                StorageType.Double => param.AsDouble(),
                _ => null
            };

            psetMap[psetName].Add(new IfcPropertySingleValue
            {
                Name = propName,
                NominalValue = value != null ? new IfcValue { Type = "IfcLabel", Value = value } : null
            });
        }

        return psetMap
            .Select(kvp => new IfcPropertySet { Name = kvp.Key, HasProperties = kvp.Value })
            .ToList();
    }

    // ─── Read helpers ─────────────────────────────────────────────────────────

    private static string GetFamilyTypeName(ElementType elementType)
    {
        var familyName = elementType.FamilyName;
        var typeName = elementType.Name;
        return string.IsNullOrEmpty(familyName) ? typeName : $"{familyName} - {typeName}";
    }

    private static string? GetTypeParamValue(ElementType elementType, string paramName)
        => elementType.LookupParameter(paramName)?.AsString();

    private static string GetIfcType(Document doc, ElementType elementType)
    {
        // Check for explicit IFC export type parameter first
        var ifcTypeParm = elementType.get_Parameter(BuiltInParameter.IFC_EXPORT_ELEMENT_TYPE_AS)?.AsString();
        if (!string.IsNullOrEmpty(ifcTypeParm)) return ifcTypeParm;

        // Fallback: map Revit category to IFC type
        return GetIfcTypeFromCategory(elementType.Category?.Name);
    }

    private static string GetIfcTypeFromCategory(string? categoryName)
    {
        return categoryName switch
        {
            "Walls" => "IfcWallType",
            "Floors" => "IfcSlabType",
            "Roofs" => "IfcRoofType",
            "Doors" => "IfcDoorType",
            "Windows" => "IfcWindowType",
            "Columns" => "IfcColumnType",
            "Beams" or "Structural Framing" => "IfcBeamType",
            "Ceilings" => "IfcCoveringType",
            "Stairs" => "IfcStairType",
            "Ramps" => "IfcRampType",
            "Furniture" => "IfcFurnitureType",
            "Mechanical Equipment" => "IfcEquipmentType",
            "Plumbing Fixtures" => "IfcSanitaryTerminalType",
            "Lighting Fixtures" => "IfcLightFixtureType",
            "Electrical Equipment" => "IfcElectricDistributionBoardType",
            "Specialty Equipment" => "IfcEquipmentType",
            "Curtain Panels" => "IfcCurtainWallType",
            "Site" => "IfcSite",
            _ => "IfcBuildingElementType"
        };
    }
}
