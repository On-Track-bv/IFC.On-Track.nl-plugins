// Purpose: Post-processes exported IFC files to fix bSDD classification reference URLs
using System.IO;
using IfcOnTrack.Core.Bridge;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Revit.IfcExport;

/// <summary>
/// Post-processes a Revit-exported IFC file to fix bSDD classification reference URLs.
///
/// Why: Revit's IFC exporter writes the DICTIONARY location URL into IfcClassificationReference
/// instead of the actual CLASSIFICATION REFERENCE location (e.g., the specific class page URL).
/// This post-processor replaces those dictionary URLs with the correct classification URLs.
///
/// Processing is done as a streaming line-by-line replace to handle large IFC files efficiently.
/// </summary>
public class IfcPostprocessor
{
    private readonly ILogger<IfcPostprocessor> _logger;
    private readonly List<ClassificationReplacement> _replacements = new();

    public IfcPostprocessor(ILogger<IfcPostprocessor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Collects classification data directly from a pre-built list of entities.
    /// Must be called before PostProcess().
    /// </summary>
    public void CollectFromEntities(IEnumerable<IfcEntity> entities)
    {
        _replacements.Clear();

        foreach (var entity in entities)
        {
            if (entity.HasAssociations == null) continue;
            foreach (var assoc in entity.HasAssociations)
            {
                if (assoc is not IfcClassificationReference classRef) continue;
                if (classRef.Location == null || classRef.Identification == null) continue;
                if (classRef.ReferencedSource?.Location == null) continue;

                var replacement = new ClassificationReplacement
                {
                    DictionaryLocation = classRef.ReferencedSource.Location,
                    ClassificationLocation = classRef.Location,
                    Identification = classRef.Identification
                };

                if (!_replacements.Any(r => r.DictionaryLocation == replacement.DictionaryLocation
                                            && r.Identification == replacement.Identification))
                {
                    _replacements.Add(replacement);
                }
            }
        }

        _logger.LogInformation("Collected {Count} classification replacements from entities", _replacements.Count);
    }

    /// <summary>
    /// Reads the IFC file from <paramref name="tempPath"/>, applies classification URL fixes,
    /// and writes the result to <paramref name="outputPath"/>.
    /// Deletes the temp file on success.
    /// </summary>
    public void PostProcess(string tempPath, string outputPath)
    {
        if (!File.Exists(tempPath))
            throw new FileNotFoundException("Temp IFC file not found", tempPath);

        if (!_replacements.Any())
        {
            // No replacements needed – just move the file
            if (File.Exists(outputPath)) File.Delete(outputPath);
            File.Move(tempPath, outputPath);
            _logger.LogInformation("No classification replacements needed, moved file directly");
            return;
        }

        _logger.LogInformation("Post-processing IFC file: {Path}", outputPath);

        using (var reader = new StreamReader(tempPath))
        using (var writer = new StreamWriter(outputPath))
        {
            const int batchSize = 1000;
            var batch = new List<string>(batchSize);
            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.Contains("=IFCCLASSIFICATIONREFERENCE("))
                    line = ApplyReplacements(line);

                batch.Add(line);
                if (batch.Count >= batchSize)
                {
                    WriteLines(writer, batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
                WriteLines(writer, batch);
        }

        if (!File.Exists(outputPath))
            throw new Exception("Failed to write post-processed IFC file");

        File.Delete(tempPath);
        _logger.LogInformation("Post-processing complete: {Path}", outputPath);
    }

    private string ApplyReplacements(string line)
    {
        foreach (var r in _replacements)
        {
            // Match: `'<dictionaryUrl>','<identification>`
            if (!line.Contains(r.Identification ?? string.Empty)) continue;

            var oldPattern = $"{r.DictionaryLocation}','{r.Identification}";
            var newPattern = $"{r.ClassificationLocation}','{r.Identification}";

            if (line.Contains(oldPattern))
            {
                line = line.Replace(oldPattern, newPattern);
                break;
            }
        }
        return line;
    }

    private static void WriteLines(TextWriter writer, List<string> lines)
    {
        foreach (var l in lines)
            writer.WriteLine(l);
    }
}

internal class ClassificationReplacement
{
    public string? DictionaryLocation { get; set; }
    public string? ClassificationLocation { get; set; }
    public string? Identification { get; set; }
}
