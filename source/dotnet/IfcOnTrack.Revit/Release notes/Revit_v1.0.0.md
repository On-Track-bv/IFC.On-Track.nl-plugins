# 🎉 IFC.On-Track.nl Plugins v1.0.0 - Initial Release

**Release Date:** June 22, 2026

We're excited to announce the first stable release of the IFC.On-Track.nl desktop plugins! This open-source plugin suite brings buildingSMART Data Dictionary (bSDD) classification and IFC enrichment directly into your BIM authoring tools.

## 📦 What's Included

- **Revit 2025** plugin (IfcOnTrack.Revit-R25-v1.0.0.msi)
- **Revit 2026** plugin (IfcOnTrack.Revit-R26-v1.0.0.msi)

## ✨ Key Features

### 🔍 bSDD Classification
- **Interactive Selection Panel** - Dockable panel for browsing and classifying element types
- **Smart Search** - Modal search dialog with full bSDD API integration
- **Multiple Dictionaries** - Support for main dictionary + multiple filter dictionaries (e.g., NL-SfB, DigiBase)
- **Classification Persistence** - Store classifications in ExtensibleStorage + Revit project parameters

### 📋 Element Management
- **Three Selection Modes:**
  - 🎯 **Make Selection** - Work with currently selected elements
  - 👁️ **Visible in View** - Classify all elements visible in active view
  - 📦 **All Elements** - Access all element types in the document
- **Composite Decomposition** - Automatically expands stacked walls and model groups to their component types
- **Smart Filtering** - Excludes utility categories (Levels, Grids, Links, imports)

### 🏷️ Parameter Management
- **Auto-Create Parameters** - Automatically creates bSDD classification and property parameters
- **Type + Instance Support** - Configure parameters as Type or Instance level via UI
- **IFC Export Ready** - Parameters are IFC-compatible (stored in correct IFC property sets)
- **Parameter Mapping** - Map dictionary classifications to existing Revit parameters (e.g., NL-SfB)

### 📤 IFC Export Enhancement
- **bSDD-Enhanced Export** - Export IFC files with correct IfcClassificationReference entries
- **Property Set Mapping** - Automatically generates UDPS mappings from bsdd/prop/ parameters
- **URL Post-Processing** - Fixes classification reference URLs in output IFC

### 🔄 Multi-Document Support
- **Per-Document Settings** - Settings are stored per Revit document (DataStorage)
- **Selection Cache** - Remembers your last selection when switching between documents
- **Automatic Restore** - Settings and UI state automatically restore on document open

### 🌐 Modern UI Stack
- **WebView2** - Uses Microsoft Edge WebView2 (bundled, no separate install needed)
- **Offline Capable** - Falls back to local UI bundle if CDN unavailable
- **High-DPI Support** - Automatic scaling on 4K/high-DPI displays
- **React SPA** - Powered by buildingSMART's bSDD-filter-UI v1.9

## 📥 Installation

### System Requirements

- **OS:** Windows 10/11 (64-bit)
- **Revit:** Autodesk Revit 2025 or 2026
- **.NET:** .NET 8 Runtime (bundled with Revit 2025+)
- **WebView2:** Microsoft Edge WebView2 Runtime (bundled with installer)

### Install Steps

1. **Download** the MSI installer for your Revit version:
   - `IfcOnTrack.Revit-R25-v1.0.0.msi` (Revit 2025)
   - `IfcOnTrack.Revit-R26-v1.0.0.msi` (Revit 2026)

2. **Run the installer** - Follow the setup wizard

3. **Start Revit** - Find "IFC.On-Track.nl" in the **Add-Ins** ribbon tab

4. **Configure** - Click "bSDD selection" to open the dockable panel and configure your dictionaries in Settings

### First Launch

On first launch, you'll need to:
1. Open the bSDD Selection panel (Add-Ins → IFC.On-Track.nl → "bSDD selection")
2. Click the ⚙️ Settings icon
3. Select your **Main Dictionary** (e.g., NL-SfB 2021, IFC 4.3)
4. Optionally add **Filter Dictionaries** for additional classifications
5. Click **Save**

## 🆕 What's New

