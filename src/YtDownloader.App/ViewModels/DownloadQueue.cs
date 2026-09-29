using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDownloader.App.Services;
using YtDownloader.Core.Engine;
using YtDownloader.Core.YtDlp;

namespace YtDownloader.App.ViewModels;

/// <summary>
/// Owns every download and playlist, keeps them in the list group matching their status, and runs up to
/// <see cref="AppSettings.MaxConcurrent"/> downloads at once. Lives on the UI thread.
/// </summary>
public sealed partial class DownloadQueue : ObservableObject
{
    private readonly EngineManager _engine;
    private readonly AppSettings _settings;

    public DownloadQueue(EngineManager engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;
        foreach (var c in new System.Collections.Specialized.INotifyCollectionChanged[] { Active, Waiting, Playlists, Finished })
            c.CollectionChanged += (_, _) => OnGroupsChanged();
        settings.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppSettings.MaxConcurrent)) Pump(); };
    }

    public ObservableCollection<DownloadItem> Active { get; } = [];
    public ObservableCollection<DownloadItem> Waiting { get; } = [];
    public ObservableCollection<PlaylistItem> Playlists { get; } = [];
    public ObservableCollection<DownloadItem> Finished { get; } = [];

    public bool HasActive => Active.Count > 0;
    public bool HasWaiting => Waiting.Count > 0;
    public bool HasPlaylists => Playlists.Count > 0;
    public bool HasFinished => Finished.Count > 0;
    public bool IsEmpty => !HasActive && !HasWaiting && !HasPlaylists && !HasFinished;
    public string ActiveCount => Active.Count.ToString();
    public string WaitingCount => Waiting.Count.ToString();
    public string FinishedCount => Finished.Count.ToString();

    /// <summary>Raised when a playlist has been looked up, so the page can offer the picker.</summary>
    public event EventHandler<PlaylistItem>? PlaylistAdded;

    private void OnGroupsChanged()
    {
        foreach (var name in new[] { nameof(HasActive), nameof(HasWaiting), nameof(HasPlaylists), nameof(HasFinished), nameof(IsEmpty), nameof(ActiveCount), nameof(WaitingCount), nameof(FinishedCount) })
            OnPropertyChanged(name);
        PauseAllCommand.NotifyCanExecuteChanged();
        ClearFinishedCommand.NotifyCanExecuteChanged();
    }

    private YtDlpRunner Runner => new(_engine.Paths);

    public bool Contains(string url) =>
        Active.Concat(Waiting).Concat(Finished).Any(i => i.Url == url) || Playlists.Any(p => p.Info.WebpageUrl == url);

    // ---- Adding ----

    public async Task AddAsync(string url, QualityOption defaultQuality)
    {
        var item = NewItem(url);
        item.Status = DownloadStatus.LookingUp;
        item.StatusText = "Looking up…";
        Waiting.Add(item);
        await LookUpAsync(item, defaultQuality);
    }

    private async Task LookUpAsync(DownloadItem item, QualityOption defaultQuality)
    {
        try
        {
            var info = await Runner.GetInfoAsync(item.Url, CancellationToken.None);
            if (!Waiting.Contains(item)) return; // removed while looking up

            if (info is PlaylistInfo playlist)
            {
                Waiting.Remove(item);
                var p = new PlaylistItem(this, playlist, defaultQuality);
                Playlists.Add(p);
                PlaylistAdded?.Invoke(this, p);
                return;
            }

            var video = (VideoInfo)info;
            item.Title = video.Title;
            item.Channel = video.Channel;
            item.Duration = video.Duration;
            item.ThumbnailUrl = video.ThumbnailUrl;
            SetQualityOptions(item, QualityOption.ForHeights(video.AvailableHeights), defaultQuality.Key);
            MoveTo(item, DownloadStatus.Waiting);
            Pump();
        }
        catch (Exception ex)
        {
            if (!Waiting.Contains(item)) return;
            item.ErrorMessage = FriendlyError(ex);
            MoveTo(item, DownloadStatus.Failed);
        }
    }

    private DownloadItem NewItem(string url) => new(this, url)
    {
        SelectedSubtitles = SubtitleOption.FromCode(_settings.SubtitleLanguage),
        SkipSponsors = _settings.SkipSponsors,
    };

    private static void SetQualityOptions(DownloadItem item, IReadOnlyList<QualityOption> options, string defaultKey)
    {
        item.QualityOptions.Clear();
        foreach (var o in options) item.QualityOptions.Add(o);
        item.SelectedQuality = QualityOption.Pick(options, defaultKey);
    }

    public void StartPlaylist(PlaylistItem playlist)
    {
        foreach (var entry in playlist.Entries.Where(e => e.IsSelected))
        {
            var item = NewItem(entry.Entry.Url);
            item.Title = entry.Entry.Title;
            item.Channel = playlist.Info.Channel;
            item.Duration = entry.Entry.Duration;
            item.ThumbnailUrl = entry.Entry.ThumbnailUrl;
            SetQualityOptions(item, QualityOption.Standard, playlist.SelectedQuality.Key);
            item.Status = DownloadStatus.Waiting;
            item.StatusText = "Waiting";
            Waiting.Add(item);
        }
        Playlists.Remove(playlist);
        Pump();
    }

    public void RemovePlaylist(PlaylistItem playlist) => Playlists.Remove(playlist);

    // ---- Scheduling ----

    private void Pump()
    {
        while (Active.Count < _settings.MaxConcurrent && Waiting.FirstOrDefault(i => i.Status == DownloadStatus.Waiting) is { } next)
            _ = RunAsync(next);
    }

    private async Task RunAsync(DownloadItem item)
    {
        var cts = new CancellationTokenSource();
        item.Cts = cts;
        item.PauseRequested = false;
        item.ErrorMessage = null;
        item.IsExpanded = false;
        MoveTo(item, DownloadStatus.Downloading);
        item.IsIndeterminate = true;
        item.StatusText = "Starting…";

        var isVideo = item.SelectedQuality?.Quality is Quality.Video;
        var expectedStreams = isVideo ? 2 : 1;
        var finishedStreams = 0;

        var progress = new Progress<OutputEvent>(e =>
        {
            switch (e)
            {
                case DownloadProgress p:
                    if (p.StreamPath is { } streamPath) item.StreamPaths.Add(streamPath);
                    item.Status = DownloadStatus.Downloading;
                    item.IsIndeterminate = p.Fraction is null;
                    var overall = (Math.Min(finishedStreams, expectedStreams - 1) + (p.Fraction ?? 0)) / expectedStreams;
                    item.Progress = overall * 100;
                    item.PercentText = $"{overall:0%}";
                    var what = !isVideo ? "audio" : finishedStreams == 0 ? "video" : "audio";
                    // Most useful first: the line is trimmed, not wrapped, in narrow windows.
                    item.StatusText = string.Join(" · ", new[]
                    {
                        p.Eta is { } eta ? $"{Format.Duration(eta)} left" : $"Downloading {what}",
                        p.BytesPerSecond is { } s ? $"{Format.Bytes((long)s)}/s" : null,
                        p.TotalBytes is { } t ? $"{Format.Bytes(p.DownloadedBytes)} of {Format.Bytes(t)}" : Format.Bytes(p.DownloadedBytes),
                        p.Eta is not null ? (what == "video" ? "Video" : "Audio") : null,
                        item.SelectedQuality?.Label,
                    }.Where(x => x is not null));
                    break;
                case StreamFinished f:
                    if (f.StreamPath is { } finishedPath) item.StreamPaths.Add(finishedPath);
                    finishedStreams++;
                    break;
                case PostProcessing pp:
                    item.Status = DownloadStatus.Processing;
                    item.IsIndeterminate = true;
                    item.PercentText = "";
                    item.StatusText = pp.Step switch
                    {
                        "Merger" => "Merging video and audio…",
                        "ExtractAudio" => "Converting audio…",
                        "SponsorBlock" or "ModifyChapters" => "Removing sponsor segments…",
                        _ => "Finishing up…",
                    };
                    break;
            }
        });

        try
        {
            var request = new DownloadRequest(item.Url, item.SelectedQuality?.Quality ?? Quality.BestVideo, _settings.OutputFolder, item.BuildOptions());
            var path = await Runner.DownloadAsync(request, progress, cts.Token);
            item.FilePath = path;
            item.SizeText = path is not null && File.Exists(path) ? Format.Bytes(new FileInfo(path).Length) : "";
            item.Progress = 100;
            MoveTo(item, DownloadStatus.Done);
        }
        catch (OperationCanceledException)
        {
            if (item.PauseRequested)
            {
                // yt-dlp keeps the .part file and resumes from it next time.
                item.StatusText = "Paused";
                MoveTo(item, DownloadStatus.Paused);
            }
            else
            {
                Active.Remove(item);
                Waiting.Remove(item);
                DeletePartialFiles(item);
            }
        }
        catch (Exception ex)
        {
            item.ErrorMessage = FriendlyError(ex);
            MoveTo(item, DownloadStatus.Failed);
        }
        finally
        {
            item.IsIndeterminate = false;
            item.Cts = null;
            cts.Dispose();
            Pump();
        }
    }

    private void MoveTo(DownloadItem item, DownloadStatus status)
    {
        var target = status switch
        {
            DownloadStatus.Downloading or DownloadStatus.Processing => Active,
            DownloadStatus.Done or DownloadStatus.Failed => Finished,
            _ => Waiting,
        };
        item.Status = status;
        if (status == DownloadStatus.Waiting) item.StatusText = "Waiting";
        foreach (var group in new[] { Active, Waiting, Finished })
            if (group != target) group.Remove(item);
        if (!target.Contains(item))
        {
            if (target == Finished) target.Insert(0, item);
            else target.Add(item);
        }
    }

    // ---- Row actions ----

    public void Pause(DownloadItem item)
    {
        item.PauseRequested = true;
        item.Cts?.Cancel();
    }

    public void Resume(DownloadItem item)
    {
        MoveTo(item, DownloadStatus.Waiting);
        Pump();
    }

    public void Retry(DownloadItem item)
    {
        item.ErrorMessage = null;
        if (item.QualityOptions.Count == 0)
        {
            // The lookup itself failed; try it again.
            item.Status = DownloadStatus.LookingUp;
            item.StatusText = "Looking up…";
            Finished.Remove(item);
            Waiting.Add(item);
            _ = LookUpAsync(item, QualityOption.FromKey(_settings.DefaultQuality));
            return;
        }
        MoveTo(item, DownloadStatus.Waiting);
        Pump();
    }

    public void Remove(DownloadItem item)
    {
        if (item.Cts is { } cts)
        {
            item.PauseRequested = false;
            cts.Cancel(); // RunAsync removes it once yt-dlp has exited.
            return;
        }
        Active.Remove(item);
        Waiting.Remove(item);
        Finished.Remove(item);
        if (item.Status != DownloadStatus.Done) DeletePartialFiles(item);
    }

    // Removes yt-dlp's leftovers for an abandoned download: "<stream>.part", "<stream>.ytdl",
    // "<stream>.part-Frag*" and finished-but-unmerged stream files. Never the final merged file.
    private static void DeletePartialFiles(DownloadItem item)
    {
        foreach (var stream in item.StreamPaths)
        {
            var folder = Path.GetDirectoryName(stream);
            if (folder is null || !Directory.Exists(folder)) continue;
            var name = Path.GetFileName(stream);
            var candidates = new[] { stream, stream + ".part", stream + ".ytdl" }
                .Concat(Directory.EnumerateFiles(folder, name + ".part-Frag*"));
            foreach (var file in candidates.Where(File.Exists).Where(f => f != item.FilePath))
            {
                try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        item.StreamPaths.Clear();
    }

    private bool CanPauseAll() => HasActive;

    [RelayCommand(CanExecute = nameof(CanPauseAll))]
    private void PauseAll()
    {
        foreach (var item in Active.ToList()) Pause(item);
    }

    private bool CanClearFinished() => HasFinished;

    [RelayCommand(CanExecute = nameof(CanClearFinished))]
    private void ClearFinished() => Finished.Clear();

    private static string FriendlyError(Exception ex) => ex switch
    {
        YtDlpException y when y.Message.Contains("Unsupported URL", StringComparison.OrdinalIgnoreCase) => "This link isn't a video or playlist that can be downloaded.",
        YtDlpException y when y.Message.Contains("unavailable", StringComparison.OrdinalIgnoreCase) => "This video is unavailable.",
        YtDlpException y when y.Message.Contains("Private video", StringComparison.OrdinalIgnoreCase) => "This video is private.",
        YtDlpException y when y.Message.Contains("Sign in", StringComparison.OrdinalIgnoreCase) => "YouTube wants a signed-in account for this video.",
        YtDlpException y => y.Message,
        System.ComponentModel.Win32Exception => "The download engine is missing. Restart the app to set it up again.",
        _ => ex.Message,
    };
}
