using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Small window on the overworld (top right):
//  - Academy items you carry (Proofs of Stremf, relics, books...) + "Hand in": auto-walks to the Academy, and once
//    inside, teleports you to the Headmaster and opens his dialog (pick "I've brought some relics").
//  - Random nearby dungeon of a chosen difficulty (Easy / Even / Hard vs your level): auto-walks there.
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
	private enum Difficulty { Easy, Even, Hard }

	private static int Threat(GazetteerDungeonEntry d) =>
		d.iThreatLevel >= 1 ? d.iThreatLevel : Game.theWorld.DetermineThreatFromLocation(d.location);

	private static Difficulty Classify(int threat, int heroLevel) =>
		threat < heroLevel - 1 ? Difficulty.Easy : threat > heroLevel + 1 ? Difficulty.Hard : Difficulty.Even;

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
			Game.AddStringToLog("No known unfinished " + diff.ToString().ToLower() + " dungeon nearby. Explore to discover more!");
			return;
		}
		GazetteerDungeonEntry pick = list[dmUtilities.RandomInt() % list.Count];
		lastPick = pick.strLocationName + " (threat " + Threat(pick) + ")";
		Game.dungeonMans.HeroAutoPathToLocation(pick.location, "Off to ^3" + pick.strLocationName + "^7 (threat " + Threat(pick) + ").");
	}

	// ---- drawing ----
	private static readonly Dictionary<string, Action> actions = new Dictionary<string, Action>();

	private static object Route(ui_mouseFrame f, int b)
	{
		if (actions.TryGetValue(f.tagStringData, out Action a))
		{
			dmAudio.PlayUIFX("ui_click_01");
			a();
		}
		return true;
	}

	private static bool StillVisible(ui_mouseFrame f) => Visible(Game);

	private static void Button(wg2DRenderer tr, string id, string label, int x, int y, int w, Action onClick, bool enabled = true)
	{
		ui_mouseFrame f = Game.theUIFrames.AddFrame(x, y, w, 24, 6, "qol_ow_" + id);
		f.tagStringData = id;
		f.RemoveIfFalse = StillVisible;
		if (enabled)
		{
			actions[id] = onClick;
			f.funcOnClick = Route;
		}
		Color c = !enabled ? Color.DarkGray : Game.theInput.IsActiveFrame(f) ? ShepColor.Yellow : ShepColor.Cyan;
		tr.DrawString("[" + label + "]", new Vector2(x, y + 2), c);
	}

	public static void Draw(dmGame g)
	{
		if (!Visible(g))
		{
			return;
		}
		wg2DRenderer tr = g.theRenderer;
		actions.Clear();
		bool items = Config.On(Config.ProofReminder);
		bool random = Config.On(Config.RandomDungeon);
		int w = 12 * 32, units = (items ? 2 : 0) + (random ? 3 : 0);
		int x = (int)tr.fWindowXSize - w - 112, y = 4;
		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		tr.DrawParchmentBox(x, y, 12, units);
		tr.SetActiveFont(tr.fontNyala18);
		int ly = y + 8;
		if (items)
		{
			List<dmAcademyItem> carried = AcademyItems();
			int proofs = carried.Where(i => i.academyUnlockType == AcademyProgressData.AcademyUnlocksEnum.PROOF_OF_STREMF).Sum(Qty);
			int others = carried.Where(i => i.academyUnlockType != AcademyProgressData.AcademyUnlocksEnum.PROOF_OF_STREMF).Sum(Qty);
			string text = carried.Count == 0 ? "No Academy items to hand in."
				: "To hand in: ^3" + proofs + "^7 Proof" + (proofs == 1 ? "" : "s") + " of Stremf" + (others > 0 ? ", ^3" + others + "^7 other item" + (others == 1 ? "" : "s") : "");
			tr.DrawString(text, new Vector2(x + 16, ly), Color.White);
			Button(tr, "handin", pendingHandIn ? "Walking to the Academy..." : "Go hand them in", x + 16, ly + 26, 260, HandIn, carried.Count > 0);
			ly += 64;
		}
		if (random)
		{
			tr.DrawString("Random nearby dungeon:", new Vector2(x + 16, ly), Color.White);
			Button(tr, "easy", "Easy", x + 16, ly + 26, 70, () => GoRandom(Difficulty.Easy), Candidates(Difficulty.Easy).Count > 0);
			Button(tr, "even", "Even", x + 96, ly + 26, 70, () => GoRandom(Difficulty.Even), Candidates(Difficulty.Even).Count > 0);
			Button(tr, "hard", "Hard", x + 176, ly + 26, 70, () => GoRandom(Difficulty.Hard), Candidates(Difficulty.Hard).Count > 0);
			if (lastPick != "")
			{
				tr.DrawString(lastPick, new Vector2(x + 16, ly + 50), Color.LightGray);
			}
		}
		tr.EndSpriteBatch();
	}
}
