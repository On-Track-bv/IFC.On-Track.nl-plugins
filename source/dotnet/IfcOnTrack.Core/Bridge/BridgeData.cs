// Purpose: Data models for bridge communication with the UI
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IfcOnTrack.Core.Bridge;

/// <summary>
/// Complete data package sent to/from the UI.
/// </summary>
public class BridgeData
{
    [JsonProperty("ifcData")]
    public List<IfcEntity> IfcData { get; set; } = new();

    [JsonProperty("settings")]
    public BridgeSettings? Settings { get; set; }

    [JsonProperty("propertyIsInstanceMap")]
    public Dictionary<string, bool>? PropertyIsInstanceMap { get; set; }
}

/// <summary>
/// IFC entity (IfcTypeProduct) representation for the UI.
/// </summary>
[JsonConverter(typeof(IfcEntityConverter))]
public class IfcEntity
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }

    [JsonProperty("objectType")]
    public string? ObjectType { get; set; }

    [JsonProperty("tag")]
    public string? Tag { get; set; }

    [JsonProperty("predefinedType")]
    public string? PredefinedType { get; set; }

    [JsonProperty("isDefinedBy")]
    public List<IfcPropertySet>? IsDefinedBy { get; set; }

    [JsonProperty("hasAssociations")]
    public List<Association>? HasAssociations { get; set; }
}

/// <summary>
/// Polymorphic JSON converter for IfcEntity – handles hasAssociations and isDefinedBy subtypes.
/// </summary>
public class IfcEntityConverter : JsonConverter<IfcEntity>
{
    public override IfcEntity ReadJson(JsonReader reader, Type objectType, IfcEntity? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        var entity = new IfcEntity
        {
            Type = obj["type"]?.Value<string>(),
            Name = obj["name"]?.Value<string>(),
            Description = obj["description"]?.Value<string>(),
            ObjectType = obj["objectType"]?.Value<string>(),
            Tag = obj["tag"]?.Value<string>() ?? obj["typeId"]?.Value<string>(),
            PredefinedType = obj["predefinedType"]?.Value<string>(),
        };

        if (obj["hasAssociations"] is JArray assocArray)
        {
            entity.HasAssociations = new List<Association>();
            foreach (var item in assocArray.OfType<JObject>())
            {
                var assocType = item["type"]?.Value<string>();
                Association? assoc = assocType switch
                {
                    "IfcClassificationReference" => item.ToObject<IfcClassificationReference>(serializer),
                    "IfcMaterial" => item.ToObject<IfcMaterial>(serializer),
                    _ => null
                };
                if (assoc != null) entity.HasAssociations.Add(assoc);
            }
        }

        if (obj["isDefinedBy"] is JArray psetArray)
        {
            entity.IsDefinedBy = new List<IfcPropertySet>();
            foreach (var psetItem in psetArray.OfType<JObject>())
            {
                var pset = new IfcPropertySet
                {
                    Type = psetItem["type"]?.Value<string>() ?? "IfcPropertySet",
                    Name = psetItem["name"]?.Value<string>(),
                };
                if (psetItem["hasProperties"] is JArray propsArray)
                {
                    pset.HasProperties = new List<IfcProperty>();
                    foreach (var propItem in propsArray.OfType<JObject>())
                    {
                        var propType = propItem["type"]?.Value<string>();
                        IfcProperty? prop = propType switch
                        {
                            "IfcPropertySingleValue" => propItem.ToObject<IfcPropertySingleValue>(serializer),
                            "IfcPropertyEnumeratedValue" => propItem.ToObject<IfcPropertyEnumeratedValue>(serializer),
                            _ => null
                        };
                        if (prop != null) pset.HasProperties.Add(prop);
                    }
                }
                entity.IsDefinedBy.Add(pset);
            }
        }

        return entity;
    }

    public override void WriteJson(JsonWriter writer, IfcEntity? value, JsonSerializer serializer)
    {
        if (value == null) { writer.WriteNull(); return; }

        // Build the JObject manually to avoid the class-level [JsonConverter] triggering
        // recursion when FromObject is called with a serializer that still sees the attribute.
        var obj = new JObject();
        if (value.Type != null)          obj["type"]          = value.Type;
        if (value.Name != null)          obj["name"]          = value.Name;
        if (value.Description != null)   obj["description"]   = value.Description;
        if (value.ObjectType != null)    obj["objectType"]    = value.ObjectType;
        if (value.Tag != null)           obj["tag"]           = value.Tag;
        if (value.PredefinedType != null) obj["predefinedType"] = value.PredefinedType;

        if (value.IsDefinedBy != null)
            obj["isDefinedBy"] = JArray.FromObject(value.IsDefinedBy, serializer);

        if (value.HasAssociations != null)
            obj["hasAssociations"] = JArray.FromObject(value.HasAssociations, serializer);

        obj.WriteTo(writer);
    }
}

