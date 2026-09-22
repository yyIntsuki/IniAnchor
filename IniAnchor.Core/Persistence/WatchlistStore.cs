using System.Text.Json;
using IniAnchor.Core.Models;

namespace IniAnchor.Core.Persistence;

/// <summary>
/// Loads/saves the watch list as JSON. Portable by default: the file lives next to
/// the running executable (AppContext.BaseDirectory), not in %LOCALAPPDATA% or the
/// registry, so the whole app folder can be copied/moved/run from a USB stick.
/// </summary>
public class WatchlistStore
{
    private const string DefaultFileName = "watchlist.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    /// <summary>
    /// filePath is optional and mainly for tests - normally you'd just use the parameterless constructor.
    /// </summary>
    public WatchlistStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(AppContext.BaseDirectory, DefaultFileName);
    }

    public List<WatchedFile> Load()
    {
        if (!File.Exists(_filePath))
            return new List<WatchedFile>();

        var json = File.ReadAllText(_filePath);

        if (string.IsNullOrWhiteSpace(json))
            return new List<WatchedFile>();

        return JsonSerializer.Deserialize<List<WatchedFile>>(json, SerializerOptions) ?? new List<WatchedFile>();
    }

    public void Save(List<WatchedFile> watchedFiles)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(watchedFiles, SerializerOptions);
        File.WriteAllText(_filePath, json);
    }
}
