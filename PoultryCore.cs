using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMans;
using HarmonyLib;

namespace DungeonmansQoL;

// Game-side logic for the Coop: breeding bug fix, Pantry storage, Feathers, and bird operations
// (feed / ascend / breed / prune / release / store / take / equip / heal). The UI lives in CoopPanel.
// Persistent data (Pantry, Feathers, Pantry size) is stored in the game's own save, as "overworld" map
// script vars, like vanilla's pet flags; they survive hero deaths and "new overworld, keep Academy".
internal static class PoultryCore
{
	public const string Passionate = "passionate_poultry_poultice";
	public const string Pruning = "power_pruning_poultice";
	private static readonly string[] PantryArchetypes = { Passionate, Pruning, "poultry_protection_potion", "bird_rename_card" };

	// ---- tunables ----
	public const int StartSlots = 10;
	public const int SlotsPerPurchase = 3;
	public const int SlotPriceStep = 25;          // upgrade k costs 25 * k Feathers (25, 50, 75...)
	public const int PoulticeFeatherPrice = 50; // Passionate Poultry Poultice only
	public const int PoulticeSellFeathers = 5;
	// Flat: generation/Pluck-based rewards would let one strong bird mint Feathers through endless breeding.
	public const int ReleaseFeatherReward = 5;
	public static int SlotPrice => SlotPriceStep * ((PantrySlots - StartSlots) / SlotsPerPurchase + 1);
	public static int ReleaseFeathers(dmBirdEgg egg) => ReleaseFeatherReward;
	public static int RenderFeathers(Food f) => f.IsPlumage ? 10 : (int)Math.Pow(5, f.Tier);

	private static dmGame Game => dmUtilities.theGame;
	private static dmHero Hero => Game.dungeonMans;

	// =====================================================================
	// Breeding bug fix: CombineTwoBirds works directly on the first parent's gene table.
	// =====================================================================
	public static void Register(Harmony h)
	{
		h.Patch(Mod.Target(typeof(dmPetBird), nameof(dmPetBird.CombineTwoBirds)),
			prefix: Mod.Hook(typeof(PoultryCore), nameof(CombinePrefix)),
			postfix: Mod.Hook(typeof(PoultryCore), nameof(CombinePostfix)));
	}

	private static void CombinePrefix(dmBirdEgg birdA, out Dictionary<string, int> __state)
	{
		__state = Config.On(Config.BreedingFix) ? new Dictionary<string, int>(birdA.GetAllGenes()) : null;
	}

	private static void CombinePostfix(dmBirdEgg birdA, Dictionary<string, int> __state)
	{
		if (__state == null)
		{
			return;
		}
		Dictionary<string, int> genes = birdA.GetAllGenes();
		genes.Clear();
		foreach (var kv in __state)
		{
			genes[kv.Key] = kv.Value;
		}
	}

	// =====================================================================
	// Feathers + Pantry (persistent)
	// =====================================================================
	private const string MapName = "overworld";

	public static int Feathers
	{
		get => Math.Max(0, dmGazetteer.GetMapScriptVar_Int("qol_feathers", MapName));
		set => dmGazetteer.SetMapScriptVar_Int("qol_feathers", Math.Max(0, value), MapName);
	}

	public static int PantrySlots
	{
		get
		{
			int v = dmGazetteer.GetMapScriptVar_Int("qol_pantry_slots", MapName);
			return v <= 0 ? StartSlots : v;
		}
		set => dmGazetteer.SetMapScriptVar_Int("qol_pantry_slots", value, MapName);
	}

	// Pantry: key -> count. Keys:  s|<monster>|<tier>|<gene>   p|<power>   i|<archetype>
	public static List<KeyValuePair<string, int>> Pantry
	{
		get
		{
			var list = new List<KeyValuePair<string, int>>();
			string raw = dmGazetteer.GetMapScriptVar_String("qol_pantry", MapName);
			if (string.IsNullOrEmpty(raw))
			{
				return list;
			}
			foreach (string part in raw.Split(';'))
			{
				int eq = part.LastIndexOf('=');
				if (eq > 0 && int.TryParse(part.Substring(eq + 1), out int n) && n > 0)
				{
					list.Add(new KeyValuePair<string, int>(part.Substring(0, eq), n));
				}
			}
			return list;
		}
	}

