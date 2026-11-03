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

	// A hand-in walk only finishes at the Academy: a new hero, or entering anywhere else (an encounter, a dungeon),
	// cancels it so the button comes back and nobody is teleported to the Headmaster later.
	public static void Register()
	{
		Events.HeroChanged += (g, hero) => pendingHandIn = false;
		Events.LevelEntered += (g, level) =>
		{
			if (!(level is dmAcademy) && level != g.theWorld)
			{
				pendingHandIn = false;
			}
		};
	}

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

	internal static GazetteerDungeonEntry AcademyEntry() =>
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
		Game.dungeonMans.HeroAutoPathToLocation(academy.location, HasItems ? "Heading to the ^3Academy^7 to hand in your finds." : "Heading back to the ^3Academy^7.");
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
	private const int W = 320;
	private const string PosX = "overworld_window_x", PosY = "overworld_window_y";

	public static void Draw(dmGame g)
	{
		if (!Visible(g))
		{
			return;
		}
		bool items = Config.On(Config.ProofReminder);
		bool random = Config.On(Config.RandomDungeon);
		int h = (items ? 96 : 0) + (random ? (lastPick == "" ? 96 : 128) : 0);

		// Draggable anywhere on its background (buttons still click); the position is saved in the QoL config.
		var size = new Point(W, h);
		var saved = new Point(QoLMod.Ctx.Settings.GetInt(PosX, Ui.ScreenSize.X - W - 112), QoLMod.Ctx.Settings.GetInt(PosY, 48));
		g.theRenderer.BeginSpriteBatch();
		Point pos = Ui.Drag("qol_ow", new Rectangle(Ui.ClampToScreen(saved, size), size), Ui.ClampToScreen(saved, size), size, p =>
		{
			QoLMod.Ctx.Settings.SetInt(PosX, p.X);
			QoLMod.Ctx.Settings.SetInt(PosY, p.Y);
		});
		var box = new Rectangle(pos.X, pos.Y, W, h);
		Ui.Box(box);
		int x = box.X + 14, y = box.Y + 10, w = W - 28;
		if (items)
		{
			List<dmAcademyItem> carried = AcademyItems();
			int proofs = carried.Where(i => i.academyUnlockType == AcademyProgressData.AcademyUnlocksEnum.PROOF_OF_STREMF).Sum(Qty);
			int others = carried.Where(i => i.academyUnlockType != AcademyProgressData.AcademyUnlocksEnum.PROOF_OF_STREMF).Sum(Qty);
			Ui.Text("Academy items", x, y, Palette.Accent, UiFont.Medium);
			Ui.TextRight("^3" + proofs + "^7 Proof" + (proofs == 1 ? "" : "s") + "   ^3" + others + "^7 other", x + w, y + 4, Palette.TextDim);
			// With nothing to hand in it still walks you home.
			Ui.BoxButton("qol_ow_handin", new Rectangle(x, y + 36, w, 32), pendingHandIn ? "Walking to the Academy..." : carried.Count > 0 ? "Go hand them in" : "Back to Academy", HandIn, !pendingHandIn,
				tooltipText: carried.Count > 0 ? "Walks to the Academy, then opens the Headmaster: pick \"I've brought some relics\"" : "Walks back to the Academy");
			y += 86;
		}
		if (items && random)
		{
			Ui.Separator(x, y - 6, w);
		}
		if (random)
		{
			Ui.Text("Random nearby dungeon", x, y, Palette.Accent, UiFont.Medium);
			DiffButton(Difficulty.Mundane, new Rectangle(x, y + 36, 96, 32), MundaneColor, "Threat 2+ below your level");
			DiffButton(Difficulty.Adventurous, new Rectangle(x + 100, y + 36, 96, 32), Palette.Text, "Threat around your level");
			DiffButton(Difficulty.Dangerous, new Rectangle(x + 200, y + 36, 96, 32), DangerousColor, "Threat 1-2 above your level");
			if (lastPick != "")
			{
				Ui.Text(Ui.Fit("Last: " + lastPick, w), x, y + 76, Palette.TextDim);
			}
		}
		g.theRenderer.EndSpriteBatch();
	}

	private static readonly Dictionary<Difficulty, Throttled<int>> counts = new Dictionary<Difficulty, Throttled<int>>();

	private static void DiffButton(Difficulty d, Rectangle r, Color c, string tip)
	{
		if (!counts.TryGetValue(d, out Throttled<int> count))
		{
			counts[d] = count = new Throttled<int>(() => Candidates(d).Count, 30);
		}
		int n = count.Value;
		Ui.BoxButton("qol_ow_" + d, r, d.ToString(), () => GoRandom(d), n > 0, tooltipText: tip + (n > 0 ? "\nKnown nearby: " + n : "\nNone known nearby"), accent: c);
	}
}
