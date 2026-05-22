# Auto-Update & Distributie Strategie

Dit document beschrijft hoe de auto-update functionaliteit werkt en hoe releases worden gedistribueerd.

## 🎯 Overzicht

De plugin heeft drie distributie kanalen:
1. **GitHub Releases** - Automatisch gebouwd via CI/CD
2. **Website Download** - Website toont altijd de laatste versie
3. **In-App Updates** - Gebruikers krijgen notificatie bij nieuwe versie

## 📦 Release Workflow

### 1. GitHub Release maken

```bash
# Via NUKE build systeem
cd build
dotnet nuke

# Of via Git tag
git tag v1.0.0
git push --tags
```

De CI/CD pipeline (.github/workflows/ci.yml) bouwt automatisch:
- `IfcOnTrack.Revit-R25-v1.0.0.exe` installer voor Revit 2025
- `IfcOnTrack.Revit-R26-v1.0.0.exe` installer voor Revit 2026
- Release notes uit `CHANGELOG.md`

### 2. Versie Beheer

Versies worden automatisch bepaald via **GitVersion**:
- `main` branch: Productie releases (1.0.0, 1.1.0, etc.)
- `develop` branch: Pre-release (1.1.0-beta.1)
- Feature branches: Build metadata (1.1.0+branch.feature-name)

### 3. Website Integratie

Voeg dit JavaScript toe aan je website om de laatste versie te tonen:

```html
<div id="download-section">
  <h2>Download IFC.On-Track.nl voor Revit</h2>
  <div id="revit-2025"></div>
  <div id="revit-2026"></div>
</div>

<script>
fetch('https://api.github.com/repos/On-Track-bv/IFC.On-Track.nl-plugins/releases/latest')
  .then(res => res.json())
  .then(data => {
    const version = data.tag_name;
    const assets = data.assets;

    // Find installers
    const r25 = assets.find(a => a.name.includes('R25'));
    const r26 = assets.find(a => a.name.includes('R26'));

    // Display download buttons
    if (r25) {
      document.getElementById('revit-2025').innerHTML = 
        `<a href="${r25.browser_download_url}" class="download-btn">
           Revit 2025 (${version})
         </a>`;
    }

    if (r26) {
      document.getElementById('revit-2026').innerHTML = 
        `<a href="${r26.browser_download_url}" class="download-btn">
           Revit 2026 (${version})
         </a>`;
    }
  });
</script>
```

## 🔔 In-App Update Notificatie

### Hoe het werkt

1. **Bij opstarten** van Revit wordt de GitHub Release API gecontroleerd
2. **Als nieuwe versie** beschikbaar is, verschijnt een gele notification bar
3. **Gebruiker klikt** op "Download" → browser opent met GitHub Release
4. **Gebruiker installeert** nieuwe versie → Revit herstarten

### Code Componenten

```
IfcOnTrack.Core/Update/
├── UpdateChecker.cs          # GitHub API check
└── UpdateInfo.cs              # Release informatie model

IfcOnTrack.Revit/UI/
└── UpdateNotificationBar.cs   # WPF notification bar
```

### Update Check Logica

```csharp
// Application.cs - OnStartup
private async Task CheckForUpdatesAsync()
{
    var updateChecker = Host.TryGetService<UpdateChecker>();
    var currentVersion = Assembly.GetName().Version?.ToString(3);
    var updateInfo = await updateChecker.CheckForUpdateAsync(currentVersion);

    if (updateInfo != null)
    {
        // Show notification
        var view = Host.TryGetService<BsddSelectionView>();
        view?.ShowUpdateNotification(updateInfo);
    }
}
```

### Notification Bar UI

