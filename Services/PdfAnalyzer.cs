using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Renamer.Services;

public record PdfAnalysisResult(
    bool IsArticle, string Author, string Title, string Year,
    int InputTokens, int OutputTokens, string Model);

public class PdfAnalyzer
{
    private static readonly string[] NonArticleKeywords = ["발제", "발제문", "발표", "발표문", "초고"];
    private static readonly string[] KorDateKeywords = ["접수", "수정", "게재", "심사", "투고", "발행", "출판"];
    private static readonly string[] EngDateKeywords = ["received", "accepted", "published", "online", "revised", "submitted"];

    public async Task<PdfAnalysisResult?> AnalyzeAsync(string filePath, ClaudeService claude, string apiKey, string model)
    {
        string firstPageText, headerFooterText, lastPageSnippet;

        try
        {
            using var pdf = PdfDocument.Open(filePath);
            var pages = pdf.GetPages().ToList();
            if (pages.Count == 0) return null;

            firstPageText = GetPageText(pages[0], 1000);

            var preCheck = Path.GetFileName(filePath) + firstPageText[..Math.Min(30, firstPageText.Length)];
            if (NonArticleKeywords.Any(k => preCheck.Contains(k)))
                return new PdfAnalysisResult(false, "", "", "", 0, 0, model);

            headerFooterText = ExtractHeaderFooter(pages);
            lastPageSnippet = ExtractDateContext(pages);
        }
        catch
        {
            return null;
        }

        const string system = "You are a file naming assistant for academic papers. Respond ONLY with valid JSON. No explanation or markdown.";
        var userPrompt = BuildPrompt(firstPageText, headerFooterText, lastPageSnippet);

        var result = await claude.SendTextAsync(apiKey, model, system, userPrompt);
        var jsonStr = ClaudeService.ExtractJson(result.Text);
        if (jsonStr == null) return null;

        try
        {
            var node = JsonNode.Parse(jsonStr)!;
            var isArticle = node["is_journal_article"]?.GetValue<bool>() ?? false;
            if (!isArticle)
                return new PdfAnalysisResult(false, "", "", "", result.InputTokens, result.OutputTokens, model);

            var author = node["author"]?.GetValue<string>() ?? "";
            var title = node["main_title"]?.GetValue<string>() ?? "";
            var year = node["pub_year"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(title)) return null;

            return new PdfAnalysisResult(true, author, title, year, result.InputTokens, result.OutputTokens, model);
        }
        catch { return null; }
    }

    private string GetPageText(Page page, int maxLen = -1)
    {
        var words = page.GetWords()
            .OrderByDescending(w => w.BoundingBox.Centroid.Y)
            .ThenBy(w => w.BoundingBox.Centroid.X);
        var text = string.Join(" ", words.Select(w => w.Text));
        return maxLen > 0 && text.Length > maxLen ? text[..maxLen] : text;
    }

    private string ExtractHeaderFooter(List<Page> pages)
    {
        var sb = new StringBuilder();
        for (int i = 1; i < Math.Min(3, pages.Count); i++)
        {
            var text = GetPageText(pages[i]);
            sb.AppendLine(text.Length > 80 ? text[..80] : text);
            if (text.Length > 80) sb.AppendLine(text[^80..]);
        }
        return sb.ToString();
    }

    private string ExtractDateContext(List<Page> pages)
    {
        var sb = new StringBuilder();
        foreach (var page in pages.TakeLast(3))
        {
            var lines = GetPageLines(page).ToList();
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (KorDateKeywords.Any(k => line.Contains(k)))
                {
                    if (i > 0) sb.AppendLine(lines[i - 1]);
                    sb.AppendLine(line);
                    if (i < lines.Count - 1) sb.AppendLine(lines[i + 1]);
                    found = true;
                }
                else if (EngDateKeywords.Any(k => line.Contains(k, StringComparison.OrdinalIgnoreCase)))
                {
                    sb.AppendLine(line);
                    found = true;
                }
            }
            if (!found)
            {
                var full = string.Join(" ", lines);
                if (full.Length > 200) sb.Append(full[^200..]);
                else sb.Append(full);
            }
        }
        return sb.ToString();
    }

    private IEnumerable<string> GetPageLines(Page page)
    {
        return page.GetWords()
            .GroupBy(w => (int)Math.Round(w.BoundingBox.Centroid.Y / 5.0) * 5)
            .OrderByDescending(g => g.Key)
            .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Centroid.X).Select(w => w.Text)));
    }

    private string BuildPrompt(string firstPage, string headerFooter, string lastPage) => $$"""
        [SOURCE 1 — First page (title, authors, journal metadata)]
        ---
        {{firstPage}}
        ---

        [SOURCE 2 — Pages 2–3 header/footer excerpts (running head, journal name, volume/year)]
        ---
        {{headerFooter}}
        ---

        [SOURCE 3 — Last page (submission / acceptance / publication dates)]
        ---
        {{lastPage}}
        ---

        Is this a journal article (학술 논문)?

        A journal article typically has: title, author(s), abstract, journal name, DOI, volume/issue number.
        Not an article: textbook chapter, thesis, conference poster, report, presentation slides, 발제문, 발표문, 초고, class handout.

        For pub_year: look independently in all three sources above.
        - SOURCE 1: journal metadata at the top of the first page (volume, issue, year / DOI / copyright line)
        - SOURCE 2: running headers or footers (e.g. "Korean J. Edu. 2023, 40(2)")
        - SOURCE 3: phrases like "게재 확정일", "최종 게재일", "Accepted", "Published" followed by a date
        If two or more sources agree on a 4-digit year, return that year.
        If only one source has a year and the others are unavailable, return that year.
        If sources conflict, return the year from SOURCE 3 (publication/acceptance date is most authoritative).
        If no year is found anywhere, return "".

        Respond with JSON only (no markdown, no explanation):
        {
          "is_journal_article": true,
          "author": "Use the language of the paper's main body. Korean paper → Korean author names: (1) 1명: 성명 전체 (예: '홍길동'). (2) 2명: '저자1·저자2' (예: '홍길동·김철수'). (3) 3명 이상: 반드시 첫 번째 저자 이름만 쓰고 ' 외' 추가 (예: 저자가 홍길동·김철수·이영희이면 → '홍길동 외'). English paper → (1) 1 author: full name. (2) 2 authors: 'Name1 & Name2'. (3) 3+ authors: first author name only followed by ' et al.' (e.g. 'Smith et al.'). If both Korean and English names appear, use the Korean names.",
          "main_title": "Use the language of the paper's main body. If both Korean and English titles appear (e.g. Korean body + English abstract at the end), use the KOREAN title. Main title ONLY — omit subtitles after ':', '—', or similar separators. If the Korean title has missing word spacing (words concatenated without spaces, which can happen in both scanned and older digital PDFs), restore proper Korean word spacing.",
          "pub_year": "2023"
        }

        If NOT a journal article:
        {"is_journal_article": false, "author": "", "main_title": "", "pub_year": ""}
        """;
}
