using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace DungeonmansQoL;

// Developer tool, inactive unless Mods\QoL\devcapture.txt exists. It loads the first save slot, runs each scene listed
// in that file (one per line, or "all"), saves a PNG of the screen per scene into Mods\QoL\shots\, then kills the game
// without saving. Used to check UI layouts without clicking through the game.
internal static class DevCapture
{
	private sealed class Scene
	{
		public string Name;
		public Action Setup;
	}

	private static readonly Scene[] Scenes =
	{
		new Scene { Name = "gallery", Setup = () => { CoopPanel.ForceClose(); gallery = true; } },
		new Scene { Name = "hud", Setup = () => { gallery = false; CoopPanel.ForceClose(); QoLMod.Ctx.Settings.Set(PoultryHud.ExpandedKey, true); } },
		new Scene { Name = "hud_collapsed", Setup = () => { CoopPanel.ForceClose(); QoLMod.Ctx.Settings.Set(PoultryHud.ExpandedKey, false); } },
		new Scene { Name = "coop_birds", Setup = () => CoopPanel.Open(0) },
		new Scene { Name = "coop_breed", Setup = () => { CoopPanel.Open(1); CoopPanel.DevPickParents(); } },
		new Scene { Name = "coop_ascend", Setup = () => CoopPanel.Open(2) },
		new Scene { Name = "coop_pantry", Setup = () => CoopPanel.Open(3) },
		new Scene { Name = "settings", Setup = () => { CoopPanel.ForceClose(); Ui.OpenModSettings(); } },
		new Scene { Name = "options", Setup = () => { Ui.CloseModSettings(); Dm.Game.ToggleOptionsMode(true); } },
		new Scene { Name = "directory", Setup = () => { Ui.CloseModSettings(); Dm.Game.ToggleOptionsMode(false); if (Dm.InAcademy) { AcademyDirectory.DevOpen(1); } } },
		new Scene { Name = "directory_fav", Setup = () => { if (Dm.InAcademy) { AcademyDirectory.DevOpen(0); } } },
		new Scene { Name = "hub", Setup = () => { AcademyDirectory.IsOpen = false; QoLHub.IsOpen = true; } },
		new Scene { Name = "world", Setup = () => { QoLHub.IsOpen = false; } },
	};

	private static bool gallery;

