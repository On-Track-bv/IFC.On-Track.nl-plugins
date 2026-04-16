// Purpose: IFC export service – orchestrates bSDD-aware IFC export from Revit
// Handles: UDPS property mapping file generation, IFC classification setup, export, post-processing
using System.IO;
using Autodesk.Revit.DB;
using IfcOnTrack.Revit.Model;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.IfcExport;

/// <summary>
/// Orchestrates a complete bSDD-aware IFC export:
/// 1. Stores temporary bSDD classifications in document DataStorage
/// 2. Generates a UDPS (User Defined Property Sets) mapping file for bsdd/prop/ parameters
/// 3. Triggers Revit's IFC export
/// 4. Post-processes the output file to fix classification reference URLs
/// 5. Restores the original document classifications
///
/// The IFC export uses Revit's standard IFCExportOptions.
/// Advanced configuration (IFC version, coordinate base, etc.) is applied via IFCExportOptions.AddOption().
/// </summary>
public class IfcExportService
{
    private readonly ILogger<IfcExportService> _logger;
    private readonly SettingsManager _settingsManager;
    private readonly ElementsManager _elementsManager;
    private readonly IfcClassificationManager _classificationManager;
    private readonly IfcPostprocessor _postprocessor;

    public IfcExportService(
        ILogger<IfcExportService> logger,
        SettingsManager settingsManager,
        ElementsManager elementsManager,
        IfcClassificationManager classificationManager,
        IfcPostprocessor postprocessor)
    {
        _logger = logger;
        _settingsManager = settingsManager;
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

        // Get save path
        var savePath = PromptForSavePath();
        if (string.IsNullOrEmpty(savePath)) return;

        var folder = Path.GetDirectoryName(savePath)!;
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

            // 2. Build IFC export options
            var options = BuildExportOptions(doc, activeViewId);

            // 3. Export to temp directory
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            var tempFilePath = Path.Combine(tempDir, fileName);

            using (var exportTx = new Transaction(doc, "IFC Export"))
            {
                exportTx.Start();
                doc.Export(tempDir, fileName, options);
                exportTx.Commit();
            }

            if (!File.Exists(tempFilePath))
                throw new InvalidOperationException($"IFC export produced no file at {tempFilePath}");

            // 4. Post-process (fix classification URLs)
            _postprocessor.PostProcess(tempFilePath, savePath);

            // Cleanup temp dir
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);

            _logger.LogInformation("IFC export complete: {Path}", savePath);
        }
        finally
        {
            // 5. Restore original classifications
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

    private IFCExportOptions BuildExportOptions(Document doc, ElementId activeViewId)
    {
        var options = new IFCExportOptions();

        // IFC version – default to IFC4x3 (override via settings if needed)
        options.AddOption("IFCVersion", "25"); // 25 = IFC4x3 in Revit's internal enum

        // UDPS property mapping
        var udpsFile = BuildUdpsFile(doc);
        if (!string.IsNullOrEmpty(udpsFile))
        {
            options.AddOption("ExportUserDefinedPsets", "true");
            options.AddOption("ExportUserDefinedPsetsFileName", udpsFile);
        }

        // Export linked files: don't
        options.AddOption("ExportLinkedFiles", "false");

        // Use active view
        if (activeViewId != ElementId.InvalidElementId)
            options.FilterViewId = activeViewId;

        return options;
    }

    /// <summary>
    /// Builds a temporary UDPS property-sets mapping file that maps bsdd/prop/ parameters
    /// into IFC property sets for the export.
    /// </summary>
    private string? BuildUdpsFile(Document doc)
    {
        try
        {
            var bsddParameters = GetAllBsddPropParameters(doc);
            if (!bsddParameters.Any()) return null;

            var paramsByPset = GroupByPropertySet(bsddParameters);
            var content = BuildUdpsContent(paramsByPset);

            var tempFile = Path.GetTempFileName().Replace(".tmp", ".txt");
            File.WriteAllText(tempFile, content);
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
        var seen = new HashSet<string>();
        var result = new List<Parameter>();

        // bsdd/prop/ parameters are always type parameters – no need to iterate instances
        foreach (var element in new FilteredElementCollector(doc).WhereElementIsElementType())
        {
            foreach (Parameter param in element.Parameters)
            {
                var name = param.Definition?.Name;
                if (!string.IsNullOrEmpty(name) && name.StartsWith("bsdd/prop/") && seen.Add(name))
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
            var psetName = kvp.Key;
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
                var propName = paramParts.Length >= 4 ? string.Join("/", paramParts.Skip(3)) : p.Definition.Name;
                var dataType = GetIfcDataType(p);
                sb.AppendLine($"\t{propName}\t{dataType}\t{p.Definition.Name}");
            }
        }
        return sb.ToString();
    }

    private static string GetIfcDataType(Parameter p)
    {
        return p.StorageType switch
        {
            StorageType.String => "Text",
            StorageType.Double => "Real",
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
            Filter = "IFC Files (*.ifc)|*.ifc",
            FilterIndex = 1,
            RestoreDirectory = true,
            Title = "Export IFC with bSDD classifications"
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
