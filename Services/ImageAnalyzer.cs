using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Renamer.Services;

public record ImageAnalysisResult(
    bool IsPoster, string EventTitle, string Year, string Month, string Day,
    int InputTokens, int OutputTokens, string Model);

public class ImageAnalyzer
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private const long MaxSize = 20 * 1024 * 1024;

    public bool CanProcess(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return SupportedExtensions.Contains(ext) && new FileInfo(filePath).Length <= MaxSize;
    }

    public async Task<ImageAnalysisResult?> AnalyzeAsync(string filePath, ClaudeService claude, string apiKey, string model)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var mediaType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        var bytes = await File.ReadAllBytesAsync(filePath);
        var base64 = Convert.ToBase64String(bytes);

        const string system = "You are a file naming assistant. Analyze images and respond ONLY with valid JSON. No explanation or markdown.";
        const string userPrompt = """
            Is this image an event poster (행사 포스터, 공연 포스터, 전시 포스터, 강연 포스터, 세미나 포스터, etc.)?

            A poster typically advertises an event with: event title, date/time, venue, and promotional design.
            Not a poster: regular photos, screenshots, product images, logos, documents, memes.

            Respond with JSON only (no markdown, no explanation):
            {
              "is_poster": true,
              "event_title": "exact event title as shown in the image (preserve original language)",
              "year":  "4-digit year, e.g. 2025. Empty string if not found.",
              "month": "2-digit month with leading zero, e.g. 06. Empty string if not found.",
              "day":   "2-digit day with leading zero, e.g. 15. Empty string if not found."
            }

            If NOT a poster:
            {"is_poster": false, "event_title": "", "year": "", "month": "", "day": ""}
            """;

        var result = await claude.SendImageAsync(apiKey, model, system, userPrompt, base64, mediaType);
        var jsonStr = ClaudeService.ExtractJson(result.Text);
        if (jsonStr == null) return null;

        try
        {
            var node = JsonNode.Parse(jsonStr)!;
            var isPoster = node["is_poster"]?.GetValue<bool>() ?? false;
            if (!isPoster)
                return new ImageAnalysisResult(false, "", "", "", "", result.InputTokens, result.OutputTokens, model);

            var title = node["event_title"]?.GetValue<string>() ?? "";
            var year = node["year"]?.GetValue<string>() ?? "";
            var month = node["month"]?.GetValue<string>() ?? "";
            var day = node["day"]?.GetValue<string>() ?? "";
            return new ImageAnalysisResult(true, title, year, month, day, result.InputTokens, result.OutputTokens, model);
        }
        catch { return null; }
    }
}
