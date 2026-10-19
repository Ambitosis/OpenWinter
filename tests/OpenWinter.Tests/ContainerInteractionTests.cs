using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ContainerInteractionTests
{
    static void ObserveClose(Game g)
    {
        g.World.EnableScripts();
        const string source = "void main(){SetLocalInt(OBJECT_SELF,\"closed\",GetLocalInt(OBJECT_SELF,\"closed\")+1);SetLocalObject(OBJECT_SELF,\"closer\",GetLastClosedBy());}";
        var bytes = NssCompiler.Compile(source, "test_closed", g.World.Scripts!.Definitions, _ => null, out var error);
        Assert.True(bytes != null, error);
        g.World.Scripts.Register("test_closed", new NcsProgram("test_closed", bytes!));
        g.Chest.Scripts["OnClosed"] = "test_closed";
    }

    static void AssertClosedOnce(Game g)
    {
        Assert.False(g.Chest.IsOpen);
        Assert.Null(g.World.System<ContainerService>().OpenFor(g.Pc));
        Assert.Equal(1, g.Chest.Locals.Ints["closed"]);
        Assert.Equal(g.Pc.Id, g.Chest.Locals.Objects["closer"]);
        Assert.Single(g.Notes(), n => n.Kind == "container-close");
        g.World.System<ContainerService>().Close(g.Pc);
        g.Tick(20);
        Assert.Equal(1, g.Chest.Locals.Ints["closed"]);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "container-close");
    }

    [DataFact]
    public void ClickingViewedContainerClosesItAndRunsItsEventOnce()
    {
        var g = new Game();
        ObserveClose(g);
        g.Click();
        g.AwaitOpen();
        g.Notes();
        g.Click();
        AssertClosedOnce(g);
        Assert.Empty(g.Pc.Actions);
    }

    [DataFact]
    public void FirstMovementStepClosesContainerWhileStillWithinReach()
    {
        var g = new Game();
        ObserveClose(g);
        g.Click();
        g.AwaitOpen();
        g.Tick(20);
        var openedAt = g.Pc.Position;
        g.Notes();
        g.Client.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = openedAt + new Vector3(0, -1, 0), Run = true }.Write());
        g.Tick();
        Assert.NotEqual(openedAt, g.Pc.Position);
        Assert.InRange(Vector3.Distance(g.Pc.Position, g.Chest.Position), 0, ContainerService.ReachDistance);
        AssertClosedOnce(g);
    }

    [DataFact]
    public void TurningAndInventoryRefreshDoNotCloseButTinyDisplacementDoes()
    {
        var g = new Game();
        ObserveClose(g);
        g.Click();
        g.AwaitOpen();
        g.Tick(20);
        g.Pc.Facing += 0.5f;
        g.World.System<ContainerService>().Refresh(g.Pc);
        g.Tick();
        Assert.True(g.Chest.IsOpen);
        g.Notes();
        // Covers displacement independently of its source: keys, pathing, scripts or a push.
        g.Pc.Position += new Vector3(0.001f, 0, 0);
        g.Tick();
        AssertClosedOnce(g);
    }

    [DataFact]
    public void ContainerTransitionsPlayAssignedSoundsOnce()
    {
        var g = new Game();
        g.Click();
        g.AwaitOpen();
        Check(g.Notes(), "as_sw_chestop1");
        DoorService.Open(g.World, g.Chest, g.Pc);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "play-sound");
        g.World.System<ContainerService>().Close(g.Pc);
        Check(g.Notes(), "as_sw_chestcl1");
        DoorService.Close(g.World, g.Chest, g.Pc);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "play-sound");
        g.Chest.Locked = true;
        Assert.False(DoorService.Open(g.World, g.Chest, g.Pc));
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "play-sound");
        // Same object, a different assigned sound appearance (cabinet/drawer).
        g.Chest.Locked = false;
        g.Chest.Appearance = 6;
        DoorService.Open(g.World, g.Chest, g.Pc);
        Check(g.Notes(), "as_sw_drawerop1");
        DoorService.Close(g.World, g.Chest, g.Pc);
        Check(g.Notes(), "as_sw_drawercl1");

        void Check(List<NotifyMessage> notes, string expected)
        {
            var sound = Assert.Single(notes, n => n.Kind == "play-sound");
            Assert.Equal(g.Chest.Id, sound.Subject);
            using var data = JsonDocument.Parse(sound.Json);
            Assert.Equal(expected, data.RootElement.GetProperty("resref").GetString());
        }
    }

    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public readonly Placeable Chest;

        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame();
            World = new World(resources);
            World.Register(World.Module);
            var area = new Area { ResRef = "chestuse", Width = 4, Height = 4 };
            World.Register(area);
            World.Areas.Add(area);
            area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
            area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
            Server = new GameServer(World, new ServerOptions { StartArea = area.ResRef, DefaultAppearance = 1, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair();
            Client = pair.Client;
            Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Chest tester" }.Write());
            Tick();
            Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(area.Id, new Vector3(20, 15, 0), 0));
            Chest = new Placeable { Appearance = 7, Useable = true, HasInventory = true, CurrentHitPoints = 50, HitPoints = 50 };
            World.AddToArea(Chest, area, new Vector3(23, 19, 0), -MathF.PI / 2);
            Notes();
        }

        public void Tick(int frames = 1) { for (int i = 0; i < frames; i++) Server.Tick(1f / 30); }
        public void Click() { Client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = Chest.Id }.Write()); Tick(); }
        public void AwaitOpen()
        {
            for (int i = 0; i < 180 && !Chest.IsOpen; i++) Tick();
            Assert.True(Chest.IsOpen);
        }

        public List<NotifyMessage> Notes()
        {
            var notes = new List<NotifyMessage>();
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type == MessageType.Notify) notes.Add(NotifyMessage.Read(reader));
            }
            return notes;
        }
    }

    [DataFact]
    public void ClickApproachesTheFrontFacesTheChestAndReachesOnce()
    {
        var g = new Game();
        g.Click();
        Assert.False(g.Chest.IsOpen);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "object-use");
        g.AwaitOpen();

        var use = DoorService.UsePoint(g.World, g.Pc, g.Chest)!.Value;
        Assert.InRange(MathF.Abs(Vector3.Distance(use.Point, g.Pc.Position) - 0.8f), 0, 0.01f);
        Assert.True(g.Pc.Position.X < g.Chest.Position.X); // the rotated PWK use point is in front
        var toward = g.Chest.Position - g.Pc.Position;
        Assert.Equal(MathF.Atan2(toward.Y, toward.X), g.Pc.Facing, 3);
        var notes = g.Notes();
        var reach = Assert.Single(notes, n => n.Kind == "object-use");
        Assert.Equal(g.Pc.Id, reach.Subject);
        using var data = JsonDocument.Parse(reach.Json);
        Assert.Equal(g.Chest.Id, data.RootElement.GetProperty("target").GetUInt32());
        Assert.Equal("getmid", data.RootElement.GetProperty("animation").GetString());
        Assert.Single(notes, n => n.Kind == "container-open");
        Assert.Equal(15, g.Pc.Actions[0].ActionType);
        g.Tick(14);
        Assert.Equal(15, g.Pc.Actions[0].ActionType);
        g.Tick(2);
        Assert.Empty(g.Pc.Actions);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "object-use");

        g.World.System<ContainerService>().Close(g.Pc);
        Assert.False(g.Chest.IsOpen);
        g.Click();
        g.AwaitOpen();
        Assert.Single(g.Notes(), n => n.Kind == "object-use");
    }

    [DataFact]
    public void LockedChestDoesNotOpenOrPlayASuccessfulReach()
    {
        var g = new Game();
        g.Chest.Locked = true;
        g.Click();
        g.Tick(180);
        Assert.False(g.Chest.IsOpen);
        Assert.DoesNotContain(g.Notes(), n => n.Kind is "object-use" or "container-open");
        Assert.Empty(g.Pc.Actions);
    }

    [DataFact]
    public void ANewMoveCancelsTheApproachWithoutOpening()
    {
        var g = new Game();
        g.Click();
        g.Tick(4);
        g.Client.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = new Vector3(15, 15, 0), Run = true }.Write());
        g.Tick(180);
        Assert.False(g.Chest.IsOpen);
        Assert.DoesNotContain(g.Notes(), n => n.Kind is "object-use" or "container-open");
    }

    [DataFact]
    public void NonContainerUseKeepsItsWiderApproachAndHasNoReach()
    {
        var g = new Game();
        g.Chest.HasInventory = false;
        g.Click();
        for (int i = 0; i < 180 && g.Pc.Actions.Count > 0; i++) g.Tick();
        Assert.Empty(g.Pc.Actions);
        var use = DoorService.UsePoint(g.World, g.Pc, g.Chest)!.Value;
        Assert.Equal(1.6f, Vector2.Distance(new(use.Point.X, use.Point.Y), new(g.Pc.Position.X, g.Pc.Position.Y)), 2);
        Assert.False(g.Chest.IsOpen);
        Assert.DoesNotContain(g.Notes(), n => n.Kind is "object-use" or "container-open");
    }

    [DataFact]
    public void CreatureClickQueuesDialogueInsteadOfASecondUseAction()
    {
        var g = new Game();
        var npc = new Creature { RulesInitialised = true, CurrentHitPoints = 50, MaxHitPoints = 50, Faction = FactionTable.Defender };
        g.World.AddToArea(npc, g.Pc.Area!, new Vector3(28, 15, 0), 0);
        g.Client.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = npc.Id }.Write());
        g.Tick();
        Assert.Equal(new[] { 0, 7 }, g.Pc.Actions.Select(a => a.ActionType).ToArray());
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "object-use");
    }

    [DataFact]
    public void ServiceAndLootUseDoNotAddClickFacingReachOrHold()
    {
        var g = new Game();
        g.Pc.Facing = 0.75f;
        Assert.True(DoorService.Use(g.World, g.Chest, g.Pc));
        Assert.Equal(0.75f, g.Pc.Facing);
        Assert.Empty(g.Pc.Actions);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "object-use");
        g.World.System<ContainerService>().Close(g.Pc);
        // /loot uses this action directly, without the mouse-click presentation flag.
        g.Pc.Actions.Add(new OpenWinter.Game.Conversations.InteractAction { Target = g.Chest.Id });
        g.Tick();
        Assert.True(g.Chest.IsOpen);
        Assert.Equal(0.75f, g.Pc.Facing);
        Assert.Empty(g.Pc.Actions);
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "object-use");
    }

    [DataFact]
    public void ScriptedInteractionKeepsOnlyItsExistingWait()
    {
        var g = new Game();
        g.Pc.Actions.Add(new OpenWinter.Game.WorldObjects.InteractAction { Target = g.Chest.Id, Range = 1.6f });
        g.AwaitOpen();
        Assert.IsType<WaitAction>(Assert.Single(g.Pc.Actions));
        Assert.DoesNotContain(g.Notes(), n => n.Kind == "object-use");
        g.Tick(16);
        Assert.Empty(g.Pc.Actions);
    }

    [DataFact]
    public void NearbyUnreachableUsePointCompletesPromptlyButOrdinaryMovesDoNot()
    {
        var g = new Game();
        var use = DoorService.UsePoint(g.World, g.Pc, g.Chest)!.Value.Point;
        g.Pc.Position = use + new Vector3(-1.2f, 0, 0);
        // No walkable route, even though the authored use point is within arm's reach.
        g.Pc.Area!.Walkmesh.Clear();
        var ordinary = new MoveAction { TargetObject = g.Chest.Id, Range = 0.8f };
        var approach = new MoveAction { TargetObject = g.Chest.Id, Range = 0.8f, GiveUpWithin = 1.6f };
        Assert.Equal(ActionStatus.InProgress, ordinary.Step(g.World, g.Pc, 1f / 30));
        Assert.Equal(ActionStatus.Done, approach.Step(g.World, g.Pc, 1f / 30));
        Assert.Null(g.Pc.MoveGoal);

        // The fallback does not report arrival from outside the permitted reach.
        g.Pc.Position = use + new Vector3(-4, 0, 0);
        var far = new MoveAction { TargetObject = g.Chest.Id, Range = 0.8f, GiveUpWithin = 1.6f };
        Assert.Equal(ActionStatus.InProgress, far.Step(g.World, g.Pc, 1f / 30));
    }
}
