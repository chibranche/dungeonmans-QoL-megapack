using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Bird status window: the equipped bird at a glance. Drag the header to move it (position is remembered),
// [-]/[+] folds it. Expanded: HP bar, both stat axes as center bars, combat numbers, power icons, Feed/Ascend/Coop.
internal static class PoultryHud
{
	public const string ExpandedKey = "poultry_hud_expanded";
	private const string PosX = "poultry_hud_x", PosY = "poultry_hud_y";
	private const int W = 336;

	private static dmBirdEgg Equipped(dmGame g) => g.dungeonMans.GetItemForSlot(ESlot.ES_BirdEggPocket) as dmBirdEgg;

	private static bool Visible(dmGame g) =>
		Config.On(Config.PoultryHud) && g.dungeonMans != null && !g.bShowMainMenu && !AcademyDirectory.UiBusy(g) && Equipped(g) != null;

	// Stat colors shared with the Coop.
	public static readonly Color Tough = new Color(226, 144, 62), Swift = new Color(96, 200, 236);
	public static readonly Color Ranged = new Color(176, 128, 236), Melee = new Color(232, 96, 84);

	public static void Draw(dmGame g)
	{
		if (!Visible(g))
		{
			return;
		}
		dmBirdEgg egg = Equipped(g);
		BirdStatInfo info = egg.GetStatInfo();
		dmPetBird pet = PoultryCore.Placed(egg);
		bool expanded = Config.On(ExpandedKey);
		bool coop = Config.On(Config.Coop);
		List<string> powers = info.powersUnlockedArchetypes.Concat(info.powersViaStatsArchetypes).Distinct().ToList();
		int h = expanded ? 214 + (coop ? 34 : 0) : 52;

		Point size = new Point(W, h);
		var defaultPos = new Point(Ui.ScreenSize.X - W - 8, Ui.ScreenSize.Y - 150 - h);
		var pos = new Point(QoLMod.Ctx.Settings.GetInt(PosX, defaultPos.X), QoLMod.Ctx.Settings.GetInt(PosY, defaultPos.Y));

		wg2DRenderer tr = g.theRenderer;
		tr.BeginSpriteBatch();
		pos = Ui.Drag("qol_phud", new Rectangle(pos.X, pos.Y, W - 40, 44), pos, size, p =>
		{
			QoLMod.Ctx.Settings.SetInt(PosX, p.X);
			QoLMod.Ctx.Settings.SetInt(PosY, p.Y);
		});
		var box = new Rectangle(pos.X, pos.Y, W, h);
		Ui.Area("qol_phud_bg", box, null, Ui.WindowLevel - 2);
		Ui.Panel(box, new Color(20, 15, 11, 225), Palette.Border);
		Ui.Fill(new Rectangle(box.X, box.Y, W, 44), new Color(0, 0, 0, 70));
		Ui.Fill(new Rectangle(box.X, box.Y + 44, W, 1), Palette.Border);

		// Header: portrait, name, generation, fold button.
		var portrait = new Rectangle(box.X + 6, box.Y + 4, 36, 36);
		Ui.Fill(portrait, Palette.BarBack);
		Ui.Sprite(egg.GetBirdSprite(), portrait);
		string name = Ui.Fit(egg.BirdName, 150, UiFont.Medium);
		Ui.Text(name, box.X + 50, box.Y + 2, Palette.Text, UiFont.Medium);
		Ui.Text("Gen ^3" + egg.GetGeneration() + "^7   Pluck ^3" + egg.GetChampionPoints(), box.X + 50, box.Y + 24, Palette.TextDim);
		Ui.BoxButton("qol_phud_fold", new Rectangle(box.Right - 34, box.Y + 8, 28, 28), expanded ? "-" : "+",
			() => Config.Toggle(ExpandedKey), tooltipText: expanded ? "Fold" : "Unfold", font: UiFont.Medium);
		if (!expanded)
		{
			// Thin HP strip under the header when folded.
			DrawHp(new Rectangle(box.X + 210, box.Y + 14, box.Right - 40 - (box.X + 210) - 6, 16), pet, info, small: true);
			tr.EndSpriteBatch();
			return;
		}

		int x = box.X + 10, y = box.Y + 54, w = W - 20;
		DrawHp(new Rectangle(x, y, w, 22), pet, info, small: false);
		y += 32;

		int t = info.birdStats[0], m = info.birdStats[1];
		StatRow("qol_phud_tvs", x, y, w, "Tough", "Swift", t, Tough, Swift, ToughSwiftHelp(t, egg.GetChampionPoints()));
		y += 26;
		StatRow("qol_phud_mvr", x, y, w, "Ranged", "Melee", m, Ranged, Melee, RangedMeleeHelp(m, egg.GetGeneration()));
		y += 30;

		// Combat numbers with the game's own icons.
		int cx = x;
		cx = Stat(cx, y, "ui_attack_block", Math.Max(0, t * -8).ToString(), "Block " + Math.Max(0, t * -8) + " (from Tough)");
		cx = Stat(cx, y, "ui_attack_parry", Math.Max(0, t * 6).ToString(), "Parry " + Math.Max(0, t * 6) + " (from Swift)");
		cx = Stat(cx, y, "ui_attack_counter", m < 0 ? (2 - m) + " tiles" : "melee", m < 0 ? "Ranged attacks from " + (2 - m) + " tiles" : "Melee attacks only");
		Stat(cx, y, "condition_atk_up", "+" + (20 * (egg.GetGeneration() - 1)) + "%", "Damage +" + (20 * (egg.GetGeneration() - 1)) + "% (+20% per generation after the first)");
		y += 30;

		// Powers.
		Ui.Separator(x, y - 2, w);
		y += 4;
		if (powers.Count == 0)
		{
			Ui.Text("No powers yet", x, y + 6, Palette.TextMuted);
		}
		int px = x;
		foreach (string p in powers.Take(8))
		{
			var r = new Rectangle(px, y, 32, 32);
			ui_mouseFrame f = Ui.Area("qol_phud_pw_" + p, r, null);
			if (!Ui.PowerIcon(p, r))
			{
				Ui.Panel(r);
				Ui.TextCentered("?", r, Palette.TextDim);
			}
			if (Ui.Hovered(f))
			{
				Ui.Outline(r, Palette.BorderBright);
				info.powerGenesCollected.TryGetValue(p, out int genes);
				Ui.Tooltip("^3" + PoultryCore.PowerName(p) + "^7" + (info.powersViaStatsArchetypes.Contains(p) ? "\nFrom its stats" : "\n" + genes + " genes"));
			}
			px += 36;
		}
		y += 40;

		if (coop)
		{
			int steaks = PoultryCore.BagFoods().Count(f => f.Useful);
			int bw = (w - 8) / 3;
			Ui.BoxButton("qol_phud_feed", new Rectangle(x, y, bw, 26), "Feed (" + steaks + ")", () => CoopPanel.Open(0, egg), tooltipText: "Rare steaks and Plumage in your bags");
			Ui.BoxButton("qol_phud_ascend", new Rectangle(x + bw + 4, y, bw, 26), "Ascend", () => CoopPanel.Open(2, egg), tooltipText: "+1 generation for Passionate Poultices");
			Ui.BoxButton("qol_phud_coop", new Rectangle(x + 2 * (bw + 4), y, bw, 26), "Coop", () => CoopPanel.Open(0, egg), tooltipText: "All your birds");
		}
		tr.EndSpriteBatch();
	}

