using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace TelemetryLab.WinUI;

internal static class Program
{
    private static App? s_app;

    [STAThread]
    private static void Main(string[] args)
    {
        App.LogInfo("Program Main start");
        App.InitialPath = args.Length == 0 ? null : string.Join(' ', args);
        ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(dispatcherQueue));
            s_app = new App();
        });
        GC.KeepAlive(s_app);
    }
}
