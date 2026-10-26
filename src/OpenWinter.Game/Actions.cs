using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game;

public enum ActionStatus { InProgress, Done, Failed }

/// <summary>An entry in an object's action queue (ActionMoveToObject, ActionWait, ActionDoCommand, ...).</summary>
public abstract class GameAction
{
    /// <summary>ACTION_* constant reported by GetCurrentAction.</summary>
    public virtual int ActionType => 65535; // ACTION_INVALID
    public abstract ActionStatus Step(World world, GameObject owner, float dt);
    public virtual void Cancel(World world, GameObject owner) { }
    /// <summary>A committed current action can finish while clearing discards its queued successors.</summary>
    public virtual bool RetainsAfterQueueClear(World world, GameObject owner) => false;
}

public sealed class DoCommandAction : GameAction
{
    public required ScriptSituation Situation;
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        // Off the queue before it runs: GetCurrentAction inside the command reports the next action.
        if (owner.Actions.Count > 0 && ReferenceEquals(owner.Actions[0], this)) owner.Actions.RemoveAt(0);
        world.Scripts?.Run(Situation, owner.Id);
        return ActionStatus.Done;
    }
}

/// <summary>
/// ActionWait, on the game clock (a SetTime jump ends it). A negative time waits until the queue is cleared,
/// like the original.
/// </summary>
public sealed class WaitAction : GameAction
{
    public float Remaining;
    double _until = double.NaN;
    public override int ActionType => 36; // ACTION_WAIT
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (Remaining < 0) return ActionStatus.InProgress;
        if (double.IsNaN(_until)) _until = world.GameTime + Remaining;
        return world.GameTime >= _until - 1e-4 ? ActionStatus.Done : ActionStatus.InProgress;
    }
}

public sealed class SpeakAction : GameAction
{
    public string Text = "";
    public int Volume;
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        world.Speak(owner, Text, Volume);
        return ActionStatus.Done;
    }
}

/// <summary>ActionPlayAnimation: blocks the queue for the animation's time (GetCurrentAction reports ACTION_INVALID).</summary>
public sealed class AnimationAction : GameAction
{
    public int Animation;
    public float Speed = 1, Duration;
    bool _started;
    float _elapsed, _length;
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (!_started)
        {
            _started = true;
            _length = BlockingTime(Animation, Speed, Duration);
            world.Output?.PlayAnimation(owner, Animation, Speed, Duration);
            return ActionStatus.InProgress;
        }
        _elapsed += dt;
        return _elapsed >= _length - 1e-4f ? ActionStatus.Done : ActionStatus.InProgress;
    }

    /// <summary>
    /// How long ActionPlayAnimation holds the queue, measured on the original: a looping animation with a duration
    /// lasts that long (whatever the speed; except ANIMATION_LOOPING_LISTEN), otherwise one play of the animation's
    /// server-side length divided by the speed. The lengths are the same for every creature model.
    /// </summary>
    public static float BlockingTime(int animation, float speed, float duration)
    {
        // (Sitting in a chair takes a little longer: the original adds about 0.075 s.)
        if (animation < 100 && duration > 0 && animation != 2) return duration + (animation == 6 ? 0.075f : 0);
        return BaseLength(animation) / (speed > 0 ? speed : 1);
    }

    static float BaseLength(int animation) => animation switch
    {
        <= 3 or (>= 8 and <= 11) => 2f,
        4 => 7f,
        7 => 1.5f,
        12 => 0.867f,
        13 => 0.5f,
        14 => 2.833f,
        15 => 4f,
        < 100 => 1f,
        103 => 4f,
        104 => 0.5f,
        109 or 110 => 1.733f,
        111 or (>= 114 and <= 116) => 1f,
        _ => 2f,
    };
}

