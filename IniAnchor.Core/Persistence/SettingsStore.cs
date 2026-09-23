using System.Text.Json;
using IniAnchor.Core.Models;

namespace IniAnchor.Core.Persistence;

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON (settings.json). Same portable approach
/// as <see cref="WatchlistStore"/>: the app passes a path next to the .exe (§3.5).
/// Unlike the watch list, a missing/unreadable/corrupt settings file is never an error -
/// it just means "use defaults".
/// </summary>
public class SettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Load()
    {
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize(json, WatchlistJsonContext.Default.AppSettings) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }
    }

    /// <summary>Best effort: returns false instead of throwing (e.g. read-only USB stick).</summary>
    public bool Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, WatchlistJsonContext.Default.AppSettings));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
