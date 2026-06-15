namespace Renamer.Models;

public class AppSettings
{
    public string AnthropicAPIKey { get; set; } = "";
    public string SelectedModel { get; set; } = "claude-haiku-4-5-20251001";
    public bool EnablePDF { get; set; } = true;
    public bool EnableImage { get; set; } = true;
    public string ArticleTemplate { get; set; } = "{name}({year}), {title}";
    public string PosterTemplate { get; set; } = "(포스터){title}({when: YYMMDD})";
    public int MaxLogCount { get; set; } = 100;
    public DateTime StatsResetDate { get; set; } = DateTime.MinValue;
}
