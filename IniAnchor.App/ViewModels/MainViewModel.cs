using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
/// Root view model for MainWindow. Per ARCHITECTURE.md §7 build-order item 5, this stage
/// only wires up the watched-file list itself (add/remove/persist) — key-editing
/// (WatchedKeyViewModel, item 6) comes later.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly WatchlistStore _store;

    public ObservableCollection<WatchedFileViewModel> WatchedFiles { get; } = new();

    [ObservableProperty]
    private bool _hasNoWatchedFiles = true;

    [ObservableProperty]
    private WatchedFileViewModel? _selectedFile;

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

        foreach (var file in _store.Load())
        {
            WatchedFiles.Add(new WatchedFileViewModel(file));
        }

        HasNoWatchedFiles = WatchedFiles.Count == 0;
        WatchedFiles.CollectionChanged += (_, _) => HasNoWatchedFiles = WatchedFiles.Count == 0;
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

    /// <summary>
    /// Adds a newly picked file to the watch list and persists. The file picker itself
    /// lives in code-behind (per §4.5, it needs the window handle), which then calls this.
    /// </summary>
    public void AddFile(string filePath)
    {
        bool alreadyWatched = WatchedFiles.Any(f =>
            string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (alreadyWatched)
            return;

        var watchedFile = new WatchedFile { FilePath = filePath };
        WatchedFiles.Add(new WatchedFileViewModel(watchedFile));
        Persist();
    }

    [RelayCommand]
    private void RemoveFile(WatchedFileViewModel? file)
    {
        if (file is null)
            return;

        WatchedFiles.Remove(file);

        if (SelectedFile == file)
            SelectedFile = null;

        Persist();
    }

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
            DesiredValue = picked.CurrentValue
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

    [ObservableProperty]
    private string _lastApplyMessage = string.Empty;

    /// <summary>
    /// The single global Apply (§5): runs the pipeline for every watched file at once.
    /// File IO happens off the UI thread via Task.Run so a large/slow file doesn't freeze
    /// the window; ApplyRunner itself is the tested, correctness-critical part (Core).
    /// </summary>
    [RelayCommand]
    private async Task ApplyAllAsync()
    {
        var fileVms = WatchedFiles.ToList(); // snapshot order so it lines up with the results below
        var fileModels = fileVms.Select(f => f.Model).ToList();

        var results = await Task.Run(() => ApplyRunner.ApplyToAll(fileModels));

        int applied = 0, notFound = 0, duplicate = 0, fileErrors = 0;

        for (var i = 0; i < fileVms.Count; i++)
        {
            var fileVm = fileVms[i];
            var fileResult = results[i];

            foreach (var keyResult in fileResult.KeyResults)
            {
                switch (keyResult.Outcome)
                {
                    case ApplyKeyOutcome.Applied: applied++; break;
                    case ApplyKeyOutcome.NotFound: notFound++; break;
                    case ApplyKeyOutcome.DuplicateMatches: duplicate++; break;
                    case ApplyKeyOutcome.FileError: fileErrors++; break;
                }

                // Route the file-level error message to the specific key(s) affected -
                // Status alone (via RefreshKeyStatuses below) can't distinguish "file is
                // locked" from a plain NotFound, since a failed re-parse also falls back
                // to NotFound for every key in that file.
                var keyVm = fileVm.WatchedKeys.FirstOrDefault(k => k.Model == keyResult.Key);
                if (keyVm is not null)
                {
                    keyVm.LastApplyError = keyResult.Outcome == ApplyKeyOutcome.FileError
                        ? fileResult.FileErrorMessage
                        : null;
                }
            }
        }

        // Statuses (unique/not-found/duplicate badges) reflect the files as they now are.
        foreach (var fileVm in WatchedFiles)
            fileVm.RefreshKeyStatuses();

        LastApplyMessage = BuildSummary(applied, notFound, duplicate, fileErrors);

        // LastAppliedValue/LastAppliedAtUtc changed on applied keys - save them.
        Persist();
    }

    private static string BuildSummary(int applied, int notFound, int duplicate, int fileErrors)
    {
        var parts = new List<string> { $"Applied {applied} key(s)." };

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
        _store.Save(WatchedFiles.Select(f => f.Model).ToList());
    }
}
