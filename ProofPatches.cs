using System;
using System.Collections.Generic;
using DungeonMans;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// Battlemaster "spend Proofs of Stremf" dialog: vanilla is strictly +1/-1 per click or keypress.
//   Shift + click / arrow  -> 10 steps
//   Ctrl  + click / arrow  -> as many steps as possible (spend everything / refund everything on that stat)
internal static class ProofPatches
{
	private const int ShiftSteps = 10;

	private static readonly Keys[] IncreaseKeys = { Keys.Right, Keys.NumPad6 };
	private static readonly Keys[] DecreaseKeys = { Keys.Left, Keys.NumPad4 };
	private static readonly Keys[] PresetKeys = { Keys.D1, Keys.D2, Keys.D3 };
	private static readonly HashSet<Keys> previouslyDown = new HashSet<Keys>();

	public static void Register(Harmony h)
	{
		h.Patch(Mod.Target(typeof(dmUICallbacks), nameof(dmUICallbacks.IncreaseStatViaProofOfStremf)),
			prefix: Mod.Hook(typeof(ProofPatches), nameof(IncreaseClickPrefix)));
		h.Patch(Mod.Target(typeof(dmUICallbacks), nameof(dmUICallbacks.DecreaseStatViaProofOfStremf)),
			prefix: Mod.Hook(typeof(ProofPatches), nameof(DecreaseClickPrefix)));
	}

	public static void RegisterKeyboard(Harmony h)
	{
		h.Patch(Mod.Target(typeof(ui_spendProofsOfStremfDialog), nameof(ui_spendProofsOfStremfDialog.HandleInput)),
			prefix: Mod.Hook(typeof(ProofPatches), nameof(HandleInputPrefix)));
	}

	public static void RegisterHint(Harmony h)
	{
		h.Patch(Mod.Target(typeof(ui_spendProofsOfStremfDialog), nameof(ui_spendProofsOfStremfDialog.Draw)),
			postfix: Mod.Hook(typeof(ProofPatches), nameof(DrawPostfix)));
	}

	// 1 = vanilla behaviour.
	private static int StepsForModifiers()
	{
		dmInput input = dmUtilities.theGame.theInput;
		if (!Config.On(Config.ProofsBulk))
		{
			return 1;
		}
		if (input.bCtrlIsDown)
		{
			return int.MaxValue;
		}
		return input.bShiftIsDown ? ShiftSteps : 1;
	}

	// Both TryIncrease/TryDecrease act on the dialog's current selection (hovering the button selects its row).
	private static int Repeat(bool increase, int steps)
	{
		ui_spendProofsOfStremfDialog dlg = dmUtilities.theGame.spendProofsDialog;
		int done = 0;
		while (done < steps && (increase ? dlg.TryIncrease(0) : dlg.TryDecrease(0)))
		{
			done++;
		}
		return done;
	}

	private static bool IncreaseClickPrefix(ref object __result)
	{
		int steps = StepsForModifiers();
		if (steps == 1)
		{
			return true;
		}
		__result = dmAudio.PlayUIFX(Repeat(increase: true, steps) > 0 ? "ui_increase_02" : "ui_back_02");
		return false;
	}

	private static bool DecreaseClickPrefix(ref object __result)
	{
		int steps = StepsForModifiers();
		if (steps == 1)
		{
			return true;
		}
		__result = dmAudio.PlayUIFX(Repeat(increase: false, steps) > 0 ? "ui_decrease_02" : "ui_back_02");
		return false;
	}

	// With Shift held the game remaps Left/Right to diagonal actions (and Ctrl+arrow maps to nothing),
	// so modified presses never reach the vanilla handler. Detect them from raw key state instead.
	private static bool HandleInputPrefix(ui_spendProofsOfStremfDialog __instance)
	{
		dmInput input = dmUtilities.theGame.theInput;
		bool increase = PressedThisFrame(input, IncreaseKeys);
		bool decrease = PressedThisFrame(input, DecreaseKeys);
		int presetSlot = Array.FindIndex(PresetKeys, k => PressedThisFrame(input, new[] { k }));
		previouslyDown.Clear();
		previouslyDown.UnionWith(input.PressedKeys);

		if (presetSlot >= 0 && !__instance.InConfirmMode() && Config.On(Config.ProofsPresets))
		{
			UsePreset(__instance, presetSlot, save: input.bShiftIsDown);
			return false;
		}

		int steps = StepsForModifiers();
		if (steps == 1 || __instance.InConfirmMode() || increase == decrease)
		{
			return true;
		}
		int done = Repeat(increase, steps);
		dmAudio.PlayUIFX(done > 0 ? (increase ? "ui_click_01" : "ui_click_02") : "ui_back_02");
		return false;
	}

