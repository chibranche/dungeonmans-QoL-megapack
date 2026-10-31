using System;
using System.Collections.Generic;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// QoL Hub: every QoL tool on one screen, with its key, so nothing has to be remembered. End key or the "QoL"
// button top left. Tiles that don't apply here (e.g. the Directory outside the Academy) are grayed with the reason.
internal static class QoLHub
{
	private const Keys HubKey = Keys.End;
	public static bool IsOpen;
	private static bool keyWasDown;

	private sealed class Tile
	{
		public string Title, Key, Icon, Tip;
		public Func<string> Blocked; // null or "" = available, otherwise why not
		public Action Run;
		public bool KeepOpen;
	}

	private static readonly Tile[] Tiles =
	{
		new Tile { Title = "Academy Directory", Key = "Home", Icon = "ui_character_sheet", Tip = "Go to anyone in the Academy in one click",
			Blocked = () => !Config.On(Config.AcademyDirectory) ? "Off in Mod Settings" : !Dm.InAcademy ? "Only in the Academy" : null,
			Run = () => AcademyDirectory.DevOpen(1) },
		new Tile { Title = "Prepare hero", Key = "P (Directory)", Icon = "ui_levelup", Tip = "All new-hero chores at once",
			Blocked = () => !Config.On(Config.HeroPrep) ? "Off in Mod Settings" : !Dm.InAcademy ? "Only in the Academy" : HeroPrep.Plan().Count == 0 ? "Nothing left to do" : null,
			Run = HeroPrep.Run },
		new Tile { Title = "The Coop", Key = "", Icon = "ui_favorite_heart", Tip = "Birds, feeding, breeding, Ascend, Pantry",
			Blocked = () => !Config.On(Config.Coop) ? "Off in Mod Settings" : null,
			Run = () => CoopPanel.Open() },
		new Tile { Title = "Game speed", Key = "F11", Icon = "condition_speed_up", Tip = "Cycle x1 / x2 / x3", KeepOpen = true,
			Blocked = () => !Config.On(Config.GameSpeed) ? "Off in Mod Settings" : null,
			Run = GameSpeed.CycleFromHub },
		new Tile { Title = "Hand in Academy items", Key = "", Icon = "ui_points_to_spend", Tip = "Walk to the Academy and open the Headmaster",
			Blocked = () => !Config.On(Config.ProofReminder) ? "Off in Mod Settings" : !Dm.OnOverworld ? "Only on the overworld" : !OverworldHud.HasItems ? "No Academy items carried" : null,
			Run = OverworldHud.HandInFromHub },
		new Tile { Title = "Map travel", Key = "M, click", Icon = "sprite_world_select_marker", Tip = "Open the map and click a place to walk there", KeepOpen = true,
			Blocked = () => !Config.On(Config.MapTravel) ? "Off in Mod Settings" : !Dm.OnOverworld ? "Only on the overworld" : null,
			Run = () => Ui.Toast("Press M, then click a location") },
		new Tile { Title = "Mod Settings", Key = "", Icon = "ui_menu_pullout", Tip = "Switch any feature on or off",
			Run = Ui.OpenModSettings },
	};

	public static void Toggle()
	{
		IsOpen = !IsOpen;
		dmAudio.PlayUIFX(IsOpen ? "open_dialog" : "ui_back_02");
	}

	// From the QoL input handler. Returns true when the input was consumed.
	public static bool HandleInput(dmGame g)
	{
		bool down = g.theInput.PressedKeys.Contains(HubKey);
		bool pressed = down && !keyWasDown;
		keyWasDown = down;
		if (!IsOpen)
		{
			if (pressed && !Ui.IsBusy(g))
			{
				Toggle();
				return true;
			}
			return false;
		}
		if (pressed || g.theInput.ExecutedActions.Contains(ActionButton.Close))
		{
			Toggle();
		}
		g.theInput.ExecutedActions.Clear();
		return true;
	}

	public static void DrawButton(dmGame g)
	{
		if (IsOpen || Ui.IsBusy(g))
		{
			return;
		}
		g.theRenderer.BeginSpriteBatch();
		Ui.BoxButton("qol_hub_open", new Rectangle(106, 6, 56, 30), "QoL", Toggle, font: UiFont.Medium, tooltipText: "All QoL tools (End)", stackLevel: 6);
		g.theRenderer.EndSpriteBatch();
	}

	public static void Draw(dmGame g)
	{
		if (!IsOpen)
		{
			return;
		}
		g.theRenderer.BeginSpriteBatch();
		Rectangle c = Ui.ModalWindow("qol_hub", 22, 11, "QoL tools", Toggle, "End to open / close");
		const int cols = 2, th = 66;
		int tw = (c.Width - (cols - 1) * 10) / cols;
		for (int i = 0; i < Tiles.Length; i++)
		{
			Tile t = Tiles[i];
			var r = new Rectangle(c.X + (i % cols) * (tw + 10), c.Y + (i / cols) * (th + 8), tw, th);
			string why = t.Blocked?.Invoke();
			bool ok = string.IsNullOrEmpty(why);
			ui_mouseFrame f = Ui.Area("qol_hub_tile" + i, r, ok ? () =>
			{
				dmAudio.PlayUIFX("ui_click_01");
				if (!t.KeepOpen)
				{
					IsOpen = false;
				}
				t.Run();
			} : (Action)null, Ui.WindowLevel + 1);
			bool hot = Ui.Hovered(f);
			Ui.Panel(r, !ok ? Palette.Panel : hot ? Palette.PanelHover : Palette.PanelRaised, ok && hot ? Palette.BorderBright : Palette.Border);
			var icon = new Rectangle(r.X + 10, r.Y + 12, 42, 42);
			Ui.Fill(icon, Palette.BarBack);
			Ui.Icon(t.Icon, new Rectangle(icon.X + 5, icon.Y + 5, 32, 32), ok ? (Color?)null : new Color(110, 100, 90));
			string title = t.Title + (t == Tiles[3] ? "  x" + GameSpeed.Current : "");
			Ui.Text(title, r.X + 64, r.Y + 8, ok ? Palette.Text : Palette.TextMuted, UiFont.Medium);
			Ui.Text(ok ? t.Tip : why, r.X + 64, r.Y + 38, ok ? Palette.TextDim : Palette.Warn);
			if (!string.IsNullOrEmpty(t.Key))
			{
				int kw = (int)Ui.Measure(t.Key).X + 12;
				Ui.Chip(r.Right - kw - 10, r.Y + 10, t.Key, Palette.TextDim);
			}
		}
		g.theRenderer.EndSpriteBatch();
	}
}
