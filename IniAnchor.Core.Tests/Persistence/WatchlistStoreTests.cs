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
    public void Load_when_file_does_not_exist_returns_empty_list()
    {
        var store = new WatchlistStore(_tempFilePath);

        var result = store.Load();

        Assert.Empty(result);
    }

    [Fact]
    public void Save_then_load_round_trips_watched_files_and_keys()
    {
        var store = new WatchlistStore(_tempFilePath);
        var original = new List<WatchedFile>
        {
            new()
            {
                FilePath = @"C:\configs\app.ini",
                DisplayName = "My App",
                WatchedKeys = new List<WatchedKey>
                {
                    new()
                    {
                        Section = "General",
                        KeyName = "Name",
                        DesiredValue = "Ada",
                        LastAppliedValue = "OldAda",
                        LastAppliedAtUtc = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
                    },
                    new()
                    {
                        Section = null,
                        KeyName = "root_key",
                        DesiredValue = "root_value"
                    }
                }
            }
        };

        store.Save(original);
        var loaded = store.Load();

        var loadedFile = Assert.Single(loaded);
        Assert.Equal(original[0].Id, loadedFile.Id);
        Assert.Equal(original[0].FilePath, loadedFile.FilePath);
        Assert.Equal(original[0].DisplayName, loadedFile.DisplayName);
        Assert.Equal(2, loadedFile.WatchedKeys.Count);
        Assert.Equal("Ada", loadedFile.WatchedKeys[0].DesiredValue);
        Assert.Equal("OldAda", loadedFile.WatchedKeys[0].LastAppliedValue);
        Assert.Null(loadedFile.WatchedKeys[1].Section);
    }

    [Fact]
    public void Save_creates_missing_parent_directory()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), $"IniAnchorTests_{Guid.NewGuid():N}", "watchlist.json");
        var store = new WatchlistStore(nestedPath);

        try
        {
            store.Save(new List<WatchedFile>());

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

        store.Save(new List<WatchedFile>());
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
