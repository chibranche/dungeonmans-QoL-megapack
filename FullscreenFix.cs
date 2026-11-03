using System;
using DungeonMans;
using DungeonmansModKit;
using HarmonyLib;

namespace DungeonmansQoL;

// Fix for a base-game error in fullscreen: when the window changes (launch, alt-tab), the game re-applies fullscreen
// right away (dmGame.Window_ClientSizeChanged -> Window_AfterToggleFullScreen -> graphics.ApplyChanges). If Windows
// hasn't given the game focus back yet, DirectX answers DXGI_ERROR_NOT_CURRENTLY_AVAILABLE (0x887A0022) and, with
// nothing catching it, Windows shows an ".NET unhandled exception" box. Here that one error is swallowed and the
// switch is tried again once the game window is active.
internal static class FullscreenFix
{
	private const int NotCurrentlyAvailable = unchecked((int)0x887A0022);
	private static bool retry;
	private static readonly System.Reflection.MethodInfo ApplyChanges = AccessTools.Method(typeof(dmGame), "Window_GraphicsApplyChanges");
	private static int retries, logged;

	public static void Register(Harmony h)
	{
		HarmonyMethod finalizer = Mod.Hook(typeof(FullscreenFix), nameof(Finalizer));
		h.Patch(Mod.Target(typeof(dmGame), "Window_ClientSizeChanged"), finalizer: finalizer);
		h.Patch(Mod.Target(typeof(dmGame), nameof(dmGame.Window_AfterToggleFullScreen)), finalizer: finalizer);
		Events.Update += Retry;
	}

	private static bool IsNotAvailable(Exception e)
	{
		for (; e != null; e = e.InnerException)
		{
			if (e.HResult == NotCurrentlyAvailable || (e.Message != null && e.Message.Contains("NOT_CURRENTLY_AVAILABLE")))
			{
				return true;
			}
		}
		return false;
	}

	// Returning null swallows the exception; anything else is passed on unchanged.
	private static Exception Finalizer(Exception __exception)
	{
		if (__exception == null || !Config.On(Config.FullscreenFix) || !IsNotAvailable(__exception))
		{
			return __exception;
		}
		if (logged++ < 3)
		{
			Log.Write("Fullscreen not available yet (window not focused): will retry when the game is active");
		}
		retry = true;
		return null;
	}

	private static void Retry(dmGame g)
	{
		if (!retry || !g.IsActive)
		{
			return;
		}
		retry = false;
		if (++retries > 20)
		{
			return; // something else is wrong; stop trying
		}
		// Window_AfterToggleFullScreen would see fullscreen already requested and skip ApplyChanges, so call the game's
		// apply step directly (its HardwareModeSwitch prefix runs; a new "not available" schedules another try).
		try
		{
			ApplyChanges?.Invoke(g, null);
		}
		catch (System.Reflection.TargetInvocationException e) when (IsNotAvailable(e.InnerException))
		{
			retry = true;
		}
	}
}
