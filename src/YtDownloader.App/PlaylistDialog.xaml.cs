using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YtDownloader.App.ViewModels;

namespace YtDownloader.App;

/// <summary>Lets the user pick which videos of a playlist to download, and in what quality.</summary>
public sealed partial class PlaylistDialog : ContentDialog
{
    public PlaylistDialog(PlaylistItem playlist)
    {
        Playlist = playlist;
        InitializeComponent();
        // ContentDialog adds 24 px padding each side plus a margin to the window edge.
        // Follow the window while open too, since the user can resize it.
        void Fit() => Body.Width = Math.Min(590, XamlRoot.Size.Width - 96);
        void OnRootChanged(Microsoft.UI.Xaml.XamlRoot root, Microsoft.UI.Xaml.XamlRootChangedEventArgs args) => Fit();
        Opened += (_, _) => { Fit(); XamlRoot.Changed += OnRootChanged; };
        Closed += (_, _) => XamlRoot.Changed -= OnRootChanged;
        UpdateSelectAll();
        playlist.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(PlaylistItem.SelectedCount)) UpdateSelectAll(); };
    }

    public PlaylistItem Playlist { get; }

    public string Byline =>
        string.Join(" · ", new[] { "Playlist", $"{Playlist.Info.TotalCount} videos", Playlist.Info.Channel }.Where(s => !string.IsNullOrEmpty(s)));

    // yt-dlp can return fewer entries than the playlist count (private or deleted videos).
    public string MoreNote => Playlist.Info.TotalCount > Playlist.Entries.Count
        ? $"{Playlist.Info.TotalCount - Playlist.Entries.Count} videos in this playlist are private or unavailable."
        : "";

    public static bool HasAny(int count) => count > 0;
    public static string AddLabel(int count) => count == 1 ? "Add 1 to queue" : $"Add {count} to queue";
    public static string CountLabel(int selected, int total) => $"{selected} of {total} selected";

    private void UpdateSelectAll()
    {
        var count = Playlist.SelectedCount;
        SelectAllBox.IsChecked = count == Playlist.Entries.Count ? true : count == 0 ? false : null;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) =>
        Playlist.SelectAll(Playlist.SelectedCount != Playlist.Entries.Count);
}
