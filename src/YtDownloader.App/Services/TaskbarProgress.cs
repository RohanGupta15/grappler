using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using YtDownloader.App.ViewModels;
using YtDownloader.Core.Progress;

namespace YtDownloader.App.Services;

/// <summary>
/// Mirrors the queue's overall progress on the window's taskbar button: green while downloading,
/// a moving bar before sizes are known, yellow when everything left is paused, nothing when idle.
/// Samples the queue twice a second rather than listening to every progress line.
/// </summary>
public sealed class TaskbarProgress
{
    private readonly IntPtr _hwnd;
    private readonly DownloadQueue _queue;
    private readonly ITaskbarList3? _taskbar;
    private readonly DispatcherQueueTimer _timer;
    private OverallProgress _shown = new(TaskbarState.None, 0);

    public TaskbarProgress(IntPtr hwnd, DownloadQueue queue)
    {
        _hwnd = hwnd;
        _queue = queue;
        try
        {
            _taskbar = (ITaskbarList3)new TaskbarListCom();
            _taskbar.HrInit();
        }
        catch (COMException)
        {
            _taskbar = null; // No taskbar (for example Explorer isn't running); nothing to show.
        }

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(500);
        _timer.Tick += (_, _) => Update();
        _timer.Start();
    }

    private void Update()
    {
        if (_taskbar is null) return;
        var now = OverallProgress.From(_queue.Active.Concat(_queue.Waiting).Select(ToProgress).OfType<ItemProgress>());
        if (now.State == _shown.State && Math.Abs(now.Fraction - _shown.Fraction) < 0.002) return;
        _shown = now;

        _taskbar.SetProgressState(_hwnd, now.State switch
        {
            TaskbarState.Indeterminate => Tbpf.Indeterminate,
            TaskbarState.Normal => Tbpf.Normal,
            TaskbarState.Paused => Tbpf.Paused,
            _ => Tbpf.NoProgress,
        });
        if (now.State is TaskbarState.Normal or TaskbarState.Paused)
            _taskbar.SetProgressValue(_hwnd, (ulong)Math.Round(now.Fraction * 1000), 1000);
    }

    private static ItemProgress? ToProgress(DownloadItem item) => item.Status switch
    {
        DownloadStatus.Downloading => new ItemProgress(ItemPhase.Running, item.IsIndeterminate ? null : item.Progress / 100),
        DownloadStatus.Processing => new ItemProgress(ItemPhase.Running, 1),
        DownloadStatus.Waiting => new ItemProgress(ItemPhase.Queued, null),
        DownloadStatus.Paused => new ItemProgress(ItemPhase.Paused, item.Progress / 100),
        _ => null, // Looking up, ready to start, done or failed: not part of the running batch.
    };

    private enum Tbpf { NoProgress = 0, Indeterminate = 0x1, Normal = 0x2, Error = 0x4, Paused = 0x8 }

    // ITaskbarList3 from shobjidl_core.h; methods listed in vtable order, including the inherited ones.
    [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITaskbarList3
    {
        void HrInit();
        void AddTab(IntPtr hwnd);
        void DeleteTab(IntPtr hwnd);
        void ActivateTab(IntPtr hwnd);
        void SetActiveAlt(IntPtr hwnd);
        void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fullscreen);
        void SetProgressValue(IntPtr hwnd, ulong completed, ulong total);
        void SetProgressState(IntPtr hwnd, Tbpf flags);
    }

    [ComImport, Guid("56FDF344-FD6D-11d0-958A-006097C9A090"), ClassInterface(ClassInterfaceType.None)]
    private class TaskbarListCom;
}
