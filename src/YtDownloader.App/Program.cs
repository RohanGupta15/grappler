using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Velopack;

namespace YtDownloader.App;

// Replaces the XAML-generated Main (DISABLE_XAML_GENERATED_MAIN) so Velopack can handle its
// install, update and uninstall hooks before any window opens. Outside a Velopack install it does nothing.
public static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App();
        });
    }
}
