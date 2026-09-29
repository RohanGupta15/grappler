using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.Storage.Pickers;
using YtDownloader.App.ViewModels;

namespace YtDownloader.App;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new(App.Current.Settings, App.Current.Engine, App.Current.Queue);

    public SettingsPage() => InitializeComponent();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadVersionsAsync();
    }

    private async void ChangeFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker(XamlRoot.ContentIslandEnvironment.AppWindowId);
        var result = await picker.PickSingleFolderAsync();
        if (result is not null) ViewModel.Settings.OutputFolder = result.Path;
    }
}
