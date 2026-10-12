# Skateboard for Graveyard Keeper 2

A BepInEx mod that lets the keeper ride a skateboard around the graveyard.

![Riding the skateboard](media/skateboard1.png)

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Get on / off | K | L3 (click left stick) |
| Ollie | Space | RT |

You can change these in `BepInEx/config/sean.gk2.skateboard.cfg`. The same file also holds speed, acceleration, coasting, turning, braking, ride height, ollie height and volume.

## Install

**With a mod manager:** get it from Thunderstore using r2modman or the Thunderstore app.

**Manually:**

1. Install [BepInExPack](https://thunderstore.io/c/graveyard-keeper-2/p/BepInEx/BepInExPack/) (BepInEx 5.4.23.x, x64).
2. Put `GK2Skateboard.dll` in `BepInEx/plugins/GK2Skateboard/`.

The mod adds no items and changes nothing in your save.

## How it works

- **Movement:** Harmony patches on `PlayerPhysicalBody` take over the keeper's ground velocity while riding. That's where acceleration, carving, braking and coasting happen.
- **Board graphic:** a sprite under `PlayerView`. It copies the keeper's material and pixel grid, and switches between side and end-on views with the keeper's own 4-way facing.
- **Gamepad:** buttons are read directly over XInput, so they never share a binding with game actions.
- **Sound:** goes through the game's SFX mixer group, so the SFX volume setting applies.

## Build

You need the .NET SDK and Graveyard Keeper 2 with BepInEx installed.

```
dotnet build -c Release
```

The build copies the DLL into the game's `BepInEx/plugins/GK2Skateboard/` folder. Pass `-p:GameDir=...` if the game isn't in the default Steam location.

- **Art:** the board sprites are generated from `tools/make_art.py` into `art/skateboard.png`.
- **Package:** the Thunderstore package files are in `release/pkg/`.

## Credits

All sounds are CC0 recordings from Freesound:

- "SkateRolling.wav" by Lanterr
- "skateboard ollie" by nolimitkid

Links are in `sfx/CREDITS.md`.
