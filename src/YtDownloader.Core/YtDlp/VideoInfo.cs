namespace YtDownloader.Core.YtDlp;

/// <summary>What a link points at: one video or a playlist.</summary>
public abstract record MediaInfo(string Id, string Title, string? Channel, string? WebpageUrl);

public sealed record VideoInfo(
    string Id,
    string Title,
    string? Channel,
    TimeSpan? Duration,
    string? ThumbnailUrl,
    string? WebpageUrl,
    IReadOnlyList<int> AvailableHeights) : MediaInfo(Id, Title, Channel, WebpageUrl);

public sealed record PlaylistEntry(string Id, string Title, string Url, TimeSpan? Duration, string? ThumbnailUrl);

public sealed record PlaylistInfo(
    string Id,
    string Title,
    string? Channel,
    string? WebpageUrl,
    int TotalCount,
    IReadOnlyList<PlaylistEntry> Entries) : MediaInfo(Id, Title, Channel, WebpageUrl);
