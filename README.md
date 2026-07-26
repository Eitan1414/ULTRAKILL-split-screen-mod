# ULTRAKILL Split-Screen Mod

Experimental Windows split-screen launcher and BepInEx bridge for **ULTRAKILL**, supporting one to four local instances.

> [!IMPORTANT]
> This project uses multiple ULTRAKILL processes. Jaket supplies multiplayer synchronization, while this project handles launching, windows, controller routing and optional lobby automation. Jaket still normally needs a distinct usable Steam identity for each player process.

## Version 0.3 highlights

- Press **Ctrl+P while playing solo** to open an in-game split-screen popup.
- Keep the current game as player 1 and launch only the additional players.
- Choose 2, 3 or 4 total player windows.
- Choose automatic controller discovery, Xbox/XInput, PlayStation PS4/PS5, or Nintendo Switch Pro mapping.
- See the controller names detected by Unity before launching.
- Put the entire split-screen layout on the primary monitor or the second monitor.
- Optionally fill the whole target monitor, or keep every window at 16:9.
- Correct active camera aspect ratios after windows are resized.
- Automatically create a private Jaket lobby for player 1 and pass its code to the other instances.

## Installation

1. Install **BepInEx 5** for ULTRAKILL and start the game once.
2. Install **Jaket** separately if you want the players to share the same level.
3. Download the latest `ULTRAKILL-SplitScreen-v0.3.0-win-x64` artifact from **Actions**.
4. Extract the **entire ZIP directly into the ULTRAKILL game folder**.
5. Confirm that these files exist:

```text
ULTRAKILL/
├── ULTRAKILLSplitScreen.Launcher.exe
├── splitscreen.json
└── BepInEx/
    └── plugins/
        └── ULTRAKILLSplitScreen/
            └── ULTRAKILLSplitScreen.Plugin.dll
```

The launcher must remain in the ULTRAKILL root folder for the in-game `Ctrl+P` shortcut.

## Starting from a solo game

1. Start ULTRAKILL normally.
2. Enter a level or remain in the menu.
3. Press **Ctrl+P**.
4. Choose the total number of players: 2, 3 or 4.
5. Choose the controller mapping profile:
   - automatic search;
   - Xbox / XInput;
   - PlayStation PS4 / PS5;
   - Nintendo Switch Pro.
6. Choose the primary or second monitor.
7. Enable **Fill the target screen** to occupy the complete monitor, or leave it disabled to keep 16:9 windows.
8. Select **Activate split-screen**.

The current process becomes player 1. The launcher attaches to its window and starts players 2–4 as required.

## Second-monitor mode

The popup can move the complete split-screen layout onto monitor 2. Windows uses the monitor's real desktop coordinates, so monitors positioned to the left, right, above or below the primary display are supported.

If monitor 2 is requested but Windows cannot find it, the launcher safely falls back to the primary monitor and prints a warning.

## Controller mapping

The plugin isolates one Unity Input System gamepad per process.

- `auto` accepts all gamepads in Unity's detected order and is recommended for mixed controller types.
- `xbox` matches Xbox and XInput names.
- `playstation` matches DualShock, DualSense, PlayStation, Sony and common Wireless Controller names.
- `switch` matches Nintendo, Switch, Pro Controller and Joy-Con names.

Connect every controller before activating split-screen. Steam Input, DS4Windows or a compatible adapter may be needed when a controller is not exposed to Unity as a gamepad.

## Configuration

```json
{
  "gameExecutable": "",
  "players": 2,
  "layout": "auto",
  "aspectMode": "fit",
  "targetAspectRatio": "16:9",
  "targetMonitor": 0,
  "windowGapPixels": 4,
  "launchDelayMs": 2500,
  "windowReadyTimeoutMs": 30000,
  "borderless": true,
  "controllerIsolation": true,
  "controllerProfile": "auto",
  "controllerAssignments": [0, 1, 2, 3],
  "mutedPlayers": [],
  "jaket": {
    "enabled": true,
    "autoHostJoin": true,
    "hostPlayer": 1,
    "lobbyCodeFile": "jaket-lobby-code.txt",
    "startDelaySeconds": 8,
    "timeoutSeconds": 60
  },
  "extraArguments": ""
}
```

`targetMonitor` is zero-based in JSON: `0` is the primary monitor and `1` is the second monitor.

## Launcher command line

```powershell
ULTRAKILLSplitScreen.Launcher.exe --players 4 --monitor 2 --controller-profile auto --fill-screen
```

Attach an existing solo process manually:

```powershell
ULTRAKILLSplitScreen.Launcher.exe --attach-pid 12345 --players 3 --monitor 2
```

Available options include `--players`, `--layout`, `--aspect-mode`, `--monitor`, `--controller-profile`, `--attach-pid`, `--fill-screen` and `--dry-run`.

## Important Jaket limitation

Jaket's lobby code can be automated, but its members are identified through Steam. Multiple ULTRAKILL processes running under the same Steam account may not become distinct Jaket players. This project does not bypass Steam, DRM or account restrictions.

## Building locally

Install the .NET 8 SDK, then run:

```powershell
dotnet restore ULTRAKILLSplitScreen.sln
dotnet build ULTRAKILLSplitScreen.sln -c Release
dotnet publish src/Launcher/ULTRAKILLSplitScreen.Launcher.csproj -c Release -r win-x64 --self-contained true
```

## Legal

This project does not include ULTRAKILL, BepInEx, Jaket or copyrighted game files. You must own and install the game separately.