This is the **first stable release**, migrated and modernized from the community [bSDD-Revit-plugin](https://github.com/buildingsmart-community/bSDD-Revit-plugin).

### Architectural Improvements
- ✅ **Modern .NET 8** - Upgraded from .NET Framework 4.8
- ✅ **Nice3point Revit SDK** - Clean, modern Revit API integration
- ✅ **WebView2** - Replaced CefSharp for better performance and smaller footprint
- ✅ **Dependency Injection** - Clean service architecture with Microsoft.Extensions
- ✅ **Structured Logging** - Serilog integration for better diagnostics

### New Features vs. Original Plugin
- ✅ **Multi-document selection cache** - Remembers your work when switching documents
- ✅ **Composite decomposition** - Stacked walls and groups automatically decompose to component types
- ✅ **Instance parameter handling** - Proper support for instance-level parameters (binding only, no value writing)
- ✅ **Enhanced filtering** - Excludes unwanted categories (Levels, Grids, imports)
- ✅ **Local UI bundle support** - Offline fallback for CDN-hosted UI

### Code Quality
- 🧹 **Clean Architecture** - Bridge pattern separates host-specific code from core logic
- 📝 **Comprehensive Documentation** - Architecture, bridge contract, storage strategy all documented
- ✨ **Modern C# 12** - Records, pattern matching, nullable reference types
- 🏗️ **Monorepo Structure** - Ready for Tekla and other host plugins

## 📚 Documentation

- **[README.md](https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/blob/main/README.md)** - Project overview and structure
- **[Architecture Docs](https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/tree/main/docs/Revit%20plugin%20analyse)**
  - `ARCHITECTURE.md` - Technical architecture and design
  - `BRIDGE_CONTRACT.md` - JavaScript ↔ C# bridge specification
  - `FEATURE_PARITY.md` - Feature comparison with original plugin
  - `REVIT_STORAGE.md` - How data is persisted in Revit
- **[UI Versioning Strategy](https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/blob/main/docs/UI_VERSIONING_STRATEGY.md)** - UI compatibility and update protocol
- **[Auto-Update Strategy](https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/blob/main/docs/AUTO_UPDATE_STRATEGY.md)** - Release workflow and update mechanism

## ⚠️ Known Limitations

- **Instance parameter values** are not written automatically - parameters are created with correct binding, but you must fill values manually (by design)
- **Rooms and Areas** use special handling - classified as virtual entities ("All Rooms", "All Area's")
- **IFC Export** requires manual trigger - use "IFC export" button in Add-Ins ribbon (Revit's standard IFC export won't include bSDD data)
- **Offline mode** requires manual UI bundle installation - local `ui/` folder must be populated from bSDD-filter-UI build

## 🐛 Bug Reports & Feature Requests

Found a bug or have a feature request? Please open an issue on [GitHub Issues](https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/issues).

Include:
- Revit version (2025 or 2026)
- Plugin version (v1.0.0)
- Steps to reproduce
- Expected vs. actual behavior
- Screenshots if applicable

## 🤝 Contributing

We welcome contributions! This is an open-source project under the MIT License.

**How to contribute:**
1. Fork the repository
2. Create a feature branch (`feat/my-feature`)
3. Make your changes
4. Submit a pull request

See [README.md](https://github.com/On-Track-bv/IFC.On-Track.nl-plugins#contributing) for branch naming and commit conventions.

## 📜 License

MIT License - see [LICENSE](https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/blob/main/LICENSE)

## 🙏 Acknowledgments

- **buildingSMART International** - For the bSDD API and data dictionary infrastructure
- **buildingSMART Community** - For the original [bSDD-Revit-plugin](https://github.com/buildingsmart-community/bSDD-Revit-plugin) and [bSDD-filter-UI](https://github.com/buildingSMART/bSDD-filter-UI)
- **Nice3point** - For the excellent [Revit Templates and SDK](https://github.com/Nice3point/RevitTemplates)

## 🔗 Links

- **Website:** https://ifc.on-track.nl
- **GitHub:** https://github.com/On-Track-bv/IFC.On-Track.nl-plugins
- **Issues:** https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/issues
- **bSDD:** https://www.buildingsmart.org/users/services/buildingsmart-data-dictionary/

---

**Happy classifying! 🎉**
