using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Renamer.Services;

public record ClaudeResult(string Text, int InputTokens, int OutputTokens, string Model);

public class ClaudeService
{
    private static readonly HttpClient Http = new();

    public async Task<ClaudeResult> SendTextAsync(string apiKey, string model, string system, string userPrompt)
    {
        var body = new
        {
            model,
            max_tokens = 1024,
            system,
            messages = new[] { new { role = "user", content = userPrompt } }
        };
        return await PostAsync(apiKey, model, body);
    }

    public async Task<ClaudeResult> SendImageAsync(string apiKey, string model, string system,
        string userPrompt, string base64Data, string mediaType)
    {
        var body = new
        {
            model,
            max_tokens = 1024,
            system,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "image", source = new { type = "base64", media_type = mediaType, data = base64Data } },
                        new { type = "text", text = userPrompt }
                    }
                }
            }
        };
        return await PostAsync(apiKey, model, body);
    }

    private async Task<ClaudeResult> PostAsync(string apiKey, string model, object body)
    {
        var json = JsonSerializer.Serialize(body);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(responseJson)!;
        var text = node["content"]![0]!["text"]!.GetValue<string>();
        var inputTokens = node["usage"]!["input_tokens"]!.GetValue<int>();
        var outputTokens = node["usage"]!["output_tokens"]!.GetValue<int>();
        return new ClaudeResult(text, inputTokens, outputTokens, model);
    }

    public static double CalcCost(string model, int inputTokens, int outputTokens)
    {
        var (inRate, outRate) = model.Contains("haiku") ? (1.0, 5.0) : (3.0, 15.0);
        return inputTokens / 1_000_000.0 * inRate + outputTokens / 1_000_000.0 * outRate;
    }

    public static string? ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : null;
    }

    public async Task<bool> ValidateKeyAsync(string apiKey)
    {
        try
        {
            var body = new
            {
                model = "claude-haiku-4-5-20251001",
                max_tokens = 1,
                messages = new[] { new { role = "user", content = "hi" } }
            };
            var json = JsonSerializer.Serialize(body);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
            request.Headers.Add("x-api-key", apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await Http.SendAsync(request);
            return response.IsSuccessStatusCode || (int)response.StatusCode == 529;
        }
        catch
        {
            return false;
        }
    }
}
