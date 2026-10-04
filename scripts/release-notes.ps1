# Writes the GitHub release notes for a version: its section from CHANGELOG.md plus install steps.
#
#   pwsh ./scripts/release-notes.ps1 -Version 0.1.0 -OutFile notes.md
param(
    [Parameter(Mandatory)] [string]$Version,
    [string]$OutFile = 'release-notes.md'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$changelog = Get-Content (Join-Path $root 'CHANGELOG.md')

$start = ($changelog | Select-String -Pattern "^## \[$([regex]::Escape($Version))\]" | Select-Object -First 1).LineNumber
if (-not $start) { throw "CHANGELOG.md has no '## [$Version]' section" }
$next = ($changelog | Select-Object -Skip $start | Select-String -Pattern '^## \[|^\[[^\]]+\]: ' | Select-Object -First 1).LineNumber
$section = if ($next) { $changelog[$start..($start + $next - 2)] } else { $changelog[$start..($changelog.Count - 1)] }
$section = ($section -join "`n").Trim()

$notes = @"
$section

## Install

1. Download **YtDownloaderApp-win-x64-Setup.exe** below and run it. It installs for your user only, with no admin rights, and adds a Start menu entry and an uninstaller.
2. On first launch the app downloads its engine (yt-dlp, FFmpeg and Deno, about 250 MB). This happens once.

Windows SmartScreen may say "Windows protected your PC" because the installer isn't signed with a paid certificate. Click **More info**, then **Run anyway**.

Already installed? The app updates itself: it downloads this version in the background and offers **Restart now**.

Prefer no installer? Unzip **YtDownloaderApp-win-x64-Portable.zip** and run ``YtDownloader.App.exe``. If an ``.msix`` is attached, see [Installing the MSIX](https://github.com/RohanGupta15/grappler/blob/main/CONTRIBUTING.md#installing-the-msix) for the one-time certificate step.

For personal use only. Please read the [disclaimer](https://github.com/RohanGupta15/grappler#disclaimer).
"@

Set-Content -Path $OutFile -Value $notes -Encoding utf8
Write-Host "Wrote $OutFile"
