using System.IO;
using Renamer.Models;

namespace Renamer.Services;

public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly ClaudeService _claude = new();
    private readonly PdfAnalyzer _pdfAnalyzer = new();
    private readonly ImageAnalyzer _imageAnalyzer = new();

    private string _apiKey = "";
    private string _model = "claude-haiku-4-5-20251001";
    private bool _enablePdf = true;
    private bool _enableImage = true;
    private string _articleTemplate = "{name}({year}), {title}";
    private string _posterTemplate = "(포스터){title}({when: YYMMDD})";

    public bool IsRunning { get; private set; }

    public event Action<ActivityEntry>? LogAdded;
    public event Action<bool, double>? StatRecorded;

    public void Configure(AppSettings settings)
    {
        _apiKey = settings.AnthropicAPIKey;
        _model = settings.SelectedModel;
        _enablePdf = settings.EnablePDF;
        _enableImage = settings.EnableImage;
        _articleTemplate = settings.ArticleTemplate;
        _posterTemplate = settings.PosterTemplate;
    }

    public void Start()
    {
        if (IsRunning) return;
        var downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        _watcher = new FileSystemWatcher(downloadsPath)
        {
            NotifyFilter = NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Created += OnFileCreated;
        IsRunning = true;
        EmitLog("모니터링 시작: 다운로드 폴더");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _watcher?.Dispose();
        _watcher = null;
        IsRunning = false;
        EmitLog("모니터링 중지");
    }

    private async void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        await Task.Delay(2000);
        if (!File.Exists(e.FullPath)) return;

        var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (ext == ".pdf" && _enablePdf)
            await ProcessWithRetryAsync(e.FullPath, ProcessPdfAsync);
        else if (_enableImage && _imageAnalyzer.CanProcess(e.FullPath))
            await ProcessWithRetryAsync(e.FullPath, ProcessImageAsync);
    }

    private async Task ProcessWithRetryAsync(string filePath, Func<string, Task> process)
    {
        int[] delays = [5, 15, 30];
        for (int attempt = 0; attempt <= 3; attempt++)
        {
            try
            {
                if (!File.Exists(filePath)) return;
                await process(filePath);
                return;
            }
            catch (Exception ex)
            {
                if (attempt < 3)
                {
                    var delay = delays[attempt];
                    EmitLog($"↩ 재시도 {attempt + 1}/3 ({delay}초 후): {Path.GetFileName(filePath)}");
                    await Task.Delay(delay * 1000);
                }
                else
                {
                    EmitLog($"⚠️ 최종 실패 ({Path.GetFileName(filePath)}): {ex.Message}");
                }
            }
        }
    }

    private async Task ProcessPdfAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        EmitLog($"PDF 분석 중: {fileName}");

        var result = await _pdfAnalyzer.AnalyzeAsync(filePath, _claude, _apiKey, _model);
        if (result == null) return;

        var cost = ClaudeService.CalcCost(result.Model, result.InputTokens, result.OutputTokens);

        if (!result.IsArticle)
        {
            EmitLog($"논문 아님, 제목 변경 안 함: {fileName}",
                result.InputTokens, result.OutputTokens, result.Model, cost);
            StatRecorded?.Invoke(false, cost);
            return;
        }

        var newBaseName = NameTemplate.RenderArticle(_articleTemplate, result.Author, result.Year, result.Title);
        var folder = Path.GetDirectoryName(filePath)!;
        var newPath = NameTemplate.MakeUnique(folder, newBaseName, ".pdf");
        File.Move(filePath, newPath);

        EmitLog($"✓ {fileName}\n  → {Path.GetFileName(newPath)}",
            result.InputTokens, result.OutputTokens, result.Model, cost);
        StatRecorded?.Invoke(true, cost);
    }

    private async Task ProcessImageAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        EmitLog($"이미지 분석 중: {fileName}");

        var result = await _imageAnalyzer.AnalyzeAsync(filePath, _claude, _apiKey, _model);
        if (result == null) return;

        var cost = ClaudeService.CalcCost(result.Model, result.InputTokens, result.OutputTokens);

        if (!result.IsPoster)
        {
            EmitLog($"포스터 아님, 제목 변경 안 함: {fileName}",
                result.InputTokens, result.OutputTokens, result.Model, cost);
            StatRecorded?.Invoke(false, cost);
            return;
        }

        var newBaseName = NameTemplate.RenderPoster(_posterTemplate, result.EventTitle, result.Year, result.Month, result.Day);
        var ext = Path.GetExtension(filePath);
        var folder = Path.GetDirectoryName(filePath)!;
        var newPath = NameTemplate.MakeUnique(folder, newBaseName, ext);
        File.Move(filePath, newPath);

        EmitLog($"✓ {fileName}\n  → {Path.GetFileName(newPath)}",
            result.InputTokens, result.OutputTokens, result.Model, cost);
        StatRecorded?.Invoke(true, cost);
    }

    private void EmitLog(string message, int? inputTokens = null, int? outputTokens = null,
        string? model = null, double? cost = null)
    {
        LogAdded?.Invoke(new ActivityEntry
        {
            Message = message,
            Timestamp = DateTime.Now,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            Model = model,
            Cost = cost
        });
    }

    public void Dispose() => Stop();
}
