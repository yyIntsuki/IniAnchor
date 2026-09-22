namespace IniAnchor.Core.Models;

/// <summary>
/// An ini file the user has added to their watch list, plus the keys they care about in it.
/// </summary>
public class WatchedFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Optional friendly label shown in the UI instead of the raw path.
    /// </summary>
    public string? DisplayName { get; set; }

    public List<WatchedKey> WatchedKeys { get; set; } = new();
}
