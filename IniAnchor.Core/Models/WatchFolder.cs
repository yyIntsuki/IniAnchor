namespace IniAnchor.Core.Models;

/// <summary>
/// A user-created folder in the sidebar. Works like a profile: each folder has its own
/// file entries (<see cref="WatchedFile.FolderId"/>), each with its own keys and desired
/// values - so the same ini file can be in several folders with different values.
/// The built-in "All" view is not a WatchFolder; it's simply every file entry.
/// </summary>
public class WatchFolder
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;
}
