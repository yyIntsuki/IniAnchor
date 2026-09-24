using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IniAnchor.Core.Applying;
using IniAnchor.Core.Models;
using IniAnchor.Core.Persistence;

namespace IniAnchor.App.ViewModels;

/// <summary>
/// Root view model for MainWindow: folder sidebar, the selected folder's file entries,
/// and the selected entry's keys.
///
/// Folders work like profiles (see <see cref="WatchFolder"/>): each file entry belongs to
/// one folder or none, and the same ini file can have an entry in several folders with
/// different keys/values. "All" shows every entry.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly WatchlistStore _store;

    // What gets saved to watchlist.json. Kept in sync on every add/remove, so Persist()
    // just saves it and folder rules (delete, apply order, conflicts) come from Core.
    private readonly Watchlist _watchlist;

    // Every file entry across all folders, same order as _watchlist.Files.
    private readonly List<WatchedFileViewModel> _allFiles = new();

    // --- Folders (sidebar) ---

    public FolderViewModel AllFolder { get; } = new(null);

    /// <summary>"All" first, then user folders in sidebar order.</summary>
    public ObservableCollection<FolderViewModel> Folders { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUserFolderSelected))]
    private FolderViewModel? _selectedFolder;

    // Null (e.g. Ctrl+click deselected the sidebar row) simply counts as "All".
    private bool IsAllSelected => SelectedFolder is null || SelectedFolder.IsAll;

    /// <summary>Enables "Apply folder" - a flat bool, not a nested x:Bind path (§4.1).</summary>
    public bool HasUserFolderSelected => !IsAllSelected;

    // --- Files (middle panel) ---

    /// <summary>The selected folder's file entries (every entry when "All" is selected).</summary>
    public ObservableCollection<WatchedFileViewModel> VisibleFiles { get; } = new();

    [ObservableProperty]
    private bool _hasNoVisibleFiles = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedFile))]
    [NotifyPropertyChangedFor(nameof(SelectedFileKeys))]
    private WatchedFileViewModel? _selectedFile;

    // Flat bool for enabling "Add key..." - deliberately not a nested x:Bind path (§4.1).
    public bool HasSelectedFile => SelectedFile is not null;

    // Flat source for the keys list. Deliberately not the nested x:Bind path
    // "SelectedFile.WatchedKeys": that never cleared the list when SelectedFile became null
    // (deselect/remove), same x:Bind null-path problem as in §4.1.
    public ObservableCollection<WatchedKeyViewModel>? SelectedFileKeys => SelectedFile?.WatchedKeys;

    // Combined message for the right panel's empty state. Deliberately NOT a nested x:Bind
    // path like "SelectedFile.HasNoWatchedKeys" - x:Bind's null-fallback through a second
    // object level wasn't collapsing reliably here, so this is computed explicitly instead
    // and refreshed at every point that could change it.
    [ObservableProperty]
    private bool _showKeysEmptyMessage = true;

    [ObservableProperty]
    private string _keysEmptyMessage = SelectFileHint;

    private const string SelectFileHint = "Select a file on the left to see its watched keys.";
    private const string NoKeysHint = "No keys watched in this file yet. Click 'Add key...' to get started.";

    [ObservableProperty]
    private string _lastApplyMessage = string.Empty;

    // Stores watchlist.json next to the actual .exe (the portable "copy the folder" goal, §3.5).
    // Deliberately NOT WatchlistStore's AppContext.BaseDirectory default: in the published
    // single-file build that points to a random temp extraction folder under
    // %TEMP%\.net\IniAnchor.App\, so the watchlist would be lost on every new build.
    public MainViewModel() : this(new WatchlistStore(Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
        "watchlist.json")))
    {
    }

    // Allows tests / alternate storage locations to supply their own store.
    public MainViewModel(WatchlistStore store)
    {
        _store = store;
        _watchlist = _store.Load();

        Folders.Add(AllFolder);
        foreach (var folder in _watchlist.Folders)
            Folders.Add(new FolderViewModel(folder));

        foreach (var file in _watchlist.Files)
            _allFiles.Add(new WatchedFileViewModel(file) { FolderName = FolderNameOf(file.FolderId) });

        VisibleFiles.CollectionChanged += (_, _) => HasNoVisibleFiles = VisibleFiles.Count == 0;

        SelectedFolder = AllFolder; // fills VisibleFiles via OnSelectedFolderChanged
    }

    private string? FolderNameOf(Guid? folderId) =>
        folderId is Guid id ? _watchlist.Folders.FirstOrDefault(f => f.Id == id)?.Name : null;

    private bool IsInSelectedFolder(WatchedFileViewModel file) =>
        IsAllSelected || file.Model.FolderId == SelectedFolder!.Model!.Id;

    partial void OnSelectedFolderChanged(FolderViewModel? value) => RefreshVisibleFiles();

    private void RefreshVisibleFiles()
    {
        VisibleFiles.Clear();

        foreach (var file in _allFiles)
        {
            if (!IsInSelectedFolder(file))
                continue;

            file.ShowFolderName = IsAllSelected && file.FolderName is not null;
            VisibleFiles.Add(file);
        }

        if (SelectedFile is not null && !VisibleFiles.Contains(SelectedFile))
            SelectedFile = null;
    }

    partial void OnSelectedFileChanged(WatchedFileViewModel? value)
    {
        value?.RefreshKeyStatuses();
        UpdateKeysEmptyMessage();
    }

    private void UpdateKeysEmptyMessage()
    {
        if (SelectedFile is null)
        {
            ShowKeysEmptyMessage = true;
            KeysEmptyMessage = SelectFileHint;
        }
        else if (SelectedFile.WatchedKeys.Count == 0)
        {
            ShowKeysEmptyMessage = true;
            KeysEmptyMessage = NoKeysHint;
        }
        else
        {
            ShowKeysEmptyMessage = false;
        }
    }

    // --- Folder management (name prompts/confirmations live in code-behind: need XamlRoot) ---

    /// <summary>"New folder", or "New folder 2", "New folder 3"... if that name is taken.</summary>
    public string SuggestNewFolderName()
    {
        var name = "New folder";
        for (var n = 2; Folders.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)); n++)
            name = $"New folder {n}";
        return name;
    }

    public void CreateFolder(string name)
    {
        var folder = new WatchFolder { Name = name };
        _watchlist.Folders.Add(folder);

        var folderVm = new FolderViewModel(folder);
        Folders.Add(folderVm);
        SelectedFolder = folderVm;
        Persist();
    }

    public void RenameFolder(FolderViewModel folder, string name)
    {
        if (folder.IsAll)
            return;

        folder.Name = name; // also updates the WatchFolder model

        foreach (var file in _allFiles.Where(f => f.Model.FolderId == folder.Model!.Id))
            file.FolderName = name;

        Persist();
    }

    /// <summary>How many file entries a folder has - shown in the delete confirmation.</summary>
    public int EntryCount(FolderViewModel folder) =>
        folder.IsAll ? _allFiles.Count : _allFiles.Count(f => f.Model.FolderId == folder.Model!.Id);

    /// <summary>Deletes a user folder and all of its file entries. The ini files on disk aren't touched.</summary>
    public void DeleteFolder(FolderViewModel folder)
    {
        if (folder.IsAll)
            return;

        _watchlist.DeleteFolder(folder.Model!.Id);
        _allFiles.RemoveAll(f => !_watchlist.Files.Contains(f.Model));

        // Move off the folder before removing it, so the sidebar doesn't pass through "nothing selected".
        if (SelectedFolder == folder)
            SelectedFolder = AllFolder;
        else
            RefreshVisibleFiles(); // e.g. "All" was showing the deleted entries

        Folders.Remove(folder);
        Persist();
    }

    // --- File entries ---

    /// <summary>
    /// Adds a newly picked file to the selected folder (no folder when "All" is selected)
    /// and persists. The file picker itself lives in code-behind (§4.5, needs the window).
    /// The same file may already be in other folders - each folder gets its own entry.
    /// </summary>
    public void AddFile(string filePath)
    {
        Guid? folderId = IsAllSelected ? null : SelectedFolder!.Model!.Id;

        bool alreadyInFolder = _allFiles.Any(f =>
            f.Model.FolderId == folderId &&
            string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (alreadyInFolder)
            return;

        var model = new WatchedFile { FilePath = filePath, FolderId = folderId };
        _watchlist.Files.Add(model);

        var fileVm = new WatchedFileViewModel(model) { FolderName = FolderNameOf(folderId) };
        _allFiles.Add(fileVm);
        VisibleFiles.Add(fileVm); // always belongs to what's currently shown
        Persist();
    }

    [RelayCommand]
    private void RemoveFile(WatchedFileViewModel? file)
    {
        if (file is null)
            return;

        _watchlist.Files.Remove(file.Model);
        _allFiles.Remove(file);
        VisibleFiles.Remove(file);

        if (SelectedFile == file)
            SelectedFile = null;

        Persist();
    }

    /// <summary>
    /// Opens the file in whatever app Windows has associated with .ini files (usually
    /// Notepad) - the same as double-clicking it in Explorer.
    /// </summary>
    public void OpenFileInEditor(WatchedFileViewModel file)
    {
        if (!File.Exists(file.FilePath))
        {
            LastApplyMessage = $"Could not open file - it no longer exists: {file.FilePath}";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(file.FilePath) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            // e.g. no app is associated with .ini files
            LastApplyMessage = $"Could not open file: {ex.Message}";
        }
    }

    // --- Keys ---

    /// <summary>
    /// Adds a key picked from <see cref="SelectedFile"/>'s picker dialog to that file's
    /// watch list. Desired value starts out equal to the current value on disk — a
    /// sensible default the user then edits.
    /// </summary>
    public void AddKeyToSelectedFile(IniKeyPickItem picked)
    {
        if (SelectedFile is null)
            return;

        bool alreadyWatched = SelectedFile.WatchedKeys.Any(k =>
            string.Equals(k.Section, picked.Section ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(k.KeyName, picked.KeyName, StringComparison.OrdinalIgnoreCase));

        if (alreadyWatched)
            return;

        var watchedKey = new WatchedKey
        {
            Section = picked.Section,
            KeyName = picked.KeyName,
            DesiredValue = picked.CurrentValue,
            OriginalValue = picked.CurrentValue // kept forever, for "revert"
        };

        SelectedFile.Model.WatchedKeys.Add(watchedKey);
        SelectedFile.WatchedKeys.Add(new WatchedKeyViewModel(watchedKey));
        SelectedFile.RefreshKeyStatuses();
        UpdateKeysEmptyMessage();
        Persist();
    }

    [RelayCommand]
    private void RemoveKey(WatchedKeyViewModel? key)
    {
        if (key is null || SelectedFile is null)
            return;

        SelectedFile.Model.WatchedKeys.Remove(key.Model);
        SelectedFile.WatchedKeys.Remove(key);
        UpdateKeysEmptyMessage();
        Persist();
    }

    // --- Apply ---

    /// <summary>
    /// The entries an Apply would write, in Core's apply order (entries without a folder
    /// first, then folders top to bottom - lower folders win on conflicting keys).
    /// allFolders=false limits it to the selected folder ("Apply folder").
    /// </summary>
    public List<WatchedFile> FilesToApply(bool allFolders)
    {
        var ordered = _watchlist.FilesInApplyOrder();

        return allFolders || IsAllSelected
            ? ordered
            : ordered.Where(f => f.FolderId == SelectedFolder!.Model!.Id).ToList();
    }

    /// <summary>
    /// Runs the Apply pipeline (§5) for the given entries. File IO happens off the UI thread
    /// via Task.Run so a large/slow file doesn't freeze the window; ApplyRunner itself is the
    /// tested, correctness-critical part (Core). Confirmation happens in code-behind first.
    /// </summary>
    public async Task ApplyAsync(List<WatchedFile> files)
    {
        var results = await Task.Run(() => ApplyRunner.ApplyToAll(files));

        var fileVmByModel = _allFiles.ToDictionary(f => f.Model);
        int applied = 0, alreadySet = 0, overridden = 0, notFound = 0, duplicate = 0, fileErrors = 0;

        foreach (var fileResult in results)
        {
            fileVmByModel.TryGetValue(fileResult.File, out var fileVm);

            foreach (var keyResult in fileResult.KeyResults)
            {
                switch (keyResult.Outcome)
                {
                    case ApplyKeyOutcome.Applied: applied++; break;
                    case ApplyKeyOutcome.AlreadySet: alreadySet++; break;
                    case ApplyKeyOutcome.Overridden: overridden++; break;
                    case ApplyKeyOutcome.NotFound: notFound++; break;
                    case ApplyKeyOutcome.DuplicateMatches: duplicate++; break;
                    case ApplyKeyOutcome.FileError: fileErrors++; break;
                }

                // Route the file-level error message to the specific key(s) affected -
                // Status alone (via RefreshKeyStatuses below) can't distinguish "file is
                // locked" from a plain NotFound, since a failed re-parse also falls back
                // to NotFound for every key in that file.
                var keyVm = fileVm?.WatchedKeys.FirstOrDefault(k => k.Model == keyResult.Key);
                if (keyVm is not null)
                {
                    keyVm.LastApplyError = keyResult.Outcome == ApplyKeyOutcome.FileError
                        ? fileResult.FileErrorMessage
                        : null;
                }
            }
        }

        // Statuses (unique/not-found/duplicate badges) reflect the files as they now are.
        // All entries, not just the applied ones: other entries may point at the same files.
        foreach (var fileVm in _allFiles)
            fileVm.RefreshKeyStatuses();

        LastApplyMessage = BuildSummary(applied, alreadySet, overridden, notFound, duplicate, fileErrors);
    }

    private static string BuildSummary(int applied, int alreadySet, int overridden, int notFound, int duplicate, int fileErrors)
    {
        var parts = new List<string> { $"Applied {applied} key(s)." };

        if (alreadySet > 0)
            parts.Add($"{alreadySet} already set.");
        if (overridden > 0)
            parts.Add($"{overridden} overridden by a lower folder.");
        if (notFound > 0)
            parts.Add($"{notFound} not found.");
        if (duplicate > 0)
            parts.Add($"{duplicate} duplicate.");
        if (fileErrors > 0)
            parts.Add($"{fileErrors} file error(s).");

        return string.Join(" ", parts);
    }

    public void Persist()
    {
        _store.Save(_watchlist);
    }
}
