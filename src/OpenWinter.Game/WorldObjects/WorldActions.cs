using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.WorldObjects;

/// <summary>An action that first walks the owner (if a creature) to a target object (a door's or placeable's use point), then does something there.</summary>
public abstract class ApproachAction : GameAction
{
    public uint Target = GameObject.InvalidId;
    public bool Run;
    public float Range;
    MoveAction? _move;
    bool _arrived;

    protected abstract ActionStatus Arrive(World world, GameObject owner, GameObject target);

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        var target = world.Get(Target);
        if (target == null) return ActionStatus.Failed;
        if (!_arrived)
        {
            if (owner is Creature c && c != target)
            {
                if (target.Area != c.Area) return ActionStatus.Failed;
                _move ??= new MoveAction { TargetObject = Target, Range = Range, Run = Run };
                var status = _move.Step(world, owner, dt);
                if (status == ActionStatus.InProgress) return ActionStatus.InProgress;
                // Doors and furniture block their own footprint: close enough counts.
                if (status == ActionStatus.Failed && Distance(c, target) > Range + 3f) return ActionStatus.Failed;
            }
            _arrived = true;
        }
        return Arrive(world, owner, target);
    }

    static float Distance(GameObject a, GameObject b) => Vector2.Distance(new(a.Position.X, a.Position.Y), new(b.Position.X, b.Position.Y));

    /// <summary>After opening, closing or using something the original stands half a second (ACTION_WAIT) facing it.</summary>
    protected static void Pause(World world, GameObject owner, GameObject target, ApproachAction self)
    {
        if (owner is Creature c && c != target)
        {
            var d = new Vector2(target.Position.X - c.Position.X, target.Position.Y - c.Position.Y);
            if (d.LengthSquared() > 1e-6f) { c.Facing = MathF.Atan2(d.Y, d.X); world.MarkChanged(c); }
        }
        if (owner.Actions.Count > 0 && ReferenceEquals(owner.Actions[0], self))
            owner.Actions.Insert(1, target is Door && owner is Creature
                ? new DoorRecoveryAction { Target = target.Id } : new WaitAction { Remaining = 0.5f });
    }

    public override void Cancel(World world, GameObject owner) => _move?.Cancel(world, owner);
}

/// <summary>
/// ActionOpenDoor: approach, spend one second reaching for the door, then open it and wait half a second.
/// Like the original, a placeable is only walked to.
/// </summary>
public sealed class OpenDoorAction : ApproachAction
{
    double _releaseAt = double.NaN;
    /// <summary>
    /// DoDoorAction(DOOR_ACTION_OPEN) on a door that already stands open runs OnOpen on arrival, without the second of
    /// reaching for it that ActionOpenDoor still spends (nwserver, door_bashed CO_0: 1.0 s for ActionOpenDoor beside it).
    /// </summary>
    public bool NoReachWhenOpen;
    public override int ActionType => double.IsNaN(_releaseAt) ? 5 : 65535;
    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        if (target is Door && owner is Creature)
        {
            if (NoReachWhenOpen && double.IsNaN(_releaseAt) && DoorService.IsOpen(target))
            {
                world.RunEvent(target, "OnOpen", new OpenWinter.Game.Scripting.EventContext().Set("OpenedBy", owner.Id));
                Pause(world, owner, target, this);
                return ActionStatus.Done;
            }
            if (double.IsNaN(_releaseAt))
            {
                if (DoorService.RefuseLockedOpen(world, target, owner)) return ActionStatus.Failed;
                _releaseAt = world.GameTime + 1;
                var direction = target.Position - owner.Position;
                owner.Facing = MathF.Atan2(direction.Y, direction.X);
                world.MarkChanged(owner);
                world.Output?.Notify("object-use", owner, new { target = target.Id, animation = "getmid" });
            }
            if (world.GameTime < _releaseAt - 1e-4) return ActionStatus.InProgress;
            if (DoorService.IsOpen(target))
                world.RunEvent(target, "OnOpen", new OpenWinter.Game.Scripting.EventContext().Set("OpenedBy", owner.Id));
        }
        if (target is Door && !DoorService.Open(world, target, owner)) return ActionStatus.Failed;
        Pause(world, owner, target, this);
        return ActionStatus.Done;
    }
}

/// <summary>An attack on a broken door that was closed again: walk to it and knock it open (DoorService.KnockOpen).</summary>
public sealed class KnockOpenAction : ApproachAction
{
    public override int ActionType => 3; // still the attack (ACTION_ATTACKOBJECT)
    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        if (target is Door d) DoorService.KnockOpen(world, d, owner);
        return ActionStatus.Done;
    }
}

/// <summary>ActionCloseDoor: walk to the door and close it, then stand half a second (a placeable is only walked to).</summary>
public sealed class CloseDoorAction : ApproachAction
{
    public override int ActionType => 6; // ACTION_CLOSEDOOR
    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        if (target is Door && owner is Creature && !DoorService.IsOpen(target))
            world.RunEvent(target, "OnClosed", new OpenWinter.Game.Scripting.EventContext().Set("ClosedBy", owner.Id));
        if (target is Door && !DoorService.Close(world, target, owner)) return ActionStatus.Failed;
        Pause(world, owner, target, this);
        return ActionStatus.Done;
    }
}

