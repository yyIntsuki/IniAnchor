using IniAnchor.Core.Applying;
using IniAnchor.Core.Models;
using Xunit;

namespace IniAnchor.Core.Tests.Applying;

public class ApplyRunnerTests : IDisposable
{
    private const string SampleIni =
        "[General]\r\n" +
        "Name = Ada\r\n" +
        "Enabled=true\r\n" +
        "\r\n" +
        "[Advanced]\r\n" +
        "Name = Advanced Ada\r\n";

    private const string DuplicateIni =
        "[General]\r\n" +
        "Name = First\r\n" +
        "Name = Second\r\n";

    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory();

    public void Dispose() => _tempDir.Delete(recursive: true);

    private string WriteSampleFile(string contents)
    {
        var path = Path.Combine(_tempDir.FullName, $"{Guid.NewGuid():N}.ini");
        File.WriteAllText(path, contents);
        return path;
    }

    [Fact]
    public void Unique_key_is_applied_and_written_to_disk()
    {
        var path = WriteSampleFile(SampleIni);
        var key = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "Grace" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { key } };

        var result = ApplyRunner.ApplyToFile(file);

        Assert.Null(result.FileErrorMessage);
        Assert.True(result.AnyApplied);
        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(result.KeyResults).Outcome);
        Assert.Contains("Name = Grace", File.ReadAllText(path));
        // Advanced.Name, same key name different section, must be untouched.
        Assert.Contains("Name = Advanced Ada", File.ReadAllText(path));
    }

    [Fact]
    public void Missing_key_is_reported_as_NotFound_and_nothing_is_written_for_it()
    {
        var path = WriteSampleFile(SampleIni);
        var key = new WatchedKey { Section = "General", KeyName = "DoesNotExist", DesiredValue = "x" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { key } };
        var originalContents = File.ReadAllText(path);

        var result = ApplyRunner.ApplyToFile(file);

        Assert.Equal(ApplyKeyOutcome.NotFound, Assert.Single(result.KeyResults).Outcome);
        Assert.False(result.AnyApplied);
        Assert.Equal(originalContents, File.ReadAllText(path)); // untouched - no write happened at all
    }

    [Fact]
    public void Duplicate_key_is_reported_as_DuplicateMatches_and_nothing_is_written_for_it()
    {
        var path = WriteSampleFile(DuplicateIni);
        var key = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "x" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { key } };
        var originalContents = File.ReadAllText(path);

        var result = ApplyRunner.ApplyToFile(file);

        Assert.Equal(ApplyKeyOutcome.DuplicateMatches, Assert.Single(result.KeyResults).Outcome);
        Assert.Equal(originalContents, File.ReadAllText(path));
    }

    [Fact]
    public void One_bad_key_does_not_block_a_good_key_in_the_same_file()
    {
        var path = WriteSampleFile(SampleIni);
        var goodKey = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "Grace" };
        var badKey = new WatchedKey { Section = "General", KeyName = "DoesNotExist", DesiredValue = "x" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { goodKey, badKey } };

        var result = ApplyRunner.ApplyToFile(file);

        Assert.Equal(2, result.KeyResults.Count);
        Assert.Contains(result.KeyResults, r => r.Key == goodKey && r.Outcome == ApplyKeyOutcome.Applied);
        Assert.Contains(result.KeyResults, r => r.Key == badKey && r.Outcome == ApplyKeyOutcome.NotFound);
        Assert.Contains("Name = Grace", File.ReadAllText(path));
    }

    [Fact]
    public void Nonexistent_file_reports_FileError_for_every_key_and_does_not_throw()
    {
        var path = Path.Combine(_tempDir.FullName, "does-not-exist.ini");
        var key = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "x" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { key } };

        var result = ApplyRunner.ApplyToFile(file);

        Assert.NotNull(result.FileErrorMessage);
        Assert.Equal(ApplyKeyOutcome.FileError, Assert.Single(result.KeyResults).Outcome);
    }

    [Fact]
    public void File_with_no_watched_keys_is_a_no_op()
    {
        var path = WriteSampleFile(SampleIni);
        var file = new WatchedFile { FilePath = path };

        var result = ApplyRunner.ApplyToFile(file);

        Assert.Empty(result.KeyResults);
        Assert.Null(result.FileErrorMessage);
        Assert.False(result.AnyApplied);
    }

    [Fact]
    public void ApplyToAll_processes_every_file_independently()
    {
        var path1 = WriteSampleFile(SampleIni);
        var path2 = WriteSampleFile(SampleIni);

        var file1 = new WatchedFile
        {
            FilePath = path1,
            WatchedKeys = { new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "One" } }
        };
        var file2 = new WatchedFile
        {
            FilePath = path2,
            WatchedKeys = { new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "Two" } }
        };

        var results = ApplyRunner.ApplyToAll(new[] { file1, file2 });

        Assert.Equal(2, results.Count);
        Assert.Contains("Name = One", File.ReadAllText(path1));
        Assert.Contains("Name = Two", File.ReadAllText(path2));
    }

    [Fact]
    public void Key_already_at_desired_value_is_AlreadySet_and_file_is_not_rewritten()
    {
        var path = WriteSampleFile(SampleIni);
        var oldTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, oldTimestamp);
        var key = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "Ada" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { key } };

        var result = ApplyRunner.ApplyToFile(file);

        Assert.Null(result.FileErrorMessage);
        Assert.False(result.AnyApplied);
        Assert.Equal(ApplyKeyOutcome.AlreadySet, Assert.Single(result.KeyResults).Outcome);
        Assert.Equal(oldTimestamp, File.GetLastWriteTimeUtc(path)); // no write happened at all
        Assert.Equal(SampleIni, File.ReadAllText(path));
    }

    [Fact]
    public void Read_only_file_that_is_already_correct_gives_no_FileError()
    {
        var path = WriteSampleFile(SampleIni);
        File.SetAttributes(path, FileAttributes.ReadOnly);
        var key = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "Ada" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { key } };

        try
        {
            var result = ApplyRunner.ApplyToFile(file);

            Assert.Null(result.FileErrorMessage);
            Assert.Equal(ApplyKeyOutcome.AlreadySet, Assert.Single(result.KeyResults).Outcome);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal); // so Dispose can delete it
        }
    }

    [Fact]
    public void Mix_of_changed_and_already_set_keys_writes_file_and_reports_each_correctly()
    {
        var path = WriteSampleFile(SampleIni);
        var changedKey = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "Grace" };
        var sameKey = new WatchedKey { Section = "General", KeyName = "Enabled", DesiredValue = "true" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { changedKey, sameKey } };

        var result = ApplyRunner.ApplyToFile(file);

        Assert.Contains(result.KeyResults, r => r.Key == changedKey && r.Outcome == ApplyKeyOutcome.Applied);
        Assert.Contains(result.KeyResults, r => r.Key == sameKey && r.Outcome == ApplyKeyOutcome.AlreadySet);
        var contents = File.ReadAllText(path);
        Assert.Contains("Name = Grace", contents);
        Assert.Contains("Enabled=true", contents);
    }

    // --- Several entries for the same file (same ini file in several folders) ---

    private static WatchedFile EntryFor(string path, params (string Key, string Value)[] keys) => new()
    {
        FilePath = path,
        WatchedKeys = keys.Select(k => new WatchedKey { Section = "General", KeyName = k.Key, DesiredValue = k.Value }).ToList()
    };

    [Fact]
    public void Same_file_conflicting_key_last_entry_wins_and_earlier_is_Overridden()
    {
        var path = WriteSampleFile(SampleIni);
        var upper = EntryFor(path, ("Name", "Upper"));
        var lower = EntryFor(path, ("Name", "Lower"));

        var results = ApplyRunner.ApplyToAll(new[] { upper, lower });

        Assert.Equal(ApplyKeyOutcome.Overridden, Assert.Single(results[0].KeyResults).Outcome);
        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(results[1].KeyResults).Outcome);
        Assert.Contains("Name = Lower", File.ReadAllText(path));
    }

    [Fact]
    public void Same_file_same_value_in_both_entries_is_not_Overridden()
    {
        var path = WriteSampleFile(SampleIni);
        var upper = EntryFor(path, ("Name", "Grace"));
        var lower = EntryFor(path, ("Name", "Grace"));

        var results = ApplyRunner.ApplyToAll(new[] { upper, lower });

        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(results[0].KeyResults).Outcome);
        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(results[1].KeyResults).Outcome);
        Assert.Contains("Name = Grace", File.ReadAllText(path));
    }

    [Fact]
    public void Same_file_conflict_where_winner_already_matches_leaves_file_untouched()
    {
        var path = WriteSampleFile(SampleIni);
        var oldTimestamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, oldTimestamp);
        var upper = EntryFor(path, ("Name", "Grace"));
        var lower = EntryFor(path, ("Name", "Ada")); // Ada is already in the file

        var results = ApplyRunner.ApplyToAll(new[] { upper, lower });

        Assert.Equal(ApplyKeyOutcome.Overridden, Assert.Single(results[0].KeyResults).Outcome);
        Assert.Equal(ApplyKeyOutcome.AlreadySet, Assert.Single(results[1].KeyResults).Outcome);
        Assert.Equal(oldTimestamp, File.GetLastWriteTimeUtc(path)); // the losing value never caused a write
        Assert.Equal(SampleIni, File.ReadAllText(path));
    }

    [Fact]
    public void Same_file_different_keys_from_two_entries_are_both_written()
    {
        var path = WriteSampleFile(SampleIni);
        var first = EntryFor(path, ("Name", "Grace"));
        var second = EntryFor(path, ("Enabled", "false"));

        var results = ApplyRunner.ApplyToAll(new[] { first, second });

        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(results[0].KeyResults).Outcome);
        Assert.Equal(ApplyKeyOutcome.Applied, Assert.Single(results[1].KeyResults).Outcome);
        var contents = File.ReadAllText(path);
        Assert.Contains("Name = Grace", contents);
        Assert.Contains("Enabled=false", contents);
    }

    [Fact]
    public void Same_file_that_cannot_be_read_gives_FileError_for_every_entry()
    {
        var path = Path.Combine(_tempDir.FullName, "does-not-exist.ini");
        var first = EntryFor(path, ("Name", "x"));
        var second = EntryFor(path, ("Enabled", "y"));

        var results = ApplyRunner.ApplyToAll(new[] { first, second });

        Assert.All(results, r =>
        {
            Assert.NotNull(r.FileErrorMessage);
            Assert.Equal(ApplyKeyOutcome.FileError, Assert.Single(r.KeyResults).Outcome);
        });
    }

    [Fact]
    public void ApplyToAll_returns_one_result_per_entry_in_input_order()
    {
        var pathA = WriteSampleFile(SampleIni);
        var pathB = WriteSampleFile(SampleIni);
        var a1 = EntryFor(pathA, ("Name", "A1"));
        var b = EntryFor(pathB, ("Name", "B"));
        var a2 = EntryFor(pathA, ("Enabled", "false"));

        var results = ApplyRunner.ApplyToAll(new[] { a1, b, a2 });

        Assert.Equal(new[] { a1, b, a2 }, results.Select(r => r.File));
    }
}
