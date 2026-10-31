using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Poultry status window, above the hero's vitals (bottom right). Shows the equipped bird: HP when it's out,
// generation, Pluck, stats, powers. [-]/[+] collapses it to one line; [Feed] opens the Coop on that bird.
internal static class PoultryHud
{
	private const string ExpandedKey = "poultry_hud_expanded";
	private static readonly Dictionary<string, Action> actions = new Dictionary<string, Action>();

	private static dmGame Game => dmUtilities.theGame;

	private static dmBirdEgg Equipped(dmGame g) => g.dungeonMans.GetItemForSlot(ESlot.ES_BirdEggPocket) as dmBirdEgg;

	private static bool Visible(dmGame g) =>
		Config.On(Config.PoultryHud) && g.dungeonMans != null && !AcademyDirectory.UiBusy(g) && Equipped(g) != null;

	private static bool StillVisible(ui_mouseFrame f) => Visible(Game);

	private static object Route(ui_mouseFrame f, int b)
	{
		if (actions.TryGetValue(f.tagStringData, out Action a))
		{
			dmAudio.PlayUIFX("ui_click_01");
			a();
		}
		return true;
	}

	private static void Button(wg2DRenderer tr, string id, string label, int x, int y, int w, Action onClick)
	{
		ui_mouseFrame f = Game.theUIFrames.AddFrame(x, y, w, 22, 6, "qol_phud_" + id);
		f.tagStringData = id;
		f.RemoveIfFalse = StillVisible;
		actions[id] = onClick;
		f.funcOnClick = Route;
		tr.DrawString("[" + label + "]", new Vector2(x, y + 1), Game.theInput.IsActiveFrame(f) ? ShepColor.Yellow : ShepColor.Cyan);
	}

	private static string Axis(int v, string neg, string pos) => v < 0 ? neg + " " + (-v) : v > 0 ? pos + " " + v : neg + "/" + pos + " 0";

	public static void Draw(dmGame g)
	{
		if (!Visible(g))
		{
			return;
		}
		wg2DRenderer tr = g.theRenderer;
		actions.Clear();
		dmBirdEgg egg = Equipped(g);
		BirdStatInfo info = egg.GetStatInfo();
		dmPetBird pet = PoultryCore.Placed(egg);
		bool expanded = Config.On(ExpandedKey);

		int wUnits = 9, hUnits = expanded ? 4 : 1;
		int w = wUnits * 32;
		int x = (int)tr.fWindowXSize - w - 8;
		int y = (int)tr.fWindowYSize - 150 - hUnits * 32;
		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		tr.DrawParchmentBox(x, y, wUnits, hUnits);
		tr.SetActiveFont(tr.fontNyala18);

		string hp = pet != null ? "HP ^3" + pet.health + "/" + pet.maxHealth + "^7" : info.bWounded ? "^1Wounded^7" : "not called";
		tr.DrawString(egg.BirdName + "  G" + egg.GetGeneration() + "  " + hp, new Vector2(x + 12, y + 6), Color.White);
		Button(tr, "toggle", expanded ? "-" : "+", x + w - 34, y + 4, 24, () => Config.Toggle(ExpandedKey));
		if (expanded)
		{
			tr.DrawString("Pluck " + egg.GetChampionPoints() + "   " + Axis(info.birdStats[0], "Tough", "Swift") + "   " + Axis(info.birdStats[1], "Ranged", "Melee"),
				new Vector2(x + 12, y + 30), Color.LightGray);
			string powers = info.powersUnlockedArchetypes.Count == 0 ? "No powers yet" : string.Join(", ", info.powersUnlockedArchetypes.Select(PoultryCore.PowerName));
			tr.DrawString(powers.Length > 44 ? powers.Substring(0, 42) + "..." : powers, new Vector2(x + 12, y + 52), Color.LightGray);
			int steaks = PoultryCore.BagFoods().Count;
			if (Config.On(Config.Coop))
			{
				Button(tr, "feed", "Feed (" + steaks + " steaks)", x + 12, y + 80, 150, () => CoopPanel.Open(0, egg));
				Button(tr, "ascend", "Ascend", x + 170, y + 80, 80, () => CoopPanel.Open(2, egg));
			}
		}
		tr.EndSpriteBatch();
	}
}
