// Purpose: Create MSI installer(s) using WixSharp, one per plugin
using System.Diagnostics;
using Build.Options;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace Build.Modules;

[DependsOn<PackageModule>]
public sealed class CreateInstallerModule(IOptions<BuildOptions> options) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var rootDirectory = context.Git().RootDirectory;
        var sourceDirectory = Path.Combine(rootDirectory.Path, "source", "dotnet");
        var version = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? options.Value.Version;

        await InstallWixToolsetAsync(cancellationToken);

        foreach (var plugin in options.Value.Plugins)
        {
            var installerProject = Path.Combine(rootDirectory.Path, "install", plugin.Name, "Installer.csproj");

            // Fall back to root install/Installer.csproj for backwards compatibility
            if (!File.Exists(installerProject))
                installerProject = Path.Combine(rootDirectory.Path, "install", "Installer.csproj");

            if (!File.Exists(installerProject))
                continue;

            var binDirectories = plugin.Configurations
                .Select(c => Path.Combine(sourceDirectory, $"IfcOnTrack.{plugin.Name}", "bin", c))
                .Where(Directory.Exists)
                .ToList();

            if (binDirectories.Count == 0)
                throw new InvalidOperationException($"No build outputs found for plugin '{plugin.Name}'. Run compile first.");

            var arguments = $"run --project \"{installerProject}\" -- \"{version}\" "
                + string.Join(" ", binDirectories.Select(d => $"\"{d}\""));

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = rootDirectory.Path,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start installer process");

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Installer for '{plugin.Name}' failed (exit {process.ExitCode})\n{output}\n{error}");
        }

        var outputFolder = Path.Combine(rootDirectory.Path, "output");
        var msiFiles = Directory.GetFiles(outputFolder, "*.msi");

        if (msiFiles.Length == 0)
            throw new FileNotFoundException("No MSI files were created.");
    }

    private static async Task InstallWixToolsetAsync(CancellationToken cancellationToken)
    {
        var checkProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "tool list --global",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        var isInstalled = false;
        if (checkProcess != null)
        {
            var output = await checkProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            await checkProcess.WaitForExitAsync(cancellationToken);
            isInstalled = output.Contains("wix");
        }

        if (!isInstalled)
        {
            var install = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "tool install --global wix --version 7.*",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (install != null) await install.WaitForExitAsync(cancellationToken);
        }

        var eula = Process.Start(new ProcessStartInfo
        {
            FileName = "wix",
            Arguments = "eula accept wix7",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (eula != null) await eula.WaitForExitAsync(cancellationToken);

        var versionProc = Process.Start(new ProcessStartInfo
        {
            FileName = "wix",
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        var wixVersion = "7.0.0";
        if (versionProc != null)
        {
            var versionOutput = await versionProc.StandardOutput.ReadToEndAsync(cancellationToken);
            await versionProc.WaitForExitAsync(cancellationToken);
            var match = System.Text.RegularExpressions.Regex.Match(versionOutput, @"^(\d+\.\d+\.\d+)");
            if (match.Success) wixVersion = match.Groups[1].Value;
        }

        var ext = Process.Start(new ProcessStartInfo
        {
            FileName = "wix",
            Arguments = $"extension add -g WixToolset.UI.wixext/{wixVersion}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (ext != null) await ext.WaitForExitAsync(cancellationToken);
    }
}
