// Purpose: Loads and manages the embedded IFC.On-Track.nl UI
using System.IO;
using Microsoft.Extensions.Logging;

namespace IfcOnTrack.Core.UI;

/// <summary>
/// Manages loading the IFC.On-Track.nl UI from CDN or local bundle.
/// </summary>
public class UiLoader
{
    private const string CdnBaseUrl = "https://buildingsmart-community.github.io/bSDD-filter-UI/v1.9/";
    private const string LocalUiFolder = "ui";

    private readonly ILogger<UiLoader> _logger;
    private readonly string _pluginDirectory;

    public UiLoader(string pluginDirectory, ILogger<UiLoader> logger)
    {
        _pluginDirectory = pluginDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Get the URL/path to load the UI from.
    /// Priority: Local bundle > CDN
    /// </summary>
    /// <param name="module">UI module to load</param>
    /// <returns>URL or file path to the UI</returns>
    public string GetUiUrl(UiModule module = UiModule.Full)
    {
        var modulePath = GetModulePath(module);

        // Check for local UI bundle first
        var localPath = Path.Combine(_pluginDirectory, LocalUiFolder, modulePath, "index.html");
        if (File.Exists(localPath))
        {
            _logger.LogInformation("Loading UI from local bundle: {Path}", localPath);
            return localPath;
        }

        // Fall back to CDN
        var cdnUrl = $"{CdnBaseUrl}{modulePath}/";
        _logger.LogInformation("Loading UI from CDN: {Url}", cdnUrl);
        return cdnUrl;
    }

    /// <summary>
    /// Check if local UI bundle is available.
    /// </summary>
    public bool HasLocalUi(UiModule module = UiModule.Full)
    {
        var modulePath = GetModulePath(module);
        var localPath = Path.Combine(_pluginDirectory, LocalUiFolder, modulePath, "index.html");
        return File.Exists(localPath);
    }

    /// <summary>
    /// Get version info from local UI bundle.
    /// </summary>
    public string? GetLocalUiVersion()
    {
        var versionFile = Path.Combine(_pluginDirectory, LocalUiFolder, "version.json");
        if (!File.Exists(versionFile)) return null;

        try
        {
            var json = File.ReadAllText(versionFile);
            return json;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read UI version file");
            return null;
        }
    }

    private static string GetModulePath(UiModule module)
    {
        return module switch
        {
            UiModule.Bsdd => "bsdd",
            UiModule.BsddSearch => "bsdd_search",
            UiModule.BsddSelection => "bsdd_selection",
            UiModule.Ids => "ids",
            UiModule.Modeler => "modeler",
            UiModule.Checker => "checker",
            UiModule.Full => "full",
            _ => "full"
        };
    }
}

/// <summary>
/// Available UI modules.
/// </summary>
public enum UiModule
{
    /// <summary>bSDD search and selection</summary>
    Bsdd,
    /// <summary>bSDD search panel for modal dialogs</summary>
    BsddSearch,
    /// <summary>bSDD selection dockable panel</summary>
    BsddSelection,
    /// <summary>IDS editor and validator</summary>
    Ids,
    /// <summary>IFC data modeler</summary>
    Modeler,
    /// <summary>Model checker</summary>
    Checker,
    /// <summary>Full application with all modules</summary>
    Full
}
