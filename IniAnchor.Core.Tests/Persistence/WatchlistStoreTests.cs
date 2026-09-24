using IniAnchor.Core.Models;
using IniAnchor.Core.Persistence;
using Xunit;

namespace IniAnchor.Core.Tests.Persistence;

public class WatchlistStoreTests : IDisposable
{
    private readonly string _tempFilePath;

    public WatchlistStoreTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"IniAnchorTests_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);
    }

    [Fact]
    public void Load_when_file_does_not_exist_returns_empty_watchlist()
    {
        var store = new WatchlistStore(_tempFilePath);

        var result = store.Load();

        Assert.Empty(result.Folders);
        Assert.Empty(result.Files);
    }

    [Fact]
    public void Save_then_load_round_trips_watched_files_and_keys()
    {
        var store = new WatchlistStore(_tempFilePath);
        var original = new Watchlist
        {
            Files =
            {
                new()
                {
                    FilePath = @"C:\configs\app.ini",
                    WatchedKeys = new List<WatchedKey>
                    {
                        new()
                        {
                            Section = "General",
                            KeyName = "Name",
                            DesiredValue = "Ada",
                            OriginalValue = "Original"
                        },
                        new()
                        {
                            Section = null,
                            KeyName = "root_key",
                            DesiredValue = "root_value"
                        }
                    }
                }
            }
        };

        store.Save(original);
        var loaded = store.Load();

        var loadedFile = Assert.Single(loaded.Files);
        Assert.Equal(original.Files[0].Id, loadedFile.Id);
        Assert.Equal(original.Files[0].FilePath, loadedFile.FilePath);
        Assert.Null(loadedFile.FolderId);
        Assert.Equal(2, loadedFile.WatchedKeys.Count);
        Assert.Equal("Ada", loadedFile.WatchedKeys[0].DesiredValue);
        Assert.Equal("Original", loadedFile.WatchedKeys[0].OriginalValue);
        Assert.Null(loadedFile.WatchedKeys[1].Section);
    }

    [Fact]
    public void Save_then_load_round_trips_folders_and_folder_membership()
    {
        var store = new WatchlistStore(_tempFilePath);
        var folder = new WatchFolder { Name = "Profile A" };
        var original = new Watchlist
        {
            Folders = { folder },
            Files = { new WatchedFile { FilePath = @"C:\configs\app.ini", FolderId = folder.Id } }
        };

        store.Save(original);
        var loaded = store.Load();

        var loadedFolder = Assert.Single(loaded.Folders);
        Assert.Equal(folder.Id, loadedFolder.Id);
        Assert.Equal("Profile A", loadedFolder.Name);
        Assert.Equal(folder.Id, Assert.Single(loaded.Files).FolderId);
    }

    [Fact]
    public void Load_reads_old_pre_folders_format_as_entries_without_a_folder()
    {
        // The format before folders existed: a plain JSON array of file entries.
        File.WriteAllText(_tempFilePath,
            """
            [
              {
                "FilePath": "C:\\configs\\app.ini",
                "WatchedKeys": [ { "Section": "General", "KeyName": "Name", "DesiredValue": "Ada" } ]
              }
            ]
            """);
        var store = new WatchlistStore(_tempFilePath);

        var loaded = store.Load();

        Assert.Empty(loaded.Folders);
        var file = Assert.Single(loaded.Files);
        Assert.Equal(@"C:\configs\app.ini", file.FilePath);
        Assert.Null(file.FolderId);
        Assert.Equal("Ada", Assert.Single(file.WatchedKeys).DesiredValue);
    }

    [Fact]
    public void Old_format_is_saved_back_in_the_new_format()
    {
        File.WriteAllText(_tempFilePath, """[ { "FilePath": "C:\\configs\\app.ini" } ]""");
        var store = new WatchlistStore(_tempFilePath);

        store.Save(store.Load());

        Assert.StartsWith("{", File.ReadAllText(_tempFilePath).TrimStart());
        Assert.Single(store.Load().Files);
    }

    [Fact]
    public void Load_ignores_removed_fields_from_older_files()
    {
        // DisplayName, LastAppliedValue and LastAppliedAtUtc were removed from the model;
        // files saved before that must still load.
        File.WriteAllText(_tempFilePath,
            """
            {
              "Folders": [],
              "Files": [
                {
                  "FilePath": "C:\\configs\\app.ini",
                  "DisplayName": "My App",
                  "WatchedKeys": [
                    { "KeyName": "Name", "DesiredValue": "Ada", "LastAppliedValue": "Ada", "LastAppliedAtUtc": "2024-01-01T12:00:00Z" }
                  ]
                }
              ]
            }
            """);
        var store = new WatchlistStore(_tempFilePath);

        var loaded = store.Load();

        Assert.Equal("Ada", Assert.Single(Assert.Single(loaded.Files).WatchedKeys).DesiredValue);
    }

    [Fact]
    public void Save_creates_missing_parent_directory()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), $"IniAnchorTests_{Guid.NewGuid():N}", "watchlist.json");
        var store = new WatchlistStore(nestedPath);

        try
        {
            store.Save(new Watchlist());

            Assert.True(File.Exists(nestedPath));
        }
        finally
        {
            var dir = Path.GetDirectoryName(nestedPath)!;
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Default_constructor_points_next_to_the_running_executable()
    {
        var store = new WatchlistStore();

        // No public path getter is needed by the app, but we can confirm the portable
        // behavior indirectly: saving with the default constructor must not throw and
        // must land in AppContext.BaseDirectory, not %LOCALAPPDATA%.
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "watchlist.json");

        store.Save(new Watchlist());
        try
        {
            Assert.True(File.Exists(expectedPath));
        }
        finally
        {
            if (File.Exists(expectedPath))
                File.Delete(expectedPath);
        }
    }
}
