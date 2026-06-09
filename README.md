# SnipDock

SnipDock is a lightweight local Windows snippet manager for prompts, commands, code snippets, notes, and other reusable text. It is designed for quick save, search, copy, and organization from a floating desktop entry point.

Current release: **v0.1.0-beta**. This is a beta build intended for early public testing.

## Features

- Windows floating ball and system tray entry.
- Global hotkey to open the management panel.
- Prompt, command, snippet, and note item types.
- Search by title and tags only. Item content is not searched.
- Type filters, tag filters, favorites, pinned items, recent usage, and usage counts.
- Create, edit, delete, and copy items.
- Non-blocking toast feedback after copy.
- Optional auto-hide after copying.
- Light and dark themes with accent colors.
- Settings panel.
- Optional Windows startup launch.
- Local JSON storage with safe writes.
- `prompts.json.bak` safety backup plus automatic `backups/` snapshots.
- JSON import and export.
- Backup restore.
- Single-instance protection.
- Compatibility migration from the previous PromptShelf configuration.

Command items are copy-only. SnipDock does not execute saved commands.

## Screenshots

Screenshots will be added before the stable release. Suggested screenshots:

- Floating ball on the Windows desktop.
- Management panel with filters and item details.
- Settings panel showing theme, storage, backup, and startup options.

## Requirements

- Windows
- .NET 9 SDK for development
- Visual Studio 2022 is recommended for WPF development

## Run

```powershell
dotnet run --project src/SnipDock.App/SnipDock.App.csproj
```

## Build

```powershell
dotnet restore SnipDock.sln
dotnet build SnipDock.sln
dotnet test SnipDock.sln
```

## Publish

```powershell
dotnet publish src/SnipDock.App/SnipDock.App.csproj -c Release -r win-x64 --self-contained false -o .\publish\SnipDock
```

The publish output is intentionally ignored by Git.

## Data Storage

On first launch, SnipDock asks the user to choose a local storage directory. SnipDock stores user data in that selected directory:

- `prompts.json`: main item data file.
- `prompts.json.bak`: safety backup of the previous main data file.
- `settings.json`: local app settings for the selected data directory.
- `logs/`: runtime logs.
- `backups/`: automatic and manual backup snapshots.

Bootstrap configuration is stored at:

```text
%APPDATA%\SnipDock\bootstrap.json
```

User data files, logs, backups, and publish output are excluded by `.gitignore`.

## Migration From PromptShelf

SnipDock was previously named PromptShelf. For compatibility, SnipDock can read the old bootstrap configuration from:

```text
%APPDATA%\PromptShelf\bootstrap.json
```

If the new SnipDock bootstrap file does not exist and the old PromptShelf bootstrap file is found, SnipDock copies the bootstrap settings to:

```text
%APPDATA%\SnipDock\bootstrap.json
```

The old PromptShelf configuration is not deleted. User-selected storage paths are not renamed or moved, and the main data file remains `prompts.json` for now.

## Privacy

SnipDock is local-first. It does not provide cloud sync and does not send snippet content to any server. Data remains in the local directory selected by the user.

Users should avoid committing their personal data directory to Git. The repository `.gitignore` excludes the known SnipDock data files and backup folders.

## Known Limitations

- Windows only.
- No cloud sync.
- No command execution. Command items are copied to the clipboard only.
- Search covers titles and tags only, not item content.
- Screenshots and packaged installers are not included yet.

## Roadmap

- Prepare signed or packaged release artifacts.
- Add public screenshots and release notes.
- Continue stability testing around backup restore, import/export, startup launch, and storage path changes.
- Improve documentation for keyboard shortcuts and backup workflows.
- Review UI text and localization before a stable release.
