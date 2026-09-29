using System.Globalization;

namespace YtDownloader.App.ViewModels;

internal static class Format
{
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : value >= 100 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

    public static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");

    public static string Height(int h) => h switch
    {
        >= 4320 => $"{h}p (8K)",
        >= 2160 => $"{h}p (4K)",
        >= 1440 => $"{h}p (2K)",
        >= 1080 => $"{h}p (Full HD)",
        >= 720 => $"{h}p (HD)",
        _ => $"{h}p",
    };

    /// <summary>Parses "90", "1:30" or "1:02:03"; blank gives null.</summary>
    public static bool TryParseTime(string? text, out TimeSpan? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        double seconds = 0;
        foreach (var part in text.Trim().Split(':'))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || n < 0) return false;
            seconds = seconds * 60 + n;
        }
        value = TimeSpan.FromSeconds(seconds);
        return true;
    }
}
