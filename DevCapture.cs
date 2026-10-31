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
		new Scene { Name = "hud", Setup = () => { CoopPanel.ForceClose(); QoLMod.Ctx.Settings.Set(PoultryHud.ExpandedKey, true); } },
		new Scene { Name = "hud_collapsed", Setup = () => { CoopPanel.ForceClose(); QoLMod.Ctx.Settings.Set(PoultryHud.ExpandedKey, false); } },
		new Scene { Name = "coop_birds", Setup = () => CoopPanel.Open(0) },
		new Scene { Name = "coop_breed", Setup = () => { CoopPanel.Open(1); CoopPanel.DevPickParents(); } },
		new Scene { Name = "coop_ascend", Setup = () => CoopPanel.Open(2) },
		new Scene { Name = "coop_pantry", Setup = () => CoopPanel.Open(3) },
		new Scene { Name = "settings", Setup = () => { CoopPanel.ForceClose(); Ui.OpenModSettings(); } },
		new Scene { Name = "options", Setup = () => { Ui.CloseModSettings(); Dm.Game.ToggleOptionsMode(true); } },
		new Scene { Name = "directory", Setup = () => { Dm.Game.ToggleOptionsMode(false); if (Dm.InAcademy) { AcademyDirectory.DevOpen(1); } } },
		new Scene { Name = "directory_fav", Setup = () => { if (Dm.InAcademy) { AcademyDirectory.DevOpen(0); } } },
		new Scene { Name = "hub", Setup = () => { AcademyDirectory.IsOpen = false; QoLHub.IsOpen = true; } },
		new Scene { Name = "world", Setup = () => { QoLHub.IsOpen = false; } },
	};

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
