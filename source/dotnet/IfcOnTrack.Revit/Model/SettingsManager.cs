// Purpose: Manages bSDD settings storage in Revit document
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using IfcOnTrack.Core.Bridge;

namespace IfcOnTrack.Revit.Model;

/// <summary>
/// Manages bSDD settings stored in Revit document ExtensibleStorage.
/// </summary>
public class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    
    // Schema GUID for settings storage (unique for IFC.On-Track.nl plugin)
    // Different from old bSDD-Revit-plugin to avoid conflicts
    private static readonly Guid SettingsSchemaGuid = new("A3F8E2D1-4B9C-4E7A-8F2D-1C5B9A7E3D6F");
    
    // Cached settings
    private BridgeSettings? _cachedSettings;

    public SettingsManager(ILogger<SettingsManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get current settings (from cache or defaults).
    /// </summary>
    public BridgeSettings GetSettings()
    {
        return _cachedSettings ?? GetDefaultSettings();
    }

    /// <summary>
    /// Load settings from document storage. Always reads fresh from DataStorage.
    /// </summary>
    public BridgeSettings LoadSettings(Document doc)
    {
        try
        {
            var schema = Schema.Lookup(SettingsSchemaGuid);
            if (schema == null)
            {
                _logger.LogDebug("Settings schema not found, using defaults");
                return GetDefaultSettings();
            }

            // Find DataStorage element with our settings
            var dataStorage = FindSettingsStorage(doc, schema);
            if (dataStorage == null)
            {
                _logger.LogDebug("Settings storage not found, using defaults");
                return GetDefaultSettings();
            }

            var entity = dataStorage.GetEntity(schema);
            if (!entity.IsValid())
                return GetDefaultSettings();

            var json = entity.Get<string>("SettingsJson");
            if (string.IsNullOrEmpty(json))
                return GetDefaultSettings();

            _logger.LogInformation("Loaded settings from document");
            var loaded = JsonConvert.DeserializeObject<BridgeSettings>(json) ?? GetDefaultSettings();
            // Ensure FilterDictionaries is never null — JS calls .map() on it
            loaded.FilterDictionaries ??= new List<BsddDictionary>();
            _cachedSettings = loaded;
            return loaded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
            return GetDefaultSettings();
        }
    }

    /// <summary>
    /// Save settings to document storage. Call this INSIDE an active transaction.
    /// </summary>
    public void SaveSettings(Document doc, BridgeSettings settings)
    {
        try
        {
            var schema = GetOrCreateSettingsSchema();
            var dataStorage = GetOrCreateSettingsStorage(doc, schema);

            var entity = new Entity(schema);
            var json = JsonConvert.SerializeObject(settings);
            entity.Set("SettingsJson", json);
            dataStorage.SetEntity(entity);

            _cachedSettings = settings;
            _logger.LogInformation("Saved settings to document");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
        }
    }

    /// <summary>
    /// Clear cached settings (call when document changes).
    /// </summary>
    public void ClearCache()
    {
        _cachedSettings = null;
    }

    private DataStorage? FindSettingsStorage(Document doc, Schema schema)
    {
        var collector = new FilteredElementCollector(doc)
            .OfClass(typeof(DataStorage));

        foreach (var element in collector)
        {
            var dataStorage = (DataStorage)element;
            var entity = dataStorage.GetEntity(schema);
            if (entity.IsValid())
            {
                return dataStorage;
            }
        }

        return null;
    }

    private DataStorage GetOrCreateSettingsStorage(Document doc, Schema schema)
    {
        var existing = FindSettingsStorage(doc, schema);
        if (existing != null) return existing;

        var dataStorage = DataStorage.Create(doc);
        dataStorage.Name = "IfcOnTrack_Settings_v2";
        return dataStorage;
    }

    private Schema GetOrCreateSettingsSchema()
    {
        var schema = Schema.Lookup(SettingsSchemaGuid);
        if (schema != null) return schema;

        var builder = new SchemaBuilder(SettingsSchemaGuid);
        builder.SetSchemaName("IfcOnTrackSettings_v2");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.SetVendorId("IFC.On-Track.nl");
        builder.AddSimpleField("SettingsJson", typeof(string));
        
        return builder.Finish();
    }

    private static BridgeSettings GetDefaultSettings()
    {
        try
        {
            // Load default settings from BsddSettings.json file
            var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyPath == null)
            {
                Log.Warning("Could not determine assembly path, using hardcoded defaults");
                return CreateHardcodedDefaults();
            }

            var settingsFilePath = Path.Combine(assemblyPath, "UI", "Settings", "BsddSettings.json");

            if (!File.Exists(settingsFilePath))
            {
                // Fallback: also check directly in the assembly directory (in case UI/Settings structure differs)
                settingsFilePath = Path.Combine(assemblyPath, "BsddSettings.json");
            }

            if (File.Exists(settingsFilePath))
            {
                Log.Information("Loading default settings from: {Path}", settingsFilePath);
                var json = File.ReadAllText(settingsFilePath);
                var settings = JsonConvert.DeserializeObject<BridgeSettings>(json);

                if (settings != null)
                {
                    // Ensure FilterDictionaries is never null — the JS side calls .map() on this array
                    settings.FilterDictionaries ??= new List<BsddDictionary>();
                    Log.Information("Successfully loaded default settings from JSON file");
                    return settings;
                }

                Log.Warning("Failed to deserialize settings from {Path}, using hardcoded defaults", settingsFilePath);
            }
            else
            {
                Log.Warning("BsddSettings.json not found at {Path}, using hardcoded defaults", settingsFilePath);
            }
        }
        catch (Exception ex)
        {
            // If file loading fails, fall back to hardcoded defaults
            Log.Error(ex, "Error loading default settings from JSON file, using hardcoded defaults");
        }

        return CreateHardcodedDefaults();
    }

    /// <summary>
    /// Hardcoded fallback defaults if BsddSettings.json cannot be loaded.
    /// </summary>
    private static BridgeSettings CreateHardcodedDefaults()
    {
        return new BridgeSettings
        {
            BsddApiEnvironment = "production",
            Language = "nl-NL",
            IncludeTestDictionaries = false,
            // Default main dictionary: NL-SfB Tabel 1 (2021)
            MainDictionary = new BsddDictionary
            {
                IfcClassification = new IfcClassification
                {
                    Source = "Ketenstandaard Bouw en Techniek",
                    Edition = "2021",
                    EditionDate = new DateTime(2024, 12, 18),
                    Name = "NL-SfB Tabel 1",
                    Location = "https://data.ketenstandaard.nl/publications/nlsfb/2021",
                    Description = "Nederlandse classificatie volgens NL-SfB Tabel 1"
                }
            },
            // Default IFC dictionary: IFC 4.3
            IfcDictionary = new BsddDictionary
            {
                IfcClassification = new IfcClassification
                {
                    Source = "buildingSMART International",
                    Edition = "4.3",
                    Name = "IFC",
                    Location = "https://identifier.buildingsmart.org/uri/buildingsmart/ifc/4.3",
                    Description = "Industry Foundation Classes version 4.3"
                }
            },
            // Must be an empty list, not null — the JS side calls .map() on this array
            // and null.map() throws a TypeError that silently breaks the settings slice,
            // leaving includeTestDictionaries as undefined → fetchDictionaries never fires.
            FilterDictionaries = new List<BsddDictionary>()
        };
    }
}
