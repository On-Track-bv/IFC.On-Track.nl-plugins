// Purpose: IFC export service – orchestrates bSDD-aware IFC export from Revit
// Handles: IFC export configuration (DataStorage-backed, user-editable), UDPS mapping, export, post-processing
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using BIM.IFC.Export.UI;
using IfcOnTrack.Revit.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace IfcOnTrack.Revit.IfcExport;

/// <summary>
/// Orchestrates a complete bSDD-aware IFC export:
/// 1. Loads (or creates) the "Bsdd export settings" IFCExportConfiguration from document DataStorage
/// 2. Stores temporary bSDD classifications in document DataStorage
/// 3. Merges bsdd/prop/ parameters into the UDPS mapping file from the stored configuration
/// 4. Triggers Revit's IFC export via IFCExportConfiguration.UpdateOptions()
/// 5. Post-processes the output file to fix classification reference URLs
/// 6. Restores the original document classifications
///
/// The stored IFCExportConfiguration uses the same DataStorage schema GUID as the legacy
/// bSDD-Revit-plugin so that documents carry their export settings across plugin versions.
/// Users can modify the export profile through Revit's native IFC Export dialog.
/// </summary>
public class IfcExportService
{
    private readonly ILogger<IfcExportService> _logger;
    private readonly ElementsManager _elementsManager;
    private readonly IfcClassificationManager _classificationManager;
    private readonly IfcPostprocessor _postprocessor;

    // Same schema GUID as bSDD-Revit-plugin — keeps export settings when opening old documents.
    private static readonly Guid ExportConfigSchemaGuid = new("c2a3e6fe-ce51-4f35-8ff1-20c34567b687");
    private const string ExportConfigSchemaName = "IFCExportConfigurationMap";
    private const string ExportConfigFieldName  = "MapField";
    private const string BsddConfigurationName  = "Bsdd export settings";

    public IfcExportService(
        ILogger<IfcExportService> logger,
        ElementsManager elementsManager,
        IfcClassificationManager classificationManager,
        IfcPostprocessor postprocessor)
    {
        _logger = logger;
        _elementsManager = elementsManager;
        _classificationManager = classificationManager;
        _postprocessor = postprocessor;
    }

