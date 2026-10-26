using System.Runtime.CompilerServices;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>
/// NWNX_Events raised for a player's connection, from the header's documentation (a server without players can't
/// measure them): NWNX_ON_CLIENT_CONNECT (OBJECT_SELF the module; a skip refuses the connection, its result the
/// reason), NWNX_ON_CHECK_STICKY_PLAYER_NAME_RESERVED (a skip refuses the name unless its result is "1"),
/// NWNX_ON_SERVER_SEND_AREA and NWNX_ON_CLIENT_DISCONNECT (OBJECT_SELF the character, or OBJECT_INVALID for a player
/// who never chose one).
/// </summary>
public static class NwnxSession
{
    static readonly ConditionalWeakTable<World, HashSet<string>> _arrived = new();

    /// <summary>The reason to refuse a connecting player, or null to let them in.</summary>
    public static string? Connect(World world, string playerName, string cdKey, bool dm, string address)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_CLIENT_CONNECT", world.Module);
        if (ev == null) return null;
        ev.Add("PLAYER_NAME", playerName).Add("CDKEY", cdKey).Add("IS_DM", dm).Add("IP_ADDRESS", address)
            .Add("VERSION_MAJOR", 0).Add("VERSION_MINOR", 0).Add("VERSION_POSTFIX", 0).Add("PLATFORM_ID", 0);
        bool skipped = ev.Before();
        ev.After();
        return skipped ? ev.Result.Length > 0 ? ev.Result : "Connection refused." : null;
    }

    /// <summary>Whether a server-vault player name may be used: the engine's answer, or a subscriber's.</summary>
    public static bool StickyName(World world, string playerName, string cdKey, bool dm, Func<bool> check)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_CHECK_STICKY_PLAYER_NAME_RESERVED", world.Module);
        if (ev == null) return check();
        ev.Add("PLAYER_NAME", playerName).Add("CDKEY", cdKey).Add("LEGACY_CDKEY", "").Add("IS_DM", dm);
        bool ok = ev.Before() ? ev.Result == "1" : check();
        ev.After();
        return ok;
    }

    /// <summary>The area being sent to a player, between NWNX_ON_SERVER_SEND_AREA_BEFORE and _AFTER.</summary>
    public static void SendArea(World world, Creature player, string playerKey, Area area, Action send)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_SERVER_SEND_AREA", player);
        if (ev == null) { _arrived.GetValue(world, _ => new()).Add(playerKey); send(); return; }
        bool first = _arrived.GetValue(world, _ => new()).Add(playerKey);
        ev.Add("AREA", area).Add("PLAYER_NEW_TO_MODULE", first);
        ev.Before();
        send();
        ev.After();
    }

    /// <summary>NWNX_ON_CLIENT_DISCONNECT around a player leaving (not skippable).</summary>
    public static NwnxEvent? Disconnecting(World world, Creature? player, string playerName, string cdKey)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_CLIENT_DISCONNECT", player?.Id ?? GameObject.InvalidId)?.Add("PLAYER_NAME", playerName).Add("CDKEY", cdKey);
        ev?.Before();
        return ev;
    }
}
