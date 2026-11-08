using System.Windows;
using System.Windows.Input;
using TodoDock.Services;
using TodoDock.ViewModels;

namespace TodoDock.Views;

/// <summary>
/// Окно с задачами на рабочем столе: лежит под всеми окнами приложений и показывает задачи
/// только тех папок, у которых включено «Показывать на рабочем столе».
/// </summary>
public partial class DesktopWidgetWindow : Window
{
    private readonly MainViewModel _vm;
    private bool _enabled;
    private bool _pending;

    public DesktopWidgetWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        _enabled = vm.GetSetting("widget.enabled") != "0";

        var area = SystemParameters.WorkArea;
        MaxHeight = area.Height - 80;
        Left = double.TryParse(vm.GetSetting("widget.left"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var l) ? l : area.Right - Width - 24;
        Top = double.TryParse(vm.GetSetting("widget.top"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : area.Top + 100;
        ClampToScreen();

        SourceInitialized += (_, _) => { NativeMethods.MakeToolNoActivate(this); NativeMethods.KeepAtBottom(this); };
        // Список пересобирается целиком при каждом обновлении — оцениваем видимость один раз после серии изменений,
        // иначе окно мигало бы (скрыть/показать) и теряло своё место под другими окнами.
        vm.WidgetFolders.CollectionChanged += (_, _) =>
        {
            if (_pending) return;
            _pending = true;
            Dispatcher.BeginInvoke(new Action(() => { _pending = false; UpdateVisibility(); }), System.Windows.Threading.DispatcherPriority.Background);
        };
        UpdateVisibility();
    }

    public bool IsEnabledByUser => _enabled;

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _vm.SetSetting("widget.enabled", enabled ? "1" : "0");
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var show = _enabled && _vm.WidgetFolders.Count > 0;
        if (show && !IsVisible) { Show(); NativeMethods.SendToBottom(this); }
        else if (!show && IsVisible) Hide();
    }

    private void ClampToScreen()
    {
        var vx = SystemParameters.VirtualScreenLeft; var vy = SystemParameters.VirtualScreenTop;
        var vw = SystemParameters.VirtualScreenWidth; var vh = SystemParameters.VirtualScreenHeight;
        if (Left < vx || Left > vx + vw - 80 || Top < vy || Top > vy + vh - 80)
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 24; Top = area.Top + 100;
        }
    }

    private void OnDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();                                   // блокируется до отпускания кнопки
        ClampToScreen();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        _vm.SetSetting("widget.left", Left.ToString(inv));
        _vm.SetSetting("widget.top", Top.ToString(inv));
    }
}