/// <summary>
/// Walks a creature to a point (stopping exactly on it), to an object (stopping Range away, but never closer than
/// the two creatures' personal spaces allow), or away from an object: when nearer than Range, Range metres further
/// away in a straight line, setting off about 0.3 s later.
/// </summary>
public sealed class MoveAction : GameAction
{
    public Vector3 Target;
    public uint TargetObject = GameObject.InvalidId;
    /// <summary>Combat approaches the object's origin; interaction actions use its authored PWK/DWK use point.</summary>
    public bool TargetOrigin;
    /// <summary>A door's closed-state use point even when it stands open (where a creature takes a door transition).</summary>
    public bool ClosedUsePoint;
    /// <summary>Arrive at full speed (a leg that a transition continues), not braking over the last second.</summary>
    public bool NoBraking;
    public float Range = 1f;
    /// <summary>Inside another action, wait a moment after a block before setting off again (combat approaches).</summary>
    public bool RetryWait = true;
    /// <summary>Optional object-approach fallback: accept this horizontal distance to its use point
    /// when no path exists or movement stalls. Zero keeps ordinary moves' existing failure behavior.</summary>
    public float GiveUpWithin;
    public bool Run;
    /// <summary>Player ground clicks may step backward within 1 m and 45 degrees of directly behind.
    /// Native clickbackfine brackets the boundaries at 0.9875/1.0169 m and 43.836/45.836 degrees.</summary>
    public bool AllowBackward;
    bool? _backward;
    /// <summary>Move away instead of toward.</summary>
    public bool Away;
    public float Timeout = 30f;
    /// <summary>ActionForceMove*: when time runs out, jump to the destination, then stand a moment.</summary>
    public bool JumpOnTimeout;
    /// <summary>ActionMoveToLocation / ActionMoveToObject only: plan through closed doors and stop against them with
    /// OnBlocked (parity suite door_blocked). Every other move plans round closed doors.</summary>
    public bool PassClosedDoors;
    /// <summary>
    /// ActionMoveToObject whose target is an open transition door: arriving at it (within the default 1 m) takes the
    /// mover through, player or NPC. Measured on nwmain (visual scene doormove): both walked to the Graduation
    /// Chamber's open doorway and came out in the Training Halls; with a 3 m range, or the door closed, they stopped.
    /// </summary>
    public bool TakeDoorTransition;
    /// <summary>Path search budget (grid cells expanded); small for short ambient moves.</summary>
    public int MaxSearch = 60000;
    float _elapsed, _stuck;
    Vector3 _last;
    Vector3? _awayGoal;
    (Vector3 Point, float MinRange)? _usePoint;
    const float AwayDelay = 0.3f;
    List<Vector3>? _path;
    bool _noPath;
    Vector3 _pathGoal;
    /// <summary>The planned stopping point, for feedback on player-issued moves (including object approaches).</summary>
    public Vector3? PlannedDestination => _path != null ? _pathGoal : null;
    int _repaths;
    public override int ActionType => 0; // ACTION_MOVETOPOINT

    /// <summary>
    /// How close a mover gets to an object (its use radius): 0.3 m plus both creatures' CREPERSPACE (appearance.2da)
    /// when the object is a creature, the mover's PERSPACE plus 0.5 for an ordinary trigger, its PERSPACE for anything
    /// else (doors and placeables: <see cref="WorldObjects.DoorService.UsePoint"/>). Measured on nwserver by parity suite
    /// approach_use (1.3 m from a human, 1.5 m for a half-orc; 0.3 m from an item or waypoint with fRange 0.1).
    /// </summary>
    public static float Clearance(World world, Creature mover, GameObject target) => target switch
    {
        Creature t => 0.3f + PersonalSpace(world, mover) + PersonalSpace(world, t),
        Trigger { TriggerType: not Trigger.TypeTransition } => Game.PersonalSpace.Radius(world, mover) + TriggerUseRadius,
        _ => Game.PersonalSpace.Radius(world, mover),
    };

    /// <summary>An ordinary trigger's use radius beyond the user's PERSPACE.</summary>
    public const float TriggerUseRadius = 0.5f;
    /// <summary>"In range" allows this much beyond a use radius (approach_use J110 stood still at 1.10 m).</summary>
    public const float UseTolerance = 0.1f;

    /// <summary>A creature's CREPERSPACE (appearance.2da): how close other creatures come.</summary>
    public static float PersonalSpace(World world, Creature c) => Game.PersonalSpace.CreatureSpace(world, c);

    /// <summary>Overrides the closest approach to an object (following keeps nearer than ActionMoveToObject).</summary>
    public float? MinRange;

