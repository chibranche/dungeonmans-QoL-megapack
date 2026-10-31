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
	}

	private static readonly string[] Categories = { "New hero", "Teachers", "Turn-ins", "Gear", "Pets", "Other" };

	private static readonly string[] CategoryHelp =
	{
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

	private static void Open()
	{
		Rebuild();
		IsOpen = true;
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

	private static List<Entry> TabEntries() =>
		entries.Where(e => e.Kind.Category == Categories[tab]).ToList();

	// ---------- input ----------

	// ModKit Events.Input handler for all QoL input. Returns false when this frame's input was consumed.
	internal static bool InputPrefix(dmGame __instance)
	{
		dmGame g = __instance;
		GameSpeed.HandleKey(g);
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

	// ---------- mouse callbacks ----------

	private static object ClickOpen(ui_mouseFrame f, int b)
	{
		if (CanOpen(Game))
		{
			Open();
		}
		return true;
	}

	private static object ClickPrepare(ui_mouseFrame f, int b)
	{
		Prepare();
		return true;
	}

	private static void Prepare()
	{
		IsOpen = false;
		HeroPrep.Run();
	}

	private static object ClickClose(ui_mouseFrame f, int b)
	{
		Close();
		return true;
	}

	private static object ClickTab(ui_mouseFrame f, int b)
	{
		tab = dmUtilities.GetIntValue(f.tagStringData);
		selected = 0;
		scroll = 0;
		dmAudio.PlayUIFX("ui_click_01");
		return true;
	}

	private static void Scroll(int dir, int count)
	{
		int before = scroll;
		scroll = Math.Max(0, Math.Min(Math.Max(0, count - MaxRows), scroll + dir));
		selected = Math.Max(scroll, Math.Min(scroll + MaxRows - 1, selected));
		if (scroll != before)
		{
			dmAudio.PlayUIFX("ui_click_01");
		}
	}

	private static void KeepSelectionVisible()
	{
		if (selected < scroll)
		{
			scroll = selected;
		}
		else if (selected >= scroll + MaxRows)
		{
			scroll = selected - MaxRows + 1;
		}
	}

	private static object ClickScroll(ui_mouseFrame f, int b)
	{
		Scroll(dmUtilities.GetIntValue(f.tagStringData), TabEntries().Count);
		return true;
	}

	private static object ClickEntry(ui_mouseFrame f, int b)
	{
		List<Entry> list = TabEntries();
		int i = dmUtilities.GetIntValue(f.tagStringData);
		if (i < list.Count)
		{
			GoTo(list[i]);
		}
		return true;
	}

	private static bool PanelOpen(ui_mouseFrame f) => IsOpen;
	private static bool PrepVisible(ui_mouseFrame f) => IsOpen && Config.On(Config.HeroPrep);
	private static bool ButtonVisible(ui_mouseFrame f) => !IsOpen && CanOpen(Game);

	// ---------- drawing ----------

	private const int PanelW = 24 * 32, RowH = 64, MaxRows = 5;
	private static int scroll; // index of the first visible row in the current tab

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
				DrawOpenButton(g);
			}
		}
		catch (Exception ex)
		{
			IsOpen = false;
			Log.Write("Academy Directory draw failed: " + ex);
		}
	}

	private static void DrawOpenButton(dmGame g)
	{
		wg2DRenderer tr = g.theRenderer;
		int w = 8 * 32, x = (int)tr.fWindowXSize - w - 112, y = 4; // left of the game's own 80px top-right icon
		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		tr.DrawParchmentBox(x, y, 8, 1);
		ui_mouseFrame frame = g.theUIFrames.AddFrame(x, y, w, 32, 5, "qol_academy_dir_button");
		frame.funcOnClick = ClickOpen;
		frame.RemoveIfFalse = ButtonVisible;
		tr.SetActiveFont(tr.fontNyala18);
		Color c = g.theInput.IsActiveFrame(frame) ? ShepColor.Yellow : Color.White;
		tr.DrawString("Academy Directory  ^3[Home]^7", new Vector2(x + 30, y + 6), c);
		tr.EndSpriteBatch();
	}

	private static void DrawPanel(dmGame g)
	{
		wg2DRenderer tr = g.theRenderer;
		dmInput input = g.theInput;
		dmUIMouseFrameManager frames = g.theUIFrames;
		List<Entry> list = TabEntries();
		if (selected >= list.Count)
		{
			selected = Math.Max(0, list.Count - 1);
		}

		int panelH = 15 * 32 + 96;
		int x = (int)(tr.fWindowXSize * 0.5f) - PanelW / 2;
		int y = Math.Max(8, (int)(tr.fWindowYSize * 0.5f) - panelH / 2);

		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		tr.DimScreen();
		// Blocks clicks from reaching the world (and walking the hero) while the panel is open.
		frames.AddFrame(0, 0, (int)tr.fWindowXSize, (int)tr.fWindowYSize, 20, "qol_academy_dir_blocker").RemoveIfFalse = PanelOpen;
		tr.DrawParchmentBox(x, y, 24, 15);
		tr.DrawParchmentBox(x, y + 15 * 32 + 8, 24, 3);

		tr.SetActiveFont(tr.fontNyala30);
		tr.DrawString("Academy Directory", new Vector2(x + 24, y + 12), ShepColor.Yellow);
		// "Prepare hero" button: every new-hero chore in one click (see HeroPrep).
		bool prepHot = false;
		if (Config.On(Config.HeroPrep))
		{
			ui_mouseFrame prep = frames.AddFrame(x + 280, y + 12, 230, 32, 21, "qol_academy_dir_prep");
			prep.funcOnClick = ClickPrepare;
			prep.RemoveIfFalse = PrepVisible;
			prepHot = input.IsActiveFrame(prep);
			tr.SetActiveFont(tr.fontNyala24);
			tr.DrawString("[ Prepare hero ]  ^7(P)", new Vector2(x + 284, y + 14), prepHot ? ShepColor.Yellow : ShepColor.Cyan);
		}
		tr.SetActiveFont(tr.fontNyala18);
		tr.DrawString("Click / Enter: go there    Left/Right: tabs    Esc: close", new Vector2(x + 500, y + 22), Color.LightGray);

		ui_mouseFrame close = frames.AddFrame(x + PanelW - 44, y + 8, 36, 32, 21, "qol_academy_dir_close");
		close.funcOnClick = ClickClose;
		close.RemoveIfFalse = PanelOpen;
		tr.SetActiveFont(tr.fontNyala24);
		tr.DrawString("X", new Vector2(x + PanelW - 34, y + 10), input.IsActiveFrame(close) ? ShepColor.Yellow : Color.White);

		// Tabs
		int tabX = x + 20, tabY = y + 56;
		tr.SetActiveFont(tr.fontNyala18);
		for (int t = 0; t < Categories.Length; t++)
		{
			int count = entries.Count(e => e.Kind.Category == Categories[t]);
			string label = Categories[t] + " (" + count + ")";
			int w = label.Length * 8 + 24;
			ui_mouseFrame tf = frames.AddFrame(tabX, tabY, w, 28, 21, "qol_academy_dir_tab_" + t);
			tf.tagStringData = t.ToString();
			tf.funcOnClick = ClickTab;
			tf.RemoveIfFalse = PanelOpen;
			bool hot = input.IsActiveFrame(tf);
			Color c = t == tab ? ShepColor.Yellow : hot ? Color.White : Color.DarkGray;
			tr.DrawString(label, new Vector2(tabX + 8, tabY + 4), c);
			if (t == tab)
			{
				tr.DrawLine(tabX + 4, tabY + 28, tabX + w - 8, tabY + 28, 2, ShepColor.Yellow);
			}
			tabX += w + 8;
		}
		tr.DrawLine(x + 16, tabY + 34, x + PanelW - 16, tabY + 34, 1, Color.Gray);

		// Entries
		int rowY = tabY + 44;
		if (list.Count == 0)
		{
			tr.DrawString("Nothing of this kind in your Academy yet.", new Vector2(x + 40, rowY + 10), Color.LightGray);
		}
		scroll = Math.Max(0, Math.Min(Math.Max(0, list.Count - MaxRows), scroll));
		for (int i = scroll; i < Math.Min(list.Count, scroll + MaxRows); i++)
		{
			Entry e = list[i];
			int ry = rowY + (i - scroll) * RowH;
			ui_mouseFrame rf = frames.AddFrame(x + 16, ry, PanelW - 32, RowH - 4, 21, "qol_academy_dir_row_" + (i - scroll));
			rf.tagStringData = i.ToString();
			rf.funcOnClick = ClickEntry;
			rf.RemoveIfFalse = PanelOpen;
			if (input.IsActiveFrame(rf))
			{
				selected = i;
			}
			if (i == selected)
			{
				tr.DrawLightBox(x + 16, ry - 4, 23, 2);
			}
			DrawIcon(tr, e.Actor.sprite, x + 28, ry + 2, 52, 52);
			tr.SetActiveFont(tr.fontNyala24);
			tr.DrawString(e.Title, new Vector2(x + 96, ry + 4), i == selected ? ShepColor.Yellow : Color.White);
			tr.SetActiveFont(tr.fontNyala18);
			tr.DrawString(e.Kind.Purpose + (e.Kind.Status != null ? "  ^3(" + e.Kind.Status() + ")^7" : ""), new Vector2(x + 96, ry + 32), Color.LightGray);
		}
		// Scroll indicators (also clickable); the mouse wheel and Up/Down scroll too.
		int indY = rowY + MaxRows * RowH + 2;
		if (scroll > 0)
		{
			DrawScrollButton(tr, input, frames, "^ " + scroll + " more above", x + 40, indY, -1);
		}
		int below = list.Count - (scroll + MaxRows);
		if (below > 0)
		{
			DrawScrollButton(tr, input, frames, "v " + below + " more below", x + PanelW - 240, indY, 1);
		}

		// Help strip: what the selected entry is for (or what the tab is about).
		tr.SetActiveFont(tr.fontNyala18);
		string help = list.Count > 0 ? list[selected].Title + ": " + list[selected].Kind.Help : CategoryHelp[tab];
		if (prepHot)
		{
			List<string> plan = HeroPrep.Plan();
			help = "Prepare hero will: " + (plan.Count == 0 ? "nothing left to do for this hero." : string.Join(", ", plan) + ".")
				+ (ProofPresets.IsEmpty(0) ? " (Save Proof preset 1 at the Battlemaster to include stat spending.)" : "");
		}
		Vector2 hp = new Vector2(x + 24, y + 15 * 32 + 22);
		tr.DrawWordWrappedString("^3?^7 " + help, hp, (int)hp.X + PanelW - 48);
		tr.EndSpriteBatch();
	}

	private static void DrawScrollButton(wg2DRenderer tr, dmInput input, dmUIMouseFrameManager frames, string label, int bx, int by, int dir)
	{
		ui_mouseFrame f = frames.AddFrame(bx - 4, by, 200, 26, 21, "qol_academy_dir_scroll_" + dir);
		f.tagStringData = dir.ToString();
		f.funcOnClick = ClickScroll;
		f.RemoveIfFalse = PanelOpen;
		tr.SetActiveFont(tr.fontNyala18);
		tr.DrawString(label, new Vector2(bx, by + 2), input.IsActiveFrame(f) ? ShepColor.Yellow : ShepColor.Cyan);
	}

	private static void DrawIcon(wg2DRenderer tr, dmSprite sprite, int x, int y, int maxW, int maxH)
	{
		if (sprite.Width <= 0 || sprite.Height <= 0)
		{
			return;
		}
		float scale = Math.Min(maxW / (float)sprite.Width, maxH / (float)sprite.Height);
		int w = (int)(sprite.Width * scale), h = (int)(sprite.Height * scale);
		tr.DrawSpriteToScreenCoordinates(sprite, x + (maxW - w) / 2, y + (maxH - h) / 2, w, h);
	}
}
