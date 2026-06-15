using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Renamer;

public partial class SettingsWindow : Window
{
    private const string DefaultArticleTemplate = "{name}({year}), {title}";
    private const string DefaultPosterTemplate = "(포스터){title}({when: YYMMDD})";

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = App.Settings;
        ApiKeyBox.Password = s.AnthropicAPIKey;
        HaikuRadio.IsChecked = s.SelectedModel.Contains("haiku");
        SonnetRadio.IsChecked = s.SelectedModel.Contains("sonnet");
        PdfToggle.IsChecked = s.EnablePDF;
        ImageToggle.IsChecked = s.EnableImage;
        ArticleTemplateBox.Text = s.ArticleTemplate;
        PosterTemplateBox.Text = s.PosterTemplate;

        foreach (ComboBoxItem item in MaxLogCombo.Items)
        {
            if (item.Tag?.ToString() == s.MaxLogCount.ToString())
            {
                MaxLogCombo.SelectedItem = item;
                break;
            }
        }
        if (MaxLogCombo.SelectedIndex < 0) MaxLogCombo.SelectedIndex = 0;

        UpdateBothOffWarning();
    }

    private async void ValidateBtn_Click(object sender, RoutedEventArgs e)
    {
        var key = ApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ShowApiStatus("API 키를 입력하세요.", "#FF3B30");
            return;
        }

        ValidateBtn.IsEnabled = false;
        ValidateBtn.Content = "확인 중...";
        ShowApiStatus("", "#8E8E93");

        var svc = new Services.ClaudeService();
        var ok = await svc.ValidateKeyAsync(key);

        ValidateBtn.IsEnabled = true;
        ValidateBtn.Content = "검증 및 저장";

        if (ok)
        {
            App.Settings.AnthropicAPIKey = key;
            App.SettingsSvc.Save(App.Settings);
            App.WatcherSvc.Configure(App.Settings);
            ShowApiStatus("✓ 저장되었습니다.", "#34C759");
        }
        else
        {
            ShowApiStatus("✗ 유효하지 않은 API 키입니다.", "#FF3B30");
        }
    }

    private void ShowApiStatus(string msg, string color)
    {
        ApiKeyStatus.Text = msg;
        ApiKeyStatus.Foreground = (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }

    private void Target_Changed(object sender, RoutedEventArgs e)
    {
        App.Settings.EnablePDF = PdfToggle.IsChecked == true;
        App.Settings.EnableImage = ImageToggle.IsChecked == true;
        App.SettingsSvc.Save(App.Settings);
        App.WatcherSvc.Configure(App.Settings);
        UpdateBothOffWarning();
    }

    private void UpdateBothOffWarning()
    {
        BothOffWarning.Visibility = (PdfToggle.IsChecked != true && ImageToggle.IsChecked != true)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SaveTemplates_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.ArticleTemplate = ArticleTemplateBox.Text.Trim();
        App.Settings.PosterTemplate = PosterTemplateBox.Text.Trim();
        App.SettingsSvc.Save(App.Settings);
        App.WatcherSvc.Configure(App.Settings);
        ShowTemplateSaved();
    }

    private async void ShowTemplateSaved()
    {
        var btn = (Button)FindName("SaveTemplatesBtn") ?? null;
        // Just flash the model selection briefly to indicate save
        var prev = Title;
        Title = "Renamer 설정 — 저장됨";
        await Task.Delay(1500);
        Title = prev;
    }

    private void ResetArticle_Click(object sender, RoutedEventArgs e)
    {
        ArticleTemplateBox.Text = DefaultArticleTemplate;
    }

    private void ResetPoster_Click(object sender, RoutedEventArgs e)
    {
        PosterTemplateBox.Text = DefaultPosterTemplate;
    }

    private void MaxLogCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MaxLogCombo.SelectedItem is not ComboBoxItem item) return;
        if (!int.TryParse(item.Tag?.ToString(), out var count)) return;
        App.Settings.MaxLogCount = count;
        App.SettingsSvc.Save(App.Settings);
        App.LogSvc.SetMaxCount(count);

        // Update model selection when combo changes
        var model = HaikuRadio.IsChecked == true
            ? "claude-haiku-4-5-20251001"
            : "claude-sonnet-4-6";
        App.Settings.SelectedModel = model;
        App.SettingsSvc.Save(App.Settings);
        App.WatcherSvc.Configure(App.Settings);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "활동 로그를 모두 삭제하시겠습니까?\n통계 데이터는 영향받지 않습니다.",
            "로그 초기화",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result == MessageBoxResult.OK)
            App.LogSvc.Clear();
    }

    protected override void OnClosed(EventArgs e)
    {
        // Save model selection on close
        var model = HaikuRadio.IsChecked == true
            ? "claude-haiku-4-5-20251001"
            : "claude-sonnet-4-6";
        App.Settings.SelectedModel = model;
        App.SettingsSvc.Save(App.Settings);
        App.WatcherSvc.Configure(App.Settings);
        base.OnClosed(e);
    }
}
