using System.IO;
using System.Windows;
using System.Windows.Threading;
using HyperPOS.Desktop.Data;

namespace HyperPOS.Desktop;

public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_lifecycle.log");

    public static void Log(string msg)
    {
        try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n"); } catch {}
    }

    public App()
    {
        try
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        }
        catch { }
        Log("App constructed");
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("App OnStartup called");
        try
        {
            var db = new PosDatabase();
            SaraaSqlImporter.ImportSaraaProducts(db);
            Log("App OnStartup database initialized and Saraa Boutique data seeded successfully");
        }
        catch (Exception ex)
        {
            Log($"App OnStartup database initialization error: {ex}");
        }
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log($"App OnExit called with Application ExitCode: {e.ApplicationExitCode}");
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.Exception.ToString());
        }
        catch { }
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_domain.log"), e.ExceptionObject?.ToString());
        }
        catch { }
    }
}
