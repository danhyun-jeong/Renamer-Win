using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Renamer;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private StatsPopupWindow? _statsPopup;
    private bool _statsOpen;
    private SettingsWindow? _settingsWindow;

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = App.LogSvc.Entries;
        App.LogSvc.Entries.CollectionChanged += (_, _) =>
        {
            UpdateEmptyHint();
            // 새 항목이 추가될 때 맨 위로 스크롤
            if (App.LogSvc.Entries.Count > 0)
                LogScroll.ScrollToTop();
        };
        UpdateStatus();
        UpdateApiKeyState();
        UpdateEmptyHint();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int pref = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (!_statsOpen)
            Hide();
    }

    public void PositionNearTray()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 8;
        Top = area.Bottom - Height - 8;
    }

    public void UpdateStatus()
    {
        var running = App.WatcherSvc.IsRunning;
        StatusDot.Fill = running
            ? new SolidColorBrush(Color.FromRgb(52, 199, 89))
            : new SolidColorBrush(Color.FromRgb(255, 149, 0));
        StatusText.Text = running ? "동작 중" : "동작 중지";
        ToggleBtn.Content = running ? "중지" : "시작";
    }

    public void UpdateApiKeyState()
    {
        var hasKey = !string.IsNullOrWhiteSpace(App.Settings.AnthropicAPIKey);
        ToggleBtn.Visibility = hasKey ? Visibility.Visible : Visibility.Collapsed;
        NoApiText.Visibility = hasKey ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = App.LogSvc.Entries.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        if (App.WatcherSvc.IsRunning) App.WatcherSvc.Stop();
        else App.WatcherSvc.Start();
        UpdateStatus();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow != null)
        {
            _settingsWindow.Activate();
            return;
        }
        Hide();
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            UpdateStatus();
            UpdateApiKeyState();
        };
        _settingsWindow.Show();
    }

    private void StatsBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_statsPopup != null && _statsPopup.IsVisible)
        {
            _statsOpen = false;
            _statsPopup.Close();
            return;
        }

        _statsOpen = true;
        _statsPopup = new StatsPopupWindow();
        _statsPopup.Loaded += (_, _) =>
        {
            // Loaded 후 실제 Height 확정 — 항상 메인 창 바로 위 8px 여백
            _statsPopup.Left = Left;
            _statsPopup.Top = Top - _statsPopup.ActualHeight - 8;
        };
        _statsPopup.Show();
        _statsPopup.Closed += (_, _) =>
        {
            _statsPopup = null;
            _statsOpen = false;
            Activate();
        };
    }

    private void QuitBtn_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }
}
