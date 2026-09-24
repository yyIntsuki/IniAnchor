namespace IniAnchor.Core.Models;

/// <summary>
/// A single key the user wants to watch/set within a specific ini file.
/// </summary>
public class WatchedKey
{
    /// <summary>
    /// Section the key lives in (e.g. "General"). Null/empty means "no section"
    /// (a key that appears before any [Section] header in the file).
    /// </summary>
    public string? Section { get; set; }

    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// The value the user wants written into the file on Apply.
    /// </summary>
    public string DesiredValue { get; set; } = string.Empty;

    /// <summary>
    /// The value the key had in the file when it was added to the watch list. Never changes
    /// afterwards, so the user can always revert DesiredValue back to it. Null for keys
    /// added before this was recorded.
    /// </summary>
    public string? OriginalValue { get; set; }

    /// <summary>
    /// The key is turned off: Apply comments its line out (e.g. "; launch = x") instead of
    /// setting DesiredValue. The value is kept for when the key is turned back on.
    /// Missing in older watchlist files, so they load with every key on (false).
    /// </summary>
    public bool CommentedOut { get; set; }

    /// <summary>Whether the key was commented out when it was added - restored by revert.</summary>
    public bool OriginalCommentedOut { get; set; }

    /// <summary>
    /// True when two keys want the same result in the file: both off, or both on with the
    /// same value. An off key's value doesn't count - nothing is written for it.
    /// </summary>
    public bool HasSameDesiredStateAs(WatchedKey other) =>
        CommentedOut == other.CommentedOut && (CommentedOut || DesiredValue == other.DesiredValue);
}
