using System;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// Game speed x1 / x2 / x3: the button top left, F11, or "Game Speed" in the Options menu. Multiplies the frame time
// the game receives in Update/Draw, so animations, movement, turn delays and effects play faster; the rules don't
// change. Side effect: held-key repeat is faster too. Never applied on the main menu. Remembered between launches.
internal static class GameSpeed
{
	private const Keys CycleKey = Keys.F11;
	private const string SpeedKey = "game_speed_value";
	private static int speed = 1;
	private static bool keyWasDown;
	private static GameOption option;

	public static void Register(Harmony h)
	{
		speed = Math.Clamp(QoLMod.Ctx.Settings.GetInt(SpeedKey, 1), 1, 3);
		h.Patch(Mod.Target(typeof(dmGame), "Update"), prefix: Mod.Hook(typeof(GameSpeed), nameof(ScaleTime)));
		h.Patch(Mod.Target(typeof(dmGame), "Draw"), prefix: Mod.Hook(typeof(GameSpeed), nameof(ScaleTime)));
		h.Patch(Mod.Target(typeof(GameOption), nameof(GameOption.ChangeValue)), postfix: Mod.Hook(typeof(GameSpeed), nameof(OptionChanged)));
	}

	private static bool Active(dmGame g) => Config.On(Config.GameSpeed) && speed > 1 && !g.bShowMainMenu && g.dungeonMans != null;

	private static void ScaleTime(dmGame __instance, ref GameTime gameTime)
	{
		if (Active(__instance))
		{
			gameTime = new GameTime(gameTime.TotalGameTime, TimeSpan.FromTicks(gameTime.ElapsedGameTime.Ticks * speed), gameTime.IsRunningSlowly);
		}
	}

	private static void Set(int value, bool announce)
	{
		speed = Math.Clamp(value, 1, 3);
		QoLMod.Ctx.Settings.SetInt(SpeedKey, speed);
		option?.SetIntOption(speed, 1, 3);
		if (announce)
		{
			dmAudio.PlayUIFX("ui_click_01");
			Ui.Toast("Game speed x" + speed, speed > 1 ? Palette.Info : Palette.TextDim);
		}
	}

	private static void Cycle() => Set(speed % 3 + 1, true);

	public static void CycleFromHub() => Cycle();

	public static int Current => speed;

	// The Options menu entry. Added while the switch is on; the game only draws and cycles it, we read the value.
	public static void SyncOption(dmGame g)
	{
		var list = g.theRenderer?.optionsList;
		if (list == null)
		{
			return;
		}
		bool want = Config.On(Config.GameSpeed);
		if (option == null)
		{
			option = new GameOption { strOptionText = "Game Speed (F11)" };
			option.SetIntOption(speed, 1, 3);
		}
		if (want && !list.Contains(option))
		{
			list.Add(option);
		}
		else if (!want && list.Contains(option))
		{
			list.Remove(option);
		}
	}

	private static void OptionChanged(GameOption __instance)
	{
		if (__instance == option)
		{
			Set(option.GetIntValue(), false);
		}
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

	public static void Draw(dmGame g)
	{
		if (!Config.On(Config.GameSpeed) || g.bShowMainMenu || g.dungeonMans == null || AcademyDirectory.UiBusy(g))
		{
			return;
		}
		g.theRenderer.BeginSpriteBatch();
		var r = new Rectangle(8, 6, 92, 30);
		bool fast = speed > 1;
		bool hot = Ui.BoxButton("qol_speed", r, "", Cycle, accent: fast ? Palette.Info : (Color?)null, selected: fast,
			tooltipText: "Game speed x" + speed + "\nClick or F11: x1 / x2 / x3\nAlso in the Options menu");
		Ui.Icon("condition_speed_up", new Rectangle(r.X + 6, r.Y + 3, 24, 24), fast || hot ? (Color?)null : new Color(170, 160, 140));
		Ui.Text("x" + speed, r.X + 38, r.Y + 2, fast ? Palette.Accent : Palette.Text, UiFont.Medium);
		g.theRenderer.EndSpriteBatch();
	}
}
