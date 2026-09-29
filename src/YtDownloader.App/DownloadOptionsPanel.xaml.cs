using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YtDownloader.App.ViewModels;

namespace YtDownloader.App;

/// <summary>A waiting download's options; reflows for compact windows.</summary>
public sealed partial class DownloadOptionsPanel : UserControl
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(nameof(Item), typeof(DownloadItem), typeof(DownloadOptionsPanel),
            new PropertyMetadata(null, (d, _) => ((DownloadOptionsPanel)d).Bindings.Update()));

    public DownloadOptionsPanel() => InitializeComponent();

    public DownloadItem? Item
    {
        get => (DownloadItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    private void TrimButton_Click(object sender, RoutedEventArgs e)
    {
        CompactTrim.Visibility = Visibility.Visible;
        TrimButton.Visibility = Visibility.Collapsed;
    }
}
