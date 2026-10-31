using System;
using DungeonMans;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// Game speed x1 / x2 / x3 (F11 or click the label, top left). Multiplies the frame time the game receives in
// Update/Draw, so animations, movement, turn delays and effects play faster; the rules don't change.
// Side effect: held-key repeat is faster too. Never applied on the main menu.
internal static class GameSpeed
{
	private const Keys CycleKey = Keys.F11;
	private static int speed = 1;
	private static bool keyWasDown;

	public static void Register(Harmony h)
	{
		h.Patch(Mod.Target(typeof(dmGame), "Update"), prefix: Mod.Hook(typeof(GameSpeed), nameof(ScaleTime)));
		h.Patch(Mod.Target(typeof(dmGame), "Draw"), prefix: Mod.Hook(typeof(GameSpeed), nameof(ScaleTime)));
	}

	private static bool Active(dmGame g) => Config.On(Config.GameSpeed) && speed > 1 && !g.bShowMainMenu && g.dungeonMans != null;

	private static void ScaleTime(dmGame __instance, ref GameTime gameTime)
	{
		if (Active(__instance))
		{
			gameTime = new GameTime(gameTime.TotalGameTime, TimeSpan.FromTicks(gameTime.ElapsedGameTime.Ticks * speed), gameTime.IsRunningSlowly);
		}
	}

	private static void Cycle()
	{
		speed = speed % 3 + 1;
		dmAudio.PlayUIFX("ui_click_01");
		dmUtilities.theGame.AddStringToLog("^3Game speed:^7 x" + speed);
	}

	// Called every frame from the in-game input hook (doesn't consume input).
	public static void HandleKey(dmGame g)
	{
		bool down = g.theInput.PressedKeys.Contains(CycleKey);
		if (down && !keyWasDown && Config.On(Config.GameSpeed))
		{
			Cycle();
		}
		keyWasDown = down;
	}

	private static object Click(ui_mouseFrame f, int b)
	{
		Cycle();
		return true;
	}

	private static bool Visible(ui_mouseFrame f) => Config.On(Config.GameSpeed) && !dmUtilities.theGame.bShowMainMenu;

	public static void Draw(dmGame g)
	{
		if (!Config.On(Config.GameSpeed) || AcademyDirectory.UiBusy(g))
		{
			return;
		}
		wg2DRenderer tr = g.theRenderer;
		tr.BeginSpriteBatch();
		tr.bForceDropShadows = true;
		ui_mouseFrame f = g.theUIFrames.AddFrame(8, 4, 130, 26, 6, "qol_speed");
		f.funcOnClick = Click;
		f.RemoveIfFalse = Visible;
		tr.SetActiveFont(tr.fontNyala18);
		Color c = g.theInput.IsActiveFrame(f) ? ShepColor.Yellow : speed > 1 ? ShepColor.Cyan : Color.LightGray;
		tr.DrawString("Speed x" + speed + "  [F11]", new Vector2(12, 8), c);
		tr.EndSpriteBatch();
	}
}
