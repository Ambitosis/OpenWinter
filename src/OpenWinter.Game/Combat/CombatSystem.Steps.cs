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
    /// How far off where it stands to fight (<see cref="MeleeDistance"/>) a melee attacker's steps take it straight
    /// out or in rather than round its target (visual scene pcsteps: with a dagger, 1.4 m, from 1.1 m it stepped back
    /// to 1.4 m as the round opened; from 1.5 m it went round).
    /// </summary>
    public const float StepBand = 0.25f;

    /// <summary>
    /// Plans a melee attacker's combat steps as its round opens, as measured on the original (visual scene pcsteps: a
    /// player's character and its NPC copy, each fighting a passive bandit). A creature making one attack a round steps
    /// half way through the time its swing leaves (3.4 s into the player's 5.5 s round, 3.2 s into the NPC's 5 s one),
    /// and one that isn't a player's character also steps at a random moment of the round, never while a swing plays.
    /// So a player's character steps once a round with one attack and never with more; an NPC twice with one and once
    /// with more. An attacker standing out of <see cref="StepBand"/> first steps to where it fights from.
    /// </summary>
    void PlanMeleeSteps(Creature c, CombatState s, GameObject target)
    {
        s.StepsPlanned = true;
        s.BackStep = false;
        s.StepTimes.Clear();
        var holds = SwingHolds(s.Plan.Count);
        float free = s.RoundLength - holds.Sum(h => h.Length);
        if (free > 0)
        {
            if (!c.IsPC) s.StepTimes.Add(s.RoundStart + OffSwings(Random.Shared.NextDouble() * free, holds));
            if (s.Plan.Count == 1) s.StepTimes.Add(s.RoundStart + OffSwings(free / 2, holds));
            s.StepTimes.Sort();
        }
        if (CanStep(c)) MeleeStep(c, target, sideways: false);
    }

    /// <summary>
    /// When a round's swings hold its clock (offsets into the round): attacks less than half a second apart are one
    /// swing, which holds it for (attacks + 1) / 2 times the time per attack: 1.25 s with one attack a round, 1 s with
    /// two, and with more three seconds shared out. The NPC's random step falls outside these (pcsteps: none of its
    /// one-attack rounds' steps fell 0.4-1.8 s in, none of its six-attack rounds' while a pair of swings played).
    /// </summary>
    static List<(float Start, float Length)> SwingHolds(int attacks)
    {
        var times = AttackTimes(attacks);
        float each = attacks <= 1 ? 1.25f : attacks == 2 ? 1f : Math.Min(3f, 0.75f * attacks + 0.5f) / (3 + (attacks - 3) / 2f);
        var holds = new List<(float, float)>();
        for (int i = 0; i < times.Length;)
        {
            int k = 1;
            while (i + k < times.Length && times[i + k] - times[i + k - 1] < 0.5f) k++;
            holds.Add((times[i], (k + 1) / 2f * each));
            i += k;
        }
        return holds;
    }

    /// <summary>A moment of the round's own clock as time into the round, past the swings that hold it.</summary>
    static double OffSwings(double clock, List<(float Start, float Length)> holds)
    {
        foreach (var (start, length) in holds)
            if (clock >= start) clock += length;
        return clock;
    }

    /// <summary>
    /// Plans a ranged attacker's steps for the rest of the round, once its shots are made: only an archer that isn't a
    /// player's character, close to a creature it shoots, steps (straight back, <see cref="RangedBackStepRange"/>).
    /// </summary>
    void PlanSteps(Creature c, CombatState s, GameObject target, bool ranged)
    {
        s.StepsPlanned = true;
        s.StepTimes.Clear();
        // Not a player's character (visual scene difficulty: shooting a crossbow 1.4 m from a bandit fighting her,
        // the player drew no attack of opportunity at any difficulty).
        s.BackStep = ranged && !c.IsPC && target is Objects.Creature && Distance2(c, target) < BackStepRange(c, target) - 0.05f;
        // An archer steps back two or three times in its first round from 1.5 m (archer_backoff), but while its target
        // fights it in melee now and then only once (archer_backoff, seven runs: 5.1-7.4 m, 6.2 on average, 27 s after
        // the first shot from 1.5 m, the passive target having taken up the fight).
        int count = !s.BackStep ? 0 : s.Plan.Count <= 1 ? 2 : s.Plan.Count == 2 ? 1 : 0;
        if (s.BackStep && TargetOf(target) == c.Id && Random.Shared.Next(4) == 0) count = Math.Min(count, 1);
        // An archer's first step back comes a while after the shot (archer_backoff: about half of them had stepped
        // 2.5 s after the shot, most by 2.6 s, in one run of seven one 0.25 s after it), the next 0.6-1.5 s later.
        double at = _world.Time + 2.2 + Random.Shared.NextDouble() * 1.2, until = s.RoundStart + s.RoundLength - 0.3;
        for (int i = 0; i < count && at <= until; i++)
        {
            s.StepTimes.Add(at);
            at += 0.6 + Random.Shared.NextDouble() * 0.9;
        }
    }

    /// <summary>
    /// Whether the creature can take a combat step now: it can move (a cutscene-immobilised one doesn't step, cond_misc
    /// cutimm.near) and isn't busy with something other than fighting.
    /// </summary>
    bool CanStep(Creature c) =>
        OpenWinter.Game.Rules.CreatureRules.MovementSpeedFactor(_world, c) > 0 && (c.Actions.Count == 0 || c.Actions[0].ActionType == 3);

    /// <summary>Takes a planned step if one is due.</summary>
    void StepAround(Creature c, CombatState s, GameObject target, double now)
    {
        if (s.StepTimes.Count == 0 || now < s.StepTimes[0]) return;
        s.StepTimes.RemoveAt(0);
        s.StepsTaken++;
        if (!CanStep(c)) return;
        if (s.BackStep) BackStep(c, target);
        else MeleeStep(c, target, sideways: true);
    }

    /// <summary>
    /// A melee attacker's step: straight out or in to where it fights from when it stands more than
    /// <see cref="StepBand"/> off it (at most <see cref="CombatStep"/>), otherwise round its target to either side at
    /// random (pcsteps: of fourteen rounds with two steps, seven kept the first step's side). The step that opens a
    /// round only ever goes out or in.
    /// </summary>
    void MeleeStep(Creature c, GameObject target, bool sideways)
    {
        float dist = Distance2(c, target), stand = MeleeDistance(c, target);
        if (dist < stand - StepBand) StraightStep(c, target, Math.Min(CombatStep, stand - dist));
        else if (dist > stand + StepBand) StraightStep(c, target, -Math.Min(CombatStep, dist - stand));
        else if (sideways) SideStep(c, target, Random.Shared.Next(2) == 0 ? 1 : -1);
    }

    /// <summary>An archer's step straight back from its target (<see cref="RangedBackStepRange"/>).</summary>
    void BackStep(Creature c, GameObject target)
    {
        // Far enough by now (the round's earlier step, or the target fell back): no step.
        float step = Math.Min(CombatStep, BackStepRange(c, target) - Distance2(c, target));
        // The step itself provokes nothing: the archer's shots inside the target's reach do (ProvokeShot; nwserver
        // archer_aoo: an archer stepping from 3.1 to 3.8 m past a bandit in combat that saw it drew nothing).
        if (step >= 0.05f) StraightStep(c, target, step);
    }

    /// <summary>A step straight away from the target (a positive length) or toward it (negative), still facing it.</summary>
    void StraightStep(Creature c, GameObject target, float length)
    {
        if (c.Area is not { } area || MathF.Abs(length) < 0.05f) return;
        var away = new Vector2(c.Position.X - target.Position.X, c.Position.Y - target.Position.Y);
        float dist = away.Length();
        if (dist < 0.05f) away = new Vector2(-MathF.Cos(c.Facing), -MathF.Sin(c.Facing));
        else away /= dist;
        float x = c.Position.X + away.X * length, y = c.Position.Y + away.Y * length;
        if (area.Walkmesh.GroundHeight(x, y, c.Position.Z, 2f) is not { } z || !PersonalSpace.Free(_world, area, c, x, y, z, ignore: target as Objects.Creature)) return;
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
    void SideStep(Creature c, GameObject target, int sign)
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
