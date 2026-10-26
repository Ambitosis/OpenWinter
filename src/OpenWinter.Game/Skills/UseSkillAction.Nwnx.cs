using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Skills;

/// <summary>
/// NWNX_ON_TRAP_DISARM / _FLAG / _RECOVER / _EXAMINE / _SET for the trap skills (parity suite nwnx_events_traps): a
/// _BEFORE half with NEEDS_TO_MOVE 1 as the creature sets off towards the trap (only when it has to walk), another
/// with NEEDS_TO_MOVE 0 as it reaches it, and the _AFTER half with ACTION_RESULT once the attempt is decided. A skip
/// ends the attempt. Set Trap also names its target and where the trap goes.
/// </summary>
public sealed partial class UseSkillAction
{
    NwnxEvent? _trapEvent;
    System.Numerics.Vector3? _trapStart;
    bool _trapSetOff;

    string TrapEventName => Skill == SkillIds.SetTrap ? "NWNX_ON_TRAP_SET" : SubSkill switch
    {
        SkillIds.SubSkillFlagTrap => "NWNX_ON_TRAP_FLAG",
        SkillIds.SubSkillRecoverTrap => "NWNX_ON_TRAP_RECOVER",
        SkillIds.SubSkillExamineTrap => "NWNX_ON_TRAP_EXAMINE",
        _ => "NWNX_ON_TRAP_DISARM",
    };

    NwnxEvent? TrapEvent(World world, Creature c, GameObject? trap)
    {
        var ev = NwnxEvent.Begin(world, TrapEventName, c);
        if (ev == null) return null;
        if (Skill == SkillIds.SetTrap)
        {
            ev.AddObject("TRAP_OBJECT_ID", ItemId).Add("TARGET_OBJECT_ID", trap == c ? null : trap)
                .Position("TARGET_POSITION_", Spot?.Position ?? trap?.Position ?? c.Position);
        }
        else ev.Add("TRAP_OBJECT_ID", trap);
        return ev;
    }

    /// <summary>The _BEFORE half as the creature sets off or reaches the trap. True when a subscriber skipped it.</summary>
    bool TrapBefore(World world, Creature c, GameObject? trap, bool needsToMove)
    {
        if (needsToMove ? _trapSetOff : _trapEvent != null) return false;
        if (needsToMove) _trapSetOff = true;
        var ev = TrapEvent(world, c, trap);
        if (ev == null) return false;
        if (!needsToMove) _trapEvent = ev;
        ev.Add("NEEDS_TO_MOVE", needsToMove);
        if (!ev.Before()) return false;
        ev.Remove("NEEDS_TO_MOVE").Add("ACTION_RESULT", false).After();
        _trapEvent = null;
        return true;
    }

    /// <summary>The _AFTER half once the attempt is decided.</summary>
    void TrapAfter(bool result)
    {
        _trapEvent?.Remove("NEEDS_TO_MOVE").Add("ACTION_RESULT", result).After();
        _trapEvent = null;
    }
}
