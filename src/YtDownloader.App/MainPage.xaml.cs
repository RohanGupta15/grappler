using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using YtDownloader.App.ViewModels;

namespace YtDownloader.App;

public sealed partial class MainPage : Page
{
    private bool _dialogOpen;

    public MainViewModel ViewModel { get; } = App.Current.ViewModel;

    public MainPage()
    {
        InitializeComponent();
        BuildQualityMenu();
        ViewModel.Queue.PlaylistAdded += async (_, p) =>
        {
            p.ChooseRequested += async (_, _) => await ShowPlaylistDialogAsync(p);
            await ShowPlaylistDialogAsync(p);
        };
        Loaded += async (_, _) =>
        {
            if (!ViewModel.IsEngineReady && !ViewModel.IsInstallingEngine)
                await ViewModel.EnsureEngineCommand.ExecuteAsync(null);
        };
    }

    public static string EmptyTitle(bool ready) => ready ? "Paste a link to get started" : "Your downloads will show up here";

    public static string EmptyBody(bool ready) => ready
        ? "Copy a video or playlist link from your browser, then press Ctrl+V anywhere in this window. It downloads in the quality picked next to Add."
        : "You can paste a link as soon as setup finishes.";

    // MenuFlyout items can't be data-bound, so the quality menu is built here.
    private void BuildQualityMenu()
    {
        foreach (var option in ViewModel.QualityChoices)
        {
            var item = new RadioMenuFlyoutItem { Text = option.Label, GroupName = "quality", IsChecked = option == ViewModel.DefaultQuality };
            item.Click += (_, _) => ViewModel.DefaultQuality = option;
            QualityMenu.Items.Add(item);
        }
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.DefaultQuality)) return;
            foreach (var item in QualityMenu.Items.OfType<RadioMenuFlyoutItem>())
                item.IsChecked = item.Text == ViewModel.DefaultQuality.Label;
        };
    }

    private async Task AddIfValidAsync()
    {
        if (ViewModel.AddCommand.CanExecute(null))
            await ViewModel.AddCommand.ExecuteAsync(null);
    }

    private async void UrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        await AddIfValidAsync();
    }

    // Ctrl+V anywhere outside a text field pastes the link and adds it straight away.
    private async void PasteAccelerator_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox) return;
        args.Handled = true;
        await PasteAndAddAsync();
    }

    private async void Paste_Click(object sender, RoutedEventArgs e) => await PasteAndAddAsync();

    private async Task PasteAndAddAsync()
    {
        try
        {
            var content = Clipboard.GetContent();
            if (!content.Contains(StandardDataFormats.Text)) return;
            ViewModel.Url = (await content.GetTextAsync()).Trim();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            return;
        }
        await AddIfValidAsync();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => App.Current.Window.OpenSettings();

    private async Task ShowPlaylistDialogAsync(PlaylistItem playlist)
    {
        // Only one ContentDialog can be open at a time.
        if (_dialogOpen || !ViewModel.Queue.Playlists.Contains(playlist)) return;
        _dialogOpen = true;
        try
        {
            var dialog = new PlaylistDialog(playlist) { XamlRoot = XamlRoot };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                playlist.StartCommand.Execute(null);
        }
        finally
        {
            _dialogOpen = false;
        }
    }
}
