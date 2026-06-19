# IFC.On-Track.nl Plugins - Copilot Coding Guidelines

> **📚 Project Overview:** See [README.md](../README.md)

**This file contains coding rules for the plugins monorepo.**

## Quick Start

```powershell
# Prerequisites: .NET 8 SDK, Revit 2025/2026

# Clone and build
git clone https://github.com/on-track-nl/IFC.On-Track.nl-plugins.git
cd IFC.On-Track.nl-plugins
dotnet restore -p:Configuration="Debug.R25"
dotnet build -c "Debug.R25"

# Or use NUKE
dotnet tool install Nuke.GlobalTool --global
nuke
```

**Tech Stack:** C# 12, .NET 8, Nice3point.Revit.Toolkit, NUKE Build, WebView2

## Architecture Overview

```
IFC.On-Track.nl-plugins/
├── contracts/                 # Language-agnostic JSON Schema contracts
│   └── bridge-data.schema.json
├── source/
│   ├── dotnet/                # All C# projects
│   │   ├── IfcOnTrack.Core/   # Shared: bridge, UI loader, license
│   │   ├── IfcOnTrack.Revit/  # Revit plugin
│   │   └── IfcOnTrack.Tekla/  # Tekla plugin (planned)
│   └── typescript/            # TypeScript npm workspaces
│       └── packages/
│           ├── core/          # Bridge types (generated from schema)
│           └── trimble-connect/ # Trimble Connect addon (planned)
├── build/                     # NUKE build scripts
└── output/                    # Build artifacts (gitignored)
```

**Contract-first principle:** `BridgeData` types are defined once in `contracts/bridge-data.schema.json`.
C# models in `IfcOnTrack.Core` and TypeScript types in `@ifc-on-track/core` must stay in sync with this schema.
Run `npm run generate-types` in `source/typescript` to regenerate TypeScript types from schema.

**Key Design Principles:**

1. **Core handles all shared logic** - Bridge interface, UI loading, license management
2. **Plugins are thin wrappers** - Convert host data to/from BridgeData format
3. **UI is loaded from CDN or local bundle** - Never embedded in plugin DLL
4. **WebView2 for all** - .NET 8 + WebView2 for Revit 2025+

## Build Configurations

| Configuration | Target Framework | Revit Version |
|---------------|------------------|---------------|
| Debug.R25     | net8.0-windows   | 2025          |
| Debug.R26     | net8.0-windows   | 2026          |
| Release.R*    | Same as Debug    | Production    |

## Git Workflow

**Branch naming:**
- `feat/` - New features
- `fix/` - Bug fixes
- `refactor/` - Code improvements

**Commits:**
```
feat(revit): add batch element selection
fix(core): handle license timeout
refactor(bridge): simplify IFC conversion
```

## Code Standards

### File Headers (Required)

```csharp
// Purpose: Brief description of the file's purpose
```

### Namespaces

```csharp
namespace IfcOnTrack.Core.Bridge;    // Core library
namespace IfcOnTrack.Revit.Commands; // Revit commands
namespace IfcOnTrack.Revit.UI;       // Revit UI components
```

### Bridge Implementation Pattern

When implementing a bridge for a new host application:

```csharp
public class HostBridge : IIfcOnTrackBridge
{
    // 1. Convert host selection to IfcEntity list
    public Task<string> LoadBridgeData() { ... }
    
    // 2. Apply BridgeData back to host elements
    public Task<string> Save(string dataJson) { ... }
    
    // 3. Load/save settings to host storage
    public Task<string> LoadSettings() { ... }
    public void SaveSettings(string settingsJson) { ... }
}
```

### Logging

Use Serilog via `Application.Logger`:

```csharp
Application.Logger.Information("Processing {Count} elements", count);
Application.Logger.Error(ex, "Failed to save data");
```

### Error Handling

```csharp
public Result Execute(...)
{
    try
    {
        // Command logic
        return Result.Succeeded;
    }
    catch (Exception ex)
    {
        Application.Logger.Error(ex, "Command failed");
        message = ex.Message;
        return Result.Failed;
    }
}
```

## Testing

```powershell
# Manual testing in Revit
# 1. Build in Debug R25 configuration
# 2. Start Revit (automatic with debug settings)
# 3. Test commands from Add-Ins ribbon
```

## Related Repository

- **IFC.On-Track.nl** (private) - Web platform and embedded UI builds
- Bridge types in `IfcOnTrack.Core.Bridge` must match TypeScript interfaces in the web repo
