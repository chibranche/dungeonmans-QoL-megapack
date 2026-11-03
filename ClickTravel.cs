using System;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace DungeonmansQoL;

// Click to walk, in dungeons, towns and the Academy:
//  - Click any tile more than one step away (seen or not) and the hero walks there with the game's own auto-path
//    (the one behind right-click "Go here"), so it stops when a monster shows up, like auto-explore.
//    A wall or unreachable spot sends the hero to the nearest walkable tile.
//  - Same on the Tab area map: click a spot, the map closes and the hero walks there.
// Adjacent clicks, Shift-clicks, attacks and clicks on UI keep the game's behavior.
internal static class ClickTravel
{
	private static bool wasDown;
	private static bool holding; // our walk started from this press: ignore the held button until it is released

	// From the QoL input handler (before the game reads input). Returns true when the input was consumed.
	public static bool HandleInput(dmGame g)
	{
		bool down = Mouse.GetState().LeftButton == ButtonState.Pressed;
		bool pressed = down && !wasDown;
		wasDown = down;
		if (!down)
		{
			holding = false;
		}
		dmHero hero = g.dungeonMans;
		if (!Config.On(Config.ClickTravel) || hero == null || g.bShowMainMenu || hero.bIsInOverworld || !g.IsActive)
		{
			return false;
		}
		if (holding)
		{
			// Holding the button would add a step each frame and cancel the walk.
			g.theInput.ExecutedActions.Remove(ActionButton.MouseTakeStep);
			return false;
		}
		if (!pressed || g.theInput.activeMouseFrame != null || g.theInput.bShiftIsDown || g.bTargetingMode)
		{
			return false;
		}
		wgWorldData level = g.theCurrentLevel;
		if (level == null)
		{
			return false;
		}

		if (g.bAreaMapMode)
		{
			if (!OnlyAreaMapOpen(g))
			{
				return false;
			}
			LocValue onMap = AreaMapTile(g, level);
			if (!onMap.IsValid())
			{
				return false;
			}
			LocValue goal = NearestWalkable(level, onMap);
			if (!goal.IsValid() || goal == hero.Location)
			{
				return false;
			}
			g.TurnOffAllDialogModes();
			Walk(g, hero, goal);
			return true;
		}

		if (Ui.IsBusy(g))
		{
			return false;
		}
		LocValue clicked = g.theRenderer.GetLocValueFromScreenCoordinates(g.theInput.vMousePosition);
		WorldTile tile = level.GetTile(clicked);
		if (tile == null || clicked.FlatTileDistanceFrom(hero.Location) <= 1)
		{
			return false;
		}
		// Seen tiles: only plain "move" clicks (not attacks, doors, NPCs...). Unseen tiles: always a walk, since
		// the game would otherwise act on whatever hides there.
		if (tile.bExplored && !g.theInput.ExecutedActions.Contains(ActionButton.MouseTakeStep))
		{
			return false;
		}
		LocValue target = NearestWalkable(level, clicked);
		if (!target.IsValid() || target.FlatTileDistanceFrom(hero.Location) <= 1)
		{
			return false;
		}
		Walk(g, hero, target);
		return true;
	}

	private static void Walk(dmGame g, dmHero hero, LocValue goal)
	{
		hero.HeroAutoPathToLocation(goal, null);
		holding = true;
	}

	private static bool OnlyAreaMapOpen(dmGame g) =>
		g.GetFullScreenMenuFlavor() == 0 && !g.bDialogBoxMode && !g.bInventoryMode && !g.bContextMenuOpen && !g.bOptionsMode;

	// Inverse of wg2DRenderer.DrawAreaMap: 16 px tiles from (32, 32), window centered on drawAreaMapAnchor.
	private static LocValue AreaMapTile(dmGame g, wgWorldData area)
	{
		wg2DRenderer tr = g.theRenderer;
		int cols = (int)(tr.fWindowXSize / 21.333f);
		int rows = (int)(tr.fWindowYSize / 18.18181f);
		LocValue anchor = g.dungeonMans.drawAreaMapAnchor;
		int x0 = Math.Min(area.WorldSizeX - cols, Math.Max(0, anchor.X - cols / 2));
		int y0 = Math.Min(area.WorldSizeY - rows, Math.Max(0, anchor.Y - rows / 2));
		Vector2 m = g.theInput.vMousePosition;
		if (m.X < 32 || m.Y < 32)
		{
			return new LocValue(-1, -1);
		}
		int tx = x0 + (int)((m.X - 32) / 16), ty = y0 + (int)((m.Y - 32) / 16);
		if (tx < x0 || ty < y0 || tx > x0 + cols || ty > y0 + rows || area.GetTile(tx, ty) == null)
		{
			return new LocValue(-1, -1);
		}
		return new LocValue(tx, ty);
	}

	private static bool Walkable(WorldTile t) =>
		t != null && !t.bClipped && !t.IsWall() && !t.IsSky()
		&& t.iTerrain != TerrainType.TT_WATER && t.iTerrain != TerrainType.TT_MOUNTAIN && t.iTerrain != TerrainType.TT_FORESTMOUNTAIN;

	// The clicked tile if it can be walked on, otherwise the closest one within 6 tiles.
	private static LocValue NearestWalkable(wgWorldData level, LocValue at)
	{
		for (int r = 0; r <= 6; r++)
		{
			LocValue best = new LocValue(-1, -1);
			int bestD = int.MaxValue;
			for (int dx = -r; dx <= r; dx++)
			{
				for (int dy = -r; dy <= r; dy++)
				{
					if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r)
					{
						continue;
					}
					WorldTile t = level.GetTile(at.X + dx, at.Y + dy);
					int d = dx * dx + dy * dy;
					if (Walkable(t) && d < bestD)
					{
						bestD = d;
						best = t.Location;
					}
				}
			}
			if (best.IsValid())
			{
				return best;
			}
		}
		return new LocValue(-1, -1);
	}
}
