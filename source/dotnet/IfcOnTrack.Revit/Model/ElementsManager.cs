// Purpose: Manages Revit elements and converts them to/from IFC JSON
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nice3point.Revit.Extensions;
using IfcOnTrack.Core.Bridge;

namespace IfcOnTrack.Revit.Model;

/// <summary>
/// Manages reading and writing bSDD data to Revit elements.
/// Uses Nice3point extensions for cleaner API access.
/// </summary>
public class ElementsManager
{
    private readonly ILogger<ElementsManager> _logger;
    
    // Schema GUID for storing IfcEntity data on elements
    private static readonly Guid SchemaGuid = new("79717CB2-D47B-4EC0-8E74-83A43E7D9F0A");

    public ElementsManager(ILogger<ElementsManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all ElementTypes in the document as IFC JSON.
    /// </summary>
    public List<IfcEntity> GetAllElementTypesAsIfcJson(Document doc)
    {
        var elementTypes = doc.GetTypes().OfType<ElementType>().ToList();
        return ConvertToIfcEntities(doc, elementTypes);
    }

    /// <summary>
    /// Get ElementTypes visible in the active view as IFC JSON.
    /// </summary>
    public List<IfcEntity> GetViewElementTypesAsIfcJson(Document doc, View view)
    {
        var collector = new FilteredElementCollector(doc, view.Id)
            .WhereElementIsNotElementType();
        
        var typeIds = collector
            .Select(e => e.GetTypeId())
            .Where(id => id != ElementId.InvalidElementId)
            .Distinct()
            .ToList();

        var elementTypes = typeIds
            .Select(id => doc.GetElement(id) as ElementType)
            .Where(t => t != null)
            .Cast<ElementType>()
            .ToList();

        return ConvertToIfcEntities(doc, elementTypes);
    }

    /// <summary>
    /// Get selected elements as IFC JSON (gets their types).
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

        return ConvertToIfcEntities(doc, elementTypes);
    }

