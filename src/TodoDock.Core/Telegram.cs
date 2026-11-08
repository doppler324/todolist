using System.Net;
using System.Text;
using System.Text.Json;

namespace TodoDock.Core;

public sealed record TelegramResult(bool Ok, bool Retryable, string? Error);

/// <summary>Минимальный клиент Telegram Bot API. Токен нигде не логируется.</summary>
public sealed class TelegramClient
{
    private readonly HttpClient _http;
    public TelegramClient(HttpClient http) => _http = http;

    private static string Url(string token, string method) => $"https://api.telegram.org/bot{token}/{method}";

    public async Task<TelegramResult> SendAsync(string token, long chatId, string htmlText, CancellationToken ct = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { chat_id = chatId, text = htmlText, parse_mode = "HTML", disable_web_page_preview = true });
            using var resp = await _http.PostAsync(Url(token, "sendMessage"), new StringContent(body, Encoding.UTF8, "application/json"), ct);
            if (resp.IsSuccessStatusCode) return new(true, false, null);
            var code = (int)resp.StatusCode;
            var desc = ReadDescription(await resp.Content.ReadAsStringAsync(ct)) ?? resp.StatusCode.ToString();
            // 429 и 5xx — временные; 400/401/403/404 — неверный токен/чат, повторять бессмысленно
            return new(false, code == 429 || code >= 500, $"{code}: {desc}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return new(false, true, "Нет связи с Telegram: " + ex.Message);
        }
    }

    /// <summary>Проверяет токен. Возвращает имя бота (@username) или ошибку.</summary>
    public async Task<(bool Ok, string? Username, string? Error)> GetMeAsync(string token, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(Url(token, "getMe"), ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) return (false, null, ReadDescription(json) ?? resp.StatusCode.ToString());
            using var doc = JsonDocument.Parse(json);
            return (true, doc.RootElement.GetProperty("result").GetProperty("username").GetString(), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
        { return (false, null, ex.Message); }
    }

    /// <summary>Ищет чат, из которого боту писали (после /start). Берётся самое свежее личное сообщение.</summary>
    public async Task<(long ChatId, string Name)?> FindChatAsync(string token, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(Url(token, "getUpdates?limit=100&allowed_updates=%5B%22message%22%5D"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(ReadDescription(json) ?? resp.StatusCode.ToString());
        using var doc = JsonDocument.Parse(json);
        (long, string)? found = null;
        foreach (var u in doc.RootElement.GetProperty("result").EnumerateArray())
        {
            if (!u.TryGetProperty("message", out var m)) continue;
            var chat = m.GetProperty("chat");
            if (chat.GetProperty("type").GetString() != "private") continue;
            var name = chat.TryGetProperty("first_name", out var fn) ? fn.GetString() ?? "" : "";
            if (chat.TryGetProperty("username", out var un)) name += " (@" + un.GetString() + ")";
            found = (chat.GetProperty("id").GetInt64(), name.Trim());
        }
        return found;
    }

    private static string? ReadDescription(string json)
    {
        try { using var d = JsonDocument.Parse(json); return d.RootElement.TryGetProperty("description", out var x) ? x.GetString() : null; }
        catch (JsonException) { return null; }
    }
}

public static class TelegramFormat
{
    public static string Notification(AppNotification n) =>
        $"🔔 <b>{WebUtility.HtmlEncode(n.Title)}</b>" + (n.Body.Length > 0 ? "\n" + WebUtility.HtmlEncode(n.Body) : "");
}

/// <summary>Настройки Telegram в БД. Токен хранится в защищённом виде (в приложении — DPAPI).</summary>
public sealed class TelegramSettings
{
    private readonly TodoRepository _repo;
    private readonly Func<string, string> _protect, _unprotect;

    public TelegramSettings(TodoRepository repo, Func<string, string> protect, Func<string, string> unprotect)
    { _repo = repo; _protect = protect; _unprotect = unprotect; }

    public bool Enabled { get => _repo.GetSetting("telegram.enabled") == "1"; set => _repo.SetSetting("telegram.enabled", value ? "1" : "0"); }

    public string? Token
    {
        get { var s = _repo.GetSetting("telegram.token"); if (s == null) return null; try { return _unprotect(s); } catch { return null; } }
        set => _repo.SetSetting("telegram.token", string.IsNullOrWhiteSpace(value) ? null : _protect(value.Trim()));
    }

    public long? ChatId
    {
        get => long.TryParse(_repo.GetSetting("telegram.chat_id"), out var v) ? v : null;
        set => _repo.SetSetting("telegram.chat_id", value?.ToString());
    }

    public bool IsReady => Enabled && !string.IsNullOrEmpty(Token) && ChatId != null;
}

/// <summary>Отправляет накопленную очередь с повторами (экспоненциальная задержка, потолок 1 час).</summary>
public sealed class TelegramOutboxProcessor
{
    public const int MaxAttempts = 10;
    public Action<string>? Log { get; set; }
    private readonly TodoRepository _repo;
    private readonly TelegramClient _client;
    private readonly TelegramSettings _settings;

    public TelegramOutboxProcessor(TodoRepository repo, TelegramClient client, TelegramSettings settings)
    { _repo = repo; _client = client; _settings = settings; }

    public static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, Math.Max(0, attempts - 1)), 3600));

    public async Task<int> ProcessAsync(DateTime now, CancellationToken ct = default)
    {
        var token = _settings.Token; var chat = _settings.ChatId;
        if (!_settings.Enabled || string.IsNullOrEmpty(token) || chat == null) return 0;

        var sent = 0;
        foreach (var item in _repo.GetDueOutbox(now))
        {
            var res = await _client.SendAsync(token, chat.Value, item.Text, ct);
            if (res.Ok) { _repo.MarkOutboxSent(item.Id, now); sent++; Log?.Invoke($"отправлено #{item.Id}"); continue; }
            Log?.Invoke($"ошибка #{item.Id} (попытка {item.Attempts + 1}, {(res.Retryable ? "повтор" : "окончательно")}): {res.Error}");

            var attempts = item.Attempts + 1;
            if (!res.Retryable || attempts >= MaxAttempts) _repo.MarkOutboxFailed(item.Id, res.Error);
            else _repo.MarkOutboxRetry(item.Id, attempts, now + Backoff(attempts), res.Error);
            if (res.Retryable) break; // сеть/лимит недоступны — не долбим остальные сообщения
        }
        return sent;
    }
}
