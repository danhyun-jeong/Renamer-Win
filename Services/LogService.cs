using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using Renamer.Models;

namespace Renamer.Services;

public class LogService
{
    private static readonly string LogPath = Path.Combine(SettingsService.AppDataPath, "activity_log.json");

    public ObservableCollection<ActivityEntry> Entries { get; } = new();
    private int _maxCount = 100;

    public void SetMaxCount(int max) => _maxCount = max;

    public void Load()
    {
        try
        {
            if (File.Exists(LogPath))
            {
                var json = File.ReadAllText(LogPath);
                var entries = JsonSerializer.Deserialize<List<ActivityEntry>>(json) ?? new();
                Entries.Clear();
                foreach (var e in entries) Entries.Add(e);
            }
        }
        catch { }
    }

    public void Add(ActivityEntry entry)
    {
        Entries.Insert(0, entry);
        while (Entries.Count > _maxCount)
            Entries.RemoveAt(Entries.Count - 1);
        SaveAsync();
    }

    public void Clear()
    {
        Entries.Clear();
        SaveAsync();
    }

    private void SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(SettingsService.AppDataPath);
            var json = JsonSerializer.Serialize(Entries.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(LogPath, json);
        }
        catch { }
    }
}
