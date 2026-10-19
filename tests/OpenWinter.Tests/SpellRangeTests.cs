using System.Numerics;
using System.Text;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

public class SpellRangeTests
{
    static World Authored(string ranges)
    {
        var empty = Path.Combine(Path.GetTempPath(), "openwinter-absent-" + Guid.NewGuid().ToString("N"));
        var resources = new ResMan(new GamePaths(empty, empty));
        var memory = new MemorySource("authored spell range controls");
        memory.Set(ResKey.Of("ranges", ResType.TwoDA), Encoding.UTF8.GetBytes(ranges));
        memory.Set(ResKey.Of("spells", ResType.TwoDA), Encoding.UTF8.GetBytes(
            "2DA V2.0\n\nLabel Range\n0 Personal p\n1 Touch T\n2 Short S\n3 Medium M\n4 Long L\n5 Infinite I\n6 Unknown Q\n"));
        resources.AddFirst(memory);
        return new World(resources);
    }

    [Fact]
    public void ModuleResolvedRowsChangeRealSpellRangeWithoutChangingInfiniteOrUnknownFallback()
    {
        var world = Authored("2DA V2.0\n\nLabel PrimaryRange\n0 RenamedPersonal 1\n1 RenamedTouch 1.5\n2 RenamedShort 4\n3 RenamedMedium 7\n4 RenamedLong 10\n");
        float[] expected = [1, 1.5f, 4, 7, 10, 1000, 8];
        for (int spell = 0; spell < expected.Length; spell++) Assert.Equal(expected[spell], SpellTable.Range(world, spell));
    }

    [Fact]
    public void MissingMalformedAndNonFiniteCellsRetainSafeStockFallbacks()
    {
        foreach (string cell in new[] { "****", "invalid", "NaN", "Infinity", "-1" })
        {
            var world = Authored($"2DA V2.0\n\nLabel PrimaryRange\n0 P {cell}\n1 T {cell}\n2 S {cell}\n3 M {cell}\n4 L {cell}\n");
            float[] expected = [0, 2.25f, 8, 20, 40, 1000, 8];
            for (int spell = 0; spell < expected.Length; spell++) Assert.Equal(expected[spell], SpellTable.Range(world, spell));
        }
        Assert.Equal(20, SpellTable.RangeOfCode(Authored(""), "M"));
    }

    [DataFact]
    public void InstalledSpellRowsPreserveStockRanges()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        foreach (string code in new[] { "P", "T", "S", "M", "L", "I", "unknown" })
            Assert.Equal(SpellTable.RangeOfCode(code), SpellTable.RangeOfCode(world, code));
    }

    [DataFact]
    public void LocationCastWalksAroundAWallToReachRange()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        world.Register(world.Module);
        var area = new Area { ResRef = "range_wall", Tag = "range_wall", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        void Rect(float x0, float y0, float x1, float y1, bool walkable)
        {
            area.Walkmesh.Add(new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), 4, walkable);
            area.Walkmesh.Add(new(x0, y0, 0), new(x1, y1, 0), new(x0, y1, 0), 4, walkable);
        }
        // A wall band across y 21-25 with a gap at x 28-32.
        Rect(0, 0, 40, 21, true);
        Rect(0, 21, 28, 25, false);
        Rect(28, 21, 32, 25, true);
        Rect(32, 21, 40, 25, false);
        Rect(0, 25, 40, 40, true);
        var spells = world.Resources.TwoDA("spells");
        int spell = Enumerable.Range(0, spells.RowCount).First(r => spells.Get(r, "Range") == "S"
            && !spells.IsEmpty(r, "ImpactScript"));
        float range = SpellTable.Range(world, spell);
        var caster = new Creature { AppearanceType = 6, CurrentHitPoints = 10, MaxHitPoints = 10 };
        world.AddToArea(caster, area, new Vector3(10, 5, 0), 0);
        var target = new Vector3(10, 30, 0);
        caster.Actions.Add(new CastSpellAction { Spell = spell, Cheat = true, TargetLocation = new Location(area.Id, target, 0) });
        float closest = float.MaxValue;
        for (float t = 0; t < 30 && caster.Actions.Count > 0; t += 1 / 30f)
        {
            world.Step(1 / 30f);
            closest = Math.Min(closest, Vector2.Distance(new(caster.Position.X, caster.Position.Y), new(target.X, target.Y)));
        }
        // Through the gap, not jammed against the wall at y ~21.
        Assert.True(closest <= range + 0.3f + 0.05f, $"closest {closest} for range {range}");
        Assert.True(caster.Position.Y > 25, $"caster stopped at {caster.Position}");
    }
}
