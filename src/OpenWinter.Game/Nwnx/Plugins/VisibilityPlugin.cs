using OpenWinter.Game.Players;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Visibility: overrides of which objects players' clients are shown (<see cref="VisibilityOverrides"/>; the
/// server stops or starts sending the object at once). Measured on nwserver with NWNX:EE (parity suite nwnx_reveal):
/// an unset override reads -1; values outside -1..4 and an invalid target fail the argument check (nothing changes,
/// nothing is returned); an override for a non-player "player" is still stored.
/// </summary>
static class VisibilityPlugin
{
    const string P = "Visibility";

    [NwnxFunc(P, "SetVisibilityOverride")]
    static void SetOverride(NwnxCall n)
    {
        uint player = n.PopObjectId(); var target = n.PopObject(); int value = n.PopInt();
        if (target == null || value is < VisibilityOverrides.Default or > VisibilityOverrides.AlwaysVisibleDMOnly) return;
        n.World.System<VisibilityOverrides>().Set(player, target.Id, value);
        n.World.MarkChanged(target);
    }

    [NwnxFunc(P, "GetVisibilityOverride")]
    static void GetOverride(NwnxCall n)
    {
        uint player = n.PopObjectId(); var target = n.PopObject();
        if (target == null) return;
        n.Push(n.World.System<VisibilityOverrides>().Get(player, target.Id));
    }
}
