using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Creatures keeping apart (PersonalSpace), measured on the original server: parity suites coll_place, coll_walls,
/// coll_move and coll_doors.
/// </summary>
public sealed class PersonalSpaceTests
{
    const int Goblin = 86, Fairy = 55, Ogre = 127; // appearance.2da: PERSPACE 0.3/0.1/0.4, CREPERSPACE 0.4/0.1/1.1

    /// <summary>A world with one flat 40 m area whose floor covers x from <paramref name="floorFrom"/> to <paramref name="floorTo"/>.</summary>
    static (World World, Area Area) MakeWorld(float floorFrom = 0, float floorTo = 40)
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        var area = new Area { ResRef = "space_test", Tag = "space_test", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        area.Walkmesh.Add(new(floorFrom, 0, 0), new(floorTo, 0, 0), new(floorTo, 40, 0), 4, true);
        area.Walkmesh.Add(new(floorFrom, 0, 0), new(floorTo, 40, 0), new(floorFrom, 40, 0), 4, true);
        return (world, area);
    }

    static Creature Add(World world, Area area, int appearance, float x, float y, string tag = "")
    {
        var c = new Creature { Tag = tag, AppearanceType = appearance, CurrentHitPoints = 10, MaxHitPoints = 10 };
        world.AddToArea(c, area, new Vector3(x, y, 0), 0);
        return c;
    }

    static Vector2 Offset(Creature c, float x, float y) => new(MathF.Round((c.Position.X - x) * 100) / 100, MathF.Round((c.Position.Y - y) * 100) / 100);

    static void Perceive(params Creature[] all)
    {
        foreach (var a in all)
            foreach (var b in all)
                if (a != b) a.Society.Seen.Add(b.Id);
    }

    static void Run(World world, float seconds)
    {
        for (float t = 0; t < seconds; t += 1 / 30f) world.Step(1 / 30f);
    }

    [DataFact]
    public void FreeSpotsAreSearchedInRingsTurningClockwiseFromNorth()
    {
        var (world, area) = MakeWorld();
        // Fairies (0.1 m apart steps, 0.2 m apart): each new one takes the next free point of the search.
        var expected = new[] { (0, 0), (0, 2), (2, 0), (0, -2), (-2, 0), (2, 2), (2, -2), (-2, -2), (-2, 2), (1, 4), (4, -1), (-1, -4), (-4, 1), (-1, 4), (4, 1), (1, -4), (-4, -1) };
        foreach (var (x, y) in expected)
            Assert.Equal(new Vector2(x / 10f, y / 10f), Offset(Add(world, area, Fairy, 20, 20), 20, 20));
    }

    [DataFact]
    public void CreaturesOnATakenSpotMoveToTheFirstFreeOne()
    {
        var (world, area) = MakeWorld();
        Add(world, area, Goblin, 4, 4);
        Assert.Equal(new Vector2(0.6f, 0.6f), Offset(Add(world, area, Goblin, 4, 4), 4, 4));
        Assert.Equal(new Vector2(0.6f, -0.6f), Offset(Add(world, area, Goblin, 4, 4), 4, 4));
        // Exactly personal space away is free.
        Add(world, area, Goblin, 10, 4);
        Assert.Equal(new Vector2(0.3f, 0), Offset(Add(world, area, Goblin, 10.5f, 4), 10.5f, 4));
        // The step is the new creature's PERSPACE, the distance the sum of both CREPERSPACE.
        Add(world, area, Fairy, 16, 4);
        Assert.Equal(new Vector2(0, 1.2f), Offset(Add(world, area, Ogre, 16, 4), 16, 4));
        Add(world, area, Ogre, 22, 4);
        Assert.Equal(new Vector2(0.9f, 1.2f), Offset(Add(world, area, Goblin, 22.1f, 4), 22.1f, 4));
        // Jumping onto a taken spot is the same search.
        var target = Add(world, area, Goblin, 28, 4);
        var jumper = Add(world, area, Goblin, 28, 10);
        world.Jump(jumper, target.Location);
        Assert.Equal(new Vector2(0.6f, 0.6f), Offset(jumper, 28, 4));
    }

