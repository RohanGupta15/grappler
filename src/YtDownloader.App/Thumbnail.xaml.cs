using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace YtDownloader.App;

/// <summary>16:9 video thumbnail with a duration badge; shows a video glyph until the image loads.</summary>
public sealed partial class Thumbnail : UserControl
{
    public static readonly DependencyProperty UrlProperty =
        DependencyProperty.Register(nameof(Url), typeof(string), typeof(Thumbnail), new PropertyMetadata(null));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(string), typeof(Thumbnail), new PropertyMetadata(""));

    public Thumbnail() => InitializeComponent();

    public string? Url
    {
        get => (string?)GetValue(UrlProperty);
        set => SetValue(UrlProperty, value);
    }

    public string Duration
    {
        get => (string)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }
}
