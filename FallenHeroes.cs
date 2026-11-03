using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Fallen heroes: when a hero dies in a dungeon, the next visit to that floor spawns a memorial on a random free tile.
// Touching it gives 25% of the XP their level took, +N to all stats, every Academy item they carried and, from level 5,
// an enchanted copy of their weapon (or armor). Everything else they wore is gone. The game only marks the dungeon
// "Unavenged" on the map. This adds:
//  - a window (QoL hub, Academy Directory) listing every memorial not collected yet: hero, dungeon and floor, killer,
//    what is waiting there, and "Go there" (walks to the dungeon, from the overworld);
//  - in the dungeon: a gold frame on the memorial once its tile is explored, and a message when you reach the floor.
internal static class FallenHeroes
{
	public static bool IsOpen;
	private static int page;
	private static wgWorldData announcedLevel;
	private static readonly Color MemorialColor = new Color(250, 206, 92);
	private static readonly Dictionary<string, string> itemNames = new Dictionary<string, string>();

	public sealed class Fallen
	{
		public RecordOfPlayerCharacter Rec;
		public GazetteerDungeonEntry Dungeon;
		public int Floor => Rec.iDeathLocation_Floor;
		public int Xp => (int)(dmGazetteer.RequiredExperienceToLevel[Math.Min(9, Math.Max(0, Rec.iFinalLevel))] * 0.25f);
		public int StatBonus => Rec.iFinalLevel / 6 + 1;

		public string Gear
		{
			get
			{
				if (Rec.iFinalLevel < 5)
				{
					return null;
				}
				string arc = !string.IsNullOrEmpty(Rec.strWeaponArchOnDeath) ? Rec.strWeaponArchOnDeath : Rec.strArmorArcOnDeath;
				return string.IsNullOrEmpty(arc) ? null : Rec.strName + "'s " + ItemName(arc) + " +" + Rec.iFinalLevel / 2;
			}
		}
	}

	// Recomputed a few times a second: the window, the hub tile and the Directory status all ask every frame.
	private static readonly Throttled<List<Fallen>> cache = new Throttled<List<Fallen>>(Compute);

	public static List<Fallen> All() => cache.Value;

	// The game's own rule (dmGazetteer.GetUncollectedDungeonmansWhoDiedHere), for every dungeon.
	private static List<Fallen> Compute()
	{
		var list = new List<Fallen>();
		if (dmFiniteDungeon.bInfiniteDungeonMode)
		{
			return list;
		}
		foreach (RecordOfPlayerCharacter r in dmGazetteer.GetDeadAndRetiredHeroCharacters() ?? new List<RecordOfPlayerCharacter>())
		{
			if ((r.careerStatus != HeroCareerStatus.dead_and_buried && r.careerStatus != HeroCareerStatus.dead_and_ghost_teacher)
				|| r.bHasBeenCollectedByFutureGraduate || r.iDeathLocation_GDE_ID == 999999)
			{
				continue;
			}
			GazetteerDungeonEntry d = dmGazetteer.GetGDEForDungeon(r.iDeathLocation_GDE_ID);
			if (d != null)
			{
				list.Add(new Fallen { Rec = r, Dungeon = d });
			}
		}
		return list.OrderBy(f => f.Dungeon.strLocationName).ThenBy(f => f.Floor).ToList();
	}

	private static string ItemName(string arc)
	{
		if (!itemNames.TryGetValue(arc, out string name))
		{
			try
			{
				name = dmUtilities.theGame.GetItemNameFromArchetype(arc);
			}
			catch
			{
				name = null;
			}
			itemNames[arc] = name = string.IsNullOrEmpty(name) ? arc : Ui.StripCodes(name);
		}
		return name;
	}

	private static string AcademyItems(Fallen f) =>
		f.Rec.strAcademyItemArcs == null || f.Rec.strAcademyItemArcs.Count == 0 ? null
			: string.Join(", ", f.Rec.strAcademyItemArcs.GroupBy(a => a).Select(gr => ItemName(gr.Key) + (gr.Count() > 1 ? " x" + gr.Count() : "")));

	public static string Blocked() => !Config.On(Config.FallenHeroes) ? "Off in Mod Settings" : All().Count == 0 ? "No memorial waiting" : null;

	public static void Open()
	{
		IsOpen = true;
		page = 0;
		dmAudio.PlayUIFX("open_dialog");
	}

	private static void Close()
	{
		IsOpen = false;
		dmAudio.PlayUIFX("ui_back_02");
	}

	// From the QoL input handler. Returns true when the input was consumed.
	public static bool HandleInput(dmGame g)
	{
		if (!IsOpen)
		{
			return false;
		}
		if (g.theInput.ExecutedActions.Contains(ActionButton.Close))
		{
			Close();
		}
		g.theInput.ExecutedActions.Clear();
		return true;
	}

	private static void GoThere(Fallen f)
	{
		IsOpen = false;
		dmUtilities.theGame.dungeonMans.HeroAutoPathToLocation(f.Dungeon.location,
			"Heading to ^3" + f.Dungeon.strLocationName + "^7, floor " + f.Floor + ": the memorial of ^5" + f.Rec.strName + "^7.");
	}

