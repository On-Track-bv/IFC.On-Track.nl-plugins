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

            // Create Files with unique ID prefix to avoid duplicates across versions
            // Wrap in a subfolder to match Revit's expected structure: Addins\2025\IfcOnTrack.Revit\
            var pluginFolder = new Dir(
                new Id($"PluginFolder_{fileVersion}"),
                "IfcOnTrack.Revit",
                new Files(feature, $@"{directory}\*.*")
                {
                    Id = new Id($"Files_{fileVersion}")
                },
                // Include subdirectories (publish, runtimes, UI, etc.)
                new DirFiles(feature, $@"{directory}\*\*.*")
                {
                    Id = new Id($"SubdirFiles_{fileVersion}")
                }
            );

            if (versionStorages.TryGetValue(fileVersion, out var storage))
            {
                storage.Add(pluginFolder);
            }
            else
            {
                versionStorages.Add(fileVersion, [pluginFolder]);
            }

            LogFeatureFiles(directory, fileVersion);
        }

        return versionStorages
            .Select(storage => new Dir(new Id($"INSTALL{storage.Key}"), storage.Key, storage.Value.ToArray()))
            .Cast<WixEntity>()
            .ToArray();
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
