namespace YtDownloader.Core.YtDlp;

/// <summary>Builds yt-dlp command lines. Output tags here are what <see cref="OutputLineParser"/> reads back.</summary>
public static class DownloadArgs
{
    public const string ProgressTag = "YTDL";
    public const string PostProcessTag = "YTPP";
    public const string FileTag = "YTFILE";

    public static IReadOnlyList<string> ForInfo(string url, EnginePaths engine) =>
    [
        .. Common(engine),
        "-J",
        "--", url,
    ];

    public static IReadOnlyList<string> ForDownload(DownloadRequest request, EnginePaths engine)
    {
        List<string> args =
        [
            .. Common(engine),
            "--ffmpeg-location", engine.FfmpegDirectory,
            "-P", request.OutputFolder,
            "-o", "%(title)s [%(id)s].%(ext)s",
            "--no-mtime",
            "--newline", "--progress", "--no-colors",
            "--progress-template",
            $"download:{ProgressTag}|%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s",
            "--progress-template", $"postprocess:{PostProcessTag}|%(progress.status)s|%(progress.postprocessor)s",
            "--print", $"after_move:{FileTag}|%(filepath)s",
        ];

        switch (request.Quality)
        {
            case Quality.Video v:
                // Prefer H.264 at equal resolution: it plays everywhere. YouTube only serves it up to 1080p,
                // so higher resolutions fall back to VP9/AV1.
                var res = v.MaxHeight is { } h ? $"res:{h}" : "res";
                args.AddRange(["-f", "bv*+ba/b", "-S", $"{res},vcodec:h264,ext:mp4:m4a", "--merge-output-format", "mp4"]);
                break;
            case Quality.Audio { Format: "m4a" }:
                args.AddRange(["-f", "ba[ext=m4a]/ba/b", "-x", "--audio-format", "m4a"]);
                break;
            case Quality.Audio a:
                args.AddRange(["-f", "ba/b", "-x", "--audio-format", a.Format, "--audio-quality", "0"]);
                break;
        }

        args.AddRange(["--", request.Url]);
        return args;
    }

    private static string[] Common(EnginePaths engine) =>
    [
        "--ignore-config",
        "--no-playlist",
        "--js-runtimes", $"deno:{engine.Deno}",
    ];
}
