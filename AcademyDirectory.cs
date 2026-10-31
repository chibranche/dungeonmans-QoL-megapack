using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// "Academy Directory": a clickable panel listing everyone/everything you can interact with in the Academy,
// grouped in tabs. Clicking an entry teleports the hero next to it and bumps into it, which opens the
// same menu as walking up to it. Open with Home, or with the button at the top of the screen.
// Entries are discovered from the live level, so only things your Academy actually has are listed.
internal static class AcademyDirectory
{
	private const Keys ToggleKey = Keys.Home;

	private sealed class Kind
	{
		public string Category;
		public string Title;    // null = use the actor's own name
		public string Purpose;  // one line, shown on the card
		public string Help;     // longer reminder, shown at the bottom for the selected card
		public Func<dmActor, bool> Match;
		public bool OnePerKind; // e.g. the 4 research-desk tiles are one desk
		public Func<string> Status; // optional live suffix for the purpose line
		public Func<string> Use;    // optional: run this instead of walking up to the actor; returns a log message
	}

	private sealed class Entry
	{
		public Kind Kind;
		public dmActor Actor;
		public string Title => Kind.Title ?? Actor.Name;
		public string Key => Kind.Category + "|" + Title;
	}

	private const string FavoritesTab = "Favorites";
	private static readonly string[] Categories = { FavoritesTab, "New hero", "Teachers", "Turn-ins", "Gear", "Pets", "Other" };

	private static readonly string[] CategoryHelp =
	{
		"Your favorites. Right-click any entry (or click its heart) to add or remove it.",
		"Things to do once per hero, before leaving: spend Proofs, get potions and scrolls identified, empty the war chest.",
		"Retired heroes (level 10+) and avenged ghosts. Each teaches every new hero once: a full mastery, a mastery point or a hammer.",
		"Bring back relics, books and Mysterious Items. They upgrade the Academy for every future hero.",
		"Persistent gear: Armory metal and the Wargear vault, Star Shards and the Stellar Anvil.",
		"Battle Poultry, bird colours, and the piggy bank savings shop.",
		"Daily Danger, secrets and the way out.",
	};

	private static bool Is<T>(dmActor a) => a is T;
	private static bool Arc(dmActor a, string archetype) => a.ArchetypeName == archetype;

