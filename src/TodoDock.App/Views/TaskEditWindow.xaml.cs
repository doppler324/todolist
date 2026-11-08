using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TodoDock.Core;

namespace TodoDock.Views;

/// <summary>Редактор задачи: название, заметка, папка, срок, повтор, дополнительные напоминания.</summary>
public partial class TaskEditWindow : Window
{
    private const string RepeatDaysOfWeek = "@days", RepeatMonthDays = "@monthdays";
    private static readonly string[] DateFormats = { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy", "dd.MM", "d.M" };
    private static readonly string[] TimeFormats = { @"h\:mm", @"hh\:mm" };
    private static readonly CultureInfo Ru = new("ru-RU");

    private readonly TaskItem _item;
    private readonly List<DateTime> _reminders;
    private TextBox _calTarget = null!;
    private DateTime _viewMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public TaskEditWindow(TaskItem item, IEnumerable<Folder> folders)
    {
        InitializeComponent();
        _item = item;
        _reminders = new List<DateTime>(item.Reminders);
        _calTarget = DueDate;
        TitleBox.Text = item.Title;
        NoteBox.Text = item.Note;
        TgBox.IsChecked = item.SendTelegram;

        AddPill(FolderPanel, "folder", "Без папки", 0L, (item.FolderId ?? 0) == 0);
        foreach (var f in folders) AddPill(FolderPanel, "folder", $"{f.Icon} {f.Name}", f.Id, item.FolderId == f.Id);

        BuildRepeat(item.RepeatRule);

        if (item.DueAt is { } d) { DueDate.Text = d.ToString("dd.MM.yyyy"); DueTime.Text = d.ToString("HH:mm"); }
        DueDate.TextChanged += (_, _) => DatePlaceholder.Visibility = DueDate.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        DatePlaceholder.Visibility = DueDate.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        // маски ввода: двоеточие и точки ставятся сами
        InputMasks.AttachTime(DueTime); InputMasks.AttachTime(RemTime);
        InputMasks.AttachDate(DueDate); InputMasks.AttachDate(RemDate);

        RenderReminders();
        Loaded += (_, _) => { TitleBox.Focus(); TitleBox.SelectAll(); };
    }

    // ---------- Повтор ----------
    private void BuildRepeat(string? current)
    {
        var rule = RepeatRule.Parse(current);
        var weekdays = RepeatRule.Weekdays.ToString();
        string selected = current == null || rule == null ? "" :
            rule.IsMonthDays ? RepeatMonthDays :
            rule.IsDaysOfWeek && rule.ToString() != weekdays ? RepeatDaysOfWeek : rule.ToString();

        var options = new (string Name, string Tag)[]
        {
            ("Не повторять", ""), ("Каждый день", "daily"), ("По будням", weekdays), ("Каждую неделю", "weekly"),
            ("Каждый месяц", "monthly"), ("Дни недели…", RepeatDaysOfWeek), ("Числа месяца…", RepeatMonthDays),
        };
        foreach (var (name, tag) in options)
        {
            var rb = AddPill(RepeatPanel, "repeat", name, tag, tag == selected);
            rb.Checked += (_, _) => UpdateRepeatSubPanels();
        }

        var names = new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
        var order = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        for (var i = 0; i < 7; i++)
            WeekdaysPanel.Children.Add(new ToggleButton
            {
                Content = names[i], Tag = order[i], Style = (Style)FindResource("ChipToggle"),
                IsChecked = selected == RepeatDaysOfWeek && rule!.WeekDays.Contains(order[i]),
            });

        if (rule is { IsMonthDays: true }) MonthDaysBox.Text = string.Join(", ", rule.MonthDays);
        UpdateRepeatSubPanels();
    }

    private void UpdateRepeatSubPanels()
    {
        if (RepeatPanel.Children.Count == 0 || WeekdaysPanel == null) return;
        var tag = (string)Selected(RepeatPanel);
        WeekdaysPanel.Visibility = tag == RepeatDaysOfWeek ? Visibility.Visible : Visibility.Collapsed;
        MonthDaysGrid.Visibility = tag == RepeatMonthDays ? Visibility.Visible : Visibility.Collapsed;
    }

    private RadioButton AddPill(Panel panel, string group, string text, object tag, bool selected)
    {
        var rb = new RadioButton { Content = text, Tag = tag, GroupName = group, IsChecked = selected, Style = (Style)FindResource("ChipRadio") };
        panel.Children.Add(rb);
        return rb;
    }

    private static object Selected(Panel p) => p.Children.OfType<RadioButton>().First(r => r.IsChecked == true).Tag;

    // ---------- Дополнительные напоминания ----------
    private void RenderReminders()
    {
        RemindersPanel.Children.Clear();
        foreach (var at in _reminders.OrderBy(x => x))
        {
            var chip = new Button
            {
                Content = $"🔔 {at:dd.MM.yyyy HH:mm}  ✕", Style = (Style)FindResource("ChipButton"), Tag = at,
                Margin = new Thickness(0, 0, 6, 6), ToolTip = "Убрать напоминание",
            };
            chip.Click += (_, _) => { _reminders.Remove((DateTime)chip.Tag); RenderReminders(); };
            RemindersPanel.Children.Add(chip);
        }
    }

    private void OnAddReminder(object sender, RoutedEventArgs e)
    {
        Error.Visibility = Visibility.Collapsed;
        if (!TryParseDate(RemDate.Text, out var day)) { ShowError("Для напоминания укажите дату: дд.мм.гггг."); return; }
        if (!TryParseTime(RemTime.Text, out var t)) { ShowError("Время напоминания в формате ЧЧ:ММ."); return; }
        var at = day.Date + t;
        if (!_reminders.Contains(at)) _reminders.Add(at);
        RemDate.Text = "";
        RenderReminders();
    }

    // ---------- Разбор даты и времени ----------
    private static bool TryParseDate(string text, out DateTime day)
    {
        text = text.Trim();
        if (!DateTime.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return false;
        if (text.Count(c => c == '.') < 2 && day < DateTime.Today) day = day.AddYears(1);   // «15.11» без года — ближайшая такая дата
        return true;
    }

    private static bool TryParseTime(string text, out TimeSpan t) =>
        TimeSpan.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, out t) && t < TimeSpan.FromDays(1);

    // ---------- Календарь (общий для срока и напоминаний) ----------
    private DateTime? CurrentDate() => TryParseDate(_calTarget.Text, out var d) ? d : null;

    private void OnOpenCalendar(object sender, RoutedEventArgs e) => OpenCalendarFor(DueDate);
    private void OnOpenRemCalendar(object sender, RoutedEventArgs e) => OpenCalendarFor(RemDate);

    private void OpenCalendarFor(TextBox target)
    {
        _calTarget = target;
        CalPopup.PlacementTarget = target;
        var d = CurrentDate() ?? DateTime.Today;
        _viewMonth = new DateTime(d.Year, d.Month, 1);
        if (CalWeekdays.Children.Count == 0)
            foreach (var n in new[] { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" })
                CalWeekdays.Children.Add(new TextBlock { Text = n, HorizontalAlignment = HorizontalAlignment.Center, FontSize = 11, Foreground = (Brush)FindResource("MutedBrush") });
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
        var accent = (Brush)FindResource("AccentBrush");
        var text = (Brush)FindResource("TextBrush");
        var muted = (Brush)FindResource("MutedBrush");
        for (var i = 0; i < 42; i++)
        {
            var day = _viewMonth.AddDays(i - offset);
            var btn = new Button
            {
                Content = day.Day.ToString(), Style = (Style)FindResource("CalDay"), Tag = day,
                Foreground = day.Month == _viewMonth.Month ? text : muted,
            };
            if (day.Date == DateTime.Today) btn.BorderBrush = accent;
            if (selected?.Date == day.Date) { btn.Background = accent; btn.Foreground = Brushes.White; }
            btn.Click += (_, _) =>
            {
                _calTarget.Text = ((DateTime)btn.Tag).ToString("dd.MM.yyyy");
                if (_calTarget == DueDate && string.IsNullOrWhiteSpace(DueTime.Text)) DueTime.Text = "09:00";
                CalPopup.IsOpen = false;
            };
            CalDays.Children.Add(btn);
        }
    }

    // ---------- Окно ----------
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
            if (!TryParseDate(DueDate.Text, out var day)) { ShowError("Дата в формате дд.мм.гггг, например 15.11.2026."); return; }
            if (!TryParseTime(DueTime.Text, out var t)) { ShowError("Время в формате ЧЧ:ММ, например 18:30."); return; }
            due = day.Date + t;
        }

        // повтор
        string? rule = null;
        switch ((string)Selected(RepeatPanel))
        {
            case "": break;
            case RepeatDaysOfWeek:
                var days = WeekdaysPanel.Children.OfType<ToggleButton>().Where(b => b.IsChecked == true).Select(b => (DayOfWeek)b.Tag).ToArray();
                if (days.Length == 0) { ShowError("Отметьте хотя бы один день недели."); return; }
                rule = RepeatRule.OnDays(days).ToString();
                break;
            case RepeatMonthDays:
                var nums = MonthDaysBox.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => int.TryParse(x, out var n) && n is >= 1 and <= 31 ? n : 0).ToArray();
                if (nums.Length == 0 || nums.Any(n => n == 0)) { ShowError("Числа месяца — от 1 до 31 через запятую, например: 5, 15, 25."); return; }
                rule = RepeatRule.OnMonthDays(nums).ToString();
                break;
            case var other: rule = other; break;
        }
        if (rule != null && due == null) { ShowError("Для повтора укажите срок."); return; }
        // «по числам» и «по дням недели»: первый срок — ближайший подходящий день
        if (rule != null && due is { } d0 && RepeatRule.Parse(rule) is { } rr) due = rr.FirstOnOrAfter(d0);

        _item.Title = title;
        _item.Note = NoteBox.Text.Trim();
        var folderId = (long)Selected(FolderPanel);
        _item.FolderId = folderId == 0 ? null : folderId;
        _item.DueAt = due;
        _item.RepeatRule = rule;
        _item.Reminders = _reminders.OrderBy(x => x).ToList();
        _item.SendTelegram = TgBox.IsChecked == true;
        DialogResult = true;
    }
}
