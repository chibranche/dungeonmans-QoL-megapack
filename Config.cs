using DungeonmansModKit;

namespace DungeonmansQoL;

// Per-feature on/off switches, shown in ModKit's Mod Settings menu and saved in Mods\QoL\config.json.
// Every patch checks its switch each time it runs, so toggling takes effect immediately (map filters: at next launch).
internal static class Config
{
	public sealed class Feature
	{
		public string Group;
		public string Key;
		public string Label;
		public string Description;
		public bool DefaultOn = true;
	}

	public const string Proofs = "proofs";
	public const string FullscreenFix = "fullscreen_fix";
	public const string AcademyDirectory = "academy_directory";
	public const string HeroPrep = "hero_prep";
	public const string MapFilters = "map_filters";
	public const string MapTravel = "map_travel";
	public const string OptionsLayout = "options_layout";
	public const string ProofReminder = "proof_reminder";
	public const string RandomDungeon = "random_dungeon";
	public const string GameSpeed = "game_speed";
	public const string HighlightFavorites = "highlight_favorites";
	public const string HideMaxedTurnIns = "hide_maxed_turnins";
	public const string ClickTravel = "click_travel";
	public const string PigFarm = "pig_farm";
	public const string AutoEquip = "auto_equip";
	public const string PoolSense = "pool_sense";
	public const string FallenHeroes = "fallen_heroes";

	public static readonly Feature[] Features =
	{
		F("Academy", AcademyDirectory, "Academy Directory", "Home key or the top-right button in the Academy: every NPC and station, click to go there."),
		F("Academy", HeroPrep, "Prepare hero button", "One click: identify scrolls & potions, learn from teachers, spend Proofs (preset 1), Warchest, bookstands, favorite bird."),
		F("Academy", Proofs, "Proof of Stremf QoL", "Battlemaster stats: Shift = 10 points, Ctrl = as many as possible. Shift+1-3 saves a stat split, 1-3 spends every Proof in that ratio."),
		F("Academy", HideMaxedTurnIns, "Hide 'give books' at 101 books", "The Headmaster stops offering the Library option once crafting crits are guaranteed."),
		F("Overworld", MapTravel, "Map: click to travel", "Click a map location (or Enter) to auto-walk there."),
		F("Overworld", MapFilters, "Map: remember filters", "Filters are kept; cleared dungeons start hidden. Next launch."),
		F("Overworld", ProofReminder, "Academy items reminder", "Shows the Academy items you carry, with a button to go hand them in."),
		F("Overworld", PigFarm, "A pig farm in every world", "The town closest to the Academy always has Pig Farmin' (the pig vendor). New or rerolled worlds."),
		F("Overworld", RandomDungeon, "Random nearby dungeon", "Mundane / Adventurous / Dangerous: walk to a random nearby unfinished dungeon."),
		F("Dungeons", ClickTravel, "Click to walk", "Click any tile (even unexplored) or a spot on the Tab map: the hero walks there and stops if a monster shows up."),
		F("Dungeons", PoolSense, "Dungeon Sense on magic pools", "A good feeling shows in green, bad news in red, and the pool keeps a green / red frame until you drink."),
		F("Dungeons", FallenHeroes, "Fallen heroes", "QoL hub or the Gravedigger in the Directory: every memorial left to collect (dungeon, floor, rewards) with Go there. Memorials get a gold frame."),
		F("Dungeons", HighlightFavorites, "Outline favorite floor items", "Floor items matching your locked / favorite items get the champion outline."),
		F("General", FullscreenFix, "Fix: fullscreen error on alt-tab", "The base game can show a .NET error box (DXGI NOT_CURRENTLY_AVAILABLE) at launch or alt-tab in fullscreen. Fixed: it waits until the window is back, then switches."),
		F("General", AutoEquip, "Equip best armor", "Insert key, the QoL hub or the inventory button: wears your best helm, gloves and body armor of the weight you pick (Light / Medium / Real / Any)."),
		F("General", GameSpeed, "Game speed x1 / x2 / x3", "Speed button top right, F11, or the Options menu. Animations and turn delays play faster."),
		F("General", OptionsLayout, "Options: save buttons at bottom", "Moves 'Save Changes & Close' and 'Save Game and Quit' away from other entries."),
	};

	private static Feature F(string group, string key, string label, string description, bool defaultOn = true) =>
		new Feature { Group = group, Key = key, Label = label, Description = description, DefaultOn = defaultOn };

	private static ModSettings settings;

	// Called once by QoLMod.Load: every feature becomes a switch in ModKit's Mod Settings menu.
	public static void Register(ModSettings s)
	{
		settings = s;
		foreach (Feature f in Features)
		{
			s.AddToggle(f.Key, f.Label, f.Description, f.DefaultOn, f.Group);
		}
	}

	public static bool On(string key) => settings?.On(key) ?? true;

	public static void Toggle(string key) => settings?.Toggle(key);
}
