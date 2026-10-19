using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

public sealed partial class CombatSystem
{
    /// <summary>A combat step: 0.8 m along the circle round the target, as the original's sidesteps measure.</summary>
    public const float CombatStep = 0.8f;

    /// <summary>
    /// A ranged attacker that isn't a player, closer than this to a creature it shoots, takes its round's combat steps
    /// straight back from it (0.8 m each, the last one only as far as this distance), whatever its scripts: measured on
    /// nwserver with archers that had no event scripts at all (parity suite archer_backoff): from 1.5 m it stepped to 2.3,
    /// 3.1 and 3.8 m, from 3 m once to 3.8 m, from 6 m never.
    /// </summary>
    public const float RangedBackStepRange = 3.8f;

    /// <summary>
    /// How far an archer backs off from its target: <see cref="RangedBackStepRange"/>, the same against a halberd
    /// (nwserver archer_aoo halberd: 3.8 m, not the 4.0 m the halberd's longer PrefAttackDist would give); against a
    /// target that itself holds a ranged weapon only its own appearance.2da PREFATCKDIST + the target's HITDIST
    /// (archer_aoo bow: a drow archer 1.0 m from a bandit with a short bow stepped to 1.8 m).
    /// </summary>
    float BackStepRange(Creature c, GameObject target)
    {
        if (target is not Creature t || Creature is not { } r || r.Weapons(t).Main is not { } main || !r.Stats(main).Ranged) return RangedBackStepRange;
        var app = _world.Resources.TwoDA("appearance");
        return app.GetFloat(c.AppearanceType, "PREFATCKDIST", 1.5f) + app.GetFloat(t.AppearanceType, "HITDIST", 0.3f);
    }

    /// <summary>
    /// Plans a melee attacker's sideways steps for the rest of the round, once its attacks are made, as measured on
    /// the original (parity/visual/scenes/combatsound and combatsteps): two steps after a round of one attack, one after
    /// a round of two, none after more, the first 0.4-2.6 s after the swing and the next soon after. The
    /// first step's side is random and the second usually keeps it. The attacker keeps facing its target through
    /// each step.
    /// </summary>
    void PlanSteps(Creature c, CombatState s, GameObject target, bool ranged)
    {
        s.StepsPlanned = true;
        s.StepTimes.Clear();
        // Not a player's character (visual scene difficulty: shooting a crossbow 1.4 m from a bandit fighting her,
        // the player drew no attack of opportunity at any difficulty).
        s.BackStep = ranged && !c.IsPC && target is Objects.Creature && Distance2(c, target) < BackStepRange(c, target) - 0.05f;
        // An archer steps back as often as a melee attacker steps round (archer_backoff: from 1.5 m two or three steps
        // in its first round), but while its target fights it in melee now and then only one (archer_backoff, seven
        // runs: 5.1-7.4 m, 6.2 on average, 27 s after the first shot from 1.5 m, the passive target having taken up the fight).
        int count = !s.BackStep && (ranged || target is not Objects.Creature) ? 0 : s.Plan.Count <= 1 ? 2 : s.Plan.Count == 2 ? 1 : 0;
        if (s.BackStep && TargetOf(target) == c.Id && Random.Shared.Next(4) == 0) count = Math.Min(count, 1);
        // combatsteps: the first step 0.4-2.6 s after the swing, the next 0.2-1.5 s later (given time for the
        // half-second step), all before the next round. An archer's first step back comes later (archer_backoff: about
        // half of them had stepped 2.5 s after the shot, most by 2.6 s, in one run of seven one 0.25 s after it).
        double at = _world.Time + (s.BackStep ? 2.2 + Random.Shared.NextDouble() * 1.2 : 0.4 + Random.Shared.NextDouble() * 2.2),
            until = s.RoundStart + s.RoundLength - 0.3;
        for (int i = 0; i < count && at <= until; i++)
        {
            s.StepTimes.Add(at);
            at += 0.6 + Random.Shared.NextDouble() * 0.9;
        }
        s.StepSign = Random.Shared.Next(2) == 0 ? 1 : -1;
    }

    /// <summary>Takes a planned step if one is due.</summary>
    void StepAround(Creature c, CombatState s, GameObject target, double now)
    {
        // A creature that can't move doesn't step round its target either (cutscene immobilised: cond_misc cutimm.near).
        if (OpenWinter.Game.Rules.CreatureRules.MovementSpeedFactor(_world, c) <= 0) return;
        if (s.StepTimes.Count > 0 && now >= s.StepTimes[0])
        {
            s.StepTimes.RemoveAt(0);
            // The second step of a round keeps the first's side three times in four.
            if (s.StepsTaken++ > 0 && Random.Shared.Next(4) == 0) s.StepSign = -s.StepSign;
            if (c.Actions.Count == 0 || c.Actions[0].ActionType == 3)
            {
                if (s.BackStep) BackStep(c, target);
                else SideStep(c, target, ref s.StepSign);
            }
        }
    }

