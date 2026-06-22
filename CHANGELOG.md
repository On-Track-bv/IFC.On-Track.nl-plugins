# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.0]: https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/releases/tag/v1.0.0
