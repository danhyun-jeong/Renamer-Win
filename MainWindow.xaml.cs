using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Media;
using Renamer.Services;

namespace Renamer;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = App.LogSvc.Entries;
        App.LogSvc.Entries.CollectionChanged += (_, _) => UpdateEmptyHint();
        UpdateStatus();
        UpdateApiKeyState();
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
        if (!StatsPopup.IsOpen) Hide();
    }

    public void PositionNearTray()
    {
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 8;
        Top = area.Bottom - ActualHeight - 8;
    }

    private void UpdateStatus()
    {
        var running = App.WatcherSvc.IsRunning;
        StatusDot.Fill = running
            ? new SolidColorBrush(Color.FromRgb(52, 199, 89))
            : new SolidColorBrush(Color.FromRgb(255, 149, 0));
        StatusText.Text = running ? "동작 중" : "동작 중지";
        ToggleBtn.Content = running ? "중지" : "시작";
    }

    private void UpdateApiKeyState()
    {
        var hasKey = !string.IsNullOrWhiteSpace(App.Settings.AnthropicAPIKey);
        ToggleBtn.Visibility = hasKey ? Visibility.Visible : Visibility.Collapsed;
        NoApiText.Visibility = hasKey ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = App.LogSvc.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleBtn_Click(object sender, RoutedEventArgs e)
    {
        if (App.WatcherSvc.IsRunning)
            App.WatcherSvc.Stop();
        else
            App.WatcherSvc.Start();
        UpdateStatus();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        Hide();
        var win = new SettingsWindow { Owner = null };
        win.Closed += (_, _) =>
        {
            UpdateStatus();
            UpdateApiKeyState();
        };
        win.ShowDialog();
    }

    private void StatsBtn_Click(object sender, RoutedEventArgs e)
    {
        BuildStatsPanel();
        StatsPopup.IsOpen = !StatsPopup.IsOpen;
    }

    private void QuitBtn_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void BuildStatsPanel()
    {
        StatsPanel.Children.Clear();

        var reset = App.Settings.StatsResetDate;
        var since = reset == DateTime.MinValue ? DateTime.MinValue : reset;
        var (a1, r1, c1) = App.StatsSvc.GetSince(since);
        var (a2, r2, c2) = App.StatsSvc.GetLast30Days();
        var resetLabel = reset == DateTime.MinValue ? "처음부터" : reset.ToString("yy. MM. dd.");

        void AddRow(string label, string value, bool bold = false)
        {
            var g = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Foreground = bold ? new SolidColorBrush(Color.FromRgb(28, 28, 30)) : new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            var val = new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily("Segoe UI Mono, Consolas"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(val, 1);
            g.Children.Add(lbl);
            g.Children.Add(val);
            StatsPanel.Children.Add(g);
        }

        void AddSeparator()
        {
            StatsPanel.Children.Add(new Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Color.FromRgb(229, 229, 234)),
                Margin = new Thickness(0, 8, 0, 8)
            });
        }

        AddRow("분석 건수", $"{a1}건");
        AddRow("파일 이름 수정 건수", $"{r1}건");
        AddRow("누적 API 비용", $"${c1:F4}");

        // Reset date + button row
        var resetGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var resetLbl = new TextBlock
        {
            Text = $"기준일: {resetLabel}",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
            VerticalAlignment = VerticalAlignment.Center
        };
        var resetBtn = new Button
        {
            Content = "카운팅 초기화",
            Style = (Style)FindResource("FlatButton"),
            FontSize = 12,
            Padding = new Thickness(8, 4, 8, 4)
        };
        resetBtn.Click += (_, _) =>
        {
            App.Settings.StatsResetDate = DateTime.Now;
            App.SettingsSvc.Save(App.Settings);
            BuildStatsPanel();
        };
        Grid.SetColumn(resetBtn, 1);
        resetGrid.Children.Add(resetLbl);
        resetGrid.Children.Add(resetBtn);
        StatsPanel.Children.Add(resetGrid);

        AddSeparator();

        var section2 = new TextBlock
        {
            Text = "지난 30일 간의",
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
            Margin = new Thickness(0, 0, 0, 4)
        };
        StatsPanel.Children.Add(section2);

        AddRow("분석 건수", $"{a2}건");
        AddRow("파일 이름 수정 건수", $"{r2}건");
        AddRow("누적 API 비용", $"${c2:F4}");
    }
}
