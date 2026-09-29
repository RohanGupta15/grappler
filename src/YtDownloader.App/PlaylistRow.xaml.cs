using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YtDownloader.App.ViewModels;

namespace YtDownloader.App;

/// <summary>A looked-up playlist in the list; its buttons move under the title in compact windows.</summary>
public sealed partial class PlaylistRow : UserControl
{
    public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(nameof(Item), typeof(PlaylistItem), typeof(PlaylistRow),
            new PropertyMetadata(null, (d, _) => ((PlaylistRow)d).Bindings.Update()));

    public PlaylistRow() => InitializeComponent();

    public PlaylistItem? Item
    {
        get => (PlaylistItem?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }
}
