using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DungeonMans;
using HarmonyLib;

namespace DungeonmansQoL;

// Overworld map (M / Tab):
//  - The filter buttons (Towns, Cleared, In Progress...) are remembered between sessions. The game resets them
//    all to "on" at every launch; with this mod, cleared dungeons start hidden.
//  - Click a location icon (or press Enter on the selected one) to auto-walk there. This is the game's own
//    auto-path, so travel takes the same turns and encounters as walking; it isn't a teleport.
internal static class MapPatches
{
	private static readonly string FilterPath = Path.Combine(
		Path.GetDirectoryName(typeof(MapPatches).Assembly.Location) ?? ".", "map_filters.json");

	private static readonly string[] FilterFields =
	{
		"bShowAll", "bShowTowns", "bShowClearedDungeons", "bShowUnexplored", "bShowInProgressDungeons",
		"bShowUnavenged", "bShowRoadblocks", "bShowTrivial", "bShowOnlyAMaps",
	};

	private static readonly AccessTools.FieldRef<dmOverworldMapDisplay, List<GazetteerLocationEntry>> DisplayedLocations =
		AccessTools.FieldRefAccess<dmOverworldMapDisplay, List<GazetteerLocationEntry>>("listDisplayedLocations");

	private static readonly AccessTools.FieldRef<dmOverworldMapDisplay, string> SelectedLocation =
		AccessTools.FieldRefAccess<dmOverworldMapDisplay, string>("selectedMapLocationIdx");

	private static readonly AccessTools.FieldRef<dmUIMouseFrameManager, ui_mouseFrame[]> MouseFrames =
		AccessTools.FieldRefAccess<dmUIMouseFrameManager, ui_mouseFrame[]>("mouseFrames");

	public static void RegisterFilters(Harmony h)
	{
		h.Patch(Mod.Target(typeof(dmOverworldMapDisplay), nameof(dmOverworldMapDisplay.Init)),
			postfix: Mod.Hook(typeof(MapPatches), nameof(InitPostfix)));
		h.Patch(Mod.Target(typeof(dmOverworldMapDisplay), nameof(dmOverworldMapDisplay.OptionButtonClicked)),
			postfix: Mod.Hook(typeof(MapPatches), nameof(SaveFilters)));
	}

	public static void RegisterTravel(Harmony h)
	{
		h.Patch(Mod.Target(typeof(dmOverworldMapDisplay), "AddUIForEachLocation"),
			postfix: Mod.Hook(typeof(MapPatches), nameof(AddUIPostfix)));
		h.Patch(Mod.Target(typeof(dmOverworldMapDisplay), nameof(dmOverworldMapDisplay.HandleInput)),
			prefix: Mod.Hook(typeof(MapPatches), nameof(HandleInputPrefix)));
	}

	private static void InitPostfix(dmOverworldMapDisplay __instance)
	{
		if (!Config.On(Config.MapFilters))
		{
			return;
		}
		var map = Traverse.Create(__instance);
		try
		{
			if (File.Exists(FilterPath))
			{
				var saved = JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(FilterPath));
				foreach (string f in FilterFields)
				{
					if (saved != null && saved.TryGetValue(f, out bool v))
					{
						map.Field(f).SetValue(v);
					}
				}
				return;
			}
		}
		catch (Exception e)
		{
			Log.Write("Could not read " + FilterPath + ": " + e.Message);
		}
		// First run: vanilla "everything on", minus cleared dungeons.
		map.Field("bShowClearedDungeons").SetValue(false);
		map.Field("bShowAll").SetValue(false);
	}

	private static void SaveFilters(dmOverworldMapDisplay __instance)
	{
		if (!Config.On(Config.MapFilters))
		{
			return;
		}
		var map = Traverse.Create(__instance);
		try
		{
			File.WriteAllText(FilterPath, JsonSerializer.Serialize(FilterFields.ToDictionary(f => f, f => map.Field(f).GetValue<bool>())));
		}
		catch (Exception e)
		{
			Log.Write("Could not write " + FilterPath + ": " + e.Message);
		}
	}

	// The vanilla location frames only have mouse-over handlers; add a click handler to each.
	private static void AddUIPostfix()
	{
		if (!Config.On(Config.MapTravel))
		{
			return;
		}
		foreach (ui_mouseFrame f in MouseFrames(dmUtilities.theGame.theUIFrames))
		{
			if (f != null && f.IsActive && f.strFrameID != null && f.strFrameID.StartsWith("overworld_map_location_button_"))
			{
				f.funcOnClick = ClickLocation;
			}
		}
	}

	private static object ClickLocation(ui_mouseFrame frame, int iButton)
	{
		WalkTo(dmUtilities.theGame.theOverworldMap, frame.tagStringData);
		return true;
	}

	private static bool HandleInputPrefix(dmOverworldMapDisplay __instance)
	{
		List<ActionButton> a = dmUtilities.theGame.theInput.ExecutedActions;
		if (Config.On(Config.MapTravel) && a.Contains(ActionButton.Enter) || a.Contains(ActionButton.Select))
		{
			WalkTo(__instance, SelectedLocation(__instance));
			return false;
		}
		return true;
	}

	private static void WalkTo(dmOverworldMapDisplay map, string locationName)
	{
		dmGame g = dmUtilities.theGame;
		GazetteerLocationEntry entry = DisplayedLocations(map)?
			.FirstOrDefault(l => l.strLocationName.ToLowerInvariant() == locationName);
		if (entry == null || g.dungeonMans == null || !g.dungeonMans.bIsInOverworld)
		{
			return;
		}
		map.DeactivateMap();
		g.TurnOffAllDialogModes();
		g.dungeonMans.HeroAutoPathToLocation(entry.location, "Travelling to ^3" + entry.strLocationName + "^7.");
	}
}
