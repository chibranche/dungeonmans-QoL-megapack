using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// "The Coop": one window for all poultry management (see PoultryCore for the game logic).
//   Birds  - list of every bird (bags + Academy), details, feed, forget powers, store/take/equip, favorite, free.
//   Breed  - pick two parents, exact preview of the egg (generation, stats, Pluck, chance of each power).
//   Ascend - +1 generation for N Passionate Poultices, Pluck * (1 - 0.5^N) kept.
//   Pantry - storage for steaks, feathers, poultices (bought with Feathers), deposit/withdraw/render.
// Opens from Hatch's menu, the Academy Directory, or the poultry status window (anywhere; Pantry and
// Academy-only actions are disabled outside the Academy).
internal static class CoopPanel
{
	public static bool IsOpen;
	private static int tab;
	private static readonly string[] Tabs = { "Birds", "Breed", "Ascend", "Pantry" };

	private static dmBirdEgg selected;
	private static dmBirdEgg parentA, parentB;
	private static dmBirdEgg releaseConfirm;
	private static int ascendN = 2;
	private static int birdScroll, feedScroll, bagScroll, pantryScroll;
	private static string message = "";

	private static dmGame Game => dmUtilities.theGame;
	public static bool AtAcademy => PoultryCore.Hatch != null;

	public static void Open(int startTab = 0, dmBirdEgg focus = null)
	{
		IsOpen = true;
		tab = startTab;
		selected = focus ?? PoultryCore.AllBirds().FirstOrDefault(PoultryCore.IsEquipped) ?? PoultryCore.AllBirds().FirstOrDefault();
		releaseConfirm = null;
		message = AtAcademy ? "" : "Away from the Academy: only birds and items in your bags are available.";
		dmAudio.PlayUIFX("open_dialog");
	}

	private static void Close()
	{
		IsOpen = false;
		dmAudio.PlayUIFX("ui_back_02");
	}

	private static void Say(string s)
	{
		if (!string.IsNullOrEmpty(s))
		{
			message = s;
			Game.AddStringToLog("^3Coop:^7 " + s);
		}
	}

	// ---------------- input ----------------
	public static void HandleInput(dmGame g)
	{
		List<ActionButton> a = g.theInput.ExecutedActions;
		List<dmBirdEgg> birds = VisibleBirds();
		if (a.Contains(ActionButton.Close))
		{
			Close();
		}
		else if (a.Contains(ActionButton.MouseWheel_Up))
		{
			birdScroll = Math.Max(0, birdScroll - 1);
		}
		else if (a.Contains(ActionButton.MouseWheel_Down))
		{
			birdScroll++;
		}
		else if (a.Contains(ActionButton.Left) || a.Contains(ActionButton.ArrowKey_Left))
		{
			tab = (tab + Tabs.Length - 1) % Tabs.Length;
		}
		else if (a.Contains(ActionButton.Right) || a.Contains(ActionButton.ArrowKey_Right))
		{
			tab = (tab + 1) % Tabs.Length;
		}
		else if ((a.Contains(ActionButton.Up) || a.Contains(ActionButton.ArrowKey_Up)) && birds.Count > 0)
		{
			selected = birds[Math.Max(0, birds.IndexOf(selected) - 1)];
		}
		else if ((a.Contains(ActionButton.Down) || a.Contains(ActionButton.ArrowKey_Down)) && birds.Count > 0)
		{
			selected = birds[Math.Min(birds.Count - 1, birds.IndexOf(selected) + 1)];
		}
		a.Clear();
	}

	private static List<dmBirdEgg> VisibleBirds() =>
		(AtAcademy ? PoultryCore.AllBirds() : PoultryCore.BagBirds())
			.OrderByDescending(PoultryCore.IsEquipped)
			.ThenByDescending(e => e.BirdName == PoultryCore.FavoriteName)
			.ThenByDescending(e => e.GetGeneration())
			.ThenByDescending(e => e.GetChampionPoints())
			.ToList();

	// ---------------- tiny immediate-mode button system ----------------
	private static readonly Dictionary<string, Action> actions = new Dictionary<string, Action>();
	private static int frameCounter;

