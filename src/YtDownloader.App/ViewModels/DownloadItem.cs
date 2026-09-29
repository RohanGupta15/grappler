using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDownloader.Core.YtDlp;

namespace YtDownloader.App.ViewModels;

public enum DownloadStatus { LookingUp, Ready, Waiting, Paused, Downloading, Processing, Done, Failed }

public sealed partial class DownloadItem : ObservableObject
{
    private readonly DownloadQueue _queue;

    public DownloadItem(DownloadQueue queue, string url)
    {
        _queue = queue;
        Url = url;
        Title = url;
    }

    public string Url { get; }
    internal CancellationTokenSource? Cts { get; set; }
    internal bool PauseRequested { get; set; }

    /// <summary>Stream files yt-dlp has written for this item, so partial downloads can be cleaned up.</summary>
    internal HashSet<string> StreamPaths { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Byline))]
    public partial string? Channel { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Byline), nameof(DurationText))]
    public partial TimeSpan? Duration { get; set; }

    [ObservableProperty]
    public partial string? ThumbnailUrl { get; set; }

    public string DurationText => Duration is { } d ? Format.Duration(d) : "";
    public string Byline => string.Join(" · ", new[] { Channel, Duration is { } d ? Format.Duration(d) : null }.Where(s => !string.IsNullOrEmpty(s)));

    // ---- Status ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLookingUp), nameof(CanEditOptions), nameof(IsPaused), nameof(CanStart), nameof(StartText), nameof(IsDone), nameof(IsFailed), nameof(StatusGlyph))]
    public partial DownloadStatus Status { get; set; }

    public bool IsLookingUp => Status == DownloadStatus.LookingUp;
    public bool CanEditOptions => Status is DownloadStatus.Ready or DownloadStatus.Waiting or DownloadStatus.Paused;
    /// <summary>Looked up but not started yet, or paused: the row shows its start button.</summary>
    public bool CanStart => Status is DownloadStatus.Ready or DownloadStatus.Paused;
    public string StartText => IsPaused ? "Resume" : "Start download";
    public bool IsPaused => Status == DownloadStatus.Paused;
    public bool IsDone => Status == DownloadStatus.Done;
    public bool IsFailed => Status == DownloadStatus.Failed;
    public string StatusGlyph => IsFailed ? "" : "";

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string PercentText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileName))]
    public partial string? FilePath { get; set; }

    public string FileName => FilePath is null ? Title : System.IO.Path.GetFileName(FilePath);

    [ObservableProperty]
    public partial string SizeText { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    // ---- Options (editable while waiting) ----

    public ObservableCollection<QualityOption> QualityOptions { get; } = [];

    [ObservableProperty]
    public partial QualityOption? SelectedQuality { get; set; }

    public IReadOnlyList<SubtitleOption> SubtitleOptions => SubtitleOption.All;

    [ObservableProperty]
    public partial SubtitleOption SelectedSubtitles { get; set; } = SubtitleOption.All[0];

    [ObservableProperty]
    public partial bool SkipSponsors { get; set; }

    [ObservableProperty]
    public partial string TrimStartText { get; set; } = "";

    [ObservableProperty]
    public partial string TrimEndText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public DownloadOptions BuildOptions()
    {
        Format.TryParseTime(TrimStartText, out var start);
        Format.TryParseTime(TrimEndText, out var end);
        return new DownloadOptions
        {
            SubtitleLanguage = SelectedSubtitles.Code,
            SkipSponsors = SkipSponsors,
            TrimStart = start,
            TrimEnd = end,
        };
    }

    // ---- Row actions ----

    [RelayCommand] private void Pause() => _queue.Pause(this);
    [RelayCommand] private void Resume() => _queue.Resume(this);
    [RelayCommand] private void Cancel() => _queue.Remove(this);
    [RelayCommand] private void Retry() => _queue.Retry(this);

    [RelayCommand]
    private void Open()
    {
        if (FilePath is { } p && File.Exists(p))
            Process.Start(new ProcessStartInfo(p) { UseShellExecute = true });
    }

    [RelayCommand]
    private void ShowInFolder()
    {
        if (FilePath is { } p)
            Process.Start("explorer.exe", $"/select,\"{p}\"");
    }
}
