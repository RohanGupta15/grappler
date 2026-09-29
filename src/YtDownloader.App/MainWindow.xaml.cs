using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;

namespace YtDownloader.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");
        // AppWindow sizes are physical pixels; scale the 1200 x 800 design size by the monitor's DPI.
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        AppWindow.Resize(new Windows.Graphics.SizeInt32((int)(1200 * scale), (int)(800 * scale)));

        Activated += Window_Activated;
        RootFrame.Navigate(typeof(MainPage));
    }

    public void OpenSettings() =>
        RootFrame.Navigate(typeof(SettingsPage), null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });

    private void RootFrame_Navigated(object sender, NavigationEventArgs e) =>
        AppTitleBar.IsBackButtonVisible = RootFrame.CanGoBack;

    private void AppTitleBar_BackRequested(TitleBar sender, object args)
    {
        if (RootFrame.CanGoBack) RootFrame.GoBack();
    }

    // Offer a copied link when the user switches back to the app.
    private async void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated) return;
        try
        {
            var content = Clipboard.GetContent();
            if (content.Contains(StandardDataFormats.Text))
                App.Current.ViewModel.OfferClipboard(await content.GetTextAsync());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            // Another app can hold the clipboard open; just skip this time.
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