	private static object Route(ui_mouseFrame f, int b)
	{
		if (actions.TryGetValue(f.tagStringData, out Action act))
		{
			try
			{
				act();
			}
			catch (Exception e)
			{
				Log.Write("Coop action failed: " + e);
				message = "^1That failed^7 (see DungeonmansQoL.log).";
			}
		}
		return true;
	}

	private static bool PanelOpen(ui_mouseFrame f) => IsOpen;

	private static bool Button(string label, int x, int y, int w, Action onClick, bool enabled = true, Color? color = null)
	{
		string id = "b" + frameCounter++;
		ui_mouseFrame f = Game.theUIFrames.AddFrame(x, y, w, 24, 41, "qol_coop_" + id);
		f.tagStringData = id;
		f.RemoveIfFalse = PanelOpen;
		bool hot = Game.theInput.IsActiveFrame(f);
		if (enabled)
		{
			actions[id] = () =>
			{
				dmAudio.PlayUIFX("ui_click_01");
				onClick();
			};
			f.funcOnClick = Route;
		}
		Color c = !enabled ? Color.DarkGray : hot ? ShepColor.Yellow : color ?? ShepColor.Cyan;
		tr.DrawString("[" + label + "]", new Vector2(x, y + 2), c);
		return hot;
	}

	private static bool Hover(int x, int y, int w, int h)
	{
		string id = "h" + frameCounter++;
		ui_mouseFrame f = Game.theUIFrames.AddFrame(x, y, w, h, 41, "qol_coop_" + id);
		f.tagStringData = id;
		f.RemoveIfFalse = PanelOpen;
		return Game.theInput.IsActiveFrame(f);
	}

	private static wg2DRenderer tr;

	private static void Text(string s, int x, int y, Color? c = null) => tr.DrawString(s, new Vector2(x, y), c ?? Color.White);

	private static void Font(int size)
	{
		tr.SetActiveFont(size >= 30 ? tr.fontNyala30 : size >= 24 ? tr.fontNyala24 : tr.fontNyala18);
	}

	// ---------------- drawing ----------------
	private const int W = 38 * 32, H = 21 * 32;
	private static int X0, Y0;

	public static void Draw(dmGame g)
	{
		tr = g.theRenderer;
		actions.Clear();
		frameCounter = 0;
		if (selected != null && !PoultryCore.AllBirds().Contains(selected))
		{
			selected = null;
		}
		X0 = (int)(tr.fWindowXSize * 0.5f) - W / 2;
		Y0 = Math.Max(4, (int)(tr.fWindowYSize * 0.5f) - H / 2);

		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		tr.DimScreen();
		g.theUIFrames.AddFrame(0, 0, (int)tr.fWindowXSize, (int)tr.fWindowYSize, 40, "qol_coop_blocker").RemoveIfFalse = PanelOpen;
		tr.DrawParchmentBox(X0, Y0, W / 32, H / 32);

		Font(30);
		Text("The Coop", X0 + 24, Y0 + 12, ShepColor.Yellow);
		Font(18);
		Text("Feathers: ^3" + PoultryCore.Feathers + "^7    Passionate Poultices: ^3" + PoultryCore.Count(PoultryCore.Passionate)
			+ "^7    Pruning Poultices: ^3" + PoultryCore.Count(PoultryCore.Pruning) + "^7" + (AtAcademy ? "" : "   ^1(bags only)^7"),
			X0 + 200, Y0 + 22);
		Button("X", X0 + W - 48, Y0 + 14, 32, Close, true, Color.White);

		int tx = X0 + 24;
		for (int t = 0; t < Tabs.Length; t++)
		{
			int tt = t;
			bool enabled = t != 3 || AtAcademy;
			Font(24);
			Button(Tabs[t], tx, Y0 + 52, 110, () => { tab = tt; releaseConfirm = null; }, enabled, t == tab ? ShepColor.Yellow : Color.White);
			tx += 130;
		}
		Font(18);
		tr.DrawLine(X0 + 16, Y0 + 84, X0 + W - 16, Y0 + 84, 1, Color.Gray);

		switch (tab)
		{
		case 0:
			DrawBirdList(BirdClick.Select);
			DrawBirdDetails();
			break;
		case 1:
			DrawBirdList(BirdClick.Parent);
			DrawBreed();
			break;
		case 2:
			DrawBirdList(BirdClick.Select);
			DrawAscend();
			break;
		case 3:
			DrawPantry();
			break;
		}

		tr.DrawLine(X0 + 16, Y0 + H - 44, X0 + W - 16, Y0 + H - 44, 1, Color.Gray);
		Font(18);
		Text(message, X0 + 24, Y0 + H - 36, Color.LightGray);
		tr.EndSpriteBatch();
	}

