using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Controls;
using TodoDock.Services;
using TodoDock.Core;
using TodoDock.ViewModels;

namespace TodoDock.Views;

public partial class DrawerWindow : Window
{
    private bool _shown;

    private readonly MainViewModel _vm;
    private Point _dragStart;
    private bool _dragCandidate;
    private bool _dialogOpen;

    private readonly TelegramService _telegram;

    private readonly NotificationService _notifications;

    public DrawerWindow(MainViewModel vm, TelegramService telegram, NotificationService notifications)
    {
        _notifications = notifications;
        _vm = vm;
        _telegram = telegram;
        InitializeComponent();
        DataContext = vm;
        NotifList.ItemsSource = notifications.Active;
        notifications.Active.CollectionChanged += (_, _) => UpdateNotifTab();
        UpdateNotifTab();
        var area = SystemParameters.WorkArea;
        Top = area.Top;
        Height = area.Height;
        Left = area.Right;                       // за краем экрана
        SourceInitialized += (_, _) => NativeMethods.ApplyWin11Style(this, mica: false);
    }

    public bool IsOpen => _shown;
    public event Action<bool>? OpenChanged;

    public void Toggle() { if (_shown) Close_(); else Open(); }

    public void Open()
    {
        if (_shown) return;
        _shown = true;
        OpenChanged?.Invoke(true);
        Visibility = Visibility.Visible;
        Activate();
        QuickAdd.Focus();
        var right = SystemParameters.WorkArea.Right;
        BeginAnimation(LeftProperty, new DoubleAnimation(right, right - Width, TimeSpan.FromMilliseconds(200))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    public void Close_()
    {
        if (!_shown) return;
        _shown = false;
        OpenChanged?.Invoke(false);
        var right = SystemParameters.WorkArea.Right;
        var anim = new DoubleAnimation(right, TimeSpan.FromMilliseconds(160))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        anim.Completed += (_, _) => { if (!_shown) Visibility = Visibility.Hidden; };
        BeginAnimation(LeftProperty, anim);
    }

    private void OnKey(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close_(); }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (PinButton.IsChecked != true && !_dialogOpen) Close_();
    }

    // ---------- Вкладки ----------
    private void UpdateNotifTab()
    {
        var n = _notifications.Active.Count;
        TabNotifs.Content = n > 0 ? $"Уведомления ({n})" : "Уведомления";
        NotifEmpty.Visibility = n > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnTab(object sender, RoutedEventArgs e)
    {
        if (PageTasks == null) return; // событие во время InitializeComponent
        var tab = int.Parse((string)((FrameworkElement)sender).Tag);
        PageTasks.Visibility = tab == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageNotifs.Visibility = tab == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageToday.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static AppNotification? Notif(object sender) => (sender as FrameworkElement)?.DataContext as AppNotification;
    private void OnNotifClose(object sender, RoutedEventArgs e) { if (Notif(sender) is { } n) _notifications.Dismiss(n); }
    private void OnNotifDone(object sender, RoutedEventArgs e) { if (Notif(sender) is { } n) _notifications.Done(n); }
    private void OnNotifSnooze(object sender, RoutedEventArgs e)
    {
        if (Notif(sender) is not { } n) return;
        var tag = (string)((FrameworkElement)sender).Tag;
        _notifications.Snooze(n, tag == "tomorrow" ? DateTime.Today.AddDays(1).AddHours(9) : DateTime.Now.AddMinutes(int.Parse(tag)));
    }

    // ---------- Папки ----------
    private void OnFolderClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        if (((FrameworkElement)sender).DataContext is FolderVM f) f.IsExpanded = !f.IsExpanded;
    }

    private void OnFolderEditKey(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not FolderVM f) return;
        if (e.Key == Key.Enter) f.CommitEdit();
        else if (e.Key == Key.Escape) { f.Name = f.Folder?.Name ?? f.Name; f.IsEditing = false; e.Handled = true; }
    }
    private void OnFolderEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is FolderVM { IsEditing: true } f) f.CommitEdit();
    }

    // ---------- Задачи: inline-редактирование ----------
    private void OnTaskTitleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is TaskVM t) { OpenEditor(t); e.Handled = true; }
    }
    private void OnEditMenu(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TaskVM t) OpenEditor(t);
    }

    private void OnSettings(object sender, RoutedEventArgs e) => OpenSettings();

    public void OpenSettings()
    {
        _dialogOpen = true;
        try { new SettingsWindow(_telegram).ShowDialog(); }
        finally { _dialogOpen = false; if (IsOpen) Activate(); }
    }

    private void OpenEditor(TaskVM t)
    {
        _dialogOpen = true;                    // панель не должна прятаться, пока открыт диалог
        try
        {
            var copy = _vm.CloneTask(t.Item);
            var dlg = new TaskEditWindow(copy, _vm.GetFolders());
            if (dlg.ShowDialog() == true) _vm.UpdateTask(copy);
        }
        finally { _dialogOpen = false; Activate(); }
    }

    private void OnTaskEditKey(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not TaskVM t) return;
        if (e.Key == Key.Enter) t.CommitEdit();
        else if (e.Key == Key.Escape) { t.Title = t.Item.Title; t.IsEditing = false; e.Handled = true; }
    }
    private void OnTaskEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TaskVM { IsEditing: true } t) t.CommitEdit();
    }
    private void OnEditBoxVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Фокус ставим после закрытия контекстного меню, иначе оно возвращает фокус
        // обратно, поле тут же теряет его и редактирование сразу завершается.
        if (e.NewValue is true && sender is TextBox tb)
            Dispatcher.BeginInvoke(new Action(() => { tb.Focus(); Keyboard.Focus(tb); tb.SelectAll(); }),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    // ---------- Drag & drop ----------
    private void OnTaskMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragCandidate = e.OriginalSource is not (CheckBox or TextBox) && !IsInside<CheckBox>(e.OriginalSource);
    }
    private void OnTaskMouseUp(object sender, MouseButtonEventArgs e) => _dragCandidate = false;

    private void OnTaskMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragCandidate || e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(null);
        if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragCandidate = false;
        if (((FrameworkElement)sender).DataContext is TaskVM t)
        {
            var wasPinned = PinButton.IsChecked == true;
            PinButton.IsChecked = true; // не прятать панель во время перетаскивания
            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(typeof(TaskVM), t), DragDropEffects.Move);
            PinButton.IsChecked = wasPinned;
        }
    }

    private void OnDropOnFolder(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TaskVM)) is TaskVM t && ((FrameworkElement)sender).DataContext is FolderVM f)
        { _vm.MoveTask(t, f, null); e.Handled = true; }
    }
    private void OnDropOnTask(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(TaskVM)) is TaskVM t && ((FrameworkElement)sender).DataContext is TaskVM target)
        { _vm.MoveTask(t, target.Folder, target); e.Handled = true; }
    }

    private static bool IsInside<T>(object? src) where T : DependencyObject
    {
        var d = src as DependencyObject;
        while (d != null) { if (d is T) return true; d = System.Windows.Media.VisualTreeHelper.GetParent(d); }
        return false;
    }
}