    /// <summary>An archer's step straight back from its target (<see cref="RangedBackStepRange"/>).</summary>
    void BackStep(Creature c, GameObject target)
    {
        if (c.Area is not { } area) return;
        var away = new Vector2(c.Position.X - target.Position.X, c.Position.Y - target.Position.Y);
        float dist = away.Length();
        // Far enough by now (the round's earlier step, or the target fell back): no step.
        float step = Math.Min(CombatStep, BackStepRange(c, target) - dist);
        if (step < 0.05f) return;
        if (dist < 0.05f) away = new Vector2(-MathF.Cos(c.Facing), -MathF.Sin(c.Facing));
        else away /= dist;
        float x = c.Position.X + away.X * step, y = c.Position.Y + away.Y * step;
        if (area.Walkmesh.GroundHeight(x, y, c.Position.Z, 2f) is not { } z || !PersonalSpace.Free(_world, area, c, x, y, z, ignore: target as Objects.Creature)) return;
        // The step itself provokes nothing: the archer's shots inside the target's reach do (ProvokeShot; nwserver
        // archer_aoo: an archer stepping from 3.1 to 3.8 m past a bandit in combat that saw it drew nothing).
        var from = c.Position;
        float before = c.Facing;
        c.Position = new Vector3(x, y, z);
        c.Facing = MathF.Atan2(target.Position.Y - y, target.Position.X - x);
        KeepStandFacing(c, before, from);
        area.CreatureGrid.Update(c);
        _world.Output?.Notify("creature-bump", c, new
        {
            from = new[] { from.X, from.Y, from.Z },
            to = new[] { x, y, z },
            facing = c.Facing,
            clipFacing = c.Facing,
            face = target.Id,
        });
        _world.MarkChanged(c);
    }

    /// <summary>
    /// A combat step doesn't turn the creature as far as attacks of opportunity go: it keeps the facing it stood with
    /// (<see cref="CombatState.StandFacing"/>; nwserver ai_cast_rounds: goblins made facing away from the wizard they
    /// fought drew no extra swing at its casts, 0 of 6, though one-attack rounds have a step).
    /// </summary>
    void KeepStandFacing(Creature c, float before, Vector3 from)
    {
        var s = State(c);
        if (s.StandFacing == null) { s.StandFacing = before; s.StandOrigin = from; }
        s.StandPos = c.Position;
    }

    /// <summary>Moves the attacker one step round its target (the other side when that one is blocked).</summary>
    void SideStep(Creature c, GameObject target, ref int sign)
    {
        if (c.Area is not { } area) return;
        var offset = new Vector2(c.Position.X - target.Position.X, c.Position.Y - target.Position.Y);
        float dist = offset.Length();
        if (dist < 0.3f) return;
        float angle = MathF.Atan2(offset.Y, offset.X), turn = 2 * MathF.Asin(Math.Min(1f, CombatStep / 2 / dist));
        for (int attempt = 0; attempt < 2; attempt++, sign = -sign)
        {
            float a = angle + sign * turn;
            float x = target.Position.X + dist * MathF.Cos(a), y = target.Position.Y + dist * MathF.Sin(a);
            if (area.Walkmesh.GroundHeight(x, y, c.Position.Z, 2f) is not { } z || !PersonalSpace.Free(_world, area, c, x, y, z, ignore: target as Objects.Creature)) continue;
            var from = c.Position;
            float before = c.Facing;
            c.Position = new Vector3(x, y, z);
            c.Facing = MathF.Atan2(target.Position.Y - y, target.Position.X - x);
            KeepStandFacing(c, before, from);
            area.CreatureGrid.Update(c);
            // The position changes at once; players see a sideways step (as a bump) turning to keep the target in
            // front: the step's clip is chosen from the facing halfway round, and the client faces the target.
            float midway = MathF.Atan2(target.Position.Y - (from.Y + y) / 2, target.Position.X - (from.X + x) / 2);
            _world.Output?.Notify("creature-bump", c, new
            {
                from = new[] { from.X, from.Y, from.Z },
                to = new[] { x, y, z },
                facing = c.Facing,
                clipFacing = midway,
                face = target.Id,
            });
            _world.MarkChanged(c);
            return;
        }
    }
}
