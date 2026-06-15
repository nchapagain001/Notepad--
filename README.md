<img src="assets/icon.png" alt="notepad-- icon" width="96" align="left" />

# notepad--

> Notepad, minus the friction.

The opposite of Notepad++: less, not more. A plain-text scratchpad for Windows that autosaves silently, starts clean, and never asks if you'd like to save before closing.

![build](../../actions/workflows/build.yml/badge.svg)

<br clear="left"/>

## Download

Grab the latest `NotepadMinus.exe` from the [**Releases**](../../releases/latest) page. Single self-contained file — no installer, no .NET runtime needed.

> **First run:** Windows SmartScreen may show *"Windows protected your PC"* because the exe isn't code-signed. Click **More info → Run anyway**. One time per machine.

## Features

- **Tabbed editor** — create, edit, close, switch.
- **Silent autosave** every 10 seconds. `Ctrl+S` to flush instantly.
- **Plain `.txt` files** in `%USERPROFILE%\Documents\NotepadMinus\notes\` — back up with anything, search with `findstr`, delete a file to delete the note.
- **Auto-naming** from the first non-empty line + date: `Meeting with Aria 2026-06-14.txt`.
- **Find & Replace** (`Ctrl+F` / `Ctrl+H`) — VS Code-style: incremental, regex with backrefs, match case, whole word.
- **Collapsible sidebar** with newest-first list and unified search across filenames and content.

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.


| Script | What it does |
| --- | --- |
| `init.cmd` | Verify the .NET 8 SDK and restore NuGet packages. |
| `build.cmd` | Release build of the solution. |
| `build-test.cmd` | Build, then run all unit tests. |
| `clean.cmd` | Delete `out\` and any stray `bin\` / `obj\` under `src\`. |

To run the app from source: `dotnet run --project src/NotepadMinus`

To produce your own self-contained exe locally:

```powershell
dotnet publish src/NotepadMinus -c Release -r win-x64 --self-contained
```

The CI release workflow (`.github/workflows/release.yml`) does the same thing automatically when you push a tag like `v1.0.0`, and attaches the resulting `NotepadMinus.exe` to a GitHub Release.

## Where your notes are saved

Plain `.txt` files in `%USERPROFILE%\Documents\NotepadMinus\notes\`. No database, no sidecar metadata. Delete a file → the note is gone. Drop a file in → it shows up in the sidebar.

## Why this exists

Windows Notepad doesn't auto-save and reopens with every tab from last week. This does the opposite: silent auto-save to a plain-text folder, clean slate on launch, yesterday's work one click away.

## Contributing

This is a personal tool. Issues and small PRs welcome; the design bar for v1 is intentionally narrow — features that add prompts, popups, or "just one more setting" are likely to be declined.

## License

[MIT](./LICENSE)