	private static readonly Kind[] Kinds =
	{
		new Kind { Category = "New hero", Title = "Battlemaster", Match = Is<NPCAcademyBattlemaster>,
			Purpose = "Spend Proofs of Stremf on base stats.",
			Help = "Every Proof the Academy ever received becomes stat points for each new hero. Stremf also raises max HP, and that bonus grows with level. Shift = x10, Ctrl = max, keys 1-3 load a preset." },
		new Kind { Category = "New hero", Title = "Chemistress (Alchemy Lab)", Match = Is<NPCAcademyAlchemist>,
			Purpose = "Pre-identify potions known by the Academy.",
			Help = "Alchemy Lab upgrades (from returned artifacts) let her identify potions up to a higher tier for every new hero. Ask her once per hero." },
		new Kind { Category = "New hero", Title = "Libromancer (Library)", Match = Is<NPCAcademyLibrarian>,
			Purpose = "Pre-identify scrolls known by the Academy.",
			Help = "Books returned to the Headmaster raise the Library rank, which pre-identifies scrolls up to that tier. Ask once per hero." },
		new Kind { Category = "Teachers", Title = null, Match = Is<NPCAcademyGhostTeacher>,
			Purpose = "Free mastery points from a past hero.",
			Help = "Heroes who retired at level 10+ (or ghosts you avenged) teach here. Accept their lesson to start with free mastery points." },
		new Kind { Category = "New hero", Title = "Grand Library Bookstands", OnePerKind = true,
			Match = a => a is dmContainer c && c.iBookstandID != 0,
			Purpose = "Take the free book from every bookstand not looted yet.",
			Status = () => HeroPrep.UnlootedBookstands.Count + " left",
			Use = () =>
			{
				int n = HeroPrep.LootBookstands();
				return n == 0 ? "All Grand Library bookstands are already empty for this hero."
					: "Took the book from " + n + " Grand Library bookstand" + (n > 1 ? "s" : "") + ".";
			},
			Help = "The Grand Library's 4 bookstands each give one item, refilled for every new hero. Clicking takes all remaining ones at once (items go to your bags). Also part of Prepare hero." },
		new Kind { Category = "New hero", Title = "Academy Warchest", Match = a => a is dmAcademyWarchest,
			Purpose = "Rewards from Academy upgrades, for the current hero.",
			Help = "When you upgrade the Academy by returning artifacts or books, rewards are sometimes placed in here. Unlike upgrades, these help YOU right now." },

		new Kind { Category = "Turn-ins", Title = "Headmaster", Match = Is<NPCAcademyHeadmaster>,
			Purpose = "Turn in relics and books, identify your bags, retire.",
			Help = "Hand over Academy relics (they open new wings) and Library books. He can identify everything in your bags. Later: retirement (level 10+) and starting a new overworld while keeping the Academy." },
		new Kind { Category = "Turn-ins", Title = "Curator (Museum)", Match = Is<NPCAcademyCurator>,
			Purpose = "Monster Knowledge, and researched Mysterious Items.",
			Help = "Check Monster Knowledge progress. Mysterious Items you turn in are researched when a hero dies or retires; later heroes can then withdraw them here." },
		new Kind { Category = "Turn-ins", Title = "Cartography Desk", Match = Is<dmUsableResearchDesk>, OnePerKind = true,
			Purpose = "Adventure maps, Terraforming Beacons, Dungeon Dissolver.",
			Help = "Spend research on discovering Adventure Maps of a chosen level, buying Terraforming Beacons or brewing the Dungeon Dissolver. Requires the Foomwardens wing." },

		new Kind { Category = "Gear", Title = "Armorer (Armory)", Match = a => a is NPCAcademyArmorer || a is NPCAcademyCheater,
			Purpose = "Metalworking with Armory metal, Wargear vault.",
			Help = "Metal caches you bring back stock the Armory. Craft and upgrade gear, and store renowned or legendary gear in the Wargear vault for later heroes." },
		new Kind { Category = "Gear", Title = "Stellar Anvil", Match = Is<dmStellarAnvil>, OnePerKind = true,
			Purpose = "Spend Star Shards to enchant or forge gear.",
			Help = "1 Star Shard per enchant; 2 for a legendary weapon. Shards drop from tier-2+ champions." },
		new Kind { Category = "Gear", Title = "Star Orb", Match = Is<dmStarshardOrb>, OnePerKind = true,
			Purpose = "Store or take out Star Shards.",
			Help = "Shards stored in the orb are kept by the Academy for future heroes." },

		new Kind { Category = "Pets", Title = "Professor Hatch", Match = Is<NPCAcademyHatch>,
			Purpose = "Battle Poultry: store, withdraw, heal, breed, shop.",
			Help = "Deposit all / Withdraw all are on the side of the lists. Pets match your level; breed with Passionate Poultry Poultice, make a pet forget a power with Power Pruning Poultice." },
		new Kind { Category = "Pets", Title = "The Coop", OnePerKind = true, Match = Is<NPCAcademyHatch>,
			Purpose = "All birds in one window: feed, breed, ascend, Pantry.",
			Use = () => { if (Config.On(Config.Coop)) { CoopPanel.Open(); } return null; },
			Help = "Every bird (bags + Academy) with details; feed from bags or the Pantry; breeding preview; Ascend for +1 generation; Pantry storage bought with Feathers." },
		new Kind { Category = "Pets", Title = "Chromafication Device", Match = Is<dmChickenChromaficationDevice>, OnePerKind = true,
			Purpose = "Change your birds' colours.",
			Help = "Cosmetic recolouring of Battle Poultry." },
		new Kind { Category = "Pets", Title = null, Match = a => a is dmPigkeeper || Arc(a, "npc_academy_piggy_bank_shop"),
			Purpose = "Spend piggy bank savings.",
			Help = "Your pet piggy bank collects coins into a purse that survives death. Spend it here (including the Pig Farm License)." },

		new Kind { Category = "Other", Title = null, Match = Is<NPCAcademyGravedigger>,
			Purpose = "Necromansy secrets and the ghost by the exit.",
			Help = "Talk to him to learn about Necromansy and the Horseless Headsmans." },
		new Kind { Category = "Other", Title = "Daily Danger", Match = Is<dmDailyDangerBook>, OnePerKind = true,
			Purpose = "Seeded daily challenge with your Academy roster.",
			Help = "A daily run. Winning pays Proofs of Stremf into the war chest." },
		new Kind { Category = "Other", Title = "Leave the Academy", Match = a => a is dmTouchGateway && (Arc(a, "overworld_gateway") || Arc(a, "default_gateway")), OnePerKind = true,
			Purpose = "Walk out to the overworld.",
			Help = "Uses the Academy exit." },
	};

