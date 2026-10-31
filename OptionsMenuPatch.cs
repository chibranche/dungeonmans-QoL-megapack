using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DungeonMans;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Options menu (Esc): "Save Changes & Close" and "Save Game and Quit" sit right under "Edit Hotbars", easy to
// misclick. Move them to the bottom of the right column, with at least one empty row above them.
// Done with a transpiler that only rewrites the position passed to those two DrawOption_Bool calls (mouse
// hit-boxes follow automatically), plus the Y of the keyboard selection sword.
internal static class OptionsMenuPatch
{
	private const float FirstRowY = 80f; // vector.Y (32) + 48, as in the vanilla method
	private const float RowH = 40f;
	private const int LeftColumnOptions = 8; // vanilla: optionsList entries before the column break

	private static float saveY = -1, quitY = -1;

	public static void Register(Harmony h)
	{
		h.Patch(Mod.Target(typeof(wg2DRenderer), nameof(wg2DRenderer.Dialog_Options_2021)),
			transpiler: Mod.Hook(typeof(OptionsMenuPatch), nameof(Transpiler)));
	}

	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		List<CodeInstruction> code = instructions.ToList();
		MethodInfo remap = AccessTools.Method(typeof(OptionsMenuPatch), nameof(Remap));
		MethodInfo fixSword = AccessTools.Method(typeof(OptionsMenuPatch), nameof(FixSwordY));
		FieldInfo vecY = AccessTools.Field(typeof(Vector2), nameof(Vector2.Y));

		int patched = 0;
		foreach (var (label, which) in new[] { ("Save Changes & Close", 0), ("Save Game and Quit", 1) })
		{
			int s = code.FindIndex(c => c.opcode == OpCodes.Ldstr && (string)c.operand == label);
			if (s < 0)
			{
				continue;
			}
			// The position argument is the first Vector2 local loaded after the label string.
			for (int i = s + 1; i < code.Count && i < s + 12; i++)
			{
				if ((code[i].opcode == OpCodes.Ldloc_S || code[i].opcode == OpCodes.Ldloc) && code[i].operand is LocalBuilder lb && lb.LocalType == typeof(Vector2))
				{
					code.InsertRange(i + 1, new[] { new CodeInstruction(OpCodes.Ldc_I4, which), new CodeInstruction(OpCodes.Call, remap) });
					patched++;
					break;
				}
			}
		}

		// Sword: last DrawSpriteToScreenCoordinates in the method; patch the Y read just before it.
		int sword = code.FindLastIndex(c => c.operand is MethodInfo m && m.Name == nameof(wg2DRenderer.DrawSpriteToScreenCoordinates));
		for (int i = sword - 1; i > sword - 10 && i >= 0; i--)
		{
			if (code[i].opcode == OpCodes.Ldfld && Equals(code[i].operand, vecY))
			{
				code.Insert(i + 1, new CodeInstruction(OpCodes.Call, fixSword));
				patched++;
				break;
			}
		}
		if (patched != 3)
		{
			Log.Write("  Options menu transpiler: only " + patched + "/3 spots found; layout may be partly vanilla");
		}
		return code;
	}

	private static Vector2 Remap(Vector2 natural, int which)
	{
		if (!Config.On(Config.OptionsLayout))
		{
			saveY = quitY = -1;
			return natural;
		}
		wg2DRenderer tr = dmUtilities.theGame.theRenderer;
		int count = tr.optionsList.Count;
		int leftRows = 2 + Math.Min(count, LeftColumnOptions);
		int rightRowsBefore = Math.Max(0, count - LeftColumnOptions) + 2; // options + Edit Keybinds + Edit Hotbars
		bool hasQuit = !dmUtilities.theGame.bShowMainMenu;
		int buttons = hasQuit ? 2 : 1;
		int lastRow = Math.Max(leftRows, rightRowsBefore + 1 + buttons) - 1; // +1 = the empty separator row
		int row = lastRow - (buttons - 1) + which;
		float y = FirstRowY + row * RowH;
		if (which == 0)
		{
			saveY = y;
		}
		else
		{
			quitY = y;
		}
		return new Vector2(natural.X, y);
	}

	private static float FixSwordY(float y)
	{
		wg2DRenderer tr = dmUtilities.theGame.theRenderer;
		int saveIdx = 2 + tr.optionsList.Count + 2;
		if (tr.ui_Options_SelectedOption == saveIdx && saveY >= 0)
		{
			return saveY;
		}
		if (tr.ui_Options_SelectedOption == saveIdx + 1 && quitY >= 0)
		{
			return quitY;
		}
		return y;
	}
}