/// <summary>
/// Base class for hasAssociations items.
/// </summary>
public class Association
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }
}

/// <summary>
/// IFC ClassificationReference for bSDD linking.
/// Uses string Location for JSON serialization; parse with Uri.TryCreate() when needed.
/// </summary>
public class IfcClassificationReference : Association
{
    [JsonProperty("location")]
    public string? Location { get; set; }

    [JsonProperty("identification")]
    public string? Identification { get; set; }

    [JsonProperty("referencedSource")]
    public IfcClassification? ReferencedSource { get; set; }

    public IfcClassificationReference() { Type = "IfcClassificationReference"; }
}

/// <summary>
/// IFC Material for bSDD linking.
/// </summary>
public class IfcMaterial : Association
{
    [JsonProperty("description")]
    public string? Description { get; set; }

    public IfcMaterial() { Type = "IfcMaterial"; }
}

/// <summary>
/// IFC Classification (bSDD dictionary).
/// Uses string Location for JSON serialization; parse with Uri.TryCreate() when needed.
/// </summary>
public class IfcClassification
{
    [JsonProperty("type")]
    public string Type { get; set; } = "IfcClassification";

    [JsonProperty("source")]
    public string? Source { get; set; }

    [JsonProperty("edition")]
    public string? Edition { get; set; }

    [JsonProperty("editionDate")]
    public DateTime? EditionDate { get; set; }

    [JsonProperty("location")]
    public string? Location { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("description")]
    public string? Description { get; set; }
}

/// <summary>
/// IFC PropertySet representation.
/// </summary>
public class IfcPropertySet
{
    [JsonProperty("type")]
    public string Type { get; set; } = "IfcPropertySet";

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("hasProperties")]
    public List<IfcProperty>? HasProperties { get; set; }
}

/// <summary>
/// IFC Property base class.
/// </summary>
public class IfcProperty
{
    [JsonProperty("type")]
    public virtual string? Type { get; set; } = "IfcProperty";

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("specification")]
    public string? Specification { get; set; }
}

/// <summary>
/// IFC PropertySingleValue.
/// </summary>
public class IfcPropertySingleValue : IfcProperty
{
    [JsonProperty("type")]
    public override string? Type { get; set; } = "IfcPropertySingleValue";

    [JsonProperty("nominalValue")]
    public IfcValue? NominalValue { get; set; }
}

/// <summary>
/// IFC PropertyEnumeratedValue.
/// </summary>
public class IfcPropertyEnumeratedValue : IfcProperty
{
    [JsonProperty("type")]
    public override string? Type { get; set; } = "IfcPropertyEnumeratedValue";

    [JsonProperty("enumerationValues")]
    public List<IfcValue>? EnumerationValues { get; set; }

    [JsonProperty("enumerationReference")]
    public IfcPropertyEnumeration? EnumerationReference { get; set; }
}

/// <summary>
/// IFC property value with typed value.
/// </summary>
public class IfcValue
{
    [JsonProperty("type")]
    public string? Type { get; set; }

    [JsonProperty("value")]
    public object? Value { get; set; }
}

/// <summary>
/// IFC PropertyEnumeration.
/// </summary>
public class IfcPropertyEnumeration
{
    [JsonProperty("type")]
    public string Type { get; set; } = "IfcPropertyEnumeration";

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("enumerationValues")]
    public List<IfcValue>? EnumerationValues { get; set; }
}

/// <summary>
/// UI Settings.
/// </summary>
public class BridgeSettings
{
    [JsonProperty("bsddApiEnvironment")]
    public string BsddApiEnvironment { get; set; } = "production";

    [JsonProperty("mainDictionary")]
    public BsddDictionary? MainDictionary { get; set; }

    [JsonProperty("ifcDictionary")]
    public BsddDictionary? IfcDictionary { get; set; }

    [JsonProperty("filterDictionaries")]
    public List<BsddDictionary>? FilterDictionaries { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; } = "nl-NL";

    [JsonProperty("includeTestDictionaries")]
    public bool IncludeTestDictionaries { get; set; }

    [JsonProperty("displayScale")]
    public double? DisplayScale { get; set; }
}

/// <summary>
/// bSDD Dictionary reference.
/// </summary>
public class BsddDictionary
{
    [JsonProperty("ifcClassification")]
    public IfcClassification? IfcClassification { get; set; }

    [JsonProperty("parameterMapping")]
    public string? ParameterMapping { get; set; }
}
