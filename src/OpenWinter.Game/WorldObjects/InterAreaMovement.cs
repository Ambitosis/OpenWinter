using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Routes between areas: the area graph made by doors and triggers that lead somewhere (their LinkedTo or
/// SetTransitionTarget target), searched for the fewest transitions. Measured on nwserver (parity suites
/// interarea_move, interarea_pursuit, route_ties; docs/INTER_AREA_MOVEMENT.md): the original takes the route through the
/// fewest areas even when a longer chain of small areas is a shorter walk, breaks ties by the exits' X, and plans
/// through locked and closed doors as if they were open.
/// </summary>
public static class AreaRoutes
{
    /// <summary>
    /// The transitions to take, in order, from <paramref name="from"/> (standing at <paramref name="position"/>) to
    /// <paramref name="to"/>; empty when already there, null when no chain of transitions leads there.
    /// </summary>
    public static List<GameObject>? Plan(World world, Area from, Vector3 position, Area to)
    {
        if (from == to) return new List<GameObject>();
        // Routes of 1, then 2, ... up to 9 transitions, each searched depth first with every area's exits in order of
        // X (west first), so between routes through as many areas the one whose first differing exit lies furthest
        // west wins, however long the walk. Measured on nwserver by parity suite route_ties (the exit at x=8 was taken
        // over the nearer one at x=22, through a larger area or not).
        for (int depth = 1; depth <= MaxTransitions; depth++)
        {
            var seen = new HashSet<Area> { from };
            var route = new List<GameObject>();
            if (Search(world, from, to, depth, seen, route)) return route;
        }
        return null;
    }

    /// <summary>The most transitions an automatic route takes.</summary>
    public const int MaxTransitions = 9;

    /// <summary>
    /// One depth-limited search. An area counts as seen once it is listed as an exit's destination, so a later branch of
    /// the same search never goes through it again (not measured in the unusual layouts it affects).
    /// </summary>
    static bool Search(World world, Area area, Area to, int remaining, HashSet<Area> seen, List<GameObject> route)
    {
        var exits = Exits(world, area).OrderBy(e => e.Via.Position.X).ToList();
        foreach (var (via, dest, _) in exits)
            if (dest == to) { route.Add(via); return true; }
        if (remaining <= 1) return false;
        var next = new List<(GameObject Via, Area Dest)>();
        foreach (var (via, dest, _) in exits)
            if (seen.Add(dest)) next.Add((via, dest));
        foreach (var (via, dest) in next)
        {
            route.Add(via);
            if (Search(world, dest, to, remaining - 1, seen, route)) return true;
            route.RemoveAt(route.Count - 1);
        }
        return false;
    }

    /// <summary>An area's doors and transition triggers with the area each leads to and roughly where one arrives.</summary>
    public static IEnumerable<(GameObject Via, Area Dest, Vector3 Arrival)> Exits(World world, Area area)
    {
        foreach (var o in area.Objects)
        {
            if (o.IsDestroyed || o is not (Door or Trigger)) continue;
            if (o is Trigger t && t.TriggerType != Trigger.TypeTransition && t.TransitionTargetId == GameObject.InvalidId) continue;
            if (TransitionService.ResolveTarget(world, o) is not { Area: { } dest } target || dest == area) continue;
            yield return (o, dest, target.Position);
        }
    }
}

/// <summary>
/// A move to an object or location in another area (ActionMoveToObject, ActionForceMoveToObject,
/// ActionMoveToLocation, ActionForceMoveToLocation) and an attacker's pursuit of a target that left its area. The
/// creature walks to the next transition and takes it, then carries on in the next area. Every rule here is a
/// measurement of the original on nwserver (docs/INTER_AREA_MOVEMENT.md):
/// <list type="bullet">
/// <item>Doors are taken from their use point (where ActionOpenDoor stands); triggers on walking into them. A closed
/// door stops the creature at the use point, firing OnBlocked again every tick until something opens it (a locked one
/// is never routed round). An OnClick script runs instead of the transition, retried every 0.67 s.</item>
/// <item>Through a door the creature lands on the destination door's outer use point side, 1.9 m out; through a
/// trigger on the target waypoint. Its associates jump with it. It then stands 1.3 s.</item>
/// <item>After a trigger the original forgets the route: it first walks towards the target's coordinates in the area
/// it is in (stopping 1 m short), then plans again. A location move ends there; in the target's own area the creature
/// walks onto the target.</item>
/// <item>After a door a location move walks to the next transition and ends there; an object move goes on.</item>
/// <item>In the target's area: a one-transition move through a door first walks to the source door's use point (the
/// same coordinates in the new area), then to the target, stopping at the usual range; a longer route ends exactly on
/// the target.</item>
/// <item>The leg in the area reached by the route's first door, legs to the target and the detours are walked even
/// when running was asked for.</item>
/// <item>With no route an object move stands, retrying every 2 s (1 s as ACTION_MOVETOPOINT, 1 s as ACTION_WAIT); a
/// location move ends at once.</item>
/// </list>
/// </summary>
public sealed class InterAreaMoveAction : GameAction
{
    public enum Goal { Object, Location, Pursuit }

    public Goal Kind = Goal.Object;
    public uint TargetObject = GameObject.InvalidId;
    public Location TargetLocation = Location.Invalid;
    public bool Run;
    /// <summary>The usual stopping range at an object reached through a single door.</summary>
    public float Range = 1f;
    /// <summary>ActionForceMove*: when the time runs out, jump to the target.</summary>
    public bool JumpOnTimeout;
    public float Timeout = float.MaxValue;
    /// <summary>The move made in the target's area when the route was never needed (the target is in the mover's own
    /// area when the action starts): exactly the ordinary same-area move.</summary>
    public MoveAction? SameArea;

    /// <summary>Seconds a creature stands after arriving through a transition.</summary>
    public const float ArrivalPause = 1.3f;
    /// <summary>Seconds a creature stands at the end of its detour before setting off on the new route.</summary>
    public const float DetourPause = 1.0f;
    /// <summary>How long GetCurrentAction reads ACTION_INVALID after an arrival (and inside the area's OnEnter).</summary>
    public const float ArrivalInvalid = 0.25f;
    /// <summary>Between OnClick attempts at a transition whose script doesn't move the creature.</summary>
    public const float ClickRetry = 0.67f;
    /// <summary>How long a pursuer stands at a door after its first transition before going through.</summary>
    public const float PursuitDoorWait = 0.9f;
    /// <summary>How long a creature stands at a door after its first transition before going through.</summary>
    public const float DoorWait = 0.4f;
    double _doorWaitUntil = double.NaN;
    float _stall;
    Vector3 _stallAt;
    /// <summary>How far past a door's outer use point a creature lands (local -Y of the destination door).</summary>
    public const float DoorArrivalOut = 1.0f;

    enum Phase { Start, Leg, AtDoor, Pause, Detour, Final, NoRoute, Done }

    Phase _phase = Phase.Start;
    List<GameObject>? _route;
    int _hop;            // index into _route of the next transition
    bool _firstPlan = true;
    /// <summary>False for a move that continues an earlier one (a pursuit taken up again after a transition): its
    /// legs follow the rules of a route planned on the way, not of the first one.</summary>
    public bool FirstPlan { init => _firstPlan = value; }
    int _legInPlan;      // legs walked under the current plan (0 = the area the plan was made in)
    MoveAction? _leg;
    double _until;
    Phase _afterPause;
    bool _lastByTrigger;
    Vector3? _sourceUse; // the use point of the door just taken (coordinates kept for the new area)
    bool _final2;        // the one-door final leg's second part (to the target)
    bool _triggerApproached, _triggerPaused;
    double _arrivedAt = double.NegativeInfinity, _nextClick, _nextBlock;
    bool _inClick;
    float _elapsed;
    /// <summary>Set once a transition has been taken (pursuit reports ACTION_MOVETOPOINT from then on).</summary>
    public bool Transitioned { get; private set; }

    public override int ActionType
    {
        get
        {
            if (_inClick) return 65535;
            if (Kind == Goal.Pursuit && !double.IsNaN(_doorWaitUntil) && _lastWorldTime < _doorWaitUntil) return 3; // ACTION_ATTACKOBJECT
            if (_phase == Phase.NoRoute && _noRouteWaiting) return 36; // ACTION_WAIT
            if (Transitioned && _phase == Phase.Pause && _lastWorldTime - _arrivedAt < ArrivalInvalid) return 65535;
            return 0; // ACTION_MOVETOPOINT
        }
    }
    double _lastWorldTime;
    bool _noRouteWaiting;

    /// <summary>The target's area and position now (an object's current place).</summary>
    (Area? Area, Vector3 Position, GameObject? Object) Target(World world)
    {
        if (Kind == Goal.Location) return (world.Get(TargetLocation.Area) as Area, TargetLocation.Position, null);
        var o = world.Get(TargetObject);
        if (o == null || o.IsDestroyed) return (null, default, null);
        // A player loading the area it went to is already there for whoever follows it.
        var (area, position) = world.Whereabouts(o);
        return (area, position, o);
    }

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        _lastWorldTime = world.GameTime;
        if (owner is not Creature c) return ActionStatus.Failed;
        if (SameArea != null) return SameArea.Step(world, owner, dt);
        // A player loading the next area waits for it.
        if (c.Area == null) return c.Player?.IsAreaLoading == true ? ActionStatus.InProgress : ActionStatus.Failed;
        var (area, position, obj) = Target(world);
        if (area == null)
        {
            // A location in no area: the original ignores the order (a destroyed target object fails the move).
            if (Kind == Goal.Location) return ActionStatus.Done;
            Movement.Stop(c);
            return ActionStatus.Failed;
        }
        _elapsed += dt;
        if (JumpOnTimeout && _elapsed > Timeout)
        {
            Movement.Stop(c);
            world.Jump(c, new Location(area.Id, position, c.FacingDegrees));
            if (c.Actions.Count > 0 && ReferenceEquals(c.Actions[0], this)) c.Actions.Insert(1, new WaitAction { Remaining = 0.75f });
            return ActionStatus.Done;
        }
        switch (_phase)
        {
            case Phase.Start:
                if (area == c.Area && !Transitioned)
                {
                    // Nothing to route: the ordinary same-area move.
                    SameArea = Kind == Goal.Location
                        ? new MoveAction { Target = position, Run = Run, PassClosedDoors = true }
                        : new MoveAction { TargetObject = TargetObject, Run = Run, Range = Range, PassClosedDoors = Kind == Goal.Object };
                    return SameArea.Step(world, owner, dt);
                }
                return Replan(world, c, area, position);
            case Phase.NoRoute:
                if (world.GameTime < _until) return ActionStatus.InProgress;
                if (!_noRouteWaiting) { _noRouteWaiting = true; _until = world.GameTime + 1; return ActionStatus.InProgress; }
                _noRouteWaiting = false;
                return Replan(world, c, area, position);
            case Phase.Pause:
                if (world.GameTime < _until) return ActionStatus.InProgress;
                _phase = _afterPause;
                return AfterPause(world, c, area, position, dt);
            case Phase.Leg:
            case Phase.AtDoor:
                return StepLeg(world, c, area, position, dt);
            case Phase.Detour:
            {
                var status = _leg!.Step(world, c, dt);
                if (status == ActionStatus.InProgress) return status;
                _leg = null;
                if (Kind == Goal.Location) return Finish(c);
                if (area == c.Area) return Finish(c);
                // A short stop, then a fresh route.
                _firstPlan = false;
                Pause(world, DetourPause, Phase.Start);
                return ActionStatus.InProgress;
            }
            case Phase.Final:
                return StepFinal(world, c, area, position, obj, dt);
            default:
                return ActionStatus.Done;
        }
    }

    ActionStatus Finish(Creature c)
    {
        Movement.Stop(c);
        _phase = Phase.Done;
        return ActionStatus.Done;
    }

    void Pause(World world, float seconds, Phase then)
    {
        _phase = Phase.Pause;
        _until = world.GameTime + seconds;
        _afterPause = then;
    }

    ActionStatus Replan(World world, Creature c, Area area, Vector3 position)
    {
        if (area == c.Area)
        {
            _phase = Phase.Final;
            return ActionStatus.InProgress;
        }
        _route = AreaRoutes.Plan(world, c.Area!, c.Position, area);
        _hop = 0;
        _legInPlan = 0;
        if (_route == null || _route.Count == 0)
        {
            // No way there: a location move gives up at once, the others stand and retry.
            if (Kind == Goal.Location) return Finish(c);
            Movement.Stop(c);
            _phase = Phase.NoRoute;
            _noRouteWaiting = false;
            _until = world.GameTime + 1;
            return ActionStatus.InProgress;
        }
        _phase = Phase.Leg;
        _leg = null;
        _triggerApproached = _triggerPaused = false;
        return ActionStatus.InProgress;
    }

    /// <summary>Running applies except on the first plan's leg through the area its first door reached.</summary>
    bool LegRun => Run && !(_firstPlan && _legInPlan == 1);

    ActionStatus StepLeg(World world, Creature c, Area area, Vector3 position, float dt)
    {
        var via = _route![_hop];
        if (via.IsDestroyed || via.Area != c.Area) return Replan(world, c, area, position);
        if (via is Door door)
        {
            if (_phase == Phase.Leg)
            {
                // The creature walks to the door's closed-state use point on its side, stopping as far short as
                // ActionOpenDoor does (measured: 1.05 m for a stock bandit, whether the door is open or not); from the
                // area it set out from it goes through without braking.
                _leg ??= new MoveAction { Target = DoorStand(world, c, door), Run = LegRun, PassClosedDoors = true, Timeout = float.MaxValue, NoBraking = _legInPlan == 0 };
                var status = _leg.Step(world, c, dt);
                // Close enough to the use point, or held up just short of it (by the open leaf): through.
                if (status == ActionStatus.InProgress && DoorService.UsePoint(world, c, door, closedOnly: true) is { } near)
                {
                    float d = Vector2.Distance(Flat(near.Point), Flat(c.Position));
                    _stall = Vector3.DistanceSquared(_stallAt, c.Position) < 1e-4f ? _stall + dt : 0;
                    _stallAt = c.Position;
                    if (d <= near.MinRange + 0.05f || d <= near.MinRange + 1f && _stall >= 0.25f) { _leg.Cancel(world, c); status = ActionStatus.Done; }
                }
                if (status == ActionStatus.InProgress) return status;
                _leg = null;
                if (status == ActionStatus.Failed)
                {
                    // Blocked on the way (a creature, another door): try again next tick, like the original's
                    // repeated OnBlocked.
                    return ActionStatus.InProgress;
                }
                _phase = Phase.AtDoor;
            }
            if (!door.IsOpen)
            {
                // Standing at a closed door: OnBlocked every tick until something opens it.
                if (world.GameTime >= _nextBlock)
                {
                    _nextBlock = world.GameTime + 0.05;
                    c.LastBlockedAt = world.GameTime;
                    int before = c.Actions.Count;
                    DoorService.ReportBlocked(world, c, door);
                    // What OnBlocked queued (the default script's DoDoorAction) goes first; the route waits behind it
                    // (measured: the door opens about 1.1 s after the block, as ActionOpenDoor takes).
                    if (c.Actions.Count > before && c.Actions.Count > 0 && ReferenceEquals(c.Actions[0], this))
                    {
                        var added = c.Actions.GetRange(before, c.Actions.Count - before);
                        c.Actions.RemoveRange(before, added.Count);
                        c.Actions.InsertRange(0, added);
                        _phase = Phase.Leg;
                    }
                }
                // Pushed away from the door meanwhile: walk back.
                if (DoorService.UsePoint(world, c, door, closedOnly: true) is { } use && Vector2.Distance(Flat(use.Point), Flat(c.Position)) > use.MinRange + 1.5f)
                    _phase = Phase.Leg;
                return ActionStatus.InProgress;
            }
            // After the first transition the creature stops at each further door and stands a moment before going
            // through: about 0.4 s, or 0.9 s for a pursuer, which reads ACTION_ATTACKOBJECT meanwhile (measured).
            if (_legInPlan > 0)
            {
                if (double.IsNaN(_doorWaitUntil)) _doorWaitUntil = world.GameTime + (Kind == Goal.Pursuit ? PursuitDoorWait : DoorWait);
                if (world.GameTime < _doorWaitUntil) return ActionStatus.InProgress;
            }
            return Take(world, c, door);
        }
        var trigger = (Trigger)via;
        if (trigger.Contains(c.Position)) return Take(world, c, trigger);
        if (_leg == null)
        {
            var near = NearestOnPolygon(trigger, c.Position);
            // Arriving through a door, the original first stops short of a trigger, then steps into it.
            bool approach = _legInPlan > 0 && !_lastByTrigger;
            if (approach && !_triggerApproached)
            {
                var toward = Flat(c.Position) - Flat(near);
                var back = toward.LengthSquared() > 1e-6f ? Vector2.Normalize(toward) * 0.9f : Vector2.Zero;
                _leg = new MoveAction { Target = near + new Vector3(back, 0), Run = LegRun, Timeout = float.MaxValue };
            }
            else if (approach && !_triggerPaused)
            {
                _triggerPaused = true;
                Pause(world, 0.5f, Phase.Leg);
                return ActionStatus.InProgress;
            }
            else
            {
                // Into the trigger just past its edge (measured: the original slows into it).
                var centre = Centroid(trigger);
                var entry = approach ? near : Crossing(trigger, c.Position, centre) ?? near;
                var inward = Flat(centre) - Flat(entry);
                var goal = inward.LengthSquared() > 0.04f ? entry + new Vector3(Vector2.Normalize(inward) * 0.15f, 0) : centre;
                _leg = new MoveAction { Target = goal, Run = LegRun, Timeout = float.MaxValue };
            }
        }
        var legStatus = _leg.Step(world, c, dt);
        if (trigger.Contains(c.Position)) { _leg = null; return Take(world, c, trigger); }
        if (legStatus != ActionStatus.InProgress)
        {
            _leg = null;
            if (_legInPlan > 0 && !_lastByTrigger && !_triggerApproached) _triggerApproached = true;
            // A location move that came through a door ends at the next transition (measured).
            if (Kind == Goal.Location && Transitioned && _triggerApproached) return Finish(c);
        }
        return ActionStatus.InProgress;
    }

    /// <summary>Takes a transition: its OnClick script if it has one (retried), otherwise the jump.</summary>
    ActionStatus Take(World world, Creature c, GameObject via)
    {
        // A location move that already came through a door stops at the next transition instead (measured).
        if (Kind == Goal.Location && Transitioned) return Finish(c);
        if (via.Script("OnClick").Length > 0)
        {
            Movement.Stop(c);
            if (world.GameTime >= _nextClick)
            {
                _nextClick = world.GameTime + ClickRetry;
                _inClick = true;
                try { world.RunEvent(via, "OnClick", new EventContext().Set("Clicking", c.Id)); }
                finally { _inClick = false; }
            }
            return ActionStatus.InProgress;
        }
        if (TransitionService.ResolveTarget(world, via) is not { Area: not null } target) return Replan(world, c, c.Area!, c.Position);
        var fromArea = c.Area!;
        _sourceUse = via is Door d && DoorService.UsePoint(world, c, d, closedOnly: true) is { } use ? use.Point : null;
        var destination = TransitionService.NpcDestination(world, target);
        Movement.Stop(c);
        _leg = null;
        world.Jump(c, destination);
        if (c.Area == fromArea && c.Player?.IsAreaLoading != true) return ActionStatus.InProgress;
        // A closed destination door opens itself as the creature arrives (nwserver, door_bashed: DS_0_R, whose source
        // DestroyObject had opened without it).
        if (target is Door { IsOpen: false } arrival) DoorService.Open(world, arrival, arrival);
        TransitionService.BringAssociates(world, c, fromArea);
        Transitioned = true;
        _doorWaitUntil = double.NaN;
        _lastByTrigger = via is Trigger;
        _hop++;
        _legInPlan++;
        _arrivedAt = world.GameTime;
        _triggerApproached = _triggerPaused = false;
        Pause(world, ArrivalPause, Phase.Start);
        return ActionStatus.InProgress;
    }

    ActionStatus AfterPause(World world, Creature c, Area area, Vector3 position, float dt)
    {
        if (_phase == Phase.Leg) return StepLeg(world, c, area, position, dt);
        if (_phase != Phase.Start) return ActionStatus.InProgress;
        // Just arrived through a transition (or replanning after a detour).
        if (!Transitioned || _route == null || _hop == 0 && !_lastByTrigger) return Replan(world, c, area, position);
        if (_lastByTrigger)
        {
            // The route is forgotten: walk towards the target's coordinates here first (a location move ends there).
            _route = null;
            if (Kind == Goal.Location)
            {
                _leg = new MoveAction { Target = OnGround(c.Area!, position, c.Position), Timeout = float.MaxValue };
                _phase = Phase.Detour;
                return ActionStatus.InProgress;
            }
            if (area == c.Area)
            {
                _phase = Phase.Final;
                _final2 = true; // straight onto the target
                return ActionStatus.InProgress;
            }
            _leg = new MoveAction { Target = Short(c, OnGround(c.Area!, position, c.Position), 1f), Timeout = float.MaxValue };
            _phase = Phase.Detour;
            return ActionStatus.InProgress;
        }
        if (area == c.Area)
        {
            _phase = Phase.Final;
            _final2 = !(_route.Count == 1 && _firstPlan && _sourceUse != null);
            return ActionStatus.InProgress;
        }
        if (_hop >= _route.Count) return Replan(world, c, area, position);
        _phase = Phase.Leg;
        _leg = null;
        return StepLeg(world, c, area, position, dt);
    }

    ActionStatus StepFinal(World world, Creature c, Area area, Vector3 position, GameObject? obj, float dt)
    {
        if (area != c.Area) return Replan(world, c, area, position);
        if (_leg == null)
        {
            if (!_final2 && _sourceUse is { } use)
                // A pursuer goes all the way to those coordinates (measured), a mover stops its usual range short.
                _leg = new MoveAction { Target = Short(c, OnGround(c.Area!, use, c.Position), Kind == Goal.Pursuit ? 0 : Range), Timeout = float.MaxValue };
            else if (Kind == Goal.Location || obj == null)
                _leg = new MoveAction { Target = position, Timeout = float.MaxValue };
            else if (Kind == Goal.Pursuit && !_firstPlan)
                // Taken up again on the way: to where the target is now, then the attack takes over (measured).
                _leg = new MoveAction { Target = position, Timeout = float.MaxValue };
            else
            {
                // A single door's move keeps the usual range; a longer route ends on the target.
                bool usual = _route is { Count: 1 } && _firstPlan && !_lastByTrigger || !Transitioned;
                _leg = obj is Creature
                    ? new MoveAction { TargetObject = obj.Id, Range = Range, Timeout = float.MaxValue }
                    : usual ? new MoveAction { Target = Short(c, position, Range), Timeout = float.MaxValue }
                    : new MoveAction { Target = position, Timeout = float.MaxValue };
            }
        }
        var status = _leg.Step(world, c, dt);
        if (status == ActionStatus.InProgress) return status;
        _leg = null;
        if (!_final2) { _final2 = true; return ActionStatus.InProgress; }
        Movement.Stop(c);
        _phase = Phase.Done;
        return status == ActionStatus.Failed && Kind != Goal.Location ? ActionStatus.Failed : ActionStatus.Done;
    }

    /// <summary>
    /// Where a creature stands to take a door: short of the door's closed-state use point on its side by the distance
    /// ActionOpenDoor keeps, on the line it comes in on (measured: approached at an angle, the original stops off the
    /// door's axis). When that spot is blocked (by the open leaf) or can't see the use point, straight out along the
    /// door's normal instead.
    /// </summary>
    public static Vector3 DoorStand(World world, Creature c, Door door)
    {
        var use = DoorService.UsePoint(world, c, door, closedOnly: true) ?? (door.Position, 1f);
        var wm = c.Area!.Walkmesh;
        var toward = Flat(c.Position) - Flat(use.Point);
        if (toward.LengthSquared() > 1e-6f)
        {
            var q = Flat(use.Point) + Vector2.Normalize(toward) * use.MinRange;
            if (wm.GroundHeight(q.X, q.Y, use.Point.Z, 1f) is { } qz && wm.Trace(new Vector3(q.X, q.Y, qz), use.Point, 0.9f) >= 0.999f)
                return new Vector3(q.X, q.Y, qz);
        }
        var normal = new Vector2(-MathF.Sin(door.Facing), MathF.Cos(door.Facing));
        var side = Flat(use.Point) - Flat(door.Position);
        if (side.LengthSquared() < 1e-6f) side = toward;
        if (Vector2.Dot(side, normal) < 0) normal = -normal;
        var p = Flat(use.Point) + normal * use.MinRange;
        return new Vector3(p.X, p.Y, wm.GroundHeight(p.X, p.Y, use.Point.Z, 2f) ?? use.Point.Z);
    }

    /// <summary>A point <paramref name="range"/> short of <paramref name="p"/> on the line from the creature.</summary>
    static Vector3 Short(Creature c, Vector3 p, float range)
    {
        var d = Flat(p) - Flat(c.Position);
        float len = d.Length();
        if (len <= range) return c.Position;
        var q = Flat(c.Position) + d / len * (len - range);
        return new Vector3(q.X, q.Y, p.Z);
    }

    /// <summary>The same coordinates in another area, on its ground (or as close as a straight walk gets).</summary>
    static Vector3 OnGround(Area area, Vector3 p, Vector3 from)
    {
        if (area.Walkmesh.GroundHeight(p.X, p.Y, from.Z, 50f) is { } z) return p with { Z = z };
        // Off this area's walkmesh: as far along the line as walking gets (the original stops at the edge).
        float f = area.Walkmesh.Trace(from, p with { Z = from.Z }, 0.9f);
        var q = Vector3.Lerp(from, p with { Z = from.Z }, MathF.Max(0, f - 0.01f));
        return q with { Z = area.Walkmesh.GroundHeight(q.X, q.Y, from.Z, 2f) ?? from.Z };
    }

    static Vector2 Flat(Vector3 v) => new(v.X, v.Y);

    static Vector3 Centroid(Trigger t)
    {
        if (t.Geometry.Count == 0) return t.Position;
        var sum = Vector3.Zero;
        foreach (var p in t.Geometry) sum += p;
        return sum / t.Geometry.Count;
    }

    /// <summary>Where the line from <paramref name="from"/> to <paramref name="to"/> first crosses a trigger's outline.</summary>
    static Vector3? Crossing(Trigger t, Vector3 from, Vector3 to)
    {
        if (t.Geometry.Count < 3) return null;
        Vector2 p = Flat(from), r = Flat(to) - p;
        float best = float.MaxValue;
        for (int i = 0, j = t.Geometry.Count - 1; i < t.Geometry.Count; j = i++)
        {
            Vector2 q = Flat(t.Geometry[j]), sv = Flat(t.Geometry[i]) - q;
            float den = r.X * sv.Y - r.Y * sv.X;
            if (MathF.Abs(den) < 1e-8f) continue;
            var qp = q - p;
            float u = (qp.X * sv.Y - qp.Y * sv.X) / den, v = (qp.X * r.Y - qp.Y * r.X) / den;
            if (u is >= 0 and <= 1 && v is >= 0 and <= 1 && u < best) best = u;
        }
        if (best == float.MaxValue) return null;
        var hit = p + r * best;
        return new Vector3(hit.X, hit.Y, from.Z);
    }

    /// <summary>The point of a trigger's outline nearest to <paramref name="p"/>.</summary>
    static Vector3 NearestOnPolygon(Trigger t, Vector3 p)
    {
        if (t.Geometry.Count < 2) return t.Position;
        var at = Flat(p);
        Vector2 best = Flat(t.Geometry[0]);
        float bestD = float.MaxValue;
        for (int i = 0, j = t.Geometry.Count - 1; i < t.Geometry.Count; j = i++)
        {
            var a = Flat(t.Geometry[j]); var b = Flat(t.Geometry[i]);
            var ab = b - a;
            float u = ab.LengthSquared() > 1e-8f ? Math.Clamp(Vector2.Dot(at - a, ab) / ab.LengthSquared(), 0, 1) : 0;
            var q = a + ab * u;
            float d = Vector2.DistanceSquared(q, at);
            if (d < bestD) { bestD = d; best = q; }
        }
        return new Vector3(best.X, best.Y, p.Z);
    }

    public override void Cancel(World world, GameObject owner)
    {
        SameArea?.Cancel(world, owner);
        _leg?.Cancel(world, owner);
        if (owner is Creature c) Movement.Stop(c);
    }
}
