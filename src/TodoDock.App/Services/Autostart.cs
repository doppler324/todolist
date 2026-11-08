using Microsoft.Win32;

namespace TodoDock.Services;

internal static class Autostart
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "TodoDock";

    public static bool IsEnabled
    {
        get { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(Name) != null; }
    }

    public static void Set(bool on)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key, true)!;
        if (on) k.SetValue(Name, $"\"{Environment.ProcessPath}\"");
        else k.DeleteValue(Name, false);
    }
}
