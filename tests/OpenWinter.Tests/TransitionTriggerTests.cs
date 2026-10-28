using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;
using V3 = System.Numerics.Vector3;

namespace OpenWinter.Tests;

/// <summary>Area transition triggers reach the client (the pointer lights them up and a click takes them).</summary>
public sealed class TransitionTriggerTests
{
    [DataFact]
    public void TransitionTriggersAreSentWithTheirPolygonAndGenericOnesAreNot()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Trigger tester");
        var world = f.Server.World;
        var pc = session.Creature!;
        var area = pc.Area!;
        Trigger Add(int type, V3 offset)
        {
            var t = new Trigger { TriggerType = type };
            var at = pc.Position + offset;
            t.Geometry = [at + new V3(-1, -1, 0), at + new V3(2, -1, 0), at + new V3(2, 1, 0.5f), at + new V3(-1, 1, 0)];
            world.AddToArea(t, area, at, 0);
            return t;
        }
        var transition = Add(Trigger.TypeTransition, new V3(5, 0, 0));
        var generic = Add(Trigger.TypeGeneric, new V3(-5, 0, 0));
        f.Server.Tick(1f / 30);
        var spawns = ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Spawn)
            .Select(p => SpawnMessage.Read(new MessageReader(p))).ToList();
        Assert.DoesNotContain(spawns, s => s.ObjectId == generic.Id);
        var spawn = Assert.Single(spawns, s => s.ObjectId == transition.Id);
        Assert.Equal(ObjectKind.Trigger, spawn.Kind);
        Assert.Equal(ObjectFlags.Selectable | ObjectFlags.Transition, spawn.Flags);
        var points = JsonDocument.Parse(spawn.Extra).RootElement.GetProperty("points").EnumerateArray()
            .Select(p => p.EnumerateArray().Select(n => n.GetSingle()).ToArray()).ToList();
        Assert.Equal(new V3(2, 1, 0.5f), new V3(points[2][0], points[2][1], points[2][2]), (a, b) => V3.Distance(a, b) < 1e-4f);
    }

    [Fact]
    public void ConcavePolygonsTriangulateToTheirArea()
    {
        // An L shape: 3 x 3 less a 2 x 2 corner.
        System.Numerics.Vector2[] l = [new(0, 0), new(3, 0), new(3, 1), new(1, 1), new(1, 3), new(0, 3)];
        static float Area(List<(System.Numerics.Vector2 A, System.Numerics.Vector2 B, System.Numerics.Vector2 C)> tris)
            => tris.Sum(t => Math.Abs((t.B.X - t.A.X) * (t.C.Y - t.A.Y) - (t.B.Y - t.A.Y) * (t.C.X - t.A.X)) / 2);
        Assert.Equal(4, TriggerPolygon.Triangulate(l).Count);
        Assert.Equal(5f, Area(TriggerPolygon.Triangulate(l)), 3);
        // Clockwise too.
        Assert.Equal(5f, Area(TriggerPolygon.Triangulate(l.Reverse().ToArray())), 3);
        Assert.True(TriggerPolygon.Contains(l, new(0.5f, 2.5f)));
        Assert.False(TriggerPolygon.Contains(l, new(2, 2)));
    }
}
