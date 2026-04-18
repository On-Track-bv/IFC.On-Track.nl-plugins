# Migration to Revit 2025+ Only

## Overview
Removed Revit 2024 support to simplify the codebase and eliminate .NET Framework 4.8 complexity.

## What Changed

### 1. **IfcOnTrack.Revit.csproj** - Project Configuration
- ✅ Removed `Debug.R24` and `Release.R24` build configurations
- ✅ Removed CefSharp.Wpf package reference
- ✅ Simplified to WebView2 only for all versions
- ✅ Removed ILRepack exception for R24 (now always enabled)
- ✅ Simplified Microsoft.Extensions.Hosting to version 8.x only
- ✅ Simplified Serilog.Extensions.Hosting to version 8.x only

### 2. **BsddSelectionView.cs** - Selection Panel UI
- ✅ Removed `#if NET48` field declarations (`_cefBrowser`)
- ✅ Removed `LoadCefSharpBrowser()` method
- ✅ Removed conditional compilation in `LoadBrowser()`
- ✅ Removed conditional compilation in `PushSelectionToJs()`
- ✅ Removed conditional compilation in `PushSettingsToJs()`
- ✅ Removed `#if !NET48` guards from async helper methods

### 3. **.github/copilot-instructions.md** - Documentation
- ✅ Updated prerequisites (removed .NET Framework 4.8)
- ✅ Updated tech stack (WebView2 only)
- ✅ Removed R24 from build configurations table
- ✅ Updated key design principles

### 4. **Completed**
- ✅ Cleaned **BsddSearchView.cs** - removed NET48 CefSharp browser code
- ✅ Cleaned **Host.cs** - removed NET48 DI container and conditionals
- ✅ Updated **build/.github/workflows/ci.yml** - removed R24 from build matrix
- ✅ Updated **README.md** - removed Revit 2024 and .NET Framework 4.8 references

## Benefits

### Simplified Codebase
- No more `#if NET48` / `#else` blocks
- Single browser implementation (WebView2)
- No CefSharp version conflicts
- Cleaner async/await patterns throughout

### Better Developer Experience
- Modern .NET 8 APIs only
- Easier debugging (one code path)
- Faster build times (one less configuration)
- No ILRepack issues with native DLLs

### Easier Deployment
- ILRepack always enabled
- Single executable deployment
- No dependency on Revit's CefSharp version
- WebView2 runtime auto-installed by Windows

## Migration Path for Users

Users on Revit 2024 will need to:
1. Upgrade to Revit 2025 or 2026, OR
2. Continue using the last version that supported Revit 2024

Consider tagging the last R24-compatible commit:
```bash
git tag -a v1.0-r24-final -m "Last version supporting Revit 2024"
git push origin v1.0-r24-final
```

## Next Steps

1. **Complete cleanup**: Remove remaining NET48 conditionals in other files
2. **Update NUKE build**: Remove R24 targets from build system
3. **Test thoroughly**: Build and test in Revit 2025 and 2026
4. **Update deployment**: Ensure .addin manifest doesn't reference R24
5. **Document breaking change**: Add to CHANGELOG.md or release notes
