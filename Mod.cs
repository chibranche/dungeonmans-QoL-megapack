using System;
using System.Reflection;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;

namespace DungeonmansQoL;

// Forwards to ModKit's shared log (ModKit\modkit.log), prefixed "[qol]".
internal static class Log
{
	public static void Write(string msg) => QoLMod.Ctx?.Log.Write(msg);
}

// ModKit entry point: registers settings, patches, windows and input.
public sealed class QoLMod : IMod
{
	internal static ModContext Ctx;

	public void Load(ModContext ctx)
	{
		Ctx = ctx;
		Config.Register(ctx.Settings);

		ctx.Patch("Proofs: bulk +/- on mouse click", ProofPatches.Register);
		ctx.Patch("Proofs: bulk +/- on keyboard", ProofPatches.RegisterKeyboard);
		ctx.Patch("Proofs: hint line", ProofPatches.RegisterHint);
		ctx.Patch("Hatch: skip greeting", HatchPatches.RegisterSkipGreeting);
		ctx.Patch("Hatch: FAQ in main menu", HatchPatches.RegisterFaqInMenu);
		ctx.Patch("Hatch: deposit/withdraw all", HatchPatches.RegisterBulkTransfer);
		ctx.Patch("Map: remembered filters", MapPatches.RegisterFilters);
		ctx.Patch("Map: click to travel", MapPatches.RegisterTravel);
		ctx.Patch("Options menu: save/quit moved to bottom", OptionsMenuPatch.Register);
		ctx.Patch("Poultry: breeding bug fix", PoultryCore.Register);
		ctx.Patch("Game speed", GameSpeed.Register);
		ctx.Patch("Headmaster: hide full Library", HeadmasterPatches.Register);

		// QoL windows count as open menus for every mod (and for each other).
		Ui.AddModal(() => AcademyDirectory.IsOpen);
		Ui.AddModal(() => CoopPanel.IsOpen);
		Ui.AddModal(() => QoLHub.IsOpen);

		DevCapture.Register(ctx, ctx.Harmony);

		Events.Input += g => !AcademyDirectory.InputPrefix(g);
		Events.DrawOverlay += DrawOverlay;
	}

	private static void DrawOverlay(dmGame g)
	{
		GameSpeed.SyncOption(g);
		AcademyDirectory.DrawOverlay(g);
		if (g.dungeonMans != null && !g.bShowMainMenu)
		{
			FloorHighlight.Draw(g);
			OverworldHud.Draw(g);
			GameSpeed.Draw(g);
			QoLHub.DrawButton(g);
			QoLHub.Draw(g);
			PoultryHud.Draw(g);
			if (CoopPanel.IsOpen)
			{
				CoopPanel.Draw(g);
			}
		}
	}
}

internal static class Mod
{
	public static HarmonyMethod Hook(Type type, string name) => Patching.Hook(type, name);

	public static MethodInfo Target(Type type, string name) => Patching.Method(type, name);
}