	public static bool IsOpen;
	private static int tab;
	private static int selected;
	private static List<Entry> entries = new List<Entry>();
	private static bool toggleWasDown;
	private static bool prepWasDown = true; // avoid firing if P was already held when the panel opened


	private static dmGame Game => dmUtilities.theGame;

	private static bool InAcademy(dmGame g) => g.dungeonMans != null && g.theCurrentLevel is dmAcademy && !g.dungeonMans.bIsInOverworld;

	// Any game menu, dialog or mod window open (ModKit's test; QoL registers its own windows as modals).
	public static bool UiBusy(dmGame g) => Ui.IsBusy(g);

	private static bool CanOpen(dmGame g) => Config.On(Config.AcademyDirectory) && InAcademy(g) && !UiBusy(g);

	public static void DevOpen(int startTab)
	{
		tab = startTab;
		Open();
	}

	private static void Open()
	{
		Rebuild();
		IsOpen = true;
		if (Categories[tab] == FavoritesTab && EntriesIn(tab).Count == 0)
		{
			tab = 1;
		}
		selected = 0;
		scroll = 0;
		dmAudio.PlayUIFX("open_dialog");
	}

	private static void Close()
	{
		IsOpen = false;
		dmAudio.PlayUIFX("ui_back_02");
	}

	private static void Rebuild()
	{
		var found = new List<Entry>();
		var seenOnce = new HashSet<Kind>();
		foreach (dmActor actor in Game.allActors.ToList())
		{
			if (actor == null || actor == Game.dungeonMans)
			{
				continue;
			}
			// One "go there" kind per actor, plus any one-click "Use" kinds (e.g. The Coop on Hatch).
			Kind first = Kinds.FirstOrDefault(k => k.Use == null && k.Match(actor));
			foreach (Kind kind in Kinds.Where(k => (k == first || k.Use != null) && k.Match(actor)))
			{
				if (kind.OnePerKind && !seenOnce.Add(kind))
				{
					continue;
				}
				found.Add(new Entry { Kind = kind, Actor = actor });
			}
		}
		entries = found;
	}

	private static List<Entry> TabEntries() => EntriesIn(tab);

	private static List<Entry> EntriesIn(int t) =>
		Categories[t] == FavoritesTab ? entries.Where(IsFavorite).ToList() : entries.Where(e => e.Kind.Category == Categories[t]).ToList();

	// ---------- favorites (Mods\QoL\directory_favorites.txt, one entry key per line) ----------

	private static HashSet<string> favorites;

	private static string FavoritesPath => QoLMod.Ctx.PathFor("directory_favorites.txt");

	private static HashSet<string> Favorites
	{
		get
		{
			if (favorites == null)
			{
				favorites = new HashSet<string>();
				try
				{
					if (System.IO.File.Exists(FavoritesPath))
					{
						favorites.UnionWith(System.IO.File.ReadAllLines(FavoritesPath).Where(l => l.Length > 0));
					}
				}
				catch (Exception e)
				{
					Log.Write("Could not read " + FavoritesPath + ": " + e.Message);
				}
			}
			return favorites;
		}
	}

