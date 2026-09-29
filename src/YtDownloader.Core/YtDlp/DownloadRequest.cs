namespace YtDownloader.Core.YtDlp;

/// <summary>Locations of the external tools yt-dlp relies on.</summary>
public sealed record EnginePaths(string YtDlp, string FfmpegDirectory, string Deno);

public abstract record Quality
{
    public static readonly Quality BestVideo = new Video(null);
    public static Quality VideoUpTo(int height) => new Video(height);
    public static readonly Quality AudioMp3 = new Audio("mp3");
    public static readonly Quality AudioM4a = new Audio("m4a");

    /// <param name="MaxHeight">null means the highest available.</param>
    public sealed record Video(int? MaxHeight) : Quality;

    public sealed record Audio(string Format) : Quality;
}

public sealed record DownloadRequest(string Url, Quality Quality, string OutputFolder);
