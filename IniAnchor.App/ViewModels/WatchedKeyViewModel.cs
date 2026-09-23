using CommunityToolkit.Mvvm.ComponentModel;
using IniAnchor.Core.Models;
using IniAnchor.Core.Parsing;

namespace IniAnchor.App.ViewModels;

/// <summary>
/// Bindable wrapper around a <see cref="WatchedKey"/>. Per ARCHITECTURE.md §4.3.
/// </summary>
public partial class WatchedKeyViewModel : ObservableObject
{
    public WatchedKey Model { get; }

    public WatchedKeyViewModel(WatchedKey model)
    {
        Model = model;
        _desiredValue = model.DesiredValue;
    }

    public string Section => Model.Section ?? string.Empty;

    public string KeyName => Model.KeyName;

    /// <summary>What the key list shows: "[Section] KeyName" or just "KeyName" when there's no section.</summary>
    public string DisplayLabel => string.IsNullOrEmpty(Model.Section) ? Model.KeyName : $"[{Model.Section}] {Model.KeyName}";

    /// <summary>True while the pointer is over this key's row - shows the row's remove button.</summary>
    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private string _desiredValue;

    partial void OnDesiredValueChanged(string value) => Model.DesiredValue = value;

    /// <summary>
    /// Whether this key is currently unique/missing/duplicated in the actual file on disk.
    /// Set by <see cref="WatchedFileViewModel.RefreshKeyStatuses"/>, including right after
    /// an Apply run - only UniqueMatch was eligible to actually be written.
    /// </summary>
    [ObservableProperty]
    private KeyLookupStatus _status = KeyLookupStatus.NotFound;

    /// <summary>
    /// Set when the most recent Apply run hit a file-level error (locked/missing/permission
    /// denied) for this key specifically. Null otherwise - including after a successful
    /// apply, or a plain NotFound/DuplicateMatches, both of which are already visible via
    /// <see cref="Status"/> and don't need a separate error message.
    /// </summary>
    [ObservableProperty]
    private string? _lastApplyError;

    partial void OnLastApplyErrorChanged(string? value) => OnPropertyChanged(nameof(HasApplyError));

    public bool HasApplyError => !string.IsNullOrEmpty(LastApplyError);
}
