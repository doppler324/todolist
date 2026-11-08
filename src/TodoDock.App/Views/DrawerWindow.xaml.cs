using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Input;
using TodoDock.Services;

namespace TodoDock.Views;

public partial class DrawerWindow : Window
{
    private bool _shown;

    public DrawerWindow()
    {
        InitializeComponent();
        var area = SystemParameters.WorkArea;
        Top = area.Top;
        Height = area.Height;
        Left = area.Right;                       // за краем экрана
        SourceInitialized += (_, _) => NativeMethods.ApplyWin11Style(this, mica: false);
    }

    public bool IsOpen => _shown;

    public void Toggle() { if (_shown) Close_(); else Open(); }

    public void Open()
    {
        if (_shown) return;
        _shown = true;
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
}