	private static void SavePantry(List<KeyValuePair<string, int>> list)
	{
		dmGazetteer.SetMapScriptVar_String("qol_pantry",
			string.Join(";", list.Where(kv => kv.Value > 0).Select(kv => kv.Key + "=" + kv.Value)), MapName);
	}

	public static int PantryCount(string key) => Pantry.Where(kv => kv.Key == key).Sum(kv => kv.Value);
	public static int UsedSlots => Pantry.Count;

	private static bool PantryAdd(string key, int n)
	{
		var list = Pantry;
		int i = list.FindIndex(kv => kv.Key == key);
		if (i < 0)
		{
			if (list.Count >= PantrySlots)
			{
				return false;
			}
			list.Add(new KeyValuePair<string, int>(key, n));
		}
		else
		{
			list[i] = new KeyValuePair<string, int>(key, list[i].Value + n);
		}
		SavePantry(list);
		return true;
	}

	private static bool PantryTake(string key, int n)
	{
		var list = Pantry;
		int i = list.FindIndex(kv => kv.Key == key);
		if (i < 0 || list[i].Value < n)
		{
			return false;
		}
		list[i] = new KeyValuePair<string, int>(key, list[i].Value - n);
		SavePantry(list);
		return true;
	}

	// =====================================================================
	// Food: a steak / feather, from the bags or from the Pantry
	// =====================================================================
	private static readonly FieldInfo SteakMonster = AccessTools.Field(typeof(dmEnemyBirdSteak), "archetypeThatIAmAChunkOf");
	private static readonly FieldInfo SteakLevel = AccessTools.Field(typeof(dmEnemyBirdSteak), "level");
	private static readonly FieldInfo SteakTier = AccessTools.Field(typeof(dmEnemyBirdSteak), "champTier");
	private static readonly FieldInfo SteakGene = AccessTools.Field(typeof(dmEnemyBirdSteak), "powerGene");
	private static readonly MethodInfo SteakSetName = AccessTools.Method(typeof(dmEnemyBirdSteak), "SetMeatNameAndText");

	public sealed class Food
	{
		public string Key;          // pantry key
		public string Name;
		public int Tvs, Mvr, Tier;  // tough(-)/swift(+), ranged(-)/melee(+), pluck gained
		public string Gene;
		public bool IsPlumage;
		public dmEnemyBirdSteak BagItem; // set when the food is in the hero's bags
		public int Count;
		// Worth feeding on purpose: raises Pluck (rare steaks) or teaches a power for sure (Plumage).
		public bool Useful => Tier > 0 || IsPlumage;
	}

	public static Food FoodFromItem(dmEnemyBirdSteak s)
	{
		bool plume = s is dmPlumageOfPower;
		string gene = s.GetPowerGene();
		string key = plume ? "p|" + gene : "s|" + SteakMonster.GetValue(s) + "|" + SteakTier.GetValue(s) + "|" + (gene ?? "none");
		return new Food
		{
			Key = key, Name = s.GetName(), IsPlumage = plume, Gene = gene, BagItem = s, Count = 1,
			Tvs = s.GetAdjustmentForBirdStat(EBirdStatAxis.tough_vs_swift),
			Mvr = s.GetAdjustmentForBirdStat(EBirdStatAxis.ranged_vs_melee),
			Tier = s.GetChampionPoints(),
		};
	}

	public static Food FoodFromKey(string key, int count)
	{
		string[] p = key.Split('|');
		if (p[0] == "p")
		{
			return new Food { Key = key, IsPlumage = true, Gene = p[1], Count = count, Name = "Plumage of Power: " + PowerName(p[1]) };
		}
		if (p[0] != "s" || !dmPoultryGuide.BirdSteakMenu.TryGetValue(p[1], out SteakDataStruct data))
		{
			return null;
		}
		int tier = int.Parse(p[2]);
		string monster = Game.GetActorArchetype(p[1])?.Name ?? p[1];
		string prefix = tier == 1 ? "Rare " : tier == 2 ? "Aged Rare " : tier >= 3 ? "Hella Aged Rare " : "";
		return new Food
		{
			Key = key, Name = prefix + monster + " Steak", Gene = p[3], Tier = tier, Count = count,
			Tvs = data.toughOrSwift, Mvr = data.meleeOrRanged,
		};
	}

	public static List<Food> BagFoods() =>
		Hero.GetInventoryList().OfType<dmEnemyBirdSteak>().Where(s => !s.bEquipped).Select(FoodFromItem).ToList();

