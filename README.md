# Dungeonmans QoL

Quality-of-life mod for Dungeonmans (Steam, .NET 8). Requires [Dungeonmans ModKit](https://github.com/chibranche/dungeonmans-mod-toolkit) (included in the
`DungeonmansQoL-...-with-ModKit.zip` release). It changes the game through Harmony patches at runtime;
`Dungeonmans.dll` is never modified.

## Features

**Battlemaster, spending Proofs of Stremf**
- `Shift` + click on `+`/`-` (or `Shift` + Left/Right): 10 steps at once.
- `Ctrl` + click (or `Ctrl` + arrow): as many steps as possible. This spends everything you can afford on that stat, or refunds it completely.
- **Presets (3 slots):** set up an allocation, then `Shift+1/2/3` or **[Save]** stores it. On a new hero, `1/2/3` or **[Load]** applies it.
  - A preset stores the points you *added* to each stat and uses them as a ratio. Loading undoes pending changes, then spends every Proof you can afford in that ratio. The stat furthest below its share gets the next point. A preset saved with 100 Proofs therefore still fits when you have 300.
  - Presets are saved to `<game>\Mods\QoL\proof_presets.json`, not to the save game, so they work across heroes and worlds.
- A panel under the dialog shows these shortcuts and the 3 preset slots.

**Professor Hatch (poultry corral)**
- Bumping into Hatch opens the work menu directly. The first-meeting tutorial is unchanged.
- The Ovomansy FAQ that used to sit behind the greeting is now an entry in the work menu.
- The deposit list has a **Deposit all (keeps equipped egg)** side action, and the withdraw list has a **Withdraw all** side action. Press Right or click to reach them.

**Academy Directory**
- Press `Home`, or click the **Academy Directory** button at the top of the screen, while you're in the Academy.
- Tabs: New hero, Turn in & research, Gear & shards, Pets & pigs, Other. Each tab lists only what your Academy actually has, with the NPC's picture and a one-line purpose. A help strip at the bottom explains the selected entry.
- Click an entry, or select it and press Enter. The hero teleports next to it and bumps into it, which opens its normal menu.
- Keys: Left/Right switch tabs, Up/Down select, Enter goes, Home or Esc closes.
- **[ Prepare hero ]** (or `P` in the Directory) does every new-hero chore at once. Hover it to see what it will do.
  - The Libromancer and Chemistress identify your scrolls and potions. As in the base game, this only works before the hero first leaves the Academy.
  - You learn from every ghost teacher you haven't learned from yet.
  - Unspent Proofs are spent with **preset 1**, if you saved one.
  - Warchest rewards are tossed next to you.
  - A summary dialog lists what was done. Museum withdrawals aren't automated, because you have to pick the items.

**Options menu (Esc)**
- "Save Changes & Close" and "Save Game and Quit" move to the bottom of the right column, with an empty row above them, so they're harder to misclick.

**Overworld map (M / Tab)**
- The filter buttons are remembered between sessions, in `<game>\Mods\QoL\map_filters.json`. On first use, **Cleared** dungeons start hidden. Click "Cleared" to show them again.
- Click a location icon, or press Enter on the selected one, to auto-walk there. This uses the game's own auto-path, so turns and encounters still happen. It's not a teleport.

**The Coop (poultry)**
- Open it from Professor Hatch's menu ("Open the Coop"), the Academy Directory (Pets tab), or the poultry status window. Away from the Academy, it only works with the birds and items in your bags.
- **Birds:** every bird with its details: both stat scores as bars, Pluck, Generation, powers with gene counts, genes not learned yet and forgotten powers.
  - One-click Store, Take, Equip, Favorite, Heal all, and Set free (click twice).
  - "forget" next to each power costs 1 Pruning Poultice.
  - Feed list: steaks and feathers from your bags and the Pantry, each showing its effect and the chance to learn its power.
- **Breed:** pick two parents. Before you breed, it shows the egg's exact generation, Pluck and stat scores, and the chance of each power. Both parents are kept.
- **Ascend:** +1 generation for the same bird, paid with N Passionate Poultices. Pluck after = Pluck − ⌊Pluck × 0.5^N⌋, and stats, powers and genes are kept.
- **Pantry:** Academy storage for steaks, Plumage of Power, poultices, rename cards and protection potions. It starts with 10 stacks.
  - It's kept in the game's own save, so it survives hero deaths.
  - Store all, Take (Shift = whole stack), and Render steaks into Feathers (1 / 5 / 25 / 125 by rarity).
- **Feathers:** an Academy currency. You earn 5 for each bird you set free, 5 for each Passionate Poultice you sell, or by rendering steaks.
  - Buy +3 Pantry stacks (25, then 50, 75… Feathers), or a Passionate Poultry Poultice for 50 Feathers. Pruning Poultices can't be bought.
- **Breeding bug fix (switchable):** vanilla breeding overwrites the first parent's gene table with the egg's. With the fix, both parents keep their genes.
- **Favorite bird:** Prepare hero takes it out of the Academy and equips it.

**Poultry status window** (bottom right, above your hero's vitals)
- Shows the equipped bird's name, generation, and HP (or Wounded), with Pluck, stat scores and powers when expanded.
- Click [-]/[+] to collapse or expand it. **Feed** and **Ascend** open the Coop on that bird.

**Overworld window** (top right)
- **Academy items:** shows the Proofs of Stremf and other Academy items you carry. **Go hand them in** walks you to the Academy, then teleports you to the Headmaster and opens his dialog. Pick "I've brought some relics".
- **Random nearby dungeon:** Easy, Even or Hard, compared with your level (threat below level−1, within ±1, or above level+1). It walks you to a random one of the 5 nearest unfinished dungeons of that difficulty.

**Game speed:** F11, or click "Speed xN" at the top left, cycles ×1, ×2 and ×3. Animations, movement and turn delays play faster, and the game rules don't change. Held keys also repeat faster.

**Highlight favorite items on the floor:** floor items get the champion outline when their type is on your
favorites list (the list the game uses to auto-lock pickups) or matches a locked item you carry.

**Headmaster: hide full Library:** "I want to give you some books for the Library" disappears at 101 books, when the
blacksmith's "outdid himself" crafting chance (1 % per book, roll 1d100 < books) is guaranteed.

## Settings

Every feature is a switch in **Mod Settings** (title screen bottom left, or the button under the Options window),
under "Dungeonmans QoL". Changes are saved to `<game>\Mods\QoL\config.json` and apply immediately, except
"Map: remember filters" (next launch).

Your Pantry, Feathers and favorite bird are stored in the game's own save.

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
