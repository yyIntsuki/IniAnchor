using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IniAnchor.App.ViewModels;
using IniAnchor.Core.Parsing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace IniAnchor.App.Views;

/// <summary>
/// "Pick a key to watch" dialog per ARCHITECTURE.md §4.4: parses the file and shows a
/// searchable "Section → Key = currentValue" list instead of free-text entry.
/// </summary>
public sealed partial class KeyPickerDialog : ContentDialog
{
    private readonly List<IniKeyPickItem> _allItems = new(); // key rows only; headers added on display

    /// <summary>
    /// The item the user confirmed (Add button or double-tap). Stays null on Cancel/Esc,
    /// even if a row was highlighted - merely highlighting a row is not a pick.
    /// </summary>
    public IniKeyPickItem? SelectedItem { get; private set; }

    public KeyPickerDialog(string filePath)
    {
        InitializeComponent();
        LoadItems(filePath);
        ResultsList.ItemsSource = WithSectionHeaders(_allItems);
    }

    /// <summary>
    /// Inserts a section title row wherever the section changes, so each section's name is
    /// shown once above its keys instead of in front of every key. Keys outside any section
    /// (top of the file) get no title. Used both on open and after every search.
    /// </summary>
    private static List<IniKeyPickItem> WithSectionHeaders(IEnumerable<IniKeyPickItem> keys)
    {
        var rows = new List<IniKeyPickItem>();
        string? currentSection = null;

        foreach (var key in keys)
        {
            if (!string.IsNullOrEmpty(key.Section) && key.Section != currentSection)
                rows.Add(new IniKeyPickItem { Section = key.Section, IsHeader = true });

            currentSection = key.Section;
            rows.Add(key);
        }

        return rows;
    }

    private void LoadItems(string filePath)
    {
        IniDocument document;
        try
        {
            document = IniParser.ParseFile(filePath);
        }
        catch (IOException)
        {
            return; // _allItems stays empty; primary button stays disabled
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        foreach (var line in document.Lines)
        {
            // Active keys, plus commented-out keys that have no active line - the ones that
            // are turned off. A commented example next to a real key isn't listed (the active
            // line is what IniAnchor would find for that key anyway).
            var listed = line.Kind == IniLineKind.KeyValue ||
                         (line.Kind == IniLineKind.CommentedKeyValue &&
                          document.FindKeyOccurrences(line.Section, line.Key!).Count == 0);

            if (!listed)
                continue;

            _allItems.Add(new IniKeyPickItem
            {
                Section = line.Section,
                KeyName = line.Key!,
                CurrentValue = line.Value ?? string.Empty,
                IsCommentedOut = line.IsCommentedOut
            });
        }
    }

    private void SearchBox_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
    {
        var filter = SearchBox.Text;

        var matches = string.IsNullOrWhiteSpace(filter)
            ? _allItems
            : _allItems.Where(item =>
                item.KeyName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (item.Section?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));

        ResultsList.ItemsSource = WithSectionHeaders(matches);
    }

    // Title rows can't be clicked (no hover highlight either). Containers get recycled, so
    // this is set both ways every time.
    private void ResultsList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        args.ItemContainer.IsHitTestVisible = args.Item is not IniKeyPickItem { IsHeader: true };
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // A title row can still be reached with the arrow keys - don't let it count as a pick.
        if (ResultsList.SelectedItem is IniKeyPickItem { IsHeader: true })
        {
            ResultsList.SelectedItem = null; // re-raises this event, which disables "Add"
            return;
        }

        // Only enables "Add" - SelectedItem is set on confirm, so Cancel adds nothing.
        IsPrimaryButtonEnabled = ResultsList.SelectedItem is IniKeyPickItem;
    }

    private void Dialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        SelectedItem = ResultsList.SelectedItem as IniKeyPickItem;
    }

    private void ResultsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is IniKeyPickItem { IsHeader: false } item)
        {
            SelectedItem = item;
            Hide();
            // Note: Hide() alone closes with no result. The caller (MainWindow) checks
            // SelectedItem itself rather than relying on ContentDialogResult for this path.
        }
    }
}
