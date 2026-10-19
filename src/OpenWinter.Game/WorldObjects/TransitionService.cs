using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Area transitions: doors and triggers whose LinkedTo names a door or waypoint
/// (LinkedToFlags 1 = door, 2 = waypoint), or whose target was set with
/// SetTransitionTarget. A player walking into a transition trigger, or clicking
/// a transition door, runs the object's OnClick script if it has one (the script
/// decides, usually jumping the player itself); otherwise the engine jumps the
/// player to the destination.
/// </summary>
public sealed class TransitionService : IWorldSystem
{
    readonly World _world;
    /// <summary>(creature, trigger) pairs where the creature arrived inside the trigger: no bounce back until it leaves.</summary>
    readonly HashSet<(uint Creature, uint Trigger)> _arrivedInside = new();
    /// <summary>Players still loading the area they went to, with the area they left: their associates come when they appear.</summary>
    readonly Dictionary<uint, uint> _associatesWaiting = new();

    public int Order => 10;

    public TransitionService(World world)
    {
        _world = world;
        world.TriggerEntered += OnTriggerEntered;
        world.CreatureMoved += OnMoved;
        world.AreaTransition += OnArrived;
        // A player's associates come with it whenever it changes area, however it goes (measured on nwmain, scene
        // transitfollow: walking into a doorway, JumpToObject and a cross-area ActionMoveToObject alike).
        world.CreatureLeftArea += (c, from) => { if (c.IsPC) BringAssociates(_world, c, from); };
        world.ObjectAdded += o =>
        {
            if (o is Creature c && c.Area is { } area && _associatesWaiting.Remove(c.Id, out var from)
                && _world.Get(from) is Area left && left != area)
                BringAssociates(_world, c, left);
        };
    }

    public void Step(float dt)
    {
        if (_arrivedInside.Count == 0) return;
        _arrivedInside.RemoveWhere(p => _world.Get<Creature>(p.Creature) is not { } c || _world.Get<Trigger>(p.Trigger) is not { } t
            || c.Area != t.Area || !t.Contains(c.Position));
    }

    /// <summary>
    /// A player walking into a transition trigger takes it, unless a script is walking her (ActionMoveToLocation or
    /// ActionMoveToObject): measured on nwmain (visual scenes doormove, doormove2), W held into the trigger took her
    /// through, while a scripted move onto it or through it to a point beyond walked on and stayed in the area, and
    /// ActionMoveToObject(trigger) didn't move her at all.
    /// </summary>
    void OnTriggerEntered(Trigger t, Creature c)
    {
        if (t.TriggerType != Trigger.TypeTransition || !c.IsPC) return;
        if (c.Actions.Count > 0 && c.Actions[0] is MoveAction { PassClosedDoors: true }) return;
        if (_arrivedInside.Remove((c.Id, t.Id))) return;
        Transition(c, t);
    }

    /// <summary>
    /// A player steering by keyboard into an open transition door's doorway takes it, as a click on the open doorway
    /// does; a closed door only stops it. Measured on nwmain (scene transitfollow, posted W keys): walking at the
    /// Graduation Chamber's open doorway the player went through when its centre reached the door's line (0.02 m short
    /// of it in the last reading before it left); at the same doorway closed it stopped 0.27 m short and the door
    /// stayed shut. Transition triggers are walked into in the same way (<see cref="OnTriggerEntered"/>).
    /// </summary>
    void OnMoved(Creature c, Vector3 before)
    {
        if (!c.IsPC || c.Area is not { } area || c.Actions.Count > 0 || c.MoveGoal != null || c.MoveDirection == Vector3.Zero) return;
        foreach (var d in area.Doors)
        {
            if (d.IsDestroyed || !d.IsOpen || !(d.IsTransition || d.Script("OnClick").Length > 0)) continue;
            if (!IntoDoorway(d, before, c.Position)) continue;
            Transition(c, d);
            return;
        }
    }

    /// <summary>How close to a doorway's line a step must end (moving towards it) to count as walking into it.</summary>
    const float DoorwayReach = 0.25f;
    readonly Dictionary<string, float> _doorHalfWidths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a step from <paramref name="from"/> to <paramref name="to"/> reaches or crosses the door's line within its width.</summary>
    bool IntoDoorway(Door d, Vector3 from, Vector3 to)
    {
        if (MathF.Abs(to.Z - d.Position.Z) > 2f) return false;
        float cos = MathF.Cos(d.Facing), sin = MathF.Sin(d.Facing);
        // The door's frame: x along the leaf, y out of the doorway (as in NpcDestination).
        (float X, float Y) Local(Vector3 p) { var r = p - d.Position; return (r.X * cos + r.Y * sin, -r.X * sin + r.Y * cos); }
        var (x0, y0) = Local(from);
        var (x1, y1) = Local(to);
        bool crossed = y0 != 0 && MathF.Sign(y0) != MathF.Sign(y1);
        bool reached = MathF.Abs(y1) <= DoorwayReach && MathF.Abs(y1) < MathF.Abs(y0);
        if (!crossed && !reached) return false;
        float x = crossed ? x0 + (x1 - x0) * (y0 / (y0 - y1)) : x1;
        return MathF.Abs(x) <= DoorHalfWidth(d);
    }

