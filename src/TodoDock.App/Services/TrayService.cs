using System.Drawing;
using System.Windows.Forms;

namespace TodoDock.Services;

internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayService(Action toggle, Action openSettings, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть / скрыть панель", null, (_, _) => toggle());
        menu.Items.Add("Настройки (Telegram)…", null, (_, _) => openSettings());
        var auto = new ToolStripMenuItem("Запускать при входе в Windows") { Checked = Autostart.IsEnabled, CheckOnClick = true };
        auto.CheckedChanged += (_, _) => Autostart.Set(auto.Checked);
        menu.Items.Add(auto);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => exit());

        _icon = new NotifyIcon { Icon = LoadIcon(), Text = "TodoDock", Visible = true, ContextMenuStrip = menu };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) toggle(); };
    }

    private static Icon LoadIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/app.ico"));
            using var stream = info.Stream;
            return new Icon(stream, SystemInformation.SmallIconSize);
        }
        catch { return SystemIcons.Application; }
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
