using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// "Equip best armor": for head, shoulders, chest, pants, boots and gloves, wears the best piece from your bags.
// Body pieces (shoulders, chest, pants, boots) must be of the chosen weight: Light 'Armor', Medium Armor,
// Real Armor, or Any. Runs only when asked: Insert key, the QoL hub, or the button shown with the inventory.
// Skips unidentified magic items, cursed items, items you can't use yet, and slots the game won't empty (cursed
// piece, full bags with the Pay 2 Lose bag cap). Does nothing under Pay 2 Lose "all gear stuck": gear put on stays.
internal static class AutoEquip
{
	private const Keys HotKey = Keys.Insert;
	private const string WeightKey = "autoequip_weight"; // 0 light, 1 medium, 2 real, 3 any
	private static bool keyWasDown;

	private static readonly ESlot[] Slots = { ESlot.ES_Head, ESlot.ES_Shoulders, ESlot.ES_Chest, ESlot.ES_Pants, ESlot.ES_Boots, ESlot.ES_Glove };
	public static readonly string[] WeightNames = { "Light", "Medium", "Real", "Any" };
	private static readonly ulong[] WeightFlags = { 0x100, 0x200, 0x400, 0 };
	private const ulong AllWeights = 0x100 | 0x200 | 0x400;

	public static int Weight
	{
		get => Math.Clamp(QoLMod.Ctx.Settings.GetInt(WeightKey, 3), 0, 3);
		set => QoLMod.Ctx.Settings.SetInt(WeightKey, value);
	}

	public static bool Available(dmGame g) => Config.On(Config.AutoEquip) && g.dungeonMans != null && !g.bShowMainMenu;

	// From the QoL input handler. Returns true when the input was consumed.
	public static bool HandleInput(dmGame g)
	{
		bool down = g.theInput.PressedKeys.Contains(HotKey);
		bool pressed = down && !keyWasDown;
		keyWasDown = down;
		if (!pressed || !Available(g) || (Ui.IsBusy(g) && !g.bInventoryMode))
		{
			return false;
		}
		Run();
		return true;
	}

	public static void Run()
	{
		dmHero hero = dmUtilities.theGame.dungeonMans;
		if (dmPay2Lose.GetModifier(EPay2LoseModifiers.bs_all_gear_stuck) > 0)
		{
			Ui.Toast("Pay 2 Lose: gear can't come off, so nothing is swapped automatically", Palette.Warn);
			return;
		}
		var changes = new List<string>();
		foreach (ESlot slot in Slots)
		{
			dmEquippable current = hero.GetItemForSlot(slot) as dmEquippable;
			if (current != null && hero.CanRemoveItemFromSlot(slot) != RemoveItemResult.CanRemove)
			{
				continue;
			}
			double currentScore = current == null || !WeightOk(current) ? double.MinValue : Score(current);
			dmEquippable best = hero.GetInventoryList().OfType<dmEquippable>()
				.Where(e => !e.bEquipped && Usable(hero, e, slot))
				.OrderByDescending(Score).FirstOrDefault();
			if (best != null && Score(best) > currentScore && hero.EquipItemInSlot(best, slot))
			{
				changes.Add(best.GetName());
			}
		}
		if (changes.Count == 0)
		{
			Ui.Toast("Already wearing your best " + (Weight == 3 ? "" : WeightNames[Weight] + " ") + "armor");
			return;
		}
		// The game's own equip path does this too: block, parry and dodge are cached until marked dirty.
		hero.DirtyBattleCache();
		hero.RequestBattleCacheRecalcIfNecessary();
		dmAudio.PlayUIFX("pickup_armor");
		Ui.Toast("Equipped " + changes.Count + " piece" + (changes.Count == 1 ? "" : "s"), Palette.Good);
		dmUtilities.theGame.AddStringToLog("^3Equip best:^7 " + string.Join(", ", changes));
	}

	private static bool Usable(dmHero hero, dmEquippable e, ESlot slot) =>
		hero.IsSlotValidForItem(e, slot) && hero.CanEquipItem(e) && (e.bIsIdentified || !e.bIsMagic) && !e.IsCursed() && WeightOk(e);

	// Body pieces have a weight; helms and gloves don't and always pass.
	private static bool WeightOk(dmEquippable e)
	{
		ulong w = e.itemInfoFlags & AllWeights;
		return w == 0 || Weight == 3 || w == WeightFlags[Weight];
	}

