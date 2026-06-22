# UI & Plugin Versioning Strategy

This document describes how we manage compatibility between the bSDD-filter-UI (external React SPA) and the various host plugins (Revit, Tekla, etc.).

## 🎯 Core Principle

The bSDD UI is **external** and maintained by the buildingSMART community. Plugins **consume** the UI and must implement the bridge contract that the UI expects. This creates a dependency chain:

```
bSDD-filter-UI (external)
    ↓ defines bridge contract
Plugin (Revit/Tekla)
    ↓ uses
Host Application (Revit 2025/2026, Tekla, etc.)
```

## 📊 Version Matrix

### Current Production

| Component | Version | Status | Notes |
|-----------|---------|--------|-------|
| bSDD-filter-UI | v1.9 | ✅ Stable | Production CDN |
| IFC.On-Track.nl Revit | 1.0.0+ | ✅ Compatible | All versions support v1.9 |
| IFC.On-Track.nl Tekla | - | 🚧 Planned | Will target v1.9 or later |

### Compatibility Rules

1. **UI Version is hardcoded** in `UiLoader.cs`:
   ```csharp
   private const string CdnBaseUrl = "https://buildingsmart-community.github.io/bSDD-filter-UI/v1.9/";
   ```

2. **All plugins targeting the same UI version must implement the same bridge contract**
   - Contract is defined in `contracts/bridge-data.schema.json`
   - TypeScript types in `@ifc-on-track/core` (generated from schema)
   - C# models in `IfcOnTrack.Core.Bridge`

3. **Breaking changes in UI require plugin updates**
   - Example: v1.9 → v2.0 with breaking changes = new plugin release

## 🔄 UI Update Protocol

### When to Update UI Version

Monitor https://github.com/buildingSMART/bSDD-filter-UI for new releases:

1. **Patch versions (v1.9.1 → v1.9.2)** - Bug fixes only
   - ✅ Safe to update without testing
   - Update `CdnBaseUrl` constant

2. **Minor versions (v1.9 → v1.10)** - New features, backwards compatible
   - ⚠️ Test locally first
   - Verify bridge contract unchanged
   - Update and release patch version

3. **Major versions (v1.9 → v2.0)** - Breaking changes
   - 🚨 Full testing required
   - Update bridge contract implementation
   - Update schema + generated types
   - Release as new major version

### Update Workflow

```bash
# 1. Check bSDD-filter-UI releases
# https://github.com/buildingSMART/bSDD-filter-UI/releases

# 2. Update UiLoader.cs
# Change: private const string CdnBaseUrl = "https://...v1.10/";

# 3. Test locally with CDN version
dotnet build -c Debug.R25
# Start Revit, test all workflows

# 4. (Optional) Bundle UI locally for offline testing
# Download build artifacts from bSDD-filter-UI release
# Place in: source/dotnet/IfcOnTrack.Revit/ui/bsdd_selection/
# Place in: source/dotnet/IfcOnTrack.Revit/ui/bsdd_search/

# 5. Update CHANGELOG.md
## [1.0.1] - 2026-06-22
### Changed
- Updated bSDD UI to v1.10 (minor improvements)

# 6. Release
git tag v1.0.1
git push --tags
```

## 🧪 Testing Strategy

### Before Updating UI Version

Test all core workflows:

#### Selection Panel (Dockable)
- [ ] Load panel with "All Elements"
- [ ] Switch to "Visible in View"
- [ ] Switch to "Make Selection"
- [ ] Verify element list updates correctly
- [ ] Click row → verify Revit selection highlights instances
- [ ] Open Settings → save changes → verify persistence

#### Search Panel (Modal Dialog)
- [ ] Select elements → open search from selection panel
- [ ] Search for classification in main dictionary
- [ ] Add classification to element
- [ ] Add properties from property set
- [ ] Save → verify parameters created in Revit
- [ ] Verify selection panel refreshes with new data

#### Multi-Document
- [ ] Open doc A → classify elements
- [ ] Open doc B → verify settings restored per-document
- [ ] Switch back to doc A → verify selection cache restored

#### Settings
- [ ] Change main dictionary
- [ ] Add filter dictionaries
- [ ] Change language (nl-NL ↔ en-US)
- [ ] Verify DPI scale (displayScale) works on high-DPI monitors

