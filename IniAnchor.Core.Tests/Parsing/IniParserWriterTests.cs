using IniAnchor.Core.Parsing;
using Xunit;

namespace IniAnchor.Core.Tests.Parsing;

public class IniParserWriterTests
{
    private const string SampleIni =
        "; leading comment\r\n" +
        "root_key=root_value\r\n" +
        "\r\n" +
        "[General]\r\n" +
        "Name = Ada\r\n" +
        "# hash comment style\r\n" +
        "ConnectionString=Server=localhost;Db=test\r\n" +
        "\r\n" +
        "[Advanced]\r\n" +
        "Name = Advanced Ada\r\n" +
        "Enabled=true\r\n";

    [Fact]
    public void Parse_then_write_with_no_changes_is_byte_identical()
    {
        var doc = IniParser.Parse(SampleIni);

        var result = IniWriter.WriteToString(doc);

        Assert.Equal(SampleIni, result);
    }

    [Fact]
    public void Parse_then_write_with_no_trailing_newline_preserves_that()
    {
        var textNoTrailingNewLine = SampleIni.TrimEnd('\r', '\n');

        var doc = IniParser.Parse(textNoTrailingNewLine);
        var result = IniWriter.WriteToString(doc);

        Assert.Equal(textNoTrailingNewLine, result);
    }

    [Fact]
    public void Changing_one_key_only_changes_that_line()
    {
        var doc = IniParser.Parse(SampleIni);

        var matches = doc.FindKeyOccurrences("General", "Name");
        Assert.Single(matches);
        doc.SetValueAtLine(matches[0], "Grace");

        var result = IniWriter.WriteToString(doc);
        var originalLines = SampleIni.Split("\r\n");
        var resultLines = result.Split("\r\n");

        Assert.Equal(originalLines.Length, resultLines.Length);
        for (var i = 0; i < originalLines.Length; i++)
        {
            if (i == matches[0])
            {
                Assert.Equal("Name = Grace", resultLines[i]);
            }
            else
            {
                Assert.Equal(originalLines[i], resultLines[i]);
            }
        }
    }

    [Fact]
    public void Value_containing_equals_sign_is_kept_intact()
    {
        var doc = IniParser.Parse(SampleIni);

        var matches = doc.FindKeyOccurrences("General", "ConnectionString");
        Assert.Single(matches);
        Assert.Equal("Server=localhost;Db=test", doc.Lines[matches[0]].Value);
    }

    [Fact]
    public void Key_with_no_section_is_found_with_null_section()
    {
        var doc = IniParser.Parse(SampleIni);

        var matches = doc.FindKeyOccurrences(null, "root_key");

        Assert.Single(matches);
        Assert.Equal("root_value", doc.Lines[matches[0]].Value);
    }

    [Fact]
    public void Same_key_in_different_sections_is_scoped_by_section()
    {
        var doc = IniParser.Parse(SampleIni);

        var generalMatches = doc.FindKeyOccurrences("General", "Name");
        var advancedMatches = doc.FindKeyOccurrences("Advanced", "Name");

        Assert.Single(generalMatches);
        Assert.Single(advancedMatches);
        Assert.NotEqual(generalMatches[0], advancedMatches[0]);
        Assert.Equal("Ada", doc.Lines[generalMatches[0]].Value);
        Assert.Equal("Advanced Ada", doc.Lines[advancedMatches[0]].Value);
    }

    [Fact]
    public void Missing_key_returns_no_matches()
    {
        var doc = IniParser.Parse(SampleIni);

        var matches = doc.FindKeyOccurrences("General", "DoesNotExist");

        Assert.Empty(matches);
    }

    [Fact]
    public void Duplicate_key_in_same_section_returns_multiple_matches()
    {
        var textWithDuplicate =
            "[General]\r\n" +
            "Name = First\r\n" +
            "Name = Second\r\n";

        var doc = IniParser.Parse(textWithDuplicate);

        var matches = doc.FindKeyOccurrences("General", "Name");

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void WriteToFile_creates_a_new_file_when_none_exists()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(tempDir.FullName, "new.ini");
            var doc = IniParser.Parse(SampleIni);

            IniWriter.WriteToFile(doc, path);

            Assert.Equal(SampleIni, File.ReadAllText(path));
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void WriteToFile_atomically_replaces_an_existing_file()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(tempDir.FullName, "existing.ini");
            File.WriteAllText(path, "old content that should be fully replaced");

            var doc = IniParser.Parse(SampleIni);
            IniWriter.WriteToFile(doc, path);

            Assert.Equal(SampleIni, File.ReadAllText(path));
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void WriteToFile_leaves_no_temp_file_behind()
    {
        var tempDir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(tempDir.FullName, "existing.ini");
            File.WriteAllText(path, "old content");

            IniWriter.WriteToFile(IniParser.Parse(SampleIni), path);

            // Only the real file should remain - no leftover "*.tmp" sibling.
            var filesInDir = Directory.GetFiles(tempDir.FullName);
            Assert.Single(filesInDir);
            Assert.Equal(path, filesInDir[0]);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }
}