	private enum BirdClick { Select, Parent }

	private const int ListW = 400, RowH = 54, Rows = 9;

	private static void DrawBirdList(BirdClick mode)
	{
		List<dmBirdEgg> birds = VisibleBirds();
		birdScroll = Math.Max(0, Math.Min(Math.Max(0, birds.Count - Rows), birdScroll));
		int x = X0 + 20, y = Y0 + 92;
		for (int i = birdScroll; i < Math.Min(birds.Count, birdScroll + Rows); i++)
		{
			dmBirdEgg e = birds[i];
			int ry = y + (i - birdScroll) * RowH;
			bool hot = Hover(x, ry, ListW, RowH - 4);
			string id = "row" + i;
			actions[id] = () => ClickBird(e, mode);
			ui_mouseFrame f = Game.theUIFrames.AddFrame(x, ry, ListW, RowH - 4, 41, "qol_coop_" + id);
			f.tagStringData = id;
			f.funcOnClick = Route;
			f.RemoveIfFalse = PanelOpen;
			bool isSel = e == selected || e == parentA || e == parentB;
			if (isSel || hot)
			{
				tr.DrawLightBox(x - 4, ry - 6, ListW / 32, 1);
			}
			DrawIcon(e.GetBirdSprite(), x, ry, 36, 46);
			Font(24);
			string name = (e.BirdName == PoultryCore.FavoriteName ? "^3*^7 " : "") + e.BirdName;
			Text(name, x + 44, ry, isSel ? ShepColor.Yellow : Color.White);
			Font(18);
			Point ax = e.GetStatAxis();
			string tags = (PoultryCore.IsEquipped(e) ? "^5Equipped^7 " : PoultryCore.InBag(e) ? "Bag " : "Academy ")
				+ (e.GetStatInfo().bWounded ? "^1Wounded^7 " : "") + (!e.BirdHasEverHatched ? "^7Unhatched^7 " : "")
				+ (mode == BirdClick.Parent && e == parentA ? "^3[Parent 1]^7" : mode == BirdClick.Parent && e == parentB ? "^3[Parent 2]^7" : "");
			Text("G" + e.GetGeneration() + "  Pluck " + e.GetChampionPoints() + "  " + Axis(ax.X, "T", "S") + " " + Axis(ax.Y, "R", "M") + "   " + tags,
				x + 44, ry + 26, Color.LightGray);
		}
		if (birds.Count == 0)
		{
			Text("No birds here.", x + 10, y + 10, Color.LightGray);
		}
		int iy = y + Rows * RowH;
		if (birdScroll > 0)
		{
			Button("^ " + birdScroll + " more", x, iy, 120, () => birdScroll--);
		}
		if (birds.Count - birdScroll - Rows > 0)
		{
			Button("v " + (birds.Count - birdScroll - Rows) + " more", x + 220, iy, 140, () => birdScroll++);
		}
	}

	private static void ClickBird(dmBirdEgg e, BirdClick mode)
	{
		releaseConfirm = null;
		if (mode == BirdClick.Select)
		{
			selected = e;
			return;
		}
		if (e == parentA)
		{
			parentA = null;
		}
		else if (e == parentB)
		{
			parentB = null;
		}
		else if (parentA == null)
		{
			parentA = e;
		}
		else
		{
			parentB = e;
		}
	}

	private static string Axis(int v, string neg, string pos) => v < 0 ? neg + (-v) : v > 0 ? pos + v : neg + "0";

