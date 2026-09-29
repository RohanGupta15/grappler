using System.Text.Json;

namespace YtDownloader.Core.YtDlp;

/// <summary>Turns the JSON printed by <c>yt-dlp -J</c> into a <see cref="VideoInfo"/>.</summary>
public static class VideoInfoParser
{
    public static VideoInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var heights = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in formats.EnumerateArray())
            {
                var hasVideo = String(f, "vcodec") is { } vcodec && vcodec != "none";
                if (hasVideo && f.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number)
                    heights.Add(h.GetInt32());
            }
        }

        double? seconds = root.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
            ? d.GetDouble()
            : null;

        return new VideoInfo(
            Id: String(root, "id") ?? "",
            Title: String(root, "title") ?? "",
            Channel: String(root, "channel") ?? String(root, "uploader"),
            Duration: seconds is { } s ? TimeSpan.FromSeconds(s) : null,
            ThumbnailUrl: String(root, "thumbnail"),
            WebpageUrl: String(root, "webpage_url"),
            AvailableHeights: heights.ToList());
    }

    private static string? String(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
