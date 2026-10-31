using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DungeonMans;
using HarmonyLib;

namespace DungeonmansQoL;

// Proof of Stremf presets. A preset stores how many points were added to each stat (the cyan numbers),
// used as a ratio: loading undoes pending changes, then spends every affordable Proof one point at a time,
// always on the stat furthest behind its share. So a preset keeps working as lifetime Proofs grow.
// Stored outside the save game (QoLMod\proof_presets.json) so it carries across heroes and worlds.
internal static class ProofPresets
{
	public const int SlotCount = 3;
	private const int StatCount = 4;

	private static readonly string PresetPath = Path.Combine(
		Path.GetDirectoryName(typeof(ProofPresets).Assembly.Location) ?? ".", "proof_presets.json");

	private static readonly AccessTools.FieldRef<ui_spendProofsOfStremfDialog, int> SelectionIdx =
		AccessTools.FieldRefAccess<ui_spendProofsOfStremfDialog, int>("iSelectionIdx");

	private static readonly FieldInfo StatChangesField = AccessTools.Field(typeof(ui_spendProofsOfStremfDialog), "statChanges");
	private static readonly Type StatChangeType = AccessTools.Inner(typeof(ui_spendProofsOfStremfDialog), "StatChangeValue");
	private static readonly FieldInfo StartingValue = AccessTools.Field(StatChangeType, "startingValue");
	private static readonly FieldInfo CurrentValue = AccessTools.Field(StatChangeType, "currentValue");

	private static int[][] slots;

	public static int[][] Slots => slots ??= LoadFile();

	public static bool IsEmpty(int slot) => Slots[slot].All(v => v <= 0);

	public static string Describe(int slot)
	{
		if (IsEmpty(slot))
		{
			return "(empty)";
		}
		return string.Join("  ", Enumerable.Range(0, StatCount)
			.Where(i => Slots[slot][i] > 0)
			.Select(i => dmUtilities.GetStatName((CharacterStat)i) + " ^5+" + Slots[slot][i] + "^7"));
	}

	public static bool Save(ui_spendProofsOfStremfDialog dlg, int slot)
	{
		Array changes = (Array)StatChangesField.GetValue(dlg);
		int[] deltas = new int[StatCount];
		for (int i = 0; i < StatCount; i++)
		{
			object sc = changes.GetValue(i);
			deltas[i] = (int)CurrentValue.GetValue(sc) - (int)StartingValue.GetValue(sc);
		}
		if (deltas.All(d => d <= 0))
		{
			return false;
		}
		Slots[slot] = deltas;
		SaveFile();
		return true;
	}

	public static bool Apply(ui_spendProofsOfStremfDialog dlg, int slot)
	{
		if (IsEmpty(slot))
		{
			return false;
		}
		int[] weights = Slots[slot];
		int savedSelection = SelectionIdx(dlg);

		for (int i = 0; i < StatCount; i++)
		{
			SelectionIdx(dlg) = i;
			while (dlg.TryDecrease(0))
			{
			}
		}

		int[] added = new int[StatCount];
		bool[] exhausted = new bool[StatCount];
		while (true)
		{
			int best = -1;
			double bestShare = double.MaxValue;
			for (int i = 0; i < StatCount; i++)
			{
				if (weights[i] <= 0 || exhausted[i])
				{
					continue;
				}
				double share = (added[i] + 1) / (double)weights[i];
				if (share < bestShare)
				{
					best = i;
					bestShare = share;
				}
			}
			if (best < 0)
			{
				break;
			}
			SelectionIdx(dlg) = best;
			if (dlg.TryIncrease(0))
			{
				added[best]++;
			}
			else
			{
				// Can't afford this one any more; cheaper stats may still take the remaining Proofs.
				exhausted[best] = true;
			}
		}

		SelectionIdx(dlg) = savedSelection;
		return true;
	}

	private static int[][] LoadFile()
	{
		int[][] result = Enumerable.Range(0, SlotCount).Select(_ => new int[StatCount]).ToArray();
		try
		{
			if (File.Exists(PresetPath))
			{
				int[][] read = JsonSerializer.Deserialize<int[][]>(File.ReadAllText(PresetPath));
				for (int s = 0; s < Math.Min(SlotCount, read?.Length ?? 0); s++)
				{
					if (read[s]?.Length == StatCount)
					{
						result[s] = read[s];
					}
				}
			}
		}
		catch (Exception e)
		{
			Log.Write("Could not read " + PresetPath + ": " + e.Message);
		}
		return result;
	}

	private static void SaveFile()
	{
		try
		{
			File.WriteAllText(PresetPath, JsonSerializer.Serialize(Slots));
		}
		catch (Exception e)
		{
			Log.Write("Could not write " + PresetPath + ": " + e.Message);
		}
	}
}
