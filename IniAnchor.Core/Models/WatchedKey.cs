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
}
