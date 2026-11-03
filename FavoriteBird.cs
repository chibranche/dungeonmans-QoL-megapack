using System.Linq;
using DungeonMans;
using DungeonmansModKit;

namespace DungeonmansQoL;

// Prepare hero's "equip your favorite bird", without needing any other mod. The favorite is:
//  - the bird marked with a heart in the Poultry mod's Coop, if that mod set one (the save variable is shared:
//    "qol_favorite_bird", a name kept from when the Coop was part of QoL);
//  - otherwise the bird your previous hero had equipped (remembered by this mod in "qol_last_bird").
// It is taken from the Academy's coop or your bags, healed, and put in the egg pocket.
internal static class FavoriteBird
{
	private const string MapName = "overworld";
	private const string FavoriteVar = "qol_favorite_bird", LastVar = "qol_last_bird";

	private static dmGame Game => dmUtilities.theGame;

	public static void Register()
	{
		// Remember the equipped bird as heroes come and go (each new area, and on death).
		Events.LevelEntered += (g, level) => Remember(g);
		Events.HeroDied += (g, hero) => Remember(g);
	}

	private static void Remember(dmGame g)
	{
		if (g.dungeonMans?.GetItemForSlot(ESlot.ES_BirdEggPocket) is dmBirdEgg egg && !string.IsNullOrEmpty(egg.BirdName))
		{
			dmGazetteer.SetMapScriptVar_String(LastVar, egg.BirdName, MapName);
		}
	}

	private static string WantedName()
	{
		string fav = dmGazetteer.GetMapScriptVar_String(FavoriteVar, MapName);
		return !string.IsNullOrEmpty(fav) ? fav : dmGazetteer.GetMapScriptVar_String(LastVar, MapName);
	}

	/// <summary>The bird Prepare hero would equip, or null (none known, already equipped, or not reachable).</summary>
	public static dmBirdEgg ToEquip()
	{
		dmHero hero = Game?.dungeonMans;
		string name = WantedName();
		if (hero == null || string.IsNullOrEmpty(name) || !Dm.InAcademy)
		{
			return null;
		}
		if (hero.GetItemForSlot(ESlot.ES_BirdEggPocket) is dmBirdEgg worn && worn.BirdName == name)
		{
			return null;
		}
		return hero.GetInventoryList().OfType<dmBirdEgg>().Concat(dmGazetteer.eggsStoredAtAcademy)
			.FirstOrDefault(e => e.BirdName == name && e.BirdHasEverHatched);
	}

	/// <summary>Takes, heals and equips the bird. Returns the message to show, or null if nothing was done.</summary>
	public static string Equip()
	{
		dmBirdEgg egg = ToEquip();
		dmHero hero = Game.dungeonMans;
		if (egg == null)
		{
			return null;
		}
		if (hero.GetItemForSlot(ESlot.ES_BirdEggPocket) != null && hero.CanRemoveItemFromSlot(ESlot.ES_BirdEggPocket) != RemoveItemResult.CanRemove)
		{
			return "Your egg pocket can't be emptied right now, so " + egg.BirdName + " stays put.";
		}
		if (dmGazetteer.eggsStoredAtAcademy.Remove(egg))
		{
			hero.GiveItemToCharacter(egg);
			// Hatch's wandering birds follow the stored list.
			Game.allActors.OfType<NPCAcademyHatch>().FirstOrDefault()?.UpdateWanderingPoultryList(Game.theCurrentLevel as dmDungeonLevel);
		}
		egg.GetStatInfo().bWounded = false;
		return hero.EquipItemInSlot(egg, ESlot.ES_BirdEggPocket)
			? "Favorite bird: " + egg.BirdName + " is equipped."
			: "Could not equip " + egg.BirdName + ".";
	}
}
