// Purpose: Package build outputs into distributable archives
using System.IO.Compression;
using Build.Options;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace Build.Modules;

[DependsOn<CompileModule>]
public sealed class PackageModule(IOptions<BuildOptions> options) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var rootDirectory = context.Git().RootDirectory;
        var outputDirectory = Path.Combine(rootDirectory.Path, "output");
        var sourceDirectory = Path.Combine(rootDirectory.Path, "source", "dotnet");

        // Ensure output directory exists
        Directory.CreateDirectory(outputDirectory);

        // Get version
        var version = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? options.Value.Version;

        foreach (var configuration in options.Value.Configurations)
        {
            // Extract Revit version (e.g., "R25" from "Release.R25")
            var revitVersion = configuration.Split('.').Last();

            // Source: bin folder of Revit plugin
            var binPath = Path.Combine(sourceDirectory, "IfcOnTrack.Revit", "bin", configuration);

            if (!Directory.Exists(binPath))
            {
                continue;
            }

            // Target ZIP file
            var zipFileName = $"IfcOnTrack.Revit-v{version}-{revitVersion}.zip";
            var zipPath = Path.Combine(outputDirectory, zipFileName);

            // Delete existing ZIP if present
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            // Create ZIP from bin folder
            ZipFile.CreateFromDirectory(binPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
        }

        // Create combined package with both versions
        var combinedZipName = $"IfcOnTrack.Revit-v{version}-All.zip";
        var combinedZipPath = Path.Combine(outputDirectory, combinedZipName);

        if (File.Exists(combinedZipPath))
        {
            File.Delete(combinedZipPath);
        }

        using (var archive = ZipFile.Open(combinedZipPath, ZipArchiveMode.Create))
        {
            foreach (var configuration in options.Value.Configurations)
            {
                var revitVersion = configuration.Split('.').Last();
                var binPath = Path.Combine(sourceDirectory, "IfcOnTrack.Revit", "bin", configuration);

                if (!Directory.Exists(binPath))
                    continue;

                // Add files to ZIP with subfolder per Revit version
                foreach (var file in Directory.GetFiles(binPath, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(binPath, file);
                    var entryName = Path.Combine(revitVersion, relativePath).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
        }

        await Task.CompletedTask;
    }
}