	private static void DrawAxisBar(int x, int y, string neg, string pos, int value)
	{
		Font(18);
		Text(neg, x, y);
		int bx = x + 70;
		for (int i = -5; i <= 5; i++)
		{
			int cx = bx + (i + 5) * 18;
			bool filled = value < 0 ? i >= value && i < 0 : value > 0 ? i <= value && i > 0 : false;
			Color c = i == 0 ? Color.Gray : filled ? (i < 0 ? ShepColor.Orange : ShepColor.Cyan) : new Color(60, 50, 40);
			tr.DrawLine(cx, y + 10, cx + 14, y + 10, 12, c);
		}
		Text(pos, bx + 11 * 18 + 8, y);
		Text("^3" + Axis(value, neg.Substring(0, 1), pos.Substring(0, 1)) + "^7", bx + 11 * 18 + 80, y);
	}

	// ---------- Birds tab ----------
	private static void DrawBirdDetails()
	{
		int x = X0 + 440, y = Y0 + 92;
		dmBirdEgg e = selected;
		if (e == null)
		{
			Text("Select a bird on the left.", x, y);
			return;
		}
		BirdStatInfo info = e.GetStatInfo();
		dmPetBird placed = PoultryCore.Placed(e);
		DrawIcon(e.GetBirdSprite(), x, y, 64, 96);
		Font(30);
		Text(e.BirdName, x + 76, y);
		Font(18);
		Text("Generation ^3" + e.GetGeneration() + "^7   Pluck ^3" + e.GetChampionPoints() + "^7 (+" + e.GetChampionPoints() + "% HP)   Level " + e.BirdLevel
			+ (placed != null ? "   HP ^3" + placed.health + "/" + placed.maxHealth + "^7" : ""), x + 76, y + 36);
		Text((PoultryCore.IsEquipped(e) ? "^5Equipped^7" : PoultryCore.InBag(e) ? "In your bags" : "At the Academy")
			+ (info.bWounded ? "   ^1Wounded^7" : "") + (!e.BirdHasEverHatched ? "   Unhatched: equip it and blow the whistle" : ""), x + 76, y + 58);

		// Action row
		int bx = x, by = y + 100;
		if (PoultryCore.InBag(e))
		{
			Button("Store", bx, by, 70, () => Say(PoultryCore.Store(e)), AtAcademy);
		}
		else
		{
			Button("Take", bx, by, 70, () => Say(PoultryCore.Take(e)), AtAcademy);
		}
		Button("Equip", bx + 80, by, 70, () => Say(PoultryCore.Equip(e)), !PoultryCore.IsEquipped(e) && (AtAcademy || PoultryCore.InBag(e)));
		bool fav = e.BirdName == PoultryCore.FavoriteName;
		Button(fav ? "Unfavorite" : "Favorite", bx + 160, by, 110, () =>
		{
			PoultryCore.FavoriteName = fav ? "" : e.BirdName;
			Say(fav ? "No favorite bird." : e.BirdName + " is your favorite: Prepare hero will equip it.");
		});
		Button("Heal all", bx + 280, by, 90, () => Say(PoultryCore.HealAll()), AtAcademy);
		if (releaseConfirm == e)
		{
			Button("Click again: free for " + PoultryCore.ReleaseFeathers(e) + " Feathers", bx + 380, by, 330, () =>
			{
				releaseConfirm = null;
				Say(PoultryCore.Release(e));
			}, true, ShepColor.Orange);
		}
		else
		{
			Button("Set free", bx + 380, by, 90, () => { releaseConfirm = e; }, AtAcademy && !e.InventoryLocked && !fav);
		}

		// Stats
		int sy = by + 36;
		DrawAxisBar(x, sy, "Tough", "Swift", info.birdStats[0]);
		DrawAxisBar(x, sy + 26, "Ranged", "Melee", info.birdStats[1]);

		// Powers
		int py = sy + 64;
		Text("^3Powers^7 (" + info.powersUnlockedArchetypes.Count + "/4)   Forget costs 1 Pruning Poultice", x, py);
		py += 22;
		foreach (string p in info.powersUnlockedArchetypes.ToList())
		{
			info.powerGenesCollected.TryGetValue(p, out int gcount);
			Text(PoultryCore.PowerName(p) + "  ^7(" + gcount + " genes)^7", x + 10, py);
			Button("forget", x + 300, py - 2, 70, () => Say(PoultryCore.Prune(e, p)), PoultryCore.Count(PoultryCore.Pruning) > 0, ShepColor.Orange);
			py += 22;
		}
		if (info.powersViaStatsArchetypes.Count > 0)
		{
			Text("From stats: " + string.Join(", ", info.powersViaStatsArchetypes.Select(PoultryCore.PowerName)), x + 10, py, Color.LightGray);
			py += 22;
		}
		var unlearned = info.powerGenesCollected.Where(kv => kv.Key != "none" && !info.powersUnlockedArchetypes.Contains(kv.Key))
			.OrderByDescending(kv => kv.Value).Take(5).ToList();
		if (unlearned.Count > 0)
		{
			Text("Genes not learned: " + string.Join(", ", unlearned.Select(kv => PoultryCore.PowerName(kv.Key) + " " + kv.Value)), x + 10, py, Color.LightGray);
			py += 22;
		}
		if (info.sealedPowers.Count > 0)
		{
			Text("Forgotten: " + string.Join(", ", info.sealedPowers.Select(PoultryCore.PowerName)), x + 10, py, Color.Gray);
		}

		DrawFeedList(e, X0 + 440, Y0 + 420);
	}

