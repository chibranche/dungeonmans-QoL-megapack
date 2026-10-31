using DungeonmansModKit;

namespace DungeonmansQoL;

// Per-feature on/off switches, shown in ModKit's Mod Settings menu and saved in Mods\QoL\config.json.
// Every patch checks its switch each time it runs, so toggling takes effect immediately (map filters: at next launch).
internal static class Config
{
	public sealed class Feature
	{
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
		new Feature { Key = ProofsBulk, Label = "Proofs: Shift x10 / Ctrl max",
			Description = "Battlemaster stat dialog: Shift + click/arrow changes 10 points, Ctrl goes as far as possible." },
		new Feature { Key = ProofsPresets, Label = "Proofs: presets",
			Description = "3 saved stat splits under the Battlemaster dialog. Shift+1-3 saves, 1-3 loads (spends every Proof in that ratio)." },
		new Feature { Key = HatchSkipGreeting, Label = "Hatch: skip greeting",
			Description = "Bumping Professor Hatch opens his work menu directly. His Ovomansy FAQ stays available in that menu." },
		new Feature { Key = HatchBulk, Label = "Hatch: deposit / withdraw all",
			Description = "Side buttons in Hatch's bird lists to move every bird at once (the equipped egg stays with you)." },
		new Feature { Key = AcademyDirectory, Label = "Academy Directory",
			Description = "Home key / top button in the Academy: list of everyone and everything, click to go there and open it." },
		new Feature { Key = HeroPrep, Label = "Prepare hero button",
			Description = "In the Academy Directory: identify scrolls & potions, learn from all teachers, spend Proofs with preset 1, empty the Warchest." },
		new Feature { Key = MapFilters, Label = "Map: remember filters",
			Description = "The overworld map remembers its filter buttons; cleared dungeons start hidden. Takes effect at next launch." },
		new Feature { Key = MapTravel, Label = "Map: click to travel",
			Description = "Click a location on the overworld map (or Enter) to auto-walk there. Normal turns and encounters still happen." },
		new Feature { Key = Coop, Label = "The Coop (poultry window)",
			Description = "One window for all birds: details, feeding, breeding preview, Ascend (+1 generation), Pantry storage and Feathers." },
		new Feature { Key = BreedingFix, Label = "Fix: breeding gene bug",
			Description = "Vanilla breeding overwrites the first parent's gene table with the egg's. With this fix, both parents keep their genes." },
		new Feature { Key = PoultryHud, Label = "Poultry status window",
			Description = "Small window above your hero status: equipped bird's HP, stats, powers; click to expand; Feed opens the Coop." },
		new Feature { Key = ProofReminder, Label = "Overworld: Academy items reminder",
			Description = "On the overworld, shows the Academy items you carry (Proofs of Stremf, relics...) with a button to walk to the Academy and hand them in." },
		new Feature { Key = RandomDungeon, Label = "Overworld: random nearby dungeon",
			Description = "On the overworld, pick a difficulty (easy / even / hard) and auto-walk to a random nearby unfinished dungeon of that difficulty." },
		new Feature { Key = GameSpeed, Label = "Game speed x1/x2/x3",
			Description = "F11 (or click the label top left) cycles x1 / x2 / x3: animations, movement and turn delays play faster." },
		new Feature { Key = HighlightFavorites, Label = "Highlight favorite items on the floor",
			Description = "Outlines floor items of the same type as your locked/favorite items (same outline as champions)." },
		new Feature { Key = HideMaxedTurnIns, Label = "Headmaster: hide full Library",
			Description = "Hides 'give books for the Library' at 101 books, when the blacksmith's crafting crit (1d100 < books) is guaranteed." },
		new Feature { Key = OptionsLayout, Label = "Options: save buttons at bottom",
			Description = "Moves 'Save Changes & Close' and 'Save Game and Quit' to the bottom of the options window, away from other entries." },
	};

	private static ModSettings settings;

	// Called once by QoLMod.Load: every feature becomes a switch in ModKit's Mod Settings menu.
	public static void Register(ModSettings s)
	{
		settings = s;
		foreach (Feature f in Features)
		{
			s.AddToggle(f.Key, f.Label, f.Description);
		}
	}

	public static bool On(string key) => settings?.On(key) ?? true;

	public static void Toggle(string key) => settings?.Toggle(key);
}
