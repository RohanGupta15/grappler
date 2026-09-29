using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using YtDownloader.App.ViewModels;
using YtDownloader.Core.Engine;

namespace YtDownloader.App;

public partial class App : Application
{
    private Window? _window;

    public static new App Current => (App)Application.Current;

    public MainViewModel ViewModel { get; } =
        new(new EngineManager(EngineManager.DefaultRoot, new HttpClient()), DownloadsFolder());

    public static string DataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtDownloader");

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => LogCrash(e.Exception);
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

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
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
