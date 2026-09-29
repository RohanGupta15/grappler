using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDownloader.App.Services;
using YtDownloader.Core.Engine;

namespace YtDownloader.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly EngineManager _engine;
    private readonly DownloadQueue _queue;

    public SettingsViewModel(AppSettings settings, EngineManager engine, DownloadQueue queue)
    {
        Settings = settings;
        _engine = engine;
        _queue = queue;
        SelectedQuality = QualityOption.FromKey(settings.DefaultQuality);
        SelectedSubtitles = SubtitleOption.FromCode(settings.SubtitleLanguage);
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.DefaultQuality)) SelectedQuality = QualityOption.FromKey(settings.DefaultQuality);
        };
        // Replacing yt-dlp.exe while it runs would fail, so updates wait for downloads to finish.
        queue.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DownloadQueue.HasActive)) CheckForUpdatesCommand.NotifyCanExecuteChanged();
        };
    }

    public AppSettings Settings { get; }

    public IReadOnlyList<QualityOption> QualityChoices => QualityOption.Standard;
    public IReadOnlyList<SubtitleOption> SubtitleChoices => SubtitleOption.All;

    [ObservableProperty]
    public partial QualityOption SelectedQuality { get; set; }

    partial void OnSelectedQualityChanged(QualityOption value) => Settings.DefaultQuality = value.Key;

    [ObservableProperty]
    public partial SubtitleOption SelectedSubtitles { get; set; }

    partial void OnSelectedSubtitlesChanged(SubtitleOption value) => Settings.SubtitleLanguage = value.Code;

    public double MaxConcurrent
    {
        get => Settings.MaxConcurrent;
        set
        {
            // NumberBox reports NaN when cleared.
            if (double.IsNaN(value)) return;
            Settings.MaxConcurrent = Math.Clamp((int)Math.Round(value), 1, 5);
            OnPropertyChanged();
        }
    }

    // ---- Engine ----

    [ObservableProperty]
    public partial string YtDlpVersion { get; set; } = "…";

    [ObservableProperty]
    public partial string DenoVersion { get; set; } = "…";

    [ObservableProperty]
    public partial string FfmpegVersion { get; set; } = "…";

    [ObservableProperty]
    public partial string EngineStatus { get; set; } = "Update when downloads start failing. YouTube changes often.";

    public async Task LoadVersionsAsync()
    {
        var v = await _engine.GetVersionsAsync(CancellationToken.None);
        YtDlpVersion = v.GetValueOrDefault("yt-dlp", "Not installed");
        DenoVersion = v.GetValueOrDefault("Deno", "Not installed");
        FfmpegVersion = v.GetValueOrDefault("FFmpeg", "Not installed");
    }

    private bool CanUpdate() => !_queue.HasActive;

    [RelayCommand(CanExecute = nameof(CanUpdate))]
    private async Task CheckForUpdatesAsync()
    {
        EngineStatus = "Checking for updates…";
        try
        {
            var updated = await _engine.UpdateAsync(new Progress<EngineInstallProgress>(p =>
            {
                if (!p.Finished && p.TotalBytes is { } t)
                    EngineStatus = $"Updating {p.Component}: {Format.Bytes(p.DownloadedBytes)} of {Format.Bytes(t)}";
            }), CancellationToken.None);
            EngineStatus = updated.Count == 0 ? "Everything is up to date." : $"Updated {string.Join(", ", updated)}.";
            await LoadVersionsAsync();
        }
        catch (Exception ex)
        {
            EngineStatus = $"Couldn't check for updates: {ex.Message}";
        }
    }
}
