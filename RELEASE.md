# Release Steps

## Prerequisites

- GitHub repo: `https://github.com/nazmulpro/AIUsageHub`
- `vpk` CLI installed: `dotnet tool install -g vpk`
- Changes committed and pushed to `main`

---

## Release Checklist

### 1. Make a Release

```powershell
# Ensure main is up to date
git checkout main
git pull origin main

# Create and push a version tag
git tag v1.0.0
git push origin v1.0.0
```

### 2. What CI Does (`.github/workflows/release.yml`)

After the tag push, GitHub Actions automatically:

| Step | What It Does |
|---|---|
| `actions/checkout@v4` | Clones the repo |
| `setup-dotnet@v4` | Installs .NET 10 SDK |
| `dotnet tool install -g vpk` | Installs Velopack CLI |
| `dotnet publish` | Builds self-contained `win-x64` release to `publish/` |
| `vpk pack` | Creates `Setup.exe`, `RELEASES`, `.nupkg` files in `packaged/` |
| `softprops/action-gh-release` | Uploads all files to a new GitHub Release |

### 3. Generated Artifacts

After CI completes, the GitHub Release contains:

| File | Size | Purpose |
|---|---|---|
| `Setup.exe` | ~80 MB | Windows installer for end users |
| `RELEASES` | ~1 KB | Update metadata (auto-checked by the app) |
| `AIUsageHub-1.0.0-full.nupkg` | ~80 MB | Full package (for fresh installs) |
| `AIUsageHub-1.0.0-delta.nupkg` | ~1 KB | Delta update (first release has no delta) |

> Subsequent releases (v1.1.0, v1.2.0) will produce smaller delta packages with only the changed bytes.

---

## How Users Get Updates

1. **New users** download `Setup.exe` from GitHub Releases and install
2. **Existing users** — the app silently checks for updates 5 seconds after launch
3. If a new version is found, a badge appears in **Settings → About** tab
4. User clicks **"Download & Install"** → app downloads the delta/package → restarts on the new version

---

## Testing Locally Before Releasing

```powershell
# Publish the app
dotnet publish src/AIUsageHub/AIUsageHub.csproj `
    -c Release -r win-x64 --self-contained true -o publish

# Package with a test version
vpk pack `
    --packId "AIUsageHub" `
    --packVersion "1.0.0-test" `
    --packDir "publish" `
    --mainExe "AIUsageHub.exe" `
    -o "packaged"

# Install on this machine
.\packaged\Setup.exe
```

---

## Version Convention

| Tag | When |
|---|---|
| `v1.0.0` | Initial release |
| `v1.0.1` | Bug fix |
| `v1.1.0` | New feature (backward compatible) |
| `v2.0.0` | Breaking change |

Tags use the `v` prefix. The CI workflow watches for `v*` tag pushes.
