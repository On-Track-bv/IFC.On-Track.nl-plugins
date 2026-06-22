# IFC.On-Track.nl Plugins

> Open source desktop plugins for the IFC.On-Track.nl platform.

These plugins embed the [IFC.On-Track.nl](https://ifc.on-track.nl) platform into desktop BIM applications, providing bSDD linking, IDS validation, and IFC data enrichment directly in your modeling environment.

## Supported Applications

| Application | Status | Docs |
|---|---|---|
| Autodesk Revit | ✅ Active | [README](source/dotnet/IfcOnTrack.Revit/README.md) |
| Tekla Structures | 🚧 Planned | — |
| Trimble Connect | 🔗 Web-based | — |

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

## Project Structure

```
IFC.On-Track.nl-plugins/
├── source/
│   └── dotnet/
│       ├── IfcOnTrack.Core/      # Shared library (UI loader, bridge, update checker)
│       └── IfcOnTrack.Revit/     # Revit plugin — zie README aldaar
├── build/                        # ModularPipelines build systeem
├── install/                      # WixSharp MSI installer
├── output/                       # Build artifacts (gitignored)
└── Directory.Build.props         # Gedeelde MSBuild properties
```

## Versioning

De plugin suite gebruikt **SemVer**: `MAJOR.MINOR.PATCH`

Alle plugins delen hetzelfde versienummer. Een release aanmaken:

```bash
git tag v1.2.0
git push origin v1.2.0
```

Dit triggert automatisch de release pipeline: compile → sign → GitHub Release.

**Belangrijke documentatie:**
- [docs/AUTO_UPDATE_STRATEGY.md](docs/AUTO_UPDATE_STRATEGY.md) - Plugin release workflow en update mechanisme
- [docs/UI_VERSIONING_STRATEGY.md](docs/UI_VERSIONING_STRATEGY.md) - UI versie compatibiliteit en update protocol
- [source/dotnet/IfcOnTrack.Revit/README.md](source/dotnet/IfcOnTrack.Revit/README.md) - Revit plugin details, debug en CI/CD flow

## Installation (End Users)

1. Download de laatste release van [Releases](https://github.com/on-track-nl/IFC.On-Track.nl-plugins/releases)
2. Voer de `.msi` installer uit
3. Selecteer de Revit versies om te installeren
4. Start Revit — vind IFC.On-Track in het Add-Ins ribbon

## Contributing

### Branch Naming

- `feat/` — nieuwe functionaliteit
- `fix/` — bug fixes
- `refactor/` — code verbeteringen

### Commit Format

```
feat(revit): add batch selection support
fix(core): correct license validation timeout
```

## License

MIT License — zie [LICENSE](LICENSE)

## Related

- [IFC.On-Track.nl](https://ifc.on-track.nl) — Web platform
- [bSDD](https://www.buildingsmart.org/users/services/buildingsmart-data-dictionary/) — buildingSMART Data Dictionary
- [IDS](https://github.com/buildingSMART/IDS) — Information Delivery Specification
