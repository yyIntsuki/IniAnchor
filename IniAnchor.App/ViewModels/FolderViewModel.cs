using CommunityToolkit.Mvvm.ComponentModel;
using IniAnchor.Core.Models;

namespace IniAnchor.App.ViewModels;

/// <summary>
/// One row in the folder sidebar. Wraps a user-created <see cref="WatchFolder"/>, or -
/// when <see cref="Model"/> is null - the built-in "All" folder, which shows every file
/// entry and can't be renamed or deleted.
/// </summary>
public partial class FolderViewModel : ObservableObject
{
    public WatchFolder? Model { get; }

    public FolderViewModel(WatchFolder? model)
    {
        Model = model;
        _name = model?.Name ?? "All";
    }

    public bool IsAll => Model is null;

    /// <summary>Segoe Fluent icon: "all apps" grid for All, a folder for user folders.</summary>
    public string Glyph => IsAll ? "\uE71D" : "\uE8B7";

    [ObservableProperty]
    private string _name;

    partial void OnNameChanged(string value)
    {
        if (Model is not null)
            Model.Name = value;
    }

    /// <summary>True while the pointer is over this folder's row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowActions))]
    private bool _isHovered;

    /// <summary>Rename/delete buttons: only on hover, and never for "All".</summary>
    public bool ShowActions => IsHovered && !IsAll;
}
