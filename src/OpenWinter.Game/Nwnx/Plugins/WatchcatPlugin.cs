namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Watchcat: OpenWinter has no stall watchdog, so there is nothing to disable.</summary>
static class WatchcatPlugin
{
    [NwnxFunc("Watchcat", "DisableUntilScriptExit")]
    static void DisableUntilScriptExit(NwnxCall n) { }
}
