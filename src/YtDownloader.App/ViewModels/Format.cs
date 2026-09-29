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
        return unit == 0 ? $"{bytes} B" : $"{value:0.0} {units[unit]}";
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
}
