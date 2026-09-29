using System.Diagnostics;
using System.IO.Compression;
using YtDownloader.Core.YtDlp;

namespace YtDownloader.Core.Engine;

public sealed record EngineInstallProgress(string Component, long DownloadedBytes, long? TotalBytes)
{
    public double? Fraction => TotalBytes is > 0 ? (double)DownloadedBytes / TotalBytes.Value : null;
}

/// <summary>
/// Downloads yt-dlp, Deno and FFmpeg from their official GitHub releases into a local folder,
/// verifying each against the SHA-256 published alongside it.
/// </summary>
public sealed class EngineManager(string root, HttpClient http)
{
    private sealed record Component(
        string Name,
        string AssetUrl,
        string ChecksumUrl,
        string AssetName,
        // Zip entry name suffix → destination relative to root. Empty means the asset itself is the file.
        (string EntrySuffix, string Destination)[] Files);

    private static readonly Component[] Components =
    [
        new("yt-dlp",
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS",
            "yt-dlp.exe",
            [("", "yt-dlp.exe")]),
        new("Deno",
            "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip",
            "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip.sha256sum",
            "deno-x86_64-pc-windows-msvc.zip",
            [("deno.exe", "deno.exe")]),
        new("FFmpeg",
            "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
            "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/checksums.sha256",
            "ffmpeg-master-latest-win64-gpl.zip",
            [("/bin/ffmpeg.exe", @"ffmpeg\ffmpeg.exe"), ("/bin/ffprobe.exe", @"ffmpeg\ffprobe.exe")]),
    ];

    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtDownloader", "engine");

    public EnginePaths Paths => new(
        Path.Combine(root, "yt-dlp.exe"),
        Path.Combine(root, "ffmpeg"),
        Path.Combine(root, "deno.exe"));

    public bool IsInstalled => Components.All(IsPresent);

    private bool IsPresent(Component c) => c.Files.All(f => File.Exists(Path.Combine(root, f.Destination)));

    /// <summary>Installs any missing components.</summary>
    public async Task InstallAsync(IProgress<EngineInstallProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        foreach (var component in Components.Where(c => !IsPresent(c)))
            await InstallAsync(component, progress, ct);
    }

    private async Task InstallAsync(Component c, IProgress<EngineInstallProgress>? progress, CancellationToken ct)
    {
        var checksums = await http.GetStringAsync(c.ChecksumUrl, ct);
        var expected = ChecksumFile.FindSha256(checksums, c.AssetName)
            ?? throw new InvalidOperationException($"{c.Name}: no checksum published for {c.AssetName}.");

        var temp = Path.Combine(root, c.AssetName + ".part");
        try
        {
            await DownloadAsync(c, temp, progress, ct);
            if (!ChecksumFile.Verify(temp, expected))
                throw new InvalidOperationException($"{c.Name}: downloaded file failed its checksum check.");

            foreach (var (suffix, destination) in c.Files)
            {
                var target = Path.Combine(root, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (suffix == "")
                {
                    File.Move(temp, target, overwrite: true);
                    continue;
                }
                using var zip = ZipFile.OpenRead(temp);
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidOperationException($"{c.Name}: {suffix} not found in archive.");
                entry.ExtractToFile(target, overwrite: true);
            }
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private async Task DownloadAsync(Component c, string path, IProgress<EngineInstallProgress>? progress, CancellationToken ct)
    {
        using var response = await http.GetAsync(c.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var file = File.Create(path);
        var buffer = new byte[81920];
        long done = 0;
        var sinceReport = Stopwatch.StartNew();
        progress?.Report(new(c.Name, 0, total));

        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct);
            done += read;
            if (sinceReport.ElapsedMilliseconds >= 100)
            {
                progress?.Report(new(c.Name, done, total));
                sinceReport.Restart();
            }
        }
        progress?.Report(new(c.Name, done, total));
    }
}