    /// <summary>
    /// Runs the complete bSDD IFC export workflow.
    /// Prompts the user for a save path via a dialog.
    /// </summary>
    /// <param name="doc">The active Revit document.</param>
    /// <param name="activeViewId">The active view ElementId (used to scope the export).</param>
    public void ExportIfc(Document doc, ElementId activeViewId)
    {
        // Collect existing (non-bSDD) classifications so we can restore them later
        var existingClassifications = _classificationManager.GetStoredClassifications(doc);

        // Collect all entities from the document for post-processing
        var allEntities = _elementsManager.GetAllElementTypesAsIfcJson(doc);
        _postprocessor.CollectFromEntities(allEntities);

        // Load or create the bSDD IFC export configuration — always override ActiveViewId
        var exportConfig = GetOrSetBsddConfiguration(doc);
        exportConfig.ActiveViewId = activeViewId;
        exportConfig.ActivePhaseId = -1;

        // Get save path
        var savePath = PromptForSavePath();
        if (string.IsNullOrEmpty(savePath)) return;

        var folder   = Path.GetDirectoryName(savePath)!;
        var fileName = Path.GetFileName(savePath);

        try
        {
            // 1. Write bSDD classifications to DataStorage
            using (var tx = new Transaction(doc, "Store bSDD interim classifications"))
            {
                tx.Start();
                var bsddClassifications = _classificationManager.GetAllIfcClassificationsFromSettings();
                var paramMap = _classificationManager.GetClassificationParameterMap();
                _classificationManager.UpdateClassifications(doc, bsddClassifications, isBsddExport: true, paramMap);
                tx.Commit();
            }

            // 2. Merge bsdd/prop/ parameters into the UDPS file from the configuration
            var udpsFile = BuildUdpsFile(doc, exportConfig.ExportUserDefinedPsetsFileName);
            if (!string.IsNullOrEmpty(udpsFile))
            {
                exportConfig.ExportUserDefinedPsets = true;
                exportConfig.ExportUserDefinedPsetsFileName = udpsFile;
            }

            // 3. Build IFC export options from the (potentially user-modified) configuration
            var options = new IFCExportOptions();
            exportConfig.UpdateOptions(options, activeViewId);

            // 4. Export to temp directory, then move to final path
            var tempDir      = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var tempFilePath = Path.Combine(tempDir, fileName);
            Directory.CreateDirectory(tempDir);

            using (var exportTx = new Transaction(doc, "IFC Export"))
            {
                exportTx.Start();
                doc.Export(tempDir, fileName, options);
                exportTx.Commit();
            }

            if (!File.Exists(tempFilePath))
                throw new InvalidOperationException($"IFC export produced no file at {tempFilePath}");

            // 5. Post-process (fix classification reference URLs)
            _postprocessor.PostProcess(tempFilePath, savePath);

            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);

            _logger.LogInformation("IFC export complete: {Path}", savePath);
        }
        finally
        {
            // 6. Restore original classifications
            try
            {
                using var restoreTx = new Transaction(doc, "Restore IFC classifications");
                restoreTx.Start();
                _classificationManager.UpdateClassifications(doc, existingClassifications, isBsddExport: false);
                restoreTx.Commit();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restore IFC classifications");
            }
        }
    }

    // ─── IFC Export Configuration (DataStorage) ───────────────────────────────

    /// <summary>
    /// Returns the stored "Bsdd export settings" IFCExportConfiguration, or creates and persists a default one.
    /// Uses the same DataStorage schema as the legacy bSDD-Revit-plugin for document compatibility.
    /// </summary>
    public IFCExportConfiguration GetOrSetBsddConfiguration(Document doc)
    {
        var stored = FindStoredConfiguration(doc);
        if (stored != null)
        {
            _logger.LogDebug("Loaded IFC export configuration '{Name}' from DataStorage", stored.Name);
            return stored;
        }

        var config = GetDefaultExportConfiguration();

        using var tx = new Transaction(doc, "Create bSDD IFC export configuration");
        tx.Start();
        SaveConfigurationToDataStorage(doc, config);
        tx.Commit();

        _logger.LogInformation("Created default IFC export configuration '{Name}'", config.Name);
        return config;
    }

    private IFCExportConfiguration? FindStoredConfiguration(Document doc)
    {
        var schema = Schema.Lookup(ExportConfigSchemaGuid);
        if (schema == null) return null;

        var field = schema.GetField(ExportConfigFieldName);

        foreach (var ds in new FilteredElementCollector(doc)
            .OfClass(typeof(DataStorage))
            .Cast<DataStorage>())
        {
            try
            {
                var entity = ds.GetEntity(schema);
                if (!entity.IsValid()) continue;

                var json = entity.Get<string>(field);
                if (string.IsNullOrEmpty(json)) continue;

                var config = JsonConvert.DeserializeObject<IFCExportConfiguration>(
                    json, new IFCExportConfigurationConverter());
                if (config?.Name == BsddConfigurationName)
                    return config;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize IFC export configuration from DataStorage");
            }
        }

        return null;
    }

    private void SaveConfigurationToDataStorage(Document doc, IFCExportConfiguration config)
    {
        var schema = GetOrCreateExportConfigSchema();
        var dataStorage = DataStorage.Create(doc);
        var entity = new Entity(schema);
        entity.Set<string>(schema.GetField(ExportConfigFieldName), config.SerializeConfigToJson());
        dataStorage.SetEntity(entity);
    }

    private static IFCExportConfiguration GetDefaultExportConfiguration()
    {
        var config = IFCExportConfiguration.CreateDefaultConfiguration();
        config.Name                              = BsddConfigurationName;
        config.IFCVersion                        = IFCVersion.IFC4x3;
        config.ExchangeRequirement               = 0;
        config.IFCFileType                       = 0;
        config.SpaceBoundaries                   = 0;
        config.SplitWallsAndColumns              = false;
        config.VisibleElementsOfCurrentView      = true;
        config.ExportRoomsInView                 = false;
        config.IncludeSteelElements              = true;
        config.Export2DElements                  = false;
        config.ExportInternalRevitPropertySets   = false;
        config.ExportIFCCommonPropertySets       = true;
        config.ExportBaseQuantities              = true;
        config.ExportSchedulesAsPsets            = false;
        config.ExportSpecificSchedules           = false;
        config.ExportUserDefinedPsets            = false;
        config.ExportUserDefinedPsetsFileName    = string.Empty;
        config.ExportUserDefinedParameterMapping = false;
        config.ExportUserDefinedParameterMappingFileName = string.Empty;
        config.TessellationLevelOfDetail         = 0.5;
        config.ExportPartsAsBuildingElements     = false;
        config.ExportSolidModelRep               = false;
        config.UseActiveViewGeometry             = false;
        config.UseFamilyAndTypeNameForReference  = false;
        config.Use2DRoomBoundaryForVolume        = false;
        config.IncludeSiteElevation              = false;
        config.StoreIFCGUID                      = true;
        config.ExportBoundingBox                 = false;
        config.UseOnlyTriangulation              = false;
        config.UseTypeNameOnlyForIfcType         = false;
        config.UseVisibleRevitNameAsEntityName   = false;
        config.ActivePhaseId                     = -1;
        return config;
    }

    private static Schema GetOrCreateExportConfigSchema()
    {
        var existing = Schema.Lookup(ExportConfigSchemaGuid);
        if (existing != null) return existing;

        var builder = new SchemaBuilder(ExportConfigSchemaGuid);
        builder.SetSchemaName(ExportConfigSchemaName);
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.AddSimpleField(ExportConfigFieldName, typeof(string));
        return builder.Finish();
    }

    // ─── UDPS file ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a temporary UDPS property-sets mapping file that maps bsdd/prop/ parameters
    /// into IFC property sets for the export. If the stored configuration already has a user
    /// UDPS file, the bSDD properties are appended to it.
    /// </summary>
    private string? BuildUdpsFile(Document doc, string? existingUdpsFilePath)
    {
        try
        {
            var bsddParameters = GetAllBsddPropParameters(doc);
            if (!bsddParameters.Any()) return null;

            var paramsByPset = GroupByPropertySet(bsddParameters);
            var bsddContent  = BuildUdpsContent(paramsByPset);

            var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".txt");

            if (!string.IsNullOrEmpty(existingUdpsFilePath) && File.Exists(existingUdpsFilePath))
            {
                File.Copy(existingUdpsFilePath, tempFile);
                File.AppendAllText(tempFile, bsddContent);
            }
            else
            {
                File.WriteAllText(tempFile, bsddContent);
            }

            return tempFile;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build UDPS file");
            return null;
        }
    }

    private IList<Parameter> GetAllBsddPropParameters(Document doc)
    {
        var seen   = new HashSet<string>();
        var result = new List<Parameter>();

        // bsdd/prop/ parameters are always type parameters – no need to iterate instances
        foreach (var element in new FilteredElementCollector(doc).WhereElementIsElementType())
        {
            foreach (Parameter param in element.Parameters)
            {
                var name = param.Definition?.Name;
                if (!string.IsNullOrEmpty(name) &&
                    name.StartsWith("bsdd/prop/", StringComparison.Ordinal) &&
                    seen.Add(name))
                    result.Add(param);
            }
        }
        return result;
    }

    private static Dictionary<string, List<Parameter>> GroupByPropertySet(IList<Parameter> parameters)
    {
        var groups = new Dictionary<string, List<Parameter>>();
        foreach (var p in parameters)
        {
            var parts = p.Definition!.Name.Split('/');
            if (parts.Length < 3) continue;
            var psetName = parts[2];
            if (!groups.ContainsKey(psetName)) groups[psetName] = new();
            groups[psetName].Add(p);
        }
        return groups;
    }

    private static string BuildUdpsContent(Dictionary<string, List<Parameter>> paramsByPset)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kvp in paramsByPset)
        {
            var psetName   = kvp.Key;
            var parameters = kvp.Value;
            sb.AppendLine();
            sb.AppendLine("#");
            sb.AppendLine("#");
            sb.AppendLine($"PropertySet:\t{psetName}\tT\tIfcElementType, IfcSpaceType, IfcObject, IfcObjectType, IfcSite, IfcSiteType");
            sb.AppendLine("#");
            sb.AppendLine("#\tGenerated by IFC.On-Track.nl bSDD plugin");
            sb.AppendLine("#");

            foreach (var p in parameters)
            {
                var paramParts = p.Definition!.Name.Split('/');
                var propName   = paramParts.Length >= 4 ? string.Join("/", paramParts.Skip(3)) : p.Definition.Name;
                var dataType   = GetIfcDataType(p);
                sb.AppendLine($"\t{propName}\t{dataType}\t{p.Definition.Name}");
            }
        }
        return sb.ToString();
    }

    private static string GetIfcDataType(Parameter p)
    {
        return p.StorageType switch
        {
            StorageType.String  => "Text",
            StorageType.Double  => "Real",
            StorageType.Integer => p.Definition.GetDataType().TypeId == "autodesk.spec:spec.bool-1.0.0"
                ? "Boolean"
                : "Integer",
            _ => "Text"
        };
    }

    private static string? PromptForSavePath()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter       = "IFC Files (*.ifc)|*.ifc",
            FilterIndex  = 1,
            RestoreDirectory = true,
            Title        = "Export IFC with bSDD classifications"
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
