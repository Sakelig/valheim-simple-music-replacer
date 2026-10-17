Replace Valheim's music with your own, per biome or per game track. Made for playing Sea Shanty 2 on repeat while you're out on the ocean.

No music is included. You add your own files.

## Setup

1. Install with a mod manager, or copy `SeaShanty.dll` into `BepInEx/plugins/SeaShanty/`.
2. Start the game once. This creates `BepInEx/config/SeaShanty/` with an empty `ocean` folder and an `available_music.txt` file.
3. Put `.ogg`, `.wav` or `.mp3` files in a folder, then restart the game.

```
BepInEx/config/SeaShanty/
  ocean/        <- Sea Shanty 2 goes here: loops while you're on the ocean (including sailing)
  meadows/      <- plays in the Meadows
  blackforest/
  combat/       <- replaces the vanilla "combat" music
  sailing/      <- replaces the vanilla sailing music (outside the ocean biome)
```

- **Biome folders** (`meadows`, `blackforest`, `swamp`, `mountain`, `plains`, `mistlands`, `ashlands`, `deepnorth`, `ocean`) loop your music whenever you're in that biome, at any time of day.
- **Any other folder** has to match a Valheim music name, and replaces that music. Every valid name is listed in `available_music.txt`, for example `home`, `combat`, `sailing`, `intro` and `respawn`.
- **Several files in one folder** make a playlist. They play in alphabetical order, or shuffled if `Shuffle` is on.

Modpack authors can also put folders in `BepInEx/plugins/SeaShanty/music/`.

## Config (`BepInEx/config/seachanti.seashanty.cfg`)

| Setting | Default | Description |
|---|---|---|
| Volume | 0.7 | Volume of custom biome music, also scaled by the in-game music volume |
| Shuffle | false | Shuffle playlists |
| OceanReplacesSailingMusic | true | Play the `ocean` music instead of the vanilla sailing music while on the ocean |

Only affects your own client. Other players don't need it installed.
