using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Threading;
using TodoDock.Core;

namespace TodoDock.Services;

/// <summary>Складывает уведомления в очередь Telegram и периодически отправляет её.</summary>
public sealed class TelegramService : IDisposable
{
    private readonly TodoRepository _repo;
    private readonly TelegramOutboxProcessor _processor;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private bool _busy;

    public TelegramSettings Settings { get; }
    public TelegramClient Client { get; }

    public TelegramService(TodoRepository repo)
    {
        _repo = repo;
        Settings = new TelegramSettings(repo, Protect, Unprotect);
        Client = new TelegramClient(new HttpClient { Timeout = TimeSpan.FromSeconds(20) });
        _processor = new TelegramOutboxProcessor(repo, Client, Settings);
        _timer.Tick += async (_, _) => await KickAsync();
        _timer.Start();
    }

    public void Enqueue(IEnumerable<AppNotification> fresh)
    {
        if (!Settings.IsReady) return;
        var any = false;
        foreach (var n in fresh.Where(n => n.SendTelegram))
        { _repo.EnqueueTelegram(TelegramFormat.Notification(n), n.Id, DateTime.Now); any = true; }
        if (any) _ = KickAsync();
    }

    public async Task KickAsync()
    {
        if (_busy || !Settings.IsReady) return;
        _busy = true;
        try { await _processor.ProcessAsync(DateTime.Now); }
        catch { /* очередь повторит позже */ }
        finally { _busy = false; }
    }

    // DPAPI: токен можно расшифровать только под этой учётной записью Windows.
    private static string Protect(string s) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));
    private static string Unprotect(string s) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser));

    public void Dispose() => _timer.Stop();
}
