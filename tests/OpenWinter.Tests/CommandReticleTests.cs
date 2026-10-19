using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Click feedback through the real client/server protocol, on an unobstructed test floor.</summary>
public sealed class CommandReticleTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly Area Area;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;

        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame();
            World = new World(resources);
            World.Register(World.Module);
            Area = new Area { ResRef = "reticles", Width = 4, Height = 4 };
            World.Register(Area);
            World.Areas.Add(Area);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
            Server = new GameServer(World, new ServerOptions { StartArea = Area.ResRef, DefaultAppearance = 6, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair();
            Client = pair.Client;
            Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Reticle tester" }.Write());
            Tick();
            Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(Area.Id, new Vector3(20, 15, 0), 0));
            Markers();
        }

        public void Tick(int frames = 1) { for (int i = 0; i < frames; i++) Server.Tick(1f / 30); }
        public void Move(Vector3 point) => Click(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = point, Run = true });
        public void Click(GameObject target) => Click(new ActionRequestMessage { Action = target is Creature ? ActionKind.Talk : ActionKind.Use, Target = target.Id });
        void Click(ActionRequestMessage message) { Client.Send(message.Write()); Tick(); }

        public List<JsonElement> Markers()
        {
            var result = new List<JsonElement>();
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type != MessageType.Notify) continue;
                var message = NotifyMessage.Read(reader);
                if (message.Kind != "command-reticle") continue;
                Assert.Equal(Pc.Id, message.Subject);
                using var doc = JsonDocument.Parse(message.Json);
                result.Add(doc.RootElement.Clone());
            }
            return result;
        }

        public Creature Npc(bool hostile)
        {
            var npc = new Creature { AppearanceType = 6, CurrentHitPoints = 100, MaxHitPoints = 100, Plot = true,
                Faction = hostile ? FactionTable.Hostile : FactionTable.Commoner };
            World.AddToArea(npc, Area, new Vector3(20, 23, 0), 0);
            World.Factions.SetPersonal(npc, Pc, hostile ? 0 : 50);
            return npc;
        }
    }

    static void Kind(JsonElement marker, string expected) => Assert.Equal(expected, marker.GetProperty("kind").GetString());

    [DataFact]
    public void InvalidGroundClearsFeedbackButStillAttemptsToMove()
    {
        var g = new Game();
        g.Move(new Vector3(20, 25, 0));
        Kind(Assert.Single(g.Markers()), "move");
        var start = g.Pc.Position;
        g.Move(new Vector3(43, 15, 0));
        Kind(Assert.Single(g.Markers()), "clear");
        Assert.Contains(g.Pc.Actions, a => a is MoveAction);
        g.Tick(30);
        Assert.True(g.Pc.Position.X > start.X + 1);
        Assert.Empty(g.Markers());
    }

    [DataFact]
    public void NonWalkableFaceOverFloorDoesNotGetGreenFeedback()
    {
        var g = new Game();
        g.Area.Walkmesh.Add(new(22, 14, 0), new(24, 14, 0), new(23, 17, 0), 7, false);
        g.Move(new Vector3(23, 15, 0));
        Kind(Assert.Single(g.Markers()), "clear");
    }

    [DataFact]
    public void GroundMarkerPersistsUntilArrivalAndScriptedMovesDoNotCreateOne()
    {
        var g = new Game();
        var destination = new Vector3(20, 21, 0);
        g.Move(destination);
        var marker = Assert.Single(g.Markers());
        Kind(marker, "move");
        Assert.Equal(destination.X, marker.GetProperty("x").GetSingle());
        Assert.Equal(destination.Y, marker.GetProperty("y").GetSingle());
        g.Tick(10);
        Assert.Empty(g.Markers());
        Assert.NotEmpty(g.Pc.Actions);
        g.Tick(180);
        Kind(Assert.Single(g.Markers()), "clear");
        Assert.InRange(Vector3.Distance(destination, g.Pc.Position), 0, 0.01f);
        g.Pc.Actions.Add(new MoveAction { Target = new Vector3(25, 21, 0), Run = true });
        g.Tick(150);
        Assert.Empty(g.Markers());
    }

    [DataFact]
    public void TankTurnClearsAQueuedMoveAndItsMarkerThroughTheProtocol()
    {
        var g = new Game();
        g.Move(new Vector3(20, 30, 0));
        g.Markers();
        g.Client.Send(new DriveInputMessage { Active = true, Turn = 1 }.Write());
        g.Tick();
        Assert.Empty(g.Pc.Actions);
        Kind(Assert.Single(g.Markers()), "clear");
        g.Client.Send(new DriveInputMessage().Write());
        g.Tick(10); // A released tap completes thirty degrees at the held-turn rate.
        Assert.Null(g.Pc.DriveInput);
    }

    [DataFact]
    public void RapidReplacementMovesKeepRunningAndReachTheLatestDestination()
    {
        foreach (float distance in new[] { 0.7f, 3.7f, 7.5f })
        {
            var g = new Game();
            g.Move(g.Pc.Position + new Vector3(distance, 0, 0));
            g.Tick(3);
            Vector3 destination = default;
            for (int click = 0; click < 6; click++)
            {
                var before = g.Pc.Position;
                // Include changes of heading as well as extending the current route.
                destination = before + new Vector3(distance, click % 2 == 0 ? 0.2f : -0.2f, 0);
                g.Move(destination);
                Assert.True(g.Pc.Speed > 0, $"Replacement {click} at {distance} m inserted a stop.");
                Assert.Equal(MovementMode.Run, g.Pc.Mode);
                Assert.True(Vector3.Distance(before, g.Pc.Position) > 0);
                for (int tick = 0; tick < 8; tick++)
                {
                    g.Tick();
                    Assert.True(g.Pc.Speed > 0);
                    Assert.Equal(MovementMode.Run, g.Pc.Mode);
                }
            }
            g.Tick(180);
            Assert.InRange(Vector3.Distance(destination, g.Pc.Position), 0, 0.01f);
            Assert.Equal(MovementMode.Stand, g.Pc.Mode);
        }
    }

    [DataFact]
    public void GroundClickReplacesDrivingWithoutAnIdleTick()
    {
        var g = new Game();
        g.Client.Send(new DriveInputMessage { Active = true, Direction = new Vector3(0, 1, 0) }.Write());
        g.Tick(3);
        Assert.NotNull(g.Pc.DriveInput);
        var before = g.Pc.Position;
        var destination = before + new Vector3(7.5f, 0, 0);
        g.Move(destination);
        Assert.Null(g.Pc.DriveInput);
        Assert.True(g.Pc.Speed > 0);
        Assert.Equal(MovementMode.Run, g.Pc.Mode);
        Assert.True(g.Pc.Position.X > before.X);
        g.Tick(180);
        Assert.InRange(Vector3.Distance(destination, g.Pc.Position), 0, 0.01f);
    }

    [DataFact]
    public void ReplacementRestartsFeedbackAndKeyboardMovementClearsIt()
    {
        var g = new Game();
        g.Move(new Vector3(20, 25, 0));
        Kind(Assert.Single(g.Markers()), "move");
        g.Move(new Vector3(25, 15, 0));
        var replacement = Assert.Single(g.Markers());
        Kind(replacement, "move");
        Assert.Equal(25, replacement.GetProperty("x").GetSingle());
        g.Client.Send(new MoveInputMessage { Direction = new Vector3(1, 0, 0) }.Write());
        g.Tick();
        Kind(Assert.Single(g.Markers()), "clear");
    }

    [DataFact]
    public void PlaceableApproachClearsOnArrivalButTalkingNeverCreatesAMarker()
    {
        var g = new Game();
        var chest = new Placeable { Appearance = 1, Useable = true };
        g.World.AddToArea(chest, g.Area, new Vector3(23, 20, 0), 0);
        g.Click(chest);
        Kind(Assert.Single(g.Markers()), "move");
        g.Tick(240);
        Kind(Assert.Single(g.Markers()), "clear");
        g.Move(new Vector3(15, 15, 0));
        Kind(Assert.Single(g.Markers()), "move");
        g.Click(g.Npc(false));
        Kind(Assert.Single(g.Markers()), "clear");
        g.Tick(240);
        Assert.Empty(g.Markers());
    }

    [DataFact]
    public void HostileClickTargetsTheEnemyUntilItDies()
    {
        var g = new Game();
        var foe = g.Npc(true);
        g.Click(foe);
        var marker = Assert.Single(g.Markers());
        Kind(marker, "attack");
        Assert.Equal(foe.Id, marker.GetProperty("target").GetUInt32());
        g.Tick(10);
        Assert.Empty(g.Markers());
        foe.CurrentHitPoints = -10;
        foe.IsDead = true;
        g.Tick();
        Kind(Assert.Single(g.Markers()), "clear");
    }

    [DataFact]
    public void ImmediateArrivalClearsTheOldMarkerAndAreaChangesClearPendingOrders()
    {
        var g = new Game();
        g.Move(new Vector3(20, 25, 0));
        g.Markers();
        g.Move(g.Pc.Position);
        Kind(Assert.Single(g.Markers()), "clear");
        g.Move(new Vector3(20, 25, 0));
        g.Markers();
        var other = new Area { ResRef = "other" };
        g.World.Register(other);
        g.World.Areas.Add(other);
        g.Server.EnterArea(g.Pc.Player!, other, Vector3.Zero, 0);
        g.Tick();
        Kind(Assert.Single(g.Markers()), "clear");
    }
}
