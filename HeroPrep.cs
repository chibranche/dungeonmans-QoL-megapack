using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMans;
using HarmonyLib;

namespace DungeonmansQoL;

// "Prepare hero": does every new-hero Academy chore in one go, using the same game calls the NPC dialogs use:
//  - Libromancer / Chemistress: identify scrolls / potions up to the Library / Alchemy Lab rank (only before
//    the hero first leaves the Academy, like vanilla).
//  - Every ghost teacher you haven't learned from: their full mastery, their mastery point, or their hammer.
//  - Battlemaster: spends unspent Proofs with preset 1 (only if preset 1 is saved).
//  - Academy Warchest: tosses its rewards next to you.
//  - Grand Library bookstands (refilled for every new hero): takes the item from each one not yet looted.
// Museum withdrawals are left alone: they need you to choose items.
internal static class HeroPrep
{
	private static readonly MethodInfo TeacherLearned = Mod.Target(typeof(NPCAcademyGhostTeacher), "DidHeroLearnFromUs");
	private static readonly MethodInfo TeacherSetLearned = Mod.Target(typeof(NPCAcademyGhostTeacher), "SetHeroHasLearnedFromUs");
	private static readonly AccessTools.FieldRef<NPCAcademyLibrarian, bool> LibrarianDone =
		AccessTools.FieldRefAccess<NPCAcademyLibrarian, bool>("bAlreadyTrainedHero");

	private const string ScrollsVar = "qol_prep_scrolls";
	private const string PotionsVar = "qol_prep_potions";

	private static dmGame Game => dmUtilities.theGame;
	private static dmHero Hero => Game.dungeonMans;

	private static IEnumerable<T> Actors<T>() => Game.allActors.OfType<T>().ToList();

	private static int LibraryRank => dmGazetteer.academyProgress.GetLibraryRank();
	private static int AlchemyRank => dmGazetteer.academyProgress.GetUnlockCount(AcademyProgressData.AcademyUnlocksEnum.ALCHEMIST);

	private static bool CanScrolls => Hero.bNeverLeftAcademy && LibraryRank > 0 && Hero.GetScriptVar_String(ScrollsVar) != "true"
		&& Actors<NPCAcademyLibrarian>().Any();

	private static bool CanPotions => Hero.bNeverLeftAcademy && AlchemyRank > 0 && Hero.GetScriptVar_String(PotionsVar) != "true"
		&& Actors<NPCAcademyAlchemist>().Any();

	private static List<NPCAcademyGhostTeacher> PendingTeachers =>
		Actors<NPCAcademyGhostTeacher>().Where(t => !(bool)TeacherLearned.Invoke(t, null)).ToList();

	private static bool CanProofs => ProofPresets.Slots != null && !ProofPresets.IsEmpty(0)
		&& dmGazetteer.academyProgress.iUnspentProofsOfStremf > 0
		&& dmGazetteer.academyProgress.GetUnlockCount(AcademyProgressData.AcademyUnlocksEnum.PROOF_OF_STREMF) > 0
		&& dmPay2Lose.GetModifier(EPay2LoseModifiers.academy_no_battlemaster) == 0
		&& Actors<NPCAcademyBattlemaster>().Any();

	// The 4 Grand Library bookstands are containers with a bookstand id (see dmAcademy.BuildGrandLibraryBookArea).
	public static List<dmContainer> UnlootedBookstands =>
		Actors<dmContainer>().Where(c => c.iBookstandID != 0 && !c.bExhausted).ToList();

	// Same as bumping each bookstand: the item goes straight into the hero's bags.
	public static int LootBookstands()
	{
		int n = 0;
		foreach (dmContainer stand in UnlootedBookstands)
		{
			if (stand.UsedByActor(Hero, Game))
			{
				n++;
			}
		}
		return n;
	}

	private static int WarchestCount => Actors<dmAcademyWarchest>().Any() ? dmGazetteer.itemsInAwardChest?.Count ?? 0 : 0;

	// What the button would do right now (shown in the Directory's help strip). Asked every frame by the Directory and
	// the QoL hub, so it is recomputed a few times a second.
	private static readonly Throttled<List<string>> plan = new Throttled<List<string>>(ComputePlan);

	public static List<string> Plan() => plan.Value;

	private static List<string> ComputePlan()
	{
		var plan = new List<string>();
		if (CanScrolls)
		{
			plan.Add("identify scrolls (Library rank " + LibraryRank + ")");
		}
		if (CanPotions)
		{
			plan.Add("identify potions (Alchemy rank " + AlchemyRank + ")");
		}
		int teachers = PendingTeachers.Count;
		if (teachers > 0)
		{
			plan.Add("learn from " + teachers + " teacher" + (teachers > 1 ? "s" : ""));
		}
		if (CanProofs)
		{
			plan.Add("spend " + dmGazetteer.academyProgress.iUnspentProofsOfStremf + " Proofs with preset 1");
		}
		dmBirdEgg fav = FavoriteBird.ToEquip();
		if (fav != null)
		{
			plan.Add("equip your favorite bird " + fav.BirdName);
		}
		int stands = UnlootedBookstands.Count;
		if (stands > 0)
		{
			plan.Add("take the book from " + stands + " Grand Library bookstand" + (stands > 1 ? "s" : ""));
		}
		int chest = WarchestCount;
		if (chest > 0)
		{
			plan.Add("collect " + chest + " Warchest reward" + (chest > 1 ? "s" : ""));
		}
		return plan;
	}

