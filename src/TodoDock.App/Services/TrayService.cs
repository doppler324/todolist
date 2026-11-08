using System.Drawing;
using System.Windows.Forms;

namespace TodoDock.Services;

internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayService(Action toggle, Action exit)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Открыть / скрыть панель", null, (_, _) => toggle());
        var auto = new ToolStripMenuItem("Запускать при входе в Windows") { Checked = Autostart.IsEnabled, CheckOnClick = true };
        auto.CheckedChanged += (_, _) => Autostart.Set(auto.Checked);
        menu.Items.Add(auto);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => exit());

        _icon = new NotifyIcon { Icon = SystemIcons.Application, Text = "TodoDock", Visible = true, ContextMenuStrip = menu };
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) toggle(); };
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
