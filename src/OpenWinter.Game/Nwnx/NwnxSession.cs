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

    /// <summary>NWNX_ON_CHECK_STICKY_PLAYER_NAME_RESERVED's _BEFORE half, or null when nobody listens.</summary>
    public static NwnxEvent? StickyNameBefore(World world, string playerName, string cdKey, bool dm)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_CHECK_STICKY_PLAYER_NAME_RESERVED", world.Module)?
            .Add("PLAYER_NAME", playerName).Add("CDKEY", cdKey).Add("LEGACY_CDKEY", "").Add("IS_DM", dm);
        ev?.Before();
        return ev;
    }

    /// <summary>Whether a server-vault player name may be used: the vault's answer, or a subscriber's; ends the event.</summary>
    public static bool StickyName(NwnxEvent? ev, World world, string playerName, string cdKey)
    {
        bool ok = ev is { Skipped: true } ? ev.Result == "1" : Persistence.ServerVault.ClaimFolder(world, playerName, cdKey);
        ev?.After();
        return ok;
    }

    /// <summary>NWNX_ON_SERVER_SEND_AREA's _BEFORE half as an area is sent to a player (the caller runs After).</summary>
    public static NwnxEvent? SendingArea(World world, Creature player, string playerKey, Area area)
    {
        bool first = _arrived.GetValue(world, _ => new()).Add(playerKey);
        var ev = NwnxEvent.Begin(world, "NWNX_ON_SERVER_SEND_AREA", player)?.Add("AREA", area).Add("PLAYER_NEW_TO_MODULE", first);
        ev?.Before();
        return ev;
    }

    /// <summary>NWNX_ON_CLIENT_DISCONNECT around a player leaving (not skippable).</summary>
    public static NwnxEvent? Disconnecting(World world, Creature? player, string playerName, string cdKey)
    {
        var ev = NwnxEvent.Begin(world, "NWNX_ON_CLIENT_DISCONNECT", player?.Id ?? GameObject.InvalidId)?.Add("PLAYER_NAME", playerName).Add("CDKEY", cdKey);
        ev?.Before();
        return ev;
    }
}
