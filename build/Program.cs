// Purpose: ModularPipelines build entry point
using Build.Modules;
using Build.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;

var builder = Pipeline.CreateBuilder();

builder.Configuration.AddJsonFile("appsettings.json");
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

builder.Services.AddOptions<BuildOptions>().Bind(builder.Configuration.GetSection("Build"));

// Default: just compile
if (args.Length == 0)
{
    builder.Services.AddModule<CompileModule>();
}

// Clean build
if (args.Contains("clean"))
{
    builder.Services.AddModule<CleanModule>();
}

// Full build (clean + compile)
if (args.Contains("build"))
{
    builder.Services.AddModule<CleanModule>();
    builder.Services.AddModule<CompileModule>();
}

await builder.Build().RunAsync();
