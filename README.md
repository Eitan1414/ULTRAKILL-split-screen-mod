# ULTRAKILL Split-Screen Mod

Experimental Windows split-screen launcher and BepInEx bridge for **ULTRAKILL**.

## Version 0.2 features

- Launches **one to four** ULTRAKILL processes.
- Automatic two-player vertical layout and three/four-player 2×2 grid.
- Vertical, horizontal and grid layout overrides.
- New `fit` aspect mode that keeps every game window at a real 16:9 ratio instead of stretching it.
- Per-process controller isolation through Unity's Input System.
- Configurable controller assignment for players 1–4.
- Automatic Jaket host/join bridge when Jaket is installed.
- Steam installation detection, borderless positioning and background execution.
- Self-contained Windows x64 launcher and installable ZIP built by GitHub Actions.

> [!IMPORTANT]
> ULTRAKILL is a single-player game. This project starts separate game processes; Jaket is still responsible for synchronizing players, enemies and levels. Multiple local Jaket clients generally need **distinct Steam identities**. This project does not bypass Steam, DRM or account restrictions.

## Controller compatibility

The bridge selects a different Unity Input System `Gamepad` for each process and disables the other gamepads inside that process.

Expected support:

- Xbox 360 / Xbox One / Xbox Series controllers: normally detected directly as XInput gamepads.
- PlayStation 4 / PlayStation 5 controllers: supported when Unity, Steam Input or DS4Windows exposes them as a gamepad.
- Nintendo Switch Pro Controller and Joy-Con adapters: supported when Steam Input, BetterJoy or another adapter exposes them as a gamepad.
- Generic controllers: supported when Windows/Unity reports them as a `Gamepad`.

The order is zero-based. With this configuration, player 1 gets the first detected gamepad, player 2 the second, and so on:

```json
"controllerAssignments": [0, 1, 2, 3]
```

The BepInEx log lists the detected gamepads. If a PlayStation or Switch controller is not detected, enable Steam Input or use a trusted XInput adapter before starting the launcher.

## Jaket compatibility

Install Jaket normally into ULTRAKILL's `BepInEx/plugins` directory. The split-screen bridge waits for Jaket to initialize, asks the host process to create a private lobby, writes the lobby code to a temporary text file and asks the other processes to join it.

The integration uses Jaket's public runtime methods through reflection, so the project does not include or redistribute Jaket code.

Known limitation: multiple processes using the same Steam account are not separate Jaket members. For genuine two-to-four-player synchronization, every process must have a distinct usable Steam identity/session. If automatic joining is unavailable in your setup, set `autoHostJoin` to `false` and join manually through Jaket.

## Installation

1. Install BepInEx 5 for ULTRAKILL and run the game once.
2. Install Jaket into the same BepInEx installation if cooperative synchronization is wanted.
3. Download the latest `ULTRAKILL-SplitScreen-*.zip` artifact from the repository's **Actions** tab.
4. Copy the ZIP's `BepInEx` folder into the ULTRAKILL folder.
5. Keep `ULTRAKILLSplitScreen.Launcher.exe` and `splitscreen.json` together.
6. Connect all controllers before launching.
7. Run `ULTRAKILLSplitScreen.Launcher.exe`.

## Configuration

```json
{
  "gameExecutable": "",
  "players": 2,
  "layout": "auto",
  "aspectMode": "fit",
  "targetAspectRatio": "16:9",
  "windowGapPixels": 4,
  "launchDelayMs": 2500,
  "windowReadyTimeoutMs": 30000,
  "borderless": true,
  "controllerIsolation": true,
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

### Layouts

- `auto`: vertical for two players; 2×2 grid for three or four players.
- `vertical`: all windows placed in columns.
- `horizontal`: all windows placed in rows.
- `grid`: 2×2 grid for three/four players.

### Aspect modes

- `fit`: preserves `targetAspectRatio` and centers each window inside its split-screen tile. This prevents the stretched image from version 0.1. Empty space around a two-player window is expected because two 16:9 pictures cannot completely fill one 16:9 monitor without cropping or distortion.
- `stretch`: fills the complete tile, matching the old behavior.

A target ratio can be written as `16:9`, `4:3`, `21:9` or a decimal value.

### Command-line overrides

```powershell
ULTRAKILLSplitScreen.Launcher.exe --players 4 --layout grid --aspect-mode fit
```

Other options:

```text
--game <path>
--players <1-4>
--layout <auto|vertical|horizontal|grid>
--aspect-mode <fit|stretch>
--dry-run
```

## Building locally

Install the .NET 8 SDK, then run:

```powershell
dotnet restore ULTRAKILLSplitScreen.sln
dotnet build ULTRAKILLSplitScreen.sln -c Release
dotnet publish src/Launcher/ULTRAKILLSplitScreen.Launcher.csproj -c Release -r win-x64 --self-contained true
```

The plugin targets `netstandard2.1` and avoids a compile-time dependency on Jaket or Unity Input System by detecting both at runtime.

## Current limitations

- Same-account Steam instances are not distinct Jaket players.
- Controller isolation depends on ULTRAKILL receiving controllers through Unity Input System. Controllers converted to XInput through Steam Input/DS4Windows/BetterJoy are the most reliable fallback.
- Keyboard and mouse are not isolated between processes.
- Saves and most BepInEx configuration files are still shared.
- Shared pause and cooperative respawn rules are not yet implemented.

## Legal

This project does not include ULTRAKILL, BepInEx, Jaket, controller drivers or copyrighted game files. You must own and install the game separately.
