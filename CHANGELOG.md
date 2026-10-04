# Changelog

Notable changes to this project are recorded here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Fixed

- Releases now get their notes from this changelog automatically.

## [0.1.0] - 2026-10-04

First public test release.

### Added

- One-page download manager: link box with a default quality, and a list grouped into Downloading, Up next, Playlists and Done.
- Added videos wait as "Ready to download" until you press Start, or **Start all**.
- Quality choices: best available, 1080p, 720p, 480p, or audio only as M4A or MP3.
- Per-download subtitles, SponsorBlock sponsor removal and trimming to a clip.
- Playlists with a video picker.
- Pause and resume from the partial file, cancel with clean-up, retry, and 1 to 5 downloads at once.
- Clipboard link offer and Ctrl+V anywhere in the window.
- First-run engine setup that downloads and SHA-256 checks yt-dlp, FFmpeg and Deno, plus an engine update check in Settings.
- Overall download progress on the taskbar button: green while downloading, yellow when everything is paused.
- A Windows notification when downloads finish while the app is in the background, with Open and Show in folder for a single video. It can be turned off in Settings.
- Setup.exe installer with automatic app updates from GitHub Releases, and a signed MSIX.

[Unreleased]: https://github.com/RohanGupta15/grappler/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/RohanGupta15/grappler/releases/tag/v0.1.0
