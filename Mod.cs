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
		ctx.Patch("Map: remembered filters", MapPatches.RegisterFilters);
		ctx.Patch("Map: click to travel", MapPatches.RegisterTravel);
		ctx.Patch("Options menu: save/quit moved to bottom", OptionsMenuPatch.Register);
		ctx.Patch("Game speed", GameSpeed.Register);
		ctx.Patch("Headmaster: hide full Library", HeadmasterPatches.Register);
		ctx.Patch("Towns: pig farm in every world", PigFarm.Register);

		// QoL windows count as open menus for every mod (and for each other).
		Ui.AddModal(() => AcademyDirectory.IsOpen);
		Ui.AddModal(() => QoLHub.IsOpen);
		Ui.AddModal(() => FallenHeroes.IsOpen);

		QoLCapture.Register();
		ctx.Patch("Pool sense", PoolSense.Register);
		ctx.Patch("Equip best armor: button under the bag", AutoEquip.Register);
		ctx.Patch("Fullscreen alt-tab fix", FullscreenFix.Register);
		ctx.Patch("Borderless fullscreen", BorderlessFullscreen.Register);
		FavoriteBird.Register();
		OverworldHud.Register();

		Events.Input += FallenHeroes.HandleInput;
		Events.Input += g => !AcademyDirectory.InputPrefix(g);
		Events.Input += ClickTravel.HandleInput;
		Events.Input += AutoEquip.HandleInput;
		Events.DrawOverlay += DrawOverlay;
	}

	private static void DrawOverlay(dmGame g)
	{
		GameSpeed.SyncOption(g);
		BorderlessFullscreen.SyncLabel(g);
		AcademyDirectory.DrawOverlay(g);
		if (g.dungeonMans != null && !g.bShowMainMenu)
		{
			FloorHighlight.Draw(g);
			PoolSense.Draw(g);
			FallenHeroes.Draw(g);
			OverworldHud.Draw(g);
			// Top bar buttons are added to ModKit's shared TopBar, which lays them out after every mod has drawn.
			QoLHub.DrawButton(g);
			GameSpeed.Draw(g);

			QoLHub.Draw(g);
			AutoEquip.DrawInventoryButton(g);
		}
	}
}

internal static class Mod
{
	public static HarmonyMethod Hook(Type type, string name) => Patching.Hook(type, name);

	public static MethodInfo Target(Type type, string name) => Patching.Method(type, name);
}
