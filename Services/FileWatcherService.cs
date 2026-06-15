using System.IO;
using System.Runtime.InteropServices;
using Renamer.Models;

namespace Renamer.Services;

public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly ClaudeService _claude = new();
    private readonly PdfAnalyzer _pdfAnalyzer = new();
    private readonly ImageAnalyzer _imageAnalyzer = new();

    // 처리한 파일의 인덱스(Windows inode 상당)를 기억 — rename 후에도 동일값 유지
    // 최대 500개로 고정, 초과 시 가장 오래된 것부터 제거
    private readonly HashSet<ulong> _processedFileIds = new();
    private readonly Queue<ulong> _fileIdQueue = new();
    private const int MaxTrackedIds = 500;

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
        _watcher.Created += OnFileArrived;
        _watcher.Renamed += OnFileArrived;
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

    private static readonly string[] TempExts = [".crdownload", ".part", ".tmp", ".download"];

    private void OnFileArrived(object sender, FileSystemEventArgs e)
    {
        // Renamed 이벤트일 때 출발지가 임시파일이 아니면 무시
        // (브라우저: .crdownload→.pdf ✓ / 앱 자신의 rename: .pdf→.pdf ✗)
        if (e is RenamedEventArgs renamed)
        {
            var oldExt = Path.GetExtension(renamed.OldFullPath).ToLowerInvariant();
            if (!TempExts.Contains(oldExt)) return;
        }

        HandleNewFile(e.FullPath);
    }

    private async void HandleNewFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (TempExts.Contains(ext)) return;

        bool isPdf = ext == ".pdf" && _enablePdf;
        bool isImage = _enableImage && _imageAnalyzer.CanProcess(filePath);
        if (!isPdf && !isImage) return;

        await Task.Delay(2000);
        if (!File.Exists(filePath)) return;

        // 파일 인덱스 확인 — 이미 처리한 파일이면 건너뜀
        var fileId = GetFileId(filePath);
        if (fileId.HasValue && _processedFileIds.Contains(fileId.Value)) return;

        EmitLog($"파일 감지: {Path.GetFileName(filePath)}");

        // 처리 시작 전에 등록 — rename 후 동일 인덱스로 재감지되는 것을 막음
        if (fileId.HasValue) MarkProcessed(fileId.Value);

        if (isPdf)
            await ProcessWithRetryAsync(filePath, ProcessPdfAsync);
        else
            await ProcessWithRetryAsync(filePath, ProcessImageAsync);
    }

    private void MarkProcessed(ulong fileId)
    {
        if (_processedFileIds.Contains(fileId)) return;
        if (_fileIdQueue.Count >= MaxTrackedIds)
            _processedFileIds.Remove(_fileIdQueue.Dequeue());
        _processedFileIds.Add(fileId);
        _fileIdQueue.Enqueue(fileId);
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

    // --- Windows 파일 인덱스 (inode 상당) ---

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateFile(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode,
        IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        IntPtr hObject, out ByHandleFileInformation lpFileInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    private static ulong? GetFileId(string path)
    {
        // FILE_READ_ATTRIBUTES = 0x80, FILE_SHARE_READ|WRITE|DELETE = 7
        // OPEN_EXISTING = 3, FILE_FLAG_BACKUP_SEMANTICS = 0x02000000
        var handle = CreateFile(path, 0x80, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle == new IntPtr(-1)) return null;
        try
        {
            if (!GetFileInformationByHandle(handle, out var info)) return null;
            return ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public void Dispose() => Stop();
}
