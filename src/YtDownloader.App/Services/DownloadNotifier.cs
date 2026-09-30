using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using YtDownloader.App.ViewModels;

namespace YtDownloader.App.Services;

/// <summary>
/// Shows one Windows notification when a batch of downloads finishes. A single finished video gets
/// Open and Show in folder buttons; clicking the notification itself brings the window back.
/// </summary>
public sealed class DownloadNotifier
{
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private readonly Action _bringToFront;
    private readonly bool _registered;

    public DownloadNotifier(Action bringToFront)
    {
        _bringToFront = bringToFront;
        try
        {
            if (!AppNotificationManager.IsSupported()) return;
            var manager = AppNotificationManager.Default;
            manager.NotificationInvoked += (_, e) => _dispatcher.TryEnqueue(() => OnInvoked(e.Arguments));
            if (IsPackaged())
                manager.Register();
            else
                manager.Register("YT Downloader", new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Square44x44Logo.targetsize-256.png")));
            _registered = true;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            // Notifications are a nicety; the app works without them.
        }
    }

    public void Show(DownloadBatch batch)
    {
        if (!_registered || batch.Done.Count + batch.Failed.Count == 0) return;

        var builder = new AppNotificationBuilder().AddArgument("action", "show");
        if (batch.Done.Count == 1 && batch.Failed.Count == 0 && batch.Done[0].FilePath is { } path)
        {
            builder.AddText("Download finished")
                .AddText(batch.Done[0].Title)
                .AddButton(new AppNotificationButton("Open").AddArgument("action", "open").AddArgument("path", path))
                .AddButton(new AppNotificationButton("Show in folder").AddArgument("action", "folder").AddArgument("path", path));
        }
        else if (batch.Done.Count == 0)
        {
            builder.AddText(batch.Failed.Count == 1 ? "Download failed" : $"{batch.Failed.Count} downloads failed")
                .AddText(batch.Failed.Count == 1 ? batch.Failed[0].Title : "Open YT Downloader to see what went wrong and retry.");
        }
        else
        {
            var title = batch.Done.Count == 1 ? "1 download finished" : $"{batch.Done.Count} downloads finished";
            if (batch.Failed.Count > 0) title += $", {batch.Failed.Count} failed";
            builder.AddText(title).AddText(string.Join(", ", batch.Done.Take(3).Select(i => i.Title)) + (batch.Done.Count > 3 ? "…" : ""));
        }

        try { AppNotificationManager.Default.Show(builder.BuildNotification()); }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    /// <summary>Call when the app exits, as unpackaged apps must.</summary>
    public void Unregister()
    {
        if (!_registered) return;
        try { AppNotificationManager.Default.Unregister(); }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    private void OnInvoked(IDictionary<string, string> args)
    {
        args.TryGetValue("path", out var path);
        args.TryGetValue("action", out var action);
        switch (action)
        {
            case "open" when File.Exists(path):
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                break;
            case "folder" when File.Exists(path):
                Process.Start("explorer.exe", $"/select,\"{path}\"");
                break;
            default:
                _bringToFront();
                break;
        }
    }

    private static bool IsPackaged()
    {
        try { return Windows.ApplicationModel.Package.Current is not null; }
        catch (InvalidOperationException) { return false; }
    }
}
