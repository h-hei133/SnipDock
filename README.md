# SnipDock

SnipDock is a lightweight local Windows snippet manager for quickly saving, searching, and copying prompts, commands, code snippets, notes, and frequently used information.

Current release: **v0.1.0-beta**. This is an early beta release for public testing and feedback.

Chinese README: [README.zh-CN.md](./README.zh-CN.md)

## Positioning

SnipDock is a **local-first** Windows desktop utility. It stores data in local files, does not upload user data to the cloud, and is not a command runner.

Command items are copy-only. SnipDock never automatically executes saved commands.

## Features

- Prompt / command / code snippet / note item types
- Search by title and tags
- Type filters and tag filters
- Favorites, pinned items, recent usage, and usage counts
- Global hotkey
- System tray
- Windows floating ball
- Light / Dark themes
- Multiple accent colors
- Create, edit, delete, and copy
- Non-blocking toast after copy
- Optional auto-hide after copy
- JSON import / export
- Automatic backups and backup restore
- Windows startup launch
- Local JSON safe writes
- Compatible migration from previous PromptShelf configuration

## Screenshots

![screenshot 1](./docs/images/截图1.png)

![screenshot 2](./docs/images/截图2.png)

## Download and Run

If you download a published exe package, run:

```text
SnipDock.App.exe
```

The current package is framework-dependent. If the app does not start, install the .NET 9 Desktop Runtime first.

## Run From Source

```powershell
dotnet run --project src/SnipDock.App/SnipDock.App.csproj
```

## Build

```powershell
dotnet build SnipDock.sln
```

## Test

```powershell
dotnet test SnipDock.sln
```

## Publish

```powershell
dotnet publish src/SnipDock.App/SnipDock.App.csproj -c Release -r win-x64 --self-contained false -o .\publish\SnipDock
```

The `publish/` folder is ignored by Git and should not be committed.

## Data Storage

On first launch, SnipDock asks the user to choose a local data directory. User items, settings, backups, and runtime logs are stored locally.

- Bootstrap config: `%APPDATA%\SnipDock\bootstrap.json`
- Bootstrap logs: `%LOCALAPPDATA%\SnipDock\logs\`
- User data: the directory selected by the user
- Main data file: `prompts.json`
- Safety backup: `prompts.json.bak`
- Automatic backups: `backups\`
- App logs: `logs\`
- Local settings: `settings.json`

## Migration From PromptShelf

SnipDock keeps compatibility with the previous PromptShelf bootstrap configuration:

- Reads legacy `%APPDATA%\PromptShelf\bootstrap.json`
- Migrates bootstrap settings to `%APPDATA%\SnipDock\bootstrap.json`
- Does not delete legacy configuration
- Does not move or rename the user-selected data directory
- Keeps the `prompts.json` filename for compatibility

## Privacy

- Data is stored locally
- No cloud upload
- No automatic command execution
- Command items are copied, not run
- Logs should not record item content or clipboard content

## Known Limitations

- Windows only
- No cloud sync
- No installer or auto-update yet
- Search covers titles and tags only, not item content
- Command items are copy-only
- Beta release, more real-world stability testing is still needed

## Roadmap

- Add official screenshots and demo notes
- Complete GitHub Release attachments and checksums
- Prepare a more user-friendly installer
- Continue validating backup restore, import/export, startup launch, and storage path switching
- Continue polishing documentation and UI copy

## License

SnipDock is licensed under the [MIT License](./LICENSE).
