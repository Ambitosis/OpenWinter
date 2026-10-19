using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ClickMovementTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly IConnection Client;
        public readonly Creature Pc;
        public Game(float facing = 0)
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            var area = new Area { ResRef = "clickmovement", Width = 4, Height = 4 };
            World.Register(area); World.Areas.Add(area);
            area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
            area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
            Server = new GameServer(World, new ServerOptions { StartArea = area.ResRef, DefaultAppearance = 6, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Click movement tester" }.Write());
            Tick(); Pc = Server.Sessions.Single().Creature!;
            World.Jump(Pc, new Location(area.Id, new Vector3(20, 20, 0), facing));
        }
        public void Tick(int count = 1)
        {
            for (int i = 0; i < count; i++) Server.Tick(1f / 30);
            while (Client.TryReceive(out _)) { }
        }
        public void Click(Vector3 destination)
        {
            Client.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = destination, Run = true }.Write());
            Tick(2);
        }
    }

    [DataFact]
    public void PlayerGroundClicksMatchMeasuredDistanceAndAngleBoundaries()
    {
        // Valid native clickbackfine samples, expressed as their measured world displacement.
        foreach (var (distance, heading, facing, backward) in new[]
        {
            (.9875f, 185.552f, 0f, true), (1.0169f, 185.391f, 0f, false),
            (.8897f, 186.164f, 50f, true), (.8897f, 186.164f, 52f, false),
            (.8897f, 186.164f, 180f, false),
        })
        {
            var g = new Game(facing);
            float angle = heading * MathF.PI / 180;
            var destination = g.Pc.Position + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * distance;
            g.Click(destination);
            Assert.Equal(backward ? MovementMode.Backward : MovementMode.Run, g.Pc.Mode);
            Assert.Equal(backward, g.Pc.WalkRequested);
            float expectedFacing = angle + (backward ? MathF.PI : 0);
            Assert.InRange(MathF.Abs(MathF.IEEERemainder(g.Pc.Facing - expectedFacing, MathF.Tau)), 0, .02f);
            g.Tick(90);
            Assert.InRange(Vector3.Distance(g.Pc.Position, destination), 0, .001f);
            Assert.Empty(g.Pc.Actions);
            Assert.False(g.Pc.MoveBackward);
        }
    }

    [DataFact]
    public void ReplacingABackstepClearsItsFacingAndWalkOverride()
    {
        var g = new Game();
        g.Click(g.Pc.Position - new Vector3(.8f, 0, 0));
        Assert.Equal(MovementMode.Backward, g.Pc.Mode);
        var destination = g.Pc.Position + new Vector3(3, 0, 0);
        g.Click(destination);
        Assert.Equal(MovementMode.Run, g.Pc.Mode);
        Assert.False(g.Pc.MoveBackward);
        Assert.False(g.Pc.WalkRequested);
        Assert.InRange(MathF.Abs(g.Pc.Facing), 0, .01f);
        g.Tick(90);
        Assert.InRange(Vector3.Distance(g.Pc.Position, destination), 0, .001f);
    }
}