	// Draws the game's own UI pieces side by side, to pick assets for mod windows.
	private static void DrawGallery(dmGame g)
	{
		if (!gallery)
		{
			return;
		}
		wg2DRenderer tr = g.theRenderer;
		tr.BeginSpriteBatch();
		tr.DimScreen();
		int x = 40, y = 40;
		void Label(string s, int lx, int ly) => Ui.Text(s, lx, ly, Color.White);
		tr.DrawDarkBox(x, y, 6, 3); Label("DarkBox", x, y + 100);
		tr.DrawLightBox(x + 220, y, 6, 3); Label("LightBox", x + 220, y + 100);
		tr.DrawShadowBox(x + 440, y, 6, 3); Label("ShadowBox", x + 440, y + 100);
		tr.DrawParchmentBox(x + 660, y, 6, 3); Label("ParchmentBox", x + 660, y + 100);
		tr.DrawParchmentTextBox(x + 880, y, 6, 2, false); Label("ParchTextBox", x + 880, y + 70);
		tr.DrawParchmentTextBox(x + 1100, y, 6, 2, true); Label("ParchTextBox sel", x + 1100, y + 70);
		y += 140;
		tr.DrawDarkButton(x, y, 5, 1); Label("DarkButton 5x1", x, y + 40);
		tr.DrawHighlightButton(x + 220, y, 5, 1); Label("HighlightButton 5x1", x + 220, y + 40);
		tr.DrawDarkButton(x + 440, y, 5, 2); Label("DarkButton 5x2", x + 440, y + 70);
		tr.DrawHighlightButton(x + 660, y, 5, 2); Label("HighlightButton 5x2", x + 660, y + 70);
		tr.DrawBorderedBox(x + 880, y, 200, 60); Label("BorderedBox", x + 880, y + 70);
		tr.EndSpriteBatch();
		tr.DrawBigFillBar(new Rectangle(x + 1100, y, 300, 30), 0.6f);
		tr.BeginSpriteBatch();
		Label("BigFillBar", x + 1100, y + 40);
		y += 120;
		string[] sprites = { "ui_slider_checkbox_no", "ui_slider_checkbox_yes", "ui_slider_checkbox_yes_blood", "ui_slider_checkbox_locked",
			"ui_slider_left_cap", "ui_slider_base", "ui_slider_right_cap", "ui_slider_marker",
			"ui_draggable_topbar_left", "ui_draggable_topbar_center", "ui_draggable_topbar_right",
			"ui_draggable_button_arrow_up", "ui_draggable_button_arrow_down", "ui_draggable_button_dock_to_bars", "ui_draggable_button_title_bar_flip", "ui_draggable_button_bottom_right_corner",
			"ui_keyboard_key", "ui_new_sign", "ui_points_to_spend", "ui_levelup", "ui_lock", "ui_favorite_heart", "ui_sealed_stamp", "ui_yellow_red_arrow",
			"ui_attack_block", "ui_attack_parry", "ui_attack_dodge", "ui_attack_counter", "condition_speed_up", "condition_regen", "condition_atk_up", "single_star_font" };
		int sx = x;
		foreach (string n in sprites)
		{
			dmSprite sp = tr.GetSpriteByName(n);
			int w = Math.Max(24, sp.Width), h = Math.Max(24, sp.Height);
			if (sx + w > 1880) { sx = x; y += 90; }
			tr.DrawSpriteToScreenCoordinates(sp, sx, y, sp.Width, sp.Height);
			Ui.Text(n.Replace("ui_", "").Replace("draggable_", "dr_").Replace("slider_", "sl_"), sx, y + h + 2, Color.LightGray);
			sx += Math.Max(w, 120) + 12;
		}
		y += 110;
		string[] bars = { "ook_ui_bag_statbar_empty", "ook_ui_bag_healthbar", "ook_ui_bag_staminabar", "ook_ui_bag_manabar", "ook_ui_bag_irebar", "ook_ui_bag_statbar_frame", "ook_ui_bag_xp_ribbon" };
		int by = y;
		foreach (string n in bars)
		{
			dmSprite sp = tr.GetSpriteByName(n);
			tr.DrawSpriteToScreenCoordinates(sp, x, by, sp.Width, sp.Height);
			Ui.Text(n, x + 270, by + 4, Color.LightGray);
			by += 36;
		}
		dmSprite top = tr.GetSpriteByName("ook_ui_bag_top"), back = tr.GetSpriteByName("ook_ui_bag_back"), bottom = tr.GetSpriteByName("ook_ui_bag_bottom");
		tr.DrawSpriteToScreenCoordinates(top, x + 560, y, top.Width, top.Height);
		tr.DrawSpriteToScreenCoordinates(back, x + 560, y + top.Height, back.Width, back.Height);
		tr.DrawSpriteToScreenCoordinates(bottom, x + 560, y + top.Height + back.Height, bottom.Width, bottom.Height);
		dmSprite paper = tr.GetSpriteByName("ui_official_bird_paper");
		tr.DrawSpriteToScreenCoordinates(paper, x + 860, y, paper.Width, paper.Height);
		dmSprite page = tr.GetSpriteByName("ui_book_page_bg");
		tr.DrawSpriteToScreenCoordinates(page, x + 1300, y, 460, 270);
		tr.EndSpriteBatch();
	}

	private static string ListPath => QoLMod.Ctx.PathFor("devcapture.txt");
	private static string ShotsDir => QoLMod.Ctx.PathFor("shots");

	private static List<Scene> queue;
	private static int stage; // 0 wait menu, 1 loading, 2 settle, 3 scenes
	private static int wait;
	private static Scene current;
	private static bool restoreExpanded;

