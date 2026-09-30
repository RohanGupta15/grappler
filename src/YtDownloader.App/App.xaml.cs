using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using YtDownloader.App.Services;
using YtDownloader.App.ViewModels;
using YtDownloader.Core.Engine;

namespace YtDownloader.App;

public partial class App : Application
{
    private Window? _window;

    public static new App Current => (App)Application.Current;

    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtDownloader");

    public EngineManager Engine { get; }
    public AppSettings Settings { get; }
    public DownloadQueue Queue { get; }
    public MainViewModel ViewModel { get; }
    public AppUpdater Updater { get; } = new();
    public DownloadNotifier Notifier { get; private set; } = null!;
    public TaskbarProgress Taskbar { get; private set; } = null!;

    public MainWindow Window => (MainWindow)_window!;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => LogCrash(e.Exception);

        Engine = new EngineManager(EngineManager.DefaultRoot, new HttpClient());
        Settings = AppSettings.Load(Path.Combine(DataFolder, "settings.json"), DownloadsFolder());
        Queue = new DownloadQueue(Engine, Settings);
        ViewModel = new MainViewModel(Engine, Settings, Queue);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Register for notifications before the window opens, as the Windows App SDK asks.
        Notifier = new DownloadNotifier(() => Window.BringToFront());
        _window = new MainWindow();
        _window.Activate();
        _ = Updater.CheckAsync();

        Taskbar = new TaskbarProgress(WinRT.Interop.WindowNative.GetWindowHandle(_window), Queue);
        Queue.BatchFinished += (_, batch) =>
        {
            if (Settings.NotifyWhenFinished && !Window.IsActive) Notifier.Show(batch);
        };
        _window.Closed += (_, _) => Notifier.Unregister();
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.AppendAllText(Path.Combine(DataFolder, "crash.log"), $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch (IOException) { }
    }

    // The user may have moved Downloads, so ask the shell rather than assuming %USERPROFILE%\Downloads.
    private static string DownloadsFolder()
    {
        var downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");
        if (SHGetKnownFolderPath(downloads, 0, IntPtr.Zero, out var ptr) == 0)
        {
            try { return Marshal.PtrToStringUni(ptr)!; }
            finally { Marshal.FreeCoTaskMem(ptr); }
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint flags, IntPtr token, out IntPtr path);
}