	private static bool IsFavorite(Entry e) => Favorites.Contains(e.Key);

	private static void ToggleFavorite(Entry e)
	{
		if (!Favorites.Remove(e.Key))
		{
			Favorites.Add(e.Key);
		}
		dmAudio.PlayUIFX(IsFavorite(e) ? "ui_increase_02" : "ui_decrease_02");
		try
		{
			System.IO.File.WriteAllLines(FavoritesPath, Favorites);
		}
		catch (Exception ex)
		{
			Log.Write("Could not write " + FavoritesPath + ": " + ex.Message);
		}
	}

	// ---------- input ----------

	// ModKit Events.Input handler for all QoL input. Returns false when this frame's input was consumed.
	internal static bool InputPrefix(dmGame __instance)
	{
		dmGame g = __instance;
		GameSpeed.HandleKey(g);
		if (!CoopPanel.IsOpen && !IsOpen && QoLHub.HandleInput(g))
		{
			return false;
		}
		if (CoopPanel.IsOpen)
		{
			CoopPanel.HandleInput(g);
			return false;
		}
		if (OverworldHud.HandleInput(g))
		{
			return false;
		}
		bool toggleDown = g.theInput.PressedKeys.Contains(ToggleKey);
		bool togglePressed = toggleDown && !toggleWasDown;
		toggleWasDown = toggleDown;

		if (!IsOpen)
		{
			if (togglePressed && CanOpen(g))
			{
				Open();
				return false;
			}
			return true;
		}
		if (!InAcademy(g) || !Config.On(Config.AcademyDirectory))
		{
			IsOpen = false;
			return true;
		}

		List<ActionButton> a = g.theInput.ExecutedActions;
		List<Entry> list = TabEntries();
		bool prepDown = g.theInput.PressedKeys.Contains(Keys.P);
		bool prepPressed = prepDown && !prepWasDown;
		prepWasDown = prepDown;
		if (togglePressed || a.Contains(ActionButton.Close))
		{
			Close();
		}
		else if (prepPressed && Config.On(Config.HeroPrep))
		{
			Prepare();
		}
		else if (a.Contains(ActionButton.Left) || a.Contains(ActionButton.ArrowKey_Left))
		{
			SwitchTab(-1);
		}
		else if (a.Contains(ActionButton.Right) || a.Contains(ActionButton.ArrowKey_Right))
		{
			SwitchTab(1);
		}
		else if (a.Contains(ActionButton.MouseWheel_Up))
		{
			Scroll(-1, list.Count);
		}
		else if (a.Contains(ActionButton.MouseWheel_Down))
		{
			Scroll(1, list.Count);
		}
		else if (a.Contains(ActionButton.Up) || a.Contains(ActionButton.ArrowKey_Up))
		{
			selected = Math.Max(0, selected - 1);
			KeepSelectionVisible();
		}
		else if (a.Contains(ActionButton.Down) || a.Contains(ActionButton.ArrowKey_Down))
		{
			selected = Math.Min(Math.Max(0, list.Count - 1), selected + 1);
			KeepSelectionVisible();
		}
		else if ((a.Contains(ActionButton.Enter) || a.Contains(ActionButton.Select)) && selected < list.Count)
		{
			GoTo(list[selected]);
		}
		a.Clear();
		return false;
	}

	private static void SwitchTab(int dir)
	{
		tab = (tab + dir + Categories.Length) % Categories.Length;
		selected = 0;
		scroll = 0;
		dmAudio.PlayUIFX("ui_click_01");
	}

	// Teleport next to the target, then "walk into" it so the game runs its normal bump interaction.
	private static void GoTo(Entry e)
	{
		if (e.Kind.Use != null)
		{
			IsOpen = false;
			string msg = e.Kind.Use();
			if (msg != null)
			{
				Game.AddStringToLog("^3Academy Directory:^7 " + msg);
				Ui.Toast(msg);
			}
			return;
		}
		IsOpen = false;
		Visit(e.Actor, e.Title);
	}

