using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// Traps on doors, placeables and triggers. Measured on nwserver (parity/suites/trap_api): the getters read the
/// object's trap fields whether or not it is trapped (an untrapped chest still has its blueprint's TrapType and
/// DisarmDC); on anything else they return -1 for the base type, TRUE for GetTrapActive and GetTrapRecoverable,
/// and 0 / "" for the rest. Scripted traps have no creator; DCs are clamped to 0-250.
/// </summary>
static class TrapFunctions
{
    static ITrappable? Trap(Call c, int i) => c.Obj(i) as ITrappable;

    [NwFunc("GetIsTrapped")] static object? IsTrapped(Call c) => Trap(c, 0) is { IsTrapped: true } ? 1 : 0;
    [NwFunc("GetTrapFlagged")] static object? Flagged(Call c) => Trap(c, 0) is { Trap.Flagged: true } ? 1 : 0;
    [NwFunc("GetTrapBaseType")] static object? BaseType(Call c) => Trap(c, 0) is { } t ? t.TrapBaseType : -1;
    [NwFunc("GetTrapDisarmable")] static object? Disarmable(Call c) => Trap(c, 0) is { Trap.Disarmable: true } ? 1 : 0;
    [NwFunc("SetTrapDisarmable")] static object? SetDisarmable(Call c) { if (Trap(c, 0) is { } t) { t.Trap.Disarmable = c.I(1) != 0; Changed(c); } return null; }
    [NwFunc("GetTrapDetectable")] static object? Detectable(Call c) => Trap(c, 0) is { Trap.Detectable: true } ? 1 : 0;
    [NwFunc("SetTrapDetectable")] static object? SetDetectable(Call c) { if (Trap(c, 0) is { } t) t.Trap.Detectable = c.I(1) != 0; return null; }
    [NwFunc("GetTrapOneShot")] static object? OneShot(Call c) => Trap(c, 0) is { Trap.OneShot: true } ? 1 : 0;
    [NwFunc("SetTrapOneShot")] static object? SetOneShot(Call c) { if (Trap(c, 0) is { } t) t.Trap.OneShot = c.I(1) != 0; return null; }
    [NwFunc("GetTrapRecoverable")] static object? Recoverable(Call c) => Trap(c, 0) is { } t ? (t.Trap.Recoverable ? 1 : 0) : 1;
    [NwFunc("SetTrapRecoverable")] static object? SetRecoverable(Call c) { if (Trap(c, 0) is { } t) { t.Trap.Recoverable = c.I(1) != 0; Changed(c); } return null; }
    [NwFunc("GetTrapActive")] static object? Active(Call c) => Trap(c, 0) is { } t ? (t.Trap.Active ? 1 : 0) : 1;
    [NwFunc("SetTrapActive")] static object? SetActive(Call c) { if (Trap(c, 0) is { } t) { t.Trap.Active = c.I(1) != 0; Changed(c); } return null; }
    [NwFunc("GetTrapCreator")]
    static object? Creator(Call c) =>
        Trap(c, 0) is { } t && c.World.Get(t.Trap.Creator) != null ? t.Trap.Creator : GameObject.InvalidId;
    [NwFunc("GetTrapKeyTag")] static object? KeyTag(Call c) => Trap(c, 0)?.Trap.KeyTag ?? "";
    [NwFunc("SetTrapKeyTag")] static object? SetKeyTag(Call c) { if (Trap(c, 0) is { } t) t.Trap.KeyTag = c.S(1); return null; }
    [NwFunc("GetTrapDisarmDC")] static object? DisarmDC(Call c) => Trap(c, 0)?.TrapDisarmDCValue ?? 0;
    [NwFunc("SetTrapDisarmDC")] static object? SetDisarmDC(Call c) { if (Trap(c, 0) is { } t) t.TrapDisarmDCValue = Math.Clamp(c.I(1), 0, 250); return null; }
    [NwFunc("GetTrapDetectDC")] static object? DetectDC(Call c) => Trap(c, 0)?.TrapDetectDCValue ?? 0;
    [NwFunc("SetTrapDetectDC")] static object? SetDetectDC(Call c) { if (Trap(c, 0) is { } t) t.TrapDetectDCValue = Math.Clamp(c.I(1), 0, 250); return null; }

    /// <summary>Players that see the trap hear about state changes that alter its look or radial menu.</summary>
    static void Changed(Call c)
    {
        if (c.Obj(0) is { } o && Trap(c, 0) is { IsTrapped: true }) TrapSystem.Publish(c.World, o);
    }

    // Flagging doesn't count: a flagged trap is seen by everyone, but GetTrapDetectedBy stays FALSE (trap_disarm FLAG).
    [NwFunc("GetTrapDetectedBy")]
    static object? DetectedBy(Call c) =>
        Trap(c, 0) is { } t && t.Trap.DetectedBy.Contains(c.O(1)) ? 1 : 0;

