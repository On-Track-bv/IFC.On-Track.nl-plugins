// Purpose: Translates IfcEntity data into Revit parameters to create and set
using Autodesk.Revit.DB;
using IfcOnTrack.Core.Bridge;
using IfcOnTrack.Revit.Utilities;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.Model;

/// <summary>
/// Translates an IfcEntity (from the bSDD UI) into sets of Revit project parameters
/// to create and values to set on element types.
///
/// Naming conventions (matching bSDD-Revit-plugin):
///   Classifications → "bsdd/class/{host}{path}" (type parameter)
///   Properties      → "bsdd/prop/{propertySet}/{property}" (type or instance)
///   Attributes      → "Export Type to IFC As", "Type IFC Predefined Type", etc.
/// </summary>
public class ParameterDataManagement
{
    private readonly ILogger<ParameterDataManagement> _logger;
    private readonly ParametersManager _parametersManager;
    private readonly SettingsManager _settingsManager;

    public const string RoomName = "All Rooms";
    public const string AreaName = "All Area's";

    public ParameterDataManagement(
        ILogger<ParameterDataManagement> logger,
        ParametersManager parametersManager,
        SettingsManager settingsManager)
    {
        _logger = logger;
        _parametersManager = parametersManager;
        _settingsManager = settingsManager;
    }

    /// <summary>
    /// Produces the lists of parameters to create and the dictionary of parameter values to set
    /// for a given IfcEntity + propertyIsInstanceMap coming from the bridge.
    /// </summary>
    public void GetParametersToCreateAndSet(
        Document doc,
        IfcEntity ifcEntity,
        HashSet<IfcClassification> dictionaryCollection,
        Dictionary<string, bool> propertyIsInstanceMap,
        out List<ParameterCreation> parametersToCreate,
        out Dictionary<string, object?> parametersToSet)
    {
        var specType = SpecTypeId.String.Text;

        CollectFromAssociations(doc, ifcEntity, dictionaryCollection, ifcEntity.HasAssociations,
            specType, out var classParams, out var classValues);

        CollectFromIsDefinedBy(doc, ifcEntity.IsDefinedBy, propertyIsInstanceMap,
            specType, out var propParams, out var propValues);

        CollectFromAttributes(doc, ifcEntity, specType, out var attrParams, out var attrValues);

        parametersToCreate = classParams.Concat(propParams).Concat(attrParams).ToList();
        parametersToSet = classValues
            .Concat(propValues)
            .Concat(attrValues)
            .GroupBy(p => p.Key)
            .ToDictionary(g => g.Key, g => g.Last().Value);
    }

    // ─── Classification associations ─────────────────────────────────────────

    private void CollectFromAssociations(
        Document doc,
        IfcEntity ifcEntity,
        HashSet<IfcClassification> dictionaryCollection,
        List<Association>? associations,
        ForgeTypeId specType,
        out List<ParameterCreation> parametersToCreate,
        out Dictionary<string, object?> parametersToSet)
    {
        parametersToCreate = new();
        parametersToSet = new();

        if (associations == null) return;

        bool isRoomOrArea = ifcEntity.Name == RoomName || ifcEntity.Name == AreaName;

        foreach (var assoc in associations)
        {
            if (assoc is not IfcClassificationReference classRef) continue;

            var bsddParamName = CreateParameterNameFromClassificationReference(classRef);
            if (string.IsNullOrEmpty(bsddParamName)) continue;

            if (isRoomOrArea)
            {
                // Rooms/areas use instance parameters with [Instance] suffix
                // Instance parameters are created but values are NOT set (plugin is type-level only)
                var instanceName = bsddParamName + "[Instance]";
                parametersToCreate.Add(new ParameterCreation(
                    instanceName, specType,
                    _parametersManager.ExistingProjectParameter(doc, instanceName), true));
                // DO NOT SET VALUE: parametersToSet[instanceName] = ... (user fills instance values manually)
            }
            else
            {
                parametersToCreate.Add(new ParameterCreation(
                    bsddParamName, specType,
                    _parametersManager.ExistingProjectParameter(doc, bsddParamName), false));
                parametersToSet[bsddParamName] = $"{classRef.Identification}:{classRef.Name}";
            }

            if (classRef.ReferencedSource != null)
                dictionaryCollection.Add(classRef.ReferencedSource);

            // Mapped parameter (e.g., NL-SfB code)
            var mappedName = GetMappedParameterName(classRef);
            if (!string.IsNullOrEmpty(mappedName))
                parametersToSet[mappedName] = classRef.Identification;
        }
    }

    // ─── Property sets ────────────────────────────────────────────────────────