	// Teleport next to an actor and bump into it (opens its menu). Also used by the overworld hand-in button.
	public static void Visit(dmActor actor, string title)
	{
		try
		{
			if (!Dm.Visit(actor))
			{
				dmAudio.PlayUIFX("ui_failure");
				Game.AddStringToLog("^1Academy Directory:^7 no free spot next to " + title + ".");
			}
		}
		catch (Exception ex)
		{
			Log.Write("Visit " + title + " failed: " + ex);
		}
	}

	private static void Prepare()
	{
		IsOpen = false;
		HeroPrep.Run();
	}

	private static void SetTab(int t)
	{
		tab = t;
		selected = 0;
		scroll = 0;
	}

	private static void Scroll(int dir, int count)
	{
		scroll = Math.Max(0, Math.Min(Math.Max(0, (count + Columns - 1) / Columns - Rows), scroll + dir));
		selected = Math.Max(scroll * Columns, Math.Min(Math.Min(count, (scroll + Rows) * Columns) - 1, selected));
	}

	private static void KeepSelectionVisible()
	{
		int row = selected / Columns;
		if (row < scroll)
		{
			scroll = row;
		}
		else if (row >= scroll + Rows)
		{
			scroll = row - Rows + 1;
		}
	}

	// ---------- drawing ----------

	private const int Columns = 2, Rows = 5, CardH = 64;
	private static int scroll; // first visible row of cards

	// Called from ModKit's Events.DrawOverlay (see QoLMod), so it draws under the mouse cursor.
	public static void DrawOverlay(dmGame g)
	{
		if (g.dungeonMans == null || g.bShowMainMenu)
		{
			return;
		}
		try
		{
			if (IsOpen)
			{
				DrawPanel(g);
			}
			else if (CanOpen(g))
			{
				g.theRenderer.BeginSpriteBatch();
				// Left of the game's own 80px top-right icon.
				var r = new Rectangle(Ui.ScreenSize.X - 112 - 256, 8, 256, 32);
				Ui.BoxButton("qol_dir_open", r, "Academy Directory", Open, font: UiFont.Medium, tooltipText: "Everyone and everything in the Academy (Home)", stackLevel: 6);
				g.theRenderer.EndSpriteBatch();
			}
		}
		catch (Exception ex)
		{
			IsOpen = false;
			Log.Write("Academy Directory draw failed: " + ex);
		}
	}

