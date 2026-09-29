using System.Text.Json;

namespace YtDownloader.Core.YtDlp;

/// <summary>Parses <c>yt-dlp -J --flat-playlist</c> output, which is either a video or a playlist.</summary>
public static class MediaInfoParser
{
    public static MediaInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return Json.String(root, "_type") == "playlist" ? ParsePlaylist(root) : VideoInfoParser.Parse(root);
    }

    private static PlaylistInfo ParsePlaylist(JsonElement root)
    {
        var entries = new List<PlaylistEntry>();
        if (root.TryGetProperty("entries", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in list.EnumerateArray())
            {
                if (Json.String(e, "url") is not { } url) continue;
                entries.Add(new PlaylistEntry(
                    Id: Json.String(e, "id") ?? url,
                    Title: Json.String(e, "title") ?? url,
                    Url: url,
                    Duration: Json.Seconds(e, "duration"),
                    ThumbnailUrl: FirstThumbnail(e)));
            }
        }

        var total = root.TryGetProperty("playlist_count", out var c) && c.ValueKind == JsonValueKind.Number
            ? c.GetInt32()
            : entries.Count;

        return new PlaylistInfo(
            Id: Json.String(root, "id") ?? "",
            Title: Json.String(root, "title") ?? "",
            Channel: Json.String(root, "channel") ?? Json.String(root, "uploader"),
            WebpageUrl: Json.String(root, "webpage_url"),
            TotalCount: total,
            Entries: entries);
    }

    private static string? FirstThumbnail(JsonElement e) =>
        e.TryGetProperty("thumbnails", out var t) && t.ValueKind == JsonValueKind.Array && t.GetArrayLength() > 0
            ? Json.String(t[0], "url")
            : null;
}

internal static class Json
{
    public static string? String(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    public static TimeSpan? Seconds(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? TimeSpan.FromSeconds(v.GetDouble()) : null;
}