	public static List<Food> PantryFoods() =>
		Pantry.Where(kv => kv.Key.StartsWith("s|") || kv.Key.StartsWith("p|"))
			.Select(kv => FoodFromKey(kv.Key, kv.Value)).Where(f => f != null).ToList();

	public static string PowerName(string arch)
	{
		if (string.IsNullOrEmpty(arch) || arch == "none")
		{
			return "-";
		}
		try
		{
			return Game.ReadUniqueGenerationDataArchetype<dmSpecialPower>(arch)?.strPowerName ?? arch;
		}
		catch
		{
			return arch;
		}
	}

	public static int GeneChancePerGene(string gene) =>
		dmPoultryGuide.BirdGeneticsSpecialChanceData.TryGetValue(gene, out int v) ? v : 20;

	// Chance (0-100) that eating one more of this gene teaches it, mirroring dmPetBird.AbsorbPowerGene.
	public static int LearnChance(BirdStatInfo info, string gene)
	{
		if (string.IsNullOrEmpty(gene) || gene == "none" || info.sealedPowers.Contains(gene) || info.powersUnlockedArchetypes.Contains(gene)
			|| info.powersUnlockedArchetypes.Count >= 4)
		{
			return 0;
		}
		info.powerGenesCollected.TryGetValue(gene, out int g);
		return Math.Min(100, (g + 1) * GeneChancePerGene(gene));
	}

	// =====================================================================
	// Birds
	// =====================================================================
	private static readonly FieldInfo PlacedBird = AccessTools.Field(typeof(dmBirdEgg), "birdPlacedInWorld");

	public static dmPetBird Placed(dmBirdEgg egg) => PlacedBird.GetValue(egg) as dmPetBird;

	public static List<dmBirdEgg> BagBirds() => Hero.GetInventoryList().OfType<dmBirdEgg>().ToList();

	public static List<dmBirdEgg> AllBirds() => BagBirds().Concat(dmGazetteer.eggsStoredAtAcademy).Distinct().ToList();

	public static bool InBag(dmBirdEgg e) => Hero.GetInventoryList().Contains(e);
	public static bool IsEquipped(dmBirdEgg e) => Hero.GetItemForSlot(ESlot.ES_BirdEggPocket) == e;

	public static NPCAcademyHatch Hatch => Game.allActors.OfType<NPCAcademyHatch>().FirstOrDefault();

	private static void RefreshAcademy()
	{
		Hatch?.UpdateWanderingPoultryList(Game.theCurrentLevel as dmDungeonLevel);
	}

	private static void Resync(dmBirdEgg egg)
	{
		dmPetBird placed = Placed(egg);
		if (placed != null)
		{
			egg.FillBirdStatsFromInfo(placed);
		}
	}

	public static string Store(dmBirdEgg egg)
	{
		if (!InBag(egg))
		{
			return null;
		}
		if (IsEquipped(egg))
		{
			Hero.RemoveItemFromSlot(ESlot.ES_BirdEggPocket, Game);
		}
		Hero.RemoveItemFromInventory(egg, bAndDestroy: false);
		dmGazetteer.eggsStoredAtAcademy.Add(egg);
		RefreshAcademy();
		return egg.BirdName + " now lives at the Academy.";
	}

	public static string Take(dmBirdEgg egg)
	{
		if (!dmGazetteer.eggsStoredAtAcademy.Contains(egg))
		{
			return null;
		}
		dmGazetteer.eggsStoredAtAcademy.Remove(egg);
		Hero.GiveItemToCharacter(egg);
		RefreshAcademy();
		return egg.BirdName + " joins your bags.";
	}

	public static string Equip(dmBirdEgg egg)
	{
		if (!InBag(egg))
		{
			Take(egg);
		}
		bool ok = Hero.EquipItemInSlot(egg, ESlot.ES_BirdEggPocket);
		return ok ? egg.BirdName + " is equipped. Blow the Battlebird Whistle to call it." : "Could not equip " + egg.BirdName + ".";
	}

	public static string HealAll()
	{
		int n = 0;
		foreach (dmBirdEgg e in AllBirds().Where(e => e.GetStatInfo().bWounded))
		{
			e.GetStatInfo().bWounded = false;
			n++;
		}
		return n == 0 ? "No wounded birds." : "Healed " + n + " wounded bird" + (n > 1 ? "s" : "") + ".";
	}

