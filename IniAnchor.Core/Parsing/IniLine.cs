namespace IniAnchor.Core.Parsing;

public enum IniLineKind
{
    Section,

    /// <summary>An active "key = value" line.</summary>
    KeyValue,

    /// <summary>
    /// A commented-out key line, e.g. "; include = x" (see IniParser for the exact rule).
    /// Found by lookups only when the key has no active line (IniDocument.LookupKey).
    /// </summary>
    CommentedKeyValue,

    Comment,
    Blank,
    Unknown
}

/// <summary>
/// One physical line from an ini file, tagged with what kind of line it is.
/// Keeps the original raw text so untouched lines round-trip byte-for-byte,
/// and only re-renders itself once its value or commented-out state actually changed.
///
/// Key lines (active or commented out) can switch between the two states: that is how a
/// watched key is turned off/on (commented out/uncommented) on Apply.
/// </summary>
public class IniLine
{
    private readonly IniLineKind _kind;      // for non-key lines
    private readonly bool _isKeyLine;

    public IniLineKind Kind => _isKeyLine
        ? (IsCommentedOut ? IniLineKind.CommentedKeyValue : IniLineKind.KeyValue)
        : _kind;

    /// <summary>True for key lines, active or commented out.</summary>
    public bool IsKeyLine => _isKeyLine;

    /// <summary>For key lines: whether the line is currently commented out.</summary>
    public bool IsCommentedOut { get; private set; }

    /// <summary>
    /// The section this line belongs to (the nearest [Section] header above it).
    /// Null means "before any section header" (no section).
    /// For a Section-kind line, this is that section's own name.
    /// </summary>
    public string? Section { get; }

    public string? Key { get; }

    public string? Value { get; private set; }

    private readonly string _originalRawText;

    /// <summary>Key lines: the line without its comment prefix (the whole line if it was active).</summary>
    private readonly string? _originalBody;

    /// <summary>Key lines parsed as commented out: the original prefix, e.g. "; ", reused when re-commenting.</summary>
    private readonly string? _commentPrefix;

    /// <summary>Everything up to and including "key = " in the body, used to rebuild the line if the value changes.</summary>
    private readonly string? _valuePrefix;

    private bool _valueChanged;
    private bool _commentedChanged;

    private IniLine(IniLineKind kind, string originalRawText, string? section)
    {
        _kind = kind;
        _originalRawText = originalRawText;
        Section = section;
    }

    private IniLine(string originalRawText, string? section, string key, string value, string valuePrefix,
        string? commentPrefix)
    {
        _isKeyLine = true;
        _originalRawText = originalRawText;
        Section = section;
        Key = key;
        Value = value;
        _valuePrefix = valuePrefix;
        _commentPrefix = commentPrefix;
        IsCommentedOut = commentPrefix is not null;
        _originalBody = commentPrefix is null ? originalRawText : originalRawText[commentPrefix.Length..];
    }

    public static IniLine CreateSection(string rawText, string sectionName) =>
        new(IniLineKind.Section, rawText, sectionName);

    public static IniLine CreateKeyValue(string rawText, string? section, string key, string value, string valuePrefix) =>
        new(rawText, section, key, value, valuePrefix, commentPrefix: null);

    /// <summary>
    /// A commented-out key line. commentPrefix is the start of rawText up to the key
    /// (e.g. "; "); valuePrefix is relative to the rest of the line after it.
    /// </summary>
    public static IniLine CreateCommentedKeyValue(string rawText, string? section, string commentPrefix,
        string key, string value, string valuePrefix) =>
        new(rawText, section, key, value, valuePrefix, commentPrefix);

    public static IniLine CreateComment(string rawText, string? section) =>
        new(IniLineKind.Comment, rawText, section);

    public static IniLine CreateBlank(string rawText, string? section) =>
        new(IniLineKind.Blank, rawText, section);

    public static IniLine CreateUnknown(string rawText, string? section) =>
        new(IniLineKind.Unknown, rawText, section);

    /// <summary>
    /// Sets a new value for a key line. No-ops (and doesn't mark the line as modified)
    /// if the new value is the same as the current one.
    /// </summary>
    public void SetValue(string newValue)
    {
        if (!_isKeyLine)
            throw new InvalidOperationException($"Cannot set a value on a {Kind} line.");

        if (newValue != Value)
        {
            Value = newValue;
            _valueChanged = true;
        }
    }

    /// <summary>Comments a key line out or back in. No-ops if it's already in that state.</summary>
    public void SetCommentedOut(bool commentedOut)
    {
        if (!_isKeyLine)
            throw new InvalidOperationException($"Cannot comment out a {Kind} line.");

        if (commentedOut != IsCommentedOut)
        {
            IsCommentedOut = commentedOut;
            _commentedChanged = true;
        }
    }

    /// <summary>
    /// Renders this line back to text (no line terminator). Unmodified lines are returned
    /// verbatim. A key line whose value changed is rebuilt as "prefix + new value", which
    /// preserves the original key spelling/spacing but drops any trailing inline comment
    /// that followed the old value. Commenting out adds the original comment prefix (or ";"
    /// for a line that was active); uncommenting removes it.
    /// </summary>
    public string ToRawText()
    {
        if (!_valueChanged && !_commentedChanged)
            return _originalRawText;

        var body = _valueChanged ? _valuePrefix + Value : _originalBody;
        return IsCommentedOut ? (_commentPrefix ?? ";") + body : body!;
    }
}