### Compatibility Testing Matrix

When releasing a new plugin version, test against:

| Revit Version | .NET Runtime | WebView2 Version |
|---------------|--------------|------------------|
| 2025 | .NET 8 | 122+ (bundled) |
| 2026 | .NET 8 | 122+ (bundled) |

## 🚨 Breaking Changes Policy

### What Constitutes a Breaking Change?

**UI Side** (requires plugin update):
- Bridge method signature changes (e.g., `loadBridgeData()` now requires parameter)
- JSON structure changes (e.g., `settings.mainDictionary` renamed)
- Required field additions (e.g., `BridgeData.newField` is now mandatory)
- Bridge method removal

**Plugin Side** (requires UI update):
- Removing bridge methods
- Changing JSON field types (e.g., `string` → `number`)
- Removing required fields

### Mitigation Strategy

1. **Schema-first design** - All changes go through `bridge-data.schema.json`
2. **Additive changes only** - New optional fields are safe
3. **Deprecation period** - Mark old fields as deprecated before removal
4. **Version negotiation** - Future: Add `bsddBridge.getVersion()` for runtime checks

## 🏗️ Multi-Plugin Compatibility

### Scenario: Revit Plugin 1.2.0 + Tekla Plugin 0.9.0

Both plugins can target **different UI versions** if needed:

```csharp
// Revit plugin (IfcOnTrack.Revit)
private const string CdnBaseUrl = "https://...v1.9/";

// Tekla plugin (IfcOnTrack.Tekla)
private const string CdnBaseUrl = "https://...v1.10/";
```

**Why?** Each plugin releases independently. Revit plugin might be stable on v1.9 while Tekla plugin (newer) uses v1.10.

**Requirement:** Both must implement their respective bridge contracts correctly.

### Shared Code (IfcOnTrack.Core)

The `IfcOnTrack.Core` library contains:
- Bridge interface definitions (`IIfcOnTrackBridge`)
- Bridge data models (`BridgeData`, `IfcEntity`, etc.)
- UI loader (`UiLoader`)

**Versioning Rule:** Core library version tracks the **bridge contract version**, not the UI version.

```
IfcOnTrack.Core 1.0.0 → supports UI v1.9 bridge contract
IfcOnTrack.Core 2.0.0 → supports UI v2.0 bridge contract (breaking)
```

## 📦 Local UI Bundle Strategy

### Why Bundle UI Locally?

1. **Offline development** - Work without internet
2. **Testing** - Test unreleased UI versions
3. **Customization** - Fork bSDD-filter-UI for custom features
4. **CDN fallback** - If buildingSMART CDN is down

### Bundle Structure

```
source/dotnet/IfcOnTrack.Revit/
└── ui/
    ├── version.json                    # {"version": "1.9.0", "commit": "abc123"}
    ├── bsdd_selection/
    │   ├── index.html
    │   ├── static/
    │   └── ...
    └── bsdd_search/
        ├── index.html
        ├── static/
        └── ...
```

### How UiLoader Resolves URL

```csharp
// Priority order:
1. Local bundle: ui/bsdd_selection/index.html (if exists)
2. CDN: https://buildingsmart-community.github.io/bSDD-filter-UI/v1.9/bsdd_selection/
```

### Creating Local Bundle

```bash
# Clone bSDD-filter-UI
git clone https://github.com/buildingSMART/bSDD-filter-UI.git
cd bSDD-filter-UI

# Build for production
npm install
npm run build

# Copy build output to plugin
cp -r build/bsdd_selection/* ../IFC.On-Track.nl-plugins/source/dotnet/IfcOnTrack.Revit/ui/bsdd_selection/
cp -r build/bsdd_search/* ../IFC.On-Track.nl-plugins/source/dotnet/IfcOnTrack.Revit/ui/bsdd_search/

# Create version file
echo '{"version":"1.9.0","commit":"abc123","bundled":"2026-06-22"}' > ../IFC.On-Track.nl-plugins/source/dotnet/IfcOnTrack.Revit/ui/version.json
```

**Note:** Local bundles are `.gitignore`d by default. Don't commit them unless you have a specific reason (e.g., offline deployment).

