namespace Renamer.Models;

public class StatEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool WasRenamed { get; set; }
    public double Cost { get; set; }
}
