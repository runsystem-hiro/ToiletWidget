using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp = System.Windows.Application;

namespace ToiletWidget;

public partial class MainWindow : Window
{
    private const int BaseWindowHeight = 140;
    private const int BottomRightMargin = 16;

    private const double DpiExtraFactor = 16.0;
    private const int DpiExtraMax = 16;

    private const string ResWidgetHtml = "ToiletWidget.Assets.appwidget.html";
    private const string ResAppIco = "ToiletWidget.Assets.app.ico";

    private readonly TrayService _tray;
    private readonly StatusNotificationPolicy _policy = new();

    private Settings _settings;
    private string? _lastStatus;
    private DateTime _lastNotifiedAtUtc = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();

        _settings = SettingsStore.LoadOrCreate();

        _tray = new TrayService(
            getSettings: () => _settings,
            saveSettings: () => SettingsStore.Save(_settings)
        );

        _tray.ShowRequested += ShowAndActivate;
        _tray.HideRequested += () => Hide();
        _tray.ExitRequested += () => WpfApp.Current.Shutdown();

         _tray.AlwaysOnTopChanged += enabled =>
        {
            Dispatcher.Invoke(() => ApplyAlwaysOnTop(enabled));
        };

        Loaded += OnLoaded;
        Closing += OnClosing;

        InitializeTray();
    }

    private void InitializeTray()
    {
        var assetsDir = EnsureAssetsOnDisk();
        var iconPath = Path.Combine(assetsDir, "app.ico");
        _tray.Initialize(tooltipText: "ToiletWidget", iconPath: iconPath);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var assetsDir = EnsureAssetsOnDisk();
            var htmlPath = Path.Combine(assetsDir, "appwidget.html");

            var userDataDir = Path.Combine(GetAppBaseDir(), "WebView2");
            Directory.CreateDirectory(userDataDir);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataDir);
            await WebView.EnsureCoreWebView2Async(env);

            WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            WebView.Source = new Uri(htmlPath);

            PositionToBottomRight();
            ApplyDpiSafetyMargin();

            ApplyAlwaysOnTop(_settings.AlwaysOnTop);

            if (!_settings.ShowOnStartup)
                Hide();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                "起動に失敗しました。\n\n" +
                "考えられる原因:\n" +
                "・WebView2 Runtime が未導入\n" +
                "・埋め込みリソース（Assets）が正しく同梱されていない\n\n" +
                $"詳細: {ex.Message}",
                "ToiletWidget",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            WpfApp.Current.Shutdown();
        }
    }

    private void ApplyAlwaysOnTop(bool enabled)
    {
        Topmost = enabled;
    }

    private void PositionToBottomRight()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Right - Width - BottomRightMargin;
        Top = work.Bottom - Height - BottomRightMargin;
    }

    private void ApplyDpiSafetyMargin()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var scale = dpi.DpiScaleY;

        var extra = (int)Math.Round((scale - 1.0) * DpiExtraFactor);
        if (extra < 0) extra = 0;
        if (extra > DpiExtraMax) extra = DpiExtraMax;

        Height = BaseWindowHeight + extra;
    }

    public void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { }
    }

    public void Root_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var miShow = new MenuItem { Header = "表示" };
        miShow.Click += (_, __) => ShowAndActivate();

        var miHide = new MenuItem { Header = "非表示" };
        miHide.Click += (_, __) => Hide();

        var miExit = new MenuItem { Header = "終了" };
        miExit.Click += (_, __) => WpfApp.Current.Shutdown();

        menu.Items.Add(miShow);
        menu.Items.Add(miHide);
        menu.Items.Add(new Separator());
        menu.Items.Add(miExit);

        menu.PlacementTarget = this;
        menu.IsOpen = true;

        e.Handled = true;
    }

    public void Close_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        WpfApp.Current.Shutdown();
        e.Handled = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        WpfApp.Current?.Shutdown();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!WebMessageParser.TryGetStatusChanged(e.WebMessageAsJson, out var status))
            return;

        HandleStatusChanged(status);
    }

    private void HandleStatusChanged(string status)
    {
        var nowUtc = DateTime.UtcNow;

        if (_policy.ShouldFocus(
                lastStatus: _lastStatus,
                newStatus: status,
                settings: _settings,
                nowUtc: nowUtc,
                lastNotifiedAtUtc: _lastNotifiedAtUtc,
                out var newLastNotifiedAtUtc))
        {
            _lastNotifiedAtUtc = newLastNotifiedAtUtc;
            _lastStatus = status;
            Dispatcher.Invoke(ShowAndActivate);
            return;
        }

        if (!string.Equals(_lastStatus, status, StringComparison.Ordinal))
            _lastStatus = status;
    }

    private void ShowAndActivate()
    {
        if (!IsVisible) Show();
        WindowState = WindowState.Normal;

        var keepOnTop = _settings.AlwaysOnTop;

        Topmost = true;
        Activate();
        Focus();

        Topmost = keepOnTop;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _tray.Dispose();
    }

    // ===== Assets: 埋め込み→LocalAppData へ展開 =====
    private static string GetAppBaseDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ToiletWidget"
        );
    }

    private static string EnsureAssetsOnDisk()
    {
        var baseDir = GetAppBaseDir();
        var assetsDir = Path.Combine(baseDir, "Assets");
        Directory.CreateDirectory(assetsDir);

        ExtractResourceToFile(ResWidgetHtml, Path.Combine(assetsDir, "appwidget.html"));
        ExtractResourceToFile(ResAppIco, Path.Combine(assetsDir, "app.ico"));

        return assetsDir;
    }

    private static void ExtractResourceToFile(string resourceName, string outPath)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(resourceName);
        if (s == null)
        {
            var names = asm.GetManifestResourceNames();
            throw new FileNotFoundException(
                $"埋め込みリソースが見つかりません: {resourceName}\n" +
                $"利用可能なリソース:\n- {string.Join("\n- ", names)}"
            );
        }

        using var ms = new MemoryStream();
        s.CopyTo(ms);
        ms.Position = 0;

        var embeddedHash = ComputeSha256Hex(ms);

        if (File.Exists(outPath))
        {
            using var fs = new FileStream(outPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var fileHash = ComputeSha256Hex(fs);

            if (string.Equals(embeddedHash, fileHash, StringComparison.OrdinalIgnoreCase))
                return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        ms.Position = 0;
        using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        ms.CopyTo(outFs);
    }

    private static string ComputeSha256Hex(Stream stream)
    {
        stream.Position = 0;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
