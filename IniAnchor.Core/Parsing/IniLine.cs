namespace IniAnchor.Core.Parsing;

public enum IniLineKind
{
    Section,
    KeyValue,
    Comment,
    Blank,
    Unknown
}

/// <summary>
/// One physical line from an ini file, tagged with what kind of line it is.
/// Keeps the original raw text so untouched lines round-trip byte-for-byte,
/// and only re-renders itself once a value has actually been changed.
/// </summary>
public class IniLine
{
    public IniLineKind Kind { get; }

    /// <summary>
    /// The section this line belongs to (the nearest [Section] header above it).
    /// Null means "before any section header" (no section).
    /// For a Section-kind line, this is that section's own name.
    /// </summary>
    public string? Section { get; }

    public string? Key { get; }

    public string? Value { get; private set; }

    private readonly string _originalRawText;

    /// <summary>Everything up to and including "key = " on a KeyValue line, used to rebuild the line if the value changes.</summary>
    private readonly string? _valuePrefix;

    private bool _modified;

    private IniLine(IniLineKind kind, string originalRawText, string? section, string? key, string? value, string? valuePrefix)
    {
        Kind = kind;
        _originalRawText = originalRawText;
        Section = section;
        Key = key;
        Value = value;
        _valuePrefix = valuePrefix;
    }

    public static IniLine CreateSection(string rawText, string sectionName) =>
        new(IniLineKind.Section, rawText, sectionName, null, null, null);

    public static IniLine CreateKeyValue(string rawText, string? section, string key, string value, string valuePrefix) =>
        new(IniLineKind.KeyValue, rawText, section, key, value, valuePrefix);

    public static IniLine CreateComment(string rawText, string? section) =>
        new(IniLineKind.Comment, rawText, section, null, null, null);

    public static IniLine CreateBlank(string rawText, string? section) =>
        new(IniLineKind.Blank, rawText, section, null, null, null);

    public static IniLine CreateUnknown(string rawText, string? section) =>
        new(IniLineKind.Unknown, rawText, section, null, null, null);

    /// <summary>
    /// Sets a new value for a KeyValue line. No-ops (and doesn't mark the line as modified)
    /// if the new value is the same as the current one.
    /// </summary>
    public void SetValue(string newValue)
    {
        if (Kind != IniLineKind.KeyValue)
            throw new InvalidOperationException($"Cannot set a value on a {Kind} line.");

        if (newValue != Value)
        {
            Value = newValue;
            _modified = true;
        }
    }

    /// <summary>
    /// Renders this line back to text (no line terminator). Unmodified lines are
    /// returned verbatim; a modified KeyValue line is rebuilt as "prefix + new value",
    /// which preserves the original key spelling/spacing but drops any trailing
    /// inline comment that followed the old value.
    /// </summary>
    public string ToRawText() => _modified ? _valuePrefix + Value : _originalRawText;
}