	private static void DrawFeedList(dmBirdEgg e, int x, int y)
	{
		BirdStatInfo info = e.GetStatInfo();
		var foods = PoultryCore.BagFoods()
			.GroupBy(f => f.Key).Select(gr => { var f = gr.First(); f.Count = gr.Count(); return f; })
			.Concat(AtAcademy ? PoultryCore.PantryFoods() : new List<PoultryCore.Food>())
			.ToList();
		Text("^3Feed^7 (" + foods.Sum(f => f.Count) + " in bags" + (AtAcademy ? " + Pantry" : "") + ")", x, y);
		const int rows = 6;
		feedScroll = Math.Max(0, Math.Min(Math.Max(0, foods.Count - rows), feedScroll));
		for (int i = feedScroll; i < Math.Min(foods.Count, feedScroll + rows); i++)
		{
			PoultryCore.Food f = foods[i];
			int ry = y + 22 + (i - feedScroll) * 22;
			Button("Feed", x, ry, 60, () => Say(PoultryCore.Feed(e, f)), e.BirdHasEverHatched);
			string effect = (f.Tvs != 0 ? Axis(f.Tvs, "T+", "S+").Replace("T+-", "T+") + " " : "") + (f.Mvr != 0 ? Axis(f.Mvr, "R+", "M+") + " " : "")
				+ (f.Tier > 0 ? "^3Pluck+" + f.Tier + "^7 " : "");
			int chance = PoultryCore.LearnChance(info, f.Gene);
			string gene = f.Gene != null && f.Gene != "none" ? PoultryCore.PowerName(f.Gene) + (chance > 0 ? " ^3" + chance + "%^7" : "") : "";
			Text(f.Name + " ^7x" + f.Count + (f.BagItem == null ? " (Pantry)" : "") + "^7   " + effect + gene, x + 70, ry);
		}
		if (foods.Count == 0)
		{
			Text("No steaks or feathers.", x + 10, y + 24, Color.LightGray);
		}
		if (feedScroll > 0)
		{
			Button("^", x + 700, y, 30, () => feedScroll--);
		}
		if (foods.Count - feedScroll - rows > 0)
		{
			Button("v", x + 735, y, 30, () => feedScroll++);
		}
	}

