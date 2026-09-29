# Contributing

Thanks for helping out. This guide covers setting up, making a change, and building the installers.

By taking part you agree to follow the [Code of Conduct](CODE_OF_CONDUCT.md).

## Before you start

- **Bugs:** search [existing issues](https://github.com/RohanGupta15/grappler/issues) first, then open one with the bug report form.
- **Features:** open a feature request before writing a large change, so we can agree on the approach.
- **A site stopped working?** Open Settings and press **Check for updates** under Download engine first. Most breakages are fixed by a newer yt-dlp. If it still fails with the newest yt-dlp, the problem belongs in [yt-dlp's issues](https://github.com/yt-dlp/yt-dlp/issues).
- **Security problems** go through [SECURITY.md](SECURITY.md), not public issues.

## Set up

Requirements:

- Windows 10 1809 or later, or Windows 11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Git and PowerShell 7 (`winget install Microsoft.PowerShell`)

Visual Studio is optional. Any editor works; the project builds with the `dotnet` CLI.

```powershell
git clone https://github.com/RohanGupta15/grappler.git
cd grappler
dotnet tool restore                     # installs the pinned Velopack CLI (vpk)
dotnet test tests/YtDownloader.Core.Tests
pwsh ./scripts/run-dev.ps1              # builds and launches the app
```

`run-dev.ps1` runs the app unpackaged, so you don't need Developer Mode. It uses the same data folder as an installed copy: `%LOCALAPPDATA%\YtDownloader` (settings, engine, `crash.log`).

## Project layout

| Path | What lives there |
| --- | --- |
| `src/YtDownloader.Core` | Engine download and SHA-256 checks, yt-dlp argument building, output and JSON parsing. No UI code. |
| `src/YtDownloader.App` | WinUI 3 app. `ViewModels/` (queue, items, settings), pages, controls, `Services/`. |
| `tests/YtDownloader.Core.Tests` | xUnit tests for Core. `Fixtures/` holds real yt-dlp JSON output. |
| `scripts/` | `run-dev.ps1`, `build-setup.ps1`, `build-msix.ps1` |
| `assets/logo/` | Logo SVG masters and `build-icons.ps1`, which renders every app icon from them |

## Making a change

1. Fork the repo and create a branch from `main`, for example `fix/pause-button` or `feat/toasts`.
2. Keep logic in Core where you can, and test it. Tests check behaviour through public methods, using captured yt-dlp output rather than the network. Write the failing test first when fixing a bug.
3. Follow the existing style. `.editorconfig` sets the basics; match the naming and comment style of the file you're in.
4. UI changes should use stock WinUI controls and theme resources (no hard-coded colours) and work at narrow widths (under 641 px the layout switches to compact) and in light and dark themes.
5. Run `dotnet test` and launch the app with `scripts/run-dev.ps1` before you push.
6. Open a pull request and fill in the template. Add screenshots for anything visual.

### Commit messages

Short, imperative subject lines, for example `Add taskbar progress` or `Fix resume after a failed merge`. Explain the why in the body when it isn't obvious.

### Changelog

Add a line under **Unreleased** in [CHANGELOG.md](CHANGELOG.md) for anything a user would notice.

## Building installers

Both scripts write to `artifacts/`, which git ignores.

### Setup.exe (no certificate needed)

```powershell
pwsh ./scripts/build-setup.ps1 -Version 0.1.0
```

This publishes a self-contained, unpackaged build and packs it with [Velopack](https://velopack.io/). You get `artifacts/releases/YtDownloaderApp-win-x64-Setup.exe`, a portable zip and the update feed files. Setup.exe installs per user into `%LOCALAPPDATA%\YtDownloaderApp` with no admin rights.

### MSIX (self-signed certificate)

MSIX packages must be signed, and Windows only installs them when it trusts the signer. For personal and friends-and-family use a self-signed certificate is enough.

#### 1. Build (creates the certificate the first time)

```powershell
pwsh ./scripts/build-msix.ps1 -Version 0.1.0
```

On the first run the script creates a code-signing certificate in **your** personal store (`Cert:\CurrentUser\My`). Its subject matches the `Publisher` in `src/YtDownloader.App/Package.appxmanifest` (`CN=Rohan Gupta`), it's valid for five years, and its private key never leaves your machine. Later runs reuse it. The output is:

- `artifacts/msix/YtDownloader.App_<version>_x64.msix`
- `artifacts/msix/YtDownloader.cer`, the public certificate. Share this, never the private key.

Want to create the certificate by hand instead? This is exactly what the script does:

```powershell
New-SelfSignedCertificate -Type Custom -Subject "CN=Rohan Gupta" `
  -FriendlyName "YT Downloader package signing" `
  -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 3072 `
  -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddYears(5) `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
```

If you change the `Publisher` in the manifest, use the same value as `-Subject`.

#### Installing the MSIX

Do this once per PC, from **PowerShell run as administrator**, in the folder with the `.cer`:

```powershell
Import-Certificate -FilePath .\YtDownloader.cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople
```

Then double-click the `.msix`, or run `Add-AppxPackage .\YtDownloader.App_0.1.0.0_x64.msix`.

Only trust certificates from people you know. A trusted certificate lets its owner's packages install on your PC without a warning.

To remove the trust later:

```powershell
Get-ChildItem Cert:\LocalMachine\TrustedPeople | Where-Object Subject -eq "CN=Rohan Gupta" | Remove-Item
```

#### Signing MSIX releases in GitHub Actions (maintainers)

The release workflow signs the MSIX when two repository secrets exist. Without them it skips the MSIX and still publishes Setup.exe.

1. Export your certificate with its private key, choosing a strong password:

   ```powershell
   $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object Subject -eq "CN=Rohan Gupta" | Select-Object -First 1
   $password = Read-Host -AsSecureString "PFX password"
   Export-PfxCertificate -Cert $cert -FilePath "$env:TEMP\signing.pfx" -Password $password
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("$env:TEMP\signing.pfx")) | Set-Clipboard
   Remove-Item "$env:TEMP\signing.pfx"
   ```

2. In the repo, open **Settings > Secrets and variables > Actions** and add:
   - `MSIX_CERT_PFX_BASE64`: paste the clipboard
   - `MSIX_CERT_PASSWORD`: the password you chose

Using the same certificate for every release means friends only trust it once.

## Releasing (maintainers)

1. Move the **Unreleased** notes in `CHANGELOG.md` under a new version heading with today's date.
2. Tag and push:

   ```powershell
   git tag v0.1.0
   git push origin v0.1.0
   ```

The [release workflow](.github/workflows/release.yml) runs the tests, builds Setup.exe (with delta updates from the previous release) and the MSIX, and publishes a GitHub Release. Installed copies pick up the update on their next launch.
