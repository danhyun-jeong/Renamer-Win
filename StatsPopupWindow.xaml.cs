using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Renamer;

public partial class StatsPopupWindow : Window
{
    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    public StatsPopupWindow()
    {
        InitializeComponent();
        BuildStats();
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
        Close();
    }

    private void BuildStats()
    {
        StatsPanel.Children.Clear();

        var reset = App.Settings.StatsResetDate;
        var since = reset == DateTime.MinValue ? DateTime.MinValue : reset;
        var (a1, r1, c1) = App.StatsSvc.GetSince(since);
        var (a2, r2, c2) = App.StatsSvc.GetLast30Days();
        var resetLabel = reset == DateTime.MinValue ? "처음부터" : reset.ToString("yy. MM. dd.");

        void AddRow(string label, string value)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "/Font_Pretendard/#Pretendard"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30))
            });
            var val = new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "/Font_Pretendard/#Pretendard"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(28, 28, 30)),
                TextAlignment = TextAlignment.Right
            };
            Grid.SetColumn(val, 1);
            g.Children.Add(val);
            StatsPanel.Children.Add(g);
        }

        void AddSeparator() => StatsPanel.Children.Add(new Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Color.FromRgb(229, 229, 234)),
            Margin = new Thickness(0, 8, 0, 8)
        });

        AddRow("분석 건수", $"{a1}건");
        AddRow("파일 이름 수정 건수", $"{r1}건");
        AddRow("누적 API 비용", $"${c1:F4}");

        var resetGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        resetGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        resetGrid.Children.Add(new TextBlock
        {
            Text = $"기준일: {resetLabel}",
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "/Font_Pretendard/#Pretendard"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
            VerticalAlignment = VerticalAlignment.Center
        });
        var resetBtn = new Button
        {
            Content = "카운팅 초기화",
            Style = (Style)Application.Current.FindResource("FlatButton"),
            FontSize = 12,
            Padding = new Thickness(8, 4, 8, 4)
        };
        resetBtn.Click += (_, _) =>
        {
            App.Settings.StatsResetDate = DateTime.Now;
            App.SettingsSvc.Save(App.Settings);
            BuildStats();
        };
        Grid.SetColumn(resetBtn, 1);
        resetGrid.Children.Add(resetBtn);
        StatsPanel.Children.Add(resetGrid);

        AddSeparator();

        StatsPanel.Children.Add(new TextBlock
        {
            Text = "지난 30일 간의",
            FontFamily = new FontFamily(new Uri("pack://application:,,,/"), "/Font_Pretendard/#Pretendard"),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
            Margin = new Thickness(0, 0, 0, 6)
        });

        AddRow("분석 건수", $"{a2}건");
        AddRow("파일 이름 수정 건수", $"{r2}건");
        AddRow("누적 API 비용", $"${c2:F4}");
    }
}
