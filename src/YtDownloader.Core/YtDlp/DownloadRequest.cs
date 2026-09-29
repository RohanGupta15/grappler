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

/// <summary>Optional extras. Subtitles only apply to video downloads.</summary>
public sealed record DownloadOptions
{
    public static readonly DownloadOptions None = new();

    /// <summary>Language code such as "en"; null means no subtitles.</summary>
    public string? SubtitleLanguage { get; init; }
    public bool SkipSponsors { get; init; }
    public TimeSpan? TrimStart { get; init; }
    public TimeSpan? TrimEnd { get; init; }
}

public sealed record DownloadRequest(string Url, Quality Quality, string OutputFolder, DownloadOptions? Options = null);