    [DataFact]
    public void CreaturesKeepFromWallsTwiceAsFarEastAsWest()
    {
        // Floor from x = 10 to x = 30 only.
        var (world, area) = MakeWorld(10, 30);
        // A goblin (PERSPACE 0.3) needs 0.6 m to a wall on its east...
        Assert.Equal(new Vector2(-0.3f, 0), Offset(Add(world, area, Goblin, 29.5f, 10), 29.5f, 10));
        Assert.Equal(new Vector2(0, 0), Offset(Add(world, area, Goblin, 29.4f, 15), 29.4f, 15));
        // ... and 0.3 m (and a centimetre) to one on its west.
        Assert.Equal(new Vector2(0.3f, 0), Offset(Add(world, area, Goblin, 10.2f, 20), 10.2f, 20));
        Assert.Equal(new Vector2(0, 0), Offset(Add(world, area, Goblin, 10.35f, 25), 10.35f, 25));
        // Off the walkmesh altogether: the nearest point it fits, on the ground.
        var off = new Creature { AppearanceType = Goblin };
        world.AddToArea(off, area, new Vector3(5, 30, 3), 0);
        Assert.Equal(10.4f, off.Position.X, 3);
        Assert.Equal(0f, off.Position.Z);
    }

    [DataFact]
    public void AWalkerPushesIdleCreaturesOutOfItsWay()
    {
        var (world, area) = MakeWorld();
        var a = Add(world, area, Goblin, 14, 5);
        var straight = Add(world, area, Goblin, 20, 5);
        var b = Add(world, area, Goblin, 14, 12);
        var aside = Add(world, area, Goblin, 20, 12.3f);
        Perceive(a, straight, b, aside);
        a.Actions.Add(new MoveAction { Target = new Vector3(30, 5, 0), Run = true });
        b.Actions.Add(new MoveAction { Target = new Vector3(30, 12, 0), Run = true });
        Run(world, 6);
        // Straight ahead: pushed on by personal space plus 0.1 m, again and again, to end up in front of the walker.
        Assert.Equal(5f, straight.Position.Y, 3);
        float pushes = (straight.Position.X - 20) / 0.9f;
        Assert.Equal(MathF.Round(pushes), pushes, 2);
        Assert.InRange(straight.Position.X, 30.7f, 31.7f);
        // Beside the line: moved sideways to 0.9 m off it.
        Assert.Equal(20f, aside.Position.X, 3);
        Assert.Equal(12.9f, aside.Position.Y, 3);
        Assert.Equal(30f, a.Position.X, 2);
        Assert.Equal(30f, b.Position.X, 2);
        Assert.Empty(a.Actions);
    }

