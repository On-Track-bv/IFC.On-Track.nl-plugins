// Purpose: Build configuration options
namespace Build.Options;

public sealed class BuildOptions
{
    public string Version { get; set; } = "1.0.0";

    public PluginOptions[] Plugins { get; set; } = [];
}

public sealed class PluginOptions
{
    public string Name { get; set; } = "";

    public string[] Configurations { get; set; } = [];
}