    static float Flat(Vector3 v) => new Vector2(v.X, v.Y).Length();

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature c || c.Area == null) return ActionStatus.Failed;
        // A creature that can't move at all (held, asleep, stunned, entangled, immobilised) gives the move up at
        // once rather than waiting for the condition to end (nwserver, parity suite condition_actions: premove, queued).
        if (Rules.CreatureRules.MovementSpeedFactor(world, c) <= 0) { Movement.Stop(c); return ActionStatus.Failed; }
        if (c.MoveBlockedBy != GameObject.InvalidId)
        {
            // Walked into a busy creature or closed door: the move fails and OnBlocked runs.
            uint blocker = c.MoveBlockedBy;
            c.MoveBlockedBy = GameObject.InvalidId;
            Movement.Stop(c);
            // Off the queue before OnBlocked runs (GetCurrentAction there reports the next action).
            if (c.Actions.Count > 0 && ReferenceEquals(c.Actions[0], this)) c.Actions.RemoveAt(0);
            c.LastBlockedAt = world.GameTime;
            if (world.Get<Door>(blocker) is { } door) WorldObjects.DoorService.ReportBlocked(world, c, door);
            else
            {
                c.LastBlockingDoor = blocker;
                world.RunEvent(c, "ScriptOnBlocked", new EventContext().Set("BlockingDoor", blocker));
            }
            return ActionStatus.Failed;
        }
        // A move inside another action (closing in to attack) waits a moment after a block before trying again,
        // as the original's attackers do (they bump about once a second while crowding round a target).
        if (!_started && RetryWait && world.GameTime - c.LastBlockedAt < RetryDelay && !(c.Actions.Count > 0 && ReferenceEquals(c.Actions[0], this)))
            return ActionStatus.InProgress;
        _started = true;
        GameObject? target = null;
        if (TargetObject != GameObject.InvalidId)
        {
            target = world.Get(TargetObject);
            if (target == null || target.Area != c.Area) { Movement.Stop(c); return ActionStatus.Failed; }
        }
        Vector3 goal;
        bool arrived;
        if (Away)
        {
            if (_awayGoal == null)
            {
                var from = target?.Position ?? Target;
                var d = new Vector2(c.Position.X - from.X, c.Position.Y - from.Y);
                if (d.Length() >= Range) return ActionStatus.Done;
                d = d.LengthSquared() > 1e-6f ? Vector2.Normalize(d) : new Vector2(-MathF.Cos(c.Facing), -MathF.Sin(c.Facing));
                _awayGoal = c.Position + new Vector3(d * Range, 0);
            }
            goal = _awayGoal.Value;
            arrived = Flat(goal - c.Position) < 0.001f;
            if (_elapsed < AwayDelay) { _elapsed += dt; return ActionStatus.InProgress; }
        }
        else if (target != null)
        {
            // Doors and placeables are approached at their use point.
            var aim = target.Position;
            float min = MinRange ?? Clearance(world, c, target);
            if (!TargetOrigin && target is (Door or Placeable))
            {
                _usePoint ??= WorldObjects.DoorService.UsePoint(world, c, target, ClosedUsePoint);
                if (_usePoint is { } use) { aim = use.Point; min = MinRange ?? use.MinRange; }
            }
            float stop = MathF.Max(Range, min);
            _triggerUse = TriggerUse.None;
            var used = TakeDoorTransition && !TargetOrigin && target is Trigger { Geometry.Count: > 2 } tr ? tr : null;
            if (used != null)
            {
                bool inside = used.Contains(c.Position) || used.OutlineDistance(c.Position) < 0.01f;
                if (used.TriggerType == Trigger.TypeTransition) { stop = 0; _triggerUse = TriggerUse.Transition; }
                else if (used.TriggerType == Trigger.TypeTrap)
                {
                    _trapAim ??= TrapApproachPoint(used, c.Position);
                    aim = _trapAim.Value;
                    stop = TrapStopShort;
                    _triggerUse = TriggerUse.Hold;
                }
                else if (used.Script("OnClick").Length > 0)
                {
                    _triggerUse = inside ? TriggerUse.Click : TriggerUse.Hold;
                    stop = MathF.Max(Range, Game.PersonalSpace.Radius(world, c));
                }
                if (_triggerUse is TriggerUse.Transition or TriggerUse.Click && inside) stop = float.MaxValue;
            }
            var d = new Vector2(aim.X - c.Position.X, aim.Y - c.Position.Y);
            float dist = d.Length();
            arrived = dist <= stop + 0.001f;
            // An ordinary trigger is walked to by its position, and reached once its outline is within use range
            // (approach_use TG2, TG3: 0.89 m from the nearest outline point, coming at it from either side).
            if (!arrived && !TargetOrigin && _triggerUse == TriggerUse.None && target is Trigger { TriggerType: not Trigger.TypeTransition } trigger && trigger.Geometry.Count > 1)
                arrived = trigger.OutlineDistance(c.Position) <= Game.PersonalSpace.Radius(world, c) + TriggerUseRadius + UseTolerance;
            if (stop == float.MaxValue) stop = 0;
            goal = dist > 1e-4f ? aim - new Vector3(d / dist * stop, 0) : c.Position;
            if (_approach is { } side) goal = aim + new Vector3(side * stop, 0);
            _aim = aim;
            _stop = stop;
        }
        else
        {
            goal = _clearGoal ?? Target;
            arrived = Flat(goal - c.Position) < 0.001f;
        }
        _elapsed += dt;
        if (arrived)
        {
            Movement.Stop(c);
            if (TakeDoorTransition && Range <= 1f && target is Door { IsOpen: true } door && (door.IsTransition || door.Script("OnClick").Length > 0))
                world.System<WorldObjects.TransitionService>().Transition(c, door);
            // A closed transition door isn't opened: the mover stands at its use point in its move action for good
            // (approach_click DR_SHUT, two nwserver runs).
            else if (TakeDoorTransition && Range <= 1f && target is Door { IsOpen: false } shut && (shut.IsTransition || shut.Script("OnClick").Length > 0))
                return ActionStatus.InProgress;
            switch (_triggerUse)
            {
                case TriggerUse.Transition:
                    // Inside an area-transition trigger: its OnClick, or the default transition (approach_click TR_*).
                    world.System<WorldObjects.TransitionService>().Transition(c, target!);
                    break;
                case TriggerUse.Click:
                    world.RunEvent(target!, "OnClick", new EventContext().Set("Clicking", c.Id));
                    break;
                case TriggerUse.Hold:
                    // Outside a trigger that can only be used from inside, or at a trap trigger: the original's mover
                    // stands there in its move action for good (approach_click: still ACTION_MOVETOPOINT 32 s later).
                    return ActionStatus.InProgress;
            }
            return ActionStatus.Done;
        }
        if (_elapsed > Timeout)
        {
            Movement.Stop(c);
            if (!JumpOnTimeout) return ActionStatus.Failed;
            world.Jump(c, new Location(c.Area.Id, goal, c.FacingDegrees));
            if (c.Actions.Count > 0 && ReferenceEquals(c.Actions[0], this)) c.Actions.Insert(1, new WaitAction { Remaining = 0.75f });
            return ActionStatus.Done;
        }
        c.WalkRequested = !Run;
        // Follow a path; replan when the goal moved, a player stepped into the way or we got stuck.
        if (c.MoveReplan) { c.MoveReplan = false; _path = null; }
        if (_path == null || Flat(_pathGoal - goal) > 1.5f)
        {
            var obstacles = Away ? null : Game.PersonalSpace.Obstacles(world, c, target);
            // Closing in on an object (a creature, a door's or placeable's use point) whose near side is taken by
            // busy creatures: go round to a free side.
            if (target != null && obstacles != null && Taken(goal, obstacles) && FreeSide(c, obstacles) is { } side)
            {
                _approach = side;
                goal = _aim + new Vector3(side * _stop, 0);
            }
            // A destination inside a busy or non-friendly creature's personal space: the original goes round it and
            // stops straight out from it at the personal space distance, without OnBlocked (parity suite coll_goal).
            if (target == null && !Away && _clearGoal == null && obstacles != null && ClearOf(c, goal, obstacles) is { } clear)
                _clearGoal = goal = clear;
            _path = Away ? null : (PassClosedDoors ? c.Area.MovementPathfinder : c.Area.Pathfinder).FindPath(c.Position, goal, MaxSearch, obstacles, Game.PersonalSpace.Radius(world, c));
            _noPath = _path == null;
            _path ??= new List<Vector3> { Reachable(c, goal) };
            _pathGoal = goal;
        }
        _path[^1] = goal;
        while (_path.Count > 1 && Flat(_path[0] - c.Position) < 0.05f) _path.RemoveAt(0);
        if (_backward == null)
        {
            var offset = new Vector2(goal.X - c.Position.X, goal.Y - c.Position.Y);
            var forward = new Vector2(MathF.Cos(c.Facing), MathF.Sin(c.Facing));
            // Keep detours and object approaches on their existing forward path.
            _backward = AllowBackward && TargetObject == GameObject.InvalidId && _path.Count == 1
                && offset.LengthSquared() is > 0 and <= 1f
                && Vector2.Dot(Vector2.Normalize(offset), forward) <= -MathF.Cos(MathF.PI / 4);
        }
        c.MoveBackward = _backward == true;
        if (c.MoveBackward) c.WalkRequested = true;
        var next = _path[0];
        c.MoveGoal = next;
        c.MovePassClosedDoors = PassClosedDoors;
        c.MoveArrive = _path.Count == 1 && !NoBraking;
        var step = new Vector2(next.X - c.Position.X, next.Y - c.Position.Y);
        c.MoveDirection = step.LengthSquared() > 1e-8f ? new Vector3(Vector2.Normalize(step), 0) : new Vector3(MathF.Cos(c.Facing), MathF.Sin(c.Facing), 0);
        if (Vector3.DistanceSquared(_last, c.Position) < 0.0001f) _stuck += dt; else _stuck = 0;
        _last = c.Position;
        // Prefer the close authored use point while progress is possible. A short stall or failed
        // path is enough to use a nearby container; don't burn four 1.5-second repaths on a table.
        if (!Away && target != null && GiveUpWithin > 0 && (_noPath || _stuck >= 0.25f)
            && Flat(_aim - c.Position) <= GiveUpWithin + 0.001f)
        {
            Movement.Stop(c);
            return ActionStatus.Done;
        }
        if (_stuck > 1.5f)
        {
            if (++_repaths > 3) { Movement.Stop(c); return ActionStatus.Failed; }
            _path = null;
            _stuck = 0;
        }
        return ActionStatus.InProgress;
    }

    const float RetryDelay = 0.5f;

    /// <summary>
    /// With no path: as far toward the goal as a straight walk gets (the whole way off the walkmesh's end, where the
    /// step check stops the creature anyway), so that movement is never handed a leg it refuses part way.
    /// </summary>
    static Vector3 Reachable(Creature c, Vector3 goal)
    {
        var wm = c.Area!.Walkmesh;
        float f = wm.Trace(c.Position, goal, 0.9f);
        if (f >= 0.999f) return goal;
        var p = Vector3.Lerp(c.Position, goal, MathF.Max(0, f - 0.01f));
        return p with { Z = wm.GroundHeight(p.X, p.Y, c.Position.Z, 0.9f) ?? c.Position.Z };
    }
    bool _started;
    Vector2? _approach;
    Vector3 _aim;

    /// <summary>
    /// What ActionMoveToObject does at a trigger (nwserver approach_click, a stripped bandit 6 m off). An
    /// area-transition trigger is walked into and then used: its OnClick, else the transition. An ordinary trigger with
    /// an OnClick script is used only from inside: from inside, OnClick runs at once without a step; from outside the
    /// mover stops the usual range from the trigger's position (0.30, 1.00, 5.00 m for fRange 0.1, 1 and 5) and never
    /// clicks or finishes. Without OnClick it stops 0.9 m from the outline (approach_use; the default). A trap trigger
    /// is approached at a point 0.5 m outside the nearest point of its outline; the mover stopped 0.67 m outside it and
    /// never finished.
    /// </summary>
    enum TriggerUse { None, Transition, Click, Hold }
    TriggerUse _triggerUse;
    Vector3? _trapAim;
    /// <summary>How far outside a trap trigger's outline the mover heads, and how short of that it stops (measured).</summary>
    public const float TrapApproachOutside = 0.5f, TrapStopShort = 0.17f;

    static Vector3 TrapApproachPoint(Trigger t, Vector3 from)
    {
        var q = new Vector2(from.X, from.Y);
        Vector2 best = q;
        float bestD = float.MaxValue;
        for (int i = 0, j = t.Geometry.Count - 1; i < t.Geometry.Count; j = i++)
        {
            var a = new Vector2(t.Geometry[j].X, t.Geometry[j].Y);
            var b = new Vector2(t.Geometry[i].X, t.Geometry[i].Y);
            var ab = b - a;
            float k = ab.LengthSquared() > 1e-8f ? Math.Clamp(Vector2.Dot(q - a, ab) / ab.LengthSquared(), 0, 1) : 0;
            var p = a + ab * k;
            float dd = Vector2.Distance(q, p);
            if (dd < bestD) { bestD = dd; best = p; }
        }
        var outward = q - best;
        outward = outward.LengthSquared() > 1e-8f ? Vector2.Normalize(outward) : Vector2.UnitX;
        var point = best + outward * TrapApproachOutside;
        return new Vector3(point.X, point.Y, from.Z);
    }
    float _stop;

    /// <summary>A point inside a planning obstacle's personal spaces (the circle less its margin): a mover there bumps.</summary>
    static bool Taken(Vector3 p, List<(Vector2 Centre, float Radius)> obstacles)
    {
        foreach (var (centre, radius) in obstacles)
        {
            float bump = radius - Game.PersonalSpace.PlanMargin;
            if (Vector2.DistanceSquared(centre, new Vector2(p.X, p.Y)) < bump * bump) return true;
        }
        return false;
    }

    /// <summary>
    /// For a destination inside an obstacle's personal spaces: the point straight out from its centre (or toward the
    /// mover, from right on it) at that distance, on the ground and clear of the others. Null when the destination is
    /// clear or there is no such point.
    /// </summary>
    static Vector3? ClearOf(Creature c, Vector3 goal, List<(Vector2 Centre, float Radius)> obstacles)
    {
        var at = new Vector2(goal.X, goal.Y);
        foreach (var (centre, radius) in obstacles)
        {
            float bump = radius - Game.PersonalSpace.PlanMargin;
            var away = at - centre;
            if (away.LengthSquared() >= bump * bump) continue;
            if (away.LengthSquared() < 1e-8f) away = new Vector2(c.Position.X, c.Position.Y) - centre;
            if (away.LengthSquared() < 1e-8f) return null;
            var p = centre + Vector2.Normalize(away) * (bump + ClearMargin);
            if (c.Area!.Walkmesh.GroundHeight(p.X, p.Y, goal.Z, 1f) is not { } z) return null;
            var moved = new Vector3(p.X, p.Y, z);
            return Taken(moved, obstacles) ? null : moved;
        }
        return null;
    }
    Vector3? _clearGoal;

    /// <summary>
    /// The direction from the target to stop in when the straight stopping point is taken. Measured (coll_goal): the
    /// original walks round the creature on its left and stops about where the object's range circle meets the
    /// creature's personal space. Otherwise the side nearest the mover's whose stopping point no busy creature takes.
    /// Either way the target must be walkable to in a straight line from there (not through a door or a wall).
    /// </summary>
    Vector2? FreeSide(Creature c, List<(Vector2 Centre, float Radius)> obstacles)
    {
        var from = new Vector2(c.Position.X - _aim.X, c.Position.Y - _aim.Y);
        float baseAngle = from.LengthSquared() > 1e-6f ? MathF.Atan2(from.Y, from.X) : c.Facing + MathF.PI;
        var toward = new Vector2(MathF.Cos(baseAngle), MathF.Sin(baseAngle));
        var left = new Vector2(toward.Y, -toward.X); // the mover's left, walking toward the target
        var aim = new Vector2(_aim.X, _aim.Y);
        var straight = aim + toward * _stop;
        foreach (var (centre, radius) in obstacles)
        {
            float bump = radius - Game.PersonalSpace.PlanMargin + ClearMargin;
            if (Vector2.DistanceSquared(centre, straight) >= bump * bump) continue;
            float d = Vector2.Distance(centre, aim);
            if (d < 1e-4f || _stop < 1e-4f) continue;
            float cos = (_stop * _stop + d * d - bump * bump) / (2 * _stop * d);
            if (cos is < -1 or > 1) continue;
            float phi = MathF.Acos(cos), to = MathF.Atan2(centre.Y - aim.Y, centre.X - aim.X);
            var a = new Vector2(MathF.Cos(to + phi), MathF.Sin(to + phi));
            var b = new Vector2(MathF.Cos(to - phi), MathF.Sin(to - phi));
            if (Vector2.Dot(b, left) > Vector2.Dot(a, left)) (a, b) = (b, a);
            foreach (var dir in new[] { a, b })
                if (Stoppable(c, dir, obstacles)) return dir;
        }
        // The mover's left first (measured: coll_goal objstop).
        for (int k = 1; k <= 12; k++)
            foreach (int sign in new[] { -1, 1 })
            {
                float a = baseAngle + sign * k * MathF.PI / 12;
                var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                if (Stoppable(c, dir, obstacles)) return dir;
            }
        return null;
    }

    bool Stoppable(Creature c, Vector2 dir, List<(Vector2 Centre, float Radius)> obstacles)
    {
        var p = _aim + new Vector3(dir * _stop, 0);
        return !Taken(p, obstacles) && c.Area!.Walkmesh.GroundHeight(p.X, p.Y, _aim.Z, 1f) is { } z
            && c.Area.Walkmesh.Trace(p with { Z = z }, _aim, 0.9f) >= 0.999f;
    }

    /// <summary>How far outside a creature's personal space a moved destination goes (float slack against a bump).</summary>
    const float ClearMargin = 0.02f;

    public override void Cancel(World world, GameObject owner) { if (owner is Creature c) Movement.Stop(c); }
}

