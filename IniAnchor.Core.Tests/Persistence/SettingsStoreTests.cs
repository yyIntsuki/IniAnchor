using IniAnchor.Core.Models;
using IniAnchor.Core.Persistence;
using Xunit;

namespace IniAnchor.Core.Tests.Persistence;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempFilePath;

    public SettingsStoreTests()
    {
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"IniAnchorTests_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFilePath))
            File.Delete(_tempFilePath);
    }

    [Fact]
    public void Load_when_file_does_not_exist_returns_defaults()
    {
        var store = new SettingsStore(_tempFilePath);

        var settings = store.Load();

        Assert.Null(settings.WindowWidth);
        Assert.Null(settings.WindowHeight);
    }

    [Fact]
    public void Save_then_load_round_trips_window_size()
    {
        var store = new SettingsStore(_tempFilePath);

        var saved = store.Save(new AppSettings { WindowWidth = 1200, WindowHeight = 800 });
        var loaded = store.Load();

        Assert.True(saved);
        Assert.Equal(1200, loaded.WindowWidth);
        Assert.Equal(800, loaded.WindowHeight);
    }

    [Fact]
    public void Load_when_file_is_corrupt_returns_defaults_instead_of_throwing()
    {
        File.WriteAllText(_tempFilePath, "this is { not json");
        var store = new SettingsStore(_tempFilePath);

        var settings = store.Load();

        Assert.Null(settings.WindowWidth);
        Assert.Null(settings.WindowHeight);
    }

    [Fact]
    public void Load_when_file_is_empty_returns_defaults()
    {
        File.WriteAllText(_tempFilePath, "");
        var store = new SettingsStore(_tempFilePath);

        var settings = store.Load();

        Assert.Null(settings.WindowWidth);
    }

    [Fact]
    public void Save_creates_missing_parent_directory()
    {
        var nestedPath = Path.Combine(Path.GetTempPath(), $"IniAnchorTests_{Guid.NewGuid():N}", "settings.json");
        var store = new SettingsStore(nestedPath);

        try
        {
            Assert.True(store.Save(new AppSettings { WindowWidth = 640, WindowHeight = 480 }));
            Assert.True(File.Exists(nestedPath));
        }
        finally
        {
            var dir = Path.GetDirectoryName(nestedPath)!;
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
