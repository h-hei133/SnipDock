# SnipDock

SnipDock is a lightweight local Windows snippet manager for quickly saving, searching, and copying prompts, commands, code snippets, notes, and frequently used information.

Current release: **v0.1.0-beta**. This is the first beta release for early public testing.

中文文档：[README.zh-CN.md](./README.zh-CN.md)

## Positioning

SnipDock is a **local-first** desktop utility. It does not provide cloud sync, does not upload user data, and is not a command runner.

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
- Compatibility migration from the previous PromptShelf configuration

## Screenshots

Screenshots will be added under `docs/images/`.

![image_1](./docs/images/image_1.png)

![image_2](./docs/images/image_2.png)

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

Do not commit your personal data directory.

## Migration From PromptShelf

SnipDock was previously named PromptShelf. To protect existing users, SnipDock keeps compatibility migration logic:

- Reads the old `%APPDATA%\PromptShelf\bootstrap.json`
- Migrates to `%APPDATA%\SnipDock\bootstrap.json` when the new config does not exist
- Does not delete the old PromptShelf configuration
- Does not move the user-selected data directory
- Keeps the `prompts.json` file name for compatibility

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