    /// <summary>Half the width of a door's closed leaf (its walkmesh's closed part), 1 m without one.</summary>
    float DoorHalfWidth(Door d)
    {
        if (_world.Loader == null) return 1f;
        string model = _world.Loader.DoorModel(d);
        if (_doorHalfWidths.TryGetValue(model, out var half)) return half;
        half = 1f;
        if (_world.Models.GetWalkmesh(model, Aurora.Resources.ResType.Dwk) is { } dwk)
        {
            var globals = dwk.RestGlobals();
            for (int i = 0; i < dwk.Parts.Count; i++)
                if (dwk.Parts[i].Mesh is { Vertices.Length: > 0 } mesh && dwk.Parts[i].Name.Contains("closed", StringComparison.OrdinalIgnoreCase))
                    half = mesh.Vertices.Max(v => MathF.Abs(Vector3.Transform(v, globals[i]).X));
        }
        return _doorHalfWidths[model] = half;
    }

    void OnArrived(GameObject o, Area area)
    {
        if (o is not Creature c) return;
        foreach (var t in area.Objects.OfType<Trigger>())
            if (t.TriggerType == Trigger.TypeTransition && t.Contains(c.Position)) _arrivedInside.Add((c.Id, t.Id));
    }

    /// <summary>Takes a transition: the OnClick script decides if set, otherwise jump to the destination.</summary>
    public void Transition(Creature c, GameObject transition)
    {
        if (transition.Script("OnClick").Length > 0)
        {
            _world.RunEvent(transition, "OnClick", new EventContext().Set("Clicking", c.Id));
            return;
        }
        if (ResolveTarget(_world, transition) is not { } target) return;
        // An NPC lands where a creature following a route through the transition does (doormove: 0.2 m from the
        // player's spot).
        var destination = c.IsPC ? Destination(_world, target) : NpcDestination(_world, target);
        if (destination.Area == GameObject.InvalidId) return;
        ActionQueues.Clear(_world, c);
        c.MoveDirection = Vector3.Zero;
        var from = c.Area;
        _world.Jump(c, destination);
        if (from != null) BringAssociates(_world, c, from);
    }

    /// <summary>
    /// A creature's associates (summons, henchmen, familiars, companions, the dominated) in the area it just left jump
    /// with it, however far from it they were.
    /// <list type="bullet">
    /// <item>An NPC master's land 1 m in front of it (measured on nwserver with a summoned creature, parity suite
    /// interarea_pursuit: in the same instant, 1 m from its master).</item>
    /// <item>A player's appear when the player does, once its client has loaded the area, on the player's spot moved
    /// clear of it (measured on nwmain, scene transitfollow: a henchman, familiar, summon and dominated creature 0.6 to
    /// 1.1 m round the player, in the same instant; a summon 14 m and a henchman 24 m away came too).</item>
    /// </list>
    /// </summary>
    public static void BringAssociates(World world, Creature master, Area from)
    {
        if (master.Area == null && master.Player?.IsAreaLoading == true)
        {
            world.System<TransitionService>()._associatesWaiting[master.Id] = from.Id;
            return;
        }
        if (master.Area is not { } area || area == from) return;
        var at = master.IsPC ? master.Position : master.Position + new Vector3(MathF.Cos(master.Facing), MathF.Sin(master.Facing), 0);
        foreach (var a in Society.AssociateService.AllAssociates(world, master))
        {
            if (a.Area != from || a.IsDead || a.IsDestroyed) continue;
            ActionQueues.Clear(world, a);
            Movement.Stop(a);
            world.Jump(a, new Location(area.Id, at, master.FacingDegrees));
        }
    }

    /// <summary>
    /// Where a creature following a route lands. Through a door: on the destination door's outer use point side
    /// (the walkmesh's dp_closed node with negative local Y), 1 m further out, facing the door; measured on nwserver
    /// with destination doors facing 0, 90 and 180 degrees (parity suite interarea_move): (0.4, -1.9) in the door's
    /// frame for the stock generic door. Through a trigger: the target waypoint's location.
    /// </summary>
    public static Location NpcDestination(World world, GameObject target) =>
        target is Door d ? DoorLanding(world, d, InterAreaMoveAction.DoorArrivalOut, facingAway: false) : target.Area == null ? Location.Invalid : target.Location;

