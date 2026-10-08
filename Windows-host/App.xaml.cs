using System;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Windows;

namespace WindowsHost;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 2 && e.Args[0] == "--configure-lan-port")
        {
            Shutdown(int.TryParse(e.Args[1], out var port) ? LanPortPermission.Configure(port) : 2);
            return;
        }
        base.OnStartup(e);

        // Global Exception Handlers
        this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        Logger.Log("APP", "Windows Host Started");

        ThemeManager.Initialize();
        Localization.Initialize();
        new MainWindow().Show();
    }

    private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogException(e.Exception, "Dispatcher");
        e.Handled = true; // Prevent app from crashing if possible, though some states might be corrupt
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Logger.LogException(ex, "AppDomain");
        }
    }

    private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.LogException(e.Exception, "TaskScheduler");
        e.SetObserved();
    }
}