## 🔧 Troubleshooting

### UI Version Mismatch Symptoms

**Problem:** Plugin expects UI v1.9 but loads v2.0 by mistake
- Bridge method calls fail with `undefined`
- JSON deserialization errors
- Missing fields in bridge data

**Solution:**
1. Check `UiLoader.cs` - verify correct CDN URL
2. Clear browser cache in WebView2 (delete `%LOCALAPPDATA%\Microsoft\Edge\User Data`)
3. Verify local bundle version matches `CdnBaseUrl`

### Bridge Contract Debugging

Enable verbose logging:

```csharp
// Application.cs
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

Logs will show:
- Bridge method calls (`BsddSearchBridge.Save()`)
- JSON serialization/deserialization
- UI loader decisions (local vs CDN)

### CDN Fallback Failure

**Problem:** No internet, no local bundle
- UI fails to load
- White screen in panel

**Mitigation:**
1. Show error message: "bSDD UI unavailable (offline). Please connect to internet or install local bundle."
2. Future: Ship minimal offline UI with plugin installer

## 📋 Release Checklist

When updating UI version:

### 1. Pre-Release Testing
- [ ] Test all workflows (see Testing Strategy)
- [ ] Verify on all supported Revit versions (2025, 2026)
- [ ] Test on high-DPI displays (125%, 150%, 200%)
- [ ] Test offline with local bundle

### 2. Update Code
- [ ] Update `UiLoader.cs` CDN URL
- [ ] Update `BRIDGE_CONTRACT.md` with new UI version
- [ ] Update this document's version matrix
- [ ] Update `CHANGELOG.md`

### 3. Update Tests
- [ ] Run integration tests (if available)
- [ ] Manual testing (see checklist above)

### 4. Release
- [ ] Git tag: `git tag v1.x.x`
- [ ] Push: `git push --tags`
- [ ] Monitor CI/CD build
- [ ] Verify GitHub Release artifacts
- [ ] Update website download links

### 5. Post-Release
- [ ] Monitor user reports for UI issues
- [ ] Check logs for bridge errors
- [ ] Update documentation if needed

## 🔮 Future Improvements

### 1. Runtime Version Negotiation

Add to bridge contract:

```typescript
interface BsddBridge {
  getVersion(): Promise<string>;  // Returns "1.9.0"
}
```

Plugin can check UI version at runtime and adapt:

```csharp
var uiVersion = await bridge.GetVersionAsync();
if (uiVersion.StartsWith("1.")) {
    // Use v1.x logic
} else if (uiVersion.StartsWith("2.")) {
    // Use v2.x logic
}
```

### 2. Automated Compatibility Tests

```bash
# Test plugin against multiple UI versions
dotnet test --ui-version=1.9
dotnet test --ui-version=1.10
dotnet test --ui-version=2.0
```

### 3. Schema Validation

Validate bridge data at runtime:

```csharp
var validator = new JsonSchemaValidator("contracts/bridge-data.schema.json");
validator.Validate(bridgeDataJson);  // Throws if invalid
```

### 4. UI Version Lock File

```json
// ui-version.lock.json
{
  "ui": {
    "version": "1.9.0",
    "url": "https://buildingsmart-community.github.io/bSDD-filter-UI/v1.9/",
    "commit": "abc123",
    "tested": "2026-06-22"
  },
  "contract": {
    "schema": "contracts/bridge-data.schema.json",
    "hash": "sha256:..."
  }
}
```

## 📚 Related Documentation

- [BRIDGE_CONTRACT.md](Revit%20plugin%20analyse/BRIDGE_CONTRACT.md) - Bridge API specification
- [AUTO_UPDATE_STRATEGY.md](AUTO_UPDATE_STRATEGY.md) - Plugin release workflow
- [contracts/bridge-data.schema.json](../contracts/bridge-data.schema.json) - JSON Schema for bridge data

## 🔗 External Resources

- [bSDD-filter-UI Repository](https://github.com/buildingSMART/bSDD-filter-UI)
- [bSDD-filter-UI Releases](https://github.com/buildingSMART/bSDD-filter-UI/releases)
- [bSDD API Documentation](https://github.com/buildingSMART/bSDD)
