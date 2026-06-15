namespace Renamer.Models;

public class ActivityEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Message { get; set; } = "";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public string? Model { get; set; }
    public double? Cost { get; set; }
}
