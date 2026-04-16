// Purpose: Data class describing a Revit parameter to create/update
using Autodesk.Revit.DB;

namespace IfcOnTrack.Revit.Utilities;

/// <summary>
/// Describes a Revit project parameter that needs to be created or updated.
/// </summary>
public class ParameterCreation
{
    public string ParameterName { get; set; }
    public ForgeTypeId SpecType { get; set; }
    public bool Existing { get; set; }
    public bool IsInstance { get; set; }

    public ParameterCreation(string parameterName, ForgeTypeId specType)
    {
        ParameterName = parameterName;
        SpecType = specType;
    }

    public ParameterCreation(string parameterName, ForgeTypeId specType, bool existing, bool isInstance)
    {
        ParameterName = parameterName;
        SpecType = specType;
        Existing = existing;
        IsInstance = isInstance;
    }
}
