# BsddSettings.json

This file contains the **default settings** that are loaded when:
- A new Revit document is created
- A document is opened that has no saved settings
- Settings cannot be loaded from the document's ExtensibleStorage

## Purpose

Having default settings in a JSON file (rather than hardcoded in C#) allows:

1. **Easy updates** - Change default dictionaries without recompiling
2. **Customization** - Admins can deploy custom defaults to teams
3. **Consistency** - Share same defaults with web UI configuration

## Structure

```json
{
  "bsddApiEnvironment": "production",     // or "test" for bSDD test API
  "language": "nl-NL",                    // Default UI language
  "mainDictionary": {                     // Primary classification dictionary
    "ifcClassification": {
      "type": "IfcClassification",
      "name": "NL-SfB 2005 tabel 1",
      "location": "https://identifier.buildingsmart.org/uri/digibase/nlsfb-2005/0.1"
    }
  },
  "ifcDictionary": {                      // IFC property dictionary
    "ifcClassification": {
      "type": "IfcClassification",
      "name": "IFC",
      "location": "https://identifier.buildingsmart.org/uri/buildingsmart/ifc/4.3"
    }
  },
  "filterDictionaries": [],               // Additional filter dictionaries
  "includeTestDictionaries": false        // Show test dictionaries in UI
}
```

## Updating Dictionaries

To change the default dictionaries, edit the `location` URIs:

**Common bSDD dictionary URIs:**
- NL-SfB Tabel 1 (2021): `https://data.ketenstandaard.nl/publications/nlsfb/2021`
- IFC 4.3: `https://identifier.buildingsmart.org/uri/buildingsmart/ifc/4.3`
- IFC 2x3: `https://identifier.buildingsmart.org/uri/buildingsmart/ifc/2.3`
- ETIM 9.0: `https://identifier.buildingsmart.org/uri/etim/etim/9.0`
- Omniclass: `https://identifier.buildingsmart.org/uri/csi/omniclass/1.0`

**Note:** Dictionary URIs are case-sensitive and must match exactly with the bSDD API.

## Deployment

This file is automatically copied to the plugin output directory during build via:

```xml
<ItemGroup>
    <None Include="UI\Settings\BsddSettings.json">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
</ItemGroup>
```

The file will be located at:
```
%APPDATA%\Autodesk\Revit\Addins\<version>\IfcOnTrack.Revit\UI\Settings\BsddSettings.json
```

## Fallback

If this file cannot be loaded, the plugin falls back to hardcoded defaults in `SettingsManager.CreateHardcodedDefaults()`.
