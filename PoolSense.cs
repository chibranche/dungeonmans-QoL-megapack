using System;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// Magic pools ("wells") and Dungeon Sense. Each time you step into a pool, the game rolls Dungeon Sense + 1d100;
// above 95 it adds a line saying the pool is good or bad (the line is never wrong). With this on:
//  - that line is colored: green for a good feeling, red for bad news;
//  - the pool remembers what you sensed (saved with the pool) and gets a green / red frame on the map until it is
//    drunk, and later visits repeat the reading even if the new roll fails;
//  - the pool's rolled effect is saved with it. Vanilla only saves good/bad, so after a reload the pool rolled
//    again and could turn bad under a green frame.
internal static class PoolSense
{
	private const string Var = "qol_pool_sense";
	private const string ResultVar = "qol_pool_result";
	private const string GoodLine = "^3Dungeon Sense:^7 You can never be sure, but you have a good feeling about this pool.";
	private const string BadLine = "^3Dungeon Sense:^7 Something isn't right, and you get the feeling this is all bad news.";
	private static readonly Color GoodColor = new Color(110, 220, 110), BadColor = new Color(235, 80, 70);

	public static void Register(Harmony h)
	{
		Events.DialogShown += OnDialog;
		h.Patch(Mod.Target(typeof(dmMagicPool), "InitializePool"), postfix: Mod.Hook(typeof(PoolSense), nameof(InitializePostfix)));
		h.Patch(Mod.Target(typeof(dmMagicPool), nameof(dmMagicPool.EncroachedBy)), prefix: Mod.Hook(typeof(PoolSense), nameof(EncroachedPrefix)));
	}

	// Remember what the pool rolled (script vars are saved with the actor).
	private static void InitializePostfix(dmMagicPool __instance)
	{
		try
		{
			if (Config.On(Config.PoolSense))
			{
				__instance.SetScriptVar_String(ResultVar, (__instance.bPoolIsBad ? "bad|" : "good|") + __instance.strPoolResultArchetype);
			}
		}
		catch (Exception e)
		{
			Log.Write("Pool sense: could not save the roll: " + e.Message);
		}
	}

	// After a reload: put the saved roll back before the game rolls a new one.
	private static void EncroachedPrefix(dmMagicPool __instance)
	{
		if (__instance.bPoolInitialized || !Config.On(Config.PoolSense))
		{
			return;
		}
		string[] saved;
		try
		{
			saved = (__instance.GetScriptVar_String(ResultVar) ?? "").Split('|');
		}
		catch
		{
			return;
		}
		if (saved.Length == 2 && !string.IsNullOrEmpty(saved[1]))
		{
			__instance.bPoolIsBad = saved[0] == "bad";
			__instance.strPoolResultArchetype = saved[1];
			__instance.bPoolInitialized = true;
		}
	}

	private static void OnDialog(dmGame g, dmDialogBox box)
	{
		if (!Config.On(Config.PoolSense) || !(box?.dialogInstigator is dmMagicPool pool) || box.strDialogText == null)
		{
			return;
		}
		if (box.strDialogText.Contains(GoodLine))
		{
			pool.SetScriptVar_String(Var, "good");
			box.strDialogText = box.strDialogText.Replace(GoodLine, "^2Dungeon Sense: you have a good feeling about this pool.^7");
		}
		else if (box.strDialogText.Contains(BadLine))
		{
			pool.SetScriptVar_String(Var, "bad");
			box.strDialogText = box.strDialogText.Replace(BadLine, "^1Dungeon Sense: something isn't right. This is all bad news.^7");
		}
		else
		{
			string known = pool.GetScriptVar_String(Var);
			if (known == "good")
			{
				box.strDialogText += " \n\n ^2Dungeon Sense (earlier): a good feeling about this pool.^7";
			}
			else if (known == "bad")
			{
				box.strDialogText += " \n\n ^1Dungeon Sense (earlier): this pool is bad news.^7";
			}
		}
	}

	// Green / red frame on sensed pools that are still full.
	public static void Draw(dmGame g)
	{
		if (!Config.On(Config.PoolSense) || g.theCurrentLevel == null || g.dungeonMans.bIsInOverworld || Ui.IsBusy(g))
		{
			return;
		}
		TileMarks.Draw(g, LevelActors.Of<dmMagicPool>(g)
			.Where(p => !p.bEmpty && !string.IsNullOrEmpty(p.GetScriptVar_String(Var)))
			.Select(p => (p.Location, p.GetScriptVar_String(Var) == "good" ? GoodColor : BadColor)));
	}
}
