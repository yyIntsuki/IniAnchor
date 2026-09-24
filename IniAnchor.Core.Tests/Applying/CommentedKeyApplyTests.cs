using IniAnchor.Core.Applying;
using IniAnchor.Core.Models;
using Xunit;

namespace IniAnchor.Core.Tests.Applying;

/// <summary>Apply with keys turned off (commented out) and back on.</summary>
public class CommentedKeyApplyTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory();

    public void Dispose() => _tempDir.Delete(recursive: true);

    private string WriteFile(string contents)
    {
        var path = Path.Combine(_tempDir.FullName, $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }

    private static WatchedFile Entry(string path, WatchedKey key) => new() { FilePath = path, WatchedKeys = { key } };

    private static WatchedKey Launch(bool commentedOut, string value = "game.exe") =>
        new() { Section = "Loader", KeyName = "launch", DesiredValue = value, CommentedOut = commentedOut };

    [Fact]
    public void Turning_a_key_off_comments_its_line_out()
    {
        var path = WriteFile("[Loader]\r\nlaunch = game.exe\r\n");

        var result = ApplyRunner.ApplyToFile(Entry(path, Launch(commentedOut: true)));

        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(result.KeyResults).Outcome);
        Assert.Equal("[Loader]\r\n;launch = game.exe\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Turning_a_key_on_uncomments_its_line_and_sets_the_value()
    {
        var path = WriteFile("[Loader]\r\n; launch = game.exe\r\n");

        var result = ApplyRunner.ApplyToFile(Entry(path, Launch(commentedOut: false, value: "other.exe")));

        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(result.KeyResults).Outcome);
        Assert.Equal("[Loader]\r\nlaunch = other.exe\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Off_key_already_commented_out_is_AlreadySet_even_with_a_different_value()
    {
        var contents = "[Loader]\r\n;launch = something-else.exe\r\n";
        var path = WriteFile(contents);
        var oldTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, oldTimestamp);

        var result = ApplyRunner.ApplyToFile(Entry(path, Launch(commentedOut: true)));

        Assert.Equal(ApplyKeyOutcome.AlreadySet, Assert.Single(result.KeyResults).Outcome);
        Assert.Equal(contents, File.ReadAllText(path)); // the comment's value is left alone
        Assert.Equal(oldTimestamp, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void Off_key_that_something_uncommented_is_commented_out_again()
    {
        var path = WriteFile("[Loader]\r\nlaunch = game.exe\r\n");
        var entry = Entry(path, Launch(commentedOut: true));
        ApplyRunner.ApplyToFile(entry);

        File.WriteAllText(path, "[Loader]\r\nlaunch = game.exe\r\n"); // another program uncomments it
        var result = ApplyRunner.ApplyToFile(entry);

        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(result.KeyResults).Outcome);
        Assert.Equal("[Loader]\r\n;launch = game.exe\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Commented_example_next_to_the_real_line_leaves_the_example_alone()
    {
        var path = WriteFile("[Loader]\r\n; launch = example.exe\r\nlaunch = game.exe\r\n");

        ApplyRunner.ApplyToFile(Entry(path, Launch(commentedOut: false, value: "other.exe")));

        Assert.Equal("[Loader]\r\n; launch = example.exe\r\nlaunch = other.exe\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Turning_off_next_to_a_commented_example_is_Duplicate_on_the_following_Apply()
    {
        // Known limitation: after commenting out the real line there are two commented
        // "launch" lines and no active one, so the app can't tell which is ours and
        // refuses to guess.
        var path = WriteFile("[Loader]\r\n; launch = example.exe\r\nlaunch = game.exe\r\n");
        var entry = Entry(path, Launch(commentedOut: true));

        var first = ApplyRunner.ApplyToFile(entry);
        var second = ApplyRunner.ApplyToFile(entry);

        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(first.KeyResults).Outcome);
        Assert.Equal(ApplyKeyOutcome.DuplicateMatches, Assert.Single(second.KeyResults).Outcome);
        Assert.Equal("[Loader]\r\n; launch = example.exe\r\n;launch = game.exe\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Same_file_in_two_folders_on_versus_off_the_later_entry_wins()
    {
        var path = WriteFile("[Loader]\r\nlaunch = game.exe\r\n");
        var upper = Entry(path, Launch(commentedOut: false));
        var lower = Entry(path, Launch(commentedOut: true));

        var results = ApplyRunner.ApplyToAll(new[] { upper, lower });

        Assert.Equal(ApplyKeyOutcome.Overridden, Assert.Single(results[0].KeyResults).Outcome);
        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(results[1].KeyResults).Outcome);
        Assert.Equal("[Loader]\r\n;launch = game.exe\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Same_file_in_two_folders_both_off_with_different_values_is_not_Overridden()
    {
        var path = WriteFile("[Loader]\r\nlaunch = game.exe\r\n");
        var upper = Entry(path, Launch(commentedOut: true, value: "a.exe"));
        var lower = Entry(path, Launch(commentedOut: true, value: "b.exe"));

        var results = ApplyRunner.ApplyToAll(new[] { upper, lower });

        Assert.All(results, r => Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(r.KeyResults).Outcome));
    }
}
