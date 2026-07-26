# ULTRAKILL Split-Screen Mod

Experimental Windows split-screen launcher and BepInEx bridge for **ULTRAKILL**.

> [!IMPORTANT]
> Version 0.1 is a technical prototype. It launches and arranges two ULTRAKILL processes and keeps both Unity instances running in the background. It does **not yet** isolate two controllers or automatically create a multiplayer session.

## What version 0.1 does

- Detects a Steam installation of ULTRAKILL when possible.
- Launches two instances of `ULTRAKILL.exe`.
- Supports vertical and horizontal layouts.
- Moves and resizes both windows automatically.
- Passes separate player/window settings to each process.
- Includes a BepInEx plugin that enables `Application.runInBackground` and reapplies the requested resolution.
- Builds a self-contained Windows x64 launcher and installable ZIP with GitHub Actions.

## What is still required for actual co-op

ULTRAKILL is a single-player game. A multiplayer mod such as **Jaket** is still needed to synchronize players, enemies and levels. The current prototype does not bypass Steam, DRM or account restrictions. Controller isolation and automatic local-lobby joining are planned for later versions.

## Installation

1. Install BepInEx 5 for ULTRAKILL and run the game once.
2. Download the latest `ULTRAKILL-SplitScreen-*.zip` artifact from the repository's **Actions** tab.
3. Copy `BepInEx/plugins/ULTRAKILLSplitScreen/ULTRAKILLSplitScreen.Plugin.dll` into the matching folder in the ULTRAKILL installation.
4. Keep `ULTRAKILLSplitScreen.Launcher.exe` and `splitscreen.json` together.
5. Edit `splitscreen.json` if ULTRAKILL is not detected automatically.
6. Run `ULTRAKILLSplitScreen.Launcher.exe`.

## Configuration

```json
{
  "gameExecutable": "",
  "layout": "vertical",
  "launchDelayMs": 3500,
  "windowReadyTimeoutMs": 30000,
  "borderless": true,
  "playerOneMuted": false,
  "playerTwoMuted": false,
  "extraArguments": ""
}
```

`gameExecutable` may be left empty for automatic Steam detection. Layout accepts `vertical` or `horizontal`.

Command-line overrides:

```powershell
ULTRAKILLSplitScreen.Launcher.exe --game "C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL\ULTRAKILL.exe" --layout vertical
```

Use `--dry-run` to print the detected configuration without launching the game.

## Building locally

Install the .NET 8 SDK, then run:

```powershell
dotnet restore ULTRAKILLSplitScreen.sln
dotnet build ULTRAKILLSplitScreen.sln -c Release
dotnet publish src/Launcher/ULTRAKILLSplitScreen.Launcher.csproj -c Release -r win-x64 --self-contained true
```

The plugin targets `netstandard2.1`, matching the approach used by existing ULTRAKILL BepInEx mods.

## Roadmap

- [x] Two-instance launcher
- [x] Vertical/horizontal window layout
- [x] BepInEx background-running bridge
- [x] GitHub Actions Windows build
- [ ] Controller isolation per instance
- [ ] Automatic Jaket host/join flow
- [ ] Separate saves and BepInEx configuration directories
- [ ] Shared pause and cooperative respawn rules
- [ ] Graphical launcher settings

## Legal

This project does not include ULTRAKILL, BepInEx, Jaket or any copyrighted game files. You must own and install the game separately.
