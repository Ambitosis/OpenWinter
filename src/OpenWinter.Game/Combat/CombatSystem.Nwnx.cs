using System.Globalization;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>NWNX_Events combat helpers (parity suite nwnx_events_combat).</summary>
public sealed partial class CombatSystem
{
    const int FeatDisarm = 9, FeatImprovedDisarm = 16;

    /// <summary>
    /// NWNX_ON_ATTACK_TARGET_CHANGE as an attacker takes a new target: RETARGETABLE is 1 in the _BEFORE half, where a
    /// result (an object as ObjectToString writes it) picks the target instead, and 0 after. Returns the target a
    /// subscriber chose, or null to keep <paramref name="next"/>.
    /// </summary>
    GameObject? TargetChange(Creature attacker, uint old, GameObject next, bool automatic)
    {
        var ev = Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_ATTACK_TARGET_CHANGE", attacker);
        if (ev == null) return null;
        ev.AddObject("OLD_TARGET_OBJECT_ID", old).Add("NEW_TARGET_OBJECT_ID", next).Add("AUTOMATIC_CHANGE", automatic).Add("RETARGETABLE", true);
        ev.Before();
        GameObject? chosen = null;
        if (ev.HasResult && uint.TryParse(ev.Result, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id)
            && _world.Get(id) is { } picked && picked != attacker && picked.Area == attacker.Area)
            chosen = picked;
        ev.Add("NEW_TARGET_OBJECT_ID", chosen ?? next).Add("RETARGETABLE", false).After();
        return chosen;
    }

    /// <summary>A creature leaves combat; a player raises NWNX_ON_COMBAT_EXIT around it.</summary>
    void LeaveCombat(Creature c)
    {
        var ev = c.IsPC ? Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_COMBAT_EXIT", c) : null;
        ev?.Before();
        c.InCombat = false;
        _world.MarkChanged(c);
        ev?.After();
    }
}
