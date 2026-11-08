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
        _processor = new TelegramOutboxProcessor(repo, Client, Settings) { Log = Log };
        _timer.Tick += async (_, _) => await KickAsync();
        _timer.Start();
    }

    public void Enqueue(IEnumerable<AppNotification> fresh)
    {
        var list = fresh.ToList();
        if (list.Count == 0) return;
        if (!Settings.IsReady)
        {
            Log($"уведомлений: {list.Count}, но Telegram не готов (включён: {Settings.Enabled}, токен: {!string.IsNullOrEmpty(Settings.Token)}, чат: {Settings.ChatId != null}) — не отправлено");
            return;
        }
        var any = false;
        foreach (var n in list)
        {
            if (!n.SendTelegram) { Log($"«{n.Title}»: у задачи выключено «Дублировать в Telegram»"); continue; }
            _repo.EnqueueTelegram(TelegramFormat.Notification(n), n.Id, DateTime.Now);
            Log($"«{n.Title}»: поставлено в очередь");
            any = true;
        }
        if (any) _ = KickAsync();
    }

    public async Task KickAsync()
    {
        if (_busy || !Settings.IsReady) return;
        _busy = true;
        try { await _processor.ProcessAsync(DateTime.Now); }
        catch (Exception ex) { Log("сбой отправки: " + ex.Message); /* очередь повторит позже */ }
        finally { _busy = false; }
    }

    public static string LogPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TodoDock", "logs", "telegram.log");

    /// <summary>Журнал отправки (без токена). Нужен, чтобы понять, почему сообщение не дошло.</summary>
    public static void Log(string message)
    {
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LogPath)!);
            var info = new System.IO.FileInfo(LogPath);
            if (info.Exists && info.Length > 512 * 1024) info.Delete();
            System.IO.File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch { /* журнал не должен ломать приложение */ }
    }

    public TodoRepository.OutboxStats Stats => _repo.GetOutboxStats();
    public int RequeueFailed() => _repo.RequeueFailed(DateTime.Now);

    // DPAPI: токен можно расшифровать только под этой учётной записью Windows.
    private static string Protect(string s) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));
    private static string Unprotect(string s) =>
        Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser));

    public void Dispose() => _timer.Stop();
}
