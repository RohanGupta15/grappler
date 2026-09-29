using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace YtDownloader.App;

/// <summary>Helpers called from x:Bind function bindings.</summary>
public static class Converters
{
    public static ImageSource? Thumb(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? new BitmapImage(uri) { DecodePixelWidth = 224 } : null;

    public static Visibility VisibleIf(string? text) => string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleIfNot(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static bool Not(bool value) => !value;
}