	// ---- poultices: Pantry first, then bags ----
	public static int Count(string archetype) => PantryCount("i|" + archetype) + Hero.GetNumberOfThisItem(archetype);

	private static bool Consume(string archetype, int n)
	{
		if (Count(archetype) < n)
		{
			return false;
		}
		int fromPantry = Math.Min(n, PantryCount("i|" + archetype));
		if (fromPantry > 0)
		{
			PantryTake("i|" + archetype, fromPantry);
		}
		for (int i = fromPantry; i < n; i++)
		{
			Hero.RemoveItemFromInventory(archetype, bAndDestroy: true);
		}
		return true;
	}

	// ---- feeding (works on birds at the Academy too) ----
	public static string Feed(dmBirdEgg egg, Food food)
	{
		dmEnemyBirdSteak item = food.BagItem;
		if (item == null)
		{
			item = SpawnFood(food.Key);
			if (item == null || !PantryTake(food.Key, 1))
			{
				return "That food is gone.";
			}
		}
		else
		{
			Hero.RemoveItemFromInventory(item, bAndDestroy: true);
		}
		BirdStatInfo before = Snapshot(egg.GetStatInfo());
		dmPetBird placed = Placed(egg);
		if (placed != null)
		{
			placed.FeedBird(item);
		}
		else
		{
			ApplyFood(egg.GetStatInfo(), item);
		}
		dmAudio.PlayFX("poultry_excite");
		BirdStatInfo after = egg.GetStatInfo();
		string learned = after.powersUnlockedArchetypes.Except(before.powersUnlockedArchetypes).Select(PowerName).FirstOrDefault();
		return egg.BirdName + " ate " + food.Name + "." + (learned != null ? " ^3Learned " + learned + "!^7" : "");
	}

	private static BirdStatInfo Snapshot(BirdStatInfo info)
	{
		var copy = new BirdStatInfo();
		copy.CopyFrom(info);
		return copy;
	}

	// Mirror of dmPetBird.FeedBird + AbsorbPowerGene for a bird that isn't out in the world.
	private static void ApplyFood(BirdStatInfo info, IBirdFood food)
	{
		info.championPointsEarned += food.GetChampionPoints();
		for (int a = 0; a < 2; a++)
		{
			info.birdStats[a] = Math.Max(-5, Math.Min(5, info.birdStats[a] + food.GetAdjustmentForBirdStat((EBirdStatAxis)a)));
		}
		string gene = food.GetPowerGene();
		if (string.IsNullOrEmpty(gene) || info.sealedPowers.Contains(gene))
		{
			return;
		}
		info.powerGenesCollected.TryGetValue(gene, out int g);
		g++;
		info.powerGenesCollected[gene] = g;
		if (info.powersUnlockedArchetypes.Count < 4 && !info.powersUnlockedArchetypes.Contains(gene) && gene != "none"
			&& dmUtilities.RollDice("1d100") <= g * GeneChancePerGene(gene))
		{
			info.powersUnlockedArchetypes.Add(gene);
		}
	}

	private static dmEnemyBirdSteak SpawnFood(string key)
	{
		string[] p = key.Split('|');
		if (p[0] == "p")
		{
			var plume = Game.SpawnItemFromArchetype("plumage_of_power") as dmPlumageOfPower;
			plume?.SetPowerGene(p[1]);
			return plume;
		}
		if (p[0] != "s")
		{
			return null;
		}
		var steak = Game.SpawnItemFromArchetype("enemy_bird_steak") as dmEnemyBirdSteak;
		if (steak == null)
		{
			return null;
		}
		SteakMonster.SetValue(steak, p[1]);
		SteakLevel.SetValue(steak, Hero.iLevel);
		SteakTier.SetValue(steak, int.Parse(p[2]));
		SteakSetName.Invoke(steak, null);
		SteakGene.SetValue(steak, p[3]); // keep the gene this steak rolled when it dropped
		return steak;
	}

	// ---- prune ----
	public static string Prune(dmBirdEgg egg, string power)
	{
		if (!Consume(Pruning, 1))
		{
			return "You need a Power Pruning Poultice.";
		}
		egg.SealPower(power);
		dmAudio.PlayFX("poof_disappear");
		return egg.BirdName + " forgot " + PowerName(power) + " for good.";
	}

	// ---- ascend: +1 generation for N Passionate Poultices, Pluck -= Pluck * 0.5^N ----
	public static int PluckAfterAscend(int pluck, int n) => pluck - (int)Math.Floor(pluck * Math.Pow(0.5, n));