	private static bool PressedThisFrame(dmInput input, Keys[] keys)
	{
		foreach (Keys k in keys)
		{
			if (input.PressedKeys.Contains(k) && !previouslyDown.Contains(k))
			{
				return true;
			}
		}
		return false;
	}

	private static void UsePreset(ui_spendProofsOfStremfDialog dlg, int slot, bool save)
	{
		bool ok = save ? ProofPresets.Save(dlg, slot) : ProofPresets.Apply(dlg, slot);
		dmAudio.PlayUIFX(ok ? (save ? "ui_confirm" : "ui_increase_02") : "ui_back_02");
	}

	private static object LoadPresetClicked(ui_mouseFrame frame, int iButton)
	{
		UsePreset(dmUtilities.theGame.spendProofsDialog, dmUtilities.GetIntValue(frame.tagStringData), save: false);
		return true;
	}

	private static object SavePresetClicked(ui_mouseFrame frame, int iButton)
	{
		UsePreset(dmUtilities.theGame.spendProofsDialog, dmUtilities.GetIntValue(frame.tagStringData), save: true);
		return true;
	}

	// Panel under the vanilla dialog: shortcut hint + one row per preset with clickable Load / Save.
	private static void DrawPostfix(ui_spendProofsOfStremfDialog __instance)
	{
		bool bulk = Config.On(Config.ProofsBulk), presets = Config.On(Config.ProofsPresets);
		if (__instance.InConfirmMode() || (!bulk && !presets))
		{
			return;
		}
		dmGame game = dmUtilities.theGame;
		wg2DRenderer tr = game.theRenderer;
		// Same placement as the vanilla Draw: a 512x448 box centred on screen.
		int x = (int)(tr.fWindowXSize * 0.5f) - 256;
		int y = (int)(tr.fWindowYSize * 0.5f) - 224 + 448 + 4;
		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		tr.DrawParchmentBox(x, y, 16, presets ? 4 : 1);
		tr.SetActiveFont(tr.fontNyala18);
		string hint = (bulk ? "^3Shift^7 x" + ShiftSteps + "   ^3Ctrl^7 max   " : "")
			+ (presets ? "^3Keys 1-3^7 load preset   ^3Shift+1-3^7 save" : "");
		tr.DrawString(hint, new Vector2(x + 16, y + (presets ? 10 : 6)), Color.White);
		for (int slot = 0; presets && slot < ProofPresets.SlotCount; slot++)
		{
			int rowY = y + 36 + slot * 26;
			tr.DrawString("^3" + (slot + 1) + "^7", new Vector2(x + 16, rowY), Color.White);
			DrawButton(tr, game, "Load", x + 36, rowY, slot, "proofs_preset_load_", LoadPresetClicked, enabled: !ProofPresets.IsEmpty(slot));
			DrawButton(tr, game, "Save", x + 90, rowY, slot, "proofs_preset_save_", SavePresetClicked, enabled: true);
			tr.DrawString(ProofPresets.Describe(slot), new Vector2(x + 150, rowY), ProofPresets.IsEmpty(slot) ? Color.DarkGray : Color.White);
		}
		tr.EndSpriteBatch();
	}

	private static void DrawButton(wg2DRenderer tr, dmGame game, string label, int bx, int by, int slot, string idPrefix,
		ui_mouseFrame.UIActionFunction onClick, bool enabled)
	{
		ui_mouseFrame frame = game.theUIFrames.AddFrame(bx - 4, by, 50, 24, 1, idPrefix + slot);
		frame.tagStringData = slot.ToString();
		frame.funcOnClick = onClick;
		frame.RemoveIfFalse = dmUICallbacks.GameInProofDialogMode;
		frame.RemoveIfTrue = dmUICallbacks.ProofDialogInConfirmMode;
		Color color = !enabled ? Color.DarkGray : game.theInput.IsActiveFrame(frame) ? ShepColor.Yellow : ShepColor.Cyan;
		tr.DrawString("[" + label + "]", new Vector2(bx, by), color);
	}
}
