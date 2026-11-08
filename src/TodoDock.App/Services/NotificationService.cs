using System.Collections.ObjectModel;
using System.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TodoDock.Core;

namespace TodoDock.Services;

/// <summary>
/// Планировщик и список активных уведомлений. Уведомление остаётся в списке (и в БД),
/// пока пользователь не закроет, не отложит или не выполнит задачу.
/// </summary>
public sealed class NotificationService : IDisposable
{
    private readonly TodoRepository _repo;
    private readonly Action _tasksChanged;
    private readonly TelegramService? _telegram;
    private readonly DispatcherTimer _timer = new();

    public ObservableCollection<AppNotification> Active { get; } = new();

    public NotificationService(TodoRepository repo, Action tasksChanged, TelegramService? telegram = null)
    {
        _repo = repo; _tasksChanged = tasksChanged; _telegram = telegram;
        _timer.Tick += (_, _) => Refresh();
        SystemEvents.PowerModeChanged += OnPower;
        SystemEvents.TimeChanged += OnTime;
    }

    public void Refresh()
    {
        _timer.Stop();
        var now = DateTime.Now;
        var fresh = _repo.Tick(now);
        var current = _repo.GetActiveNotifications();

        for (var i = Active.Count - 1; i >= 0; i--)
            if (current.All(c => c.Id != Active[i].Id)) Active.RemoveAt(i);
        foreach (var n in current)
            if (Active.All(a => a.Id != n.Id)) Active.Add(n);

        if (fresh.Count > 0) SystemSounds.Asterisk.Play();
        _telegram?.Enqueue(fresh);
        Schedule(now);
    }

    private void Schedule(DateTime now)
    {
        if (_repo.NextWake() is not { } next) return;
        // верхняя граница на случай перевода часов
        var delay = next - now;
        if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
        if (delay > TimeSpan.FromMinutes(30)) delay = TimeSpan.FromMinutes(30);
        _timer.Interval = delay;
        _timer.Start();
    }

    public void Dismiss(AppNotification n) { _repo.Dismiss(n.Id); Refresh(); }

    public void Snooze(AppNotification n, DateTime until) { _repo.Snooze(n.Id, until); Refresh(); }

    public void Done(AppNotification n)
    {
        if (n.TaskId is { } id) _repo.Complete(id, DateTime.Now);
        _repo.Dismiss(n.Id);
        _tasksChanged();   // перерисовать список задач (вызовет и Refresh)
        Refresh();
    }

    // После сна/смены времени пропущенное должно сработать сразу.
    private void OnPower(object? s, PowerModeChangedEventArgs e)
    { if (e.Mode == PowerModes.Resume) System.Windows.Application.Current.Dispatcher.Invoke(Refresh); }
    private void OnTime(object? s, EventArgs e) => System.Windows.Application.Current.Dispatcher.Invoke(Refresh);

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPower;
        SystemEvents.TimeChanged -= OnTime;
        _timer.Stop();
    }
}
