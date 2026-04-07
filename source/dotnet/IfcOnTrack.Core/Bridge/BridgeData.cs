// Purpose: Data models for bridge communication with the UI
using Newtonsoft.Json;

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
/// IFC entity representation for the UI.
/// </summary>
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
    public List<IfcClassificationReference>? HasAssociations { get; set; }
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
/// IFC Property representation.
/// </summary>
public class IfcProperty
{
    [JsonProperty("type")]
    public string Type { get; set; } = "IfcPropertySingleValue";

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("nominalValue")]
    public object? NominalValue { get; set; }
}

/// <summary>
/// IFC ClassificationReference for bSDD linking.
/// </summary>
public class IfcClassificationReference
{
    [JsonProperty("type")]
    public string Type { get; set; } = "IfcClassificationReference";

    [JsonProperty("location")]
    public string? Location { get; set; }

    [JsonProperty("identification")]
    public string? Identification { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("referencedSource")]
    public IfcClassification? ReferencedSource { get; set; }
}

/// <summary>
/// IFC Classification (bSDD dictionary).
/// </summary>
public class IfcClassification
{
    [JsonProperty("type")]
    public string Type { get; set; } = "IfcClassification";

    [JsonProperty("location")]
    public string? Location { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }
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
