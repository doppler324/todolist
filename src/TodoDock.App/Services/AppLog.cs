using System.IO;

namespace TodoDock.Services;

/// <summary>Журнал ошибок приложения: %LOCALAPPDATA%\TodoDock\logs\app.log.</summary>
internal static class AppLog
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TodoDock", "logs", "app.log");

    public static void Error(string where, Exception ex) => Write($"{where}: {ex}");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var info = new FileInfo(Path);
            if (info.Exists && info.Length > 512 * 1024) info.Delete();
            File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
        }
        catch { /* журнал не должен ломать приложение */ }
    }
}
