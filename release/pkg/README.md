# Skateboard

The keeper finally gets a set of wheels. Hop on a skateboard and roll around the graveyard, the village and everywhere in between.

![Riding the skateboard](https://raw.githubusercontent.com/scheissgeist/gk2-skateboard/main/media/skateboard1.png)

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Get on / off | K | L3 (click left stick) |
| Ollie | Space | RT |

The gamepad buttons were chosen because Graveyard Keeper 2 doesn't use them, so they won't trigger interactions with nearby objects.

## How it rides

- Push to reach about twice your walking speed.
- The board carves toward wherever you steer.
- Push the opposite way to brake. Let go to coast to a stop.
- The keeper stands on the deck instead of walking.
- The wheels roll, pop and clack. The sound follows the game's SFX volume.

## Install

### With a mod manager (easiest)
1. Install [r2modman](https://thunderstore.io/package/ebkr/r2modman/) or the Thunderstore app, and pick Graveyard Keeper 2.
2. Search for **Skateboard** and click Install. BepInEx comes with it.
3. Start the game from the mod manager. Press **K** in game to ride.

### Manual
1. **Find the game folder.** In Steam, right-click Graveyard Keeper 2, then Manage, then Browse local files. It's the folder with `GraveyardKeeper2.exe`.
2. **Install BepInEx.**
   - Download [BepInExPack](https://thunderstore.io/c/graveyard-keeper-2/p/BepInEx/BepInExPack/) and open the zip.
   - Copy everything inside its `BepInExPack` folder into the game folder: the `BepInEx` folder, `winhttp.dll` and `doorstop_config.ini`.
3. **Run the game once, then close it.** This creates the `BepInEx/plugins` folder.
4. **Add the mod.**
   - Download `GK2Skateboard.dll` from the [latest release](https://github.com/scheissgeist/gk2-skateboard/releases/latest).
   - Put it in `BepInEx/plugins/GK2Skateboard/` (create that folder if it isn't there).
5. **Check it loaded.** Start the game and open `BepInEx/LogOutput.log`. It should contain `Skateboard 1.0.0 loaded`. Load a save and press **K**.

### Uninstall
Delete the `BepInEx/plugins/GK2Skateboard` folder. The mod changes nothing in your save.

To remove BepInEx completely, also delete `BepInEx/`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` from the game folder.

## Config

The config file is `BepInEx/config/sean.gk2.skateboard.cfg`. It's created on first launch. You can change:

- keys and pad buttons
- top speed, acceleration, coasting, turning and braking
- ride height and ollie height
- sound volume

## Notes

- The mod adds no items and changes nothing in your save. Removing it is safe.
- The board is drawn on your screen only. In co-op, other players see you standing still while you glide.
- The ollie is visual only, so you can't jump over obstacles yet.
- Tested on the Steam build of Graveyard Keeper 2 (Windows).

## Credits

All sounds are CC0 recordings from Freesound:

- "SkateRolling.wav" by Lanterr
- "skateboard ollie" by nolimitkid

See CREDITS.md for links.

Source: https://github.com/scheissgeist/gk2-skateboard
