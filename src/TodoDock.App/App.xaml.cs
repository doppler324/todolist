using System.Windows;
using TodoDock.Services;
using TodoDock.Views;

namespace TodoDock;

public partial class App : Application
{
    private Mutex? _mutex;
    private TrayService? _tray;
    private HandleWindow? _handle;
    private DrawerWindow? _drawer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, "TodoDock.SingleInstance", out var created);
        if (!created)
        {
            // Уже запущено: просим первый экземпляр открыть панель.
            SingleInstance.SignalExisting();
            Shutdown();
            return;
        }

        _drawer = new DrawerWindow();
        _handle = new HandleWindow(_drawer);
        _handle.Show();

        _tray = new TrayService(_drawer.Toggle, Shutdown);
        SingleInstance.Listen(() => Dispatcher.Invoke(_drawer.Open));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
