using System;
using System.Linq;
using System.Collections.Generic;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// QoL Hub: every QoL tool on one screen, with its key, so nothing has to be remembered. End key or the "QoL"
// button top right. Tiles that don't apply here (e.g. the Directory outside the Academy) are grayed with the reason.
internal static class QoLHub
{
	private const Keys HubKey = Keys.End;
	public static bool IsOpen;
	private static bool keyWasDown;

	private sealed class Tile
	{
		public string Title, Key, Icon, Tip;
		public Func<string> Blocked; // null or "" = available, otherwise why not
		public Func<string> TitleSuffix; // live value shown after the title (e.g. the current speed)
		public Action Run;
		public bool KeepOpen;
	}

	private static readonly Tile[] Tiles =
	{
		new Tile { Title = "Academy Directory", Key = "Home", Icon = "npc_academy_headmaster", Tip = "Go to anyone in the Academy in one click",
			Blocked = () => !Config.On(Config.AcademyDirectory) ? "Off in Mod Settings" : !Dm.InAcademy ? "Only in the Academy" : null,
			Run = () => AcademyDirectory.DevOpen(1) },
		new Tile { Title = "Prepare hero", Key = "P (Directory)", Icon = "npc_academy_drill_sgt", Tip = "All new-hero chores at once",
			Blocked = () => !Config.On(Config.HeroPrep) ? "Off in Mod Settings" : !Dm.InAcademy ? "Only in the Academy" : HeroPrep.Plan().Count == 0 ? "Nothing left to do" : null,
			Run = HeroPrep.Run },
		new Tile { Title = "Game speed", Key = "F11", Icon = "condition_speed_up", Tip = "Cycle x1 / x2 / x3", KeepOpen = true,
			TitleSuffix = () => "  x" + GameSpeed.Current,
			Blocked = () => !Config.On(Config.GameSpeed) ? "Off in Mod Settings" : null,
			Run = GameSpeed.CycleFromHub },
		new Tile { Title = "Back to Academy", Key = "", Icon = "proof_of_stremf_sprite", Tip = "Walk home; hand in Academy items if you carry some",
			Blocked = () => !Config.On(Config.ProofReminder) ? "Off in Mod Settings" : !Dm.OnOverworld ? "Only on the overworld" : null,
			Run = OverworldHud.HandInFromHub },
		new Tile { Title = "Map travel", Key = "M, click", Icon = "adventuremap_scroll_sprite_01", Tip = "Open the map and click a place to walk there", KeepOpen = true,
			Blocked = () => !Config.On(Config.MapTravel) ? "Off in Mod Settings" : !Dm.OnOverworld ? "Only on the overworld" : null,
			Run = () => Ui.Toast("Press M, then click a location") },
		new Tile { Title = "Equip best armor", Key = "Ins", Icon = "armor_medium_chest_t1", Tip = "Best helm, gloves and body armor", KeepOpen = true,
			Blocked = () => !Config.On(Config.AutoEquip) ? "Off in Mod Settings" : null,
			Run = AutoEquip.Run },
		new Tile { Title = "Fallen heroes", Key = "", Icon = "memorial_plaque_sprite", Tip = "Memorials to collect, and where",
			Blocked = FallenHeroes.Blocked,
			Run = FallenHeroes.Open },
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
		TopBar.Add("qol.hub", 64, (r, clickable) =>
		{
			g.theRenderer.BeginSpriteBatch();
			Ui.HangingButton("qol_hub_open", r, "QoL", clickable ? Toggle : (Action)null, tooltipText: "All QoL tools (End)\nDrag to move", stackLevel: 6);
			g.theRenderer.EndSpriteBatch();
		}, priority: 20);
	}


	public static void Draw(dmGame g)
	{
		if (!IsOpen)
		{
			return;
		}
		g.theRenderer.BeginSpriteBatch();
		Rectangle c = Ui.ModalWindow("qol_hub", 22, 16, "QoL tools", Toggle);
		const int cols = 2, th = 64, tw = 320;
		Tile[] tiles = Tiles;
		for (int i = 0; i < tiles.Length; i++)
		{
			Tile t = tiles[i];
			var r = new Rectangle(c.X + (i % cols) * (tw + 16), c.Y + (i / cols) * (th + 8), tw, th);
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
			Ui.Box(r, ok && hot);
			Ui.Icon(t.Icon, new Rectangle(r.X + 8, r.Y + 6, 48, 52), ok ? (Color?)null : new Color(90, 84, 78));
			string title = t.Title + t.TitleSuffix?.Invoke();
			Ui.Text(title, r.X + 64, r.Y + 6, ok ? Palette.Text : Palette.TextMuted, UiFont.Medium);
			Ui.Text(Ui.Fit(ok ? t.Tip : why, r.Width - 74), r.X + 64, r.Y + 36, ok ? Palette.TextDim : Palette.Warn);
			if (!string.IsNullOrEmpty(t.Key))
			{
				Ui.TextRight("^3" + t.Key, r.Right - 12, r.Y + 8, Palette.TextDim);
			}
		}
		if (Config.On(Config.AutoEquip))
		{
			int rows = (tiles.Length + cols - 1) / cols;
			int py = c.Y + rows * (th + 8) + 8;
			Ui.Separator(c.X, py - 8, c.Width);
			AutoEquip.DrawWeightPicker("qol_hub_weight", c.X + 8, py + 4);
		}
		g.theRenderer.EndSpriteBatch();
	}
}
