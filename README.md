# IFC.On-Track.nl Plugins

> Open source desktop plugins for the IFC.On-Track.nl platform.

These plugins embed the [IFC.On-Track.nl](https://ifc.on-track.nl) platform into desktop BIM applications, providing bSDD linking, IDS validation, and IFC data enrichment directly in your modeling environment.

## Supported Applications

| Application | Status | Revit Versions |
|-------------|--------|----------------|
| Autodesk Revit | ✅ Active | 2025, 2026 |
| Tekla Structures | 🚧 Planned | - |
| Trimble Connect | 🔗 Web-based | N/A |

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                     IFC.On-Track.nl (private repo)                          │
│  ┌─────────────────────────────────────────────────────────────────────────┐│
│  │  React UI + TypeScript libraries + Azure backend                       ││
│  └─────────────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                          CDN / Embedded builds
                                      │
┌─────────────────────────────────────┼───────────────────────────────────────┐
│               IFC.On-Track.nl-plugins (this repo)                           │
│                                     │                                       │
│  ┌──────────────────┐    ┌──────────┴──────────┐    ┌──────────────────┐   │
│  │ IfcOnTrack.Revit │    │  IfcOnTrack.Core    │    │ IfcOnTrack.Tekla │   │
│  │                  │◄───┤  • UI Loader        ├───►│                  │   │
│  │  • Commands      │    │  • Bridge Interface │    │  • Commands      │   │
│  │  • Revit Bridge  │    │  • License Manager  │    │  • Tekla Bridge  │   │
│  │  • IFC Export    │    │  • Common Utils     │    │                  │   │
│  └──────────────────┘    └─────────────────────┘    └──────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Quick Start (Development)

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Autodesk Revit 2025 or 2026
- [NUKE](https://nuke.build/) (optional, for full build)

### Build & Debug

```powershell
# Clone repository
git clone https://github.com/on-track-nl/IFC.On-Track.nl-plugins.git
cd IFC.On-Track.nl-plugins

# Restore and build (Nice3point SDK requires config-specific restore)
dotnet restore -p:Configuration="Debug.R25"
dotnet build -c "Debug.R25"

# Or use NUKE for full build
dotnet tool install Nuke.GlobalTool --global
nuke
```

### Debug in Revit

1. Open `IfcOnTrackPlugins.sln` in Visual Studio or Rider
2. Set `IfcOnTrack.Revit` as startup project
3. Configure debug settings:
   - Start external program: `C:\Program Files\Autodesk\Revit 2025\Revit.exe`
   - Start arguments: `/language ENG`
4. Press F5 to debug

## Project Structure

```
IFC.On-Track.nl-plugins/
├── source/
│   ├── IfcOnTrack.Core/          # Shared library (UI loader, bridge, license)
│   ├── IfcOnTrack.Revit/         # Revit plugin
│   └── IfcOnTrack.Tekla/         # Tekla plugin (planned)
├── build/                         # NUKE build scripts
├── install/                       # Installer project
├── output/                        # Build artifacts (gitignored)
├── IfcOnTrackPlugins.sln
└── Directory.Build.props          # Shared MSBuild properties
```

## Installation (End Users)

1. Download the latest release from [Releases](https://github.com/on-track-nl/IFC.On-Track.nl-plugins/releases)
2. Run `IfcOnTrack-Setup.exe`
3. Select Revit versions to install
4. Launch Revit - find IFC.On-Track in the Add-Ins ribbon

## Contributing

Contributions are welcome! Please read our contributing guidelines and submit PRs.

### Branch Naming

- `feat/` - New features
- `fix/` - Bug fixes  
- `refactor/` - Code improvements

### Commit Format

```
feat(revit): add batch selection support
fix(core): correct license validation timeout
```

## License

MIT License - see [LICENSE](LICENSE)

## Related

- [IFC.On-Track.nl](https://ifc.on-track.nl) - Web platform
- [bSDD](https://www.buildingsmart.org/users/services/buildingsmart-data-dictionary/) - buildingSMART Data Dictionary
- [IDS](https://github.com/buildingSMART/IDS) - Information Delivery Specification
