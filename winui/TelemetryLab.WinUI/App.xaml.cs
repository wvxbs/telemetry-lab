using Microsoft.UI.Xaml;

namespace TelemetryLab.WinUI;

public partial class App : Application
{
    private static App? s_current;
    private static Window? s_mainWindow;
    private Window? _window;
    internal static string? InitialPath { get; set; }

    public App()
    {
        s_current = this;
        LogInfo("App constructor start");
        UnhandledException += App_UnhandledException;
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            LogInfo("MainWindow constructor start");
            _window = new MainWindow();
            s_mainWindow = _window;
            _window.Activate();
            LogInfo("Window activated");
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
    }

    private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
    }

    internal static void KeepAlive()
    {
        GC.KeepAlive(s_current);
        GC.KeepAlive(s_mainWindow);
    }

    internal static void LogInfo(string message) => WriteLog("winui-startup.log", message);

    internal static void LogCrash(Exception ex) => WriteLog("winui-crash.log", ex.ToString());

    private static void WriteLog(string fileName, string text)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TelemetryLab");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, fileName), $"{DateTimeOffset.Now:O} {text}\n");
        }
        catch
        {
        }
    }
}