	private static void DrawPanel(dmGame g)
	{
		wg2DRenderer tr = g.theRenderer;
		List<Entry> list = TabEntries();
		selected = Math.Max(0, Math.Min(selected, list.Count - 1));

		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		Rectangle c = Ui.ModalWindow("qol_dir", 26, 18, "Academy Directory", Close);

		// Prepare hero, in the title bar.
		if (Config.On(Config.HeroPrep))
		{
			List<string> plan = HeroPrep.Plan();
			string tip = plan.Count == 0 ? "Nothing left to do for this hero." : "Will:" + string.Concat(plan.Select(p => "\n  " + p));
			if (ProofPresets.IsEmpty(0))
			{
				tip += "\n(Save Proof preset 1 at the Battlemaster to include stats.)";
			}
			Ui.BoxButton("qol_dir_prep", new Rectangle(c.Right - 300, c.Y - 52, 224, 32), "Prepare hero  (P)", Prepare, plan.Count > 0,
				tooltipText: tip, accent: Palette.Good, stackLevel: Ui.WindowLevel + 2);
		}

		// Tabs with counts; Favorites first (with a heart icon drawn over the padding).
		string[] labels = Categories.Select((cat, i) => (i == 0 ? "     " : "") + cat + " " + EntriesIn(i).Count).ToArray();
		int th = Ui.Tabs("qol_dir", c.X, c.Y, labels, tab, SetTab, null, UiFont.Small);
		Ui.Icon("ui_favorite_heart", new Rectangle(c.X + 10, c.Y + 7, 18, 18));

		// Cards grid.
		var grid = new Rectangle(c.X, c.Y + th + 10, c.Width, Rows * CardH);
		const int cw = 384;
		if (list.Count == 0)
		{
			Ui.TextCentered(Categories[tab] == FavoritesTab ? "No favorites yet: right-click any entry in the other tabs." : "Nothing of this kind in your Academy yet.",
				grid, Palette.TextMuted, UiFont.Medium);
		}
		int totalRows = (list.Count + Columns - 1) / Columns;
		scroll = Math.Max(0, Math.Min(Math.Max(0, totalRows - Rows), scroll));
		for (int vi = 0; vi < Rows * Columns; vi++)
		{
			int i = scroll * Columns + vi;
			if (i >= list.Count)
			{
				break;
			}
			var r = new Rectangle(grid.X + (vi % Columns) * (cw + 8), grid.Y + (vi / Columns) * CardH, cw, CardH);
			DrawCard(list[i], i, r);
		}
		if (totalRows > Rows)
		{
			var track = new Rectangle(grid.Right - 6, grid.Y, 5, Rows * CardH - 6);
			Ui.Fill(track, Palette.BarBack);
			int thumb = Math.Max(20, track.Height * Rows / totalRows);
			Ui.Fill(new Rectangle(track.X, track.Y + (track.Height - thumb) * scroll / Math.Max(1, totalRows - Rows), 5, thumb), Palette.Border);
		}

		// Help strip: the selected entry, or what the tab is for.
		Rectangle help = Ui.Box(new Rectangle(c.X, grid.Bottom + 8, 776, 64));
		string text = list.Count > 0 ? "^3" + list[selected].Title + ":^7 " + list[selected].Kind.Help : CategoryHelp[tab];
		Ui.WrappedText(text, help.X + 14, help.Y + 10, help.Width - 28);

		Ui.Separator(c.X, c.Bottom - 28, c.Width);
		Ui.Text("^3Click^7 go there   ^3Right-click^7 favorite   ^3Left/Right^7 tabs   ^3P^7 prepare   ^3Esc^7 close", c.X + 4, c.Bottom - 22, Palette.TextDim);
		tr.EndSpriteBatch();
	}

	private static void DrawCard(Entry e, int index, Rectangle r)
	{
		int slot = index - scroll * Columns;
		ui_mouseFrame f = Ui.Area("qol_dir_card" + slot, r, () => GoTo(e), () => ToggleFavorite(e), Ui.WindowLevel + 1);
		bool hot = Ui.Hovered(f);
		if (hot)
		{
			selected = index;
		}
		bool sel = index == selected;
		Ui.Box(r, sel || hot);
		Ui.Sprite(e.Actor.sprite, new Rectangle(r.X + 8, r.Y + 6, 48, r.Height - 12));
		Ui.Text(Ui.Fit(e.Title, r.Width - 110, UiFont.Medium), r.X + 66, r.Y + 6, sel ? Palette.Accent : Palette.Text, UiFont.Medium);
		int px = r.X + 66;
		if (e.Kind.Status != null)
		{
			string st = e.Kind.Status();
			Ui.Text("^3" + st, px, r.Y + 34, Palette.Text);
			px += (int)Ui.Measure(st).X + 10;
		}
		Ui.Text(Ui.Fit(e.Kind.Purpose, r.Right - 40 - px), px, r.Y + 34, Palette.TextDim);

		// Heart: click to toggle favorite (same as right-click on the card).
		bool fav = IsFavorite(e);
		var heart = new Rectangle(r.Right - 34, r.Y + 8, 24, 24);
		ui_mouseFrame hf = Ui.Area("qol_dir_heart" + slot, heart, () => ToggleFavorite(e), Ui.WindowLevel + 2);
		bool hh = Ui.Hovered(hf);
		if (fav || hh || hot)
		{
			Ui.Icon("ui_favorite_heart", new Rectangle(heart.X + 2, heart.Y + 2, 20, 20), fav ? (Color?)null : new Color(120, 110, 100, 200));
		}
		if (hh)
		{
			Ui.Tooltip(fav ? "Remove from favorites" : "Add to favorites");
		}
	}
}
