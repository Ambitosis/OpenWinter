using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Net;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Creature's movement functions, measured on nwserver (parity suite nwnx_creature_move): the factor a script
/// sets is held to the cap (1.5 unless the creature has its own) and to the 0.125 floor, and stands until a speed
/// effect is applied or removed; setting a cap doesn't change the factor until then.
/// </summary>
static partial class CreaturePlugin
{
    /// <summary>A creaturespeed.2da row: walk and run rates (players use row 0; "default" the appearance's rate).</summary>
    [NwnxFunc("Creature", "SetMovementRate")]
    static void SetMovementRate(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int rate = n.PopInt();
        if (c == null) return;
        var speeds = n.World.Resources.TwoDA("creaturespeed");
        if (rate == 7)
        {
            var mr = n.World.Resources.TwoDA("appearance").Get(c.AppearanceType, "MOVERATE");
            rate = speeds.Find("2DAName", mr) is int r && r >= 0 ? r : 4;
        }
        if (rate < 0 || rate >= speeds.RowCount) return;
        c.MovementRateIndex = rate;
        c.WalkRate = speeds.GetFloat(rate, "WALKRATE", c.WalkRate);
        c.RunRate = speeds.GetFloat(rate, "RUNRATE", c.RunRate);
        if (c.RunRate <= 0) { c.WalkRate = 0; c.RunRate = 0; }
        Changed(n, c);
    }

    [NwnxFunc("Creature", "GetMovementRateFactor")]
    static void GetMoveFactor(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null ? CreatureRules.MovementRateFactor(n.World, c) : 0f);
    }

    [NwnxFunc("Creature", "SetMovementRateFactor")]
    static void SetMoveFactor(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); float factor = n.PopFloat();
        if (c == null) return;
        var o = O(c);
        float floor = Ruleset.Float(n.World, "MOVEMENT_SPEED_PENALTY_CAP", 0.125f);
        o.MovementFactor = Math.Clamp(factor, floor, Math.Max(floor, CreatureRules.MovementRateFactorCap(n.World, c)));
        o.StampMovementFactor(c);
        Changed(n, c);
    }

    /// <summary>The creature's own cap; 0 (none set) means the ruleset's.</summary>
    [NwnxFunc("Creature", "GetMovementRateFactorCap")]
    static void GetMoveFactorCap(NwnxCall n) => n.Push(n.PopObject<Creature>()?.Overrides?.MovementFactorCap ?? 0f);

    [NwnxFunc("Creature", "SetMovementRateFactorCap")]
    static void SetMoveFactorCap(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); float cap = n.PopFloat();
        if (c == null) return;
        var o = O(c);
        // The factor in force now stays until the speed effects change.
        float now = CreatureRules.MovementRateFactor(n.World, c);
        o.MovementFactorCap = Math.Max(0, cap);
        o.MovementFactor = now;
        o.StampMovementFactor(c);
    }

    /// <summary>NWNX_CREATURE_MOVEMENT_TYPE_*: standing, walking, running, side-stepping or walking backwards.</summary>
    [NwnxFunc("Creature", "GetMovementType")]
    static void MovementType(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c?.Mode switch
        {
            MovementMode.Walk or MovementMode.MouseWalk or MovementMode.WalkForwardLeft or MovementMode.WalkForwardRight => 1,
            MovementMode.Run or MovementMode.MouseRun or MovementMode.RunForwardLeft or MovementMode.RunForwardRight => 2,
            MovementMode.StrafeLeft or MovementMode.StrafeRight => 3,
            MovementMode.Backward => 4,
            _ => 0,
        });
    }

    /// <summary>Highest walking speed, in the original's units (2000 = 2 m/s); negative lifts the cap.</summary>
    [NwnxFunc("Creature", "SetWalkRateCap")]
    static void SetWalkRateCap(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); float cap = n.PopFloat();
        if (c == null) return;
        O(c).WalkRateCap = cap < 0 ? null : cap / 1000f;
        Changed(n, c);
    }
}
