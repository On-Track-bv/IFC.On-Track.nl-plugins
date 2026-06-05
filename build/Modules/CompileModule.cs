// Purpose: Compile solution projects
using Build.Options;
using Microsoft.Extensions.Options;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.DotNet.Extensions;
using ModularPipelines.DotNet.Options;
using ModularPipelines.Git.Extensions;
using ModularPipelines.Modules;

namespace Build.Modules;

[DependsOn<CleanModule>]
public sealed class CompileModule(IOptions<BuildOptions> options) : Module
{
    protected override async Task ExecuteModuleAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        // Use Git root directory
        var rootDirectory = context.Git().RootDirectory;
        var sourceDirectory = Path.Combine(rootDirectory.Path, "source", "dotnet");

        // Get all .csproj files (excluding build project)
        var projects = Directory.GetFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories);

        // Get version from environment variable or use default
        var version = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? options.Value.Version;

        foreach (var configuration in options.Value.Configurations)
        {
            foreach (var project in projects)
            {
                await context.DotNet().Build(new DotNetBuildOptions
                {
                    ProjectSolution = project,
                    Configuration = configuration,
                    Properties =
                    [
                        ("Version", version)
                    ]
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
