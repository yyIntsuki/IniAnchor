namespace IniAnchor.Core.Models;

/// <summary>
/// An ini file the user has added to their watch list, plus the keys they care about in it.
/// </summary>
public class WatchedFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FilePath { get; set; } = string.Empty;

    public List<WatchedKey> WatchedKeys { get; set; } = new();

    /// <summary>
    /// The <see cref="WatchFolder"/> this entry belongs to, or null for an entry without a
    /// folder (added while "All" was selected - only shows in "All").
    /// </summary>
    public Guid? FolderId { get; set; }
}