    /// <summary>
    /// The spot on a door's outer side, <paramref name="outward"/> past its closed-state use point (the dp_closed node
    /// with negative local Y; 0.9 m out on the door's axis without a walkmesh), facing the door or away from it.
    /// </summary>
    static Location DoorLanding(World world, Door d, float outward, bool facingAway)
    {
        if (d.Area is not { } area) return Location.Invalid;
        var local = new Vector2(0, -0.9f);
        if (world.Loader != null && world.Models.GetWalkmesh(world.Loader.DoorModel(d), Aurora.Resources.ResType.Dwk) is { } dwk)
        {
            var globals = dwk.RestGlobals();
            for (int i = 0; i < dwk.Parts.Count; i++)
                if (dwk.Parts[i].Mesh == null && dwk.Parts[i].Name.Contains("dp_closed", StringComparison.OrdinalIgnoreCase)
                    && globals[i].Translation.Y < 0)
                    local = new Vector2(globals[i].Translation.X, globals[i].Translation.Y);
        }
        local.Y -= outward;
        float cos = MathF.Cos(d.Facing), sin = MathF.Sin(d.Facing);
        var p = d.Position + new Vector3(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos, 0);
        if (area.Walkmesh.GroundHeight(p.X, p.Y, d.Position.Z, 2f) is { } z) p.Z = z;
        var look = facingAway ? p - d.Position : d.Position - p;
        float facing = MathF.Atan2(look.Y, look.X) * 180f / MathF.PI;
        return new Location(area.Id, p, facing < 0 ? facing + 360f : facing);
    }

    /// <summary>
    /// The transition target of an object: explicit (SetTransitionTarget) or the LinkedTo tag. A found link is
    /// remembered; a tag that finds nothing is looked up again next time, so a destination a script creates later
    /// still works.
    /// </summary>
    public static GameObject? ResolveTarget(World world, GameObject o)
    {
        if (!o.TransitionResolved)
        {
            var (tag, flags) = o switch
            {
                Door d => (d.LinkedTo, d.LinkedToFlags),
                Trigger t => (t.LinkedTo, t.LinkedToFlags),
                _ => ("", (byte)0),
            };
            if (tag.Length > 0 && o.TransitionTargetId == GameObject.InvalidId)
            {
                var candidates = world.ByTag(tag);
                var found = flags switch
                {
                    1 => candidates.FirstOrDefault(x => x is Door),
                    2 => candidates.FirstOrDefault(x => x is Waypoint),
                    _ => null,
                } ?? candidates.FirstOrDefault(x => x is Door or Waypoint) ?? candidates.FirstOrDefault();
                if (found != null)
                {
                    o.TransitionTargetId = found.Id;
                    o.TransitionResolved = true;
                }
            }
            else o.TransitionResolved = true;
        }
        return world.Get(o.TransitionTargetId);
    }

    /// <summary>
    /// Where a player taking a transition lands (and where JumpToObject puts a creature jumped to a door): a waypoint's
    /// location; through a door, on its outer side 0.8 m past its closed-state use point, facing away from the door.
    /// Measured on nwmain (scene transitfollow) with the Prelude's T_DOOR01 doorways facing 90 and 180 degrees: (0.37,
    /// -1.70) in the door's frame (its dp_closed_01 is (0.37, -0.9)), facing straight away from the door's centre,
    /// whether the player walked into the doorway or was sent by JumpToObject.
    /// </summary>
    public static Location Destination(World world, GameObject target) =>
        target is Door d ? DoorLanding(world, d, PlayerDoorArrivalOut, facingAway: true) : target.Area == null ? Location.Invalid : target.Location;

    /// <summary>How far past a door's outer use point a player lands (measured, see <see cref="Destination"/>).</summary>
    public const float PlayerDoorArrivalOut = 0.8f;

    /// <summary>
    /// A player clicked a door, placeable or transition trigger (client "use"): doors toggle
    /// or transition, placeables are used, transition triggers are walked into.
    /// </summary>
    // TODO(client): GameServer should call this for ActionKind.Use requests.
    public void Click(Creature c, GameObject target)
    {
        switch (target)
        {
            // A closed transition door opens on the first click; clicking the open doorway transitions.
            case Door d when d.IsOpen && (d.IsTransition || d.Script("OnClick").Length > 0):
                c.Actions.Add(new TransitionAction { Target = d.Id });
                break;
            case Door d:
                c.Actions.Add(d.IsOpen ? new CloseDoorAction { Target = d.Id } : new OpenDoorAction { Target = d.Id });
                break;
            case Placeable p:
                _world.RunEvent(p, "OnClick", new EventContext().Set("Clicking", c.Id));
                if (p.Useable) c.Actions.Add(new InteractAction { Target = p.Id, Range = 1.5f });
                break;
            case Trigger t when t.TriggerType == Trigger.TypeTransition:
                c.Actions.Add(new TransitionAction { Target = t.Id, Range = 1f });
                break;
            case Trigger t:
                _world.RunEvent(t, "OnClick", new EventContext().Set("Clicking", c.Id));
                break;
        }
    }
}
