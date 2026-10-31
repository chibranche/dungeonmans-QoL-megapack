using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Small window on the overworld (top right):
//  - Academy items you carry (Proofs of Stremf, relics, books...) + "Hand in": auto-walks to the Academy, and once
//    inside, teleports you to the Headmaster and opens his dialog (pick "I've brought some relics").
//  - Random nearby dungeon: Mundane / Adventurous / Dangerous (the game's own labels vs your level): auto-walks there.
// Auto-walking is the game's own auto-path, so normal turns and encounters happen on the way.
internal static class OverworldHud
{
	private static bool pendingHandIn;
	private static string lastPick = "";

	private static dmGame Game => dmUtilities.theGame;

	private static bool Visible(dmGame g) =>
		g.dungeonMans != null && g.dungeonMans.bIsInOverworld && g.theCurrentLevel == g.theWorld && !AcademyDirectory.UiBusy(g)
		&& (Config.On(Config.ProofReminder) || Config.On(Config.RandomDungeon));

	// Called every frame from the input hook. Finishes the hand-in once the hero has arrived in the Academy.
	public static bool HandleInput(dmGame g)
	{
		if (!pendingHandIn || g.dungeonMans == null || !(g.theCurrentLevel is dmAcademy) || AcademyDirectory.UiBusy(g))
		{
			return false;
		}
		pendingHandIn = false;
		NPCAcademyHeadmaster head = g.allActors.OfType<NPCAcademyHeadmaster>().FirstOrDefault();
		if (head != null && AcademyItems().Count > 0)
		{
			AcademyDirectory.Visit(head, "Headmaster");
			return true;
		}
		return false;
	}

	private static List<dmAcademyItem> AcademyItems() =>
		Game.dungeonMans.GetAllItemsOfType<dmAcademyItem>().OfType<dmAcademyItem>().ToList();

	private static int Qty(dmItem i) => Math.Max(1, i.GetBulkCount());

	private static GazetteerDungeonEntry AcademyEntry() =>
		dmGazetteer.locationDictionary.GetAllLocationsOfType<GazetteerDungeonEntry>()
			.FirstOrDefault(d => d.dungeonArchetype != null && d.dungeonArchetype.Contains("academy") && !d.dungeonArchetype.Contains("pitstop"))
		?? dmGazetteer.locationDictionary.GetAllLocationsOfType<GazetteerDungeonEntry>()
			.FirstOrDefault(d => d.dungeonArchetype != null && d.dungeonArchetype.Contains("academy"));

	public static bool HasItems => Game.dungeonMans != null && AcademyItems().Count > 0;

	public static void HandInFromHub() => HandIn();

	private static void HandIn()
	{
		GazetteerDungeonEntry academy = AcademyEntry();
		if (academy == null)
		{
			Game.AddStringToLog("^1Could not find the Academy on the map.^7");
			return;
		}
		pendingHandIn = true;
		Game.dungeonMans.HeroAutoPathToLocation(academy.location, "Heading to the ^3Academy^7 to hand in your finds.");
	}

	// ---- random dungeon ----
	// The game's own labels (threat - hero level, as shown when you enter an area):
	// Trivial < -2 <= Mundane < -1 <= Adventurous < 1 <= Dangerous < 3 <= Heroic / Legendary.
	private enum Difficulty { Mundane, Adventurous, Dangerous }

	private static readonly Color MundaneColor = new Color(110, 200, 110), DangerousColor = new Color(250, 206, 92);

	private static int Threat(GazetteerDungeonEntry d) =>
		d.iThreatLevel >= 1 ? d.iThreatLevel : Game.theWorld.DetermineThreatFromLocation(d.location);

	private static Difficulty? Classify(int threat, int heroLevel)
	{
		int diff = threat - heroLevel;
		return diff < -1 ? Difficulty.Mundane : diff < 1 ? Difficulty.Adventurous : diff < 3 ? Difficulty.Dangerous : null;
	}

	private static List<GazetteerDungeonEntry> Candidates(Difficulty diff)
	{
		dmHero hero = Game.dungeonMans;
		return dmGazetteer.locationDictionary.GetAllLocationsOfType<GazetteerDungeonEntry>()
			.Where(d => d.dungeonArchetype != null && !d.dungeonArchetype.Contains("academy") && !d.dungeonArchetype.Contains("roadblock")
				&& !d.dungeonArchetype.Contains("mountain_fortress")
				&& d.status != LocationStatus.cleared
				&& (d.status == LocationStatus.unvisited_but_seen || d.status == LocationStatus.visited || d.status == LocationStatus.unavenged || d.bVisitedByHero)
				&& Classify(Threat(d), hero.iLevel) == diff)
			.OrderBy(d => d.location.FlatTileDistanceFrom(hero.Location))
			.Take(5)
			.ToList();
	}

