using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// "The Coop": one window for all poultry management (game logic in PoultryCore).
//   Birds  - bird cards on the left; on the right the selected bird: actions, stat map, powers & genes, feeding.
//   Breed  - pick two parents; exact preview of the egg (generation, Pluck, stats, chance of each power).
//   Ascend - +1 generation for N Passionate Poultices; Pluck after = Pluck - floor(Pluck * 0.5^N).
//   Pantry - Academy storage for steaks, Plumage and poultices; render steaks into Feathers; Feathers shop.
// Opens from Hatch's menu, the Academy Directory or the bird status window. Away from the Academy only bag
// contents are available.
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
	private static bool feedAll;
	private static int sortMode; // 0 generation, 1 pluck, 2 name
	private static string message = "";
	private static PoultryCore.Food previewFood, previewFoodLast;

	private static dmGame Game => dmUtilities.theGame;
	public static bool AtAcademy => PoultryCore.Hatch != null;

	public static void Open(int startTab = 0, dmBirdEgg focus = null)
	{
		IsOpen = true;
		tab = startTab;
		selected = focus ?? PoultryCore.AllBirds().FirstOrDefault(PoultryCore.IsEquipped) ?? PoultryCore.AllBirds().FirstOrDefault();
		releaseConfirm = null;
		message = AtAcademy ? "" : "Away from the Academy: only your bags.";
		dmAudio.PlayUIFX("open_dialog");
	}

	public static void ForceClose() => IsOpen = false;

	// DevCapture: pick the two best hatched birds as parents.
	public static void DevPickParents()
	{
		List<dmBirdEgg> birds = VisibleBirds().Where(b => b.BirdHasEverHatched).ToList();
		parentA = birds.ElementAtOrDefault(0);
		parentB = birds.ElementAtOrDefault(1);
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
			Ui.Toast(Ui.StripCodes(s).Length > 70 ? Ui.StripCodes(s).Substring(0, 67) + "..." : s);
		}
	}

	private static void Do(Func<string> action)
	{
		try
		{
			Say(action());
		}
		catch (Exception e)
		{
			Log.Write("Coop action failed: " + e);
			message = "^1That failed^7 (see ModKit\\modkit.log).";
		}
	}

	// ---------------- input ----------------

	private static Rectangle feedRect, bagRect, pantryRect;

	public static void HandleInput(dmGame g)
	{
		List<ActionButton> a = g.theInput.ExecutedActions;
		List<dmBirdEgg> birds = VisibleBirds();
		int wheel = a.Contains(ActionButton.MouseWheel_Up) ? -1 : a.Contains(ActionButton.MouseWheel_Down) ? 1 : 0;
		if (a.Contains(ActionButton.Close))
		{
			Close();
		}
		else if (wheel != 0)
		{
			Point m = Ui.Mouse;
			if (feedRect.Contains(m))
			{
				feedScroll += wheel;
			}
			else if (bagRect.Contains(m))
			{
				bagScroll += wheel * 2;
			}
			else if (pantryRect.Contains(m))
			{
				pantryScroll += wheel * 2;
			}
			else
			{
				birdScroll += wheel;
			}
		}
		else if (a.Contains(ActionButton.Left) || a.Contains(ActionButton.ArrowKey_Left))
		{
			SetTab((tab + Tabs.Length - 1) % Tabs.Length);
		}
		else if (a.Contains(ActionButton.Right) || a.Contains(ActionButton.ArrowKey_Right))
		{
			SetTab((tab + 1) % Tabs.Length);
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

	private static void SetTab(int t)
	{
		if (t == 3 && !AtAcademy)
		{
			return;
		}
		tab = t;
		releaseConfirm = null;
	}

	private static List<dmBirdEgg> VisibleBirds()
	{
		IOrderedEnumerable<dmBirdEgg> birds = (AtAcademy ? PoultryCore.AllBirds() : PoultryCore.BagBirds())
			.OrderByDescending(PoultryCore.IsEquipped)
			.ThenByDescending(e => e.BirdName == PoultryCore.FavoriteName);
		birds = sortMode == 1 ? birds.ThenByDescending(e => e.GetChampionPoints()).ThenByDescending(e => e.GetGeneration())
			: sortMode == 2 ? birds.ThenBy(e => e.BirdName)
			: birds.ThenByDescending(e => e.GetGeneration()).ThenByDescending(e => e.GetChampionPoints());
		return birds.ToList();
	}

	// ---------------- drawing ----------------

	private const int WUnits = 38, HUnits = 21;

	public static void Draw(dmGame g)
	{
		wg2DRenderer tr = g.theRenderer;
		if (selected != null && !PoultryCore.AllBirds().Contains(selected))
		{
			selected = null;
		}
		previewFood = null;
		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		Rectangle c = Ui.ModalWindow("qol_coop", WUnits, HUnits, "The Coop", Close, AtAcademy ? null : "bags only (away from the Academy)");

		// Tabs + currencies.
		int th = Ui.Tabs("qol_coop", c.X, c.Y, Tabs, tab, SetTab, new[] { true, true, true, AtAcademy });
		int cx = c.Right;
		cx = ChipRight(cx, c.Y + 6, "Pruning " + PoultryCore.Count(PoultryCore.Pruning), Palette.TextDim, "Pruning Poultices: forget a power");
		cx = ChipRight(cx, c.Y + 6, "Passionate " + PoultryCore.Count(PoultryCore.Passionate), PoultryHud.Melee, "Passionate Poultices: breeding and Ascend");
		ChipRight(cx, c.Y + 6, "Feathers " + PoultryCore.Feathers, Palette.Accent, "Feathers: earned by freeing birds, rendering steaks, selling poultices");

		var body = new Rectangle(c.X, c.Y + th + 10, c.Width, c.Height - th - 10 - 34);
		var right = new Rectangle(body.X + ListW + 12, body.Y, body.Width - ListW - 12, body.Height);
		switch (tab)
		{
		case 0:
			DrawBirdList(body, BirdClick.Select);
			DrawBirdDetails(right);
			break;
		case 1:
			DrawBirdList(body, BirdClick.Parent);
			DrawBreed(right);
			break;
		case 2:
			DrawBirdList(body, BirdClick.Select);
			DrawAscend(right);
			break;
		case 3:
			DrawPantry(body);
			break;
		}

		Ui.Separator(c.X, c.Bottom - 28, c.Width);
		Ui.Text(string.IsNullOrEmpty(message) ? "^3Left/Right^7 tabs   ^3Up/Down^7 birds   ^3Wheel^7 scroll   ^3Esc^7 close" : message,
			c.X + 4, c.Bottom - 22, Palette.TextDim);
		tr.EndSpriteBatch();
		previewFoodLast = previewFood;
	}

	private static int ChipRight(int right, int y, string text, Color color, string tip)
	{
		int w = (int)Ui.Measure(text).X + 12;
		int x = right - w;
		ui_mouseFrame f = Ui.Area("qol_coop_chip_" + text.Split(' ')[0], new Rectangle(x, y, w, 22), null, Ui.WindowLevel + 1);
		Ui.Chip(x, y, text, color);
		if (Ui.Hovered(f))
		{
			Ui.Tooltip(tip);
		}
		return x - 8;
	}

	// ---------- bird list (left column) ----------

	private enum BirdClick { Select, Parent }

	private const int ListW = 372, CardH = 62;

	private static void DrawBirdList(Rectangle body, BirdClick mode)
	{
		List<dmBirdEgg> birds = VisibleBirds();
		int x = body.X, y = body.Y;
		Ui.Text("Sort", x, y + 4, Palette.TextDim);
		string[] sorts = { "Gen", "Pluck", "Name" };
		int sx = x + 40;
		for (int i = 0; i < sorts.Length; i++)
		{
			int s = i;
			Ui.BoxButton("qol_coop_sort" + i, new Rectangle(sx, y, 64, 26), sorts[i], () => sortMode = s, selected: sortMode == i);
			sx += 68;
		}
		Ui.TextRight(birds.Count + " bird" + (birds.Count == 1 ? "" : "s"), x + ListW, y + 4, Palette.TextDim);

		var list = new Rectangle(x, y + 34, ListW, body.Height - 34);
		int rows = list.Height / CardH;
		birdScroll = Math.Max(0, Math.Min(Math.Max(0, birds.Count - rows), birdScroll));
		for (int vi = 0; vi < rows && birdScroll + vi < birds.Count; vi++)
		{
			DrawBirdCard(birds[birdScroll + vi], new Rectangle(x, list.Y + vi * CardH, ListW - 12, CardH - 6), mode, vi);
		}
		if (birds.Count == 0)
		{
			Ui.TextCentered("No birds here.", list, Palette.TextDim, UiFont.Medium);
		}
		Scrollbar(new Rectangle(x + ListW - 7, list.Y, 5, rows * CardH - 6), birdScroll, rows, birds.Count);
	}

	private static void Scrollbar(Rectangle track, int first, int visible, int total)
	{
		if (total <= visible)
		{
			return;
		}
		Ui.Fill(track, Palette.BarBack);
		int th = Math.Max(20, track.Height * visible / total);
		int ty = track.Y + (track.Height - th) * first / Math.Max(1, total - visible);
		Ui.Fill(new Rectangle(track.X, ty, track.Width, th), Palette.Border);
	}

	private static void DrawBirdCard(dmBirdEgg e, Rectangle r, BirdClick mode, int slot)
	{
		bool isSel = mode == BirdClick.Select ? e == selected : e == parentA || e == parentB;
		ui_mouseFrame f = Ui.Area("qol_coop_card" + slot, r, () => ClickBird(e, mode), Ui.WindowLevel + 1);
		bool hot = Ui.Hovered(f);
		Ui.Panel(r, isSel ? Palette.PanelSelected : hot ? Palette.PanelHover : Palette.PanelRaised, isSel || hot ? Palette.BorderBright : Palette.Border);
		if (PoultryCore.IsEquipped(e))
		{
			Ui.Fill(new Rectangle(r.X, r.Y, 4, r.Height), Palette.Info);
		}

		var portrait = new Rectangle(r.X + 8, r.Y + 5, 46, r.Height - 10);
		Ui.Fill(portrait, Palette.BarBack);
		Ui.Sprite(e.GetBirdSprite(), portrait, e.BirdHasEverHatched ? (Color?)null : new Color(120, 120, 120));

		int tx = r.X + 62;
		if (e.BirdName == PoultryCore.FavoriteName)
		{
			Ui.Icon("ui_favorite_heart", new Rectangle(tx, r.Y + 7, 18, 18));
			tx += 22;
		}
		Ui.Text(Ui.Fit(e.BirdName, r.Right - 70 - tx, UiFont.Medium), tx, r.Y + 3, isSel ? Palette.Accent : Palette.Text, UiFont.Medium);

		int cx = r.X + 62, cy = r.Y + 31;
		cx = Ui.Chip(cx, cy, "G" + e.GetGeneration(), Palette.Accent);
		cx = Ui.Chip(cx, cy, "P" + e.GetChampionPoints(), Palette.Text);
		cx = PoultryCore.IsEquipped(e) ? Ui.Chip(cx, cy, "Equipped", Palette.Info) : Ui.Chip(cx, cy, PoultryCore.InBag(e) ? "Bag" : "Academy", Palette.TextDim);
		if (e.GetStatInfo().bWounded)
		{
			cx = Ui.Chip(cx, cy, "Hurt", Palette.Bad);
		}
		if (!e.BirdHasEverHatched)
		{
			Ui.Chip(cx, cy, "Egg", Palette.TextMuted);
		}
		if (mode == BirdClick.Parent && (e == parentA || e == parentB))
		{
			Ui.Chip(r.Right - 96, r.Y + 6, e == parentA ? "P1" : "P2", Palette.Accent);
		}

		Point s = e.GetStatAxis();
		StatMap(new Rectangle(r.Right - 52, r.Y + (r.Height - 44) / 2, 44, 44), new[] { (s.X, s.Y, Palette.Accent) }, mini: true);
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

	// ---------- shared pieces ----------

	private static readonly (string power, string name, int x0, int x1, int y0, int y1)[] StatZones =
	{
		("sp_bird_shields_up", "Shields Up", -5, -3, -5, -3),
		("sp_ai_bird_stick_and_move", "Stick and Move", 3, 5, -5, -3),
		("sp_bird_intercept", "Intercept", -5, -3, 3, 5),
		("sp_bird_dashing_charge", "Dashing Charge", 3, 5, 3, 5),
	};

	// 11x11 map of the two stat axes: x = Tough(-5) .. Swift(+5), y = Ranged(-5, top) .. Melee(+5, bottom).
	// Gold corners: stat powers. Red band (Melee 3+): Press the Attack.
	private static void StatMap(Rectangle r, IEnumerable<(int x, int y, Color c)> dots, bool mini = false, (int x, int y)? ghost = null)
	{
		Ui.Fill(r, Palette.BarBack);
		float cw = r.Width / 11f, ch = r.Height / 11f;
		Rectangle Cell(int sx, int sy) => new Rectangle(r.X + (int)((sx + 5) * cw), r.Y + (int)((sy + 5) * ch), (int)Math.Ceiling(cw), (int)Math.Ceiling(ch));
		for (int sx = -5; sx <= 5; sx++)
		{
			for (int sy = 3; sy <= 5; sy++)
			{
				Ui.Fill(Cell(sx, sy), new Color((int)PoultryHud.Melee.R, PoultryHud.Melee.G, PoultryHud.Melee.B, 34));
			}
		}
		foreach (var z in StatZones)
		{
			for (int sx = z.x0; sx <= z.x1; sx++)
			{
				for (int sy = z.y0; sy <= z.y1; sy++)
				{
					Ui.Fill(Cell(sx, sy), new Color(250, 206, 92, 50));
				}
			}
		}
		Ui.Fill(new Rectangle(r.X + r.Width / 2, r.Y, 1, r.Height), new Color(255, 255, 255, 40));
		Ui.Fill(new Rectangle(r.X, r.Y + r.Height / 2, r.Width, 1), new Color(255, 255, 255, 40));
		Ui.Outline(r, Palette.Border);
		if (ghost.HasValue)
		{
			Rectangle gc = Cell(ghost.Value.x, ghost.Value.y);
			Ui.Outline(new Rectangle(gc.X - 1, gc.Y - 1, gc.Width + 2, gc.Height + 2), Color.White, mini ? 1 : 2);
		}
		foreach (var d in dots)
		{
			Rectangle dc = Cell(Math.Clamp(d.x, -5, 5), Math.Clamp(d.y, -5, 5));
			int pad = mini ? 0 : 1;
			Ui.Fill(new Rectangle(dc.X + pad, dc.Y + pad, dc.Width - pad * 2, dc.Height - pad * 2), d.c);
		}
	}

	private static void StatMapWithLabels(string id, Rectangle r, IEnumerable<(int x, int y, Color c)> dots, (int x, int y)? ghost = null)
	{
		StatMap(r, dots, false, ghost);
		Ui.TextCentered("Ranged", new Rectangle(r.X, r.Y - 20, r.Width, 18), PoultryHud.Ranged);
		Ui.TextCentered("Melee", new Rectangle(r.X, r.Bottom + 2, r.Width, 18), PoultryHud.Melee);
		Ui.TextRight("Tough", r.X - 6, r.Y + r.Height / 2 - 10, PoultryHud.Tough);
		Ui.Text("Swift", r.Right + 6, r.Y + r.Height / 2 - 10, PoultryHud.Swift);
		ui_mouseFrame f = Ui.Area(id, r, null, Ui.WindowLevel + 1);
		if (Ui.Hovered(f))
		{
			Point m = Ui.Mouse;
			int sx = (int)((m.X - r.X) / (r.Width / 11f)) - 5, sy = (int)((m.Y - r.Y) / (r.Height / 11f)) - 5;
			string zone = StatZones.Where(z => sx >= z.x0 && sx <= z.x1 && sy >= z.y0 && sy <= z.y1).Select(z => z.name).FirstOrDefault();
			Ui.Tooltip("Tough / Swift across, Ranged / Melee down."
				+ "\nGold corners give a stat power" + (zone != null ? ": here ^3" + zone + "^7" : ".")
				+ "\nRed band (Melee 3+): Press the Attack." + (ghost.HasValue ? "\nWhite square: after the hovered food." : ""));
		}
	}

	private static void StatRows(int x, int y, int w, int t, int m, int pluck, int gen, string id)
	{
		Row(id + "_tvs", x, y, w, "Tough", "Swift", t, PoultryHud.Tough, PoultryHud.Swift, PoultryHud.ToughSwiftHelp(t, pluck));
		Row(id + "_mvr", x, y + 26, w, "Ranged", "Melee", m, PoultryHud.Ranged, PoultryHud.Melee, PoultryHud.RangedMeleeHelp(m, gen));

		static void Row(string id, int x, int y, int w, string neg, string pos, int v, Color nc, Color pc, string help)
		{
			ui_mouseFrame f = Ui.Area(id, new Rectangle(x, y, w, 22), null, Ui.WindowLevel + 1);
			Ui.Text(neg + (v < 0 ? " " + -v : ""), x, y + 1, v < 0 ? nc : Palette.TextMuted);
			Ui.TextRight((v > 0 ? v + " " : "") + pos, x + w, y + 1, v > 0 ? pc : Palette.TextMuted);
			Ui.CenterBar(new Rectangle(x + 78, y + 3, w - 156, 16), v, 5, nc, pc);
			if (Ui.Hovered(f))
			{
				Ui.Tooltip(help);
			}
		}
	}

	private static void Section(Rectangle r, string title)
	{
		Ui.Panel(r, new Color(16, 12, 9, 170), Palette.Border);
		Ui.Text(title.ToUpperInvariant(), r.X + 10, r.Y + 6, Palette.Accent);
	}

	// ---------- Birds tab ----------

	private static void DrawBirdDetails(Rectangle area)
	{
		dmBirdEgg e = selected;
		if (e == null)
		{
			Ui.TextCentered("Pick a bird on the left.", area, Palette.TextDim, UiFont.Medium);
			return;
		}
		BirdStatInfo info = e.GetStatInfo();
		dmPetBird placed = PoultryCore.Placed(e);
		int x = area.X, y = area.Y;

		// Header card: portrait, name, chips, actions, HP.
		var head = new Rectangle(x, y, area.Width, 112);
		Ui.Panel(head, Palette.PanelRaised, Palette.Border);
		var portrait = new Rectangle(x + 8, y + 8, 72, 96);
		Ui.Fill(portrait, Palette.BarBack);
		Ui.Sprite(e.GetBirdSprite(), portrait);
		bool fav = e.BirdName == PoultryCore.FavoriteName;
		Ui.Text(e.BirdName, x + 92, y + 4, Palette.Text, UiFont.Large);
		int cx = x + 92, cy = y + 42;
		cx = Ui.Chip(cx, cy, "Generation " + e.GetGeneration(), Palette.Accent);
		cx = Ui.Chip(cx, cy, "Pluck " + e.GetChampionPoints(), Palette.Text);
		cx = Ui.Chip(cx, cy, "Level " + e.BirdLevel, Palette.TextDim);
		cx = Ui.Chip(cx, cy, PoultryCore.IsEquipped(e) ? "Equipped" : PoultryCore.InBag(e) ? "In your bags" : "At the Academy", PoultryCore.IsEquipped(e) ? Palette.Info : Palette.TextDim);
		if (info.bWounded)
		{
			cx = Ui.Chip(cx, cy, "Wounded", Palette.Bad);
		}
		if (!e.BirdHasEverHatched)
		{
			Ui.Chip(cx, cy, "Unhatched", Palette.TextMuted);
		}

		int bx = x + 92, by = y + 74, bh = 28;
		bool inBag = PoultryCore.InBag(e);
		bx = Act("store", bx, by, bh, inBag ? "Store" : "Take", () => Do(() => inBag ? PoultryCore.Store(e) : PoultryCore.Take(e)), AtAcademy,
			inBag ? "Leave it at the Academy" : "Put it in your bags");
		bx = Act("equip", bx, by, bh, "Equip", () => Do(() => PoultryCore.Equip(e)), !PoultryCore.IsEquipped(e) && (AtAcademy || inBag), "Put it in your egg pocket");
		bx = Act("fav", bx, by, bh, fav ? "Unfavorite" : "Favorite", () =>
		{
			PoultryCore.FavoriteName = fav ? "" : e.BirdName;
			Say(fav ? "No favorite bird." : e.BirdName + " is your favorite.");
		}, true, "Prepare hero equips your favorite bird");
		bx = Act("heal", bx, by, bh, "Heal all", () => Do(PoultryCore.HealAll), AtAcademy, "Heal every wounded bird");
		if (releaseConfirm == e)
		{
			Ui.BoxButton("qol_coop_free", new Rectangle(bx, by, 190, bh), "Confirm: +" + PoultryCore.ReleaseFeathers(e) + " Feathers", () =>
			{
				releaseConfirm = null;
				Do(() => PoultryCore.Release(e));
			}, accent: Palette.Bad);
		}
		else
		{
			Act("free", bx, by, bh, "Set free", () => releaseConfirm = e, AtAcademy && !e.InventoryLocked && !fav,
				fav ? "Unfavorite it first" : e.InventoryLocked ? "It's locked" : "Gone for good: +" + PoultryCore.ReleaseFeathers(e) + " Feathers (asks again)", Palette.Bad);
		}
		if (placed != null && placed.maxHealth > 0)
		{
			float frac = placed.health / (float)placed.maxHealth;
			Ui.Bar(new Rectangle(head.Right - 200, y + 12, 188, 22), frac, Palette.Health(frac), "HP " + placed.health + " / " + placed.maxHealth);
		}
		y = head.Bottom + 8;

		// Stats: map + bars + combat numbers.
		int t = info.birdStats[0], m = info.birdStats[1];
		var stats = new Rectangle(x, y, 430, 204);
		Section(stats, "Stats");
		(int, int)? ghost = previewFoodLast != null ? (Math.Clamp(t + previewFoodLast.Tvs, -5, 5), Math.Clamp(m + previewFoodLast.Mvr, -5, 5)) : null;
		StatMapWithLabels("qol_coop_map", new Rectangle(stats.X + 58, stats.Y + 46, 132, 132), new[] { (t, m, Palette.Accent) }, ghost);
		int rx = stats.X + 240, rw = stats.Right - 10 - rx;
		StatRows(rx, stats.Y + 34, rw, t, m, e.GetChampionPoints(), e.GetGeneration(), "qol_coop_sr");
		int hpPct = (t < 0 ? -10 * t : -4 * t) + e.GetChampionPoints();
		int iy = stats.Y + 98;
		IconLine("ui_attack_block", rx, iy, "Block " + Math.Max(0, -8 * t), "From Tough (8 per point)");
		IconLine("ui_attack_parry", rx + 96, iy, "Parry " + Math.Max(0, 6 * t), "From Swift (6 per point)");
		IconLine("condition_regen", rx, iy + 28, "HP " + (hpPct >= 0 ? "+" : "") + hpPct + "%", "Tough +10%/pt, Swift -4%/pt, Pluck +1%/pt");
		IconLine("ui_attack_counter", rx + 96, iy + 28, m < 0 ? "Range " + (2 - m) : "Melee", m < 0 ? "Ranged attacks from " + (2 - m) + " tiles" : "Fights up close");
		IconLine("condition_atk_up", rx, iy + 56, "Damage +" + 20 * (e.GetGeneration() - 1) + "%", "+20% per generation after the first");

		// Powers & genes.
		var pw = new Rectangle(stats.Right + 8, y, area.Right - stats.Right - 8, 204);
		Section(pw, "Powers " + info.powersUnlockedArchetypes.Count + "/4");
		int py = pw.Y + 30;
		foreach (string p in info.powersUnlockedArchetypes.ToList())
		{
			info.powerGenesCollected.TryGetValue(p, out int g);
			PowerTile(p, pw.X + 10, py, pw.Width - 20, g + " genes", () => Do(() => PoultryCore.Prune(e, p)));
			py += 30;
		}
		foreach (string p in info.powersViaStatsArchetypes)
		{
			PowerTile(p, pw.X + 10, py, pw.Width - 20, "from stats", null);
			py += 30;
		}
		if (info.powersUnlockedArchetypes.Count + info.powersViaStatsArchetypes.Count == 0)
		{
			Ui.Text("None yet: feed steaks with a power gene.", pw.X + 10, py + 2, Palette.TextMuted);
			py += 26;
		}
		var genes = info.powerGenesCollected.Where(kv => kv.Key != "none" && !info.powersUnlockedArchetypes.Contains(kv.Key) && !info.sealedPowers.Contains(kv.Key))
			.OrderByDescending(kv => kv.Value).ToList();
		if (genes.Count > 0 && py < pw.Bottom - 30)
		{
			Ui.Text("Genes", pw.X + 10, py + 2, Palette.TextDim);
			int gx = pw.X + 62;
			foreach (var kv in genes)
			{
				string label = PoultryCore.PowerName(kv.Key) + " " + kv.Value;
				int gw = (int)Ui.Measure(label).X + 12;
				if (gx + gw > pw.Right - 8)
				{
					break;
				}
				ui_mouseFrame f = Ui.Area("qol_coop_gene_" + kv.Key, new Rectangle(gx, py, gw, 22), null, Ui.WindowLevel + 1);
				gx = Ui.Chip(gx, py, label, Palette.TextDim);
				if (Ui.Hovered(f))
				{
					Ui.Tooltip("^3" + PoultryCore.PowerName(kv.Key) + "^7: " + kv.Value + " genes, not learned yet."
						+ "\nNext steak with this gene: " + PoultryCore.LearnChance(info, kv.Key) + "% to learn it.");
				}
			}
		}
		if (info.sealedPowers.Count > 0)
		{
			Ui.Text("Forgotten: " + string.Join(", ", info.sealedPowers.Select(PoultryCore.PowerName)), pw.X + 10, pw.Bottom - 24, Palette.TextMuted);
		}
		y = stats.Bottom + 8;

		DrawFeedList(e, new Rectangle(x, y, area.Width, area.Bottom - y));
	}

	private static int Act(string id, int x, int y, int h, string label, Action onClick, bool enabled, string tip, Color? accent = null)
	{
		int w = Ui.ButtonWidth(label) + 8;
		Ui.BoxButton("qol_coop_act_" + id, new Rectangle(x, y, w, h), label, onClick, enabled, tooltipText: tip, accent: accent);
		return x + w + 6;
	}

	private static void IconLine(string icon, int x, int y, string text, string tip)
	{
		int w = 24 + (int)Ui.Measure(text).X;
		ui_mouseFrame f = Ui.Area("qol_coop_il_" + icon, new Rectangle(x, y, w, 22), null, Ui.WindowLevel + 1);
		Ui.Icon(icon, new Rectangle(x, y + 1, 20, 20));
		Ui.Text(text, x + 24, y + 1, Palette.Text);
		if (Ui.Hovered(f))
		{
			Ui.Tooltip(tip);
		}
	}

	private static void PowerTile(string power, int x, int y, int w, string note, Action forget)
	{
		var icon = new Rectangle(x, y, 26, 26);
		if (!Ui.PowerIcon(power, icon))
		{
			Ui.Panel(icon);
		}
		Ui.Text(PoultryCore.PowerName(power), x + 34, y + 3, Palette.Text);
		Ui.TextRight(note, x + w - (forget != null ? 80 : 0), y + 3, Palette.TextDim);
		ui_mouseFrame f = Ui.Area("qol_coop_pt_" + power, new Rectangle(x, y, w - 80, 26), null, Ui.WindowLevel + 1);
		if (Ui.Hovered(f))
		{
			dmSpecialPower sp = Ui.Power(power);
			Ui.Tooltip("^3" + PoultryCore.PowerName(power) + "^7" + (sp != null && !string.IsNullOrEmpty(PowerText(sp)) ? "\n" + Wrap(Ui.StripCodes(PowerText(sp)), 60) : ""));
		}
		if (forget != null)
		{
			bool can = PoultryCore.Count(PoultryCore.Pruning) > 0;
			Ui.BoxButton("qol_coop_forget_" + power, new Rectangle(x + w - 72, y, 72, 26), "Forget", forget, can,
				tooltipText: can ? "Costs 1 Pruning Poultice" : "Needs a Pruning Poultice", accent: Palette.Warn);
		}
	}

	private static string PowerText(dmSpecialPower sp)
	{
		try
		{
			return sp.GetTooltipMainText();
		}
		catch
		{
			return sp.strFlavorText;
		}
	}

	private static string Wrap(string s, int width)
	{
		var lines = new List<string>();
		string line = "";
		foreach (string word in s.Split(' '))
		{
			if (line.Length + word.Length + 1 > width && line.Length > 0)
			{
				lines.Add(line);
				line = "";
			}
			line += (line.Length > 0 ? " " : "") + word;
		}
		lines.Add(line);
		return string.Join("\n", lines.Take(6));
	}

	private static void DrawFeedList(dmBirdEgg e, Rectangle r)
	{
		BirdStatInfo info = e.GetStatInfo();
		List<PoultryCore.Food> all = PoultryCore.BagFoods()
			.GroupBy(f => f.Key).Select(gr => { var f = gr.First(); f.Count = gr.Count(); return f; })
			.Concat(AtAcademy ? PoultryCore.PantryFoods() : new List<PoultryCore.Food>())
			.ToList();
		List<PoultryCore.Food> foods = (feedAll ? all : all.Where(f => f.Useful))
			.OrderByDescending(f => f.IsPlumage).ThenByDescending(f => f.Tier).ThenBy(f => f.Name).ToList();
		int hidden = all.Count(f => !f.Useful);
		Section(r, "Feed");
		Ui.BoxButton("qol_coop_feedfilter", new Rectangle(r.X + 64, r.Y + 3, 150, 22), feedAll ? "Showing: all" : "Showing: rare only",
			() => feedAll = !feedAll, tooltipText: feedAll ? "Only foods that raise Pluck or teach a power" : "Also show " + hidden + " plain steak type(s): they only shift stats");
		Ui.TextRight(foods.Sum(f => f.Count) + " shown" + (AtAcademy ? " (bags + Pantry)" : " (bags)"), r.Right - 12, r.Y + 5, Palette.TextDim);

		feedRect = new Rectangle(r.X, r.Y + 30, r.Width, r.Height - 30);
		const int rowH = 30;
		int rows = Math.Max(1, (feedRect.Height - 4) / rowH);
		feedScroll = Math.Max(0, Math.Min(Math.Max(0, foods.Count - rows), feedScroll));
		for (int vi = 0; vi < rows && feedScroll + vi < foods.Count; vi++)
		{
			PoultryCore.Food f = foods[feedScroll + vi];
			var row = new Rectangle(r.X + 8, feedRect.Y + vi * rowH, r.Width - 26, rowH - 4);
			ui_mouseFrame hf = Ui.Area("qol_coop_feedrow" + vi, row, null, Ui.WindowLevel + 1);
			if (Ui.Hovered(hf))
			{
				Ui.Fill(row, Palette.PanelHover);
				previewFood = f;
			}
			Ui.BoxButton("qol_coop_feed" + vi, new Rectangle(row.X + 2, row.Y + 1, 56, row.Height - 2), "Feed", () => Do(() => PoultryCore.Feed(e, f)),
				e.BirdHasEverHatched, tooltipText: e.BirdHasEverHatched ? null : "Hatch it first", stackLevel: Ui.WindowLevel + 2);
			Ui.Text(Ui.Fit(f.Name, 250), row.X + 68, row.Y + 3, f.IsPlumage ? Palette.Info : f.Tier > 0 ? Palette.Accent : Palette.Text);
			int cx = row.X + 330;
			cx = Ui.Chip(cx, row.Y + 1, "x" + f.Count + (f.BagItem == null ? " Pantry" : ""), Palette.TextDim);
			if (f.Tier > 0)
			{
				cx = Ui.Chip(cx, row.Y + 1, "Pluck +" + f.Tier, Palette.Accent);
			}
			if (f.Tvs != 0)
			{
				cx = Ui.Chip(cx, row.Y + 1, f.Tvs < 0 ? "Tough +" + -f.Tvs : "Swift +" + f.Tvs, f.Tvs < 0 ? PoultryHud.Tough : PoultryHud.Swift);
			}
			if (f.Mvr != 0)
			{
				cx = Ui.Chip(cx, row.Y + 1, f.Mvr < 0 ? "Ranged +" + -f.Mvr : "Melee +" + f.Mvr, f.Mvr < 0 ? PoultryHud.Ranged : PoultryHud.Melee);
			}
			if (f.Gene != null && f.Gene != "none")
			{
				bool known = info.powersUnlockedArchetypes.Contains(f.Gene);
				int chance = f.IsPlumage ? 100 : PoultryCore.LearnChance(info, f.Gene);
				Ui.Chip(cx, row.Y + 1, PoultryCore.PowerName(f.Gene) + (known ? " (known)" : " " + chance + "%"), known ? Palette.TextMuted : Palette.Good);
			}
		}
		if (foods.Count == 0)
		{
			Ui.TextCentered(feedAll || hidden == 0 ? "No steaks or Plumage." : "No rare steaks or Plumage (" + hidden + " plain type(s) hidden).", feedRect, Palette.TextMuted);
		}
		Scrollbar(new Rectangle(r.Right - 12, feedRect.Y, 5, rows * rowH - 4), feedScroll, rows, foods.Count);
	}

	// ---------- Breed tab ----------

	private static void DrawBreed(Rectangle area)
	{
		int x = area.X, y = area.Y;
		bool fix = Config.On(Config.BreedingFix);
		Ui.Text("Click two birds on the left. Both parents are kept.", x, y + 2, Palette.TextDim);
		string fixText = fix ? "Gene bug fix: ON" : "Gene bug fix OFF: P1 loses its genes";
		Ui.Chip(area.Right - (int)Ui.Measure(fixText).X - 14, y, fixText, fix ? Palette.Good : Palette.Bad);
		y += 32;

		int cardW = (area.Width - 40) / 3;
		ParentCard(new Rectangle(x, y, cardW, 150), parentA, "Parent 1");
		Ui.TextCentered("+", new Rectangle(x + cardW, y, 20, 150), Palette.Accent, UiFont.Large);
		ParentCard(new Rectangle(x + cardW + 20, y, cardW, 150), parentB, "Parent 2");
		Ui.TextCentered("=", new Rectangle(x + 2 * cardW + 20, y, 20, 150), Palette.Accent, UiFont.Large);
		var egg = new Rectangle(x + 2 * cardW + 40, y, cardW, 150);

		if (parentA == null || parentB == null)
		{
			Ui.Panel(egg, Palette.Panel, Palette.Border);
			Ui.TextCentered("Egg preview", egg, Palette.TextMuted, UiFont.Medium);
			return;
		}
		if (!parentA.BirdHasEverHatched || !parentB.BirdHasEverHatched)
		{
			Ui.Panel(egg, Palette.Panel, Palette.Bad);
			Ui.TextCentered("Both must have hatched", egg, Palette.Bad);
			return;
		}
		PoultryCore.BreedPreview p = PoultryCore.Preview(parentA, parentB);
		Ui.Panel(egg, Palette.PanelSelected, Palette.BorderBright);
		Ui.Text("The egg", egg.X + 10, egg.Y + 6, Palette.Accent, UiFont.Medium);
		int ex = Ui.Chip(egg.X + 10, egg.Y + 38, "Generation " + p.Generation, Palette.Accent);
		Ui.Chip(ex, egg.Y + 38, "Pluck " + p.Pluck, Palette.Text);
		Ui.Text(parentA.GetGeneration() == parentB.GetGeneration() ? "Same generation: +1" : "Different generations: no +1", egg.X + 10, egg.Y + 66, Palette.TextDim);
		StatRows(egg.X + 10, egg.Y + 94, egg.Width - 20, p.Tvs, p.Mvr, p.Pluck, p.Generation, "qol_coop_egg");
		y += 160;

		var map = new Rectangle(x, y, 300, area.Bottom - y - 44);
		Section(map, "Stats");
		Point a = parentA.GetStatAxis(), b = parentB.GetStatAxis();
		int ms = Math.Max(66, Math.Min(map.Height - 86, 154));
		StatMapWithLabels("qol_coop_bmap", new Rectangle(map.X + (map.Width - ms) / 2, map.Y + 46, ms, ms),
			new[] { (a.X, a.Y, PoultryHud.Swift), (b.X, b.Y, PoultryHud.Ranged), (p.Tvs, p.Mvr, Palette.Accent) });
		int lx = Ui.Chip(map.X + 10, map.Bottom - 28, "P1", PoultryHud.Swift);
		lx = Ui.Chip(lx, map.Bottom - 28, "P2", PoultryHud.Ranged);
		Ui.Chip(lx, map.Bottom - 28, "Egg", Palette.Accent);

		var odds = new Rectangle(map.Right + 8, y, area.Right - map.Right - 8, area.Bottom - y - 44);
		Section(odds, "Powers it can be born with (max 4)");
		int oy = odds.Y + 32;
		foreach (var (gene, count, chance) in p.Powers.Take(Math.Max(1, (odds.Height - 40) / 28)))
		{
			var icon = new Rectangle(odds.X + 10, oy, 24, 24);
			if (!Ui.PowerIcon(gene, icon))
			{
				Ui.Panel(icon);
			}
			Ui.Text(Ui.Fit(PoultryCore.PowerName(gene), 180), odds.X + 42, oy + 2, Palette.Text);
			Ui.Text(count + " genes", odds.X + 230, oy + 2, Palette.TextDim);
			int pct = (int)Math.Round(chance * 100);
			Ui.Bar(new Rectangle(odds.X + 310, oy + 3, odds.Width - 320, 18), (float)chance, chance >= 0.99 ? Palette.Good : chance >= 0.5 ? Palette.Accent : Palette.Warn, pct + "%");
			oy += 28;
		}
		if (p.Powers.Count == 0)
		{
			Ui.Text("No power genes in either parent.", odds.X + 10, oy, Palette.TextMuted);
		}

		int have = PoultryCore.Count(PoultryCore.Passionate);
		Ui.BoxButton("qol_coop_breed", new Rectangle(area.Right - 380, area.Bottom - 36, 380, 34), "Breed  (1 Passionate Poultice, have " + have + ")",
			() => Do(() => PoultryCore.Breed(parentA, parentB)), have > 0, font: UiFont.Medium, tooltipText: have > 0 ? null : "Needs a Passionate Poultry Poultice");
	}

	private static void ParentCard(Rectangle r, dmBirdEgg e, string label)
	{
		if (e == null)
		{
			Ui.Panel(r, Palette.Panel, Palette.Border);
			Ui.TextCentered(label + ": pick a bird", r, Palette.TextMuted);
			return;
		}
		Ui.Panel(r, Palette.PanelRaised, Palette.Border);
		var portrait = new Rectangle(r.X + 8, r.Y + 8, 44, 60);
		Ui.Fill(portrait, Palette.BarBack);
		Ui.Sprite(e.GetBirdSprite(), portrait);
		Ui.Text(label, r.X + 60, r.Y + 6, Palette.TextDim);
		Ui.Text(Ui.Fit(e.BirdName, r.Width - 70, UiFont.Medium), r.X + 60, r.Y + 24, Palette.Text, UiFont.Medium);
		int cx = Ui.Chip(r.X + 60, r.Y + 54, "G" + e.GetGeneration(), Palette.Accent);
		Ui.Chip(cx, r.Y + 54, "P" + e.GetChampionPoints(), Palette.Text);
		Point s = e.GetStatAxis();
		StatRows(r.X + 10, r.Y + 94, r.Width - 20, s.X, s.Y, e.GetChampionPoints(), e.GetGeneration(), "qol_coop_par_" + label.Replace(" ", ""));
	}

	// ---------- Ascend tab ----------

	private static void DrawAscend(Rectangle area)
	{
		dmBirdEgg e = selected;
		int x = area.X, y = area.Y;
		if (e == null)
		{
			Ui.TextCentered("Pick a bird on the left.", area, Palette.TextDim, UiFont.Medium);
			return;
		}
		int have = PoultryCore.Count(PoultryCore.Passionate);
		ascendN = Math.Max(1, Math.Min(8, ascendN));
		int pluck = e.GetChampionPoints();
		int after = PoultryCore.PluckAfterAscend(pluck, ascendN);

		var head = new Rectangle(x, y, area.Width, 120);
		Ui.Panel(head, Palette.PanelRaised, Palette.Border);
		var portrait = new Rectangle(x + 8, y + 8, 76, 104);
		Ui.Fill(portrait, Palette.BarBack);
		Ui.Sprite(e.GetBirdSprite(), portrait);
		Ui.Text(e.BirdName, x + 96, y + 6, Palette.Text, UiFont.Large);
		Ui.Text("Generation", x + 96, y + 52, Palette.TextDim, UiFont.Medium);
		Ui.Text(e.GetGeneration().ToString(), x + 230, y + 46, Palette.Text, UiFont.Large);
		Ui.Text("->", x + 290, y + 50, Palette.TextDim, UiFont.Medium);
		Ui.Text((e.GetGeneration() + 1).ToString(), x + 330, y + 46, Palette.Good, UiFont.Large);
		Ui.Text("+20% damage. Keeps stats, powers and genes; only Pluck drops.", x + 96, y + 88, Palette.TextDim);
		y = head.Bottom + 12;

		var sec = new Rectangle(x, y, area.Width, 230);
		Section(sec, "Passionate Poultices to spend");
		int bx = sec.X + 10, by = sec.Y + 34;
		for (int n = 1; n <= 8; n++)
		{
			int nn = n;
			Ui.BoxButton("qol_coop_asc" + n, new Rectangle(bx, by, 56, 36), n.ToString(), () => ascendN = nn, n <= have, n == ascendN, UiFont.Medium,
				n <= have ? "Keeps " + (100 - 100.0 * Math.Pow(0.5, n)).ToString("0.#") + "% of Pluck" : "You have " + have);
			bx += 62;
		}
		Ui.Text("You have ^3" + have + "^7", bx + 12, by + 8, Palette.TextDim);

		int barW = sec.Width - 140;
		int max = Math.Max(1, pluck);
		Ui.Text("Pluck now", sec.X + 10, by + 56, Palette.TextDim);
		Ui.Bar(new Rectangle(sec.X + 120, by + 54, barW, 24), 1f, Palette.Accent, pluck.ToString());
		Ui.Text("Pluck after", sec.X + 10, by + 88, Palette.TextDim);
		Ui.Bar(new Rectangle(sec.X + 120, by + 86, barW, 24), after / (float)max, Palette.Good, after + "  (-" + (pluck - after) + ")");
		Ui.Text("Each extra poultice halves the loss. Rare steaks (Pluck +1/+2/+3) refill it.", sec.X + 10, by + 124, Palette.TextMuted);

		Ui.BoxButton("qol_coop_ascend", new Rectangle(area.Right - 380, area.Bottom - 36, 380, 34),
			"Ascend to generation " + (e.GetGeneration() + 1) + "  (" + ascendN + " poultices)", () => Do(() => PoultryCore.Ascend(e, ascendN)),
			have >= ascendN && e.BirdHasEverHatched, font: UiFont.Medium);
	}

	// ---------- Pantry tab ----------

	private static void DrawPantry(Rectangle body)
	{
		int colW = (body.Width - 12) / 2;
		var bag = PoultryCore.BagFoods().GroupBy(f => f.Key).Select(gr => (name: gr.First().Name, item: (dmItem)gr.First().BagItem, n: gr.Count()))
			.Concat(Game.dungeonMans.GetInventoryList().Where(i => PoultryCore.IsPantryItem(i) && !(i is dmEnemyBirdSteak)).GroupBy(i => i.ArchetypeName)
				.Select(gr => (name: gr.First().GetName(), item: gr.First(), n: gr.Count())))
			.ToList();
		const int rowH = 30;

		var left = new Rectangle(body.X, body.Y, colW, body.Height);
		Section(left, "In your bags (" + bag.Sum(b => b.n) + ")");
		Ui.BoxButton("qol_coop_storeall", new Rectangle(left.Right - 110, left.Y + 3, 100, 22), "Store all", () => Do(PoultryCore.DepositAll), bag.Count > 0,
			tooltipText: "Move every steak, Plumage and poultice to the Pantry (locked items stay)");
		bagRect = new Rectangle(left.X, left.Y + 32, left.Width, left.Height - 32);
		int rows = (bagRect.Height - 4) / rowH;
		bagScroll = Math.Max(0, Math.Min(Math.Max(0, bag.Count - rows), bagScroll));
		for (int vi = 0; vi < rows && bagScroll + vi < bag.Count; vi++)
		{
			var b = bag[bagScroll + vi];
			int ry = bagRect.Y + vi * rowH;
			Ui.BoxButton("qol_coop_store" + vi, new Rectangle(left.X + 10, ry, 64, rowH - 4), "Store", () => Do(() => PoultryCore.Deposit(b.item)), !b.item.InventoryLocked,
				tooltipText: b.item.InventoryLocked ? "Locked" : null);
			Ui.Text(Ui.Fit(b.name, left.Width - 170), left.X + 84, ry + 3, Palette.Text);
			if (b.n > 1)
			{
				Ui.TextRight("x" + b.n, left.Right - 18, ry + 3, Palette.TextDim);
			}
			if (b.item.InventoryLocked)
			{
				Ui.Icon("ui_lock", new Rectangle(left.Right - 66, ry + 2, 20, 20));
			}
		}
		if (bag.Count == 0)
		{
			Ui.TextCentered("Nothing to store.", bagRect, Palette.TextMuted);
		}
		Scrollbar(new Rectangle(left.Right - 8, bagRect.Y, 5, rows * rowH - 4), bagScroll, rows, bag.Count);

		var right = new Rectangle(left.Right + 12, body.Y, colW, body.Height - 118);
		var pantry = PoultryCore.Pantry;
		Section(right, "Pantry");
		Ui.Bar(new Rectangle(right.X + 84, right.Y + 5, 170, 18), pantry.Count / (float)Math.Max(1, PoultryCore.PantrySlots),
			pantry.Count >= PoultryCore.PantrySlots ? Palette.Bad : Palette.Info, pantry.Count + " / " + PoultryCore.PantrySlots + " stacks");
		Ui.TextRight("Shift = whole stack", right.Right - 12, right.Y + 5, Palette.TextMuted);
		pantryRect = new Rectangle(right.X, right.Y + 32, right.Width, right.Height - 32);
		int prow = (pantryRect.Height - 4) / rowH;
		pantryScroll = Math.Max(0, Math.Min(Math.Max(0, pantry.Count - prow), pantryScroll));
		for (int vi = 0; vi < prow && pantryScroll + vi < pantry.Count; vi++)
		{
			var kv = pantry[pantryScroll + vi];
			int ry = pantryRect.Y + vi * rowH;
			Ui.BoxButton("qol_coop_take" + vi, new Rectangle(right.X + 10, ry, 56, rowH - 4), "Take",
				() => Do(() => PoultryCore.Withdraw(kv.Key, Game.theInput.bShiftIsDown ? kv.Value : 1)), tooltipText: "Shift-click: the whole stack");
			int tx = right.X + 72;
			if (!kv.Key.StartsWith("i|"))
			{
				PoultryCore.Food f = PoultryCore.FoodFromKey(kv.Key, 1);
				int feathers = f != null ? PoultryCore.RenderFeathers(f) : 0;
				Ui.BoxButton("qol_coop_render" + vi, new Rectangle(tx, ry, 88, rowH - 4), "Render +" + feathers, () => Do(() => PoultryCore.Render(kv.Key)), f != null,
					tooltipText: "Turn one into " + feathers + " Feathers", accent: Palette.Warn);
				tx += 94;
			}
			Ui.Text(Ui.Fit(PoultryCore.ItemName(kv.Key), right.Right - 76 - tx), tx + 4, ry + 3, Palette.Text);
			Ui.TextRight("x" + kv.Value, right.Right - 18, ry + 3, Palette.TextDim);
		}
		if (pantry.Count == 0)
		{
			Ui.TextCentered("Empty: store steaks and poultices from your bags.", pantryRect, Palette.TextMuted);
		}
		Scrollbar(new Rectangle(right.Right - 8, pantryRect.Y, 5, prow * rowH - 4), pantryScroll, prow, pantry.Count);

		var shop = new Rectangle(right.X, right.Bottom + 8, colW, 110);
		Section(shop, "Feathers shop");
		Ui.TextRight("You have ^3" + PoultryCore.Feathers + "^7 Feathers", shop.Right - 12, shop.Y + 5, Palette.TextDim);
		int sw = (shop.Width - 30) / 2;
		Ui.BoxButton("qol_coop_buyslots", new Rectangle(shop.X + 10, shop.Y + 34, sw, 30), "+" + PoultryCore.SlotsPerPurchase + " stacks:  " + PoultryCore.SlotPrice,
			() => Do(PoultryCore.BuySlots), PoultryCore.Feathers >= PoultryCore.SlotPrice, tooltipText: "Each upgrade costs 25 more");
		Ui.BoxButton("qol_coop_buypoult", new Rectangle(shop.X + 20 + sw, shop.Y + 34, sw, 30), "Passionate Poultice:  " + PoultryCore.PoulticeFeatherPrice,
			() => Do(() => PoultryCore.BuyPoultice(PoultryCore.Passionate)), PoultryCore.Feathers >= PoultryCore.PoulticeFeatherPrice);
		Ui.BoxButton("qol_coop_sellpoult", new Rectangle(shop.X + 10, shop.Y + 70, sw, 30), "Sell a Passionate:  +" + PoultryCore.PoulticeSellFeathers,
			() => Do(PoultryCore.SellPoultice), PoultryCore.Count(PoultryCore.Passionate) > 0, accent: Palette.Warn);
	}
}