![Update Notification](https://via.placeholder.com/800x60/FFF3CD/000000?text=🔔+Nieuwe+versie+beschikbaar:+1.1.0+[Download]+[✕])

- **Gele achtergrond** (warning color)
- **Download knop** → opent GitHub Release in browser
- **Sluit knop** → verbergt notificatie (tot volgende Revit start)

## 🚀 Release Checklist

Wanneer je een nieuwe versie wilt uitbrengen:

### 1. Update Changelog
```markdown
# Changelog

## [1.1.0] - 2026-05-22
### Added
- Auto-update notificatie
- Nieuwe NL-SfB 2021 dictionary

### Fixed
- Selection preservation bug
- Built-in parameter duplication
```

### 2. Create Git Tag
```bash
git tag v1.1.0 -m "Release v1.1.0"
git push origin v1.1.0
```

### 3. Wacht op CI/CD
- GitHub Actions bouwt de release (3-5 minuten)
- Installers worden automatisch geupload naar GitHub Release
- Release notes worden gegenereerd uit CHANGELOG.md

### 4. Test Release
```bash
# Download installer
curl -L -o installer.exe https://github.com/On-Track-bv/IFC.On-Track.nl-plugins/releases/latest/download/IfcOnTrack.Revit-R25-v1.1.0.exe

# Test installatie
./installer.exe

# Start Revit en test plugin
```

### 5. Publiceer op Website
- Website haalt automatisch de laatste versie via GitHub API
- Controleer dat download links werken

## 🔧 Configuratie

### GitHub API Rate Limiting

GitHub API heeft rate limits:
- **Niet-geauthenticeerd**: 60 requests/uur
- **Geauthenticeerd**: 5000 requests/uur

Voor productie gebruik, voeg een GitHub token toe:

```csharp
// UpdateChecker.cs
_httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
```

### Update Check Frequentie

Standaard controleert de plugin **alleen bij Revit opstarten**. Voor frequentere checks:

```csharp
// Application.cs
var timer = new System.Timers.Timer(TimeSpan.FromHours(4).TotalMilliseconds);
timer.Elapsed += async (s, e) => await CheckForUpdatesAsync();
timer.Start();
```

## 📊 Telemetry (Optioneel)

Om te tracken hoeveel gebruikers updaten, voeg Application Insights toe:

```csharp
// UpdateChecker.cs
public async Task<UpdateInfo?> CheckForUpdateAsync(string currentVersion)
{
    _telemetry.TrackEvent("UpdateCheck", new Dictionary<string, string>
    {
        { "CurrentVersion", currentVersion },
        { "LatestVersion", latestVersion },
        { "UpdateAvailable", (latestVersion > currentVersion).ToString() }
    });
}
```

## 🐛 Troubleshooting

### Update notificatie verschijnt niet

1. Check logs: `%LOCALAPPDATA%\IFC.On-Track.nl\logs\`
2. Zoek naar: `"Checking for updates. Current version: ..."`
3. Controleer netwerk toegang tot `api.github.com`

### Verkeerde versie getoond

Controleer dat `AssemblyVersion` correct is in het project:

```xml
<PropertyGroup>
    <AssemblyVersion>1.0.0</AssemblyVersion>
</PropertyGroup>
```

## 📝 Best Practices

1. **Semantic Versioning**: Gebruik MAJOR.MINOR.PATCH (1.0.0, 1.1.0, 2.0.0)
2. **Release Notes**: Schrijf duidelijke changelog entries
3. **Testing**: Test elke release op ALLE ondersteunde Revit versies
4. **Breaking Changes**: Communiceer breaking changes duidelijk in release notes
5. **Rollback Plan**: Houd oude versies beschikbaar op GitHub Releases

## 🔐 Security

- GitHub API tokens moeten **NOOIT** in de code staan
- Gebruik environment variables of Azure Key Vault
- Update checks zijn **opt-in** - gebruikers kunnen ze uitschakelen via settings

## 📚 Zie Ook

- [GitHub Releases API](https://docs.github.com/en/rest/releases)
- [Semantic Versioning](https://semver.org/)
- [GitVersion Documentation](https://gitversion.net/docs/)
