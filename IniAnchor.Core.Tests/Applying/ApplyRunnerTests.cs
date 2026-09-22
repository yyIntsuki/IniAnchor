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
    public void Applying_sets_LastAppliedValue_and_LastAppliedAtUtc()
    {
        var path = WriteSampleFile(SampleIni);
        var key = new WatchedKey { Section = "General", KeyName = "Name", DesiredValue = "Grace" };
        var file = new WatchedFile { FilePath = path, WatchedKeys = { key } };
        var fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        ApplyRunner.ApplyToFile(file, fixedTime);

        Assert.Equal("Grace", key.LastAppliedValue);
        Assert.Equal(fixedTime, key.LastAppliedAtUtc);
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
        Assert.Null(key.LastAppliedValue);
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
}
