// Purpose: NUKE compile target
using System.IO.Enumeration;
using Nuke.Common.Tools.DotNet;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

sealed partial class Build
{
    Target Compile => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            foreach (var configuration in GlobBuildConfigurations())
            {
                Serilog.Log.Information("Building configuration: {Configuration}", configuration);
                
                DotNetBuild(settings => settings
                    .SetProjectFile(Solution)
                    .SetConfiguration(configuration)
                    .SetVersion(Version)
                    .SetVerbosity(DotNetVerbosity.minimal));
            }
        });

    List<string> GlobBuildConfigurations()
    {
        var configurations = Solution.Configurations
            .Select(pair => pair.Key)
            .Select(config => config.Remove(config.LastIndexOf('|')))
            .Distinct()
            .Where(config => Configurations.Any(wildcard => 
                FileSystemName.MatchesSimpleExpression(wildcard, config)))
            .ToList();

        Assert.NotEmpty(configurations, 
            $"No solution configurations found. Pattern: {string.Join(" | ", Configurations)}");
        
        return configurations;
    }
}
