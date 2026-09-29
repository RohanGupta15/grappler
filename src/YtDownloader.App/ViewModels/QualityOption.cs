using YtDownloader.Core.YtDlp;

namespace YtDownloader.App.ViewModels;

public sealed record QualityOption(string Key, string Label, Quality Quality)
{
    public override string ToString() => Label;

    /// <summary>Choices offered before a video's own formats are known (add bar, playlists, Settings).</summary>
    public static IReadOnlyList<QualityOption> Standard { get; } =
    [
        new("best", "Best quality", Quality.BestVideo),
        new("1080", Format.Height(1080), Quality.VideoUpTo(1080)),
        new("720", Format.Height(720), Quality.VideoUpTo(720)),
        new("480", Format.Height(480), Quality.VideoUpTo(480)),
        new("mp3", "Audio only (MP3)", Quality.AudioMp3),
        new("m4a", "Audio only (M4A)", Quality.AudioM4a),
    ];

    public static QualityOption FromKey(string? key) => Standard.FirstOrDefault(o => o.Key == key) ?? Standard[0];

    /// <summary>Choices for a video whose available heights are known.</summary>
    public static List<QualityOption> ForHeights(IReadOnlyList<int> heights)
    {
        List<QualityOption> options = [new("best", heights.Count > 0 ? $"Best ({heights[0]}p)" : "Best quality", Quality.BestVideo)];
        options.AddRange(heights.Skip(1).Where(h => h >= 144).Select(h => new QualityOption(h.ToString(), Format.Height(h), Quality.VideoUpTo(h))));
        options.Add(Standard[4]);
        options.Add(Standard[5]);
        return options;
    }

    /// <summary>The option in <paramref name="options"/> closest to the user's default without going over.</summary>
    public static QualityOption Pick(IReadOnlyList<QualityOption> options, string defaultKey)
    {
        if (options.FirstOrDefault(o => o.Key == defaultKey) is { } exact) return exact;
        if (!int.TryParse(defaultKey, out var wanted)) return options[0];
        var videos = options.Where(o => int.TryParse(o.Key, out _)).ToList();
        return videos.Where(o => int.Parse(o.Key) <= wanted).FirstOrDefault()
            ?? videos.LastOrDefault()
            ?? options[0];
    }
}

public sealed record SubtitleOption(string? Code, string Label)
{
    public override string ToString() => Label;

    public static IReadOnlyList<SubtitleOption> All { get; } =
    [
        new(null, "Off"),
        new("en", "English"),
        new("es", "Spanish"),
        new("fr", "French"),
        new("de", "German"),
        new("hi", "Hindi"),
        new("ja", "Japanese"),
        new("pt", "Portuguese"),
    ];

    public static SubtitleOption FromCode(string? code) => All.FirstOrDefault(o => o.Code == code) ?? All[0];
}
