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
        _commentedOut = model.CommentedOut;
    }

    public string Section => Model.Section ?? string.Empty;

    public string KeyName => Model.KeyName;

    /// <summary>
    /// What the key list shows: "[Section] KeyName" or just "KeyName" when there's no section,
    /// with a "; " in front when the key is turned off - just like it will look in the file.
    /// </summary>
    public string DisplayLabel =>
        (CommentedOut ? "; " : string.Empty) +
        (string.IsNullOrEmpty(Model.Section) ? Model.KeyName : $"[{Model.Section}] {Model.KeyName}");

    /// <summary>True while the pointer is over this key's row - shows the row's buttons.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRevert))]
    private bool _isHovered;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowRevert))]
    private string _desiredValue;

    partial void OnDesiredValueChanged(string value) => Model.DesiredValue = value;

    // --- On/off (commented out) ---

    /// <summary>
    /// The key is turned off: Apply comments its line out instead of setting the value.
    /// Like the value box, this only changes what the user wants - the file changes on Apply.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayLabel))]
    [NotifyPropertyChangedFor(nameof(ContentOpacity))]
    [NotifyPropertyChangedFor(nameof(ToggleGlyph))]
    [NotifyPropertyChangedFor(nameof(ToggleToolTip))]
    [NotifyPropertyChangedFor(nameof(ShowRevert))]
    private bool _commentedOut;

    partial void OnCommentedOutChanged(bool value) => Model.CommentedOut = value;

    public void ToggleCommentedOut() => CommentedOut = !CommentedOut;

    /// <summary>Turned-off keys are shown dimmed (name and value box).</summary>
    public double ContentOpacity => CommentedOut ? 0.5 : 1.0;

    /// <summary>The toggle button shows what clicking it will do: hide (turn off) or show (turn on).</summary>
    public string ToggleGlyph => CommentedOut ? "\uE890" : "\uED1A";

    public string ToggleToolTip => CommentedOut ? "Enable on Apply" : "Comment out on Apply";

    // --- Revert to how the key was when it was added (value and on/off) ---

    /// <summary>
    /// Revert button: on hover, only when there's an original to go back to and the key
    /// currently differs from it (value or on/off). Keys added before OriginalValue was
    /// recorded have none, so they never show it.
    /// </summary>
    public bool ShowRevert =>
        IsHovered && Model.OriginalValue is not null &&
        (DesiredValue != Model.OriginalValue || CommentedOut != Model.OriginalCommentedOut);

    public string RevertToolTip =>
        $"Revert to original: {(Model.OriginalCommentedOut ? "; " : string.Empty)}{Model.OriginalValue}";

    /// <summary>
    /// Sets the value and on/off state back to the original. Like typing it in: the file
    /// itself only changes on the next Apply.
    /// </summary>
    public void RevertToOriginal()
    {
        if (Model.OriginalValue is null)
            return;

        DesiredValue = Model.OriginalValue;
        CommentedOut = Model.OriginalCommentedOut;
    }

    // --- Status on disk ---

    /// <summary>
    /// Whether this key is currently unique/missing/duplicated in the actual file on disk.
    /// Set by <see cref="WatchedFileViewModel.RefreshKeyStatuses"/>, including right after
    /// an Apply run - only UniqueMatch was eligible to actually be written.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUnique))]
    [NotifyPropertyChangedFor(nameof(IsNotFound))]
    [NotifyPropertyChangedFor(nameof(IsDuplicate))]
    private KeyLookupStatus _status = KeyLookupStatus.NotFound;

    // One flat bool per status: the row shows the matching icon (tooltip "Unique",
    // "Not Found", "Duplicate") - no converters needed.
    public bool IsUnique => Status == KeyLookupStatus.UniqueMatch;
    public bool IsNotFound => Status == KeyLookupStatus.NotFound;
    public bool IsDuplicate => Status == KeyLookupStatus.DuplicateMatches;

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
