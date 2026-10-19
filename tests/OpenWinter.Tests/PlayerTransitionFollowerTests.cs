using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// A player taking a transition the way the client does it (a click or a keyboard move sent to the GameServer, the
/// client loading the next area before it answers area-ready): the attacker that was fighting it follows, and its
/// associates come through with it (docs/INTER_AREA_MOVEMENT.md).
/// </summary>
public sealed class PlayerTransitionFollowerTests
{
    /// <summary>Two flat 30 m areas joined by an open door pair at y = 25 (a) and y = 5 (b), and a single player.</summary>
    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection Client;
        public readonly Creature Pc;
        public readonly Area A, B;
        public Door DoorA = null!, DoorB = null!;
        /// <summary>Seconds the "client" takes to load an area before it answers area-ready.</summary>
        public float LoadSeconds = 0.5f;
        uint _token;
        double _readyAt = double.NaN;
        public int Loads;

        public Game(bool doors = true, bool open = true)
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            A = MakeArea("pta");
            B = MakeArea("ptb");
            if (doors)
            {
                DoorA = new Door { Tag = "PT_AB", LinkedTo = "PT_BA", LinkedToFlags = 1, IsOpen = open };
                DoorB = new Door { Tag = "PT_BA", LinkedTo = "PT_AB", LinkedToFlags = 1, IsOpen = open };
                World.AddToArea(DoorA, A, new Vector3(15, 25, 0), 0);
                World.AddToArea(DoorB, B, new Vector3(15, 5, 0), 0);
            }
            Server = new GameServer(World, new ServerOptions { StartArea = A.ResRef, DefaultAppearance = 6, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair();
            Client = pair.Client;
            Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Transition tester", AreaReadyHandshake = true }.Write());
            Tick(30);
            Pc = Server.Sessions.Single().Creature!;
            Assert.Same(A, Pc.Area);
            Pc.IsImmortal = true;
            World.Jump(Pc, new Location(A.Id, new Vector3(15, 15, 0), 90));
            Tick();
        }

        Area MakeArea(string name)
        {
            var a = new Area { ResRef = name, Tag = name.ToUpperInvariant(), Width = 3, Height = 3 };
            World.Register(a);
            World.Areas.Add(a);
            a.Walkmesh.Add(new(0, 0, 0), new(30, 0, 0), new(30, 30, 0), 4, true);
            a.Walkmesh.Add(new(0, 0, 0), new(30, 30, 0), new(0, 30, 0), 4, true);
            return a;
        }

        /// <summary>Steps the server; the client answers each area load after <see cref="LoadSeconds"/>.</summary>
        public void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                Server.Tick(1f / 30);
                while (Client.TryReceive(out var data))
                {
                    if ((MessageType)data[0] != MessageType.LoadArea) continue;
                    _token = LoadAreaMessage.Read(new MessageReader(data)).LoadToken;
                    _readyAt = World.Time + LoadSeconds;
                    Loads++;
                }
                if (!double.IsNaN(_readyAt) && World.Time >= _readyAt)
                {
                    _readyAt = double.NaN;
                    Client.Send(new ClientRequestMessage { Kind = "area-ready", Json = JsonSerializer.Serialize(new { token = _token }) }.Write());
                }
            }
        }

        public void RunUntil(float seconds, Func<bool> done)
        {
            for (int i = 0; i < seconds * 30 && !done(); i++) Tick();
        }

        public Creature Creature(Area area, Vector2 at, string tag)
        {
            var c = new Creature { Tag = tag, WalkRate = 1.75f, RunRate = 3.5f, CurrentHitPoints = 50, MaxHitPoints = 50 };
            c.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
            World.AddToArea(c, area, new Vector3(at, 0), MathF.PI / 2);
            return c;
        }

        public void Click(GameObject target) =>
            Client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = target.Id }.Write());
    }

    static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new(a.X, a.Y), new(b.X, b.Y));

    [DataFact]
    public void AttackerFollowsAPlayerWhoClicksThroughADoor()
    {
        var g = new Game();
        var attacker = g.Creature(g.A, new(13, 13), "PT_ATTACKER");
        attacker.Faction = FactionTable.Hostile;
        attacker.Actions.Add(new AttackAction { Target = g.Pc.Id });
        g.Tick(60);
        Assert.Same(g.A, attacker.Area);
        g.Click(g.DoorA);
        g.RunUntil(20, () => g.Pc.Area == g.B);
        Assert.Same(g.B, g.Pc.Area);
        g.RunUntil(20, () => attacker.Area == g.B);
        Assert.Same(g.B, attacker.Area);
        // Then it fights on.
        g.RunUntil(10, () => Flat(attacker.Position, g.Pc.Position) < 2.5f);
        Assert.True(Flat(attacker.Position, g.Pc.Position) < 2.5f, $"attacker at {attacker.Position}, player at {g.Pc.Position}");
        Assert.IsType<AttackAction>(attacker.Actions.FirstOrDefault());
    }

    /// <summary>The official Prelude with one player who loads each area in half a second (data tests).</summary>
    sealed class Prelude : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection Client;
        public readonly Creature Pc;
        public readonly Area Chamber, Halls;
        /// <summary>The Graduation Chamber's doorway to the Training Halls, and the one it leads to.</summary>
        public readonly Door Door, Far;
        readonly string _root = Path.Combine(Path.GetTempPath(), "openwinter-prelude-" + Guid.NewGuid().ToString("N"));
        uint _token;
        double _readyAt = double.NaN;

        public static string Module => Path.Combine(TestData.Paths.GameRoot, "data", "nwm", "Prelude.nwm");

        public Prelude()
        {
            World = new World(ResMan.ForModule(TestData.Paths, Module));
            World.EnableScripts();
            World.LoadModule();
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().RootOverride = _root;
            Server = new GameServer(World, new ServerOptions { DefaultAppearance = 6, Vault = VaultMode.Local });
            var (client, end) = LoopbackConnection.CreatePair();
            Client = client;
            Server.Accept(end);
            Client.Send(new HelloMessage { PlayerName = "Prelude tester", AreaReadyHandshake = true }.Write());
            Tick(30);
            Pc = Server.Sessions.Single().Creature!;
            Pc.IsImmortal = true;
            Chamber = World.Areas.Single(a => a.ResRef == "m0q1c");
            Halls = World.Areas.Single(a => a.ResRef == "m0q1b");
            Door = (Door)World.ByTag("M0Q1C_M0Q1B").Single();
            Far = (Door)World.ByTag("M0Q1B_M0Q1C").Single();
        }

        public void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++)
            {
                Server.Tick(1f / 30);
                while (Client.TryReceive(out var data))
                    if ((MessageType)data[0] == MessageType.LoadArea)
                    {
                        _token = LoadAreaMessage.Read(new MessageReader(data)).LoadToken;
                        _readyAt = World.Time + 0.5;
                    }
                if (!double.IsNaN(_readyAt) && World.Time >= _readyAt)
                {
                    _readyAt = double.NaN;
                    Client.Send(new ClientRequestMessage { Kind = "area-ready", Json = JsonSerializer.Serialize(new { token = _token }) }.Write());
                }
            }
        }

        public void OpenDoorway()
        {
            Far.Locked = false;
            Assert.True(DoorService.Open(World, Door, null));
            Assert.True(DoorService.Open(World, Far, null));
        }

        /// <summary>Where the scene transitfollow started the player: 4.8 m in front of the doorway, facing it.</summary>
        public void StandBeforeTheDoorway()
        {
            World.Jump(Pc, new Location(Chamber.Id, new Vector3(9, 35, 0), 180));
            Tick(60);
            Assert.Same(Chamber, Pc.Area);
        }

        public void HoldForward(float seconds, Func<bool> until)
        {
            for (int i = 0; i < seconds * 30 && !until(); i++)
            {
                if (!Pc.Player!.IsAreaLoading) Client.Send(new DriveInputMessage { Direction = new Vector3(0, 1, 0), Active = true }.Write());
                Tick();
            }
            Client.Send(new DriveInputMessage { Active = false }.Write());
            Tick();
        }

        public void RunScript(string name, string source)
        {
            var code = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(source, name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(code != null, error);
            World.Scripts.Register(name, new OpenWinter.Game.Scripting.NcsProgram(name, code!));
            World.Scripts.Run(name, World.Module.Id);
        }

        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            World.Resources.Dispose();
            try { Directory.Delete(_root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// In the official Prelude: fighting Aribeth (stock nw_c2_default* scripts) in the Graduation
    /// Chamber with a Summon Creature I badger, then clicking the open doorway back to the Training Halls. Aribeth
    /// follows and the badger comes through with the player.
    /// </summary>
    [DataFact]
    public void PreludeAribethAndTheSummonedBadgerFollowThroughTheGraduationChamberDoor()
    {
        if (!File.Exists(Prelude.Module)) return;
        using var p = new Prelude();
        var world = p.World;
        p.OpenDoorway();
        var aribeth = (Creature)world.ByTag("M1Q0_ARIBETH").Single();
        Assert.Same(p.Chamber, aribeth.Area);
        // Stand between Aribeth and the doorway.
        var stand = TransitionService.Destination(world, p.Door);
        world.Jump(p.Pc, new Location(p.Chamber.Id, stand.Position + (aribeth.Position - stand.Position) * 0.15f, 0));
        p.Tick(60);
        Assert.Same(p.Chamber, p.Pc.Area);

        // Summon Creature I, cast the way the /cast command casts it.
        p.Pc.Actions.Add(new OpenWinter.Game.Spells.CastSpellAction { Spell = 174, Cheat = true, Target = p.Pc.Id });
        p.Tick(30 * 8);
        var badger = AssociateService.Get(world, p.Pc, AssociateService.Summoned, 1);
        Assert.NotNull(badger);
        Assert.Same(p.Chamber, badger!.Area);

        // She turns on the player (as after the player hits her) and comes to fight; the badger joins in.
        p.RunScript("pt_aribeth", "void main(){object pc=GetFirstPC();object a=GetObjectByTag(\"M1Q0_ARIBETH\");"
            + "SetIsTemporaryEnemy(pc,a);SetIsTemporaryEnemy(a,pc);AssignCommand(a,ActionAttack(pc));}");
        p.Tick(30 * 6);
        Assert.True(aribeth.Actions.OfType<AttackAction>().Any(a => a.Target == p.Pc.Id)
            || world.System<CombatSystem>().State(aribeth).Target == p.Pc.Id, "Aribeth is not fighting the player");

        // Click the open doorway behind the player.
        p.Client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = p.Door.Id }.Write());
        for (int i = 0; i < 30 * 20 && p.Pc.Area != p.Halls; i++) p.Tick();
        Assert.Same(p.Halls, p.Pc.Area);
        p.Tick();
        Assert.Same(p.Halls, badger.Area);
        for (int i = 0; i < 30 * 30 && aribeth.Area != p.Halls; i++) p.Tick();
        Assert.Same(p.Halls, aribeth.Area);
    }

    /// <summary>
    /// Measured on nwmain (scene transitfollow, posted W keys from (9, 35) facing the doorway): closed, the player
    /// stops at x = 4.47 and the door stays shut; open, it goes through and lands in the Training Halls at
    /// (44.63, 5.90) facing 102 degrees. JumpToObject to the Training Halls door lands on the same spot.
    /// </summary>
    [DataFact]
    public void PreludeWalkingByKeyboardIntoTheGraduationChamberDoorway()
    {
        if (!File.Exists(Prelude.Module)) return;
        using var p = new Prelude();
        p.StandBeforeTheDoorway();
        p.HoldForward(3, () => false);
        Assert.Same(p.Chamber, p.Pc.Area);
        Assert.False(p.Door.IsOpen);
        Assert.InRange(p.Pc.Position.X, 4.3f, 4.7f);

        p.OpenDoorway();
        p.StandBeforeTheDoorway();
        p.HoldForward(4, () => p.Pc.Area == p.Halls);
        Assert.Same(p.Halls, p.Pc.Area);
        Assert.True(Flat(p.Pc.Position, new Vector3(44.63f, 5.90f, 0)) < 0.1f, $"landed at {p.Pc.Position}");
        Assert.InRange(p.Pc.FacingDegrees, 100f, 104.5f);

        p.StandBeforeTheDoorway();
        p.RunScript("pt_jumpdoor", "void main(){AssignCommand(GetFirstPC(),JumpToObject(GetObjectByTag(\"M0Q1B_M0Q1C\")));}");
        for (int i = 0; i < 60 && p.Pc.Area != p.Halls; i++) p.Tick();
        Assert.Same(p.Halls, p.Pc.Area);
        Assert.True(Flat(p.Pc.Position, new Vector3(44.63f, 5.90f, 0)) < 0.1f, $"jumped to {p.Pc.Position}");
    }

    /// <summary>
    /// Measured on nwmain (scene summondoor; the Graduation Chamber doorway, the player 5 m out on its use-point line,
    /// her summoned badger halfway): idle, the badger was pushed aside (1 m) and she walked straight through; walking
    /// towards her (busy), it went round her and kept going while she walked straight through.
    /// Busy but standing (waiting) is the case that used to stop her click for good; not measured.
    /// </summary>
    [DataTheory]
    [InlineData("idle")]
    [InlineData("walking")]
    [InlineData("waiting")]
    public void PreludeOwnSummonInTheWayMakesWayForADoorClick(string summon)
    {
        if (!File.Exists(Prelude.Module)) return;
        using var p = new Prelude();
        var world = p.World;
        p.OpenDoorway();
        world.Jump(p.Pc, new Location(p.Chamber.Id, new Vector3(9.2f, 35.37f, 0), 180));
        p.Tick(30);
        p.Pc.Actions.Add(new OpenWinter.Game.Spells.CastSpellAction { Spell = 174, Cheat = true, Target = p.Pc.Id });
        p.Tick(30 * 8);
        var badger = AssociateService.Get(world, p.Pc, AssociateService.Summoned, 1)!;
        world.Jump(badger, new Location(p.Chamber.Id, new Vector3(6.7f, 35.37f, 0), 0));
        world.Jump(p.Pc, new Location(p.Chamber.Id, new Vector3(9.2f, 35.37f, 0), 180));
        p.Tick(60);
        bool walking = summon == "walking";
        if (summon == "waiting") badger.Actions.Add(new WaitAction { Remaining = 30 });
        if (walking)
        {
            // As the scene's ActionMoveToLocation 8 m out from the door, through the player's spot.
            badger.Actions.Clear();
            badger.Actions.Add(new MoveAction { Target = new Vector3(12.2f, 35.37f, 0) });
            p.Tick(6);
        }
        p.Client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = p.Door.Id }.Write());
        float widest = 0, badgerFurthest = badger.Position.X;
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < 30 * 8 && p.Pc.Area != p.Halls; i++)
        {
            p.Tick();
            if (p.Pc.Area == p.Chamber) widest = Math.Max(widest, MathF.Abs(p.Pc.Position.Y - 35.37f));
            if (badger.Area == p.Chamber) badgerFurthest = Math.Max(badgerFurthest, badger.Position.X);
            if (i % 6 == 0) log.AppendLine($"pc {p.Pc.Position} badger {badger.Position}");
        }
        Assert.True(p.Halls == p.Pc.Area, log.ToString());
        // Measured: straight on (the reading's y never changed); OpenWinter heads for the door's use point.
        Assert.True(widest < 0.4f, $"strayed {widest:0.00} m " + log);
        if (walking) Assert.True(badgerFurthest > 9.2f, $"the badger stopped at {badgerFurthest:0.00} " + log);
    }

    List<Creature> AllAssociates(Game g) => new[] { AssociateService.Henchman, AssociateService.AnimalCompanion, AssociateService.Familiar, AssociateService.Summoned, AssociateService.Dominated }
        .Select((k, i) =>
        {
            // One of them far off (measured: a henchman 24 m away still came).
            var a = g.Creature(g.A, k == AssociateService.Henchman ? new(27, 2) : new(10 + 2.5f * i, 10), "PT_ASSOC" + k);
            AssociateService.Add(g.World, g.Pc, a, k);
            return a;
        }).ToList();

    static void AssertAround(Game g, List<Creature> associates)
    {
        foreach (var a in associates)
        {
            Assert.Same(g.Pc.Area, a.Area);
            // Measured: on the player's spot moved clear of it, 0.6 to 1.1 m away.
            Assert.InRange(Flat(a.Position, g.Pc.Position), 0.3f, 1.6f);
        }
    }

    [DataFact]
    public void AssociatesComeThroughWithAPlayerWhoClicksADoorWhenItArrives()
    {
        var g = new Game();
        var associates = AllAssociates(g);
        g.Click(g.DoorA);
        g.RunUntil(20, () => g.Pc.Player!.IsAreaLoading);
        Assert.True(g.Pc.Player!.IsAreaLoading);
        // While the client loads, they wait where they are (measured: they appear with the player).
        g.Tick(3);
        Assert.All(associates, a => Assert.Same(g.A, a.Area));
        g.RunUntil(5, () => g.Pc.Area == g.B);
        Assert.Same(g.B, g.Pc.Area);
        AssertAround(g, associates);
    }

    [DataFact]
    public void AssociatesComeWithAPlayerJumpedToAnotherAreaByScript()
    {
        var g = new Game(doors: false);
        g.World.EnableScripts();
        var associates = AllAssociates(g);
        var code = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(
            "void main(){AssignCommand(GetFirstPC(),JumpToLocation(Location(GetObjectByTag(\"PTB\"),Vector(20.0,20.0,0.0),0.0)));}",
            "pt_jump", g.World.Scripts!.Definitions, _ => null, out var error);
        Assert.True(code != null, error);
        g.World.Scripts.Register("pt_jump", new OpenWinter.Game.Scripting.NcsProgram("pt_jump", code!));
        g.World.Scripts.Run("pt_jump", g.World.Module.Id);
        g.RunUntil(5, () => g.Pc.Area == g.B);
        Assert.Same(g.B, g.Pc.Area);
        AssertAround(g, associates);
    }

    /// <summary>Holds W (the client's driving input) for up to <paramref name="seconds"/>.</summary>
    static void HoldForward(Game g, float seconds, Func<bool> until)
    {
        for (int i = 0; i < seconds * 30 && !until(); i++)
        {
            if (!g.Pc.Player!.IsAreaLoading)
                g.Client.Send(new DriveInputMessage { Direction = new Vector3(0, 1, 0), Active = true }.Write());
            g.Tick();
        }
        g.Client.Send(new DriveInputMessage { Active = false }.Write());
        g.Tick();
    }

    [DataFact]
    public void WalkingByKeyboardIntoAnOpenDoorwayTakesIt()
    {
        var g = new Game();
        var associates = AllAssociates(g);
        HoldForward(g, 8, () => g.Pc.Area == g.B);
        Assert.Same(g.B, g.Pc.Area);
        AssertAround(g, associates);
        // Landed on the far door's outer side, facing away from it (measured: 0.8 m past the use point).
        var landing = TransitionService.Destination(g.World, g.DoorB);
        Assert.True(Flat(g.Pc.Position, landing.Position) < 0.2f, $"player at {g.Pc.Position}, landing {landing.Position}");
    }

    [DataFact]
    public void WalkingByKeyboardIntoABrokenDoorwayTakesIt()
    {
        // A door bashed down stays open as a transition; walking into its doorway takes it like an open one.
        var g = new Game(open: false);
        DoorService.Break(g.World, g.DoorA, GameObject.InvalidId);
        g.Tick();
        Assert.True(g.DoorA.Broken);
        var associates = AllAssociates(g);
        HoldForward(g, 8, () => g.Pc.Area == g.B);
        Assert.Same(g.B, g.Pc.Area);
        AssertAround(g, associates);
    }

    [DataFact]
    public void WalkingByKeyboardIntoAClosedDoorDoesNotTakeIt()
    {
        var g = new Game(open: false);
        // A closed door blocks the way on a real walkmesh; here it only must not lead anywhere.
        HoldForward(g, 5, () => g.Pc.Area == g.B || g.Pc.Position.Y > 26);
        Assert.Same(g.A, g.Pc.Area);
        Assert.False(g.DoorA.IsOpen);
    }

    [DataFact]
    public void WalkingByKeyboardIntoATransitionTriggerTakesIt()
    {
        var g = new Game(doors: false);
        var wp = new Waypoint { Tag = "PT_LAND" };
        g.World.AddToArea(wp, g.B, new Vector3(15, 15, 0), 0);
        var t = new Trigger { Tag = "PT_TRIGGER", TriggerType = Trigger.TypeTransition, LinkedTo = "PT_LAND", LinkedToFlags = 2 };
        foreach (var (x, y) in new[] { (-2f, -1f), (2f, -1f), (2f, 1f), (-2f, 1f) }) t.Geometry.Add(new Vector3(15 + x, 22 + y, 0));
        g.World.AddToArea(t, g.A, new Vector3(15, 22, 0), 0);
        var associates = AllAssociates(g);
        HoldForward(g, 6, () => g.Pc.Area == g.B);
        Assert.Same(g.B, g.Pc.Area);
        AssertAround(g, associates);
    }
}
