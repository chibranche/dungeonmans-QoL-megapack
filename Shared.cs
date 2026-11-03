using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMans;
using DungeonmansModKit;
using Microsoft.Xna.Framework;

namespace DungeonmansQoL;

// A value recomputed at most every few frames: for checks drawn every frame (button states, status lines) that
// scan the world or call into other mods.
internal sealed class Throttled<T>
{
	private readonly Func<T> compute;
	private readonly int everyFrames;
	private int at = int.MinValue;
	private T value;

	public Throttled(Func<T> compute, int everyFrames = 15)
	{
		this.compute = compute;
		this.everyFrames = everyFrames;
	}

	public T Value
	{
		get
		{
			if (at == int.MinValue || Ui.Frame - at >= everyFrames || Ui.Frame < at)
			{
				value = compute();
				at = Ui.Frame;
			}
			return value;
		}
	}

	public void Reset() => at = int.MinValue;
}

// Actors of one type on the current level, rescanned at most 4 times a second instead of every frame.
internal static class LevelActors
{
	private sealed class Entry
	{
		public wgWorldData Level;
		public int Frame;
		public object List;
	}

	private static readonly Dictionary<Type, Entry> cache = new Dictionary<Type, Entry>();

	public static List<T> Of<T>(dmGame g) where T : dmActor
	{
		if (!cache.TryGetValue(typeof(T), out Entry e) || e.Level != g.theCurrentLevel || Ui.Frame - e.Frame >= 15 || Ui.Frame < e.Frame)
		{
			e = new Entry { Level = g.theCurrentLevel, Frame = Ui.Frame, List = g.allActors.OfType<T>().ToList() };
			cache[typeof(T)] = e;
		}
		return (List<T>)e.List;
	}
}

// Colored frames on map tiles (sensed pools, memorials): only explored tiles, only what is on screen.
internal static class TileMarks
{
	public static void Draw(dmGame g, IEnumerable<(LocValue Loc, Color Color)> marks)
	{
		wgWorldData level = g.theCurrentLevel;
		wg2DRenderer tr = g.theRenderer;
		var screen = new Rectangle(0, 0, Ui.ScreenSize.X, Ui.ScreenSize.Y);
		bool begun = false;
		foreach ((LocValue loc, Color c) in marks)
		{
			WorldTile tile = level.GetTile(loc);
			if (tile == null || !tile.bExplored)
			{
				continue;
			}
			Vector2 a = tr.GetScreenSpaceFromWorldLocation(loc);
			Vector2 b = tr.GetScreenSpaceFromWorldLocation(new LocValue(loc.X + 1, loc.Y + 1));
			var r = new Rectangle((int)a.X, (int)a.Y, (int)(b.X - a.X), (int)(b.Y - a.Y));
			if (r.Width <= 0 || r.Height <= 0 || !screen.Intersects(r))
			{
				continue;
			}
			if (!begun)
			{
				tr.BeginSpriteBatch();
				begun = true;
			}
			Ui.Fill(r, new Color((int)c.R, c.G, c.B, 50));
			Ui.Outline(r, c, 2);
		}
		if (begun)
		{
			tr.EndSpriteBatch();
		}
	}
}
