using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace YtDownloader.App.Services;

/// <summary>User preferences, saved to settings.json whenever one changes.</summary>
public sealed partial class AppSettings : ObservableObject
{
    private string? _path;

    [ObservableProperty]
    public partial string OutputFolder { get; set; } = "";

    /// <summary>Key from <see cref="ViewModels.QualityOption.Standard"/>: "best", a height like "1080", "mp3" or "m4a".</summary>
    [ObservableProperty]
    public partial string DefaultQuality { get; set; } = "best";

    [ObservableProperty]
    public partial int MaxConcurrent { get; set; } = 3;

    [ObservableProperty]
    public partial bool OfferClipboard { get; set; } = true;

    /// <summary>Show a Windows notification when downloads finish while the window is in the background.</summary>
    [ObservableProperty]
    public partial bool NotifyWhenFinished { get; set; } = true;

    /// <summary>Language code such as "en"; null means no subtitles.</summary>
    [ObservableProperty]
    public partial string? SubtitleLanguage { get; set; }

    [ObservableProperty]
    public partial bool SkipSponsors { get; set; }

    public static AppSettings Load(string path, string defaultOutputFolder)
    {
        AppSettings settings;
        try
        {
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJson.Default.AppSettings) ?? new()
                : new();
        }
        catch (JsonException)
        {
            settings = new();
        }

        if (string.IsNullOrWhiteSpace(settings.OutputFolder)) settings.OutputFolder = defaultOutputFolder;
        settings.MaxConcurrent = Math.Clamp(settings.MaxConcurrent, 1, 5);
        settings._path = path;
        settings.PropertyChanged += (_, _) => settings.Save();
        return settings;
    }

    private void Save()
    {
        if (_path is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(this, AppSettingsJson.Default.AppSettings));
        }
        catch (IOException) { }
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(WriteIndented = true)]
[System.Text.Json.Serialization.JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJson : System.Text.Json.Serialization.JsonSerializerContext;
