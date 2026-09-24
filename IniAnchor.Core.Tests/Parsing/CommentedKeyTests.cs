using IniAnchor.Core.Parsing;
using Xunit;

namespace IniAnchor.Core.Tests.Parsing;

/// <summary>
/// Commented-out keys ("; key = value"): recognized only when the comment has exactly the
/// shape of a key line, found by lookups only when there's no active line, and switchable
/// between commented and active without touching anything else.
/// </summary>
public class CommentedKeyTests
{
    // Real-world style comments (from a 3DMigoto d3dx.ini) mixed with commented-out keys.
    private const string Sample =
        "[Include]\r\n" +                                                                              // 0
        "; include = Core\\Debugger\\Debugger.ini\r\n" +                                              // 1
        "include = Core\\GIMI\\main.ini\r\n" +                                                        // 2
        "\r\n" +                                                                                       // 3
        "[Rendering]\r\n" +                                                                            // 4
        "; Highlight mode of currently selected shader / rendertarget.\r\n" +                          // 5
        "; \"skip\" = skip shader. don't render anything using the currently selected shader.\r\n" +   // 6
        "; \"pink\" = make the output hot pink to make it standout.\r\n" +                             // 7
        "marking_mode = skip\r\n" +                                                                    // 8
        "; (e.g. \"dump = dump_tex dds share_dupes mono ps-t0\"), dump resources at a\r\n" +           // 9
        "; specific point in time (e.g. \"pre dump = o0\") or dump a custom resource that\r\n" +       // 10
        "; frame analysis cannot otherwise see (e.g. \"dump = ResourceDepthBuffer\"). Use\r\n" +       // 11
        "\r\n" +                                                                                       // 12
        "[Loader]\r\n" +                                                                               // 13
        ";launch = game.exe\r\n" +                                                                     // 14
        "#  disabled_key=1\r\n";                                                                       // 15

    [Fact]
    public void Only_comments_shaped_like_a_key_line_are_commented_keys()
    {
        var doc = IniParser.Parse(Sample);

        var commentedKeys = doc.Lines
            .Where(l => l.Kind == IniLineKind.CommentedKeyValue)
            .Select(l => l.Key);

        Assert.Equal(new[] { "include", "launch", "disabled_key" }, commentedKeys);
        Assert.All(new[] { 5, 6, 7, 9, 10, 11 }, i => Assert.Equal(IniLineKind.Comment, doc.Lines[i].Kind));
    }

    [Fact]
    public void File_with_commented_keys_round_trips_byte_identical()
    {
        Assert.Equal(Sample, IniWriter.WriteToString(IniParser.Parse(Sample)));
    }

    [Fact]
    public void Active_line_wins_over_a_commented_look_alike()
    {
        var doc = IniParser.Parse(Sample);

        var result = doc.LookupKey("Include", "include");

        Assert.Equal(KeyLookupStatus.UniqueMatch, result.Status);
        Assert.Equal(2, result.MatchingLineIndices[0]);
    }

    [Fact]
    public void Commented_line_is_found_when_there_is_no_active_line()
    {
        var doc = IniParser.Parse(Sample);

        var result = doc.LookupKey("Loader", "launch");

        Assert.Equal(KeyLookupStatus.UniqueMatch, result.Status);
        Assert.Equal(14, result.MatchingLineIndices[0]);
        Assert.Empty(doc.FindKeyOccurrences("Loader", "launch")); // active lines only
    }

    [Fact]
    public void Two_commented_lines_and_no_active_line_is_Duplicate()
    {
        var doc = IniParser.Parse("[Loader]\r\n; launch = a.exe\r\n;launch = b.exe\r\n");

        Assert.Equal(KeyLookupStatus.DuplicateMatches, doc.LookupKey("Loader", "launch").Status);
    }

    [Fact]
    public void Commenting_out_an_active_line_prefixes_it_with_a_semicolon()
    {
        var doc = IniParser.Parse(Sample);

        doc.SetCommentedOutAtLine(8, true);

        Assert.Equal(";marking_mode = skip", doc.Lines[8].ToRawText());
        Assert.Equal(IniLineKind.CommentedKeyValue, doc.Lines[8].Kind);
    }

    [Fact]
    public void Uncommenting_removes_the_comment_prefix()
    {
        var doc = IniParser.Parse(Sample);

        doc.SetCommentedOutAtLine(1, false);

        Assert.Equal("include = Core\\Debugger\\Debugger.ini", doc.Lines[1].ToRawText());
        Assert.Equal(IniLineKind.KeyValue, doc.Lines[1].Kind);
    }

    [Fact]
    public void Uncommenting_and_commenting_again_restores_the_original_line_exactly()
    {
        var doc = IniParser.Parse(Sample);

        doc.SetCommentedOutAtLine(15, false);
        doc.SetCommentedOutAtLine(15, true);

        Assert.Equal("#  disabled_key=1", doc.Lines[15].ToRawText());
    }

    [Fact]
    public void Uncommenting_with_a_new_value_writes_the_new_value()
    {
        var doc = IniParser.Parse(Sample);

        doc.SetValueAtLine(14, "other.exe");
        doc.SetCommentedOutAtLine(14, false);

        Assert.Equal("launch = other.exe", doc.Lines[14].ToRawText());
    }

    [Fact]
    public void Commenting_out_one_line_changes_nothing_else_in_the_file()
    {
        var doc = IniParser.Parse(Sample);

        doc.SetCommentedOutAtLine(8, true);

        var expected = Sample.Replace("\r\nmarking_mode = skip\r\n", "\r\n;marking_mode = skip\r\n");
        Assert.Equal(expected, IniWriter.WriteToString(doc));
    }
}
