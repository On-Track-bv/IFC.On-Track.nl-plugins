// Purpose: License validation and management
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IfcOnTrack.Core.Bridge;

namespace IfcOnTrack.Core.License;

/// <summary>
/// Manages license validation for IFC.On-Track.nl plugins.
/// </summary>
public class LicenseManager
{
    private const string LicenseFileName = ".ifc-ontrack-license";
    private readonly ILogger<LicenseManager> _logger;
    private readonly string _licenseDirectory;
    private LicenseFile? _cachedLicense;

    public LicenseManager(ILogger<LicenseManager> logger)
    {
        _logger = logger;
        _licenseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IFC.On-Track.nl"
        );
    }

    /// <summary>
    /// Get current license status.
    /// </summary>
    public async Task<LicenseStatus> GetLicenseStatus()
    {
        var license = await LoadLicense();
        
        if (license == null)
        {
            return new LicenseStatus
            {
                Type = LicenseType.Community,
                IsValid = true, // Community is always valid
                Features = new List<string> { "bsdd-search", "bsdd-link", "ifc-export" }
            };
        }

        return new LicenseStatus
        {
            Type = license.Type,
            IsValid = !license.IsExpired(),
            ExpiresAt = license.ValidUntil,
            Email = license.Email,
            Organization = license.Organization,
            Features = license.Features ?? new List<string>()
        };
    }

    /// <summary>
    /// Check if a specific feature is available.
    /// </summary>
    public async Task<bool> HasFeature(string featureName)
    {
        var status = await GetLicenseStatus();
        return status.Features.Contains(featureName, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if current license is Pro or higher.
    /// </summary>
    public async Task<bool> IsPro()
    {
        var status = await GetLicenseStatus();
        return status.Type is LicenseType.Professional or LicenseType.Enterprise && status.IsValid;
    }

    /// <summary>
    /// Activate a license with a key.
    /// </summary>
    public async Task<bool> ActivateLicense(string licenseKey)
    {
        try
        {
            // TODO: Implement online license activation
            _logger.LogInformation("Activating license...");
            
            // Placeholder - would call license server
            await Task.Delay(100);
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate license");
            return false;
        }
    }

    private Task<LicenseFile?> LoadLicense()
    {
        if (_cachedLicense != null) return Task.FromResult<LicenseFile?>(_cachedLicense);

        var licensePath = Path.Combine(_licenseDirectory, LicenseFileName);
        if (!File.Exists(licensePath))
        {
            _logger.LogDebug("No license file found at {Path}", licensePath);
            return Task.FromResult<LicenseFile?>(null);
        }

        try
        {
            var json = File.ReadAllText(licensePath);
            _cachedLicense = JsonConvert.DeserializeObject<LicenseFile>(json);
            _logger.LogInformation("Loaded license for {Email}", _cachedLicense?.Email);
            return Task.FromResult(_cachedLicense);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load license file");
            return Task.FromResult<LicenseFile?>(null);
        }
    }
}

/// <summary>
/// License file structure.
/// </summary>
internal class LicenseFile
{
    [JsonProperty("type")]
    public LicenseType Type { get; set; }

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("organization")]
    public string? Organization { get; set; }

    [JsonProperty("validUntil")]
    public DateTime? ValidUntil { get; set; }

    [JsonProperty("features")]
    public List<string>? Features { get; set; }

    [JsonProperty("signature")]
    public string? Signature { get; set; }

    public bool IsExpired()
    {
        return ValidUntil.HasValue && ValidUntil.Value < DateTime.UtcNow;
    }
}
