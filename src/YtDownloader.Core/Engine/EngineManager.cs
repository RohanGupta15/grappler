using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using YtDownloader.Core.YtDlp;

namespace YtDownloader.Core.Engine;

public sealed record EngineInstallProgress(string Component, long DownloadedBytes, long? TotalBytes, bool Finished = false)
{
    public double? Fraction => Finished ? 1 : TotalBytes is > 0 ? (double)DownloadedBytes / TotalBytes.Value : null;
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
        (string EntrySuffix, string Destination)[] Files,
        string[] VersionArgs);

    private static readonly Component[] Components =
    [
        new("yt-dlp",
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
            "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS",
            "yt-dlp.exe",
            [("", "yt-dlp.exe")],
            ["--version"]),
        new("Deno",
            "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip",
            "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip.sha256sum",
            "deno-x86_64-pc-windows-msvc.zip",
            [("deno.exe", "deno.exe")],
            ["--version"]),
        new("FFmpeg",
            "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip",
            "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/checksums.sha256",
            "ffmpeg-master-latest-win64-gpl.zip",
            [("/bin/ffmpeg.exe", @"ffmpeg\ffmpeg.exe"), ("/bin/ffprobe.exe", @"ffmpeg\ffprobe.exe")],
            ["-version"]),
    ];

    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtDownloader", "engine");

    public static IReadOnlyList<string> ComponentNames { get; } = Components.Select(c => c.Name).ToArray();

    public EnginePaths Paths => new(
        Path.Combine(root, "yt-dlp.exe"),
        Path.Combine(root, "ffmpeg"),
        Path.Combine(root, "deno.exe"));

    public bool IsInstalled => Components.All(IsPresent);

    public bool IsComponentInstalled(string name) => Components.Any(c => c.Name == name && IsPresent(c));

    private bool IsPresent(Component c) => c.Files.All(f => File.Exists(Path.Combine(root, f.Destination)));

    /// <summary>Installs any missing components.</summary>
    public async Task InstallAsync(IProgress<EngineInstallProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        foreach (var component in Components.Where(c => !IsPresent(c)))
            await InstallAsync(component, await ExpectedHashAsync(component, ct), progress, ct);
    }

    /// <summary>Re-downloads every component whose published release differs from what is installed.</summary>
    /// <returns>Names of the components that were updated.</returns>
    public async Task<IReadOnlyList<string>> UpdateAsync(IProgress<EngineInstallProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(root);
        var installed = ReadInstalledHashes();
        var updated = new List<string>();
        foreach (var component in Components)
        {
            var expected = await ExpectedHashAsync(component, ct);
            if (IsPresent(component) && installed.GetValueOrDefault(component.Name) == expected)
            {
                progress?.Report(new(component.Name, 0, null, Finished: true));
                continue;
            }
            await InstallAsync(component, expected, progress, ct);
            updated.Add(component.Name);
        }
        return updated;
    }

    /// <summary>Asks each installed tool for its version; missing or failing tools are left out.</summary>
    public async Task<IReadOnlyDictionary<string, string>> GetVersionsAsync(CancellationToken ct)
    {
        var versions = new Dictionary<string, string>();
        foreach (var c in Components.Where(IsPresent))
        {
            var exe = Path.Combine(root, c.Files[0].Destination);
            try
            {
                var psi = new ProcessStartInfo(exe) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
                foreach (var a in c.VersionArgs) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi)!;
                var firstLine = (await p.StandardOutput.ReadLineAsync(ct))?.Trim() ?? "";
                await p.WaitForExitAsync(ct);
                versions[c.Name] = ToVersion(c.Name, firstLine);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        }
        return versions;
    }

    // "2026.08.19" / "deno 2.9.7 (stable, ...)" / "ffmpeg version N-12345-gabc-20260928 Copyright ..."
    private static string ToVersion(string component, string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return component switch
        {
            "Deno" when parts.Length > 1 => parts[1],
            "FFmpeg" when parts.Length > 2 => parts[2],
            _ => line,
        };
    }

    private async Task<string> ExpectedHashAsync(Component c, CancellationToken ct)
    {
        var checksums = await http.GetStringAsync(c.ChecksumUrl, ct);
        return ChecksumFile.FindSha256(checksums, c.AssetName)
            ?? throw new InvalidOperationException($"{c.Name}: no checksum published for {c.AssetName}.");
    }

    private async Task InstallAsync(Component c, string expected, IProgress<EngineInstallProgress>? progress, CancellationToken ct)
    {
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

            var installed = ReadInstalledHashes();
            installed[c.Name] = expected;
            File.WriteAllText(InstalledHashesPath, JsonSerializer.Serialize(installed, InstalledHashesJson.Default.DictionaryStringString));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private string InstalledHashesPath => Path.Combine(root, "installed.json");

    private Dictionary<string, string> ReadInstalledHashes()
    {
        try
        {
            return File.Exists(InstalledHashesPath)
                ? JsonSerializer.Deserialize(File.ReadAllText(InstalledHashesPath), InstalledHashesJson.Default.DictionaryStringString) ?? []
                : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task DownloadAsync(Component c, string path, IProgress<EngineInstallProgress>? progress, CancellationToken ct)
    {
        using var response = await http.GetAsync(c.AssetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using (var file = File.Create(path))
        {
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
            progress?.Report(new(c.Name, done, total, Finished: true));
        }
    }
}

// Source-generated so installed.json still works in the trimmed Release build.
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class InstalledHashesJson : System.Text.Json.Serialization.JsonSerializerContext;
