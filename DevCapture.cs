using System;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Screenshot scenes for ModKit's DevCapture (developer tool, inactive for players: see the toolkit's
// tools\capture.ps1). Also an asset gallery of the game's own UI pieces, to pick graphics for mod windows.
internal static class QoLCapture
{
	public static void Register()
	{
		Events.DrawOverlay += DrawGallery;
		DevCapture.Scene("qol_gallery", () => gallery = true, () => gallery = false);
		DevCapture.Scene("qol_topbar", () => { });
		DevCapture.Scene("qol_hub", () => QoLHub.IsOpen = true, () => QoLHub.IsOpen = false);
		DevCapture.Scene("qol_fallen", () => FallenHeroes.IsOpen = true, () => FallenHeroes.IsOpen = false);
		DevCapture.Scene("qol_settings", Ui.OpenModSettings, Ui.CloseModSettings);
		DevCapture.Scene("qol_inventory", () => Dm.Game.OpenInventoryPanel(), () => Dm.Game.bInventoryMode = false);
		DevCapture.Scene("qol_docs", () => Ui.OpenModDocs("qol"), Ui.CloseModSettings);
		DevCapture.Scene("qol_options", () => Dm.Game.ToggleOptionsMode(true), () => Dm.Game.ToggleOptionsMode(false));
		// Academy scenes last: qol_enter_academy moves the hero into the Academy (capture sessions never save).
		DevCapture.Scene("qol_enter_academy", EnterAcademy);
		DevCapture.Scene("qol_academy", () => { });
		DevCapture.Scene("qol_directory", () => { if (Dm.InAcademy) { AcademyDirectory.DevOpen(1); } }, () => AcademyDirectory.IsOpen = false);
		DevCapture.Scene("qol_directory_fav", () => { if (Dm.InAcademy) { AcademyDirectory.DevOpen(0); } }, () => AcademyDirectory.IsOpen = false);
	}

	private static bool gallery;

	// Same gateway the game builds when it sends a hero home after a new overworld.
	private static void EnterAcademy()
	{
		dmGame g = Dm.Game;
		if (Dm.InAcademy || g.dungeonMans == null)
		{
			return;
		}
		dmGateway gate = (dmGateway)g.SpawnActorFromArchetype("default_gateway");
		gate.worldArchetypeToLoad = "dmAcademy";
		gate.areaGenerationData = "academy_data";
		gate.saveName = "dungeonmans_academy";
		g.currentActivatedGateway = gate;
		g.bChangeMapThisFrame = true;
	}

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
}
