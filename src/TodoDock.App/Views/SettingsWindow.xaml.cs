using System.Windows;
using System.Windows.Media;
using TodoDock.Core;
using TodoDock.Services;

namespace TodoDock.Views;

public partial class SettingsWindow : Window
{
    private readonly TelegramService _tg;
    private long? _chatId;

    public SettingsWindow(TelegramService tg)
    {
        InitializeComponent();
        _tg = tg;
        EnabledBox.IsChecked = tg.Settings.Enabled;
        _chatId = tg.Settings.ChatId;
        if (!string.IsNullOrEmpty(tg.Settings.Token)) TokenStatus.Text = "Токен сохранён. Чтобы заменить — вставьте новый.";
        ShowChat();
    }

    // Пустое поле = оставить сохранённый токен.
    private string? EffectiveToken => string.IsNullOrWhiteSpace(TokenBox.Password) ? _tg.Settings.Token : TokenBox.Password.Trim();

    private void ShowChat() =>
        ChatStatus.Text = _chatId is { } id ? $"Чат привязан (id {id})" : "Чат не привязан";

    private static void Say(System.Windows.Controls.TextBlock t, string msg, bool ok)
    { t.Text = msg; t.Foreground = ok ? Brushes.SeaGreen : Brushes.Firebrick; }

    private async void OnCheck(object sender, RoutedEventArgs e)
    {
        if (EffectiveToken is not { Length: > 0 } token) { Say(TokenStatus, "Введите токен.", false); return; }
        Say(TokenStatus, "Проверяю…", true);
        var (ok, user, err) = await _tg.Client.GetMeAsync(token);
        Say(TokenStatus, ok ? $"Бот найден: @{user}" : "Не удалось: " + err, ok);
    }

    private async void OnBind(object sender, RoutedEventArgs e)
    {
        if (EffectiveToken is not { Length: > 0 } token) { Say(ChatStatus, "Сначала введите токен.", false); return; }
        try
        {
            var chat = await _tg.Client.FindChatAsync(token);
            if (chat == null) { Say(ChatStatus, "Сообщений боту не найдено. Нажмите Start в чате с ботом и повторите.", false); return; }
            _chatId = chat.Value.ChatId;
            Say(ChatStatus, $"Чат привязан: {chat.Value.Name} (id {_chatId})", true);
        }
        catch (Exception ex) { Say(ChatStatus, "Ошибка: " + ex.Message, false); }
    }

    private async void OnTest(object sender, RoutedEventArgs e)
    {
        if (EffectiveToken is not { Length: > 0 } token || _chatId is not { } chat)
        { Say(TestStatus, "Нужны токен и привязанный чат.", false); return; }
        Say(TestStatus, "Отправляю…", true);
        var res = await _tg.Client.SendAsync(token, chat, "✅ <b>TodoDock</b>\nТестовое сообщение: уведомления будут приходить сюда.");
        Say(TestStatus, res.Ok ? "Отправлено — проверьте Telegram." : "Не удалось: " + res.Error, res.Ok);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TokenBox.Password)) _tg.Settings.Token = TokenBox.Password;
        _tg.Settings.ChatId = _chatId;
        var enable = EnabledBox.IsChecked == true;
        if (enable && (string.IsNullOrEmpty(_tg.Settings.Token) || _chatId == null))
        { Say(TestStatus, "Чтобы включить, укажите токен и привяжите чат.", false); return; }
        _tg.Settings.Enabled = enable;
        DialogResult = true;
    }
}
