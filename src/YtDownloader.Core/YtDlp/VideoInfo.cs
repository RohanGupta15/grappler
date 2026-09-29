namespace YtDownloader.Core.YtDlp;

public sealed record VideoInfo(
    string Id,
    string Title,
    string? Channel,
    TimeSpan? Duration,
    string? ThumbnailUrl,
    string? WebpageUrl,
    IReadOnlyList<int> AvailableHeights);
