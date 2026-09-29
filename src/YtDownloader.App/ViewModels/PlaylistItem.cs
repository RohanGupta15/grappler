using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDownloader.Core.YtDlp;

namespace YtDownloader.App.ViewModels;

public sealed partial class PlaylistEntryItem : ObservableObject
{
    public PlaylistEntryItem(PlaylistEntry entry) => Entry = entry;

    public PlaylistEntry Entry { get; }
    public string Title => Entry.Title;
    public string? ThumbnailUrl => Entry.ThumbnailUrl;
    public string DurationText => Entry.Duration is { } d ? Format.Duration(d) : "";

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;
}

/// <summary>A looked-up playlist waiting for the user to pick which videos to download.</summary>
public sealed partial class PlaylistItem : ObservableObject
{
    private readonly DownloadQueue _queue;

    public PlaylistItem(DownloadQueue queue, PlaylistInfo info, QualityOption quality)
    {
        _queue = queue;
        Info = info;
        SelectedQuality = quality;
        Entries = info.Entries.Select(e => new PlaylistEntryItem(e)).ToList();
        foreach (var e in Entries)
            e.PropertyChanged += (_, _) => OnPropertyChanged(nameof(SelectedCount));
    }

    public PlaylistInfo Info { get; }
    public IReadOnlyList<PlaylistEntryItem> Entries { get; }
    public string Title => $"Playlist: {Info.Title}";
    public int SelectedCount => Entries.Count(e => e.IsSelected);

    public IReadOnlyList<QualityOption> QualityOptions => QualityOption.Standard;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial QualityOption SelectedQuality { get; set; }

    public string Summary => $"{Entries.Count} videos · {SelectedCount} selected · {SelectedQuality.Label}";
    public string StartLabel => $"Start {SelectedCount}";

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(SelectedCount))
        {
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(Summary)));
            base.OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs(nameof(StartLabel)));
            StartCommand.NotifyCanExecuteChanged();
        }
    }

    public void SelectAll(bool selected)
    {
        foreach (var e in Entries) e.IsSelected = selected;
    }

    /// <summary>Raised when the user wants the video picker; the page shows the dialog.</summary>
    public event EventHandler? ChooseRequested;

    [RelayCommand] private void Choose() => ChooseRequested?.Invoke(this, EventArgs.Empty);

    private bool CanStart() => SelectedCount > 0;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start() => _queue.StartPlaylist(this);

    [RelayCommand] private void Remove() => _queue.RemovePlaylist(this);
}