	// ---------- Breed tab ----------
	private static void DrawBreed()
	{
		int x = X0 + 440, y = Y0 + 92;
		Font(18);
		Text("Click two birds on the left to pick the parents (click again to remove). Both parents are kept.", x, y, Color.LightGray);
		Text(Config.On(Config.BreedingFix) ? "Bug fix ON: the first parent keeps its genes." : "^1Bug fix OFF: Parent 1's gene table will be overwritten. Pick your valuable bird second.^7",
			x, y + 22, Color.LightGray);
		Font(24);
		Text("Parent 1: " + (parentA?.BirdName ?? "-"), x, y + 54);
		Text("Parent 2: " + (parentB?.BirdName ?? "-"), x + 380, y + 54);
		Font(18);
		if (parentA == null || parentB == null)
		{
			return;
		}
		if (!parentA.BirdHasEverHatched || !parentB.BirdHasEverHatched)
		{
			Text("^1Both parents must have hatched at least once.^7", x, y + 90);
			return;
		}
		PoultryCore.BreedPreview p = PoultryCore.Preview(parentA, parentB);
		int py = y + 92;
		Text("^3The egg will have:^7", x, py);
		Text("Generation ^3" + p.Generation + "^7" + (parentA.GetGeneration() == parentB.GetGeneration() ? "  (same generation: +1)" : "  (different generations: no +1)"), x + 10, py + 24);
		Text("Pluck ^3" + p.Pluck + "^7   (average, rounded down)", x + 10, py + 46);
		DrawAxisBar(x + 10, py + 72, "Tough", "Swift", p.Tvs);
		DrawAxisBar(x + 10, py + 98, "Ranged", "Melee", p.Mvr);
		int ly = py + 132;
		Text("Powers it can be born with (max 4, rolled from most genes down):", x + 10, ly);
		foreach (var (gene, count, chance) in p.Powers.Take(8))
		{
			ly += 22;
			Color c = chance >= 0.99 ? ShepColor.Cyan : chance >= 0.5 ? Color.White : Color.LightGray;
			Text(PoultryCore.PowerName(gene) + "   " + count + " genes   ^3" + (int)Math.Round(chance * 100) + "%^7", x + 20, ly, c);
		}
		int have = PoultryCore.Count(PoultryCore.Passionate);
		Font(24);
		Button("Breed (1 Passionate Poultice, have " + have + ")", x, Y0 + H - 84, 420, () =>
		{
			Say(PoultryCore.Breed(parentA, parentB));
		}, have > 0);
	}

	// ---------- Ascend tab ----------
	private static void DrawAscend()
	{
		int x = X0 + 440, y = Y0 + 92;
		dmBirdEgg e = selected;
		Font(18);
		Text("+1 Generation for the same bird, without breeding spare birds. It keeps its stats, powers and genes;", x, y, Color.LightGray);
		Text("only Pluck drops:  Pluck after = Pluck - Pluck x 0.5^N  (N = Passionate Poultices spent).", x, y + 22, Color.LightGray);
		if (e == null)
		{
			Text("Select a bird on the left.", x, y + 60);
			return;
		}
		Font(24);
		Text(e.BirdName + ":  Generation " + e.GetGeneration() + " -> ^3" + (e.GetGeneration() + 1) + "^7", x, y + 56);
		Font(18);
		int have = PoultryCore.Count(PoultryCore.Passionate);
		ascendN = Math.Max(1, Math.Min(8, ascendN));
		int ty = y + 96;
		Text("Poultices    Pluck after    Kept", x, ty, ShepColor.Yellow);
		for (int n = 1; n <= 8; n++)
		{
			int nn = n;
			int after = PoultryCore.PluckAfterAscend(e.GetChampionPoints(), n);
			int ry = ty + n * 24;
			bool hot = Hover(x, ry, 380, 22);
			if (n == ascendN)
			{
				tr.DrawLightBox(x - 8, ry - 6, 12, 1);
			}
			Color c = n > have ? Color.Gray : n == ascendN || hot ? ShepColor.Yellow : Color.White;
			Button(n.ToString(), x, ry, 40, () => ascendN = nn, n <= have, c);
			Text(after.ToString(), x + 120, ry, c);
			Text((100 - 100.0 * Math.Pow(0.5, n)).ToString("0.#") + "%", x + 240, ry, c);
		}
		Font(24);
		Button("Ascend: spend " + ascendN + " (have " + have + ")", x, ty + 9 * 24 + 16, 380, () => Say(PoultryCore.Ascend(e, ascendN)), have >= ascendN);
		Font(18);
		Text("Tip: rare steaks (Pluck +1/+2/+3) refill Pluck afterwards.", x, ty + 9 * 24 + 52, Color.LightGray);
	}

