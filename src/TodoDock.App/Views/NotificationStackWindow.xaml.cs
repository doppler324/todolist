using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TodoDock.Core;
using TodoDock.Services;

namespace TodoDock.Views;

/// <summary>Стопка карточек уведомлений в правом верхнем углу. Автоскрытия нет.</summary>
public partial class NotificationStackWindow : Window
{
    // Отступ сверху: ниже кнопок окна (свернуть/закрыть) у приложений на весь экран
    private const double Margin_ = 52;
    private readonly NotificationService _svc;
    private readonly DispatcherTimer _fullscreenCheck = new() { Interval = TimeSpan.FromSeconds(10) };
    private double _drawerShift;
    private bool _animating;

    public NotificationStackWindow(NotificationService svc)
    {
        _svc = svc;
        InitializeComponent();
        Cards.ItemsSource = svc.Active;
        MaxHeight = SystemParameters.WorkArea.Height - Margin_ - 24;
        SourceInitialized += (_, _) => NativeMethods.MakeToolNoActivate(this);
        SizeChanged += (_, _) => Reposition();
        svc.Active.CollectionChanged += OnChanged;
        _fullscreenCheck.Tick += (_, _) => UpdateVisibility();
        _fullscreenCheck.Start();
        UpdateVisibility();
    }

    private void OnChanged(object? s, NotifyCollectionChangedEventArgs e) => UpdateVisibility();

    /// <summary>Сдвигает стопку левее, когда выдвинута панель.</summary>
    public void SetDrawerOpen(bool open)
    {
        _drawerShift = open ? 380 : 0;
        var target = TargetLeft();
        if (!IsVisible) { Left = target; return; }
        var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(200))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _animating = true;
        // после анимации снимаем «удержание», иначе Left перестанет меняться из кода
        anim.Completed += (_, _) => { _animating = false; BeginAnimation(LeftProperty, null); Left = TargetLeft(); };
        BeginAnimation(LeftProperty, anim);
    }

    private double TargetLeft() => SystemParameters.WorkArea.Right - Width - _drawerShift;

    private void Reposition()
    {
        var area = SystemParameters.WorkArea;
        Top = area.Top + Margin_;   // правый верхний угол, стопка растёт вниз
        if (!_animating) Left = TargetLeft();
    }

    private void UpdateVisibility()
    {
        // Уведомления продолжают храниться; при полноэкранном приложении просто не показываем окно.
        var shouldShow = _svc.Active.Count > 0 && !NativeMethods.IsFullscreenAppRunning();
        if (shouldShow && !IsVisible) { Left = TargetLeft(); Show(); Reposition(); }
        else if (!shouldShow && IsVisible) Hide();
    }

    private static AppNotification? N(object sender) => (sender as FrameworkElement)?.DataContext as AppNotification;

    private void OnClose(object sender, RoutedEventArgs e) { if (N(sender) is { } n) _svc.Dismiss(n); }
    private void OnDone(object sender, RoutedEventArgs e) { if (N(sender) is { } n) _svc.Done(n); }

    private void OnSnooze(object sender, RoutedEventArgs e)
    {
        if (N(sender) is not { } n) return;
        var tag = (string)((FrameworkElement)sender).Tag;
        var until = tag == "tomorrow" ? DateTime.Today.AddDays(1).AddHours(9) : DateTime.Now.AddMinutes(int.Parse(tag));
        _svc.Snooze(n, until);
    }
}
