using System.Windows;
using TodoDock.Core;
using TodoDock.Services;
using TodoDock.ViewModels;
using TodoDock.Views;

namespace TodoDock;

public partial class App : Application
{
    private Mutex? _mutex;
    private TodoRepository? _repo;
    private TrayService? _tray;
    private HandleWindow? _handle;
    private DrawerWindow? _drawer;
    private NotificationService? _notifications;
    private TelegramService? _telegram;
    private NotificationStackWindow? _stack;

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

        var dbPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TodoDock", "data.db");
        _repo = TodoRepository.OpenFile(dbPath);
        var vm = new MainViewModel(_repo);
        _telegram = new TelegramService(_repo);
        _notifications = new NotificationService(_repo, vm.Reload, _telegram);
        _drawer = new DrawerWindow(vm, _telegram, _notifications);
        _handle = new HandleWindow(_drawer);
        _handle.Show();

        _stack = new NotificationStackWindow(_notifications);
        _drawer.OpenChanged += _stack.SetDrawerOpen;
        _drawer.KeepOpenWindows = () => new System.Windows.Window?[] { _handle, _stack }.Where(w => w != null)!;
        _notifications.Active.CollectionChanged += (_, _) => _handle.SetBadge(_notifications.Active.Count);
        vm.Changed += _notifications.Refresh;
        _notifications.Refresh();   // сразу показать пропущенные и сохранённые с прошлого запуска

        _tray = new TrayService(_drawer.Toggle, () => { _drawer.Open(); _drawer.OpenSettings(); }, Shutdown);
        SingleInstance.Listen(() => Dispatcher.Invoke(_drawer.Open));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _notifications?.Dispose();
        _telegram?.Dispose();
        _repo?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
