# Dungeonmans QoL

Quality-of-life mod for Dungeonmans (Steam, .NET 8). Requires [Dungeonmans ModKit](https://github.com/chibranche/dungeonmans-mod-toolkit) (included in the
`DungeonmansQoL-...-with-ModKit.zip` release). It changes the game through Harmony patches at runtime;
`Dungeonmans.dll` is never modified.

## Features

Companion mods, separate and independent from this one:
[Dungeonmans Poultry](https://github.com/chibranche/dungeonmans-poultry-coop) (the Coop, bird window, breeding fix) and
[Dungeonmans Pay 2 Lose Metaprogression](https://github.com/chibranche/dungeonmans-pay2lose-metaprogression).

**Top bar:** the Academy Directory, QoL and game speed buttons hang from the top of the screen, in the bar ModKit shares between mods. Drag one sideways to change the order; it is remembered.

**Battlemaster, spending Proofs of Stremf**
- `Shift` + click on `+`/`-` (or `Shift` + Left/Right): 10 steps at once.
- `Ctrl` + click (or `Ctrl` + arrow): as many steps as possible. This spends everything you can afford on that stat, or refunds it completely.
- **Presets (3 slots):** set up an allocation, then `Shift+1/2/3` or **[Save]** stores it. On a new hero, `1/2/3` or **[Load]** applies it.
  - A preset stores the points you *added* to each stat and uses them as a ratio. Loading undoes pending changes, then spends every Proof you can afford in that ratio. The stat furthest below its share gets the next point. A preset saved with 100 Proofs therefore still fits when you have 300.
  - Presets are saved to `<game>\Mods\QoL\proof_presets.json`, not to the save game, so they work across heroes and worlds.
- A panel under the dialog shows these shortcuts and the 3 preset slots.


**Academy Directory**
- Press `Home`, or click the **Academy Directory** button at the top of the screen, while you're in the Academy.
- Tabs: Favorites, New hero, Teachers, Turn-ins, Gear, Pets, Other. Each tab lists only what your Academy actually has, with the NPC's picture and a one-line purpose. A help strip at the bottom explains the selected entry.
- **Favorites:** right-click an entry (or click its heart) to pin it to the Favorites tab.
- Click an entry, or select it and press Enter. The hero teleports next to it and bumps into it, which opens its normal menu.
- Keys: Left/Right switch tabs, Up/Down select, Enter goes, Home or Esc closes.
- **[ Prepare hero ]** (or `P` in the Directory) does every new-hero chore at once. Hover it to see what it will do.
  - The Libromancer and Chemistress identify your scrolls and potions. As in the base game, this only works before the hero first leaves the Academy.
  - You learn from every ghost teacher you haven't learned from yet.
  - Unspent Proofs are spent with **preset 1**, if you saved one.
  - Warchest rewards are tossed next to you, and Grand Library bookstands are emptied.
  - Your favorite bird is taken out of the coop, healed and equipped: the one marked with a heart in the Poultry mod if you use it, otherwise the bird your previous hero had equipped.
  - A summary dialog lists what was done. Museum withdrawals aren't automated, because you have to pick the items.

**Options menu (Esc)**
- "Save Changes & Close" and "Save Game and Quit" move to the bottom of the right column, with an empty row above them, so they're harder to misclick.

**Overworld map (M / Tab)**
- The filter buttons are remembered between sessions, in `<game>\Mods\QoL\map_filters.json`. On first use, **Cleared** dungeons start hidden. Click "Cleared" to show them again.
- Click a location icon, or press Enter on the selected one, to auto-walk there. This uses the game's own auto-path, so turns and encounters still happen. It's not a teleport.

**Overworld window** (top right; drag it anywhere, the spot is remembered)
- **Academy items:** shows the Proofs of Stremf and other Academy items you carry. **Go hand them in** walks you to the Academy, then teleports you to the Headmaster and opens his dialog. Pick "I've brought some relics". With no items, the button reads **Back to Academy** and just walks you home.
- **Random nearby dungeon:** Mundane, Adventurous or Dangerous, the game's own difficulty names. It walks you to a random one of the 5 nearest unfinished dungeons of that difficulty.

**Click to walk** (dungeons, towns, Academy): click any tile more than a step away, even one you haven't seen, and the hero walks there with the game's auto-path. It stops when a monster shows up. Works from the `Tab` area map too.

**A pig farm in every world:** the town closest to the Academy always builds Pig Farmin' (the pig vendor) instead of an inn. It applies to towns you haven't entered yet, so new or rerolled worlds.

**Equip best armor:** press `Insert`, use the QoL hub (`End`), or the button shown with the inventory. It wears the best helm, gloves, shoulders, chest, pants and boots in your bags. Pick the armor weight in the hub, or right-click the inventory button to cycle it: Light 'Armor', Medium Armor, Real Armor or Any (helms and gloves have no weight). Pieces are ranked by armor, dodge, block, parry, magic bonus and enchantments. Unidentified magic items, cursed items and gear you can't use yet are skipped, and a piece the game won't take off (cursed, or full bags under the Pay 2 Lose bag cap) stays on. With Pay 2 Lose "all gear stuck", nothing is swapped.

**Fallen heroes:** when a hero dies in a dungeon, a memorial appears on that floor for later heroes. Touching it gives 25% of the XP their level took, a permanent bonus to all stats, every Academy item they carried and, from level 5, an enchanted copy of their weapon or armor (the rest of their gear is lost). The game only marks the dungeon "Unavenged" on the map. Open **Fallen heroes** from the QoL hub or the Gravedigger's entry in the Academy Directory: it lists every memorial still to collect with the hero, dungeon and floor, killer and rewards (hover for details). **Go there** walks you to the dungeon (from the overworld map). On the right floor you get a message, and the memorial gets a gold frame once you have seen its tile.

**Keyboard:** in every QoL window, `Tab` / `Shift+Tab` and the arrow keys move between buttons and `Enter` / `Space` presses them.

**Game speed:** F11, or the speed button at the top right (also in the Options menu), cycles ×1, ×2 and ×3. Animations, movement and turn delays play faster, and the game rules don't change. Held keys also repeat faster.

**Dungeon Sense on magic pools:** when Dungeon Sense tells you a pool is good or bad, the line shows in green or red. The pool then keeps a green or red frame on the map until you drink from it, and the reading is repeated when you step into it again. The pool's effect is also saved with it: in the base game, a reload rolls the pool again.

**Fullscreen: Off / No border / Full Fullness:** the Options menu's Fullscreen entry gets a third value. **No border**
fills the screen as a borderless window: alt-tab is instant and other windows can go on top. **Full Fullness** is the
game's own fullscreen. With the mod installed, turning Fullscreen on gives **No border** first.

**Fix: fullscreen error on alt-tab:** in fullscreen, the base game can pop up a ".NET unhandled exception" box
(DXGI_ERROR_NOT_CURRENTLY_AVAILABLE) at launch or when you alt-tab. The mod catches that one error and switches to
fullscreen once the game window is back.

**Highlight favorite items on the floor:** floor items get the champion outline when their type is on your
favorites list (the list the game uses to auto-lock pickups) or matches a locked item you carry.

**Headmaster: hide full Library:** "I want to give you some books for the Library" disappears at 101 books, when the
blacksmith's "outdid himself" crafting chance (1 % per book, roll 1d100 < books) is guaranteed.

## Settings

Every feature is a switch in **Mod Settings** (title screen bottom left, or the button under the Options window),
under "Dungeonmans QoL". Changes are saved to `<game>\Mods\QoL\config.json` and apply immediately, except
"Map: remember filters" (next launch).


## Install

Download `DungeonmansQoL-<version>-with-ModKit.zip` from the Releases page, extract it into the Dungeonmans game
folder (Steam: right-click the game > Manage > Browse local files) and double-click `ModKit-Install.bat`.
Run it again after a Steam update. Details: `ModKit-README.txt` in the zip.

Log: `<game>\ModKit\modkit.log`, where each QoL part is listed as `[qol] ok` or `[qol] FAIL`.

## Building from source

Needs the .NET 8 SDK, the game, and [Dungeonmans ModKit](https://github.com/chibranche/dungeonmans-mod-toolkit)
installed in the game folder (the project references `<game>\ModKit\DungeonmansModKit.dll`).

```powershell
.\install.ps1 [-GameDir <path>]   # build and copy into <game>\Mods\QoL
.\build-release.ps1               # dist\DungeonmansQoL-<v>-with-ModKit.zip (uses the toolkit's release zip)
```

## License

MIT, see [LICENSE](LICENSE).
