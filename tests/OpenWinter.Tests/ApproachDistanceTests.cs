using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Use radii and where ActionMoveToObject stops (parity suite approach_use). Base game data.</summary>
public class ApproachDistanceTests
{
    static (World W, Area Area) Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var w = new World(rm);
        w.Register(w.Module);
        var area = new Area { ResRef = "approach", Tag = "approach", Width = 4, Height = 4 };
        w.Register(area);
        w.Areas.Add(area);
        float s = 4 * Area.TileSize;
        area.Walkmesh.Add(new(0, 0, 0), new(s, 0, 0), new(s, s, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(s, s, 0), new(0, s, 0), 4, true);
        return (w, area);
    }

    static Creature Walker(World w, Area area, Vector3 at, int appearance = 6)
    {
        var c = w.Creatures.FromTemplate(new GffStruct());
        c.AppearanceType = appearance;
        c.WalkRate = 1.75f;
        c.RunRate = 3.5f;
        w.Register(c);
        w.AddToArea(c, area, at, 0);
        return c;
    }

    [DataFact]
    public void UseRadiiGoByPerspaceNotCreperspace()
    {
        var (w, area) = Make();
        // Measured: a half-orc (CREPERSPACE 0.7) stops as a human does, a halfling (PERSPACE 0.25) 0.05 m nearer.
        var human = Walker(w, area, new Vector3(5, 5, 0));
        var orc = Walker(w, area, new Vector3(8, 5, 0), appearance: 5);
        var halfling = Walker(w, area, new Vector3(11, 5, 0), appearance: 3);
        var item = new Item { BaseItem = 1 };
        w.AddToArea(item, area, new Vector3(20, 20, 0), 0);
        Assert.Equal(0.3f, MoveAction.Clearance(w, human, item), 3);
        Assert.Equal(0.3f, MoveAction.Clearance(w, orc, item), 3);
        Assert.Equal(0.25f, MoveAction.Clearance(w, halfling, item), 3);
        Assert.Equal(1.5f, MoveAction.Clearance(w, orc, human), 3);
        var chest = new Placeable { Appearance = 4 };
        w.AddToArea(chest, area, new Vector3(25, 25, 0), 0);
        Assert.Equal(0.8f, DoorService.UsePoint(w, orc, chest)!.Value.MinRange, 3);
        Assert.Equal(0.75f, DoorService.UsePoint(w, halfling, chest)!.Value.MinRange, 3);
    }

    [DataFact]
    public void MoveToAnOrdinaryTriggerStopsWithinReachOfItsOutline()
    {
        var (w, area) = Make();
        // Measured (TG3): walking at the trigger's south-west corner from the north-east, the walker stops 0.89 m
        // short of the near (north-east) corner, outside, without entering.
        var t = new Trigger { Tag = "TG", TriggerType = Trigger.TypeGeneric };
        foreach (var (x, y) in new[] { (0f, 0f), (6f, 0f), (6f, 6f), (0f, 6f) }) t.Geometry.Add(new Vector3(20 + x, 20 + y, 0));
        w.AddToArea(t, area, new Vector3(20, 20, 0), 0);
        var c = Walker(w, area, new Vector3(30, 30, 0));
        var move = new MoveAction { TargetObject = t.Id, Range = 1f };
        c.Actions.Add(move);
        for (int i = 0; i < 300 && c.Actions.Count > 0; i++) w.Step(1f / 30);
        Assert.Empty(c.Actions);
        Assert.False(t.Contains(c.Position));
        Assert.InRange(t.OutlineDistance(c.Position), 0.8f, 0.9f);
    }
}
