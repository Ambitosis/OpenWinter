using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Moves to objects and locations in other areas, from the numbers measured on nwserver (parity suites
/// interarea_move and interarea_pursuit, docs/INTER_AREA_MOVEMENT.md). The areas are flat 30 m squares joined by
/// doors and transition triggers, like the suites'.
/// </summary>
public sealed class InterAreaMovementTests
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

        public Area Area(string name, int tiles = 3)
        {
            if (_areas.TryGetValue(name, out var a)) return a;
            a = new Game.Areas.Area { ResRef = name, Tag = name.ToUpperInvariant(), Width = tiles, Height = tiles };
            World.Register(a);
            World.Areas.Add(a);
            float s = tiles * Game.Areas.Area.TileSize;
            a.Walkmesh.Add(new(0, 0, 0), new(s, 0, 0), new(s, s, 0), 4, true);
            a.Walkmesh.Add(new(0, 0, 0), new(s, s, 0), new(0, s, 0), 4, true);
            return _areas[name] = a;
        }

        /// <summary>Two open doors facing each other's LinkedTo, like the suites' door pairs.</summary>
        public (Door From, Door To) Doors(string a, Vector2 at, string b, Vector2 to, string tag, float facingTo = 0, bool open = true)
        {
            var d1 = new Door { Tag = tag, LinkedTo = tag + "_R", LinkedToFlags = 1, IsOpen = open };
            var d2 = new Door { Tag = tag + "_R", LinkedTo = tag, LinkedToFlags = 1, IsOpen = open };
            World.AddToArea(d1, Area(a), new Vector3(at, 0), 0);
            World.AddToArea(d2, Area(b), new Vector3(to, 0), facingTo * MathF.PI / 180);
            return (d1, d2);
        }

        /// <summary>A 4 x 2 m transition trigger centred on <paramref name="at"/>, leading to a waypoint.</summary>
        public Trigger AddTrigger(string a, Vector2 at, string b, Vector2 landing, string tag)
        {
            var wp = Waypoint(b, landing, tag + "_RWP");
            var t = new Trigger { Tag = tag, TriggerType = Game.Objects.Trigger.TypeTransition, LinkedTo = wp.Tag, LinkedToFlags = 2 };
            foreach (var (x, y) in new[] { (-2f, -1f), (2f, -1f), (2f, 1f), (-2f, 1f) }) t.Geometry.Add(new Vector3(at.X + x, at.Y + y, 0));
            World.AddToArea(t, Area(a), new Vector3(at, 0), 0);
            return t;
        }

        public Waypoint Waypoint(string a, Vector2 at, string tag)
        {
            var w = new Waypoint { Tag = tag };
            World.AddToArea(w, Area(a), new Vector3(at, 0), 0);
            return w;
        }

        public Creature Walker(string a, Vector2 at, string tag = "IA_WALK")
        {
            var c = new Creature { Tag = tag, WalkRate = 1.75f, RunRate = 3.5f, AILevel = 2 };
            World.AddToArea(c, Area(a), new Vector3(at, 0), MathF.PI / 2);
            return c;
        }

        /// <summary>Steps the world; each area the walker enters is recorded with the time and place.</summary>
        public List<(string Area, double Time, Vector3 At)> Run(Creature c, float seconds, Func<bool>? until = null)
        {
            var seen = new List<(string, double, Vector3)>();
            var area = c.Area;
            for (int i = 0; i < seconds * 30 && until?.Invoke() != true; i++)
            {
                World.Step(1f / 30);
                if (c.Area != area && c.Area != null) { seen.Add((c.Area.ResRef, World.GameTime, c.Position)); area = c.Area; }
            }
            return seen;
        }
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y));

    [DataFact]
    public void RouteTakesTheFewestAreasAndIgnoresLocks()
    {
        var g = new Graph();
        // Measured (interarea_move RC): through one 80 m area rather than two 20 m ones.
        g.Area("rcl", 8); g.Area("rcr1", 2); g.Area("rcr2", 2);
        g.Doors("rca", new(8, 25), "rcl", new(8, 5), "RC_L");
        g.Doors("rcl", new(8, 75), "rcd", new(8, 5), "RC_LD");
        g.Doors("rca", new(22, 25), "rcr1", new(10, 5), "RC_R");
        g.Doors("rcr1", new(10, 15), "rcr2", new(10, 5), "RC_R1");
        g.Doors("rcr2", new(10, 15), "rcd", new(22, 5), "RC_R2");
        g.Doors("rca", new(15, 25), "rcx", new(10, 5), "RC_X");
        var route = AreaRoutes.Plan(g.World, g.Area("rca"), new Vector3(15, 5, 0), g.Area("rcd"))!;
        Assert.Equal(new[] { "RC_L", "RC_LD" }, route.Select(o => o.Tag));

        // Measured (LA): a locked door's one-area route beats an unlocked two-area one.
        var (locked, _) = g.Doors("laa", new(8, 25), "lab", new(8, 5), "LA_AB", open: false);
        locked.Locked = true;
        g.Doors("laa", new(22, 25), "lac", new(22, 5), "LA_AC");
        g.Doors("lac", new(15, 25), "lab", new(22, 5), "LA_CB");
        Assert.Equal(new[] { "LA_AB" }, AreaRoutes.Plan(g.World, g.Area("laa"), new Vector3(15, 5, 0), g.Area("lab"))!.Select(o => o.Tag));

        // No route (NR), and a door whose link names nothing (ML).
        g.Area("nrz");
        Assert.Null(AreaRoutes.Plan(g.World, g.Area("nra"), Vector3.Zero, g.Area("nrz")));
        var (missing, _) = g.Doors("mla", new(15, 25), "mlb", new(15, 5), "ML_AB");
        missing.LinkedTo = "NOWHERE";
        Assert.Null(AreaRoutes.Plan(g.World, g.Area("mla"), Vector3.Zero, g.Area("mlb")));
    }

    [DataFact]
    public void EqualRoutesGoByTheExitsX()
    {
        var g = new Graph();
        // Measured (route_ties TA): from beside the exit at x=22, the one at x=8 is taken, through a larger area.
        g.Area("tal", 5);
        g.AddTrigger("taa", new(8, 25), "tal", new(8, 7.5f), "TA_L");
        g.AddTrigger("taa", new(22, 25), "tah", new(22, 7.5f), "TA_H");
        g.AddTrigger("tal", new(40, 45), "tac", new(8, 7.5f), "TA_LC");
        g.AddTrigger("tah", new(15, 25), "tac", new(22, 7.5f), "TA_HC");
        var route = AreaRoutes.Plan(g.World, g.Area("taa"), new Vector3(25, 12, 0), g.Area("tac"))!;
        Assert.Equal(new[] { "TA_L", "TA_LC" }, route.Select(o => o.Tag));
    }

    [Fact]
    public void DoorLeafSwingsAwayFromTheOpenerOrToSideTwo()
    {
        // Measured (door_leafside): an opener on the door's +y side (its GetFacing) gets "open 1", one behind "open 2",
        // and a door opened with no creature "open 2".
        var door = new Door { Position = new Vector3(10, 10, 0), Facing = 0 };
        Assert.Equal(1, DoorService.OpenSide(door, new Creature { Position = new Vector3(10.4f, 11, 0) }));
        Assert.Equal(2, DoorService.OpenSide(door, new Creature { Position = new Vector3(9.7f, 9, 0) }));
        Assert.Equal(2, DoorService.OpenSide(door, new Creature { Position = new Vector3(12, 10, 0) }));
        Assert.Equal(2, DoorService.OpenSide(door, door));
        Assert.Equal(2, DoorService.OpenSide(door, null));
        var turned = new Door { Position = new Vector3(10, 10, 0), Facing = MathF.PI / 2 };
        Assert.Equal(1, DoorService.OpenSide(turned, new Creature { Position = new Vector3(9, 10.4f, 0) }));
    }

    [DataFact]
    public void DoorArrivalKeepsTheMeasuredOffsetForEveryFacing()
    {
        var g = new Graph();
        // Measured (PF, PG, MD): (0.4, -1.9) in the destination door's frame for the stock door; without its walkmesh
        // the use point falls back to the door's axis, (0, -1.9).
        foreach (var (facing, expected) in new[] { (0f, new Vector2(15, 3.1f)), (180f, new Vector2(15, 6.9f)), (90f, new Vector2(16.9f, 5)) })
        {
            var (_, to) = g.Doors("pa" + facing, new(15, 25), "pb" + facing, new(15, 5), "P" + facing, facingTo: facing);
            var at = TransitionService.NpcDestination(g.World, to);
            Assert.Equal(to.Area!.Id, at.Area);
            Assert.True(Vector2.Distance(new(at.Position.X, at.Position.Y), expected) < 0.01f, $"{facing}: {at.Position}");
        }
    }

    [DataFact]
    public void ObjectMoveGoesThroughDoorsAndTriggersWithTheMeasuredPauses()
    {
        var g = new Graph();
        // A -door- B -trigger- C -door- D, the goal in D (interarea_move MR, walking).
        g.Doors("a", new(15, 25), "b", new(15, 5), "AB");
        g.AddTrigger("b", new(15, 25), "c", new(15, 7.5f), "BC");
        g.Doors("c", new(15, 25), "d", new(15, 5), "CD");
        var goal = g.Waypoint("d", new(5, 20), "GOAL");
        var w = g.Walker("a", new(15, 5));
        var move = new InterAreaMoveAction { TargetObject = goal.Id };
        w.Actions.Add(move);
        var seen = g.Run(w, 120, () => w.Actions.Count == 0);

        Assert.Equal(new[] { "b", "c", "d" }, seen.Select(s => s.Area));
        // Through a door: 1.9 m outside the destination door; through a trigger: on its waypoint.
        Assert.True(Flat(seen[0].At, new Vector3(15, 3.1f, 0)) < 0.05f, $"door landing {seen[0].At}");
        Assert.True(Flat(seen[1].At, new Vector3(15, 7.5f, 0)) < 0.05f, $"trigger landing {seen[1].At}");
        // A route of several areas ends exactly on the target.
        Assert.Empty(w.Actions);
        Assert.True(Flat(w.Position, goal.Position) < 0.02f, $"ended at {w.Position}");
        Assert.True(move.Transitioned);
    }

    [DataFact]
    public void MovingToAnOpenTransitionDoorTakesIt()
    {
        // doormove (nwmain): ActionMoveToObject at an open transition door takes player and NPC alike through on
        // arrival; with a 3 m range, or the door closed, the mover stops.
        foreach (var (range, open, through) in new[] { (1f, true, true), (3f, true, false), (1f, false, false) })
        {
            var g = new Graph();
            var (door, _) = g.Doors("a", new(15, 25), "b", new(15, 5), "MD", open: open);
            var w = g.Walker("a", new(15, 10));
            w.Actions.Add(new MoveAction { TargetObject = door.Id, Range = range, PassClosedDoors = true, TakeDoorTransition = true });
            var seen = g.Run(w, 20, () => w.Actions.Count == 0);
            Assert.Equal(through ? new[] { "b" } : Array.Empty<string>(), seen.Select(x => x.Area));
        }
    }

    [DataFact]
    public void AScriptedMoveWalksAPlayerThroughATransitionTrigger()
    {
        // doormove2 (nwmain): ActionMoveToLocation through a transition trigger to a point beyond it, or onto it, keeps
        // the player in the area; walking into it herself (a click move here) takes her through.
        foreach (var scripted in new[] { true, false })
        {
            var g = new Graph();
            g.AddTrigger("a", new(15, 20), "b", new(15, 7.5f), "ST");
            var pc = g.Walker("a", new(15, 10), "ST_PC");
            pc.Player = new OpenWinter.Game.PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
            var start = pc.Area;
            pc.Actions.Add(new MoveAction { Target = new Vector3(15, 26, 0), Range = 0.3f, PassClosedDoors = scripted });
            g.Run(pc, 12, () => pc.Actions.Count == 0 || pc.Area != start);
            if (scripted) { Assert.Same(start, pc.Area); Assert.True(Flat(pc.Position, new Vector3(15, 26, 0)) < 0.4f, $"{pc.Position}"); }
            else Assert.NotSame(start, pc.Area);
        }
    }

    [DataFact]
    public void CreatureStandsAfterArrivingAndReadsInvalidAtFirst()
    {
        var g = new Graph();
        g.Doors("a", new(15, 25), "b", new(15, 5), "AB");
        g.Doors("b", new(15, 25), "c", new(15, 5), "BC");
        var goal = g.Waypoint("c", new(15, 20), "GOAL");
        var w = g.Walker("a", new(15, 20));
        w.Actions.Add(new InterAreaMoveAction { TargetObject = goal.Id });
        g.Run(w, 10, () => w.Area!.ResRef == "b");
        double arrived = g.World.GameTime;
        var at = w.Position;
        // Measured: GetCurrentAction reads ACTION_INVALID just after the arrival (and inside the area's OnEnter).
        Assert.Equal(65535, w.Actions[0].ActionType);
        g.Run(w, 5, () => Flat(w.Position, at) > 0.01f);
        Assert.InRange(g.World.GameTime - arrived, InterAreaMoveAction.ArrivalPause - 0.05, InterAreaMoveAction.ArrivalPause + 0.1);
        Assert.Equal(0, w.Actions[0].ActionType);
    }

    [DataFact]
    public void TriggerArrivalDetoursToTheTargetsCoordinatesFirst()
    {
        var g = new Graph();
        // Measured (MT): after a trigger the original walks towards the target's coordinates in the area it is in,
        // stopping 1 m short, then plans again.
        g.AddTrigger("a", new(15, 25), "b", new(15, 7.5f), "AB");
        g.AddTrigger("b", new(15, 25), "c", new(15, 7.5f), "BC");
        var goal = g.Waypoint("c", new(5, 20), "GOAL");
        var w = g.Walker("a", new(15, 5));
        w.Actions.Add(new InterAreaMoveAction { TargetObject = goal.Id, Run = true });
        float closest = float.MaxValue;
        g.Run(w, 60, () =>
        {
            if (w.Area?.ResRef == "b") closest = MathF.Min(closest, Flat(w.Position, goal.Position));
            return w.Area?.ResRef == "c";
        });
        Assert.InRange(closest, 0.9f, 1.1f);
    }

    [DataFact]
    public void LocationMoveEndsAtTheNextTransitionAfterADoor()
    {
        var g = new Graph();
        // Measured (LD): a location three areas away; the walker goes through the first door, then stops at the
        // second one and the move ends.
        g.Doors("a", new(15, 25), "b", new(15, 5), "AB");
        g.Doors("b", new(15, 25), "c", new(15, 5), "BC");
        g.Doors("c", new(15, 25), "d", new(15, 5), "CD");
        var w = g.Walker("a", new(15, 5));
        w.Actions.Add(new InterAreaMoveAction { Kind = InterAreaMoveAction.Goal.Location, TargetLocation = new Location(g.Area("d").Id, new Vector3(5, 20, 0), 0) });
        g.Run(w, 60, () => w.Actions.Count == 0);
        Assert.Empty(w.Actions);
        Assert.Equal("b", w.Area!.ResRef);
        Assert.True(w.Position.Y > 22.5f && w.Position.Y < 24.5f, $"stopped at {w.Position}");
    }

    [DataFact]
    public void NoRouteRetriesForObjectsAndGivesUpForLocations()
    {
        var g = new Graph();
        g.Area("z");
        var goal = g.Waypoint("z", new(15, 20), "GOAL");
        var w = g.Walker("a", new(15, 5));
        var move = new InterAreaMoveAction { TargetObject = goal.Id };
        w.Actions.Add(move);
        // Measured (NR): the move stays queued, 1 s as ACTION_MOVETOPOINT and 1 s as ACTION_WAIT in turn.
        var kinds = new HashSet<int>();
        g.Run(w, 6, () => { if (w.Actions.Count > 0) kinds.Add(w.Actions[0].ActionType); return false; });
        Assert.Single(w.Actions);
        Assert.Equal(new[] { 0, 36 }, kinds.OrderBy(k => k));
        Assert.True(Flat(w.Position, new Vector3(15, 5, 0)) < 0.01f);

        // Measured (NL): a location move ends at once.
        var l = g.Walker("a", new(20, 5), "NL");
        l.Actions.Add(new InterAreaMoveAction { Kind = InterAreaMoveAction.Goal.Location, TargetLocation = goal.Location });
        g.Run(l, 1, () => l.Actions.Count == 0);
        Assert.Empty(l.Actions);
    }

    [DataFact]
    public void ClosedDoorHoldsTheWalkerUntilItOpens()
    {
        var g = new Graph();
        var (door, _) = g.Doors("a", new(15, 25), "b", new(15, 5), "AB", open: false);
        var goal = g.Waypoint("b", new(15, 20), "GOAL");
        var w = g.Walker("a", new(15, 15));
        w.Actions.Add(new InterAreaMoveAction { TargetObject = goal.Id });
        g.Run(w, 15);
        // Measured (LK, CL): it stands at the door, blocked by it, the move still queued.
        Assert.Equal("a", w.Area!.ResRef);
        Assert.Equal(door.Id, w.LastBlockingDoor);
        Assert.Single(w.Actions);
        door.IsOpen = true;
        g.Run(w, 30, () => w.Actions.Count == 0);
        Assert.Equal("b", w.Area!.ResRef);
    }

    [DataFact]
    public void SameAreaWalkAcrossATransitionTriggerDoesNotTakeIt()
    {
        var g = new Graph();
        // Measured (SA): only a route takes a trigger.
        g.AddTrigger("a", new(15, 15), "b", new(15, 15), "T");
        var w = g.Walker("a", new(15, 5));
        w.Actions.Add(new MoveAction { Target = new Vector3(15, 25, 0) });
        g.Run(w, 20, () => w.Actions.Count == 0);
        Assert.Equal("a", w.Area!.ResRef);
    }

    [DataFact]
    public void SummonsJumpWithTheirMaster()
    {
        var g = new Graph();
        g.Doors("a", new(15, 25), "b", new(15, 5), "AB");
        var goal = g.Waypoint("b", new(20, 20), "GOAL");
        var master = g.Walker("a", new(15, 18), "SM");
        var summon = g.Walker("a", new(12, 18), "SMS");
        summon.Master = master.Id;
        summon.AssociateType = Game.Society.AssociateService.Summoned;
        master.Actions.Add(new InterAreaMoveAction { TargetObject = goal.Id });
        var seen = g.Run(master, 10, () => master.Area!.ResRef == "b");
        // Measured: in the same instant, 1 m from its master (towards the door it came through).
        Assert.Same(master.Area, summon.Area);
        Assert.InRange(Flat(summon.Position, master.Position), 0.95f, 1.05f);
        Assert.True(summon.Position.Y > master.Position.Y);
    }

    [DataFact]
    public void AttackerFollowsItsTargetThroughTransitions()
    {
        var g = new Graph();
        // Measured (JB): a target two areas away, never seen; no AI scripts on the attacker.
        g.Doors("a", new(15, 25), "b", new(15, 5), "AB");
        g.Doors("b", new(15, 25), "c", new(15, 5), "BC");
        var target = g.Walker("c", new(20, 20), "T");
        var attacker = g.Walker("a", new(15, 15), "A");
        attacker.Faction = FactionTable.Hostile;
        target.Faction = FactionTable.Commoner;
        var attack = new AttackAction { Target = target.Id };
        attacker.Actions.Add(attack);
        g.Run(attacker, 2);
        Assert.Equal(3, attack.ActionType); // ACTION_ATTACKOBJECT until the first transition
        g.Run(attacker, 30, () => attacker.Area!.ResRef == "b");
        g.Run(attacker, 1);
        Assert.Equal(0, attack.ActionType); // then ACTION_MOVETOPOINT
        g.Run(attacker, 60, () => attacker.Area!.ResRef == "c" && Flat(attacker.Position, target.Position) < 2.5f);
        Assert.Equal("c", attacker.Area!.ResRef);
    }

    [DataFact]
    public void StockBanditWalksBackToItsPostFromTwoAreasAway()
    {
        // Measured on nwserver (interarea_pursuit R2): a bandit with the default nw_c2_default*
        // scripts, taken two areas away from its POST_ waypoint, walks home on its next heartbeat through both doors.
        var g = new Graph();
        g.World.EnableScripts();
        g.Doors("pa", new(15, 25), "pb", new(15, 5), "P_AB");
        g.Doors("pb", new(15, 25), "pc", new(15, 5), "P_BC");
        var post = g.Waypoint("pa", new(15, 15), "POST_IA_POST");
        var npc = g.World.Creatures.FromBlueprint("nw_bandit001")!;
        npc.Tag = "IA_POST";
        npc.AILevel = 2; // areas without players run at a very low level, where the default heartbeat does nothing
        g.World.Inventory.FireSpawnAcquires(npc);
        g.World.AddToArea(npc, g.Area("pa"), post.Position, 0);
        g.World.QueueSpawn(npc);
        g.Run(npc, 8);
        ActionQueues.Clear(g.World, npc);
        g.World.Jump(npc, new Location(g.Area("pc").Id, new Vector3(15, 15, 0), 0));
        var seen = g.Run(npc, 90, () => npc.Area == post.Area && Flat(npc.Position, post.Position) < 0.05f);
        Assert.Equal(new[] { "pb", "pa" }, seen.Select(s => s.Area));
        Assert.True(Flat(npc.Position, post.Position) < 0.05f, $"ended at {npc.Position} in {npc.Area?.ResRef}");
    }

    [DataFact]
    public void PlayersSeeACreatureLeaveTheirAreaAndArriveInIt()
    {
        var g = new Graph();
        g.Doors("a", new(15, 25), "b", new(15, 5), "AB");
        var inB = g.Waypoint("b", new(15, 20), "IN_B");
        var inA = g.Waypoint("a", new(15, 10), "IN_A");
        var server = new GameServer(g.World, new ServerOptions { StartArea = "a", DefaultAppearance = 6, Vault = Net.VaultMode.Local });
        var (client, end) = Net.LoopbackConnection.CreatePair();
        server.Accept(end);
        client.Send(new Net.HelloMessage { PlayerName = "Watcher" }.Write());
        var known = new HashSet<uint>();
        var positions = new Dictionary<uint, List<Vector3>>();
        void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++) server.Tick(1f / 30);
            while (client.TryReceive(out var m))
            {
                var r = new Net.MessageReader(m);
                switch (r.Type)
                {
                    case Net.MessageType.LoadArea: known.Clear(); break;
                    case Net.MessageType.Spawn: { var sp = Net.SpawnMessage.Read(r); known.Add(sp.ObjectId); (positions.TryGetValue(sp.ObjectId, out var l) ? l : positions[sp.ObjectId] = new()).Add(sp.Position); break; }
                    case Net.MessageType.Update: { var u = Net.UpdateMessage.Read(r); if (positions.TryGetValue(u.ObjectId, out var l)) l.Add(u.Position); break; }
                    case Net.MessageType.Despawn: known.Remove(Net.DespawnMessage.Read(r).ObjectId); break;
                }
            }
        }
        Tick(5);
        var pc = server.Sessions.Single().Creature!;
        g.World.Jump(pc, new Location(g.Area("a").Id, new Vector3(25, 10, 0), 0));
        Tick(30);
        var npc = g.Walker("a", new(15, 15), "NPC");
        Tick(5);
        Assert.Contains(npc.Id, known);

        // It walks out through the door: the player in the area it left loses it.
        npc.Actions.Add(new InterAreaMoveAction { TargetObject = inB.Id });
        for (int i = 0; i < 30 * 30 && npc.Area!.ResRef == "a"; i++) Tick();
        Tick(2);
        Assert.Equal("b", npc.Area!.ResRef);
        Assert.DoesNotContain(npc.Id, known);

        // It comes back mid-walk: spawned for the player, then seen moving on.
        g.Run(npc, 30, () => npc.Actions.Count == 0);
        npc.Actions.Add(new InterAreaMoveAction { TargetObject = inA.Id });
        for (int i = 0; i < 40 * 30 && npc.Area!.ResRef == "b"; i++) Tick();
        Tick(2);
        Assert.Contains(npc.Id, known);
        int seenBefore = positions[npc.Id].Count;
        var arrivedAt = positions[npc.Id][^1];
        Tick(4 * 30);
        Assert.True(positions[npc.Id].Count > seenBefore);
        Assert.True(Flat(positions[npc.Id][^1], arrivedAt) > 1f, "the player sees it walk on after arriving");
    }
}
