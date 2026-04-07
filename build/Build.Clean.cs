// Purpose: NUKE clean target
using Nuke.Common.IO;

sealed partial class Build
{
    Target Clean => _ => _
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            
            // Clean bin/obj folders in source
            var sourceDir = RootDirectory / "source";
            sourceDir.GlobDirectories("**/bin", "**/obj")
                .ForEach(dir => dir.DeleteDirectory());
        });
}
