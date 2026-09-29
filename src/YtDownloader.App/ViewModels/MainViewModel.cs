using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDownloader.Core.Engine;
using YtDownloader.Core.YtDlp;

namespace YtDownloader.App.ViewModels;

public sealed record QualityOption(string Label, Quality Quality)
{
    public override string ToString() => Label;
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly EngineManager _engine;

    public MainViewModel(EngineManager engine, string outputFolder)
    {
        _engine = engine;
        OutputFolder = outputFolder;
    }

    // ---- Engine setup ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseApp))]
    public partial bool IsEngineReady { get; set; }

    [ObservableProperty]
    public partial bool IsInstallingEngine { get; set; }

    [ObservableProperty]
    public partial string EngineStatus { get; set; } = "";

    [ObservableProperty]
    public partial double EngineProgress { get; set; }

    [ObservableProperty]
    public partial string? EngineError { get; set; }

    public bool CanUseApp => IsEngineReady;

    [RelayCommand]
    private async Task EnsureEngineAsync()
    {
        EngineError = null;
        if (_engine.IsInstalled)
        {
            IsEngineReady = true;
            return;
        }

        IsInstallingEngine = true;
        try
        {
            await _engine.InstallAsync(new Progress<EngineInstallProgress>(p =>
            {
                EngineStatus = p.TotalBytes is { } total
                    ? $"Downloading {p.Component}: {Format.Bytes(p.DownloadedBytes)} of {Format.Bytes(total)}"
                    : $"Downloading {p.Component}: {Format.Bytes(p.DownloadedBytes)}";
                EngineProgress = (p.Fraction ?? 0) * 100;
            }), CancellationToken.None);
            IsEngineReady = true;
        }
        catch (Exception ex)
        {
            EngineError = $"Couldn't set up the download engine: {ex.Message}";
        }
        finally
        {
            IsInstallingEngine = false;
        }
    }

    // ---- Link + preview ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(FetchInfoCommand))]
    public partial string Url { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVideo))]
    public partial VideoInfo? Video { get; set; }

    public bool HasVideo => Video is not null;

    [ObservableProperty]
    public partial string? FetchError { get; set; }

    public ObservableCollection<QualityOption> QualityOptions { get; } = [];

    [ObservableProperty]
    public partial QualityOption? SelectedQuality { get; set; }

    // Fetching replaces the preview, so it's blocked while a download is running.
    private bool CanFetch() => !IsDownloading && Uri.TryCreate(Url.Trim(), UriKind.Absolute, out var u) && u.Scheme is "http" or "https";

    [RelayCommand(CanExecute = nameof(CanFetch))]
    private async Task FetchInfoAsync(CancellationToken ct)
    {
        FetchError = null;
        Video = null;
        ResetDownloadState();
        try
        {
            var info = await new YtDlpRunner(_engine.Paths).GetInfoAsync(Url.Trim(), ct);
            QualityOptions.Clear();
            foreach (var option in BuildQualityOptions(info.AvailableHeights))
                QualityOptions.Add(option);
            SelectedQuality = QualityOptions.FirstOrDefault();
            Video = info;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            FetchError = ex.Message;
        }
    }

    private static IEnumerable<QualityOption> BuildQualityOptions(IReadOnlyList<int> heights)
    {
        yield return new("Best quality" + (heights.Count > 0 ? $" ({Format.Height(heights[0])})" : ""), Quality.BestVideo);
        foreach (var h in heights.Skip(1).Where(h => h >= 144))
            yield return new(Format.Height(h), Quality.VideoUpTo(h));
        yield return new("Audio only (MP3)", Quality.AudioMp3);
        yield return new("Audio only (M4A)", Quality.AudioM4a);
    }

    // ---- Download ----

    [ObservableProperty]
    public partial string OutputFolder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(FetchInfoCommand))]
    public partial bool IsDownloading { get; set; }

    public bool IsIdle => !IsDownloading;

    [ObservableProperty]
    public partial double DownloadProgress { get; set; }

    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; set; }

    [ObservableProperty]
    public partial string DownloadStatus { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCompletedFile), nameof(CompletedFileName))]
    public partial string? CompletedFile { get; set; }

    public bool HasCompletedFile => CompletedFile is not null;
    public string CompletedFileName => Path.GetFileName(CompletedFile ?? "");

    [ObservableProperty]
    public partial string? DownloadError { get; set; }

    private void ResetDownloadState()
    {
        CompletedFile = null;
        DownloadError = null;
        DownloadStatus = "";
        DownloadProgress = 0;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task DownloadAsync(CancellationToken ct)
    {
        if (Video is null || SelectedQuality is null) return;
        ResetDownloadState();
        IsDownloading = true;
        IsProgressIndeterminate = true;
        DownloadStatus = "Starting…";

        // Video downloads fetch separate video and audio streams, then merge them.
        var isVideo = SelectedQuality.Quality is Quality.Video;
        var expectedStreams = isVideo ? 2 : 1;
        var finishedStreams = 0;

        var progress = new Progress<OutputEvent>(e =>
        {
            switch (e)
            {
                case DownloadProgress p:
                    IsProgressIndeterminate = p.Fraction is null;
                    var overall = (Math.Min(finishedStreams, expectedStreams - 1) + (p.Fraction ?? 0)) / expectedStreams;
                    DownloadProgress = overall * 100;
                    var what = !isVideo ? "audio" : finishedStreams == 0 ? "video" : "audio";
                    DownloadStatus = string.Join(" · ", new[]
                    {
                        $"Downloading {what}",
                        p.TotalBytes is { } t ? $"{Format.Bytes(p.DownloadedBytes)} of {Format.Bytes(t)}" : Format.Bytes(p.DownloadedBytes),
                        p.BytesPerSecond is { } s ? $"{Format.Bytes((long)s)}/s" : null,
                        p.Eta is { } eta ? $"{Format.Duration(eta)} left" : null,
                    }.Where(x => x is not null));
                    break;
                case StreamFinished:
                    finishedStreams++;
                    break;
                case PostProcessing pp:
                    IsProgressIndeterminate = true;
                    DownloadStatus = pp.Step switch
                    {
                        "Merger" => "Merging video and audio…",
                        "ExtractAudio" => "Converting audio…",
                        _ => "Finishing up…",
                    };
                    break;
            }
        });

        try
        {
            var request = new DownloadRequest(Video.WebpageUrl ?? Url.Trim(), SelectedQuality.Quality, OutputFolder);
            CompletedFile = await new YtDlpRunner(_engine.Paths).DownloadAsync(request, progress, ct);
            DownloadStatus = "";
            DownloadProgress = 100;
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "Download cancelled.";
            DownloadProgress = 0;
        }
        catch (Exception ex)
        {
            DownloadError = ex.Message;
            DownloadStatus = "";
        }
        finally
        {
            IsProgressIndeterminate = false;
            IsDownloading = false;
        }
    }
}
