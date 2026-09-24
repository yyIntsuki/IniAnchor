namespace IniAnchor.Core.Models;

/// <summary>
/// Everything saved in watchlist.json: the user's folders (in sidebar order) and every
/// file entry. A file entry belongs to one folder via <see cref="WatchedFile.FolderId"/>,
/// or to none (null).
/// </summary>
public class Watchlist
{
    public List<WatchFolder> Folders { get; set; } = new();

    public List<WatchedFile> Files { get; set; } = new();

    /// <summary>Deletes a folder together with all of its file entries (and their keys).</summary>
    public void DeleteFolder(Guid folderId)
    {
        Folders.RemoveAll(f => f.Id == folderId);
        Files.RemoveAll(f => f.FolderId == folderId);
    }

    /// <summary>
    /// The order "Apply all" writes in: entries without a folder first, then each folder
    /// top to bottom, keeping list order within each. When two entries set the same key to
    /// different values, the later one wins - i.e. the lower folder in the sidebar.
    /// Entries pointing at a folder that no longer exists are treated as having no folder.
    /// </summary>
    public List<WatchedFile> FilesInApplyOrder()
    {
        var folderRank = Folders
            .Select((folder, index) => (folder.Id, index))
            .ToDictionary(x => x.Id, x => x.index);

        int Rank(WatchedFile file) =>
            file.FolderId is Guid id && folderRank.TryGetValue(id, out var rank) ? rank : -1;

        // OrderBy is stable, so list order is kept within the same folder.
        return Files.OrderBy(Rank).ToList();
    }

    /// <summary>
    /// Counts keys that more than one entry would set to different values in the same file
    /// (only possible across folders). Used to warn before "Apply all". File paths, sections
    /// and key names compare case-insensitively (like lookups); values compare exactly.
    /// </summary>
    public static int CountConflicts(IEnumerable<WatchedFile> files) =>
        files
            .SelectMany(file => file.WatchedKeys.Select(key => (
                Path: file.FilePath.ToUpperInvariant(),
                Section: (key.Section ?? string.Empty).ToUpperInvariant(),
                Key: key.KeyName.ToUpperInvariant(),
                key.DesiredValue)))
            .GroupBy(x => (x.Path, x.Section, x.Key))
            .Count(group => group.Select(x => x.DesiredValue).Distinct().Count() > 1);
}
