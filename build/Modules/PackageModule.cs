// Purpose: Package build outputs into distributable archives per plugin
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
        var version = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? options.Value.Version;

        Directory.CreateDirectory(outputDirectory);

        foreach (var plugin in options.Value.Plugins)
        {
            var pluginBinRoot = Path.Combine(sourceDirectory, $"IfcOnTrack.{plugin.Name}", "bin");

            // Per-configuration ZIPs (e.g. IfcOnTrack.Revit-v1.2.3-R25.zip)
            foreach (var configuration in plugin.Configurations)
            {
                var binPath = Path.Combine(pluginBinRoot, configuration);
                if (!Directory.Exists(binPath))
                    continue;

                var suffix = configuration.Split('.').Last();
                var zipPath = Path.Combine(outputDirectory, $"IfcOnTrack.{plugin.Name}-v{version}-{suffix}.zip");

                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                ZipFile.CreateFromDirectory(binPath, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }

            // Combined ZIP with all versions (e.g. IfcOnTrack.Revit-v1.2.3-All.zip)
            var combinedZipPath = Path.Combine(outputDirectory, $"IfcOnTrack.{plugin.Name}-v{version}-All.zip");

            if (File.Exists(combinedZipPath))
                File.Delete(combinedZipPath);

            using var archive = ZipFile.Open(combinedZipPath, ZipArchiveMode.Create);

            foreach (var configuration in plugin.Configurations)
            {
                var binPath = Path.Combine(pluginBinRoot, configuration);
                if (!Directory.Exists(binPath))
                    continue;

                var suffix = configuration.Split('.').Last();

                foreach (var file in Directory.GetFiles(binPath, "*", SearchOption.AllDirectories))
                {
                    var relativePath = Path.GetRelativePath(binPath, file);
                    var entryName = Path.Combine(suffix, relativePath).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                }
            }
        }

        await Task.CompletedTask;
    }
}