	private static void GoRandom(Difficulty diff)
	{
		List<GazetteerDungeonEntry> list = Candidates(diff);
		if (list.Count == 0)
		{
			Game.AddStringToLog("No known unfinished " + diff + " dungeon nearby. Explore to discover more!");
			return;
		}
		GazetteerDungeonEntry pick = list[dmUtilities.RandomInt() % list.Count];
		lastPick = pick.strLocationName + "  (threat " + Threat(pick) + ")";
		Game.dungeonMans.HeroAutoPathToLocation(pick.location, "Off to ^3" + pick.strLocationName + "^7 (threat " + Threat(pick) + ").");
	}

	// ---- drawing ----
	private const int W = 330;

	public static void Draw(dmGame g)
	{
		if (!Visible(g))
		{
			return;
		}
		bool items = Config.On(Config.ProofReminder);
		bool random = Config.On(Config.RandomDungeon);
		int h = 8 + (items ? 78 : 0) + (random ? 92 : 0) + (items && random ? 8 : 0);
		var box = new Rectangle(Ui.ScreenSize.X - W - 112, 6, W, h);

		g.theRenderer.BeginSpriteBatch();
		Ui.Area("qol_ow_bg", box, null, Ui.WindowLevel - 2);
		Ui.Panel(box, new Color(20, 15, 11, 225), Palette.Border);
		int x = box.X + 10, y = box.Y + 8, w = W - 20;
		if (items)
		{
			List<dmAcademyItem> carried = AcademyItems();
			int proofs = carried.Where(i => i.academyUnlockType == AcademyProgressData.AcademyUnlocksEnum.PROOF_OF_STREMF).Sum(Qty);
			int others = carried.Where(i => i.academyUnlockType != AcademyProgressData.AcademyUnlocksEnum.PROOF_OF_STREMF).Sum(Qty);
			Ui.Text("ACADEMY ITEMS", x, y, Palette.Accent);
			int cx = Ui.Chip(x + 130, y - 2, proofs + " Proof" + (proofs == 1 ? "" : "s"), proofs > 0 ? Palette.Accent : Palette.TextMuted);
			Ui.Chip(cx, y - 2, others + " other", others > 0 ? Palette.Text : Palette.TextMuted);
			Ui.BoxButton("qol_ow_handin", new Rectangle(x, y + 28, w, 30), pendingHandIn ? "Walking to the Academy..." : "Go hand them in", HandIn, carried.Count > 0 && !pendingHandIn,
				tooltipText: "Walks to the Academy, then opens the Headmaster: pick \"I've brought some relics\"");
			y += 78;
		}
		if (items && random)
		{
			Ui.Separator(x, y - 6, w);
			y += 8;
		}
		if (random)
		{
			Ui.Text("RANDOM NEARBY DUNGEON", x, y, Palette.Accent);
			int bw = (w - 8) / 3;
			DiffButton(Difficulty.Mundane, new Rectangle(x, y + 26, bw, 30), MundaneColor, "Threat 2+ below your level");
			DiffButton(Difficulty.Adventurous, new Rectangle(x + bw + 4, y + 26, bw, 30), Palette.Text, "Threat around your level");
			DiffButton(Difficulty.Dangerous, new Rectangle(x + 2 * (bw + 4), y + 26, bw, 30), DangerousColor, "Threat 1-2 above your level");
			Ui.Text(lastPick == "" ? "Picks one of the 5 nearest unfinished ones." : Ui.Fit("Last: " + lastPick, w), x, y + 62, Palette.TextDim);
		}
		g.theRenderer.EndSpriteBatch();
	}

	private static void DiffButton(Difficulty d, Rectangle r, Color c, string tip)
	{
		int n = Candidates(d).Count;
		Ui.BoxButton("qol_ow_" + d, r, d.ToString(), () => GoRandom(d), n > 0, tooltipText: tip + (n > 0 ? "\nKnown nearby: " + n : "\nNone known nearby"), accent: c);
	}
}
