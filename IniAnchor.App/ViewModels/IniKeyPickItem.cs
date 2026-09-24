namespace IniAnchor.App.ViewModels;

/// <summary>
/// One row in the "pick a key" dialog (§4.4). Either a key found by parsing the actual file
/// (shown as "Key = value"), or - when <see cref="IsHeader"/> is true - a non-selectable
/// section title row placed above that section's keys. Not a WatchedKey yet — only becomes
/// one if picked.
///
/// Headers are rows in the same flat list (not a grouped CollectionViewSource) so the
/// dialog keeps binding a plain List&lt;IniKeyPickItem&gt;, already registered for the
/// trimmed build in WinRTExposedTypes.cs.
/// </summary>
public class IniKeyPickItem
{
    public string? Section { get; init; }

    public string KeyName { get; init; } = string.Empty;

    public string CurrentValue { get; init; } = string.Empty;

    /// <summary>True for a section title row; such rows can't be picked.</summary>
    public bool IsHeader { get; init; }

    /// <summary>
    /// The key is commented out in the file ("; key = value") - only listed when it has no
    /// active line. Picking it adds the key turned off, matching the file.
    /// </summary>
    public bool IsCommentedOut { get; init; }

    public bool IsKeyRow => !IsHeader;

    public string KeyLabel => IsCommentedOut ? $"; {KeyName} = {CurrentValue}" : $"{KeyName} = {CurrentValue}";

    /// <summary>Commented-out keys are shown dimmed, like turned-off keys in the main window.</summary>
    public double RowOpacity => IsCommentedOut ? 0.5 : 1.0;
}
