namespace IniAnchor.Core.Parsing;

public static class IniParser
{
    public static IniDocument Parse(string text)
    {
        var newLine = text.Contains("\r\n") ? "\r\n" : text.Contains('\n') ? "\n" : Environment.NewLine;

        var rawLines = SplitLines(text, out var hasTrailingNewLine);

        string? currentSection = null;
        var lines = new List<IniLine>(rawLines.Count);
        foreach (var raw in rawLines)
            lines.Add(ParseLine(raw, ref currentSection));

        return new IniDocument(lines, newLine, hasTrailingNewLine);
    }

    public static IniDocument ParseFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return Parse(text);
    }

    private static List<string> SplitLines(string text, out bool hasTrailingNewLine)
    {
        if (text.Length == 0)
        {
            hasTrailingNewLine = true; // nothing to write, treat as trivially "ends with newline"
            return new List<string>();
        }

        hasTrailingNewLine = text.EndsWith('\n') || text.EndsWith('\r');

        var normalized = text.Replace("\r\n", "\n");
        var rawLines = normalized.Split('\n').ToList();

        // Split() on a trailing newline produces one phantom empty entry at the end - drop it.
        if (hasTrailingNewLine && rawLines.Count > 0 && rawLines[^1].Length == 0)
            rawLines.RemoveAt(rawLines.Count - 1);

        return rawLines;
    }

    private static IniLine ParseLine(string rawText, ref string? currentSection)
    {
        var trimmed = rawText.Trim();

        if (trimmed.Length == 0)
            return IniLine.CreateBlank(rawText, currentSection);

        if (trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            return TryParseCommentedKey(rawText, currentSection) ?? IniLine.CreateComment(rawText, currentSection);

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            var sectionName = trimmed[1..^1].Trim();
            currentSection = sectionName;
            return IniLine.CreateSection(rawText, sectionName);
        }

        if (!TrySplitKeyValue(rawText, out var key, out var value, out var valuePrefix))
            return IniLine.CreateUnknown(rawText, currentSection);

        return IniLine.CreateKeyValue(rawText, currentSection, key, value, valuePrefix);
    }

    /// <summary>
    /// Recognizes a commented-out key such as "; include = Core\Debugger\Debugger.ini" or
    /// "#key=value": the comment marker, optional spaces, then exactly the shape of a key
    /// line whose name is identifier-like (starts with a letter, '_' or '$'; then letters,
    /// digits, '_', '-', '.', '$'). Prose comments don't fit: they have spaces or quotes
    /// before their '=' ("; \"skip\" = skip shader", "; (e.g. \"dump = ..."). Anything else
    /// stays a plain comment (returns null).
    /// </summary>
    private static IniLine? TryParseCommentedKey(string rawText, string? currentSection)
    {
        // The first non-whitespace character is the ';' or '#' marker.
        var bodyStart = rawText.IndexOfAny(CommentMarkers) + 1;
        while (bodyStart < rawText.Length && (rawText[bodyStart] == ' ' || rawText[bodyStart] == '\t'))
            bodyStart++;

        var body = rawText[bodyStart..];
        if (!TrySplitKeyValue(body, out var key, out var value, out var valuePrefix) || !IsIdentifierLike(key))
            return null;

        return IniLine.CreateCommentedKeyValue(rawText, currentSection, rawText[..bodyStart], key, value, valuePrefix);
    }

    private static readonly char[] CommentMarkers = { ';', '#' };

    private static bool IsIdentifierLike(string name)
    {
        if (!(char.IsLetter(name[0]) || name[0] == '_' || name[0] == '$'))
            return false;

        foreach (var c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == '$'))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Splits "key = value" text. valuePrefix is everything up to the value, preserving
    /// whatever whitespace already followed '=' ("key = value" vs "key=value"), so a changed
    /// value can be written back in the same style. Values may themselves contain '='.
    /// </summary>
    private static bool TrySplitKeyValue(string text, out string key, out string value, out string valuePrefix)
    {
        key = value = valuePrefix = string.Empty;

        var eqIndex = text.IndexOf('=');
        if (eqIndex < 0)
            return false;

        key = text[..eqIndex].Trim();
        if (key.Length == 0)
            return false;

        var afterEq = text[(eqIndex + 1)..];
        var valueStart = 0;
        while (valueStart < afterEq.Length && (afterEq[valueStart] == ' ' || afterEq[valueStart] == '\t'))
            valueStart++;

        valuePrefix = text[..(eqIndex + 1 + valueStart)];
        value = afterEq[valueStart..];
        return true;
    }
}
