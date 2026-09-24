using IniAnchor.Core.Models;
using Xunit;

namespace IniAnchor.Core.Tests.Models;

public class WatchlistTests
{
    private static WatchedFile Entry(string path, Guid? folderId, params (string Key, string Value)[] keys) => new()
    {
        FilePath = path,
        FolderId = folderId,
        WatchedKeys = keys.Select(k => new WatchedKey { Section = "General", KeyName = k.Key, DesiredValue = k.Value }).ToList()
    };

    [Fact]
    public void DeleteFolder_removes_the_folder_and_only_its_own_entries()
    {
        var folderA = new WatchFolder { Name = "A" };
        var folderB = new WatchFolder { Name = "B" };
        var inA = Entry(@"C:\a.ini", folderA.Id);
        var inB = Entry(@"C:\a.ini", folderB.Id); // same file, other folder - must survive
        var noFolder = Entry(@"C:\b.ini", null);
        var watchlist = new Watchlist { Folders = { folderA, folderB }, Files = { inA, inB, noFolder } };

        watchlist.DeleteFolder(folderA.Id);

        Assert.Equal(new[] { folderB }, watchlist.Folders);
        Assert.Equal(new[] { inB, noFolder }, watchlist.Files);
    }

    [Fact]
    public void FilesInApplyOrder_puts_entries_without_folder_first_then_folders_in_sidebar_order()
    {
        var folderA = new WatchFolder { Name = "A" };
        var folderB = new WatchFolder { Name = "B" };
        var inB = Entry(@"C:\1.ini", folderB.Id);
        var inA1 = Entry(@"C:\2.ini", folderA.Id);
        var noFolder = Entry(@"C:\3.ini", null);
        var inA2 = Entry(@"C:\4.ini", folderA.Id);
        var watchlist = new Watchlist { Folders = { folderA, folderB }, Files = { inB, inA1, noFolder, inA2 } };

        var ordered = watchlist.FilesInApplyOrder();

        Assert.Equal(new[] { noFolder, inA1, inA2, inB }, ordered);
    }

    [Fact]
    public void FilesInApplyOrder_treats_entry_of_missing_folder_as_having_no_folder()
    {
        var orphan = Entry(@"C:\1.ini", Guid.NewGuid());
        var folder = new WatchFolder { Name = "A" };
        var inFolder = Entry(@"C:\2.ini", folder.Id);
        var watchlist = new Watchlist { Folders = { folder }, Files = { inFolder, orphan } };

        Assert.Equal(new[] { orphan, inFolder }, watchlist.FilesInApplyOrder());
    }

    [Fact]
    public void CountConflicts_counts_same_key_with_different_values_across_entries()
    {
        var files = new[]
        {
            Entry(@"C:\game.ini", Guid.NewGuid(), ("Quality", "High"), ("Fps", "60")),
            Entry(@"C:\GAME.INI", Guid.NewGuid(), ("quality", "Low"), ("Fps", "60")) // path/key case differs
        };

        // Quality conflicts (High vs Low); Fps agrees (60 both) so isn't a conflict.
        Assert.Equal(1, Watchlist.CountConflicts(files));
    }

    [Fact]
    public void CountConflicts_is_zero_for_different_files_or_matching_values()
    {
        var files = new[]
        {
            Entry(@"C:\one.ini", null, ("Quality", "High")),
            Entry(@"C:\two.ini", null, ("Quality", "Low")),            // different file - no conflict
            Entry(@"C:\one.ini", Guid.NewGuid(), ("Quality", "High"))  // same value - no conflict
        };

        Assert.Equal(0, Watchlist.CountConflicts(files));
    }

    [Fact]
    public void CountConflicts_counts_on_versus_off_but_not_off_versus_off()
    {
        WatchedFile WithKey(bool commentedOut, string value) => new()
        {
            FilePath = @"C:\game.ini",
            WatchedKeys = { new WatchedKey { Section = "Loader", KeyName = "launch", DesiredValue = value, CommentedOut = commentedOut } }
        };

        // On vs off: conflict, even with the same value.
        Assert.Equal(1, Watchlist.CountConflicts(new[] { WithKey(false, "a"), WithKey(true, "a") }));

        // Both off: no conflict - an off key's value isn't written.
        Assert.Equal(0, Watchlist.CountConflicts(new[] { WithKey(true, "a"), WithKey(true, "b") }));
    }
}
