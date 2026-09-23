# IniAnchor

A lightweight Windows desktop app for keeping specific values in `.ini` files pinned to what you want them to be — add the files you care about, pick the keys, set the values you want, and apply them whenever you need to.

## Future Goals (for now)

- **Improve UX**: User interaction logic and flow.
- **Improve UI**: Visual structure of how elements are presented.

## Features

- **Watch a list of `.ini` files.** Add any number of files via a standard file picker; the list persists across restarts.
- **Pick keys visually, not blind.** For each watched file, open a searchable picker showing every `[Section] Key = value` pair actually found in the file — no guessing key names or typos.
- **Set a desired value per key**, editable directly in the app.
- **One-click global Apply.** A single button writes every watched key's desired value into every watched file at once, with a confirmation prompt showing how many keys/files will be touched before anything is written.
- **Safe by design:**
  - A key is only ever written if it's **unique** in the file (or within its section). If a key is missing or duplicated, it's skipped and flagged rather than guessed at — your file is never touched ambiguously.
  - Writes are **crash-safe**: each file is written to a temporary file first, then swapped in atomically, so a crash or power loss mid-write can never leave a corrupted `.ini` file.
  - Everything else in the file — comments, spacing, formatting, unrelated keys — is preserved exactly as-is.
- **Clear status per key**: after any Apply, each watched key shows whether it was applied, not found, ambiguous (duplicate), or hit a file error (with the actual error message on hover).
- **Portable.** No installer, no registry entries, no per-user profile folder. The app's data file (`watchlist.json`) lives right next to the `.exe`, so the whole folder can be copied or run from a USB stick.

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

(Swap `win-x64`/`x64` for `win-x86`/`x86` or `win-arm64`/`ARM64` if needed.) The output lands in `IniAnchor.App\bin\win-x64\publish\`: a single, trimmed and compressed `IniAnchor.App.exe` of about 34 MB. Copy it anywhere and double-click it; `watchlist.json` is created next to it. The `.pdb` files next to it are only debug symbols and don't need to be copied.

You can also publish from Visual Studio itself: right-click `IniAnchor.App` → Publish → Folder, and pick the matching `win-x64`/`win-x86`/`win-arm64` profile.

## Project structure

- **`IniAnchor.Core`** — the actual `.ini` parsing, uniqueness checking, safe writing, and apply-pipeline logic. Plain C#, no UI dependencies, fully unit tested.
- **`IniAnchor.App`** — the WinUI 3 desktop app (views, view models).
- **`IniAnchor.Core.Tests`** — unit tests for `IniAnchor.Core` (xUnit).

## What it doesn't do (yet)

- No file backups (`.bak`) before writing.
- No multiple "profiles" of watched files.
- No scheduled or automatic apply — it only runs when you click Apply.
- Won't create a key that doesn't already exist in the file — it only ever sets values for keys that are already there.