	public static string Ascend(dmBirdEgg egg, int n)
	{
		if (n < 1 || !Consume(Passionate, n))
		{
			return "Not enough Passionate Poultry Poultices.";
		}
		BirdStatInfo info = egg.GetStatInfo();
		int before = info.championPointsEarned;
		info.generation = Math.Max(1, info.generation) + 1;
		info.championPointsEarned = PluckAfterAscend(before, n);
		Resync(egg);
		dmAudio.PlayFX("poultry_excite");
		return egg.BirdName + " ascends to Generation " + info.generation + " (Pluck " + before + " -> " + info.championPointsEarned + ").";
	}

	// ---- breed ----
	public sealed class BreedPreview
	{
		public int Generation, Pluck, Tvs, Mvr;
		public List<(string gene, int count, double chance)> Powers = new List<(string, int, double)>();
	}

	// Exact mirror of CombineTwoBirds' deterministic part, plus the probability of each power being rolled.
	public static BreedPreview Preview(dmBirdEgg a, dmBirdEgg b)
	{
		var p = new BreedPreview
		{
			Generation = Math.Max(a.GetGeneration(), b.GetGeneration()) + (a.GetGeneration() == b.GetGeneration() ? 1 : 0),
			Pluck = (a.GetChampionPoints() + b.GetChampionPoints()) / 2,
			Tvs = (a.GetStatAxis().X + b.GetStatAxis().X) / 2,
			Mvr = (a.GetStatAxis().Y + b.GetStatAxis().Y) / 2,
		};
		var genes = new Dictionary<string, int>(a.GetAllGenes());
		foreach (var kv in b.GetAllGenes())
		{
			genes[kv.Key] = genes.TryGetValue(kv.Key, out int v) ? v + kv.Value : kv.Value;
		}
		genes.Remove("none");
		var ordered = genes.Select(kv => (gene: kv.Key, count: Math.Max(kv.Value / 2, 1))).OrderByDescending(g => g.count).ToList();
		// dist[k] = probability that exactly k powers were picked so far (max 4)
		double[] dist = { 1, 0, 0, 0, 0 };
		foreach (var (gene, count) in ordered)
		{
			double roll = Math.Max(0, Math.Min(99, count * 20 - 1)) / 100.0; // RollDice("1d100") < count*20
			double chance = (1 - dist[4]) * roll;
			p.Powers.Add((gene, count, chance));
			for (int k = 4; k >= 1; k--)
			{
				dist[k] = dist[k] + dist[k - 1] * roll - (k < 4 ? dist[k] * roll : 0);
			}
			dist[0] *= 1 - roll;
		}
		return p;
	}

	public static string Breed(dmBirdEgg a, dmBirdEgg b)
	{
		if (!Consume(Passionate, 1))
		{
			return "You need a Passionate Poultry Poultice.";
		}
		dmBirdEgg egg = dmPetBird.CombineTwoBirds(a, b);
		Hero.GiveItemToCharacter(egg);
		dmAudio.PlayFX("poultry_excite");
		return "A new egg from " + a.BirdName + " and " + b.BirdName + " is in your bags (Generation " + egg.GetGeneration() + "). Equip it and blow the whistle to hatch it.";
	}

	// ---- release: vanilla release (Plumage of Power drops) + Feathers ----
	private static readonly MethodInfo HatchRelease = AccessTools.Method(typeof(NPCAcademyHatch), "OnTrulySetBirdFreeForever");

	public static string Release(dmBirdEgg egg)
	{
		NPCAcademyHatch hatch = Hatch;
		if (hatch == null)
		{
			return "Professor Hatch must be nearby to set a bird free.";
		}
		int feathers = ReleaseFeathers(egg);
		if (InBag(egg) && !IsEquipped(egg))
		{
			Hero.RemoveItemFromInventory(egg, bAndDestroy: false);
		}
		hatch.SetScriptVar_Object_WillNotSerialize("set_free_target", egg);
		HatchRelease.Invoke(hatch, new object[] { 0, null });
		Feathers += feathers;
		return egg.BirdName + " flies free. +" + feathers + " Feathers (and any Plumage of Power it dropped).";
	}

	// =====================================================================
	// Pantry transfers + shop
	// =====================================================================
	public static bool IsPantryItem(dmItem item) =>
		item is dmEnemyBirdSteak || PantryArchetypes.Contains(item.ArchetypeName);