/// <summary>ActionJumpToLocation / ActionJumpToObject: jump, then stand 0.75 s (ACTION_WAIT) like the original.</summary>
public sealed class JumpAction : GameAction
{
    public required Location Destination;
    public override int ActionType => 65535;
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        world.Jump(owner, Destination);
        if (owner.Actions.Count > 0 && ReferenceEquals(owner.Actions[0], this)) owner.Actions.Insert(1, new WaitAction { Remaining = 0.75f });
        return ActionStatus.Done;
    }
}

/// <summary>Wanders randomly around the starting point until interrupted.</summary>
public sealed class RandomWalkAction : GameAction
{
    MoveAction? _leg;
    float _pause;
    Vector3? _home;
    public override int ActionType => 43; // ACTION_RANDOMWALK
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        _home ??= owner.Position;
        if (_pause > 0) { _pause -= dt; return ActionStatus.InProgress; }
        if (_leg == null)
        {
            float a = Random.Shared.NextSingle() * MathF.Tau, r = 2f + Random.Shared.NextSingle() * 6f;
            _leg = new MoveAction { Target = _home.Value + new Vector3(MathF.Cos(a) * r, MathF.Sin(a) * r, 0), Range = 0.5f, Timeout = 8f, MaxSearch = 2500 };
        }
        if (_leg.Step(world, owner, dt) != ActionStatus.InProgress)
        {
            _leg = null;
            _pause = 1f + Random.Shared.NextSingle() * 3f;
        }
        return ActionStatus.InProgress;
    }
    public override void Cancel(World world, GameObject owner) => _leg?.Cancel(world, owner);
}

