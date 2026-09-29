# Third-Party Notices

YT Downloader is MIT licensed (see [LICENSE](LICENSE)). It builds on the projects below, whose licences still apply to them.

## Downloaded by the app on first run

These are not bundled in the source or the installer. The app downloads them from their official GitHub releases, checks each one's SHA-256, and runs them as separate programs.

| Component | Source | Licence |
| --- | --- | --- |
| yt-dlp | [yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp) | [The Unlicense](https://github.com/yt-dlp/yt-dlp/blob/master/LICENSE) (the Windows `.exe` also bundles Python and other components under their own licences) |
| FFmpeg | [yt-dlp/FFmpeg-Builds](https://github.com/yt-dlp/FFmpeg-Builds), `win64-gpl` build | [GNU GPL v3](https://www.gnu.org/licenses/gpl-3.0.html), see [FFmpeg legal](https://ffmpeg.org/legal.html) |
| Deno | [denoland/deno](https://github.com/denoland/deno) | [MIT](https://github.com/denoland/deno/blob/main/LICENSE.md) |

## Libraries included in the app

| Package | Licence |
| --- | --- |
| [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) (WinUI 3) | [MIT](https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE) |
| [.NET runtime](https://github.com/dotnet/runtime) | [MIT](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT) |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) | [MIT](https://github.com/CommunityToolkit/dotnet/blob/main/License.md) |
| [CommunityToolkit.WinUI.Controls](https://github.com/CommunityToolkit/Windows) | [MIT](https://github.com/CommunityToolkit/Windows/blob/main/License.md) |
| [Velopack](https://github.com/velopack/velopack) | [MIT](https://github.com/velopack/velopack/blob/develop/LICENSE) |

## Services

- [SponsorBlock](https://sponsor.ajay.app/): when "Skip sponsor segments" is on, yt-dlp fetches segment data from the SponsorBlock API. The data is [CC BY-NC-SA 4.0](https://github.com/ajayyy/SponsorBlock/wiki/Database-and-API-License).
