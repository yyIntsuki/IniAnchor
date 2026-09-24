# IniAnchor

A lightweight Windows desktop app for keeping specific values in `.ini` files pinned to what you want them to be — add the files you care about, pick the keys, set the values you want, and apply them whenever you need to.

## Features

- **Watch a list of `.ini` files.** Add any number of files via a standard file picker; the list persists across restarts.
- **Organize with folders.** A sidebar lists an **All** folder (every entry) plus folders you create, rename, and delete. Folders work like profiles: each has its own list of `.ini` files with its own keys and values, so the same file can be in several folders with different values. Files added while **All** is selected don't belong to any folder, and in **All** each file shows which folder it belongs to. Deleting a folder deletes its entries (never the `.ini` files themselves), after a confirmation.
- **Focused key editing.** Selecting a file slides its keys panel in over the file list. From there, a drop-down switches between the folder's files without leaving the panel, and the close button slides it back out.
- **Quick row actions on hover.**
  - Folders: **rename** or **delete**.
  - Files: **open in your default `.ini` editor** (e.g. Notepad) or **remove** from the list.
  - Keys: **revert** to the value the key had when you added it, or **stop watching** it.
- **Pick keys visually, not blind.** For each watched file, open a searchable picker listing every `Key = value` pair actually found in the file, grouped under their section titles — no guessing key names or typos. Search matches section or key names.
- **Set a desired value per key**, editable directly in the app. The value found in the file when you added the key is remembered, so you can always revert to it.
- **Apply a folder or everything.** **Apply folder** writes only the selected folder's keys; **Apply all** writes every folder's keys. Both sit at the bottom of the sidebar. A confirmation prompt shows how many keys/entries will be set before anything is written.
  - If several folders set the same key in the same file to different values, the prompt warns about it and the folder **lowest in the sidebar wins**; the others are reported as overridden.
  - Entries for the same file are merged first, so each file is read once and written at most once per Apply.
- **Safe by design:**
  - A key is only ever written if it's **unique** in the file (or within its section). If a key is missing or duplicated, it's skipped and flagged rather than guessed at — your file is never touched ambiguously.
  - Writes are **crash-safe**: each file is written to a temporary file first, then swapped in atomically, so a crash or power loss mid-write can never leave a corrupted `.ini` file.
  - Everything else in the file — comments, spacing, formatting, unrelated keys — is preserved exactly as-is.
  - **Only writes what differs.** Keys that already have the desired value are left alone, and a file where nothing differs isn't written at all — its modified time stays unchanged, apps watching it aren't triggered, and a locked or read-only file that's already correct doesn't cause an error.
- **Clear status per key.** An icon next to each key shows its state in the file — hover it for **Unique**, **Not Found**, or **Duplicate** — plus an error icon (with the actual error message on hover) if the last Apply couldn't write the file. After each Apply, a summary above the apply buttons shows how many keys were applied, already set, overridden, not found, duplicate, or hit file errors.
- **Easy selection.** Click an empty area of a list, press **Esc**, or **Ctrl+click** the selected row to deselect it. Buttons that need a selection (like **Add key...**) are disabled until there is one.
- **Remembers the window size** between launches. The window has a minimum size of 1000×600 and starts at that size on first launch.
- **Portable.** No installer, no registry entries, no per-user profile folder. The app's data files — `watchlist.json` (your folders, files, and keys) and `settings.json` (preferences such as window size) — live right next to the `.exe`, so the whole folder can be copied or run from a USB stick.

## Requirements

- Windows 10 (1809+) or Windows 11
- Nothing else to run the published `.exe` — it bundles .NET and the Windows App SDK. Building from source needs the .NET 8 SDK + Visual Studio's WinUI 3 workload.

## Building from source

1. Open `IniAnchor.sln` in Visual Studio.
2. Set `IniAnchor.App` as the startup project.
3. Build and run (F5).

## Project structure

- **`IniAnchor.Core`** — the actual `.ini` parsing, uniqueness checking, safe writing, apply-pipeline logic, and watch-list/folder rules (saving, apply order, conflicts). Plain C#, no UI dependencies, fully unit tested.
- **`IniAnchor.App`** — the WinUI 3 desktop app (views, view models).
- **`IniAnchor.Core.Tests`** — unit tests for `IniAnchor.Core` (xUnit).