/// <summary>Runs queued actions for every object each tick.</summary>
public static class ActionQueues
{
    /// <summary>Player tasks append; an indefinite attack yields to the requested task.</summary>
    public static void EnqueuePlayerTask(World world, Creature player, GameAction action)
    {
        if (!Accepts(player)) return;
        if (player.Actions.FirstOrDefault() is Combat.AttackAction attack)
        {
            attack.Cancel(world, player);
            player.Actions.Remove(attack);
        }
        player.Actions.Add(action);
    }

    public static void Step(World world, GameObject o, float dt)
    {
        var queue = o.Actions;
        if (queue.Count == 0) return;
        // Dying empties the queue; a broken door does nothing by itself (nwserver, door_bashed: AC_0 closing itself).
        if (o is Creature { IsDead: true } or Door { Broken: true }) { Clear(world, o); return; }
        // Instantaneous actions (commands, speech) all complete in the same tick, like the original.
        for (int guard = 0; guard < 256 && queue.Count > 0; guard++)
        {
            var current = queue[0];
            var status = current.Step(world, o, dt);
            if (status == ActionStatus.InProgress) return;
            if (queue.Count > 0 && ReferenceEquals(queue[0], current)) queue.RemoveAt(0);
            dt = 0;
        }
    }

    /// <summary>Whether an object takes new actions: creatures that are alive and commandable, placeables and doors (not the module or areas).</summary>
    public static bool Accepts(GameObject o) => o.Commandable && o is not (Module or Areas.Area) && o is not (Creature { IsDead: true } or Door { Broken: true });

    public static void Clear(World world, GameObject o)
    {
        var current = o.Actions.FirstOrDefault();
        var retained = current?.RetainsAfterQueueClear(world, o) == true ? current : null;
        foreach (var a in o.Actions)
            if (!ReferenceEquals(a, retained)) a.Cancel(world, o);
        o.Actions.Clear();
        if (retained != null) o.Actions.Add(retained);
    }
}
