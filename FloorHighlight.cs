using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;

namespace DungeonmansQoL;

// Outlines items on the floor that match your locked / favorited items, using the game's own champion outline
// shader. "Matching" = the item type is on the game's favorite list (the list it uses to auto-lock pickups),
// or you carry a locked item with the same type and name.
internal static class FloorHighlight
{
	public static void Draw(dmGame g)
	{
		if (!Config.On(Config.HighlightFavorites) || g.theCurrentLevel == null || g.bAreaMapMode || g.bInventoryMode || g.bHelpMode
			|| g.bSelectInventoryItemForEffectMode || AcademyDirectory.UiBusy(g))
		{
			return;
		}
		var locked = new HashSet<(string, string)>(g.dungeonMans.GetInventoryList()
			.Where(i => i.InventoryLocked).Select(i => (i.ArchetypeName, i.GetName())));
		// Floor items are dmTreasure actors wrapping the dmItem.
		List<dmTreasure> matches = LevelActors.Of<dmTreasure>(g).Where(t =>
		{
			dmItem item = t.containedItem;
			if (item == null || (!dmGazetteer.IsItemSetAsFavorite(item.ArchetypeName) && !locked.Contains((item.ArchetypeName, item.GetName()))))
			{
				return false;
			}
			WorldTile tile = g.theCurrentLevel.GetTile(t.Location);
			return tile != null && tile.bExplored && tile.ContainsObject(t);
		}).ToList();
		if (matches.Count == 0)
		{
			return;
		}
		wg2DRenderer tr = g.theRenderer;
		tr.BeginSpriteBatchEdge(tr.edgeShader_Ally);
		foreach (dmTreasure item in matches)
		{
			try
			{
				item.bDrawOnlyBorder = true;
				item.DrawActor(g, tr);
			}
			finally
			{
				item.bDrawOnlyBorder = false;
			}
		}
		tr.EndSpriteBatch();
	}
}
