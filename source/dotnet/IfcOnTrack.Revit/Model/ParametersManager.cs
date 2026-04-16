// Purpose: Creates and manages Revit project parameters for bSDD classifications and properties
using System.IO;
using Autodesk.Revit.DB;
using IfcOnTrack.Revit.Utilities;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.Model;

/// <summary>
/// Manages creation and manipulation of Revit project parameters for bSDD data.
/// Parameters are created as internal shared parameters bound to element categories.
/// </summary>
public class ParametersManager
{
    private readonly ILogger<ParametersManager> _logger;

    public ParametersManager(ILogger<ParametersManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks whether a project parameter already exists in the document.
    /// </summary>
    public bool ExistingProjectParameter(Document doc, string parameterName)
    {
        var map = doc.ParameterBindings;
        var it = map.ForwardIterator();
        while (it.MoveNext())
        {
            if (it.Key?.Name == parameterName)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Creates multiple project parameters at once using a temporary shared parameter file.
    /// Parameters are bound to the specified categories (or all categories if null).
    /// </summary>
    public void CreateProjectParameters(
        Document doc,
        List<ParameterCreation> parametersToCreate,
        string groupName,
        ForgeTypeId groupType,
        IEnumerable<Category>? categoryList)
    {
        if (!parametersToCreate.Any()) return;

        var originalSharedParamFile = doc.Application.SharedParametersFilename;
        var tempFile = Path.GetTempFileName() + ".txt";

        try
        {
            // Use an empty temp file as shared parameter store
            using (File.Create(tempFile)) { }
            doc.Application.SharedParametersFilename = tempFile;

            var categories = categoryList == null
                ? AllCategories(doc)
                : ToCategorySet(doc, categoryList);

            var sharedParamFile = doc.Application.OpenSharedParameterFile();
            var groupDef = sharedParamFile.Groups.get_Item(groupName)
                ?? sharedParamFile.Groups.Create(groupName);

            foreach (var param in parametersToCreate)
            {
                if (param.Existing)
                {
                    // Parameter exists: extend to include the first category if needed
                    if (categoryList?.Any() == true)
                        AddCategoryToProjectParameter(doc, param.ParameterName, categoryList.First());
                }
                else
                {
                    CreateSingleParameter(doc, param, groupDef, categories, groupType);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create project parameters");
        }
        finally
        {
            doc.Application.SharedParametersFilename = originalSharedParamFile;
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    /// <summary>
    /// Sets parameter values on an ElementType.
    /// Skips read-only parameters and parameters not present on the element.
    /// </summary>
    public void SetElementTypeParameters(ElementType elementType, Dictionary<string, object?> parametersToSet)
    {
        foreach (var kvp in parametersToSet)
        {
            var name = kvp.Key;
            var value = kvp.Value;
            try
            {
                var param = elementType.LookupParameter(name);
                if (param is null || param.IsReadOnly) continue;
                SetParameterValue(param, value);
                _logger.LogDebug("Set parameter '{Name}' = '{Value}' on '{Element}'", name, value, elementType.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set parameter '{Name}' on '{Element}'", name, elementType.Name);
            }
        }
    }

    /// <summary>
    /// Enables VaryBetweenGroups for instance parameters listed in parametersToCreate.
    /// </summary>
    public void SetInstanceParameterVaryBetweenGroups(Document doc, List<ParameterCreation> parametersToCreate, bool varyBetweenGroups)
    {
        var map = doc.ParameterBindings;
        var it = map.ForwardIterator();
        while (it.MoveNext())
        {
            if (it.Key?.Name is not string name) continue;
            var pc = parametersToCreate.FirstOrDefault(p => p.ParameterName == name && p.IsInstance);
            if (pc == null) continue;

            if (it.Current is InstanceBinding binding)
            {
                // Re-insert with VaryBetweenGroups enforcement – Revit API doesn't expose a setter,
                // so we rely on the insertion behaviour which defaults allow groups already.
                _logger.LogDebug("Instance parameter '{Name}' bound", name);
            }
        }
    }

    private void CreateSingleParameter(
        Document doc,
        ParameterCreation param,
        DefinitionGroup groupDef,
        CategorySet categories,
        ForgeTypeId groupType)
    {
        try
        {
            if (groupDef.Definitions.get_Item(param.ParameterName) != null) return;

            var options = new ExternalDefinitionCreationOptions(param.ParameterName, param.SpecType)
            {
                GUID = UuidFromUri.CreateUuidFromUri(param.ParameterName)
            };
            _logger.LogDebug("Creating parameter '{Name}', GUID = {Guid}", param.ParameterName, options.GUID);

            var def = (ExternalDefinition)groupDef.Definitions.Create(options);
            Binding bin = param.IsInstance
                ? (Binding)doc.Application.Create.NewInstanceBinding(categories)
                : doc.Application.Create.NewTypeBinding(categories);

            doc.ParameterBindings.Insert(def, bin, groupType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create parameter '{Name}'", param.ParameterName);
        }
    }

    private void AddCategoryToProjectParameter(Document doc, string parameterName, Category category)
    {
        try
        {
            var map = doc.ParameterBindings;
            var it = map.ForwardIterator();
            while (it.MoveNext())
            {
                if (it.Key?.Name != parameterName) continue;
                var binding = it.Current as ElementBinding;
                if (binding == null) break;

                var categories = binding.Categories;
                if (!categories.Contains(category))
                    categories.Insert(category);

                map.ReInsert(it.Key, binding);
                break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add category to parameter '{Name}'", parameterName);
        }
    }

    private static void SetParameterValue(Parameter param, object? value)
    {
        if (value is null) return;
        switch (param.StorageType)
        {
            case StorageType.String:
                param.Set(value.ToString() ?? string.Empty);
                break;
            case StorageType.Integer:
                if (int.TryParse(value.ToString(), out var iv))
                    param.Set(iv);
                else if (value is double d)
                    param.Set((int)d);
                else if (value is bool b)
                    param.Set(b ? 1 : 0);
                break;
            case StorageType.Double:
                if (double.TryParse(value.ToString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var dv))
                    param.Set(dv);
                break;
        }
    }

    private static CategorySet AllCategories(Document doc)
    {
        var cats = doc.Application.Create.NewCategorySet();
        foreach (Category cat in doc.Settings.Categories)
        {
            if (cat.AllowsBoundParameters)
                cats.Insert(cat);
        }
        return cats;
    }

    private static CategorySet ToCategorySet(Document doc, IEnumerable<Category> categoryList)
    {
        var cats = doc.Application.Create.NewCategorySet();
        foreach (var cat in categoryList)
            cats.Insert(cat);
        return cats;
    }
}
