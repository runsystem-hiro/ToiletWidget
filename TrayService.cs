using System;
using System.IO;
using System.Windows.Forms;

namespace ToiletWidget;

public sealed class TrayService : IDisposable
{
    private readonly Func<Settings> _getSettings;
    private readonly Action _saveSettings;

    private NotifyIcon? _tray;

    // Menu items (状態同期用)
    private ToolStripMenuItem? _miAlwaysOnTop;
    private ToolStripMenuItem? _miOff;
    private ToolStripMenuItem? _miFocus;
    private ToolStripMenuItem? _miBusy;
    private ToolStripMenuItem? _miFree;

    public event Action? ShowRequested;
    public event Action? HideRequested;
    public event Action? ExitRequested;

    // 追加：常に最前面の切替通知
    public event Action<bool>? AlwaysOnTopChanged;

    public TrayService(Func<Settings> getSettings, Action saveSettings)
    {
        _getSettings = getSettings;
        _saveSettings = saveSettings;
    }

    public void Initialize(string tooltipText, string? iconPath)
    {
        if (_tray != null) return;

        _tray = new NotifyIcon
        {
            Visible = true,
            Text = tooltipText
        };

        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            _tray.Icon = new System.Drawing.Icon(iconPath);
        }

        var menu = BuildMenu();
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, __) => ShowRequested?.Invoke();

        SyncMenuFromSettings();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("表示", null, (_, __) => ShowRequested?.Invoke());
        menu.Items.Add("非表示", null, (_, __) => HideRequested?.Invoke());

        menu.Items.Add(new ToolStripSeparator());

        // 追加：常に最前面（チェック）
        _miAlwaysOnTop = new ToolStripMenuItem("常に最前面");
        _miAlwaysOnTop.Click += (_, __) =>
        {
            var s = _getSettings();
            s.AlwaysOnTop = !s.AlwaysOnTop;
            _saveSettings();
            SyncMenuFromSettings();
            AlwaysOnTopChanged?.Invoke(s.AlwaysOnTop);
        };
        menu.Items.Add(_miAlwaysOnTop);

        menu.Items.Add(new ToolStripSeparator());

        // 通知モード（ラジオ）
        _miOff = new ToolStripMenuItem("通知: OFF");
        _miFocus = new ToolStripMenuItem("通知: 前面表示");

        _miOff.Click += (_, __) =>
        {
            _getSettings().Mode = NotificationMode.Off;
            _saveSettings();
            SyncMenuFromSettings();
        };

        _miFocus.Click += (_, __) =>
        {
            _getSettings().Mode = NotificationMode.FocusOnly;
            _saveSettings();
            SyncMenuFromSettings();
        };

        menu.Items.Add(_miOff);
        menu.Items.Add(_miFocus);

        menu.Items.Add(new ToolStripSeparator());

        _miBusy = new ToolStripMenuItem("使用中になったら通知");
        _miFree = new ToolStripMenuItem("空きになったら通知");

        _miBusy.Click += (_, __) =>
        {
            var s = _getSettings();
            s.NotifyOnBusy = !s.NotifyOnBusy;
            _saveSettings();
            SyncMenuFromSettings();
        };

        _miFree.Click += (_, __) =>
        {
            var s = _getSettings();
            s.NotifyOnFree = !s.NotifyOnFree;
            _saveSettings();
            SyncMenuFromSettings();
        };

        menu.Items.Add(_miBusy);
        menu.Items.Add(_miFree);

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("終了", null, (_, __) => ExitRequested?.Invoke());

        return menu;
    }

    public void SyncMenuFromSettings()
    {
        if (_tray == null) return;

        var s = _getSettings();

        if (_miAlwaysOnTop != null) _miAlwaysOnTop.Checked = s.AlwaysOnTop;

        if (_miOff != null) _miOff.Checked = s.Mode == NotificationMode.Off;
        if (_miFocus != null) _miFocus.Checked = s.Mode == NotificationMode.FocusOnly;

        if (_miBusy != null) _miBusy.Checked = s.NotifyOnBusy;
        if (_miFree != null) _miFree.Checked = s.NotifyOnFree;
    }

    public void Dispose()
    {
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _tray = null;
        }
    }
}
