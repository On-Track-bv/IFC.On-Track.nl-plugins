# IfcOnTrack.Revit

Autodesk Revit plugin voor het [IFC.On-Track.nl](https://ifc.on-track.nl) platform.
Ondersteunt Revit 2025 en 2026 vanuit één MSI installer met feature-selectie.

## Lokaal debuggen

### Vereisten

- Visual Studio 2022 of Rider
- .NET 8 SDK
- Autodesk Revit 2025 en/of 2026 geïnstalleerd

### Stappen

1. Open `IFC.On-Track.nl-plugins.slnx` in Visual Studio
2. Solution Explorer → rechtermuisklik op **IfcOnTrack.Revit** → **Set as Startup Project**
3. Stel de configuratie in op **`Debug.R25`** of **`Debug.R26`** (toolbar bovenin)
4. Druk op **F5**

De [Nice3point.Revit.Sdk](https://github.com/Nice3point/RevitTemplates) zorgt automatisch voor:
- Kopiëren van de DLL en het `.addin` manifest naar de juiste Revit add-ins map
- Opstarten van Revit met debugger attached

Je hoeft zelf geen extern programma in te stellen — dat doet de SDK.

### Build configuraties

| Configuratie | Revit versie | Gebruik |
|---|---|---|
| `Debug.R25` | Revit 2025 | Lokaal debuggen |
| `Debug.R26` | Revit 2026 | Lokaal debuggen |
| `Release.R25` | Revit 2025 | CI/CD builds |
| `Release.R26` | Revit 2026 | CI/CD builds |

## Lokaal bouwen

```powershell
# Alleen Revit plugin compileren (vanuit repo root)
dotnet build source/dotnet/IfcOnTrack.Revit/IfcOnTrack.Revit.csproj -c Debug.R25

# Alleen compileren via build systeem (alle plugins, alle release configs)
dotnet run --project build

# Compileren + ZIP packages maken
dotnet run --project build -- pack

# Compileren + MSI installer bouwen
dotnet run --project build -- installer

# Volledige release build: clean + compile + ZIPs + MSI
dotnet run --project build -- release
```

De MSI verschijnt na `installer` of `release` in de `output/` map als:
- `IfcOnTrack.Revit-v{version}-SingleUser.msi`
- `IfcOnTrack.Revit-v{version}-MultiUser.msi`

Het build systeem (ModularPipelines in `build/`) pakt de configuraties en plugins uit `build/appsettings.json`.

## CI/CD pipeline

### Pull Request naar main

- Compileert de plugin (R25 + R26)
- Bouwt de MSI installer (unsigned)
- MSI is downloadbaar als artifact in de GitHub Actions UI (14 dagen)

### Push naar main (staging)

- Zelfde als PR
- GitHub pre-release `staging` wordt bijgewerkt
- De staging website toont automatisch deze build

> **Signing:** de workflow heeft een signing stap via Azure Artifact Signing, maar dit is nog niet actief. Zie [docs/CODE_SIGNING.md](../../../docs/CODE_SIGNING.md) voor de setupstappen.

### Tag `v*` (productie release)

```bash
git tag v1.2.0
git push origin v1.2.0
```

- GitHub Release `v1.2.0` aangemaakt met MSI en ZIP artifacts
- Productie website toont automatisch de nieuwe versie

> **Signing:** zelfde als hierboven — nog niet actief tot Azure Artifact Signing is geconfigureerd.

## Versioning

De hele plugin suite gebruikt **SemVer**: `MAJOR.MINOR.PATCH`

| Situation | Actie |
|---|---|
| Bugfix | `v1.0.1` |
| Nieuwe feature | `v1.1.0` |
| Breaking change | `v2.0.0` |

Alle plugins (Revit, Tekla, etc.) delen hetzelfde versienummer. Eén tag = één release met alle artifacts.

Staging builds krijgen versie `0.0.{run_number}` — altijd lager dan een echte release, zodat de MSI upgrade-logica correct werkt.

## Installer

De MSI wordt gebouwd via WixSharp en ondersteunt:
- **Single-user** installatie (`%AppData%\Autodesk\Revit\Addins\`)
- **Multi-user** installatie (`%ProgramData%\Autodesk\Revit\Addins\`)
- Feature-selectie: gebruiker kiest welke Revit versies te installeren

Zie `install/Installer.cs` voor de installer configuratie.

## Auto-update

De plugin controleert bij elke Revit-start via de GitHub Releases API of er een nieuwe versie beschikbaar is. Bij een nieuwere versie verschijnt een notificatiebalk in de UI.

Zie `source/dotnet/IfcOnTrack.Core/Update/UpdateChecker.cs`.
