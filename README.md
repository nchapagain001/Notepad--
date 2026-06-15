<img src="assets/icon.png" alt="notepad-- icon" width="96" align="left" />

# notepad--

> A calm, anxiety-free Notepad replacement for Windows.

`notepad--` is the opposite of Notepad++: less, not more. A plain-text scratchpad that **autosaves silently**, starts clean, and never asks if you'd like to save before closing.

![build](../../actions/workflows/build.yml/badge.svg)

<br clear="left"/>

## Download

Grab the latest `NotepadMinus.exe` from the [**Releases**](../../releases/latest) page. It's a single self-contained file — no installer, no .NET runtime needed. Drop it on your Desktop and double-click.

> **First run:** Windows SmartScreen may show *"Windows protected your PC"* because the exe isn't code-signed. Click **More info → Run anyway**. One time per machine.

## Features

- **Tabbed editor** — create, edit, close, switch.
- **Silent autosave** every 10 seconds. No save prompts, ever. `Ctrl+S` to flush instantly.
- **Plain `.txt` files** in `Documents\NotepadMinus\` — back up with anything, search with `findstr`.
- **Auto-naming** from the first non-empty line + date: `Meeting with Aria 2026-06-14.txt`.
- **Find & Replace** (`Ctrl+F` / `Ctrl+H`) — VS Code-style: incremental, regex with backrefs, match case, whole word, replace one or all.
- **Themes**: System / Light / Sepia / Dim / Dark.
- **Fonts**: Sans / Serif / Mono presets, plus `Ctrl+scroll` to zoom.
- **Collapsible sidebar** with newest-first note list and unified search across filenames and content.
- **Optional line numbers** in a left gutter — `View → Show Line Numbers`.
- **Workspace launch**: `NotepadMinus.exe C:\some\folder` opens that folder as the workspace.
- **Multi-extension support**: `.txt`, `.md`, `.log`, `.json`, `.xml`, `.csv`, `.cs`, `.py`, and more — original encoding and line endings preserved.
- **No telemetry**, no network, no login.

### Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+T` | New tab |
| `Ctrl+W` | Close tab |
| `Ctrl+Shift+T` | Reopen last closed tab |
| `Ctrl+N` | New window |
| `Ctrl+O` | Open file… |
| `Ctrl+S` | Save now |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Next / previous tab |
| `Ctrl+1` … `Ctrl+9` | Jump to tab N |
| `Ctrl+F` | Find |
| `Ctrl+H` | Find & Replace |
| `F3` / `Shift+F3` | Next / previous match |
| `Alt+C` / `Alt+W` / `Alt+R` | Toggle Match Case / Whole Word / Regex (in Find) |
| `Ctrl+B` | Toggle sidebar |
| `Ctrl+,` | Open `config.json` as a tab |
| `Ctrl+=` / `Ctrl+-` / `Ctrl+0` | Zoom in / out / reset |

## Configuration

Settings live at `%USERPROFILE%\Documents\NotepadMinus\config.json`. Delete the file to start fresh. All keys are optional — missing ones fall back to defaults.

```json
{
  "storage":  { "notesFolder": "%USERPROFILE%\\Documents\\NotepadMinus\\notes" },
  "autosave": { "intervalSeconds": 10 },
  "startup":  { "promptOnLaunch": true, "defaultChoice": "StartFresh" },
  "editor": {
    "fontSize":       12,
    "font":           "Mono",
    "theme":          "System",
    "wordWrap":       false,
    "showLineNumber": false,
    "showSidebar":    false
  }
}
```

| Theme | Font preset | `defaultChoice` |
| --- | --- | --- |
| `System`, `Light`, `Sepia`, `Dim`, `Dark` | `Sans`, `Serif`, `Mono` | `StartFresh`, `OpenYesterday` |

## Build from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```powershell
git clone https://github.com/<you>/notepad-minus.git
cd notepad-minus
.\build-test.cmd
```

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

Windows Notepad has no autosave, so writing freely in it carries a low-grade anxiety: every tab is one accidental close away from gone. Bigger tools (OneNote, VS Code, Obsidian) solve that but trade it for clutter, prompts, login, sync, and decisions. `notepad--` aims for the middle: a small editor that saves what you type and otherwise stays out of the way.

## Contributing

This is a personal tool. Issues and small PRs welcome; the design bar for v1 is intentionally narrow — features that add prompts, popups, or "just one more setting" are likely to be declined.

## License

[MIT](./LICENSE)
