using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Controls;
using TodoDock.Services;
using TodoDock.ViewModels;

namespace TodoDock.Views;

public partial class DrawerWindow : Window
{
    private bool _shown;

    private readonly MainViewModel _vm;
    private Point _dragStart;
    private bool _dragCandidate;

    public DrawerWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
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
        if (PinButton.IsChecked != true) Close_();
    }

    // ---------- Папки ----------
    private void OnFolderClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        if (((FrameworkElement)sender).DataContext is FolderVM f) f.IsExpanded = !f.IsExpanded;
    }

    private void OnFolderEditKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape && ((FrameworkElement)sender).DataContext is FolderVM f) f.CommitEdit();
    }
    private void OnFolderEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is FolderVM { IsEditing: true } f) f.CommitEdit();
    }

    // ---------- Задачи: inline-редактирование ----------
    private void OnTaskTitleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && ((FrameworkElement)sender).DataContext is TaskVM t) { t.IsEditing = true; e.Handled = true; }
    }
    private void OnTaskEditKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape && ((FrameworkElement)sender).DataContext is TaskVM t) t.CommitEdit();
    }
    private void OnTaskEditLostFocus(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TaskVM { IsEditing: true } t) t.CommitEdit();
    }
    private void OnEditBoxVisible(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is TextBox tb) { tb.Focus(); tb.SelectAll(); }
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
