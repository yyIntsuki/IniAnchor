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
            return IniLine.CreateComment(rawText, currentSection);

        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            var sectionName = trimmed[1..^1].Trim();
            currentSection = sectionName;
            return IniLine.CreateSection(rawText, sectionName);
        }

        var eqIndex = rawText.IndexOf('=');
        if (eqIndex < 0)
            return IniLine.CreateUnknown(rawText, currentSection);

        var key = rawText[..eqIndex].Trim();
        if (key.Length == 0)
            return IniLine.CreateUnknown(rawText, currentSection);

        // Preserve whatever whitespace already followed '=' (e.g. "key = value" vs "key=value").
        var afterEq = rawText[(eqIndex + 1)..];
        var valueStart = 0;
        while (valueStart < afterEq.Length && (afterEq[valueStart] == ' ' || afterEq[valueStart] == '\t'))
            valueStart++;

        var valuePrefix = rawText[..(eqIndex + 1 + valueStart)];
        var value = afterEq[valueStart..];

        return IniLine.CreateKeyValue(rawText, currentSection, key, value, valuePrefix);
    }
}