	public static void Register(ModContext ctx, Harmony h)
	{
		if (!File.Exists(ListPath))
		{
			return;
		}
		string[] wanted = File.ReadAllLines(ListPath).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
		queue = Scenes.Where(s => wanted.Contains("all") || wanted.Contains(s.Name)).ToList();
		ctx.Log.Write("DevCapture active: " + string.Join(", ", queue.Select(s => s.Name)));
		restoreExpanded = ctx.Settings.On(PoultryHud.ExpandedKey);
		Events.Update += Tick;
		Events.DrawOverlay += DrawGallery;
		h.Patch(Patching.Method(typeof(dmGame), "Draw"), postfix: Patching.Hook(typeof(DevCapture), nameof(DrawPostfix)));
	}

	private static void Tick(dmGame g)
	{
		switch (stage)
		{
		case 0:
			dmMainMenuInterface menu = g.mainMenu;
			if (g.bShowMainMenu && menu != null && menu.eMainMenuState == dmMainMenuInterface.MainMenuState.MMS_MainTitle && menu.loadableHeroes != null)
			{
				int slot = Array.FindIndex(menu.loadableHeroes, w => w?.activeHero != null);
				if (slot < 0)
				{
					Finish("no save with a hero");
					return;
				}
				loadableWorldData w = menu.loadableHeroes[slot];
				g.iSaveSlot = slot;
				g.selectedHeroToLoad = w;
				g.strPlayerName = w.activeHero.Name;
				g.strActiveWorldName = w.strWorldName;
				menu.InitMainMenu();
				g.BeginGameplay(bNewGame: false);
				stage = 1;
			}
			break;
		case 1:
			if (Dm.InGame && g.theCurrentLevel != null)
			{
				stage = 2;
				wait = 180;
			}
			break;
		case 2:
			// Close any welcome dialog so HUD windows aren't hidden.
			if (g.bDialogBoxMode)
			{
				g.currentDialogBox = null;
				g.bDialogBoxMode = false;
			}
			if (--wait <= 0)
			{
				stage = 3;
				NextScene();
			}
			break;
		case 3:
			if (g.bDialogBoxMode)
			{
				g.currentDialogBox = null;
				g.bDialogBoxMode = false;
			}
			wait--;
			break;
		}
	}

	private static void NextScene()
	{
		if (queue.Count == 0)
		{
			Finish("done");
			return;
		}
		current = queue[0];
		queue.RemoveAt(0);
		try
		{
			current.Setup();
		}
		catch (Exception e)
		{
			QoLMod.Ctx.Log.Write("DevCapture scene " + current.Name + " setup failed: " + e);
		}
		wait = 40;
	}

	private static void DrawPostfix(dmGame __instance)
	{
		if (stage != 3 || current == null || wait > 0)
		{
			return;
		}
		try
		{
			GraphicsDevice gd = __instance.GraphicsDevice;
			int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
			var data = new Color[w * h];
			gd.GetBackBufferData(data);
			for (int i = 0; i < data.Length; i++)
			{
				data[i].A = 255;
			}
			using var tex = new Texture2D(gd, w, h);
			tex.SetData(data);
			Directory.CreateDirectory(ShotsDir);
			using FileStream fs = File.Create(Path.Combine(ShotsDir, current.Name + ".png"));
			tex.SaveAsPng(fs, w, h);
			QoLMod.Ctx.Log.Write("DevCapture: " + current.Name + ".png " + w + "x" + h);
		}
		catch (Exception e)
		{
			QoLMod.Ctx.Log.Write("DevCapture capture failed: " + e);
		}
		current = null;
		NextScene();
	}

	private static void Finish(string why)
	{
		QoLMod.Ctx.Log.Write("DevCapture finished: " + why);
		QoLMod.Ctx.Settings.Set(PoultryHud.ExpandedKey, restoreExpanded);
		// Hard exit: never let the game save anything from a capture session.
		Environment.Exit(0);
	}
}
