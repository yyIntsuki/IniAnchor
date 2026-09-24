using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IniAnchor.App.ViewModels;
using IniAnchor.App.Views;
using IniAnchor.Core.Models;
using IniAnchor.Core.Persistence;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.Storage.Pickers;
using Windows.System;
using Windows.Graphics;

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

            // Draw the app into the title bar area so it shares the window's Mica background
            // instead of a separate white bar. AppTitleBar (XAML) is the drag region; "Tall"
            // makes the system's caption buttons match its 48px height.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

            RestoreWindowSize();
            AppWindow.Closing += (_, _) => SaveWindowSize();

            // Minimum size needs the display scale, which is only known once content is loaded.
            // Re-applied on XamlRoot.Changed, which also fires when the window moves to a
            // display with different scaling.
            RootGrid.Loaded += (_, _) =>
            {
                ApplyMinimumWindowSize();

                // No saved size (e.g. first launch): start at exactly the minimum size.
                if (!_restoredSavedSize)
                    AppWindow.Resize(MinimumWindowSize());

                RootGrid.XamlRoot.Changed += (_, _) => ApplyMinimumWindowSize();
            };

            // The keys panel opens/closes with the selected entry (see SetKeysPanelOpen).
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.HasSelectedFile))
                    SetKeysPanelOpen(ViewModel.HasSelectedFile);
            };
        }

        // --- Keys fly-in panel ---
        // Open = an entry is selected. Closing (close button, Esc/empty-space click in the file
        // list, switching folders, removing the entry) always goes through clearing SelectedFile,
        // so there's a single source of truth. Only animates when open/closed actually changes,
        // so switching entries via the panel's drop-down doesn't replay the slide.

        private bool _keysPanelOpen;

        private void SetKeysPanelOpen(bool open)
        {
            if (open == _keysPanelOpen)
                return;

            _keysPanelOpen = open;
            var width = ContentArea.ActualWidth;

            if (open)
            {
                KeysPanelTransform.X = width; // start off to the right, avoids a one-frame flash
                KeysPanel.Visibility = Visibility.Visible;
            }

            var slide = new DoubleAnimation
            {
                From = open ? width : 0,
                To = open ? 0 : width,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn }
            };
            Storyboard.SetTarget(slide, KeysPanelTransform);
            Storyboard.SetTargetProperty(slide, "X");

            var storyboard = new Storyboard();
            storyboard.Children.Add(slide);
            storyboard.Completed += (_, _) =>
            {
                // Re-check: the panel may have been reopened while it was sliding out.
                if (!_keysPanelOpen)
                    KeysPanel.Visibility = Visibility.Collapsed;
            };
            storyboard.Begin();
        }

        private void CloseKeysPanelButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SelectedFile = null;
        }

        // --- Minimum window size ---
        // In effective (DPI-independent) pixels, so it looks the same at 100% or 150% scaling.

        private const int MinWindowWidth = 1000;
        private const int MinWindowHeight = 600;

        // PreferredMinimumWidth/Height and AppWindow sizes are physical pixels, and Windows
        // doesn't rescale the minimum for display scaling (known WinUI issue), so convert here.
        private SizeInt32 MinimumWindowSize()
        {
            var scale = RootGrid.XamlRoot?.RasterizationScale ?? 1.0;
            return new SizeInt32(
                (int)Math.Ceiling(MinWindowWidth * scale),
                (int)Math.Ceiling(MinWindowHeight * scale));
        }

        private void ApplyMinimumWindowSize()
        {
            if (AppWindow.Presenter is not OverlappedPresenter presenter)
                return;

            var min = MinimumWindowSize();
            presenter.PreferredMinimumWidth = min.Width;
            presenter.PreferredMinimumHeight = min.Height;

            // Grow a window that's currently smaller, e.g. from an old saved size.
            // Skipped while maximized/minimized.
            if (presenter.State != OverlappedPresenterState.Restored)
                return;

            var size = AppWindow.Size;
            if (size.Width < min.Width || size.Height < min.Height)
                AppWindow.Resize(new SizeInt32(Math.Max(size.Width, min.Width), Math.Max(size.Height, min.Height)));
        }

        // --- Remembered window size ---
        // Stored in settings.json next to the .exe, like watchlist.json (portable, §3.5).

        private readonly SettingsStore _settingsStore = new(Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory,
            "settings.json"));

        private void RestoreWindowSize()
        {
            var settings = _settingsStore.Load();

            if (settings.WindowWidth is int width && settings.WindowHeight is int height &&
                width >= 400 && height >= 300) // ignore nonsense/tiny values
            {
                AppWindow.Resize(new SizeInt32(width, height));
                _restoredSavedSize = true;
            }
        }

        // False when there was no saved size - the window then starts at the minimum size.
        private bool _restoredSavedSize;

        private void SaveWindowSize()
        {
            // Don't save a maximized/minimized size - keep the last normal size instead.
            if (AppWindow.Presenter is OverlappedPresenter { State: not OverlappedPresenterState.Restored })
                return;

            // Load first so any other settings in the file are kept.
            var settings = _settingsStore.Load();
            settings.WindowWidth = AppWindow.Size.Width;
            settings.WindowHeight = AppWindow.Size.Height;
            _settingsStore.Save(settings);
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
        //
        // A tap is "on a row" when the tapped element's DataContext is one of our item view
        // models (everything inside a row inherits the row's item). Deliberately NOT a
        // visual-tree walk checking "is ListViewItem": that worked in Debug but not in the
        // trimmed publish build, so every single click selected and then instantly
        // deselected the row. Our own view-model types are safe to type-check when trimmed.
        private void List_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var context = (e.OriginalSource as DependencyObject)
                ?.GetValue(FrameworkElement.DataContextProperty);

            if (context is WatchedFileViewModel or WatchedKeyViewModel)
                return; // tapped a row - normal selection behavior

            ((ListView)sender).SelectedItem = null;
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

        // --- Hover buttons on rows (open/remove on files, remove on keys) ---

        // The row's item, found via DataContext - the same trim-safe lookup as List_Tapped.
        private static object? RowItem(object sender) =>
            (sender as DependencyObject)?.GetValue(FrameworkElement.DataContextProperty);

        private static void SetRowHovered(object sender, bool hovered)
        {
            switch (RowItem(sender))
            {
                case FolderViewModel folder: folder.IsHovered = hovered; break;
                case WatchedFileViewModel file: file.IsHovered = hovered; break;
                case WatchedKeyViewModel key: key.IsHovered = hovered; break;
            }
        }

        private void Row_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            SetRowHovered(sender, true);
        }

        private void Row_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            // Pointer events bubble up from children: moving from the text onto a button
            // raises PointerExited here even though the pointer is still on the row. Only
            // hide the buttons once the pointer has actually left the row's bounds.
            if (sender is UIElement row)
            {
                var p = e.GetCurrentPoint(row).Position;
                var size = row.ActualSize;
                if (p.X >= 0 && p.Y >= 0 && p.X < size.X && p.Y < size.Y)
                    return;
            }

            SetRowHovered(sender, false);
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowItem(sender) is WatchedFileViewModel file)
                ViewModel.OpenFileInEditor(file);
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowItem(sender) is WatchedFileViewModel file)
                ViewModel.RemoveFileCommand.Execute(file);
        }

        private void ToggleKeyCommentedOutButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowItem(sender) is WatchedKeyViewModel key)
            {
                key.ToggleCommentedOut(); // what the user wants - the file changes on the next Apply
                ViewModel.Persist();
            }
        }

        private void RevertKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowItem(sender) is WatchedKeyViewModel key)
            {
                key.RevertToOriginal(); // value and on/off state - the file changes on the next Apply
                ViewModel.Persist();
            }
        }

        private void RemoveKeyButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowItem(sender) is WatchedKeyViewModel key)
                ViewModel.RemoveKeyCommand.Execute(key);
        }

        // --- Folders (sidebar) ---
        // Name prompts and the delete confirmation need a XamlRoot, so they live here; the
        // actual folder logic stays in MainViewModel.

        private async void NewFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var name = await PromptForFolderNameAsync("New folder", ViewModel.SuggestNewFolderName(), "Create");
            if (name is not null)
                ViewModel.CreateFolder(name);
        }

        private async void RenameFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowItem(sender) is not FolderViewModel folder)
                return;

            var name = await PromptForFolderNameAsync("Rename folder", folder.Name, "Rename");
            if (name is not null)
                ViewModel.RenameFolder(folder, name);
        }

        private async void DeleteFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowItem(sender) is not FolderViewModel folder)
                return;

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Delete folder?",
                Content = $"Delete '{folder.Name}' and its {ViewModel.EntryCount(folder)} file entr(ies) with their keys? " +
                          "The ini files on disk are not changed.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close // deleting is destructive - Enter cancels
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                ViewModel.DeleteFolder(folder);
        }

        /// <summary>Asks for a folder name. Returns the trimmed name, or null on Cancel.</summary>
        private async Task<string?> PromptForFolderNameAsync(string title, string initialName, string primaryText)
        {
            var nameBox = new TextBox { Text = initialName, PlaceholderText = "Folder name" };

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = title,
                Content = nameBox,
                PrimaryButtonText = primaryText,
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            // No empty names; start with the text selected so typing replaces it.
            nameBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(nameBox.Text);
            nameBox.Loaded += (_, _) =>
            {
                nameBox.Focus(FocusState.Programmatic);
                nameBox.SelectAll();
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary
                ? nameBox.Text.Trim()
                : null;
        }

        // --- Apply ---
        // Confirmation needs a XamlRoot (like the key picker), so it lives here; the actual
        // apply logic stays in MainViewModel.ApplyAsync.

        private async void ApplyFolderButton_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmAndApplyAsync(ViewModel.FilesToApply(allFolders: false),
                $"the folder '{ViewModel.SelectedFolder?.Name}'");
        }

        private async void ApplyAllButton_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmAndApplyAsync(ViewModel.FilesToApply(allFolders: true), "all folders");
        }

        private async Task ConfirmAndApplyAsync(List<WatchedFile> files, string scope)
        {
            var totalKeys = files.Sum(f => f.WatchedKeys.Count);
            if (totalKeys == 0)
            {
                ViewModel.LastApplyMessage = $"Nothing to apply - no watched keys in {scope}.";
                return;
            }

            var entryCount = files.Count(f => f.WatchedKeys.Count > 0);
            var message = $"This will set {totalKeys} watched key(s) across {entryCount} file entr(ies) in {scope}. " +
                          "Keys that are already as you want them are left untouched.";

            // Only possible across folders (profiles setting the same key differently).
            var conflicts = Watchlist.CountConflicts(files);
            if (conflicts > 0)
            {
                message += $"\n\n{conflicts} key(s) are set differently by different folders " +
                           "(different values, or on in one and commented out in another). " +
                           "The folder lowest in the sidebar wins.";
            }

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Apply changes?",
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                PrimaryButtonText = "Apply",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await ViewModel.ApplyAsync(files);
        }
    }
}
