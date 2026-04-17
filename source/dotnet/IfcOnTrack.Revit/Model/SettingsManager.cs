// Purpose: Manages bSDD settings storage in Revit document
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using IfcOnTrack.Core.Bridge;

namespace IfcOnTrack.Revit.Model;

/// <summary>
/// Manages bSDD settings stored in Revit document ExtensibleStorage.
/// </summary>
public class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    
    // Schema GUID for settings storage
    private static readonly Guid SettingsSchemaGuid = new("F7D6DC5C-9521-49E4-B6D8-6F50252E9D73");
    
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
            return JsonConvert.DeserializeObject<BridgeSettings>(json) ?? GetDefaultSettings();
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
        dataStorage.Name = "IfcOnTrack_BsddSettings";
        return dataStorage;
    }

    private Schema GetOrCreateSettingsSchema()
    {
        var schema = Schema.Lookup(SettingsSchemaGuid);
        if (schema != null) return schema;

        var builder = new SchemaBuilder(SettingsSchemaGuid);
        builder.SetSchemaName("IfcOnTrackSettings");
        builder.SetReadAccessLevel(AccessLevel.Public);
        builder.SetWriteAccessLevel(AccessLevel.Public);
        builder.SetVendorId("IFC.On-Track.nl");
        builder.AddSimpleField("SettingsJson", typeof(string));
        
        return builder.Finish();
    }

    private static BridgeSettings GetDefaultSettings()
    {
        return new BridgeSettings
        {
            BsddApiEnvironment = "production",
            Language = "nl-NL",
            IncludeTestDictionaries = false
        };
    }
}
