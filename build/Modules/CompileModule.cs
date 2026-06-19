// Purpose: Compile plugin projects (Core is built automatically as a dependency)
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
        var rootDirectory = context.Git().RootDirectory;
        var sourceDirectory = Path.Combine(rootDirectory.Path, "source", "dotnet");
        var version = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? options.Value.Version;

        foreach (var plugin in options.Value.Plugins)
        {
            var pluginProject = Path.Combine(sourceDirectory, $"IfcOnTrack.{plugin.Name}", $"IfcOnTrack.{plugin.Name}.csproj");

            foreach (var configuration in plugin.Configurations)
            {
                await context.DotNet().Build(new DotNetBuildOptions
                {
                    ProjectSolution = pluginProject,
                    Configuration = configuration,
                    Properties = [("Version", version)]
                }, cancellationToken: cancellationToken);
            }
        }
    }
}
