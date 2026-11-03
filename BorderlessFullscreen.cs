using System;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DungeonmansQoL;

// The Options menu's "Fullscreen" gets three values instead of two:
//   Off            windowed (vanilla)
//   No border      fills the screen as a borderless window: instant alt-tab, other windows can go on top
//   Full Fullness  the game's own exclusive fullscreen (vanilla "on")
// The game still stores on/off in its options file; which kind of fullscreen is saved in the QoL config.
// MonoGame's GraphicsDeviceManager.HardwareModeSwitch picks the kind; it is set before each of the game's graphics
// changes (dmGame.Window_GraphicsApplyChanges).
internal static class BorderlessFullscreen
{
	private const string ModeKey = "fullscreen_mode"; // 1 no border, 2 full
	private const string FullscreenField = "option_fullScreen";
	private static AccessTools.FieldRef<dmGame, GraphicsDeviceManager> graphicsRef;

	private static bool NoBorder => QoLMod.Ctx.Settings.GetInt(ModeKey, 1) != 2;

	// Looked up here, so a renamed field only marks this part as failed in the log.
	public static void Register(Harmony h)
	{
		graphicsRef = AccessTools.FieldRefAccess<dmGame, GraphicsDeviceManager>("graphics");
		h.Patch(Mod.Target(typeof(dmGame), "Window_GraphicsApplyChanges"), prefix: Mod.Hook(typeof(BorderlessFullscreen), nameof(ApplyPrefix)));
		h.Patch(Mod.Target(typeof(GameOption), nameof(GameOption.ChangeValue)), prefix: Mod.Hook(typeof(BorderlessFullscreen), nameof(ChangeValuePrefix)));
	}

	private static void ApplyPrefix(dmGame __instance)
	{
		GraphicsDeviceManager gdm = graphicsRef(__instance);
		if (gdm != null)
		{
			gdm.HardwareModeSwitch = !NoBorder;
		}
	}

	// Called every frame (cheap): the Fullscreen entry shows which kind of fullscreen is on.
	public static void SyncLabel(dmGame g)
	{
		var list = g.theRenderer?.optionsList;
		if (list == null || graphicsRef == null)
		{
			return;
		}
		foreach (GameOption o in list)
		{
			if (o.propertyToSetInGame == FullscreenField)
			{
				o.strTrue = NoBorder ? "No border" : "Full Fullness";
			}
		}
	}

	// Off -> No border -> Full Fullness -> Off (backwards with Left). Anything unexpected falls back to vanilla.
	private static bool ChangeValuePrefix(GameOption __instance, int delta)
	{
		if (__instance.propertyToSetInGame != FullscreenField)
		{
			return true;
		}
		try
		{
			dmGame g = dmUtilities.theGame;
			int state = !dmOptionsManager.option_fullScreen ? 0 : NoBorder ? 1 : 2;
			int next = (state + (delta < 0 ? 2 : 1)) % 3;
			if (next == 0)
			{
				__instance.SetBooleanWithoutCallbacks(false);
				g.Window_AfterToggleFullScreen();
			}
			else
			{
				QoLMod.Ctx.Settings.SetInt(ModeKey, next);
				if (state == 0)
				{
					__instance.SetBooleanWithoutCallbacks(true);
					g.Window_AfterToggleFullScreen();
				}
				else
				{
					Rearm(g);
				}
			}
			SyncLabel(g);
			dmAudio.PlayUIFX("ui_click_02");
			return false;
		}
		catch (Exception e)
		{
			Log.Write("Fullscreen option: " + e.Message);
			return true;
		}
	}

	// Already fullscreen, other kind wanted: leave fullscreen and enter it again the new way.
	private static void Rearm(dmGame g)
	{
		GraphicsDeviceManager gdm = graphicsRef(g);
		if (gdm == null)
		{
			return;
		}
		DisplayMode mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
		gdm.IsFullScreen = false;
		gdm.ApplyChanges();
		gdm.HardwareModeSwitch = !NoBorder;
		gdm.PreferredBackBufferWidth = mode.Width;
		gdm.PreferredBackBufferHeight = mode.Height;
		gdm.IsFullScreen = true;
		gdm.ApplyChanges();
		g.Window_UpdateRenderInformationAfterChanges();
	}
}