	// ---------- Pantry tab ----------
	private static void DrawPantry()
	{
		int x = X0 + 24, y = Y0 + 92;
		Font(18);
		var bag = PoultryCore.BagFoods().GroupBy(f => f.Key).Select(gr => (name: gr.First().Name, item: (dmItem)gr.First().BagItem, n: gr.Count()))
			.Concat(Game.dungeonMans.GetInventoryList().Where(i => PoultryCore.IsPantryItem(i) && !(i is dmEnemyBirdSteak)).GroupBy(i => i.ArchetypeName)
				.Select(gr => (name: gr.First().GetName(), item: gr.First(), n: gr.Count())))
			.ToList();
		Text("^3In your bags^7 (" + bag.Sum(b => b.n) + ")", x, y);
		Button("Store all", x + 260, y - 2, 110, () => Say(PoultryCore.DepositAll()), bag.Count > 0);
		const int rows = 18;
		bagScroll = Math.Max(0, Math.Min(Math.Max(0, bag.Count - rows), bagScroll));
		for (int i = bagScroll; i < Math.Min(bag.Count, bagScroll + rows); i++)
		{
			var b = bag[i];
			int ry = y + 26 + (i - bagScroll) * 24;
			Button("Store", x, ry, 64, () => Say(PoultryCore.Deposit(b.item)), !b.item.InventoryLocked);
			Text(b.name + (b.n > 1 ? " ^7x" + b.n + "^7" : "") + (b.item.InventoryLocked ? " ^7(locked)^7" : ""), x + 74, ry);
		}
		ScrollButtons(x + 420, y, true, bag.Count, rows);

		int px = X0 + 600;
		var pantry = PoultryCore.Pantry;
		Text("^3Pantry^7  " + pantry.Count + "/" + PoultryCore.PantrySlots + " stacks   (Shift-click Take = whole stack)", px, y);
		pantryScroll = Math.Max(0, Math.Min(Math.Max(0, pantry.Count - 14), pantryScroll));
		for (int i = pantryScroll; i < Math.Min(pantry.Count, pantryScroll + 14); i++)
		{
			var kv = pantry[i];
			int ry = y + 26 + (i - pantryScroll) * 24;
			Button("Take", px, ry, 56, () => Say(PoultryCore.Withdraw(kv.Key, Game.theInput.bShiftIsDown ? kv.Value : 1)));
			bool food = !kv.Key.StartsWith("i|");
			if (food)
			{
				PoultryCore.Food f = PoultryCore.FoodFromKey(kv.Key, 1);
				Button("Render +" + (f != null ? PoultryCore.RenderFeathers(f) : 0), px + 62, ry, 100, () => Say(PoultryCore.Render(kv.Key)), f != null, ShepColor.Orange);
			}
			Text(PoultryCore.ItemName(kv.Key) + " ^7x" + kv.Value + "^7", px + 172, ry);
		}
		ScrollButtons(px + 520, y, false, pantry.Count, 14);

		// Shop
		int sy = Y0 + H - 110;
		Font(18);
		Text("^3Feathers shop^7 (" + PoultryCore.Feathers + " Feathers; earn them by setting birds free or rendering steaks):", px, sy - 26);
		Button("+" + PoultryCore.SlotsPerPurchase + " stacks: " + PoultryCore.SlotPrice, px, sy, 170, () => Say(PoultryCore.BuySlots()), PoultryCore.Feathers >= PoultryCore.SlotPrice);
		Button("Passionate Poultice: " + PoultryCore.PoulticeFeatherPrice, px + 180, sy, 220, () => Say(PoultryCore.BuyPoultice(PoultryCore.Passionate)), PoultryCore.Feathers >= PoultryCore.PoulticeFeatherPrice);
		Button("Sell Passionate Poultice: +" + PoultryCore.PoulticeSellFeathers, px, sy + 28, 260, () => Say(PoultryCore.SellPoultice()), PoultryCore.Count(PoultryCore.Passionate) > 0, ShepColor.Orange);
	}

	private static void ScrollButtons(int x, int y, bool isBag, int count, int rows)
	{
		int scroll = isBag ? bagScroll : pantryScroll;
		if (scroll > 0)
		{
			Button("^", x, y, 30, () => { if (isBag) bagScroll--; else pantryScroll--; });
		}
		if (count - scroll - rows > 0)
		{
			Button("v", x + 34, y, 30, () => { if (isBag) bagScroll++; else pantryScroll++; });
		}
	}

	private static void DrawIcon(dmSprite sprite, int x, int y, int maxW, int maxH)
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
