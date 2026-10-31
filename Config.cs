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
	}

	public const string ProofsBulk = "proofs_bulk";
	public const string ProofsPresets = "proofs_presets";
	public const string HatchSkipGreeting = "hatch_skip_greeting";
	public const string HatchBulk = "hatch_bulk";
	public const string AcademyDirectory = "academy_directory";
	public const string HeroPrep = "hero_prep";
	public const string MapFilters = "map_filters";
	public const string MapTravel = "map_travel";
	public const string OptionsLayout = "options_layout";
	public const string Coop = "coop";
	public const string BreedingFix = "breeding_fix";
	public const string PoultryHud = "poultry_hud";
	public const string ProofReminder = "proof_reminder";
	public const string RandomDungeon = "random_dungeon";
	public const string GameSpeed = "game_speed";
	public const string HighlightFavorites = "highlight_favorites";
	public const string HideMaxedTurnIns = "hide_maxed_turnins";

	public static readonly Feature[] Features =
	{
		F("Academy", AcademyDirectory, "Academy Directory", "Home key or the top-right button in the Academy: every NPC and station, click to go there."),
		F("Academy", HeroPrep, "Prepare hero button", "One click: identify scrolls & potions, learn from teachers, spend Proofs (preset 1), Warchest, bookstands, favorite bird."),
		F("Academy", ProofsBulk, "Proofs: Shift x10 / Ctrl max", "Battlemaster stats: Shift = 10 points, Ctrl = as many as possible."),
		F("Academy", ProofsPresets, "Proofs: 3 presets", "Shift+1-3 saves a stat split, 1-3 spends every Proof in that ratio."),
		F("Academy", HideMaxedTurnIns, "Hide 'give books' at 101 books", "The Headmaster stops offering the Library option once crafting crits are guaranteed."),
		F("Poultry", Coop, "The Coop window", "Birds, feeding, breeding preview, Ascend (+1 generation), Pantry and Feathers."),
		F("Poultry", PoultryHud, "Bird status window", "Equipped bird: HP, stats, powers. Drag it anywhere; click - / + to fold."),
		F("Poultry", BreedingFix, "Fix: breeding gene bug", "Vanilla overwrites the first parent's genes with the egg's. Fixed: both parents keep theirs."),
		F("Poultry", HatchSkipGreeting, "Hatch: skip greeting", "Bumping Hatch opens his work menu directly."),
		F("Poultry", HatchBulk, "Hatch: deposit / withdraw all", "One button to move every bird (your equipped egg stays)."),
		F("Overworld", MapTravel, "Map: click to travel", "Click a map location (or Enter) to auto-walk there."),
		F("Overworld", MapFilters, "Map: remember filters", "Filters are kept; cleared dungeons start hidden. Next launch."),
		F("Overworld", ProofReminder, "Academy items reminder", "Shows the Academy items you carry, with a button to go hand them in."),
		F("Overworld", RandomDungeon, "Random nearby dungeon", "Easy / Even / Hard: walk to a random nearby unfinished dungeon."),
		F("Dungeons", HighlightFavorites, "Outline favorite floor items", "Floor items matching your locked / favorite items get the champion outline."),
		F("General", GameSpeed, "Game speed x1 / x2 / x3", "Speed button top left, F11, or the Options menu. Animations and turn delays play faster."),
		F("General", OptionsLayout, "Options: save buttons at bottom", "Moves 'Save Changes & Close' and 'Save Game and Quit' away from other entries."),
	};

	private static Feature F(string group, string key, string label, string description) =>
		new Feature { Group = group, Key = key, Label = label, Description = description };

	private static ModSettings settings;

	// Called once by QoLMod.Load: every feature becomes a switch in ModKit's Mod Settings menu.
	public static void Register(ModSettings s)
	{
		settings = s;
		foreach (Feature f in Features)
		{
			s.AddToggle(f.Key, f.Label, f.Description, group: f.Group);
		}
	}

	public static bool On(string key) => settings?.On(key) ?? true;

	public static void Toggle(string key) => settings?.Toggle(key);
}