	private static string KeyOf(dmItem item) => item is dmEnemyBirdSteak s ? FoodFromItem(s).Key : "i|" + item.ArchetypeName;

	private static int StackSize(dmItem item) => item.IsBulkItem() ? Math.Max(1, item.GetBulkCount()) : 1;

	public static string Deposit(dmItem item)
	{
		int n = StackSize(item);
		if (!PantryAdd(KeyOf(item), n))
		{
			return "The Pantry is full. Buy more space with Feathers.";
		}
		Hero.RemoveItemFromInventory(item, bAndDestroy: true, n);
		return "Stored " + item.GetName() + (n > 1 ? " x" + n : "") + ".";
	}

	public static string DepositAll()
	{
		int moved = 0;
		bool full = false;
		foreach (dmItem item in Hero.GetInventoryList().Where(i => IsPantryItem(i) && !i.bEquipped && !i.InventoryLocked).ToList())
		{
			int n = StackSize(item);
			if (PantryAdd(KeyOf(item), n))
			{
				Hero.RemoveItemFromInventory(item, bAndDestroy: true, n);
				moved += n;
			}
			else
			{
				full = true;
			}
		}
		return "Stored " + moved + " item" + (moved == 1 ? "" : "s") + "." + (full ? " ^1Pantry full^7, some items stayed in your bags." : "");
	}

	public static string Withdraw(string key, int n)
	{
		int have = PantryCount(key);
		n = Math.Min(n, have);
		if (n <= 0)
		{
			return null;
		}
		for (int i = 0; i < n; i++)
		{
			dmItem item = key.StartsWith("i|") ? Game.SpawnItemFromArchetype(key.Substring(2)) : SpawnFood(key);
			if (item == null)
			{
				return "Could not rebuild " + key + ".";
			}
			item.bIsIdentified = true;
			Hero.GiveItemToCharacter(item);
		}
		PantryTake(key, n);
		return "Took " + n + " from the Pantry.";
	}

	public static string Render(string key)
	{
		Food f = FoodFromKey(key, 1);
		if (f == null || !PantryTake(key, 1))
		{
			return null;
		}
		int gain = RenderFeathers(f);
		Feathers += gain;
		return "Rendered " + f.Name + " into " + gain + " Feathers.";
	}

	public static string BuySlots()
	{
		int price = SlotPrice;
		if (Feathers < price)
		{
			return "Need " + price + " Feathers.";
		}
		Feathers -= price;
		PantrySlots += SlotsPerPurchase;
		return "The Pantry now holds " + PantrySlots + " stacks.";
	}

	public static string BuyPoultice(string archetype)
	{
		if (Feathers < PoulticeFeatherPrice)
		{
			return "Need " + PoulticeFeatherPrice + " Feathers.";
		}
		if (!PantryAdd("i|" + archetype, 1))
		{
			return "The Pantry is full.";
		}
		Feathers -= PoulticeFeatherPrice;
		return "Bought a " + (archetype == Passionate ? "Passionate" : "Power Pruning") + " Poultry Poultice (stored in the Pantry).";
	}

	public static string SellPoultice()
	{
		if (!Consume(Passionate, 1))
		{
			return "No Passionate Poultry Poultice to sell.";
		}
		Feathers += PoulticeSellFeathers;
		return "Sold a Passionate Poultry Poultice for " + PoulticeSellFeathers + " Feathers.";
	}

	public static string ItemName(string key)
	{
		if (key.StartsWith("i|"))
		{
			switch (key.Substring(2))
			{
			case Passionate: return "Passionate Poultry Poultice";
			case Pruning: return "Power Pruning Poultice";
			case "poultry_protection_potion": return "Poultry Protection Potion";
			case "bird_rename_card": return "Bird Rename Card";
			default: return key.Substring(2);
			}
		}
		return FoodFromKey(key, 1)?.Name ?? key;
	}

	// =====================================================================
	// Favorite bird (auto taken + equipped by Prepare hero)
	// =====================================================================
	public static string FavoriteName
	{
		get => dmGazetteer.GetMapScriptVar_String("qol_favorite_bird", MapName);
		set => dmGazetteer.SetMapScriptVar_String("qol_favorite_bird", value ?? "", MapName);
	}

	public static dmBirdEgg Favorite => AllBirds().FirstOrDefault(e => !string.IsNullOrEmpty(FavoriteName) && e.BirdName == FavoriteName);
}
