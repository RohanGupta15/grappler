using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using YtDownloader.App.Services;
using YtDownloader.Core.Engine;

namespace YtDownloader.App.ViewModels;

public sealed partial class EngineComponentItem : ObservableObject
{
    public EngineComponentItem(string name, bool installed)
    {
        Name = name;
        IsDone = installed;
        Progress = installed ? 100 : 0;
        Detail = installed ? "Ready" : "Waiting";
    }

    public string Name { get; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string Detail { get; set; }

    [ObservableProperty]
    public partial bool IsDone { get; set; }
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly EngineManager _engine;
    private string? _dismissedClipboardUrl;

    public MainViewModel(EngineManager engine, AppSettings settings, DownloadQueue queue)
    {
        _engine = engine;
        Settings = settings;
        Queue = queue;
        DefaultQuality = QualityOption.FromKey(settings.DefaultQuality);
        EngineComponents = EngineManager.ComponentNames.Select(n => new EngineComponentItem(n, engine.IsComponentInstalled(n))).ToList();
        queue.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(DownloadQueue.IsEmpty)) OnPropertyChanged(nameof(ShowEmptyState)); };
        settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.DefaultQuality) && DefaultQuality.Key != settings.DefaultQuality)
                DefaultQuality = QualityOption.FromKey(settings.DefaultQuality);
        };
    }

    public AppSettings Settings { get; }
    public DownloadQueue Queue { get; }

    // ---- Engine setup ----

    public IReadOnlyList<EngineComponentItem> EngineComponents { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial bool IsEngineReady { get; set; }

    [ObservableProperty]
    public partial bool IsInstallingEngine { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEngineError))]
    public partial string? EngineError { get; set; }

    public bool HasEngineError => EngineError is not null;

    public bool ShowEmptyState => Queue.IsEmpty;

    [RelayCommand]
    private async Task EnsureEngineAsync()
    {
        EngineError = null;
        if (_engine.IsInstalled)
        {
            IsEngineReady = true;
            return;
        }

        IsInstallingEngine = true;
        try
        {
            await _engine.InstallAsync(new Progress<EngineInstallProgress>(p =>
            {
                var c = EngineComponents.First(x => x.Name == p.Component);
                c.Progress = (p.Fraction ?? 0) * 100;
                c.IsDone = p.Finished;
                c.Detail = p.Finished
                    ? $"Done · {Format.Bytes(p.DownloadedBytes)}"
                    : p.TotalBytes is { } total ? $"{Format.Bytes(p.DownloadedBytes)} of {Format.Bytes(total)}" : Format.Bytes(p.DownloadedBytes);
            }), CancellationToken.None);
            IsEngineReady = true;
        }
        catch (Exception ex)
        {
            EngineError = $"Couldn't download the tools YT Downloader needs. Check your internet connection and try again. ({ex.Message})";
        }
        finally
        {
            IsInstallingEngine = false;
        }
    }

    // ---- Add bar ----

    public IReadOnlyList<QualityOption> QualityChoices => QualityOption.Standard;

    [ObservableProperty]
    public partial QualityOption DefaultQuality { get; set; }

    partial void OnDefaultQualityChanged(QualityOption value) => Settings.DefaultQuality = value.Key;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string Url { get; set; } = "";

    private static bool IsWebLink(string text) =>
        Uri.TryCreate(text.Trim(), UriKind.Absolute, out var u) && u.Scheme is "http" or "https";

    private bool CanAdd() => IsEngineReady && IsWebLink(Url);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private async Task AddAsync()
    {
        var url = Url.Trim();
        Url = "";
        if (ClipboardUrl == url) ClipboardUrl = null;
        await Queue.AddAsync(url, DefaultQuality);
    }

    // ---- Clipboard offer ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClipboardUrl))]
    public partial string? ClipboardUrl { get; set; }

    public bool HasClipboardUrl => ClipboardUrl is not null;

    /// <summary>Called when the window is activated with the clipboard's text.</summary>
    public void OfferClipboard(string? text)
    {
        if (!Settings.OfferClipboard || !IsEngineReady || text is null) return;
        text = text.Trim();
        if (!IsWebLink(text) || !IsVideoSite(text) || text == _dismissedClipboardUrl || text == Url || Queue.Contains(text)) return;
        ClipboardUrl = text;
    }

    private static bool IsVideoSite(string url)
    {
        var host = new Uri(url).Host.ToLowerInvariant();
        return host is "youtu.be" || host == "youtube.com" || host.EndsWith(".youtube.com");
    }

    [RelayCommand]
    private async Task AddClipboardAsync()
    {
        if (ClipboardUrl is not { } url) return;
        ClipboardUrl = null;
        await Queue.AddAsync(url, DefaultQuality);
    }

    [RelayCommand]
    private void DismissClipboard()
    {
        _dismissedClipboardUrl = ClipboardUrl;
        ClipboardUrl = null;
    }
}
