using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TodoDock.Core;

namespace TodoDock.Views;

/// <summary>Редактор задачи в стиле панели: название, заметка, папка, приоритет, срок, повтор.</summary>
public partial class TaskEditWindow : Window
{
    private readonly TaskItem _item;
    private static readonly string[] DateFormats = { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "dd.MM", "d.M" };

    public TaskEditWindow(TaskItem item, IEnumerable<Folder> folders)
    {
        InitializeComponent();
        _item = item;
        TitleBox.Text = item.Title;
        NoteBox.Text = item.Note;
        TgBox.IsChecked = item.SendTelegram;

        AddPill(FolderPanel, "folder", "Без папки", 0L, (item.FolderId ?? 0) == 0);
        foreach (var f in folders) AddPill(FolderPanel, "folder", $"{f.Icon} {f.Name}", f.Id, item.FolderId == f.Id);

        AddPill(PriorityPanel, "prio", "Низкий", Priority.Low, item.Priority == Priority.Low);
        AddPill(PriorityPanel, "prio", "Обычный", Priority.Normal, item.Priority == Priority.Normal);
        AddPill(PriorityPanel, "prio", "Высокий", Priority.High, item.Priority == Priority.High);

        var repeats = new List<(string, string?)>
        {
            ("Не повторять", null), ("Каждый день", "daily"), ("По будням", "weekdays"),
            ("Каждую неделю", "weekly"), ("Каждый месяц", "monthly"),
        };
        // нестандартное правило (например, «пн,ср,пт») сохраняем отдельным пунктом
        if (item.RepeatRule != null && repeats.All(r => r.Item2 != item.RepeatRule))
            repeats.Add((RepeatRule.Parse(item.RepeatRule)?.Describe() ?? item.RepeatRule, item.RepeatRule));
        foreach (var (name, rule) in repeats) AddPill(RepeatPanel, "repeat", name, rule ?? "", rule == item.RepeatRule);

        if (item.DueAt is { } d) { DueDate.Text = d.ToString("dd.MM.yyyy"); DueTime.Text = d.ToString("HH:mm"); }
        DueDate.TextChanged += (_, _) => DatePlaceholder.Visibility = DueDate.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        DatePlaceholder.Visibility = DueDate.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        Loaded += (_, _) => { TitleBox.Focus(); TitleBox.SelectAll(); };
    }

    private void AddPill(Panel panel, string group, string text, object tag, bool selected) =>
        panel.Children.Add(new RadioButton { Content = text, Tag = tag, GroupName = group, IsChecked = selected, Style = (Style)FindResource("ChipRadio") });

    private static object Selected(Panel p) => p.Children.OfType<RadioButton>().First(r => r.IsChecked == true).Tag;

    // ---------- Календарь ----------
    private static readonly CultureInfo Ru = new("ru-RU");
    private DateTime _viewMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    private DateTime? CurrentDate() =>
        DateTime.TryParseExact(DueDate.Text.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private void OnOpenCalendar(object sender, RoutedEventArgs e)
    {
        var d = CurrentDate() ?? DateTime.Today;
        _viewMonth = new DateTime(d.Year, d.Month, 1);
        if (CalWeekdays.Children.Count == 0)
            foreach (var n in new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" })
                CalWeekdays.Children.Add(new TextBlock { Text = n, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11,
                    Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush") });
        RenderCalendar();
        CalPopup.IsOpen = true;
    }

    private void OnPrevMonth(object s, RoutedEventArgs e) { _viewMonth = _viewMonth.AddMonths(-1); RenderCalendar(); }
    private void OnNextMonth(object s, RoutedEventArgs e) { _viewMonth = _viewMonth.AddMonths(1); RenderCalendar(); }

    private void RenderCalendar()
    {
        var name = Ru.DateTimeFormat.MonthNames[_viewMonth.Month - 1];
        CalTitle.Text = char.ToUpper(name[0]) + name[1..] + " " + _viewMonth.Year;
        CalDays.Children.Clear();
        var offset = ((int)_viewMonth.DayOfWeek + 6) % 7;      // неделя с понедельника
        var selected = CurrentDate();
        var accent = (System.Windows.Media.Brush)FindResource("AccentBrush");
        var text = (System.Windows.Media.Brush)FindResource("TextBrush");
        var muted = (System.Windows.Media.Brush)FindResource("MutedBrush");
        for (var i = 0; i < 42; i++)
        {
            var day = _viewMonth.AddDays(i - offset);
            var btn = new Button
            {
                Content = day.Day.ToString(), Style = (Style)FindResource("CalDay"), Tag = day,
                Foreground = day.Month == _viewMonth.Month ? text : muted,
            };
            if (day.Date == DateTime.Today) btn.BorderBrush = accent;
            if (selected?.Date == day.Date) { btn.Background = accent; btn.Foreground = System.Windows.Media.Brushes.White; }
            btn.Click += (_, _) =>
            {
                DueDate.Text = ((DateTime)btn.Tag).ToString("dd.MM.yyyy");
                if (string.IsNullOrWhiteSpace(DueTime.Text)) DueTime.Text = "09:00";
                CalPopup.IsOpen = false;
            };
            CalDays.Children.Add(btn);
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

    private void SetDay(DateTime d) => DueDate.Text = d.ToString("dd.MM.yyyy");
    private void OnToday(object s, RoutedEventArgs e) => SetDay(DateTime.Today);
    private void OnTomorrow(object s, RoutedEventArgs e) => SetDay(DateTime.Today.AddDays(1));
    private void OnWeek(object s, RoutedEventArgs e) => SetDay(DateTime.Today.AddDays(7));
    private void OnClearDue(object s, RoutedEventArgs e) => DueDate.Text = "";

    private void ShowError(string m) { Error.Text = m; Error.Visibility = Visibility.Visible; }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text.Trim();
        if (title.Length == 0) { ShowError("Введите название."); return; }

        DateTime? due = null;
        if (DueDate.Text.Trim().Length > 0)
        {
            if (!DateTime.TryParseExact(DueDate.Text.Trim(), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            { ShowError("Дата в формате дд.мм.гггг, например 15.11.2026."); return; }
            if (DueDate.Text.Trim().Count(c => c == '.') < 2 && day < DateTime.Today)
                day = day.AddYears(1);       // «15.11» без года — ближайшая такая дата
            if (!TimeSpan.TryParseExact(DueTime.Text.Trim(), new[] { @"h\:mm", @"hh\:mm" }, CultureInfo.InvariantCulture, out var t) || t >= TimeSpan.FromDays(1))
            { ShowError("Время в формате ЧЧ:ММ, например 18:30."); return; }
            due = day.Date + t;
        }
        var rule = (string)Selected(RepeatPanel);
        if (rule.Length > 0 && due == null) { ShowError("Для повтора укажите срок."); return; }

        _item.Title = title;
        _item.Note = NoteBox.Text.Trim();
        var folderId = (long)Selected(FolderPanel);
        _item.FolderId = folderId == 0 ? null : folderId;
        _item.Priority = (Priority)Selected(PriorityPanel);
        _item.DueAt = due;
        _item.RepeatRule = rule.Length == 0 ? null : rule;
        _item.SendTelegram = TgBox.IsChecked == true;
        DialogResult = true;
    }
}
