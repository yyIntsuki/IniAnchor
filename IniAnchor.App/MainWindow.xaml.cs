using System;
using System.Linq;
using IniAnchor.App.ViewModels;
using IniAnchor.App.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace IniAnchor.App
{
    /// <summary>
    /// Main (and, for v1, only) window. See ARCHITECTURE.md §4.2.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; } = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        // File picking needs the window (§4.5), so it lives here rather than in the view model;
        // the actual add/persist logic stays in MainViewModel.AddFile.
        // Uses the Windows App SDK picker (Microsoft.Windows.Storage.Pickers), not the old UWP
        // one (Windows.Storage.Pickers): it takes the window's AppWindow.Id directly, so no COM
        // interop (InitializeWithWindow) is needed - simpler, and safer for the trimmed publish.
        private async void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker(AppWindow.Id)
                {
                    SuggestedStartLocation = PickerLocationId.ComputerFolder
                };
                picker.FileTypeFilter.Add(".ini");

                var result = await picker.PickSingleFileAsync();
                if (result is not null)
                {
                    ViewModel.AddFile(result.Path);
                }
            }
            catch (Exception ex)
            {
                // async void handlers swallow exceptions invisibly - show it instead.
                ViewModel.LastApplyMessage = $"Could not add file: {ex.GetType().Name}: {ex.Message}";
            }
        }

        // Key picking (§4.4) needs a XamlRoot to show the ContentDialog, so it lives here;
        // the actual add/persist logic stays in MainViewModel.AddKeyToSelectedFile.
        private async void AddKeyButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = ViewModel.SelectedFile;
            if (selectedFile is null)
                return; // no file selected - nothing to pick a key from

            var dialog = new KeyPickerDialog(selectedFile.FilePath)
            {
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();

            // Checked regardless of the ContentDialogResult: the dialog sets SelectedItem
            // both on "Add" click and on double-tap (see KeyPickerDialog for why).
            if (dialog.SelectedItem is not null)
            {
                ViewModel.AddKeyToSelectedFile(dialog.SelectedItem);
            }
        }

        // Deselect (shared by both lists): clicking empty space below the rows clears the
        // selection, like Explorer. Taps on a row (or a TextBox inside one) are ignored.
        // The TwoWay SelectedItem binding pushes the null into the view model.
        private void List_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var list = (ListView)sender;

            for (var node = e.OriginalSource as DependencyObject;
                 node is not null && node != list;
                 node = VisualTreeHelper.GetParent(node))
            {
                if (node is ListViewItem)
                    return; // tapped a row - normal selection behavior
            }

            list.SelectedItem = null;
        }

        // Deselect via keyboard: Esc clears the focused list's selection.
        private void List_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                ((ListView)sender).SelectedItem = null;
                e.Handled = true;
            }
        }

        // Saves an edited DesiredValue once the user leaves the box, so it survives an app
        // restart even before the next Apply (Apply itself doesn't touch this - it just
        // reads whatever DesiredValue currently holds).
        private void DesiredValueBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ViewModel.Persist();
        }

        // Confirmation needs a XamlRoot (like the key picker), so it lives here; the actual
        // apply logic stays in MainViewModel.ApplyAllCommand.
        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var totalKeys = ViewModel.WatchedFiles.Sum(f => f.WatchedKeys.Count);
            if (totalKeys == 0)
            {
                ViewModel.LastApplyMessage = "Nothing to apply - no watched keys yet.";
                return;
            }

            var fileCount = ViewModel.WatchedFiles.Count(f => f.WatchedKeys.Count > 0);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Apply changes?",
                Content = $"This will write {totalKeys} key(s) across {fileCount} file(s) to disk.",
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.ApplyAllCommand.ExecuteAsync(null);
            }
        }
    }
}
