# IniAnchor

A lightweight Windows desktop app for keeping specific values in `.ini` files pinned to what you want them to be — add the files you care about, pick the keys, set the values you want, and apply them whenever you need to.

## Future Goals (for now)

- **Improve UX**: User interaction logic and flow.
- **Improve UI**: Visual structure of how elements are presented.

## Features

- **Watch a list of `.ini` files.** Add any number of files via a standard file picker; the list persists across restarts.
- **Organize with folders.** A sidebar lists an **All** folder (every entry) plus folders you create, rename, and delete. Folders work like profiles: each has its own list of `.ini` files with its own keys and values, so the same file can be in several folders with different values. Files added while **All** is selected don't belong to any folder. Deleting a folder deletes its entries (never the `.ini` files themselves), after a confirmation.
- **Quick row actions on hover.**
  - Folders: **rename** or **delete**.
  - Files: **open in your default `.ini` editor** (e.g. Notepad) or **remove** from the list.
  - Keys: **revert** to the value the key had when you added it, or **stop watching** it.
- **Pick keys visually, not blind.** For each watched file, open a searchable picker listing every `Key = value` pair actually found in the file, grouped under their section titles — no guessing key names or typos. Search matches section or key names.
- **Set a desired value per key**, editable directly in the app. The value found in the file when you added the key is remembered, so you can always revert to it.
- **Apply a folder or everything.** **Apply folder** writes only the selected folder's keys; **Apply all** writes every folder's keys. A confirmation prompt shows how many keys/entries will be set before anything is written.
  - If several folders set the same key in the same file to different values, the prompt warns about it and the folder **lowest in the sidebar wins**; the others are reported as overridden.
  - Entries for the same file are merged first, so each file is read once and written at most once per Apply.
- **Safe by design:**
  - A key is only ever written if it's **unique** in the file (or within its section). If a key is missing or duplicated, it's skipped and flagged rather than guessed at — your file is never touched ambiguously.
  - Writes are **crash-safe**: each file is written to a temporary file first, then swapped in atomically, so a crash or power loss mid-write can never leave a corrupted `.ini` file.
  - Everything else in the file — comments, spacing, formatting, unrelated keys — is preserved exactly as-is.
  - **Only writes what differs.** Keys that already have the desired value are left alone, and a file where nothing differs isn't written at all — its modified time stays unchanged, apps watching it aren't triggered, and a locked or read-only file that's already correct doesn't cause an error.
- **Clear status per key.** An icon next to each key shows its state in the file — hover it for **Unique**, **Not Found**, or **Duplicate** — plus an error icon (with the actual error message on hover) if the last Apply couldn't write the file. After each Apply, the status line summarizes how many keys were applied, already set, overridden, not found, duplicate, or hit file errors.
- **Easy selection.** Click an empty area of a list, press **Esc**, or **Ctrl+click** the selected row to deselect it. Buttons that need a selection (like **Add key...**) are disabled until there is one.
- **Remembers the window size** between launches.
- **Portable.** No installer, no registry entries, no per-user profile folder. The app's data files — `watchlist.json` (your folders, files, and keys) and `settings.json` (preferences such as window size) — live right next to the `.exe`, so the whole folder can be copied or run from a USB stick.

## Requirements

- Windows 10 (1809+) or Windows 11
- Nothing else to run the published `.exe` — it bundles .NET and the Windows App SDK. Building from source needs the .NET 8 SDK + Visual Studio's WinUI 3 workload.

## Building from source

1. Open `IniAnchor.sln` in Visual Studio.
2. Set `IniAnchor.App` as the startup project.
3. Build and run (F5).

## Publishing a standalone .exe

The app doesn't need to be launched through Visual Studio. To produce a standalone build that runs on its own — no Visual Studio, no installer, and no need for .NET or the Windows App SDK to be pre-installed on the target machine — publish it as a self-contained, single-file app:

```
dotnet publish IniAnchor.App\IniAnchor.App.csproj -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:PublishSingleFile=true -p:WindowsAppSDKSelfContained=true
```

(Swap `win-x64`/`x64` for `win-x86`/`x86` or `win-arm64`/`ARM64` if needed.) The output lands in `IniAnchor.App\bin\win-x64\publish\`: a single, trimmed and compressed `IniAnchor.App.exe` of about 34 MB. Copy it anywhere and double-click it; `watchlist.json` and `settings.json` are created next to it. The `.pdb` files next to it are only debug symbols and don't need to be copied.

You can also publish from Visual Studio itself: right-click `IniAnchor.App` → Publish → Folder, and pick the matching `win-x64`/`win-x86`/`win-arm64` profile.

## Project structure

- **`IniAnchor.Core`** — the actual `.ini` parsing, uniqueness checking, safe writing, apply-pipeline logic, and watch-list/folder rules (saving, apply order, conflicts). Plain C#, no UI dependencies, fully unit tested.
- **`IniAnchor.App`** — the WinUI 3 desktop app (views, view models).
- **`IniAnchor.Core.Tests`** — unit tests for `IniAnchor.Core` (xUnit).

## What it doesn't do (yet)

- No file backups (`.bak`) before writing.
- Can't move an entry between folders — remove it and add it to the other folder instead.
- No scheduled or automatic apply — it only runs when you click Apply.
- Won't create a key that doesn't already exist in the file — it only ever sets values for keys that are already there.
