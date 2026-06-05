// Purpose: Clean build artifacts and outputs
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

public sealed class CleanModule : SyncModule
{
    protected override void ExecuteModule(IModuleContext context, CancellationToken cancellationToken)
    {
        var rootDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var artifactsDirectory = Path.Combine(rootDirectory, "output");
        var sourceDirectory = Path.Combine(rootDirectory, "source", "dotnet");

        // Clean artifacts directory
        if (Directory.Exists(artifactsDirectory))
        {
            Directory.Delete(artifactsDirectory, recursive: true);
        }
        Directory.CreateDirectory(artifactsDirectory);

        // Clean bin/obj folders in source
        if (Directory.Exists(sourceDirectory))
        {
            foreach (var dir in Directory.GetDirectories(sourceDirectory, "bin", SearchOption.AllDirectories))
            {
                Directory.Delete(dir, recursive: true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDirectory, "obj", SearchOption.AllDirectories))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
