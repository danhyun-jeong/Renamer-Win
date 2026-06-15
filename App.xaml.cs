using System.Drawing;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using Renamer.Models;
using Renamer.Services;

namespace Renamer;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = new();
    public static SettingsService SettingsSvc { get; } = new();
    public static LogService LogSvc { get; } = new();
    public static StatsService StatsSvc { get; } = new();
    public static FileWatcherService WatcherSvc { get; } = new();

    private TaskbarIcon? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Settings = SettingsSvc.Load();
        LogSvc.SetMaxCount(Settings.MaxLogCount);
        LogSvc.Load();
        StatsSvc.Load();

        WatcherSvc.Configure(Settings);
        WatcherSvc.LogAdded += OnLogAdded;
        WatcherSvc.StatRecorded += OnStatRecorded;

        _trayIcon = new TaskbarIcon
        {
            Icon = CreateTrayIcon(),
            ToolTipText = "Renamer",
        };
        _trayIcon.TrayLeftMouseUp += OnTrayClick;
        _trayIcon.TrayRightMouseUp += OnTrayRightClick;

        _mainWindow = new MainWindow();

        if (!string.IsNullOrWhiteSpace(Settings.AnthropicAPIKey))
            WatcherSvc.Start();
    }

    private void OnTrayClick(object? sender, RoutedEventArgs e)
    {
        if (_mainWindow == null) return;
        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.PositionNearTray();
            _mainWindow.Show();
            _mainWindow.Activate();
        }
    }

    private void OnTrayRightClick(object? sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var quit = new System.Windows.Controls.MenuItem { Header = "종료" };
        quit.Click += (_, _) => Shutdown();
        menu.Items.Add(quit);
        menu.IsOpen = true;
    }

    private void OnLogAdded(ActivityEntry entry)
    {
        Dispatcher.Invoke(() => LogSvc.Add(entry));
    }

    private void OnStatRecorded(bool wasRenamed, double cost)
    {
        StatsSvc.Record(wasRenamed, cost);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        WatcherSvc.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private Icon CreateTrayIcon()
    {
        var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        g.FillRectangle(new SolidBrush(Color.FromArgb(0, 122, 255)), 0, 0, 32, 32);
        using var font = new Font("Segoe UI", 17, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("R", font, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        return Icon.FromHandle(bmp.GetHicon());
    }
}
