// Purpose: Build configuration options
namespace Build.Options;

public sealed class BuildOptions
{
    public string Version { get; set; } = "1.0.0";

    public string[] Configurations { get; set; } = ["Release.R25", "Release.R26"];
}
