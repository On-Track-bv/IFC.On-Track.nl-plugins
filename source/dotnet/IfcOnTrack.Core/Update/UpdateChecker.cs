// Purpose: Checks for plugin updates from GitHub Releases
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Core.Update;

/// <summary>
/// Checks for available plugin updates from GitHub Releases.
/// </summary>
public class UpdateChecker
{
    private readonly ILogger<UpdateChecker> _logger;
    private readonly HttpClient _httpClient;

    private const string GitHubApiUrl = "https://api.github.com/repos/On-Track-bv/IFC.On-Track.nl-plugins/releases/latest";

    public UpdateChecker(ILogger<UpdateChecker> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "IFC.On-Track.nl-Revit-Plugin");
    }

    /// <summary>
    /// Check if a newer version is available.
    /// </summary>
    /// <param name="currentVersion">Current plugin version (e.g., "1.0.0")</param>
    /// <returns>Update information if available, null if up-to-date or check failed</returns>
    public async Task<UpdateInfo?> CheckForUpdateAsync(string currentVersion)
    {
        try
        {
            _logger.LogInformation("Checking for updates. Current version: {Version}", currentVersion);

            var release = await _httpClient.GetFromJsonAsync<GitHubRelease>(GitHubApiUrl);
            if (release == null)
            {
                _logger.LogWarning("Failed to fetch latest release information");
                return null;
            }

            var latestVersion = release.TagName?.TrimStart('v');
            if (string.IsNullOrEmpty(latestVersion))
            {
                _logger.LogWarning("Latest release has no version tag");
                return null;
            }

            if (IsNewerVersion(currentVersion, latestVersion))
            {
                _logger.LogInformation("New version available: {LatestVersion}", latestVersion);
                return new UpdateInfo
                {
                    LatestVersion = latestVersion,
                    ReleaseUrl = release.HtmlUrl,
                    ReleaseNotes = release.Body,
                    PublishedAt = release.PublishedAt
                };
            }

            _logger.LogInformation("Plugin is up-to-date");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return null;
        }
    }

    private bool IsNewerVersion(string current, string latest)
    {
        if (!Version.TryParse(current, out var currentVer))
            return false;

        if (!Version.TryParse(latest, out var latestVer))
            return false;

        return latestVer > currentVer;
    }
}

/// <summary>
/// Information about an available update.
/// </summary>
public class UpdateInfo
{
    public string LatestVersion { get; init; } = "";
    public string? ReleaseUrl { get; init; }
    public string? ReleaseNotes { get; init; }
    public DateTime? PublishedAt { get; init; }
}

/// <summary>
/// GitHub Release API response model.
/// </summary>
internal class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("body")]
    public string? Body { get; set; }
    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }
    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }
}
