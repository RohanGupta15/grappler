using System.Diagnostics;
using System.Text;

namespace YtDownloader.Core.YtDlp;

public sealed class YtDlpException(string message) : Exception(message);

/// <summary>Runs yt-dlp as a child process and translates its output.</summary>
public sealed class YtDlpRunner(EnginePaths engine)
{
    public async Task<VideoInfo> GetInfoAsync(string url, CancellationToken ct)
    {
        var json = new StringBuilder();
        await RunAsync(DownloadArgs.ForInfo(url, engine), line => json.AppendLine(line), null, ct);
        return VideoInfoParser.Parse(json.ToString());
    }

    /// <summary>Downloads and returns the final file path, reporting parsed output events as they arrive.</summary>
    public async Task<string?> DownloadAsync(DownloadRequest request, IProgress<OutputEvent> progress, CancellationToken ct)
    {
        string? finalPath = null;
        await RunAsync(DownloadArgs.ForDownload(request, engine), null, e =>
        {
            if (e is FileCompleted f) finalPath = f.Path;
            progress.Report(e);
        }, ct);
        return finalPath;
    }

    private async Task RunAsync(IReadOnlyList<string> args, Action<string>? onStdout, Action<OutputEvent>? onEvent, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(engine.YtDlp)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        // Non-ASCII titles and paths come through garbled without this.
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        psi.Environment["PYTHONUTF8"] = "1";

        using var process = new Process { StartInfo = psi };
        string? lastError = null;

        void Handle(string? line, bool stdout)
        {
            if (line is null) return;
            if (stdout) onStdout?.Invoke(line);
            if (OutputLineParser.Parse(line) is not { } e) return;
            if (e is DownloadError err) lastError = err.Message;
            onEvent?.Invoke(e);
        }

        process.OutputDataReceived += (_, e) => Handle(e.Data, stdout: true);
        process.ErrorDataReceived += (_, e) => Handle(e.Data, stdout: false);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // yt-dlp spawns ffmpeg, so cancellation must take down the whole tree.
        await using (ct.Register(() => { try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } }))
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }

        ct.ThrowIfCancellationRequested();
        if (process.ExitCode != 0)
            throw new YtDlpException(lastError ?? $"yt-dlp exited with code {process.ExitCode}.");
    }
}