	private static void DrawHp(Rectangle r, dmPetBird pet, BirdStatInfo info, bool small)
	{
		ui_mouseFrame f = Ui.Area("qol_phud_hp", r, null);
		string tip;
		if (pet != null && pet.maxHealth > 0)
		{
			float frac = pet.health / (float)pet.maxHealth;
			Ui.Bar(r, frac, Palette.Health(frac), small ? null : "HP  " + pet.health + " / " + pet.maxHealth);
			tip = "HP " + pet.health + " / " + pet.maxHealth;
		}
		else if (info.bWounded)
		{
			Ui.Bar(r, 1f, new Color(110, 34, 28), small ? null : "Wounded");
			tip = "Wounded: heal it at Professor Hatch (or with a poultice).";
		}
		else
		{
			Ui.Bar(r, 0f, Palette.TextMuted, small ? null : "In its egg");
			tip = "Blow the bird whistle to call it.";
		}
		if (Ui.Hovered(f))
		{
			Ui.Tooltip(tip);
		}
	}

	private static void StatRow(string id, int x, int y, int w, string neg, string pos, int value, Color negC, Color posC, string help)
	{
		ui_mouseFrame f = Ui.Area(id, new Rectangle(x, y, w, 22), null);
		bool hot = Ui.Hovered(f);
		Ui.Text(neg, x, y + 1, value < 0 ? negC : Palette.TextMuted);
		Ui.TextRight(pos, x + w, y + 1, value > 0 ? posC : Palette.TextMuted);
		Ui.CenterBar(new Rectangle(x + 64, y + 3, w - 128, 16), value, 5, negC, posC);
		if (hot)
		{
			Ui.Tooltip(help);
		}
	}

	private static int Stat(int x, int y, string icon, string value, string tip)
	{
		int w = 22 + (int)Ui.Measure(value).X + 12;
		ui_mouseFrame f = Ui.Area("qol_phud_st_" + icon, new Rectangle(x, y, w, 22), null);
		Ui.Icon(icon, new Rectangle(x, y + 1, 20, 20));
		Ui.Text(value, x + 22, y + 1, Palette.Text);
		if (Ui.Hovered(f))
		{
			Ui.Tooltip(tip);
		}
		return x + w;
	}

	public static string ToughSwiftHelp(int t, int pluck)
	{
		int hp = (t < 0 ? -10 * t : -4 * t) + pluck;
		return (t < 0 ? "^3Tough " + -t + "^7" : t > 0 ? "^3Swift " + t + "^7" : "^3Balanced^7")
			+ "\nMax HP " + (hp >= 0 ? "+" : "") + hp + "%  (Tough +10%/pt, Swift -4%/pt, Pluck +1%/pt)"
			+ "\nBlock " + Math.Max(0, -8 * t) + "   Parry " + Math.Max(0, 6 * t)
			+ "\nAt 3+ on both axes the bird gains stat powers.";
	}

	public static string RangedMeleeHelp(int m, int gen) =>
		(m < 0 ? "^3Ranged " + -m + "^7\nShoots from " + (2 - m) + " tiles" : m > 0 ? "^3Melee " + m + "^7\nFights up close" : "^3Balanced^7\nFights up close")
		+ "\nDamage +" + (20 * (gen - 1)) + "% from generation"
		+ (m >= 3 ? "\nPress the Attack (Melee 3+)" : "");
}
