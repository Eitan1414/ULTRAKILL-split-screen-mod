# Prototype architecture

Version 0.1 intentionally uses two ULTRAKILL processes instead of trying to create two local players inside one Unity process.

## Launcher

The Windows launcher detects `ULTRAKILL.exe`, calculates two rectangles from the primary screen resolution, starts both processes with Unity window arguments, and then applies borderless window positioning through `user32.dll`.

## BepInEx bridge

Each process inherits `UKSS_*` environment variables. The BepInEx plugin reads those variables, enables `Application.runInBackground`, reapplies windowed resolution during startup, and can mute one instance.

## Multiplayer synchronization

The prototype does not reproduce Jaket's networking. A later integration layer must either call a supported Jaket API or maintain a compatible local-session implementation. The project must not copy AGPL code without respecting its license.

## Known limitations

- Both processes can currently see the same controllers.
- Steam may reject or restrict a second local session.
- Saves and BepInEx configuration are shared.
- Lobby creation and joining are manual.
