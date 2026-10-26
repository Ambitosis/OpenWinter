using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game;

/// <summary>
/// Server-side creature locomotion over the area walkmesh. Measured on the original: creatures start at full
/// speed, walk straight at the creaturespeed.2da rate scaled by effects, face where they go, and brake evenly
/// over the last second to stop exactly on a destination point.
/// </summary>
public static class Movement
{
    const float TurnRate = 10f;
    const float MaxStep = 0.9f;

    const int EffectForceWalk = 90; // EFFECT_TYPE_FORCE_WALK

    /// <summary>
    /// The speed a creature moves at now (m/s): its creaturespeed.2da rate times the effects' factor
    /// (CreatureRules.MovementSpeedFactor). Slowed creatures can't run (measured: a slowed runner moves at a
    /// quarter of its run speed); nor can EffectForceWalk or dazed ones, nor ones that asked to walk.
    /// <para>Movement stages (ruleset MOVEMENT_STAGE_PENALTY_*): stealth mode costs one, detect mode one more (not with
    /// Keen Sense), a player's load one (encumbered) or two (overloaded), and they add up. Stage 0 runs, stage 1 walks
    /// and each further stage halves the walk, with no rounding: a player runs at 4, encumbered or in stealth walks at 2,
    /// overloaded or encumbered in stealth moves at 1, overloaded in stealth at 0.5 and overloaded in stealth and detect
    /// mode at 0.25 m/s, whether she asked to run or to walk, and effects scale that (hasted and overloaded: 1.5).
    /// Measured: visual scenes encumberdrive and encmodes.</para>
    /// </summary>
    public static float MaxSpeed(World world, Creature c, out bool walking)
    {
        float factor = Rules.CreatureRules.MovementSpeedFactor(world, c);
        bool walk = c.WalkRequested || Rules.CreatureRules.IsSlowed(c) || c.Effects.Any(e => e.Type is EffectForceWalk or Combat.CombatK.EffectDazed);
        int stage = Math.Max(walk ? 1 : 0, Skills.SkillSystem.ModeStages(world, c) + (c.IsPC ? c.LoadStage : 0));
        walking = stage > 0;
        return (stage == 0 ? c.RunRate : c.WalkRate * MathF.Pow(0.5f, stage - 1)) * factor;
    }

    public static void Step(World world, Creature c, float dt)
    {
        if (c.Area == null) return;
        if (c.DriveInput != null)
        {
            if (!c.IsDead && c.Commandable && world.Time <= c.DriveExpires && c.Actions.Count == 0) { Driving.Step(world, c, dt); return; }
            c.DriveInput = null;
            c.DriveReleased = false;
            c.DriveHeldTime = c.DriveStepRemaining = c.DriveTurnRemaining = 0;
            c.MoveDirection = Vector3.Zero;
        }
        // A block from the last step that no move action picked up is stale now.
        c.MoveBlockedBy = GameObject.InvalidId;
        float before = c.Speed;
        bool moved = false, walking = c.WalkRequested;
        if (c.MoveGoal is { } goal && c.MoveDirection != Vector3.Zero)
        {
            // Scripted movement: straight at the next path point, braking onto a final destination.
            var delta = new Vector2(goal.X - c.Position.X, goal.Y - c.Position.Y);
            float dist = delta.Length();
            float max = MaxSpeed(world, c, out walking);
            c.Speed = 0;
            if (dist > 1e-4f && max > 0 && c.RunRate > 0)
            {
                float length = c.MoveArrive ? BrakingStep(max, dist, dt) : MathF.Min(max * dt, dist);
                float speed = length / dt;
                c.Facing = c.MoveBackward ? MathF.Atan2(-delta.Y, -delta.X) : MathF.Atan2(delta.Y, delta.X);
                var step = delta / dist * length;
                if (ClosedDoorBlocking(world, c, step) is { } door)
                {
                    c.MoveBlockedBy = door.Id;
                    Stop(c);
                }
                else if (TryMove(c, step) is { } p)
                {
                    var to = length >= dist ? new Vector3(goal.X, goal.Y, p.Z) : p;
                    var blocking = PersonalSpace.Step(world, c, to);
                    if (blocking is { IsPC: true } && !c.IsPC)
                    {
                        // Nobody pushes a player, and the original's creatures went round one that came their way
                        // (summondoor): plan again round where she is now rather than give the move up.
                        c.MoveReplan = true;
                        c.Speed = 0;
                    }
                    else if (blocking is { } blocker)
                    {
                        // Walked into a busy creature: the move action fails (OnBlocked).
                        c.MoveBlockedBy = blocker.Id;
                        Stop(c);
                    }
                    else
                    {
                        c.Position = to;
                        c.Area.CreatureGrid.Update(c);
                        c.Speed = speed;
                        moved = true;
                    }
                }
            }
        }
        else
        {
            // Steered movement (a player's keys): full speed at once, turning toward the direction.
            var dir = new Vector2(c.MoveDirection.X, c.MoveDirection.Y);
            c.Speed = 0;
            if (dir.LengthSquared() > 0.0001f && c.RunRate > 0)
            {
                float speed = MaxSpeed(world, c, out walking);
                float wanted = MathF.Atan2(dir.Y, dir.X);
                c.Facing = LerpAngle(c.Facing, wanted, Math.Clamp(TurnRate * dt, 0, 1));
                var forward = new Vector2(MathF.Cos(c.Facing), MathF.Sin(c.Facing));
                var step = forward * speed * dt;
                var next = Unblocked(world, c, TryMove(c, step)) ?? Unblocked(world, c, TryMove(c, new Vector2(step.X, 0)))
                    ?? Unblocked(world, c, TryMove(c, new Vector2(0, step.Y)));
                // Blocked (a wall, the walkmesh edge, a busy creature): stand still rather than walk on the spot.
                if (next is { } p)
                {
                    c.Position = p;
                    c.Area.CreatureGrid.Update(c);
                    c.Speed = speed;
                    moved = true;
                }
            }
        }
        // Coming to a standing stop sets the standing stance: idle out of combat, combat-ready in it (Creature.IdleStance).
        if (!moved && c.Mode != MovementMode.Stand) c.IdleStance = !c.InCombat;
        if (moved) Nwnx.NwnxMovement.Moved(world, c);
        var mode = !moved ? MovementMode.Stand : c.MoveBackward ? MovementMode.Backward : walking ? MovementMode.Walk : MovementMode.Run;
        if (moved || before != c.Speed || mode != c.Mode)
        {
            c.Mode = mode;
            world.MarkChanged(c);
        }
    }

