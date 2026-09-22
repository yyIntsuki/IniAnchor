namespace IniAnchor.App.ViewModels;

/// <summary>
/// One row in the "pick a key" dialog (§4.4): a key found by parsing the actual file,
/// shown as "[Section] Key = value". Not a WatchedKey yet — only becomes one if picked.
/// </summary>
public class IniKeyPickItem
{
    public string? Section { get; init; }

    public string KeyName { get; init; } = string.Empty;

    public string CurrentValue { get; init; } = string.Empty;

    public string DisplayLabel => string.IsNullOrEmpty(Section)
        ? $"{KeyName} = {CurrentValue}"
        : $"[{Section}] {KeyName} = {CurrentValue}";
}