    [DataFact]
    public void BumpingUsesTheBlockersRelationshipAndPreservesFacingAndQueuedWait()
    {
        var (world, area) = MakeWorld();
        var mover = Add(world, area, Goblin, 10, 10);
        var blocker = Add(world, area, Goblin, 10.9f, 10);
        var (_, connection) = OpenWinter.Net.LoopbackConnection.CreatePair();
        mover.Player = new PlayerSession { Connection = connection };
        mover.Faction = 0;
        blocker.Faction = 3;
        blocker.Facing = 1.2f;
        Perceive(mover, blocker);
        var origin = blocker.Position;
        var next = mover.Position + new Vector3(0.2f, 0, 0);
        world.Factions.SetFactionReputation(0, 3, 100);
        world.Factions.SetFactionReputation(3, 0, 50);
        Assert.Same(blocker, PersonalSpace.Step(world, mover, next));
        Assert.Equal(origin, blocker.Position);

        world.Factions.SetFactionReputation(0, 3, 50);
        world.Factions.SetFactionReputation(3, 0, 100);
        var wait = new WaitAction { Remaining = 60 };
        blocker.Actions.Add(wait);
        var output = new BumpOutput();
        world.Output = output;
        Assert.Null(PersonalSpace.Step(world, mover, next));
        Assert.NotEqual(origin, blocker.Position);
        Assert.Equal(1.2f, blocker.Facing);
        Assert.Same(wait, Assert.Single(blocker.Actions));
        var notice = Assert.Single(output.Events);
        Assert.Same(blocker, notice.Subject);
        using var json = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(notice.Data));
        Assert.Equal(origin.X, json.RootElement.GetProperty("from")[0].GetSingle());
        Assert.Equal(blocker.Position.X, json.RootElement.GetProperty("to")[0].GetSingle());
        Assert.Equal(blocker.Facing, json.RootElement.GetProperty("facing").GetSingle());
        output.Events.Clear();
        world.Jump(blocker, new Location(area.Id, new Vector3(20, 20, 0), 0));
        Assert.Empty(output.Events);
    }

    sealed class BumpOutput : IWorldOutput
    {
        public readonly List<(GameObject? Subject, object Data)> Events = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null)
        {
            if (kind == "creature-bump") Events.Add((subject, data));
        }
    }

    [DataFact]
    public void WalkingIntoABusyCreatureStopsTheWalker()
    {
        var (world, area) = MakeWorld();
        var a = Add(world, area, Goblin, 14, 5);
        var b = Add(world, area, Goblin, 20, 5);
        Perceive(a, b);
        a.Actions.Add(new MoveAction { Target = new Vector3(30, 5, 0) });
        Run(world, 0.2f);
        // Busy after the walker planned its path.
        b.Actions.Add(new WaitAction { Remaining = 60 });
        Run(world, 5);
        Assert.Empty(a.Actions);
        Assert.Equal(b.Id, a.LastBlockingDoor);
        Assert.InRange(Vector3.Distance(a.Position, b.Position), 0.79f, 0.9f);
        Assert.Equal(new Vector3(20, 5, 0), b.Position);
    }

    [DataFact]
    public void PathsGoRoundBusyCreatures()
    {
        var (world, area) = MakeWorld();
        var a = Add(world, area, Goblin, 14, 5);
        var b = Add(world, area, Goblin, 20, 5);
        Perceive(a, b);
        b.Actions.Add(new WaitAction { Remaining = 60 });
        a.Actions.Add(new MoveAction { Target = new Vector3(28, 5, 0) });
        float closest = float.MaxValue;
        for (int i = 0; i < 30 * 10; i++)
        {
            world.Step(1 / 30f);
            closest = MathF.Min(closest, Vector3.Distance(a.Position, b.Position));
        }
        Assert.True(Vector3.Distance(new Vector3(28, 5, 0), a.Position) < 0.01f);
        Assert.InRange(closest, 0.8f, 0.95f);
        Assert.Equal(GameObject.InvalidId, a.LastBlockingDoor);
    }

    [DataFact]
    public void PathsGoRoundABusyCreatureBesideTheGoal()
    {
        // The goal lies inside the busy creature's planning circle (personal spaces plus 0.05 m) but outside
        // bumping range: the route still goes round it rather than dropping it and walking into it.
        var (world, area) = MakeWorld();
        var a = Add(world, area, Goblin, 14, 5);
        var b = Add(world, area, Goblin, 20, 5);
        Perceive(a, b);
        b.Actions.Add(new WaitAction { Remaining = 60 });
        var goal = new Vector3(20.82f, 5.1f, 0);
        a.Actions.Add(new MoveAction { Target = goal, Run = true });
        float closest = float.MaxValue;
        for (int i = 0; i < 30 * 6; i++)
        {
            world.Step(1 / 30f);
            closest = MathF.Min(closest, Vector3.Distance(a.Position, b.Position));
        }
        Assert.True(Vector3.Distance(goal, a.Position) < 0.01f);
        Assert.Equal(GameObject.InvalidId, a.LastBlockingDoor);
        Assert.InRange(closest, 0.8f, 0.9f);
        Assert.Equal(new Vector3(20, 5, 0), b.Position);
    }

    [DataFact]
    public void ObjectApproachesGoRoundACreatureStandingOnTheStoppingPoint()
    {
        // Like Bim between the player and the academy door: the stopping point 1.8 m short of the object is taken
        // by a busy creature, so the walker stops on a free side instead of walking into it.
        var (world, area) = MakeWorld();
        var a = Add(world, area, Goblin, 14, 5);
        var b = Add(world, area, Goblin, 22.2f, 5);
        Perceive(a, b);
        b.Actions.Add(new WaitAction { Remaining = 60 });
        var target = new Waypoint { Tag = "target" };
        world.AddToArea(target, area, new Vector3(24, 5, 0), 0);
        a.Actions.Add(new MoveAction { TargetObject = target.Id, Run = true, Range = 1.8f });
        Run(world, 6);
        Assert.Empty(a.Actions);
        Assert.Equal(GameObject.InvalidId, a.LastBlockingDoor);
        Assert.InRange(Vector2.Distance(new(a.Position.X, a.Position.Y), new(24, 5)), 1.75f, 1.81f);
        Assert.True(Vector3.Distance(a.Position, b.Position) >= 0.8f);
        Assert.Equal(new Vector3(22.2f, 5, 0), b.Position);
    }

    [DataFact]
    public void ThePlayerWalksRoundBimToTheAcademyDoor()
    {
        // In the Prelude's first room, neutral Bim stands between the player and the door.
        // The door's stopping point lies within Bim's planning circle; the player used to walk into him and stop.
        var world = new World(ResMan.ForModule(TestData.Paths, Path.Combine(TestData.Config!.GameRoot, "data/nwm/Prelude.nwm")));
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { DefaultAppearance = 6, Vault = OpenWinter.Net.VaultMode.Local });
        var pair = OpenWinter.Net.LoopbackConnection.CreatePair(); server.Accept(pair.Server);
        pair.Client.Send(new OpenWinter.Net.HelloMessage { PlayerName = "Bim tester" }.Write());
        void Tick(int n) { for (int i = 0; i < n; i++) server.Tick(1f / 30); while (pair.Client.TryReceive(out _)) { } }
        Tick(5);
        var pc = server.Sessions.Single().Creature!;
        var area = world.FindArea("m0q1a")!;
        var bim = area.Objects.OfType<Creature>().Single(c => c.Tag == "M1Q0ABim");
        var door = area.Objects.OfType<Door>().Single(d => d.Tag == "M1Q0A_M1Q0B");
        world.Jump(pc, new Location(area.Id, new Vector3(21, 16.5f, 0), 0));
        Tick(60);
        Assert.False(world.Factions.IsFriend(bim, pc));
        Assert.Contains(bim.Id, pc.Society.Seen);
        var bimAt = bim.Position;
        pair.Client.Send(new OpenWinter.Net.ActionRequestMessage { Action = OpenWinter.Net.ActionKind.Use, Target = door.Id }.Write());
        float closest = float.MaxValue;
        for (int i = 0; i < 30 * 5; i++)
        {
            Tick(1);
            closest = MathF.Min(closest, Vector2.Distance(new(pc.Position.X, pc.Position.Y), new(bimAt.X, bimAt.Y)));
            Assert.Equal(GameObject.InvalidId, pc.LastBlockingDoor);
        }
        Assert.Equal(bimAt, bim.Position);
        Assert.True(closest >= 1f - 1e-3f, $"came within {closest} m of Bim");
        // At the door (locked until Pavel is spoken to): the open attempt ran and the queue emptied.
        Assert.Empty(pc.Actions);
        Assert.InRange(Vector2.Distance(new(pc.Position.X, pc.Position.Y), new(door.Position.X, door.Position.Y)), 0, 2.5f);
    }

    [DataFact]
    public void OnlyPerceivedCreaturesAndNoGhostsBump()
    {
        var (world, area) = MakeWorld();
        // Not perceived yet: walks straight through.
        var a = Add(world, area, Goblin, 14, 5);
        var b = Add(world, area, Goblin, 20, 5);
        a.Actions.Add(new MoveAction { Target = new Vector3(26, 5, 0), Run = true });
        Run(world, 4);
        Assert.Equal(new Vector3(20, 5, 0), b.Position);
        Assert.Equal(26f, a.Position.X, 2);
        // A cutscene ghost walks through what it perceives.
        var g = Add(world, area, Goblin, 14, 10);
        var c = Add(world, area, Goblin, 20, 10);
        Perceive(g, c);
        g.Effects.Add(new OpenWinter.Game.Scripting.Effect { Type = PersonalSpace.EffectCutsceneGhost });
        g.Actions.Add(new MoveAction { Target = new Vector3(26, 10, 0), Run = true });
        Run(world, 4);
        Assert.Equal(new Vector3(20, 10, 0), c.Position);
        Assert.Equal(26f, g.Position.X, 2);
    }

    [DataFact]
    public void AnOpenDoorsLeafBlocksTheWay()
    {
        var (world, area) = MakeWorld();
        var door = new Door { WalkmeshOwner = 5 };
        world.AddToArea(door, area, new Vector3(20, 20, 0), 0);
        // The closed slab across x 19..21, the leaf swung north (side 2) along x = 19.
        Box(area.Walkmesh, 19, 19.9f, 21, 20.1f, door.WalkmeshOwner);
        Box(area.Walkmesh, 18.9f, 20, 19.1f, 22, door.LeafOwner(2));
        area.Walkmesh.SetOwnerEnabled(door.LeafOwner(2), false);
        Assert.Null(area.Walkmesh.GroundHeight(20, 20, 0));
        Assert.NotNull(area.Walkmesh.GroundHeight(19, 21, 0));

        var opener = new Creature { AppearanceType = Goblin };
        world.AddToArea(opener, area, new Vector3(20, 17, 0), 0);
        DoorService.Open(world, door, opener);
        Assert.Equal(2, door.OpenedSide);
        Assert.NotNull(area.Walkmesh.GroundHeight(20, 20, 0));
        Assert.Null(area.Walkmesh.GroundHeight(19, 21, 0));
        // A walk up the hinge side goes round the leaf, a creature's radius clear of it.
        var path = area.Pathfinder.FindPath(new Vector3(19, 15, 0), new Vector3(19, 25, 0), radius: 0.3f)!;
        Assert.True(path.Count > 1);
        float clearance = float.MaxValue;
        var from = new Vector3(19, 15, 0);
        foreach (var to in path)
        {
            for (float t = 0; t <= 1; t += 0.01f)
            {
                var q = Vector3.Lerp(from, to, t);
                float dx = MathF.Max(0, MathF.Max(18.9f - q.X, q.X - 19.1f)), dy = MathF.Max(0, MathF.Max(20 - q.Y, q.Y - 22));
                clearance = MathF.Min(clearance, MathF.Sqrt(dx * dx + dy * dy));
            }
            from = to;
        }
        Assert.True(clearance >= 0.25f && clearance <= 0.6f, string.Join(" ", path) + " " + clearance);

        DoorService.Close(world, door, opener);
        Assert.Null(area.Walkmesh.GroundHeight(20, 20, 0));
        Assert.NotNull(area.Walkmesh.GroundHeight(19, 21, 0));
    }

    static void Box(Walkmesh wm, float x0, float y0, float x1, float y1, int owner)
    {
        wm.Add(new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), 2, false, owner);
        wm.Add(new(x0, y0, 0), new(x1, y1, 0), new(x0, y1, 0), 2, false, owner);
    }

    [DataFact]
    public void TheCreatureGridFindsNeighboursAndFollowsMoves()
    {
        var (world, area) = MakeWorld();
        var a = Add(world, area, Goblin, 5, 5);
        var b = Add(world, area, Goblin, 9, 5);
        var near = new List<Creature>();
        area.CreatureGrid.Near(new Vector3(5.5f, 5, 0), 1, near);
        Assert.Contains(a, near);
        Assert.DoesNotContain(b, near);
        b.Position = new Vector3(5.8f, 5.2f, 0); // set directly: picked up by the next sync
        area.CreatureGrid.Sync(area);
        area.CreatureGrid.Near(new Vector3(5.5f, 5, 0), 1, near);
        Assert.Contains(b, near);
        world.Destroy(b);
        Assert.Equal(1, area.CreatureGrid.Count);
    }

    [Fact]
    public void SwitchingADoorOnlyResamplesTheGroundUnderIt()
    {
        var wm = new Walkmesh();
        wm.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        int v = wm.Version;
        var boxes = new List<(float, float, float, float)>();
        wm.Add(new(10, 10, 0), new(12, 10, 0), new(12, 12, 0), 2, false, 3);
        Assert.False(wm.ChangedSince(v, boxes)); // new triangles: start over
        v = wm.Version;
        wm.SetOwnerEnabled(3, false);
        wm.SetOwnerEnabled(3, false); // no change, no new version
        Assert.True(wm.ChangedSince(v, boxes));
        Assert.Equal((10f, 10f, 12f, 12f), Assert.Single(boxes));
    }
}
