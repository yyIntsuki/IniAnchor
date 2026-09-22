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
    private readonly List<IniKeyPickItem> _allItems = new();

    /// <summary>The item the user picked, once the dialog closes with Primary result. Null otherwise.</summary>
    public IniKeyPickItem? SelectedItem { get; private set; }

    public KeyPickerDialog(string filePath)
    {
        InitializeComponent();
        LoadItems(filePath);
        ResultsList.ItemsSource = _allItems;
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
            if (line.Kind != IniLineKind.KeyValue)
                continue;

            _allItems.Add(new IniKeyPickItem
            {
                Section = line.Section,
                KeyName = line.Key!,
                CurrentValue = line.Value ?? string.Empty
            });
        }
    }

    private void SearchBox_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
    {
        var filter = SearchBox.Text;

        ResultsList.ItemsSource = string.IsNullOrWhiteSpace(filter)
            ? _allItems
            : _allItems.Where(item =>
                item.KeyName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (item.Section?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedItem = ResultsList.SelectedItem as IniKeyPickItem;
        IsPrimaryButtonEnabled = SelectedItem is not null;
    }

    private void ResultsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (ResultsList.SelectedItem is IniKeyPickItem item)
        {
            SelectedItem = item;
            Hide();
            // Note: Hide() alone closes with no result. The caller (MainWindow) checks
            // SelectedItem itself rather than relying on ContentDialogResult for this path.
        }
    }
}