	// One number per item: defense values plus a weighted sum of its enchantments.
	public static double Score(dmEquippable e)
	{
		double s = e.iArmorValue + e.iDodgeValue + 2 * e.iMagicPlusToItem + e.iBlockValue + e.iParryValue + e.iHitValue
			+ 0.5 * e.iBonusUnleashedHealth;
		if (e is dmArmor a)
		{
			s += 0.5 * a.iRangedAttackArmorValue + 0.5 * a.iReduceCriticalHitChance;
		}
		foreach (dmMagicModifier m in e.magicModifiers ?? new List<dmMagicModifier>())
		{
			if (m == null)
			{
				continue;
			}
			if (m.statModifiers != null)
			{
				s += 3 * m.statModifiers.Sum();
			}
			s += 0.5 * m.iAddBaseHealth + m.iAddBaseArmor + m.iAddBaseDodge + m.iAddBaseBlock + m.iAddBaseParry
				+ 2 * m.iAddBaseCrit + m.iAddBaseSpellpower
				+ 50 * (m.fChangeArmor + m.fChangeDodge + m.fChangeBlock + m.fChangeParry + m.fChangeCrit + m.fBaseHealthMod)
				+ 100 * (m.fUniversalDamageModifier - m.fUniversalDamageTakenModifier)
				+ 3 * ((m.ElementalResistRatings?.Count ?? 0) + (m.DamageTakenFlatResists?.Count ?? 0) + (m.DamageDealtModifiers?.Count ?? 0))
				+ 5 * (m.strGrantedPowers?.Count ?? 0) + 0.2 * m.iMagicFind;
		}
		return s;
	}

	// [Light] [Medium] [Real] [Any] row, used by the QoL hub and the inventory button strip.
	public static void DrawWeightPicker(string id, int x, int y)
	{
		const string label = "Preferred armor weight";
		int lw = (int)Ui.Measure(label).X;
		ui_mouseFrame f = Ui.Area(id + "_label", new Rectangle(x, y, lw, 32), null, Ui.WindowLevel + 2);
		Ui.Text(label, x, y + 6, Palette.TextDim);
		if (Ui.Hovered(f))
		{
			Ui.Tooltip("Used for auto-equip");
		}
		x += lw + 16;
		for (int i = 0; i < WeightNames.Length; i++)
		{
			int w = i;
			Ui.BoxButton(id + i, new Rectangle(x + i * 100, y, 96, 32), WeightNames[i], () => Weight = w, selected: Weight == i,
				tooltipText: i == 3 ? "Best piece of any weight" : "Only " + (i == 0 ? "Light 'Armor'" : i == 1 ? "Medium Armor" : "Real Armor") + " for shoulders, chest, pants and boots",
				stackLevel: Ui.WindowLevel + 2);
		}
	}

	private static void NextWeight()
	{
		Weight = (Weight + 1) % WeightNames.Length;
		Ui.Toast("Armor weight: " + WeightNames[Weight], Palette.Good);
	}

	// Where the game last drew the inventory bag (wg2DRenderer.ook_ui_DrawInventory), so the button sits right under it
	// instead of over the character panel.
	private static Rectangle bag;
	private static int bagFrame = -10;

	public static void Register(Harmony h)
	{
		h.Patch(Mod.Target(typeof(wg2DRenderer), nameof(wg2DRenderer.ook_ui_DrawInventory)),
			postfix: Mod.Hook(typeof(AutoEquip), nameof(InventoryDrawn)));
	}

	private static void InventoryDrawn(wg2DRenderer __instance, int xScreenPos, int yScreenPos)
	{
		try
		{
			// Same size formula as the game: walls plus corners.
			Traverse page = Traverse.Create(__instance).Field("ookInventoryPage");
			Rectangle Part(string name) => page.Field(name).GetValue<Rectangle>();
			int w = Part("topWall").Width + Part("topLeftCorner").Width + Part("topRightCorner").Width;
			int h = Part("leftWall").Height + Part("topLeftCorner").Height + Part("bottomLeftCorner").Height;
			bag = new Rectangle(xScreenPos, yScreenPos, w, h);
			bagFrame = Ui.Frame;
		}
		catch
		{
			bagFrame = -10;
		}
	}

	// A button under the inventory bag.
	public static void DrawInventoryButton(dmGame g)
	{
		if (!Available(g) || !g.bInventoryMode)
		{
			return;
		}
		g.theRenderer.BeginSpriteBatch();
		// Under the bag when it was just drawn; otherwise the old spot at the top.
		var r = Ui.Frame - bagFrame <= 2
			? new Rectangle(bag.X + 16, Math.Min(bag.Bottom + 8, Ui.ScreenSize.Y - 40), 288, 32)
			: new Rectangle(Ui.ScreenSize.X - 112 - 288, 8, 288, 32);
		Ui.BoxButton("qol_autoequip_inv", r, "Equip best armor (" + WeightNames[Weight] + ")", Run, font: UiFont.Medium,
			tooltipText: "Left click (or Insert): equip.\nRight click: next weight (Light > Medium > Real > Any).", stackLevel: Ui.WindowLevel + 10,
			onRightClick: NextWeight);
		g.theRenderer.EndSpriteBatch();
	}
}
