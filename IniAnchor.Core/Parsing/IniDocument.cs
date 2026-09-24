namespace IniAnchor.Core.Parsing;

/// <summary>
/// An in-memory representation of a parsed ini file: an ordered list of lines,
/// plus enough info (newline style, trailing newline) to write it back out
/// byte-identical if nothing was changed.
/// </summary>
public class IniDocument
{
    private readonly List<IniLine> _lines;

    /// <summary>
    /// (Section, Key) -> line indices of key lines (active and commented out), built once and
    /// reused for every lookup on this document. Safe to cache: a line's Section/Key never
    /// change after parsing - only its value and commented-out state do, and those are read
    /// at lookup time.
    /// </summary>
    private Dictionary<(string? Section, string Key), List<int>>? _index;

    public IniDocument(List<IniLine> lines, string newLine = "\r\n", bool hasTrailingNewLine = true)
    {
        _lines = lines;
        NewLine = newLine;
        HasTrailingNewLine = hasTrailingNewLine;
    }

    public IReadOnlyList<IniLine> Lines => _lines;

    public string NewLine { get; }

    public bool HasTrailingNewLine { get; }

    /// <summary>
    /// Looks up a (section, key) pair and classifies the result as not found / unique / duplicate.
    /// This is the method the Apply pipeline should use before writing a value.
    ///
    /// Active lines always win: if the key has any active line, only those count, and
    /// commented-out look-alikes (e.g. commented examples next to the real line) are ignored.
    /// Only when there is no active line do commented-out lines count - that's a key that
    /// has been turned off.
    /// </summary>
    public KeyLookupResult LookupKey(string? section, string key)
    {
        var active = FindKeyOccurrences(section, key);
        return KeyLookupResult.From(active.Count > 0 ? active : FindCommentedKeyOccurrences(section, key));
    }

    /// <summary>
    /// Finds all line indices where the given key appears as an active line in the given
    /// section. Section and key names are compared case-insensitively, matching common ini
    /// conventions. Pass null for section to look for a key outside any section.
    /// </summary>
    public List<int> FindKeyOccurrences(string? section, string key) =>
        FindKeyLines(section, key, commentedOut: false);

    /// <summary>Like <see cref="FindKeyOccurrences"/>, but for commented-out key lines.</summary>
    public List<int> FindCommentedKeyOccurrences(string? section, string key) =>
        FindKeyLines(section, key, commentedOut: true);

    private List<int> FindKeyLines(string? section, string key, bool commentedOut)
    {
        var index = GetOrBuildIndex();
        return index.TryGetValue((NormalizeSection(section), key), out var matches)
            ? matches.Where(i => _lines[i].IsCommentedOut == commentedOut).ToList()
            : new List<int>();
    }

    public void SetValueAtLine(int lineIndex, string newValue)
    {
        if (lineIndex < 0 || lineIndex >= _lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        _lines[lineIndex].SetValue(newValue);
    }

    public void SetCommentedOutAtLine(int lineIndex, bool commentedOut)
    {
        if (lineIndex < 0 || lineIndex >= _lines.Count)
            throw new ArgumentOutOfRangeException(nameof(lineIndex));

        _lines[lineIndex].SetCommentedOut(commentedOut);
    }

    private Dictionary<(string? Section, string Key), List<int>> GetOrBuildIndex()
    {
        if (_index is not null)
            return _index;

        var index = new Dictionary<(string? Section, string Key), List<int>>(new KeyEntryComparer());

        for (var i = 0; i < _lines.Count; i++)
        {
            var line = _lines[i];
            if (!line.IsKeyLine)
                continue;

            var entryKey = (NormalizeSection(line.Section), line.Key!);
            if (!index.TryGetValue(entryKey, out var matches))
            {
                matches = new List<int>();
                index[entryKey] = matches;
            }

            matches.Add(i);
        }

        _index = index;
        return index;
    }

    private static string? NormalizeSection(string? section) => string.IsNullOrEmpty(section) ? null : section;

    private static bool SectionsEqual(string? a, string? b) =>
        string.Equals(NormalizeSection(a), NormalizeSection(b), StringComparison.OrdinalIgnoreCase);

    private sealed class KeyEntryComparer : IEqualityComparer<(string? Section, string Key)>
    {
        public bool Equals((string? Section, string Key) x, (string? Section, string Key) y) =>
            SectionsEqual(x.Section, y.Section) && string.Equals(x.Key, y.Key, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string? Section, string Key) obj)
        {
            var sectionHash = obj.Section is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Section);
            var keyHash = StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Key);
            return HashCode.Combine(sectionHash, keyHash);
        }
    }
}
