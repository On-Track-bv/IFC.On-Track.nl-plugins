// Purpose: Bridge interface for communication between UI and host application
namespace IfcOnTrack.Core.Bridge;

/// <summary>
/// Interface that host applications (Revit, Tekla, etc.) must implement
/// to communicate with the embedded IFC.On-Track.nl UI.
/// </summary>
public interface IIfcOnTrackBridge
{
    /// <summary>
    /// Load application settings (dictionaries, language, preferences).
    /// </summary>
    /// <returns>JSON string with settings</returns>
    Task<string> LoadSettings();

    /// <summary>
    /// Save application settings.
    /// </summary>
    /// <param name="settingsJson">JSON string with settings to save</param>
    void SaveSettings(string settingsJson);

    /// <summary>
    /// Load bridge data (IFC entities, property mappings, etc.).
    /// </summary>
    /// <returns>JSON string with bridge data</returns>
    Task<string> LoadBridgeData();

    /// <summary>
    /// Save data back to the host application.
    /// </summary>
    /// <param name="dataJson">JSON string with data to save</param>
    /// <returns>Result message</returns>
    Task<string> Save(string dataJson);

    /// <summary>
    /// Cancel current operation and close UI.
    /// </summary>
    void Cancel();

    /// <summary>
    /// Get current license status.
    /// </summary>
    /// <returns>License status object</returns>
    Task<LicenseStatus> GetLicenseStatus();
}

/// <summary>
/// License status information.
/// </summary>
public class LicenseStatus
{
    public LicenseType Type { get; set; } = LicenseType.Community;
    public bool IsValid { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Email { get; set; }
    public string? Organization { get; set; }
    public List<string> Features { get; set; } = new();
}

/// <summary>
/// License types available.
/// </summary>
public enum LicenseType
{
    Community,
    Professional,
    Enterprise
}
