// Purpose: Create MSI installer using WixSharp
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
        var installerProject = Path.Combine(rootDirectory.Path, "install", "Installer.csproj");
        var sourceDirectory = Path.Combine(rootDirectory.Path, "source", "dotnet");

        // Get version
        var version = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? options.Value.Version;

        // Collect bin directories for each Revit version (bin root, not publish subfolder)
        // The Nice3point.Revit.Sdk publishes files with a specific structure that includes .addin
        var binDirectories = new List<string>();

        foreach (var configuration in options.Value.Configurations)
        {
            var binPath = Path.Combine(sourceDirectory, "IfcOnTrack.Revit", "bin", configuration);

            if (Directory.Exists(binPath))
            {
                binDirectories.Add(binPath);
            }
        }

        if (binDirectories.Count == 0)
        {
            throw new InvalidOperationException("No build outputs found. Run compile first.");
        }

        // Install WiX toolset globally and configure it
        await InstallWixToolsetAsync(cancellationToken);

        // Build arguments: version + all bin directories
        var arguments = $"run --project \"{installerProject}\" -- \"{version}\" " + string.Join(" ", binDirectories.Select(d => $"\"{d}\""));

        // Run installer via dotnet CLI
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = rootDirectory.Path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start installer process");
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Installer creation failed with exit code {process.ExitCode}\nOutput: {output}\nError: {error}");
        }

        // Verify MSI files were created
        var outputFolder = Path.Combine(rootDirectory.Path, "output");
        var msiFiles = Directory.GetFiles(outputFolder, "*.msi");

        if (msiFiles.Length == 0)
        {
            throw new FileNotFoundException($"No MSI files were created. WiX output:\n{output}");
        }
    }

    private static async Task<string> InstallWixToolsetAsync(CancellationToken cancellationToken)
    {
        // Check if WiX is already installed globally
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

        // Install WiX globally if not present
        if (!isInstalled)
        {
            var installProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "tool install --global wix --version 7.*",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (installProcess != null)
            {
                await installProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                await installProcess.WaitForExitAsync(cancellationToken);
            }
        }

        // Accept EULA (idempotent)
        var eulaProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "wix",
            Arguments = "eula accept wix7",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (eulaProcess != null)
        {
            await eulaProcess.WaitForExitAsync(cancellationToken);
        }

        // Get WiX version
        var versionProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "wix",
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        var wixVersion = "7.0.0"; // Default fallback
        if (versionProcess != null)
        {
            var versionOutput = await versionProcess.StandardOutput.ReadToEndAsync(cancellationToken);
            await versionProcess.WaitForExitAsync(cancellationToken);

            // Parse version like "7.0.0+b8977d6"
            var match = System.Text.RegularExpressions.Regex.Match(versionOutput, @"^(\d+\.\d+\.\d+)");
            if (match.Success)
            {
                wixVersion = match.Groups[1].Value;
            }
        }

        // Install UI extension (use exact version)
        var extensionProcess = Process.Start(new ProcessStartInfo
        {
            FileName = "wix",
            Arguments = $"extension add -g WixToolset.UI.wixext/{wixVersion}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });

        if (extensionProcess != null)
        {
            await extensionProcess.WaitForExitAsync(cancellationToken);
        }

        // Return empty string since we're using global tool (PATH will find it)
        return string.Empty;
    }
}