    static Door? ClosedDoorBlocking(World world, Creature c, Vector2 step)
    {
        if (!c.MovePassClosedDoors || step.LengthSquared() < 1e-12f) return null;
        var wm = c.Area!.Walkmesh;
        // The authored slab already supplies the native stopping boundary. Test the
        // attempted center step, like other physical walkmesh collisions.
        var probe = c.Position + new Vector3(step, 0);
        int owner = wm.ClosedDoorBetween(c.Position, probe, MaxStep);
        if (owner == 0 || world.Get<Door>((uint)owner) is not { IsOpen: false } door) return null;
        return wm.Trace(c.Position, probe, MaxStep, ignoreClosedDoors: true) >= 0.999f ? door : null;
    }

    /// <summary>
    /// Distance covered in dt toward a stop dist away, braking at max m/s² (from full speed to rest in one second)
    /// so that the speed is never more than sqrt(2 · max · remaining).
    /// </summary>
    public static float BrakingStep(float max, float dist, float dt)
    {
        float brakeFrom = max / 2; // the braking distance from full speed: max² / (2 · max)
        float t = dt;
        float d = dist;
        if (d > brakeFrom)
        {
            float cruise = (d - brakeFrom) / max;
            if (cruise >= t) return max * t;
            t -= cruise;
            d = brakeFrom;
        }
        float v = MathF.Sqrt(2 * max * d) - max * t;
        return v <= 0 ? dist : dist - v * v / (2 * max);
    }

    /// <summary>Stops scripted and steered movement.</summary>
    public static void Stop(Creature c)
    {
        c.DriveInput = null;
        c.DriveReleased = false;
        c.DriveHeldTime = c.DriveStepRemaining = c.DriveTurnRemaining = 0;
        c.MoveGoal = null;
        c.MoveArrive = false;
        c.MoveBackward = false;
        c.MovePassClosedDoors = false;
        c.MoveDirection = Vector3.Zero;
    }

    /// <summary>The step, unless a creature is in the way (idle ones are pushed aside).</summary>
    static Vector3? Unblocked(World world, Creature c, Vector3? next) =>
        next is { } p && PersonalSpace.Step(world, c, p) == null ? p : null;

    static Vector3? TryMove(Creature c, Vector2 step) => Step(c.Area!, c.Position, step);

    /// <summary>
    /// Where a step from <paramref name="from"/> lands on the ground, or null where the walkmesh doesn't let a creature
    /// go (a wall, a footprint, a drop). Paths are planned with <see cref="Areas.Walkmesh.Trace"/>, which agrees with
    /// this for every step size.
    /// </summary>
    public static Vector3? Step(Area area, Vector3 from, Vector2 step)
    {
        if (step.LengthSquared() < 1e-12f) return null;
        var target = from + new Vector3(step.X, step.Y, 0);
        var z = area.Walkmesh.GroundHeight(target.X, target.Y, from.Z, MaxStep);
        if (z == null) return null;
        target.Z = z.Value;
        return target;
    }

    static float LerpAngle(float from, float to, float t)
    {
        float diff = (to - from) % (2 * MathF.PI);
        if (diff > MathF.PI) diff -= 2 * MathF.PI;
        if (diff < -MathF.PI) diff += 2 * MathF.PI;
        return from + diff * t;
    }
}
