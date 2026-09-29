using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.Windows.Storage.Pickers;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using YtDownloader.App.ViewModels;
using YtDownloader.Core.YtDlp;

namespace YtDownloader.App;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; } = App.Current.ViewModel;

    public MainPage()
    {
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.EnsureEngineCommand.ExecuteAsync(null);
    }

    // ---- x:Bind helpers ----

    public static bool HasText(string? s) => !string.IsNullOrEmpty(s);

    public static Visibility VisibleIfText(string? s) => HasText(s) ? Visibility.Visible : Visibility.Collapsed;

    public static ImageSource? Thumbnail(VideoInfo? v) =>
        v?.ThumbnailUrl is { } url ? new BitmapImage(new Uri(url)) : null;

    public static string Byline(VideoInfo? v)
    {
        if (v is null) return "";
        var duration = v.Duration is { } d ? Format.Duration(d) : null;
        return string.Join(" · ", new[] { v.Channel, duration }.Where(x => !string.IsNullOrEmpty(x)));
    }

    // ---- Event handlers ----

    private async void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.FetchInfoCommand.CanExecute(null))
        {
            e.Handled = true;
            await ViewModel.FetchInfoCommand.ExecuteAsync(null);
        }
    }

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        var content = Clipboard.GetContent();
        if (!content.Contains(StandardDataFormats.Text)) return;
        ViewModel.Url = (await content.GetTextAsync()).Trim();
        if (ViewModel.FetchInfoCommand.CanExecute(null))
            await ViewModel.FetchInfoCommand.ExecuteAsync(null);
    }

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
        var result = await picker.PickSingleFolderAsync();
        if (result is not null) ViewModel.OutputFolder = result.Path;
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CompletedFile is { } path && File.Exists(path))
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CompletedFile is { } path)
            Process.Start("explorer.exe", $"/select,\"{path}\"");
    }
}
