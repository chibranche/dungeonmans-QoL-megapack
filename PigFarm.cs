using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DungeonMans;
using HarmonyLib;

namespace DungeonmansQoL;

// A pig farm in every world. A town gets either an inn (9 in 10) or Pig Farmin' with the pig vendor (1 in 10),
// so a world can easily have none. With this on, the town closest to the Academy always builds the pig farm.
// Towns are generated the first time you walk in, so it applies to towns of a new or rerolled world (a town you
// have already visited keeps its layout).
internal static class PigFarm
{
	private static string building; // name of the town being generated

	public static void Register(Harmony h)
	{
		MethodInfo build = Mod.Target(typeof(dmTown), nameof(dmTown.BuildTownSeptember2014));
		h.Patch(build, prefix: Mod.Hook(typeof(PigFarm), nameof(BuildPrefix)), postfix: Mod.Hook(typeof(PigFarm), nameof(BuildPostfix)),
			transpiler: Mod.Hook(typeof(PigFarm), nameof(Transpiler)));
	}

	private static void BuildPrefix(dmTown __instance) => building = __instance.levelName;

	private static void BuildPostfix() => building = null;

	// Replace the inn-or-pig-farm roll, RollDice("1d10") > 1 = inn, with InnRoll (the first "1d10" in the method).
	private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		MethodInfo roll = AccessTools.Method(typeof(dmUtilities), nameof(dmUtilities.RollDice), new[] { typeof(string), typeof(Random) });
		MethodInfo ours = AccessTools.Method(typeof(PigFarm), nameof(InnRoll));
		List<CodeInstruction> code = instructions.ToList();
		for (int i = 0; i < code.Count; i++)
		{
			if (code[i].opcode == OpCodes.Ldstr && (code[i].operand as string) == "1d10")
			{
				for (int j = i + 1; j < Math.Min(code.Count, i + 4); j++)
				{
					if (code[j].Calls(roll))
					{
						code[j] = new CodeInstruction(OpCodes.Call, ours).MoveLabelsFrom(code[j]);
						return code;
					}
				}
			}
		}
		Log.Write("Pig farm: inn roll not found, town generation left as is");
		return code;
	}

	public static int InnRoll(string dice, Random seeded)
	{
		if (Config.On(Config.PigFarm) && building != null && building == PigTownName())
		{
			Log.Write("Pig farm: " + building + " (closest town to the Academy) gets the pig farm");
			return 1;
		}
		return dmUtilities.RollDice(dice, seeded);
	}

	// The regular town closest to the Academy on the overworld.
	private static string PigTownName()
	{
		GazetteerDungeonEntry academy = OverworldHud.AcademyEntry();
		List<GazetteerTownEntry> towns = dmGazetteer.locationDictionary.GetAllLocationsOfType<GazetteerTownEntry>()
			.Where(t => t.townArchetype == "town_generation_data" && !t.bCreatedByPlayerAndShouldPersistBetweenOverworldWipes).ToList();
		if (towns.Count == 0)
		{
			return null;
		}
		LocValue from = academy?.location ?? towns[0].location;
		return towns.OrderBy(t => t.location.FlatTileDistanceFrom(from)).ThenBy(t => t.id).First().strLocationName;
	}
}
