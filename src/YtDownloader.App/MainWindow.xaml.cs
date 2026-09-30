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
        SizeToWorkArea();

        Activated += Window_Activated;
        RootFrame.Navigate(typeof(MainPage));
    }

    // Open at 840 x 640 (scaled by the monitor's DPI; AppWindow sizes are physical pixels),
    // but never more than 85 percent of the work area, and centred on it.
    private void SizeToWorkArea()
    {
        var scale = GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96.0;
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest).WorkArea;
        var width = Math.Min((int)(840 * scale), (int)(area.Width * 0.85));
        var height = Math.Min((int)(640 * scale), (int)(area.Height * 0.85));
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(
            area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
    }

    /// <summary>Whether this window is the foreground window; notifications only show when it isn't.</summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Restores the window if minimised and brings it to the front.</summary>
    public void BringToFront()
    {
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter { State: Microsoft.UI.Windowing.OverlappedPresenterState.Minimized } presenter)
            presenter.Restore();
        Activate();
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
        IsActive = args.WindowActivationState != WindowActivationState.Deactivated;
        if (!IsActive) return;
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