	public static void Draw(dmGame g)
	{
		DrawMemorials(g);
		if (!IsOpen)
		{
			return;
		}
		if (!Config.On(Config.FallenHeroes))
		{
			IsOpen = false;
			return;
		}
		List<Fallen> all = All();
		g.theRenderer.BeginSpriteBatch();
		Rectangle c = Ui.ModalWindow("qol_fallen", 30, 18, "Fallen heroes", Close, all.Count + " memorial" + (all.Count == 1 ? "" : "s") + " to collect");
		const int rowH = 92;
		int perPage = Math.Max(1, (c.Height - 44) / rowH);
		int pages = Math.Max(1, (all.Count + perPage - 1) / perPage);
		page = Math.Clamp(page, 0, pages - 1);
		if (all.Count == 0)
		{
			Ui.Text("No memorial waiting. When a hero dies in a dungeon, their memorial shows up here.", c.X + 8, c.Y + 8, Palette.TextDim);
		}
		bool overworld = Dm.OnOverworld;
		if (!overworld && all.Count > 0)
		{
			Ui.Text("\"Go there\" works from the overworld map: leave the Academy or dungeon first.", c.X + (pages > 1 ? 330 : 8), c.Bottom - 28, Palette.TextDim);
		}
		for (int i = 0; i < perPage && page * perPage + i < all.Count; i++)
		{
			Fallen f = all[page * perPage + i];
			var r = new Rectangle(c.X, c.Y + i * rowH, c.Width, rowH - 8);
			Ui.Box(r, false);
			if (!string.IsNullOrEmpty(f.Rec.strHeroSprite))
			{
				Ui.Icon(f.Rec.strHeroSprite, new Rectangle(r.X + 8, r.Y + 10, 56, 64));
			}
			int tx = r.X + 76, tw = r.Width - 76 - 160;
			Ui.Text("^5" + f.Rec.strName + "^7, level " + f.Rec.iFinalLevel + (string.IsNullOrEmpty(f.Rec.strClassName) ? "" : " " + f.Rec.strClassName),
				tx, r.Y + 6, Palette.Text, UiFont.Medium);
			string where = "^3" + f.Dungeon.strLocationName + "^7, floor " + f.Floor
				+ (string.IsNullOrEmpty(f.Rec.strEnemyRival) ? "" : "   Killed by ^1" + f.Rec.strEnemyRival + "^7");
			Ui.Text(Ui.Fit(where, tw), tx, r.Y + 34, Palette.Text);
			string gear = f.Gear, items = AcademyItems(f);
			string reward = "+" + f.Xp + " XP, +" + f.StatBonus + " all stats" + (gear != null ? ", ^2" + gear + "^7" : "") + (items != null ? ", " + items : "");
			ui_mouseFrame line = Ui.Area("qol_fallen_reward" + i, new Rectangle(tx, r.Y + 56, tw, 22), null, Ui.WindowLevel + 1);
			Ui.Text(Ui.Fit(reward, tw), tx, r.Y + 58, Palette.TextDim);
			if (Ui.Hovered(line))
			{
				Ui.Tooltip("^3Waiting at the memorial^7\n+" + f.Xp + " XP\n+" + f.StatBonus + " to every stat"
					+ (gear != null ? "\n^2" + gear + "^7 (identified, enchanted)" : "\nNo weapon or armor (the hero was below level 5)")
					+ (items != null ? "\nAcademy items:" + string.Concat(items.Split(", ").Select(i => "\n  " + i)) : "\nNo Academy items")
					+ "\n\nThe rest of the hero's gear was lost with them.");
			}
			Ui.BoxButton("qol_fallen_go" + i, new Rectangle(r.Right - 148, r.Y + (r.Height - 32) / 2, 136, 32), "Go there", () => GoThere(f), overworld,
				tooltipText: overworld ? "Walk to " + f.Dungeon.strLocationName + ", then take the stairs to floor " + f.Floor : "From the overworld only",
				stackLevel: Ui.WindowLevel + 2);
		}
		if (pages > 1)
		{
			int by = c.Bottom - 34;
			Ui.BoxButton("qol_fallen_prev", new Rectangle(c.X, by, 96, 32), "< Prev", () => page--, page > 0, stackLevel: Ui.WindowLevel + 2);
			Ui.BoxButton("qol_fallen_next", new Rectangle(c.X + 104, by, 96, 32), "Next >", () => page++, page < pages - 1, stackLevel: Ui.WindowLevel + 2);
			Ui.Text("Page " + (page + 1) + " / " + pages, c.X + 216, by + 6, Palette.TextDim);
		}
		g.theRenderer.EndSpriteBatch();
	}

	// Gold frame on memorials of the current floor, and a message when you arrive on a floor that has one.
	private static void DrawMemorials(dmGame g)
	{
		wgWorldData level = g.theCurrentLevel;
		if (!Config.On(Config.FallenHeroes) || level == null || g.dungeonMans.bIsInOverworld)
		{
			return;
		}
		List<dmDeadDungeonmansMemorial> here = LevelActors.Of<dmDeadDungeonmansMemorial>(g);
		if (level != announcedLevel && here.Count > 0)
		{
			announcedLevel = level;
			string names = string.Join(", ", here.Select(m => "^5" + m.strNameOfHero + "^7"));
			g.AddStringToLog("^3Fallen heroes:^7 the memorial of " + names + " is on this floor.");
			Ui.Toast("A fallen hero's memorial is on this floor", Palette.Good);
		}
		if (here.Count > 0 && !Ui.IsBusy(g))
		{
			TileMarks.Draw(g, here.Select(m => (m.Location, MemorialColor)));
		}
	}
}
