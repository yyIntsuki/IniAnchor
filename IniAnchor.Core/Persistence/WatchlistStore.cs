using System.Text.Json;
using IniAnchor.Core.Models;

namespace IniAnchor.Core.Persistence;

/// <summary>
/// Loads/saves the watch list (<see cref="Watchlist"/>: folders + file entries) as JSON.
/// Portable by default: the file lives next to the running executable
/// (AppContext.BaseDirectory), not in %LOCALAPPDATA% or the registry, so the whole app
/// folder can be copied/moved/run from a USB stick.
/// </summary>
public class WatchlistStore
{
    private const string DefaultFileName = "watchlist.json";

    private readonly string _filePath;

    /// <summary>
    /// filePath is optional and mainly for tests - normally you'd just use the parameterless constructor.
    /// </summary>
    public WatchlistStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    public Watchlist Load()
    {
        if (!File.Exists(_filePath))
            return new Watchlist();

        var json = File.ReadAllText(_filePath);

        if (string.IsNullOrWhiteSpace(json))
            return new Watchlist();

        // Old format (before folders): a plain JSON array of file entries. Load it as
        // entries without a folder; the next Save writes the new format.
        if (json.TrimStart().StartsWith('['))
        {
            var files = JsonSerializer.Deserialize(json, WatchlistJsonContext.Default.ListWatchedFile);
            return new Watchlist { Files = files ?? new List<WatchedFile>() };
        }

        return JsonSerializer.Deserialize(json, WatchlistJsonContext.Default.Watchlist) ?? new Watchlist();
    }

    public void Save(Watchlist watchlist)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(watchlist, WatchlistJsonContext.Default.Watchlist);
        File.WriteAllText(_filePath, json);
    }
}