/// <summary>Door recovery stays ahead of further door clicks; walking or script cancellation can still interrupt it.</summary>
public sealed class DoorRecoveryAction : GameAction
{
    public uint Target;
    readonly WaitAction _wait = new() { Remaining = .5f };
    public override int ActionType => 36;
    public override ActionStatus Step(World world, GameObject owner, float dt) => _wait.Step(world, owner, dt);

    public static bool Pending(Creature pc, uint target) => pc.Actions.Any(a => a switch
    {
        OpenDoorAction open => open.Target == target,
        CloseDoorAction close => close.Target == target,
        DoorRecoveryAction recovery => recovery.Target == target,
        _ => false,
    });
}

/// <summary>ActionUnlockObject: run to the lock and pick it (or use the key).</summary>
public sealed class UnlockAction : ApproachAction
{
    public UnlockAction() { Run = true; }
    public override int ActionType => 13; // ACTION_OPENLOCK
    /// <summary>Knock: no key or skill needed.</summary>
    public bool Magic;
    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        // NWNX_ON_OBJECT_UNLOCK around the attempt (nwnx_events_creature); a skip leaves the lock alone.
        var ev = Nwnx.NwnxEvent.Begin(world, "NWNX_ON_OBJECT_UNLOCK", owner)?.Add("DOOR", target).Add("THIEVES_TOOL", (GameObject?)null).Add("ACTIVE_PROPERTY_INDEX", 0);
        if (ev?.Before() == true) { ev.Add("ACTION_RESULT", false).After(); return ActionStatus.Failed; }
        bool ok = Magic ? DoorService.Unlock(world, target, null) : DoorService.Unlock(world, target, owner);
        ev?.Add("ACTION_RESULT", ok).After();
        return ok ? ActionStatus.Done : ActionStatus.Failed;
    }
}

/// <summary>ActionLockObject: run to the lock and lock it.</summary>
public sealed class LockAction : ApproachAction
{
    public LockAction() { Run = true; }
    public override int ActionType => 14; // ACTION_LOCK
    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        var ev = Nwnx.NwnxEvent.Begin(world, "NWNX_ON_OBJECT_LOCK", owner)?.Add("DOOR", target);
        if (ev?.Before() == true) { ev.Add("ACTION_RESULT", false).After(); return ActionStatus.Failed; }
        bool ok = DoorService.Lock(world, target, owner);
        ev?.Add("ACTION_RESULT", ok).After();
        return ok ? ActionStatus.Done : ActionStatus.Failed;
    }
}

/// <summary>ActionInteractObject: run to the object and use it (a door opens or closes), then stand half a second.</summary>
public sealed class InteractAction : ApproachAction
{
    public InteractAction() { Run = true; }
    public override int ActionType => 15; // ACTION_USEOBJECT
    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        if (owner is not Creature c) return ActionStatus.Failed;
        bool done = target switch
        {
            Placeable p => DoorService.Use(world, p, c),
            Door d => d.IsOpen ? DoorService.Close(world, d, c) : DoorService.Open(world, d, c),
            _ => false,
        };
        if (!done) return ActionStatus.Failed;
        Pause(world, owner, target, this);
        return ActionStatus.Done;
    }
}

/// <summary>Walks to a door or trigger and takes its area transition.</summary>
public sealed class TransitionAction : ApproachAction
{
    public override int ActionType => 15; // ACTION_USEOBJECT
    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        if (owner is not Creature c) return ActionStatus.Failed;
        if (target is Door { IsOpen: false } d && !DoorService.Open(world, d, c)) return ActionStatus.Failed;
        world.System<TransitionService>().Transition(c, target);
        return ActionStatus.Done;
    }
}

/// <summary>ActionSit: walk to the seat, sit, and stay seated until the action is cleared.</summary>
public sealed class SitAction : ApproachAction
{
    bool _seated;
    public SitAction() { Range = 1.2f; }
    public override int ActionType => 37; // ACTION_SIT

    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        if (owner is not Creature c || target is not Placeable chair) return ActionStatus.Failed;
        if (!_seated)
        {
            if (!DoorService.Sit(world, c, chair)) return ActionStatus.Failed;
            _seated = true;
        }
        // Moving off the seat (player input, a jump) stands the creature up.
        if (c.SittingOn == chair.Id && (c.Area != chair.Area || Vector3.Distance(c.Position, chair.Position) > 0.5f)) DoorService.StandUp(world, c);
        return c.SittingOn == chair.Id ? ActionStatus.InProgress : ActionStatus.Done;
    }

    public override void Cancel(World world, GameObject owner)
    {
        base.Cancel(world, owner);
        if (_seated && owner is Creature c) DoorService.StandUp(world, c);
    }
}

/// <summary>PlaySound / PlaySoundByStrRef queued as an action.</summary>
public sealed class PlaySoundAction : GameAction
{
    public string Sound = "";
    public int StrRef = -1;
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        SoundService.Play(world, owner, Sound, StrRef);
        return ActionStatus.Done;
    }
}