    private void CollectFromIsDefinedBy(
        Document doc,
        List<IfcPropertySet>? isDefinedBy,
        Dictionary<string, bool> propertyIsInstanceMap,
        ForgeTypeId specType,
        out List<ParameterCreation> parametersToCreate,
        out Dictionary<string, object?> parametersToSet)
    {
        parametersToCreate = new();
        parametersToSet = new();

        if (isDefinedBy == null) return;

        foreach (var pset in isDefinedBy)
        {
            if (pset.HasProperties == null) continue;
            foreach (var prop in pset.HasProperties)
            {
                if (prop.Type == null) continue;

                object? value = null;
                var effectiveSpecType = specType;

                if (prop is IfcPropertySingleValue spv && spv.NominalValue != null)
                {
                    value = GetValueInCorrectDatatype(spv.NominalValue);
                    effectiveSpecType = GetSpecTypeFromIfcValue(spv.NominalValue);
                }
                else if (prop is IfcPropertyEnumeratedValue epv && epv.EnumerationValues?.Any() == true)
                {
                    var first = epv.EnumerationValues.First();
                    value = GetValueInCorrectDatatype(first);
                    effectiveSpecType = GetSpecTypeFromIfcValue(first);
                }

                var propKey = prop.Name ?? string.Empty;
                var psetPropKey = $"{pset.Name}/{prop.Name}";

                bool isInstance = propertyIsInstanceMap.TryGetValue(propKey, out var iv)
                    ? iv
                    : propertyIsInstanceMap.TryGetValue(psetPropKey, out var iv2) && iv2;

                // Use simple property name for built-in Revit parameters (e.g. "Manufacturer", "LoadBearing")
                // These already exist and should not be duplicated as project parameters
                var bsddParamName = IsBuiltInParameter(propKey) 
                    ? propKey 
                    : CreateParameterNameFromPropertySet(pset.Name, prop);

                // Only create new parameter if it's NOT a built-in Revit parameter
                if (!IsBuiltInParameter(propKey))
                {
                    parametersToCreate.Add(new ParameterCreation(
                        bsddParamName, effectiveSpecType,
                        _parametersManager.ExistingProjectParameter(doc, bsddParamName), isInstance));
                }

                // Instance parameter values are not sent by the UI — only set type-level values.
                if (!isInstance)
                    parametersToSet[bsddParamName] = value;
            }
        }
    }

    // ─── IFC attribute parameters ─────────────────────────────────────────────

    private void CollectFromAttributes(
        Document doc,
        IfcEntity ifcEntity,
        ForgeTypeId specType,
        out List<ParameterCreation> parametersToCreate,
        out Dictionary<string, object?> parametersToSet)
    {
        parametersToCreate = new();
        parametersToSet = new();

        bool isRoomOrArea = ifcEntity.Name == RoomName || ifcEntity.Name == AreaName;

        if (ifcEntity.Type != null)
        {
            parametersToSet["Export Type to IFC As"] = ifcEntity.Type;
            parametersToSet["Type IFC Predefined Type"] = ifcEntity.PredefinedType ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(ifcEntity.Description) && ifcEntity.Description != "...")
            parametersToSet["Description"] = ifcEntity.Description;

        if (isRoomOrArea)
        {
            var objTypeParam = "IfcObjectType";
            parametersToCreate.Add(new ParameterCreation(
                objTypeParam, SpecTypeId.String.Text,
                _parametersManager.ExistingProjectParameter(doc, objTypeParam), true));
        }

        if (!string.IsNullOrEmpty(ifcEntity.ObjectType))
        {
            var objTypeParamType = "IfcObjectType[Type]";
            parametersToCreate.Add(new ParameterCreation(
                objTypeParamType, SpecTypeId.String.Text,
                _parametersManager.ExistingProjectParameter(doc, objTypeParamType), false));
            parametersToSet[objTypeParamType] = ifcEntity.ObjectType;
        }
    }

    // ─── Naming helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates the bsdd/class/{host}{path} parameter name from a classification reference.
    /// </summary>
    public static string CreateParameterNameFromClassificationReference(IfcClassificationReference classRef)
    {
        var locationStr = classRef.ReferencedSource?.Location;
        if (string.IsNullOrEmpty(locationStr)) return string.Empty;
        if (!Uri.TryCreate(locationStr, UriKind.Absolute, out var uri)) return string.Empty;
        return CreateParameterNameFromUri(uri);
    }

    /// <summary>
    /// Creates the bsdd/class/{host}{path} parameter name from a URI.
    /// </summary>
    public static string CreateParameterNameFromUri(Uri uri)
        => $"bsdd/class/{uri.Host}{uri.PathAndQuery}";

    /// <summary>
    /// Creates the bsdd/prop/{propertySet}/{property} parameter name, or the mapped short name
    /// if <paramref name="prop"/>.Specification matches a known IFC 4.3 property URI.
    /// </summary>
    public static string CreateParameterNameFromPropertySet(string? psetName, IfcProperty prop)
    {
        if (!string.IsNullOrEmpty(prop.Specification) &&
            IfcParameterMappings.Mappings.TryGetValue(prop.Specification, out var mappedName))
            return mappedName;
        return $"bsdd/prop/{psetName}/{prop.Name}";
    }

    /// <summary>
    /// Creates the bsdd/prop/{propertySet}/{property} parameter name.
    /// </summary>
    public static string CreateParameterNameFromPropertySet(string? psetName, string? propName)
        => $"bsdd/prop/{psetName}/{propName}";

    // ─── Mapped parameters ────────────────────────────────────────────────────

