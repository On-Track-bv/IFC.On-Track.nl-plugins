# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.3] - 2026-06-24

### Changed
- Ribbon tab renamed to "bSDD" (matches original bSDD Revit plugin — familiar for migrating users)
- bSDD selection and IFC export icons restored to original bSDD plugin icons
- Added 96×96 HiDPI icons for crisp display on 4K/high-DPI monitors
- Installer product name changed to "IFC On-Track bSDD plugin"
- Addin assembly path uses backslash for correct Windows path resolution

### Fixed
- Nullable warning suppression in `BsddSearchBridge` and `BsddSelectionView` replaced with proper guards
- `Installer` project now correctly builds as Release in Release solution configurations
- `build.sh` temp directory aligned with `build.ps1` (`.build/temp`)

## [1.0.2] - 2026-06-22

### Fixed
- **Critical:** MSI installer build now succeeds in CI/CD environments (fixed WixSharp wildcard issue with `Release.R25` paths)
- **Critical:** `.addin` file now correctly placed at `Addins\2025\` level instead of inside `IfcOnTrack.Revit\` subfolder
- Fixed duplicate WiX identifiers for nested runtime directories (win-arm64, win-x64, win-x86)
- Fixed "filename syntax is incorrect" error when building installer with dot-separated configuration names

### Technical
- Replaced WixSharp `DirFiles` wildcards with explicit recursive file enumeration
- Updated `Installer.Generator.cs` to generate unique IDs using full relative paths
- All WiX identifiers now sanitized (replaced `.` and `-` with `_`)
- Build system now fully operational for automated releases

## [1.0.1] - 2026-06-22

### Fixed
- **Critical:** Installer now includes `.addin` manifest file (plugin was invisible to Revit in v1.0.0)
- **Critical:** Files now installed in correct subfolder structure (`Addins\2026\IfcOnTrack.Revit\`)
- Plugin now correctly appears in Revit Add-Ins ribbon

### Technical
- Updated `IfcOnTrack.Revit.csproj` to copy `.addin` to build output
- Updated `Installer.Generator.cs` to create proper folder structure with subdirectories

## [1.0.0] - 2026-06-22

### Added
- Initial stable release of IFC.On-Track.nl plugins
- Revit 2025 and 2026 support via unified codebase
- bSDD classification panel (dockable) with three selection modes:
  - Make Selection (selected elements)
  - Visible in View (elements in active view)
  - All Elements (all element types in document)
- bSDD search dialog (modal) for interactive classification
- Automatic parameter creation for classifications and properties
- ExtensibleStorage for classification persistence
- IFC export with bSDD classification references
- Multi-document support with per-document settings and selection cache
- WebView2-based UI with offline fallback
- Composite decomposition (stacked walls, model groups)
- Element filtering (excludes Levels, Grids, Links, imports)
- Modern .NET 8 architecture with dependency injection
- Nice3point Revit SDK integration
- Comprehensive documentation (Architecture, Bridge Contract, Storage Strategy)
- Auto-update notification system
- High-DPI display support
- Multi-language support (nl-NL, en-US, etc.)

### Changed
- Migrated from .NET Framework 4.8 to .NET 8
- Replaced CefSharp with WebView2 for browser control
- Modernized codebase with C# 12 features
- Unified bridge contract with JSON schema (contracts/bridge-data.schema.json)
- Unique ExtensibleStorage schema GUID (no conflict with old plugin)

### Fixed
- Instance parameter handling now follows type-only rule (binding without value writing)
- PropertyIsInstanceMap completeness (includes bsdd/class/ parameters)
- Room and Area instance parameter handling
- Element duplicate prevention via GroupBy(ElementId)

### Technical
- Build system: ModularPipelines + NUKE
- Installer: WixSharp MSI with per-Revit-version selection
- CI/CD: GitHub Actions with code signing
- Logging: Serilog with structured logging
- UI Version: bSDD-filter-UI v1.9
- License: MIT

[1.0.1]: https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/releases/tag/v1.0.1
[1.0.0]: https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/releases/tag/v1.0.0
