using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Rules;
using OpenWinter.Game.WorldObjects;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Doors killed by damage, EffectDeath or bashing, from the numbers measured on nwserver (parity suite door_bashed,
/// docs/INTER_AREA_MOVEMENT.md "Killed doors"): a transition door stays, broken and open, and still leads on for
/// players and NPCs alike. The areas are flat 30 m squares joined by a door pair, like the suite's.
/// </summary>
public sealed class BashedDoorTests
{
    sealed class Graph
    {
        public readonly World World;
        readonly Dictionary<string, Area> _areas = new();

        public Graph()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
        }

        public Area Area(string name)
        {
            if (_areas.TryGetValue(name, out var a)) return a;
            a = new Area { ResRef = name, Tag = name.ToUpperInvariant(), Width = 3, Height = 3 };
            World.Register(a);
            World.Areas.Add(a);
            a.Walkmesh.Add(new(0, 0, 0), new(30, 0, 0), new(30, 30, 0), 4, true);
            a.Walkmesh.Add(new(0, 0, 0), new(30, 30, 0), new(0, 30, 0), 4, true);
            return _areas[name] = a;
        }

        /// <summary>A closed door facing 0 with its closed slab (2.1 x 0.3 m, like the stock door's) as walkmesh.</summary>
        public Door Door(string area, Vector2 at, string tag, string linkedTo = "")
        {
            var d = new Door { Tag = tag, LinkedTo = linkedTo, LinkedToFlags = (byte)(linkedTo.Length > 0 ? 1 : 0), HitPoints = 10, CurrentHitPoints = 10 };
            var a = Area(area);
            World.AddToArea(d, a, new Vector3(at, 0), 0);
            d.WalkmeshOwner = (int)d.Id;
            a.Walkmesh.SetClosedDoorOwner(d.WalkmeshOwner);
            a.Walkmesh.Add(new(at.X - 1.05f, at.Y - 0.15f, 1), new(at.X + 1.05f, at.Y - 0.15f, 1), new(at.X + 1.05f, at.Y + 0.15f, 1), 7, false, d.WalkmeshOwner);
            a.Walkmesh.Add(new(at.X - 1.05f, at.Y - 0.15f, 1), new(at.X + 1.05f, at.Y + 0.15f, 1), new(at.X - 1.05f, at.Y + 0.15f, 1), 7, false, d.WalkmeshOwner);
            return d;
        }

        /// <summary>The suite's pair: <paramref name="tag"/> at (15, 25) in a, <paramref name="tag"/>_R at (15, 5) in b.</summary>
        public (Door A, Door B) Pair(string tag = "AB")
            => (Door("a", new(15, 25), tag, tag + "_R"), Door("b", new(15, 5), tag + "_R", tag));

        public Waypoint Waypoint(string area, Vector2 at, string tag)
        {
            var w = new Waypoint { Tag = tag };
            World.AddToArea(w, Area(area), new Vector3(at, 0), 0);
            return w;
        }

        public Creature Walker(string area, Vector2 at, string tag = "WALK")
        {
            var c = new Creature { Tag = tag, WalkRate = 1.75f, RunRate = 3.5f, AILevel = 2 };
            World.AddToArea(c, Area(area), new Vector3(at, 0), MathF.PI / 2);
            return c;
        }

        public void Run(float seconds, Func<bool>? until = null)
        {
            for (int i = 0; i < seconds * 30 && until?.Invoke() != true; i++) World.Step(1f / 30);
        }
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y));

    [DataFact]
    public void KilledTransitionDoorStaysBrokenAndOpenAndOpensItsLinkedDoor()
    {
        var g = new Graph();
        var (a, b) = g.Pair();
        HitPoints.Damage(g.World, a, GameObject.InvalidId, 50, 1);
        // Measured: the same object at once, open, its hit points below zero; the linked door breaks and opens with it,
        // keeping its hit points.
        Assert.True(a.Broken);
        Assert.True(a.IsOpen);
        Assert.Equal(-40, a.CurrentHitPoints);
        Assert.True(b.IsOpen);
        Assert.True(b.Broken);
        Assert.Equal(10, b.CurrentHitPoints);
        // No walkmesh: a walk across the doorway inside its area meets no closed door.
        Assert.Equal(0, a.Area!.Walkmesh.ClosedDoorBetween(new Vector3(15, 23, 0), new Vector3(15, 27, 0), 0.5f));
        g.Run(15);
        Assert.False(a.IsDestroyed);
        Assert.Same(a, g.World.ByTag("AB").Single());
        Assert.Same(a, TransitionService.ResolveTarget(g.World, b));
        Assert.Same(b, TransitionService.ResolveTarget(g.World, a));
        // It does nothing by itself any more (AC_0 closing itself), and a creature's attack doesn't touch it.
        Assert.False(ActionQueues.Accepts(a));
        Assert.True(HitPoints.IsDead(a));
    }

    [DataFact]
    public void EffectDeathBreaksADoorButLeavesItsHitPoints()
    {
        var g = new Graph();
        var (a, _) = g.Pair();
        HitPoints.Kill(g.World, a, GameObject.InvalidId);
        Assert.True(a.Broken);
        Assert.True(a.IsOpen);
        Assert.Equal(10, a.CurrentHitPoints);
    }

    [DataFact]
    public void KilledDoorWithoutATransitionIsRemovedTwoSecondsLater()
    {
        var g = new Graph();
        var plain = g.Door("a", new(10, 15), "PLAIN");
        var nowhere = g.Door("a", new(20, 15), "NOWHERE_DOOR", "NOWHERE");
        HitPoints.Damage(g.World, plain, GameObject.InvalidId, 50, 1);
        HitPoints.Kill(g.World, nowhere, GameObject.InvalidId);
        g.Run(DoorService.BrokenDoorRemoval - 0.2f);
        Assert.False(plain.IsDestroyed);
        Assert.False(nowhere.IsDestroyed);
        g.Run(0.4f);
        Assert.True(plain.IsDestroyed);
        Assert.True(nowhere.IsDestroyed);
    }

    [DataFact]
    public void RoutePlansThroughABashedDoor()
    {
        var g = new Graph();
        var (a, _) = g.Pair();
        // A longer way round through an unlocked pair of doors (the suite's LA case shape).
        g.Door("a", new(25, 25), "AC", "AC_R");
        g.Door("c", new(25, 5), "AC_R", "AC");
        g.Door("c", new(15, 25), "CB", "CB_R");
        g.Door("b", new(25, 5), "CB_R", "CB");
        HitPoints.Damage(g.World, a, GameObject.InvalidId, 50, 1);
        g.Run(5);
        var route = AreaRoutes.Plan(g.World, g.Area("a"), new Vector3(15, 5, 0), g.Area("b"));
        Assert.NotNull(route);
        Assert.Equal(new[] { "AB" }, route!.Select(o => o.Tag));
        var back = AreaRoutes.Plan(g.World, g.Area("b"), new Vector3(15, 15, 0), g.Area("a"));
        Assert.Equal(new[] { "AB_R" }, back!.Select(o => o.Tag));
    }

    [DataFact]
    public void NpcsGoThroughABashedDoorBothWays()
    {
        var g = new Graph();
        var (a, b) = g.Pair();
        HitPoints.Damage(g.World, a, GameObject.InvalidId, 50, 1);
        var goal = g.Waypoint("b", new(15, 20), "GOAL");
        var back = g.Waypoint("a", new(25, 12), "BACK");
        var w = g.Walker("a", new(15, 5));
        w.Actions.Add(new InterAreaMoveAction { TargetObject = goal.Id });
        g.Run(60, () => w.Actions.Count == 0);
        // Measured (ED, EK, OP): through the broken door just as through an open one, landing outside the far door.
        Assert.Equal("b", w.Area!.ResRef);
        Assert.Empty(w.Actions);

        var r = g.Walker("b", new(25, 15), "BACKWARD");
        r.Actions.Add(new InterAreaMoveAction { TargetObject = back.Id });
        var landed = Vector3.Zero;
        g.Run(60, () => { if (r.Area!.ResRef == "a" && landed == Vector3.Zero) landed = r.Position; return r.Actions.Count == 0; });
        // Measured (EDR): into the broken door through its linked door, landing outside the broken door.
        Assert.Equal("a", r.Area!.ResRef);
        Assert.True(Flat(landed, new Vector3(15, 23.1f, 0)) < 0.05f, $"landed at {landed}");
    }

    [DataFact]
    public void AttackerPursuesThroughABashedDoor()
    {
        var g = new Graph();
        var (a, _) = g.Pair();
        HitPoints.Damage(g.World, a, GameObject.InvalidId, 50, 1);
        var target = g.Walker("b", new(20, 20), "T");
        var attacker = g.Walker("a", new(15, 15), "A");
        attacker.Faction = FactionTable.Hostile;
        target.Faction = FactionTable.Commoner;
        attacker.Actions.Add(new AttackAction { Target = target.Id });
        // Measured (PU): the pursuer follows its target through the broken door.
        g.Run(40, () => attacker.Area!.ResRef == "b");
        Assert.Equal("b", attacker.Area!.ResRef);
    }

    [DataFact]
    public void PlayerClicksABashedDoorAndGoesThrough()
    {
        var g = new Graph();
        var (a, b) = g.Pair();
        HitPoints.Damage(g.World, a, GameObject.InvalidId, 50, 1);
        var pc = g.Walker("a", new(15, 15), "PC");
        pc.Player = new PlayerSession { Connection = Net.LoopbackConnection.CreatePair().Item1, Creature = pc };
        // Clicking the doorway of an open transition door takes the transition (docs/DOORS.md); a broken one is open.
        g.World.System<TransitionService>().Click(pc, a);
        g.Run(20, () => pc.Area!.ResRef == "b");
        Assert.Equal("b", pc.Area!.ResRef);
        Assert.Same(b.Area, pc.Area);
    }

    [DataFact]
    public void BashedDoorClosedAgainAndLockedIsKnockedOpenByABash()
    {
        var g = new Graph();
        var (a, b) = g.Pair();
        HitPoints.Damage(g.World, a, GameObject.InvalidId, 50, 1);
        var user = g.Walker("a", new(13, 21), "USER");
        // Measured (CC): a creature can close a broken door again (its linked door closes too) and a script lock it.
        Assert.True(DoorService.Close(g.World, a, user));
        a.Locked = true;
        Assert.False(b.IsOpen);
        // Still no walkmesh (CXX walked straight across a closed broken door).
        Assert.Equal(0, a.Area!.Walkmesh.ClosedDoorBetween(new Vector3(15, 23, 0), new Vector3(15, 27, 0), 0.5f));
        // The original then traps a route walker for good; in OpenWinter a bash knocks it open again.
        var basher = g.Walker("a", new(15, 20), "BASHER");
        basher.Actions.Add(new AttackAction { Target = a.Id });
        g.Run(10, () => a.IsOpen);
        Assert.True(a.IsOpen);
        Assert.True(b.IsOpen);
        Assert.True(a.Broken);
    }

    [DataFact]
    public void DestroyObjectOpensATransitionDoorInsteadOfRemovingIt()
    {
        var g = new Graph();
        var (a, b) = g.Pair();
        var plain = g.Door("a", new(5, 15), "PLAIN");
        // Measured (DS_0, UD): a door with a transition stays and opens, its linked door unchanged; others go.
        Assert.True(DoorService.KeepOnDestroy(g.World, a));
        Assert.True(a.IsOpen);
        Assert.False(a.Broken);
        Assert.False(b.IsOpen);
        Assert.False(DoorService.KeepOnDestroy(g.World, plain));
    }
}
