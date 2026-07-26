# Architecture

Version 0.2 uses one ULTRAKILL process per local player instead of trying to create several `NewMovement`, camera, weapon and HUD singletons inside one Unity process.

## Launcher

The Windows launcher:

1. Detects `ULTRAKILL.exe` from Steam or configuration.
2. Calculates one to four screen tiles.
3. Applies `fit` aspect correction so each game window keeps the configured aspect ratio.
4. Starts each process with player, gamepad and Jaket settings in `UKSS_*` environment variables.
5. Removes window borders and repeatedly reapplies the position while Unity starts.

Automatic layouts use two vertical tiles for two players and a 2×2 grid for three or four players. Manual vertical, horizontal and grid layouts remain available.

## BepInEx bridge

The plugin enables background execution and maintains the requested window resolution. It contains two runtime-only compatibility layers:

- `GamepadIsolation`: discovers Unity Input System `Gamepad` devices by reflection, enables the assigned controller and disables the other controllers in that process.
- `JaketBridge`: waits for Jaket to initialize, invokes its public `CreateLobby` or `JoinLobby` methods by reflection, and shares the lobby code through a local text file.

No Jaket or Unity Input System binary is linked or redistributed by this project.

## Controller model

Controller assignments are zero-based and are expected to use the same enumeration order in each process. Xbox controllers normally appear directly. PlayStation and Switch controllers may appear natively or through Steam Input, DS4Windows, BetterJoy or another XInput adapter.

The bridge only filters Unity Input System gamepads. Keyboard/mouse isolation and games reading controllers exclusively through a different API require a lower-level input hook such as the approach used by split-screen launchers like Nucleus Co-op.

## Jaket model

Jaket's lobby is Steam-backed. The host writes the created lobby code and clients reconstruct Jaket's `Steamworks.Data.Lobby` value before calling `JoinLobby`.

Separate local processes using one Steam account are not separate Jaket members. True multi-player synchronization therefore normally requires distinct Steam identities/sessions. The bridge does not bypass Steam or emulate accounts.

## Remaining limitations

- Steam may reject or restrict additional local sessions.
- Saves and most BepInEx configuration files are shared.
- Keyboard and mouse are not isolated.
- Shared pause, respawn and per-player save profiles are not implemented.
