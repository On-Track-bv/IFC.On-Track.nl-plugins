// Purpose: NUKE build configuration
sealed partial class Build
{
    const string DefaultVersion = "1.0.0";
    string Version => Environment.GetEnvironmentVariable("BUILD_VERSION") ?? DefaultVersion;
    
    readonly AbsolutePath ArtifactsDirectory = RootDirectory / "output";
    readonly AbsolutePath ChangeLogPath = RootDirectory / "CHANGELOG.md";

    protected override void OnBuildInitialized()
    {
        Configurations = new[]
        {
            "Release*"
        };
    }
}