    private string? GetMappedParameterName(IfcClassificationReference classRef)
    {
        var locationStr = classRef.ReferencedSource?.Location;
        if (string.IsNullOrEmpty(locationStr)) return null;

        var settings = _settingsManager.GetSettings();
        var allDicts = new List<BsddDictionary>();
        if (settings.MainDictionary != null) allDicts.Add(settings.MainDictionary);
        if (settings.FilterDictionaries != null) allDicts.AddRange(settings.FilterDictionaries);

        foreach (var dict in allDicts)
        {
            if (dict.IfcClassification?.Location == locationStr)
                return dict.ParameterMapping;
        }
        return null;
    }

    // ─── Value conversion ─────────────────────────────────────────────────────

    private object? GetValueInCorrectDatatype(IfcValue val)
    {
        if (val.Value == null) return null;

        // Handle empty or whitespace-only strings
        var stringValue = val.Value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue)) return null;

        try
        {
            return val.Type switch
            {
                "IfcBoolean" => Convert.ToBoolean(val.Value) ? 1 : 0,
                "IfcInteger" => System.Convert.ToInt32(val.Value),
                "IfcReal" => ConvertMmToFeet(System.Convert.ToDouble(val.Value,
                    System.Globalization.CultureInfo.InvariantCulture)),
                "IfcDate" or "IfcDateTime" => System.Convert.ToDateTime(val.Value).ToString(),
                _ => stringValue
            };
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Failed to convert value '{Value}' to type '{Type}', using null", 
                stringValue, val.Type);
            return null;
        }
        catch (InvalidCastException ex)
        {
            _logger.LogWarning(ex, "Failed to cast value '{Value}' to type '{Type}', using null", 
                stringValue, val.Type);
            return null;
        }
    }

    private static ForgeTypeId GetSpecTypeFromIfcValue(IfcValue val)
    {
        return val.Type switch
        {
            "IfcBoolean" => SpecTypeId.Boolean.YesNo,
            "IfcInteger" => SpecTypeId.Int.Integer,
            "IfcReal" or "IfcPositiveLengthMeasure" or "IfcLengthMeasure" => SpecTypeId.Length,
            "IfcAreaMeasure" => SpecTypeId.Area,
            "IfcVolumeMeasure" => SpecTypeId.Volume,
            _ => SpecTypeId.String.Text
        };
    }

    private static double ConvertMmToFeet(double mm) => mm / 304.8;

    // ─── Instance/type determination ──────────────────────────────────────────

    /// <summary>
    /// Scans the document's parameter bindings to determine which parameters are
    /// instance parameters vs type parameters. Used to populate PropertyIsInstanceMap
    /// for the bSDD search bridge.
    ///
    /// Key format:
    ///   - Full parameter name → isInstance
    ///   - For bsdd/prop/ params: last path segment → isInstance (e.g., "FireRating" → false)
    /// </summary>
    public Dictionary<string, bool> GetProjectParameterTypes(Document doc)
    {
        var result = new Dictionary<string, bool>();

        try
        {
            var it = doc.ParameterBindings.ForwardIterator();
            while (it.MoveNext())
            {
                var def = it.Key as InternalDefinition;
                if (def == null) continue;

                var name = def.Name;
                bool isInstance = it.Current is InstanceBinding;

                if (IfcParameterMappings.Mappings.ContainsValue(name))
                {
                    // IFC 4.3 standard property – key is the short name (e.g., "FireRating")
                    if (!result.ContainsKey(name))
                        result[name] = isInstance;
                }
                else if (name.StartsWith("bsdd/prop/", StringComparison.Ordinal))
                {
                    // bSDD property – key is the last path segment
                    var lastSlash = name.LastIndexOf('/');
                    var shortName = lastSlash >= 0 ? name.Substring(lastSlash + 1) : name;
                    if (!result.ContainsKey(shortName))
                        result[shortName] = isInstance;
                }
                else if (name.StartsWith("bsdd/class/", StringComparison.Ordinal))
                {
                    // bSDD classification – type parameter unless it ends with [Instance] (for Rooms/Areas)
                    bool isClassInstance = name.EndsWith("[Instance]", StringComparison.Ordinal);
                    if (!result.ContainsKey(name))
                        result[name] = isClassInstance;
                }
            }

            // Disable name to be editable in UI (matching original plugin behaviour)
            result["Attributes/Name"] = true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read parameter bindings");
        }

        return result;
    }

    /// <summary>
    /// Checks if a parameter name corresponds to a built-in Revit parameter.
    /// Built-in parameters should not be duplicated as project parameters.
    /// </summary>
    private static bool IsBuiltInParameter(string parameterName)
    {
        // Common IFC/bSDD properties that map to built-in Revit parameters
        // These should use the native Revit parameter instead of creating duplicates
        return parameterName switch
        {
            "Manufacturer" => true,
            "LoadBearing" => true,
            "IsExternal" => true,
            "FireRating" => true,
            "ThermalTransmittance" => true,
            "Reference" => true,
            "Status" => true,
            "AcousticRating" => true,
            "SurfaceSpreadOfFlame" => true,
            "Combustible" => true,
            _ => false
        };
    }
}
