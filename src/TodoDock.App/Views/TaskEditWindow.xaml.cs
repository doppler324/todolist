using System.Windows;
using TodoDock.Core;

namespace TodoDock.Views;

/// <summary>Редактор задачи: название, заметка, папка, приоритет, срок и повтор.</summary>
public partial class TaskEditWindow : Window
{
    private readonly TaskItem _item;
    private sealed record RepeatOption(string Name, string? Rule);
    private readonly List<RepeatOption> _repeats = new()
    {
        new("Не повторять", null), new("Каждый день", "daily"), new("По будням (пн–пт)", "weekdays"),
        new("Каждую неделю", "weekly"), new("Каждый месяц", "monthly"),
    };
    private static readonly Folder NoFolder = new() { Id = 0, Name = "Без папки" };

    public TaskEditWindow(TaskItem item, IEnumerable<Folder> folders)
    {
        InitializeComponent();
        _item = item;
        TitleBox.Text = item.Title;
        NoteBox.Text = item.Note;
        TgBox.IsChecked = item.SendTelegram;

        var list = new List<Folder> { NoFolder };
        list.AddRange(folders);
        FolderBox.ItemsSource = list;
        FolderBox.SelectedItem = list.FirstOrDefault(f => f.Id == (item.FolderId ?? 0)) ?? NoFolder;

        PriorityBox.ItemsSource = new[] { "Низкий", "Обычный", "Высокий" };
        PriorityBox.SelectedIndex = (int)item.Priority;

        if (item.DueAt is { } d) { DueDate.SelectedDate = d.Date; DueTime.Text = d.ToString("HH:mm"); }

        // нестандартное правило (например, «пн,ср,пт») сохраняем как отдельный пункт
        if (item.RepeatRule != null && _repeats.All(r => r.Rule != item.RepeatRule))
            _repeats.Add(new(RepeatRule.Parse(item.RepeatRule)?.Describe() ?? item.RepeatRule, item.RepeatRule));
        RepeatBox.ItemsSource = _repeats;
        RepeatBox.SelectedItem = _repeats.First(r => r.Rule == item.RepeatRule);

        Loaded += (_, _) => { TitleBox.Focus(); TitleBox.SelectAll(); };
    }

    private void OnClearDue(object sender, RoutedEventArgs e) => DueDate.SelectedDate = null;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        if (title.Length == 0) { ShowError("Введите название."); return; }

        DateTime? due = null;
        if (DueDate.SelectedDate is { } day)
        {
            if (!TimeSpan.TryParse(DueTime.Text.Trim(), out var t) || t < TimeSpan.Zero || t >= TimeSpan.FromDays(1))
                { ShowError("Время нужно в формате ЧЧ:ММ, например 18:30."); return; }
            due = day.Date + new TimeSpan(t.Hours, t.Minutes, 0);
        }
        var repeat = (RepeatOption)RepeatBox.SelectedItem;
        if (repeat.Rule != null && due == null) { ShowError("Для повтора укажите срок."); return; }

        _item.Title = title;
        _item.Note = NoteBox.Text.Trim();
        var folder = (Folder)FolderBox.SelectedItem;
        _item.FolderId = folder.Id == 0 ? null : folder.Id;
        _item.Priority = (Priority)PriorityBox.SelectedIndex;
        _item.DueAt = due;
        _item.RepeatRule = repeat.Rule;
        _item.SendTelegram = TgBox.IsChecked == true;
        DialogResult = true;
    }

    private void ShowError(string m) { Error.Text = m; Error.Visibility = Visibility.Visible; }
}