    // TRUE when the object can hold a trap and the detector is a creature, unless clearing a trap it hadn't detected.
    // SetTrapDetectedBy doesn't change GetLastTrapDetected.
    [NwFunc("SetTrapDetectedBy")]
    static object? SetDetectedBy(Call c)
    {
        if (c.Obj(0) is not ITrappable t || c.Obj<Creature>(1) is not { } cr) return 0;
        bool detected = c.I(2) != 0;
        if (!detected && !t.Trap.DetectedBy.Contains(cr.Id)) return 0;
        TrapSystem.SetDetected(c.World, c.Obj(0)!, cr, detected);
        return 1;
    }

    [NwFunc("GetLastTrapDetected")]
    static object? LastDetected(Call c) =>
        c.ObjOrSelf(0) is Creature cr && c.World.Get(cr.LastTrapDetected) != null ? cr.LastTrapDetected : GameObject.InvalidId;

    // The trap goes on the event queue: OnDisarm runs (GetLastDisarmed() = the caller) once the script is done, a trap
    // trigger is destroyed after it (trap_api disabled.*).
    [NwFunc("SetTrapDisabled")]
    static object? Disable(Call c)
    {
        if (c.Obj(0) is not { } o || o is not ITrappable { IsTrapped: true }) return null;
        var world = c.World;
        var by = c.Self;
        world.After(0, () => TrapSystem.Disarm(world, o, by));
        return null;
    }

    [NwFunc("GetLastDisarmed")]
    static object? LastDisarmed(Call c)
    {
        if (c.Self is not ITrappable t) return GameObject.InvalidId;
        uint id = c.Event.Obj("Disarmer");
        if (c.World.Get(id) != null) return id;
        return c.World.Get(t.Trap.LastDisarmedBy) != null ? t.Trap.LastDisarmedBy : GameObject.InvalidId;
    }

    [NwFunc("GetNearestTrapToObject")]
    static object? NearestTrap(Call c)
    {
        var target = c.ObjOrSelf(0);
        if (target?.Area is not { } area) return GameObject.InvalidId;
        bool detectedOnly = c.I(1, 1) != 0;
        GameObject? best = null;
        float bestD = float.MaxValue;
        foreach (var o in area.Objects)
        {
            if (o == target || o.IsDestroyed || o is not ITrappable { IsTrapped: true } t) continue;
            if (detectedOnly && !t.Trap.DetectedBy.Contains(target.Id)) continue;
            float d = Vector3.DistanceSquared(o.Position, target.Position);
            if (d < bestD) { bestD = d; best = o; }
        }
        return best?.Id ?? GameObject.InvalidId;
    }

    /// <summary>STANDARD_FACTION_* (0 hostile .. 3 defender) to repute.fac faction ids (PC is 0).</summary>
    static int StandardFaction(Call c, int standard) => c.World.Factions.StandardFaction(Math.Clamp(standard, 0, 3));

    // Fails (OBJECT_INVALID) below size 1.0 and for a type traps.2da lacks. The trigger is named after the trap
    // (traps.2da TrapName), faces 0 and has no creator.
    [NwFunc("CreateTrapAtLocation")]
    static object? CreateAt(Call c)
    {
        // int nTrapType, location lLocation, float fSize, string sTag, int nFaction, string sOnDisarmScript, string sOnTrapTriggeredScript
        int type = c.I(0);
        float size = c.F(2, 2f);
        if (size < 1f || !TrapSystem.IsTrapType(c.World, type)) return GameObject.InvalidId;
        var name = uint.TryParse(c.World.Resources.TwoDA("traps").Get(type, "TrapName"), out var strref) ? new LocString { StrRef = (int)strref } : null;
        var t = TrapSystem.CreateAt(c.World, type, c.L(1), size, c.S(3), StandardFaction(c, c.I(4)), c.S(5), c.S(6), GameObject.InvalidId, name);
        return t?.Id ?? GameObject.InvalidId;
    }

    // Only on doors and placeables without a trap already.
    [NwFunc("CreateTrapOnObject")]
    static object? CreateOn(Call c)
    {
        // int nTrapType, object oObject, int nFaction, string sOnDisarmScript, string sOnTrapTriggeredScript
        if (c.Obj(1) is (Door or Placeable) and ITrappable { IsTrapped: false } && TrapSystem.IsTrapType(c.World, c.I(0)))
            TrapSystem.Arm(c.World, c.Obj(1)!, c.I(0), StandardFaction(c, c.I(2)), c.S(3), c.S(4));
        return null;
    }
}
