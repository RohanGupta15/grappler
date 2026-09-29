using System.Text.Json;

namespace YtDownloader.Core.YtDlp;

/// <summary>Turns the JSON printed by <c>yt-dlp -J</c> for a single video into a <see cref="VideoInfo"/>.</summary>
public static class VideoInfoParser
{
    public static VideoInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return Parse(doc.RootElement);
    }

    internal static VideoInfo Parse(JsonElement root)
    {
        var heights = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        if (root.TryGetProperty("formats", out var formats) && formats.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in formats.EnumerateArray())
            {
                var hasVideo = Json.String(f, "vcodec") is { } vcodec && vcodec != "none";
                if (hasVideo && f.TryGetProperty("height", out var h) && h.ValueKind == JsonValueKind.Number)
                    heights.Add(h.GetInt32());
            }
        }

        return new VideoInfo(
            Id: Json.String(root, "id") ?? "",
            Title: Json.String(root, "title") ?? "",
            Channel: Json.String(root, "channel") ?? Json.String(root, "uploader"),
            Duration: Json.Seconds(root, "duration"),
            ThumbnailUrl: Json.String(root, "thumbnail"),
            WebpageUrl: Json.String(root, "webpage_url"),
            AvailableHeights: heights.ToList());
    }
}
