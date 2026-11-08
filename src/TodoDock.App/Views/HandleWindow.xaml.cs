using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using TodoDock.Services;

namespace TodoDock.Views;

public partial class HandleWindow : Window
{
    private readonly DrawerWindow _drawer;

    public HandleWindow(DrawerWindow drawer)
    {
        _drawer = drawer;
        InitializeComponent();
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width;
        Top = area.Top + (area.Height - Height) / 2;
        Badge.Margin = new Thickness(0, 0, 14, 0);
        SourceInitialized += (_, _) => NativeMethods.MakeToolNoActivate(this);
    }

    /// <summary>Показывает число активных уведомлений; пока они есть — бейдж пульсирует.</summary>
    public void SetBadge(int count)
    {
        BadgeText.Text = count.ToString();
        Badge.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Badge.BeginAnimation(OpacityProperty, count > 0
            ? new DoubleAnimation(1, 0.45, TimeSpan.FromMilliseconds(700)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever }
            : null);
    }

    private void OnClick(object sender, MouseButtonEventArgs e) => _drawer.Toggle();

    private void OnEnter(object sender, MouseEventArgs e) => Animate(24, 1);
    private void OnLeave(object sender, MouseEventArgs e) => Animate(8, 0);

    private void Animate(double width, double chevron)
    {
        var d = new Duration(TimeSpan.FromMilliseconds(150));
        Pill.BeginAnimation(WidthProperty, new DoubleAnimation(width, d));
        Chevron.BeginAnimation(OpacityProperty, new DoubleAnimation(chevron, d));
    }
}
