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

// Default: compile only
if (args.Length == 0)
{
    builder.Services.AddModule<CompileModule>();
}

// Clean only
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

// Package (compile + package)
if (args.Contains("pack") || args.Contains("package"))
{
    builder.Services.AddModule<CompileModule>();
    builder.Services.AddModule<PackageModule>();
}

// Installer (compile + installer)
if (args.Contains("installer"))
{
    builder.Services.AddModule<CompileModule>();
    builder.Services.AddModule<CreateInstallerModule>();
}

// Full release (clean + compile + package + installer)
if (args.Contains("release"))
{
    builder.Services.AddModule<CleanModule>();
    builder.Services.AddModule<CompileModule>();
    builder.Services.AddModule<PackageModule>();
    builder.Services.AddModule<CreateInstallerModule>();
}

await builder.Build().RunAsync();