    /// <summary>
    /// Select elements in the model that match the given IFC entities.
    /// </summary>
    public void SelectElementsInModel(UIApplication uiApp, List<IfcEntity> ifcEntities)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null) return;

        var elementIds = new List<ElementId>();
        
        foreach (var entity in ifcEntities)
        {
            if (string.IsNullOrEmpty(entity.Tag)) continue;
            
            if (long.TryParse(entity.Tag, out var idLong))
            {
                var elementId = new ElementId(idLong);
                if (doc.GetElement(elementId) != null)
                {
                    elementIds.Add(elementId);
                }
            }
        }

        if (elementIds.Count > 0)
        {
            uiApp.ActiveUIDocument?.Selection.SetElementIds(elementIds);
            _logger.LogInformation("Selected {Count} elements", elementIds.Count);
        }
    }

    /// <summary>
    /// Apply bSDD data to Revit elements.
    /// </summary>
    public void ApplyBridgeData(Document doc, BridgeData bridgeData)
    {
        if (bridgeData.IfcData == null) return;

        foreach (var entity in bridgeData.IfcData)
        {
            if (string.IsNullOrEmpty(entity.Tag)) continue;
            
            if (long.TryParse(entity.Tag, out var idLong))
            {
                var elementId = new ElementId(idLong);
                var element = doc.GetElement(elementId);
                
                if (element is ElementType elementType)
                {
                    ApplyEntityToElement(doc, elementType, entity, bridgeData.PropertyIsInstanceMap);
                }
            }
        }
    }

    private void ApplyEntityToElement(
        Document doc, 
        ElementType elementType, 
        IfcEntity entity,
        Dictionary<string, bool>? propertyIsInstanceMap)
    {
        try
        {
            // Store the full entity in extensible storage
            SaveEntityToStorage(elementType, entity);

            // Apply classifications as parameters
            if (entity.HasAssociations != null)
            {
                foreach (var classification in entity.HasAssociations)
                {
                    ApplyClassification(doc, elementType, classification);
                }
            }

            // Apply properties as parameters
            if (entity.IsDefinedBy != null)
            {
                foreach (var propertySet in entity.IsDefinedBy)
                {
                    ApplyPropertySet(doc, elementType, propertySet, propertyIsInstanceMap);
                }
            }

            _logger.LogDebug("Applied bSDD data to element {Name}", elementType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply entity to element {Name}", elementType.Name);
        }
    }

    private void SaveEntityToStorage(ElementType elementType, IfcEntity entity)
    {
        var schema = GetOrCreateSchema();
        var json = JsonConvert.SerializeObject(entity);
        
        // Use Nice3point extension for schema storage
        elementType.SaveEntity(schema, json, "IfcEntityJson");
    }

    private IfcEntity? LoadEntityFromStorage(ElementType elementType)
    {
        var schema = GetSchema();
        if (schema == null) return null;

        var json = elementType.LoadEntity<string>(schema, "IfcEntityJson");
        if (string.IsNullOrEmpty(json)) return null;

        return JsonConvert.DeserializeObject<IfcEntity>(json);
    }

    private Schema? GetSchema()
    {
        return Schema.Lookup(SchemaGuid);
    }

    private Schema GetOrCreateSchema()
    {
        var schema = Schema.Lookup(SchemaGuid);
        if (schema != null) return schema;

        var builder = new SchemaBuilder(SchemaGuid);
        builder.SetSchemaName("IfcOnTrackBsddData");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField("IfcEntityJson", typeof(string));
        return builder.Finish();
    }

    private void ApplyClassification(Document doc, ElementType elementType, IfcClassificationReference classification)
    {
        // Create parameter for classification if it doesn't exist
        var paramName = classification.ReferencedSource?.Name ?? "bSDD Classification";
        var param = elementType.LookupParameter(paramName);
        
        if (param != null && !param.IsReadOnly)
        {
            param.Set(classification.Name ?? "");
        }
    }

    private void ApplyPropertySet(
        Document doc, 
        ElementType elementType, 
        IfcPropertySet propertySet,
        Dictionary<string, bool>? propertyIsInstanceMap)
    {
        if (propertySet.HasProperties == null) return;

        foreach (var property in propertySet.HasProperties)
        {
            var param = elementType.LookupParameter(property.Name ?? "");
            if (param != null && !param.IsReadOnly && property.NominalValue != null)
            {
                SetParameterValue(param, property.NominalValue);
            }
        }
    }

    private void SetParameterValue(Parameter param, object value)
    {
        switch (param.StorageType)
        {
            case StorageType.String:
                param.Set(value.ToString() ?? "");
                break;
            case StorageType.Integer:
                if (int.TryParse(value.ToString(), out var intVal))
                    param.Set(intVal);
                break;
            case StorageType.Double:
                if (double.TryParse(value.ToString(), out var doubleVal))
                    param.Set(doubleVal);
                break;
        }
    }

    private List<IfcEntity> ConvertToIfcEntities(Document doc, List<ElementType> elementTypes)
    {
        var entities = new List<IfcEntity>();

        foreach (var elementType in elementTypes)
        {
            // Try to load existing entity from storage first
            var existingEntity = LoadEntityFromStorage(elementType);
            if (existingEntity != null)
            {
                existingEntity.Tag = elementType.Id.ToString();
                entities.Add(existingEntity);
                continue;
            }

            // Create new entity from element type
            var entity = new IfcEntity
            {
                Type = GetIfcType(elementType),
                Name = GetFamilyTypeName(elementType),
                Description = elementType.LookupParameter("Description")?.AsString(),
                ObjectType = elementType.Category?.Name,
                Tag = elementType.Id.ToString(),
                PredefinedType = GetPredefinedType(elementType)
            };

            entities.Add(entity);
        }

        _logger.LogDebug("Converted {Count} element types to IFC entities", entities.Count);
        return entities;
    }

    private static string GetFamilyTypeName(ElementType elementType)
    {
        var familyName = elementType.FamilyName;
        var typeName = elementType.Name;
        
        return string.IsNullOrEmpty(familyName) 
            ? typeName 
            : $"{familyName} - {typeName}";
    }

    private static string GetIfcType(ElementType elementType)
    {
        var categoryName = elementType.Category?.Name ?? "Unknown";
        
        return categoryName switch
        {
            "Walls" => "IfcWallType",
            "Doors" => "IfcDoorType",
            "Windows" => "IfcWindowType",
            "Floors" => "IfcSlabType",
            "Roofs" => "IfcRoofType",
            "Ceilings" => "IfcCoveringType",
            "Columns" => "IfcColumnType",
            "Structural Framing" => "IfcBeamType",
            "Structural Columns" => "IfcColumnType",
            "Structural Foundations" => "IfcFootingType",
            "Furniture" => "IfcFurnitureType",
            "Plumbing Fixtures" => "IfcSanitaryTerminalType",
            "Mechanical Equipment" => "IfcDistributionElementType",
            "Electrical Equipment" => "IfcDistributionElementType",
            "Generic Models" => "IfcBuildingElementProxyType",
            _ => "IfcBuildingElementType"
        };
    }

    private static string? GetPredefinedType(ElementType elementType)
    {
        // Try to get IFC export predefined type parameter
        var param = elementType.LookupParameter("IfcExportAs");
        return param?.AsString();
    }
}