	public static void Run()
	{
		plan.Reset();
		dmGame g = Game;
		dmHero hero = Hero;
		var done = new List<string>();

		Step(done, () =>
		{
			if (!CanScrolls)
			{
				return null;
			}
			dmGazetteer.IdentifyRankOfConsumable("scroll", LibraryRank);
			hero.SetScriptVar_String(ScrollsVar, "true");
			foreach (NPCAcademyLibrarian lib in Actors<NPCAcademyLibrarian>())
			{
				LibrarianDone(lib) = true;
			}
			dmAudio.PlayFX("academy_identify_scrolls");
			return "Libromancer identified scrolls up to rank " + LibraryRank + ".";
		});

		Step(done, () =>
		{
			if (!CanPotions)
			{
				return null;
			}
			dmGazetteer.IdentifyRankOfConsumable("potion", AlchemyRank);
			hero.SetScriptVar_String(PotionsVar, "true");
			dmAudio.PlayFX("academy_identify_potions");
			return "Chemistress identified potions up to rank " + AlchemyRank + ".";
		});

		foreach (NPCAcademyGhostTeacher teacher in PendingTeachers)
		{
			Step(done, () => Teach(g, hero, teacher));
		}

		Step(done, () =>
		{
			if (!CanProofs)
			{
				return null;
			}
			int before = dmGazetteer.academyProgress.iUnspentProofsOfStremf;
			ui_spendProofsOfStremfDialog dlg = g.spendProofsDialog;
			dlg.FillWithHeroStats(hero);
			ProofPresets.Apply(dlg, 0);
			dlg.ConfirmProofOfStremfSpending();
			int spent = before - dmGazetteer.academyProgress.iUnspentProofsOfStremf;
			return "Battlemaster: spent " + spent + " Proofs of Stremf with preset 1 (" + ProofPresets.Describe(0) + ").";
		});

		Step(done, () =>
		{
			if (WarchestCount == 0)
			{
				return null;
			}
			float delay = 0f;
			int n = dmGazetteer.itemsInAwardChest.Count;
			foreach (dmItem item in dmGazetteer.itemsInAwardChest)
			{
				WorldTile tile = dmUtilities.GetSuitableNearbyTileForLootToss(hero.Location);
				g.PrepareDelayedTreasureEvent(item, hero.Location, tile.Location, delay);
				delay += 0.1f;
			}
			dmGazetteer.itemsInAwardChest.Clear();
			return "Warchest: " + n + " reward" + (n > 1 ? "s" : "") + " tossed next to you.";
		});

		Step(done, () =>
		{
			int n = LootBookstands();
			return n == 0 ? null : "Grand Library: took the book from " + n + " bookstand" + (n > 1 ? "s" : "") + ".";
		});

		Step(done, FavoriteBird.Equip);

		string text = done.Count == 0
			? "Nothing left to prepare for this hero."
			: "^3Hero prepared!^7 ^n^n" + string.Join(" ^n", done.Select(d => "* " + d));
		if (Actors<NPCAcademyCurator>().Any())
		{
			text += " ^n^nNot automated: Museum withdrawals (see the Curator) need you to pick items.";
		}
		hero.dialogReason = HeroDialogReason.HERODR_JustADialog;
		g.SpawnDialog(new dmDialogBox(hero, text) { iNumResponses = 1, dialogResponses = { [0] = "Off to crush monsters!" } });
	}

	private static void Step(List<string> done, Func<string> action)
	{
		try
		{
			string result = action();
			if (result != null)
			{
				done.Add(result);
				Game.AddStringToLog("^3Prepare hero:^7 " + result);
			}
		}
		catch (Exception e)
		{
			Log.Write("Prepare hero step failed: " + e);
			done.Add("^1A step failed^7 (see ModKit\\modkit.log).");
		}
	}

	// Mirrors NPCAcademyGhostTeacher.EncroachedBy / ProcessDialogResponse, minus the dialogs.
	private static string Teach(dmGame g, dmHero hero, NPCAcademyGhostTeacher teacher)
	{
		RecordOfPlayerCharacter record = teacher.myRecord;
		string result;
		if (record.strBestMasteryArc == "hammer")
		{
			dmItem hammer = dmArmory.GenerateHammerAtRandom();
			dmUtilities.TossLoot(teacher.Location, dmUtilities.GetSuitableNearbyTileForLootToss(hero.Location).Location, hammer);
			result = teacher.Name + " tossed you a hammer.";
		}
		else if (record.strBestMasteryArc == "wildcard")
		{
			hero.iUnspentMasteryPoints++;
			result = teacher.Name + " gave you a mastery point.";
		}
		else
		{
			dmMastery mastery = (dmMastery)g.GetGameSystemDataArchetype(record.strBestMasteryArc);
			while (hero.AddPointToMastery(hero.GetMasteryOrUseDefault(mastery), bPointProvidedByEffect: true))
			{
			}
			result = teacher.Name + " taught you " + mastery.strMasteryName + ".";
		}
		TeacherSetLearned.Invoke(teacher, null);
		dmAudio.PlayFX("academy_turnin");
		return result;
	}
}
