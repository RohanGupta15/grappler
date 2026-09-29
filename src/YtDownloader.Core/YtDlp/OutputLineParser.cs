using System.Globalization;

namespace YtDownloader.Core.YtDlp;

public abstract record OutputEvent;

/// <summary>Progress of the stream currently downloading (video and audio are separate streams).</summary>
public sealed record DownloadProgress(long DownloadedBytes, long? TotalBytes, double? BytesPerSecond, TimeSpan? Eta)
    : OutputEvent
{
    public double? Fraction => TotalBytes is > 0 ? (double)DownloadedBytes / TotalBytes.Value : null;
}

public sealed record StreamFinished : OutputEvent;

/// <summary>A post-processor (e.g. Merger, ExtractAudio) has started.</summary>
public sealed record PostProcessing(string Step) : OutputEvent;

public sealed record FileCompleted(string Path) : OutputEvent;

public sealed record DownloadError(string Message) : OutputEvent;

/// <summary>Parses one line of yt-dlp output produced with the templates in <see cref="DownloadArgs"/>.</summary>
public static class OutputLineParser
{
    public static OutputEvent? Parse(string line)
    {
        if (line.StartsWith("ERROR:", StringComparison.Ordinal))
            return new DownloadError(line["ERROR:".Length..].Trim());

        // The path may itself contain '|', so only split off the tag.
        if (line.StartsWith(DownloadArgs.FileTag + "|", StringComparison.Ordinal))
            return new FileCompleted(line[(DownloadArgs.FileTag.Length + 1)..]);

        var parts = line.Split('|');
        if (parts[0] == DownloadArgs.ProgressTag && parts.Length >= 7)
        {
            return parts[1] switch
            {
                "downloading" => new DownloadProgress(
                    DownloadedBytes: Long(parts[2]) ?? 0,
                    TotalBytes: Long(parts[3]) ?? Long(parts[4]),
                    BytesPerSecond: Double(parts[5]),
                    Eta: Double(parts[6]) is { } eta ? TimeSpan.FromSeconds(eta) : null),
                "finished" => new StreamFinished(),
                _ => null,
            };
        }

        if (parts[0] == DownloadArgs.PostProcessTag && parts.Length >= 3 && parts[1] == "started")
            return new PostProcessing(parts[2]);

        return null;
    }

    private static double? Double(string s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    // total_bytes_estimate can be fractional, so parse as double and round.
    private static long? Long(string s) => Double(s) is { } v ? (long)Math.Round(v) : null;
}
