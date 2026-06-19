# Code Signing

MSI installers worden gesigned via **Azure Artifact Signing** (voorheen Trusted Signing).
De workflows zijn al voorbereid — alleen de Azure setup en GitHub Secrets moeten nog worden ingesteld.

## Wat er al staat

- `ci.yml` — signt MSIs bij elke push naar `main` (staging release)
- `release.yml` — signt MSIs bij elke release-tag (`v*`)
- Lokale builds blijven unsigned; Revit laadt de plugin gewoon via het `.addin` manifest

## Setup

Alles wordt aangemaakt in de **On-Track tenant** (de tenant met de Azure subscription).
De External ID tenant (voor klantlogins op ifc.on-track.nl) is niet relevant voor deze setup.

### Stap 1 — Artifact Signing Account

1. [portal.azure.com](https://portal.azure.com) → zoek **Artifact Signing Accounts** → **Create**
2. Vul in:
   - Resource group: `rg-on-track-tooling`
   - Name: `on-track-codesigning`
   - Region: `West Europe`
   - SKU: `Basic` (~$9/mnd)
3. **Review + Create**

### Stap 2 — Identity Validation ⚠️ duurt 1–3 werkdagen

In het Artifact Signing Account → **Identity Validations** → **Add**:
- Type: **Organization Validation**
- Vul in: bedrijfsnaam (On-Track B.V.), land, primair e-mailadres
- Microsoft verifieert en stuurt bevestiging per e-mail

### Stap 3 — Certificate Profile (pas na verificatie stap 2)

In het Artifact Signing Account → **Certificate Profiles** → **Add**:
- Name: `on-track-plugins`
- Profile type: **Public Trust**
- Identity Validation: kies de geverifieerde uit stap 2

### Stap 4 — App Registration

**Microsoft Entra ID** (On-Track tenant) → **App registrations** → **New registration**:
- Name: `on-track-plugins-ci`
- Supported account types: Single tenant
- **Register**

Noteer na aanmaken:
- **Application (client) ID** → wordt `AZURE_CLIENT_ID`
- **Directory (tenant) ID** → wordt `AZURE_TENANT_ID`

### Stap 5 — Client Secret

In de App Registration → **Certificates & secrets** → **New client secret**:
- Description: `github-actions`
- Expires: 24 months
- **Add** → kopieer de **Value** direct (maar één keer zichtbaar)

Dit wordt `AZURE_CLIENT_SECRET`. Zet een reminder voor verlenging over ~22 maanden.

### Stap 6 — Rol toewijzen

In het Artifact Signing Account → **Access Control (IAM)** → **Add role assignment**:
- Rol: **Artifact Signing Certificate Profile Signer**
- Assign to: de App Registration `on-track-plugins-ci`

### Stap 7 — GitHub Secrets toevoegen

Plugin repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**:

| Secret naam | Waarde |
|---|---|
| `AZURE_TENANT_ID` | Directory (tenant) ID uit stap 4 |
| `AZURE_CLIENT_ID` | Application (client) ID uit stap 4 |
| `AZURE_CLIENT_SECRET` | Secret value uit stap 5 |
| `AZURE_ARTIFACT_SIGNING_ENDPOINT` | `https://weu.codesigning.azure.net/` |
| `AZURE_ARTIFACT_SIGNING_ACCOUNT` | `on-track-codesigning` |
| `AZURE_ARTIFACT_SIGNING_PROFILE` | `on-track-plugins` |

Na stap 7 werkt signing automatisch — geen verdere codewijzigingen nodig.

## Toekomstige uitbreiding: DLL signing

Op dit moment worden alleen de MSI installers gesigned. De DLLs erin zijn unsigned.
Voor volledig gesignde DLLs moet de build in twee fases worden gesplitst:

```
Compile → Sign DLLs in bin/ → Create MSI (bevat gesignde DLLs) → Sign MSI
```

Dit vereist een aanpassing in de build pipeline (ModularPipelines). Prioriteit: laag —
MSI signing dekt het SmartScreen probleem voor eindgebruikers volledig af.
