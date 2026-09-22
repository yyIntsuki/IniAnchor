using IniAnchor.Core.Parsing;
using Xunit;

namespace IniAnchor.Core.Tests.Parsing;

public class KeyLookupResultTests
{
    private const string SampleIni =
        "root_key=root_value\r\n" +
        "[General]\r\n" +
        "Name = Ada\r\n" +
        "Name = Duplicate\r\n" +
        "[Advanced]\r\n" +
        "Name = Advanced Ada\r\n";

    [Fact]
    public void Unique_key_reports_UniqueMatch_with_one_line_index()
    {
        var doc = IniParser.Parse(SampleIni);

        var result = doc.LookupKey("Advanced", "Name");

        Assert.Equal(KeyLookupStatus.UniqueMatch, result.Status);
        Assert.Single(result.MatchingLineIndices);
    }

    [Fact]
    public void Duplicate_key_in_same_section_reports_DuplicateMatches()
    {
        var doc = IniParser.Parse(SampleIni);

        var result = doc.LookupKey("General", "Name");

        Assert.Equal(KeyLookupStatus.DuplicateMatches, result.Status);
        Assert.Equal(2, result.MatchingLineIndices.Count);
    }

    [Fact]
    public void Missing_key_reports_NotFound()
    {
        var doc = IniParser.Parse(SampleIni);

        var result = doc.LookupKey("General", "DoesNotExist");

        Assert.Equal(KeyLookupStatus.NotFound, result.Status);
        Assert.Empty(result.MatchingLineIndices);
    }

    [Fact]
    public void Key_lookup_is_case_insensitive_on_section_and_key()
    {
        var doc = IniParser.Parse(SampleIni);

        var result = doc.LookupKey("advanced", "name");

        Assert.Equal(KeyLookupStatus.UniqueMatch, result.Status);
    }

    [Fact]
    public void Repeated_lookups_reuse_the_same_cached_index_and_stay_correct()
    {
        var doc = IniParser.Parse(SampleIni);

        // Call twice to exercise the lazy-build-once path, then mutate and look up again
        // to confirm the cached index still points at the right (now-changed) line.
        _ = doc.LookupKey("Advanced", "Name");
        var result = doc.LookupKey("Advanced", "Name");
        doc.SetValueAtLine(result.MatchingLineIndices[0], "Changed");

        var afterChange = doc.LookupKey("Advanced", "Name");

        Assert.Equal(KeyLookupStatus.UniqueMatch, afterChange.Status);
        Assert.Equal("Changed", doc.Lines[afterChange.MatchingLineIndices[0]].Value);
    }
}
