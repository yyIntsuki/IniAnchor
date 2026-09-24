using System.Collections.Generic;
using System.Collections.ObjectModel;
using IniAnchor.App.ViewModels;

// Trimming support for WinUI: every generic collection this app hands to a WinUI control
// (ListView.ItemsSource) must be listed here. The CsWinRT source generator can't detect these on
// its own because x:Bind passes them to WinUI typed as plain 'object'. Without this, the trimmed
// published app throws a NullReferenceException inside CsWinRT when setting ItemsSource, and the
// list silently stays empty (it still works in Debug, which isn't trimmed).
// If a new collection type is ever bound to a control, add it here too.
[assembly: WinRT.GeneratedWinRTExposedExternalType(typeof(ObservableCollection<FolderViewModel>))]
[assembly: WinRT.GeneratedWinRTExposedExternalType(typeof(ObservableCollection<WatchedFileViewModel>))]
[assembly: WinRT.GeneratedWinRTExposedExternalType(typeof(ObservableCollection<WatchedKeyViewModel>))]
[assembly: WinRT.GeneratedWinRTExposedExternalType(typeof(List<IniKeyPickItem>))]
