using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Velopack;
using Velopack.Sources;

namespace YtDownloader.App.Services;

/// <summary>
/// Checks GitHub Releases for a newer version and downloads it in the background. Only active when the
/// app was installed with Setup.exe; dev builds and the MSIX never update themselves. A downloaded update
/// is applied on the next launch, or straight away through <see cref="RestartCommand"/>.
/// </summary>
public sealed partial class AppUpdater : ObservableObject
{
    public const string RepositoryUrl = "https://github.com/RohanGupta15/grappler";

    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));
    private VelopackAsset? _downloaded;

    /// <summary>The installed version, or the build's own version when not installed with Setup.exe.</summary>
    public string CurrentVersion =>
        _manager.CurrentVersion?.ToString()
        ?? typeof(AppUpdater).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateReady), nameof(UpdateMessage))]
    public partial string? ReadyVersion { get; set; }

    public bool IsUpdateReady => ReadyVersion is not null;
    public string UpdateMessage => $"Version {ReadyVersion} is downloaded. Restart to start using it, or it installs next time you open the app.";

    public async Task CheckAsync()
    {
        if (!_manager.IsInstalled) return;
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) return;
            await _manager.DownloadUpdatesAsync(update);
            _downloaded = update.TargetFullRelease;
            ReadyVersion = _downloaded.Version.ToString();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            // Offline or GitHub unreachable: try again next launch.
        }
    }

    [RelayCommand]
    private void Restart()
    {
        if (_downloaded is not null) _manager.ApplyUpdatesAndRestart(_downloaded);
    }
}
