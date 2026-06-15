using System.IO;
using System.Text.Json;
using Renamer.Models;

namespace Renamer.Services;

public class StatsService
{
    private static readonly string StatsPath = Path.Combine(SettingsService.AppDataPath, "stats_log.json");
    private List<StatEntry> _entries = new();

    public void Load()
    {
        try
        {
            if (File.Exists(StatsPath))
            {
                var json = File.ReadAllText(StatsPath);
                _entries = JsonSerializer.Deserialize<List<StatEntry>>(json) ?? new();
            }
        }
        catch { }
        Cleanup();
    }

    public void Record(bool wasRenamed, double cost)
    {
        _entries.Add(new StatEntry { WasRenamed = wasRenamed, Cost = cost });
        Cleanup();
        Save();
    }

    public (int analyzed, int renamed, double cost) GetSince(DateTime since)
    {
        var filtered = _entries.Where(e => e.Timestamp >= since).ToList();
        return (filtered.Count, filtered.Count(e => e.WasRenamed), filtered.Sum(e => e.Cost));
    }

    public (int analyzed, int renamed, double cost) GetLast30Days()
        => GetSince(DateTime.Today.AddDays(-30));

    private void Cleanup()
    {
        var cutoff = DateTime.Today.AddDays(-60);
        _entries = _entries.Where(e => e.Timestamp >= cutoff).ToList();
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.AppDataPath);
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StatsPath, json);
        }
        catch { }
    }
}
