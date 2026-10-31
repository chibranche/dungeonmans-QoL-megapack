using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;

namespace DungeonmansQoL;

// Headmaster: once the Library holds 101 books, hide "I want to give you some books for the Library." from his
// visiting menu. Rank 4 (the last rank) is reached at 10 books, but every book adds +1% blacksmith "outdid himself"
// crafting chance (1d100 < books, so 101 books = always).
// ModKit's Dialogs.HideResponse keeps the answers after it mapped to their original numbers.
internal static class HeadmasterPatches
{
	private const int BooksResponse = 1;
	private const int LibraryFullBooks = 101;

	public static void Register(Harmony h)
	{
		h.Patch(Mod.Target(typeof(NPCAcademyHeadmaster), "ShowVisitingDungeonmansDialog"),
			postfix: Mod.Hook(typeof(HeadmasterPatches), nameof(VisitingPostfix)));
	}

	private static void VisitingPostfix()
	{
		if (Config.On(Config.HideMaxedTurnIns)
			&& dmGazetteer.academyProgress.GetUnlockCount(AcademyProgressData.AcademyUnlocksEnum.LIBRARY) >= LibraryFullBooks)
		{
			Dialogs.HideResponse(dmUtilities.theGame.currentDialogBox, BooksResponse);
		}
	}
}
