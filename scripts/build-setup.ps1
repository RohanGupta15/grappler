# Builds YtDownloaderApp-win-x64-Setup.exe with Velopack into artifacts/releases.
# Setup.exe installs per user (no admin, no certificate) into %LOCALAPPDATA%\YtDownloaderApp,
# adds Start menu and desktop shortcuts and an uninstaller, and the app updates itself from
# GitHub Releases afterwards. The pack id differs from the app's data folder (%LOCALAPPDATA%\YtDownloader)
# so installs, updates and uninstalls never touch settings or the downloaded engines.
#
#   pwsh ./scripts/build-setup.ps1 -Version 0.1.0
param(
    [string]$Version = '0.1.0',
    [ValidateSet('x64', 'ARM64')] [string]$Platform = 'x64'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\YtDownloader.App\YtDownloader.App.csproj'
$rid = "win-$($Platform.ToLowerInvariant())"
$releases = Join-Path $root 'artifacts\releases'

Push-Location $root
try {
    dotnet tool restore | Out-Null

    # Unpackaged and self-contained: runs on any Windows 10 1809+ PC without installing .NET or the
    # Windows App SDK. Published to a clean folder of its own so no MSIX build output mixes in.
    $publish = Join-Path $root "artifacts\publish\$rid"
    if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
    dotnet publish $project -c Release -p:Platform=$Platform -r $rid `
        -p:WindowsPackageType=None `
        -p:WindowsAppSDKSelfContained=true `
        -p:Version=$Version `
        -p:PublishDir="$publish\"
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

    dotnet vpk pack `
        --packId YtDownloaderApp `
        --packVersion $Version `
        --packTitle 'YT Downloader' `
        --packAuthors 'Rohan Gupta' `
        --packDir $publish `
        --mainExe YtDownloader.App.exe `
        --icon (Join-Path $root 'src\YtDownloader.App\Assets\AppIcon.ico') `
        --channel $rid `
        --outputDir $releases
    if ($LASTEXITCODE -ne 0) { throw "vpk pack failed ($LASTEXITCODE)" }
}
finally { Pop-Location }

Get-ChildItem $releases -File | ForEach-Object { '{0}  {1:N1} MB' -f $_.Name, ($_.Length / 1MB) }
