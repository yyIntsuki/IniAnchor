using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using IniAnchor.Core.Models;
using IniAnchor.Core.Parsing;

namespace IniAnchor.App.ViewModels;

/// <summary>
/// Thin bindable wrapper around a <see cref="WatchedFile"/>. Per ARCHITECTURE.md §4.3.
/// </summary>
public partial class WatchedFileViewModel : ObservableObject
{
    public WatchedFile Model { get; }

    public ObservableCollection<WatchedKeyViewModel> WatchedKeys { get; }

    public WatchedFileViewModel(WatchedFile model)
    {
        Model = model;
        WatchedKeys = new ObservableCollection<WatchedKeyViewModel>(
            model.WatchedKeys.Select(k => new WatchedKeyViewModel(k)));
    }

    /// <summary>What the file list shows: the friendly name if set, otherwise the raw path.</summary>
    public string DisplayText => string.IsNullOrWhiteSpace(Model.DisplayName) ? Model.FilePath : Model.DisplayName!;

    public string FilePath => Model.FilePath;

    /// <summary>
    /// Re-parses the file on disk and updates every watched key's Status (unique/not-found/
    /// duplicate), per §4.4 ("surfaces duplicates immediately"). Swallows IO/parse errors —
    /// if the file can't be read, every key just shows NotFound rather than crashing the UI.
    /// </summary>
    public void RefreshKeyStatuses()
    {
        IniDocument? document;
        try
        {
            document = IniParser.ParseFile(Model.FilePath);
        }
        catch (IOException)
        {
            document = null;
        }
        catch (UnauthorizedAccessException)
        {
            document = null;
        }

        foreach (var keyVm in WatchedKeys)
        {
            keyVm.Status = document is null
                ? KeyLookupStatus.NotFound
                : document.LookupKey(keyVm.Section, keyVm.KeyName).Status;
        }
    }
}
