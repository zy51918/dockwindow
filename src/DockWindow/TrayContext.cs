using System.Drawing;
using System.Windows.Forms;

namespace DockWindow;

internal sealed class TrayContext : ApplicationContext
{
    private readonly NotifyIcon         _icon;
    private readonly DockManager        _manager;
    private readonly WindowEventHook    _hook;
    private readonly MousePoller        _poller;
    private readonly ToolStripMenuItem  _enabledItem;

    public TrayContext()
    {
        if (SynchronizationContext.Current is not WindowsFormsSynchronizationContext)
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        _hook    = new WindowEventHook(SynchronizationContext.Current!);
        _poller  = new MousePoller();
        var policy = RulesConfig.Load(RulesConfig.DefaultPath());
        _manager = new DockManager(_hook, _poller, policy);
        _poller.Start();

        _enabledItem = new ToolStripMenuItem("Enabled")
        {
            Checked      = true,
            CheckOnClick = true,
        };
        _enabledItem.CheckedChanged += (_, _) =>
        {
            _manager.Enabled = _enabledItem.Checked;
            if (!_enabledItem.Checked) _manager.RestoreAll();
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add("Reload rules", null, (_, _) =>
            _manager.ApplyPolicy(RulesConfig.Load(RulesConfig.DefaultPath())));
        menu.Items.Add("Restore all", null, (_, _) => _manager.RestoreAll());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _icon = new NotifyIcon
        {
            Icon             = LoadTrayIcon(),
            Text             = "DockWindow",
            ContextMenuStrip = menu,
            Visible          = true,
        };
    }

    private static Icon LoadTrayIcon()
    {
        var asm = typeof(TrayContext).Assembly;
        using var stream = asm.GetManifestResourceStream("DockWindow.Resources.tray.ico");
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _manager.RestoreAll();
            _icon.Visible = false;
            _icon.Dispose();
            _manager.Dispose();
            _poller.Dispose();
            _hook.Dispose();
        }
        base.Dispose(disposing);
    }
}
