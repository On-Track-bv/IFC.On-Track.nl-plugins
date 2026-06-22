// Purpose: Generate WixSharp features per Revit version
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using WixSharp;

namespace Installer;

public static partial class Generator
{
    /// <summary>
    ///     Generates Wix entities, features and directories for the installer.
    /// </summary>
    public static WixEntity[] GenerateWixEntities(string[] directories)
    {
        var versionStorages = new Dictionary<string, List<WixEntity>>();
        var revitFeature = new Feature
        {
            Name = "Revit Add-in",
            Description = "Revit add-in installation files",
            Display = FeatureDisplay.expand
        };

        foreach (var directory in directories)
        {
            var directoryInfo = new DirectoryInfo(directory);

            if (!TryParseVersion(directoryInfo.Name, out var fileVersion))
            {
                Console.WriteLine($"Warning: Could not parse version from directory path: {directory}");
                continue;
            }

            var feature = new Feature
            {
                Name = fileVersion,
                Description = $"Install add-in for Revit {fileVersion}",
                ConfigurableDir = $"INSTALL{fileVersion}"
            };

            revitFeature.Add(feature);

            // Create Revit addon structure:
            // - .addin file at Addins\2025\ level
            // - All other files in Addins\2025\IfcOnTrack.Revit\ subfolder
            var versionEntities = new List<WixEntity>();

            // Add .addin file to version root (Addins\2025\)
            var addinFile = Directory.GetFiles(directory, "*.addin").FirstOrDefault();
            if (addinFile != null)
            {
                versionEntities.Add(new WixSharp.File(feature, addinFile));
            }

            // Add all other files to IfcOnTrack.Revit subfolder
            var pluginFolderEntities = new List<WixEntity>();
            var rootFiles = Directory.GetFiles(directory).Where(f => !f.EndsWith(".addin", StringComparison.OrdinalIgnoreCase));
            foreach (var file in rootFiles)
            {
                pluginFolderEntities.Add(new WixSharp.File(feature, file));
            }

            // Recursively add subdirectories to IfcOnTrack.Revit subfolder
            pluginFolderEntities.AddRange(BuildDirectoryStructure(directory, directory, fileVersion, feature));

            var pluginFolder = new Dir(
                new Id($"PluginFolder_{fileVersion}"),
                "IfcOnTrack.Revit",
                pluginFolderEntities.ToArray()
            );
            versionEntities.Add(pluginFolder);

            if (versionStorages.TryGetValue(fileVersion, out var storage))
            {
                storage.AddRange(versionEntities);
            }
            else
            {
                versionStorages.Add(fileVersion, versionEntities);
            }

            LogFeatureFiles(directory, fileVersion);
        }

        return versionStorages
            .Select(storage => new Dir(new Id($"INSTALL{storage.Key}"), storage.Key, storage.Value.ToArray()))
            .Cast<WixEntity>()
            .ToArray();
    }

    /// <summary>
    ///     Recursively build directory structure for WixSharp without using wildcards.
    ///     This avoids issues with dots in directory names like "Release.R25".
    /// </summary>
    private static List<WixEntity> BuildDirectoryStructure(string rootBaseDirectory, string currentDirectory, string fileVersion, Feature feature)
    {
        var entities = new List<WixEntity>();
        var subdirectories = Directory.GetDirectories(currentDirectory);

        foreach (var subdirectory in subdirectories)
        {
            var subdirName = Path.GetFileName(subdirectory);
            var subdirEntities = new List<WixEntity>();

            // Add files in this subdirectory individually
            var files = Directory.GetFiles(subdirectory);
            foreach (var file in files)
            {
                subdirEntities.Add(new WixSharp.File(feature, file));
            }

            // Recursively add nested subdirectories
            subdirEntities.AddRange(BuildDirectoryStructure(rootBaseDirectory, subdirectory, fileVersion, feature));

            // Create directory entity if it has any content
            if (subdirEntities.Count > 0)
            {
                // Create safe ID using full relative path from root to ensure uniqueness
                var relativePath = Path.GetRelativePath(rootBaseDirectory, subdirectory);
                var safeId = relativePath
                    .Replace("\\", "_")
                    .Replace(".", "_")
                    .Replace("-", "_");
                entities.Add(new Dir(
                    new Id($"Dir_{fileVersion}_{safeId}"),
                    subdirName,
                    subdirEntities.ToArray()
                ));
            }
        }

        return entities;
    }

    /// <summary>
    ///     Parse a Revit version string from the given input (e.g., "R25" -> "2025", "2025" -> "2025").
    /// </summary>
    private static bool TryParseVersion(string input, [NotNullWhen(true)] out string? version)
    {
        version = null;

        // Try to match "R25", "R26", etc.
        var rMatch = RevitRVersionRegex().Match(input);
        if (rMatch.Success)
        {
            version = $"20{rMatch.Groups[1].Value}";
            return true;
        }

        // Try to match "2025", "2026", etc.
        var fullMatch = FullVersionRegex().Match(input);
        if (fullMatch.Success)
        {
            version = fullMatch.Value;
            return true;
        }

        return false;
    }

    /// <summary>
    ///    Write a list of installer files.
    /// </summary>
    private static void LogFeatureFiles(string directory, string fileVersion)
    {
        var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
        Console.WriteLine($"Installer files for Revit {fileVersion}:");

        foreach (var file in files)
        {
            Console.WriteLine($"  - {Path.GetRelativePath(directory, file)}");
        }
    }

    /// <summary>
    ///     Regex to match "R25", "R26", etc.
    /// </summary>
    [GeneratedRegex(@"R(\d{2})")]
    private static partial Regex RevitRVersionRegex();

    /// <summary>
    ///     Regex to match full year versions like "2025", "2026", etc.
    /// </summary>
    [GeneratedRegex(@"20\d{2}")]
    private static partial Regex FullVersionRegex();
}
