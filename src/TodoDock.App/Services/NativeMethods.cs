using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TodoDock.Services;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    /// <summary>Нажата ли сейчас левая или правая кнопка мыши (где бы ни был курсор).</summary>
    public static bool IsMouseButtonDown() => (GetAsyncKeyState(0x01) & 0x8000) != 0 || (GetAsyncKeyState(0x02) & 0x8000) != 0;

    public static Point CursorPosition() { GetCursorPos(out var p); return new Point(p.X, p.Y); }

    /// <summary>Прямоугольник окна в экранных пикселях (пустой, если окно скрыто).</summary>
    public static Rect ScreenRect(Window w)
    {
        if (!w.IsVisible || w.ActualWidth == 0) return Rect.Empty;
        var tl = w.PointToScreen(new Point(0, 0));
        var br = w.PointToScreen(new Point(w.ActualWidth, w.ActualHeight));
        return new Rect(tl, br);
    }

    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);

    /// <summary>Полноэкранное приложение/игра/презентация — всплывающие окна лучше не показывать.</summary>
    public static bool IsFullscreenAppRunning()
    {
        try { return SHQueryUserNotificationState(out var st) == 0 && st is 2 or 3 or 4; }
        catch { return false; }
    }

    /// <summary>Окно не берёт фокус и не показывается в Alt+Tab.</summary>
    public static void MakeToolNoActivate(Window w)
    {
        var h = new WindowInteropHelper(w).Handle;
        SetWindowLong(h, GWL_EXSTYLE, GetWindowLong(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
    }

    /// <summary>Скругление углов Windows 11 и (если доступно) Mica.</summary>
    public static void ApplyWin11Style(Window w, bool mica)
    {
        var h = new WindowInteropHelper(w).Handle;
        int round = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(h, 33, ref round, sizeof(int));
        if (mica)
        {
            int dark = 1; DwmSetWindowAttribute(h, 20, ref dark, sizeof(int));
            int backdrop = 2; DwmSetWindowAttribute(h, 38, ref backdrop, sizeof(int)); // Mica
        }
    }
}
