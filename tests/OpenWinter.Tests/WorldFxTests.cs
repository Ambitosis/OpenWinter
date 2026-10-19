using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Client world presentation: the Godot-free logic under client/Views/WorldFx/Logic (linked into
/// this project) and the server payloads it depends on.
/// </summary>
public class WorldFxTests
{
    // ---- visual transforms: the client must follow the server's curves exactly ----

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void VisualTransformLerpsMatchServer(int lerp)
    {
        foreach (var (behavior, repeats) in new[] { (0, 0), (0, 2), (1, 3), (1, -1), (0, -1) })
        {
            var client = new VisualTransformSet();
            client.Set(0, VisualTransformSet.Scale, 1f, 3f, lerp, 2f, behavior, repeats, now: 100);
            var server = new VisualTransform { From = 1f, To = 3f, LerpType = lerp, LerpDuration = 2f, Behavior = behavior, Repeats = repeats, StartTime = 100 };
            for (double t = 99; t < 112; t += 0.137)
                Assert.Equal(server.ValueAt(t), client.Value(0, VisualTransformSet.Scale, t));
        }
    }

    [Fact]
    public void VisualTransformCurvesFollowNwscriptFormulas()
    {
        // OBJECT_VISUAL_TRANSFORM_LERP_* as documented in nwscript.nss, at x = 0.25.
        float x = 0.25f;
        var expected = new Dictionary<int, float>
        {
            [1] = x,
            [2] = x * x * (3 - 2 * x),
            [3] = 0.5f - MathF.Sin(MathF.Asin(1f - 2f * x) / 3f),
            [4] = 1 - MathF.Cos(x * MathF.PI * 0.5f),
            [5] = MathF.Sin(x * MathF.PI * 0.5f),
            [6] = x * x,
            [7] = x * x * x * (x * (6f * x - 15f) + 10f),
        };
        foreach (var (lerp, f) in expected)
        {
            var set = new VisualTransformSet();
            set.Set(0, VisualTransformSet.TranslateZ, 0f, 4f, lerp, 1f, 0, 0, now: 0);
            Assert.Equal(4f * f, set.Value(0, VisualTransformSet.TranslateZ, 0.25), 4);
        }
    }

    [Fact]
    public void VisualTransformDefaultsElapsedAndClear()
    {
        var set = new VisualTransformSet();
        Assert.Equal(VisualPose.Identity, set.Pose(0, 5));
        // A transform re-sent on area entry joins its lerp where the server is.
        set.Set(0, VisualTransformSet.RotateZ, 0f, 90f, 1, 4f, 0, 0, now: 10, elapsed: 1);
        Assert.Equal(22.5f, set.Value(0, VisualTransformSet.RotateZ, 10), 3);
        Assert.True(set.Animating(10));
        Assert.False(set.Animating(14));
        set.Set(254, VisualTransformSet.Scale, 1f, 2f, 0, 0f, 0, 0, now: 10);
        set.Clear(0);
        Assert.Equal(0f, set.Value(0, VisualTransformSet.RotateZ, 20));
        Assert.Equal(2f, set.Value(254, VisualTransformSet.Scale, 20));
        set.Clear(-1);
        Assert.True(set.IsEmpty);
    }

    [Fact]
    public void VisualPoseScalesThenRotatesThenTranslates()
    {
        var pose = VisualPose.Identity with { Scale = 2, RotateZ = 90, TranslateZ = 1 };
        var p = Vector3.Transform(new Vector3(1, 0, 0), pose.Matrix());
        Assert.Equal(0f, p.X, 4);
        Assert.Equal(2f, p.Y, 4);
        Assert.Equal(1f, p.Z, 4);
        Assert.True(VisualPose.Identity.IsIdentity);
        Assert.False(pose.IsIdentity);
    }

    // ---- colours, grass, lightning ----

    [Fact]
    public void ScriptColoursAreRgbAndAreaColoursBgr()
    {
        Assert.Equal(0x302010u, FxMath.RgbToBgr(0x102030));
        Assert.Equal(0x102030u, FxMath.BgrToRgb(0x302010));
        Assert.Equal(0x000000u, FxMath.LerpBgr(0x000000, 0xFF00FF, 0));
        Assert.Equal(0xFF00FFu, FxMath.LerpBgr(0x000000, 0xFF00FF, 1));
        Assert.Equal(0x800080u, FxMath.LerpBgr(0x000000, 0xFF00FF, 0.5f));
    }

    [Fact]
    public void GrassScatterIsDeterministicDenseAndSkipsCliffs()
    {
        // Two flat triangles making a 10 m square, and a vertical wall.
        var flat = new List<Vector3> { new(0, 0, 0), new(10, 0, 0), new(10, 10, 0), new(0, 0, 0), new(10, 10, 0), new(0, 10, 0) };
        var a = FxMath.ScatterGrass(flat, 2f, seed: 7);
        var b = FxMath.ScatterGrass(flat, 2f, seed: 7);
        Assert.Equal(a, b);
        Assert.InRange(a.Count, 180, 220);
        Assert.All(a, g => Assert.True(g.Position.X is >= 0 and <= 10 && g.Position.Y is >= 0 and <= 10 && g.Position.Z == 0));
        var wall = new List<Vector3> { new(0, 0, 0), new(10, 0, 0), new(10, 0, 10) };
        Assert.Empty(FxMath.ScatterGrass(wall, 5f, seed: 1));
        Assert.Empty(FxMath.ScatterGrass(flat, 0f, seed: 1));
    }

    [Fact]
    public void LightningPathKeepsItsEndsAndStaysNearTheLine()
    {
        var from = new Vector3(1, 2, 1.5f);
        var to = new Vector3(9, 4, 1.2f);
        var path = FxMath.LightningPath(from, to, 12, 0.5f, new Random(3));
        Assert.Equal(13, path.Length);
        Assert.Equal(from, path[0]);
        Assert.Equal(to, path[^1]);
        var dir = Vector3.Normalize(to - from);
        foreach (var p in path)
        {
            var offset = p - from - dir * Vector3.Dot(p - from, dir);
            Assert.True(offset.Length() <= 0.5f * MathF.Sqrt(2) + 1e-4f);
        }
    }

    [Fact]
    public void ZeroRadiusBeamsStayStraightAcrossDirectionsDistancesAndMovingEnds()
    {
        var start = new Vector3(13, -7, 2);
        foreach (var direction in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Normalize(new Vector3(2, 3, -1)) })
        foreach (float distance in new[] { 0.1f, 4f, 30f })
        foreach (int seed in new[] { 1, 7, 31 })
        {
            var end = start + direction * distance;
            var path = FxMath.BeamPath(start, end, 12, 0, new Random(seed));
            Assert.Equal(start, path[0]);
            Assert.True(Vector3.Distance(end, path[^1]) < 0.00001f);
            for (int i = 0; i < path.Length; i++)
                Assert.True(Vector3.Distance(Vector3.Lerp(start, end, i / 12f), path[i]) < 0.00001f);
        }
    }

    [Fact]
    public void NonzeroAndGeneratedFallbackBeamsKeepTheirExistingJaggedPath()
    {
        var start = new Vector3(2, 3, 4);
        var end = start + Vector3.UnitX * 4;
        foreach (var path in new[]
        {
            FxMath.BeamPath(start, end, 12, 0.2f, new Random(3)),
            FxMath.BeamPath(start, end, 12, 0, new Random(3), legacyFallback: true),
        })
        {
            Assert.Equal(start, path[0]);
            Assert.Equal(end, path[^1]);
            Assert.Contains(path, p => Math.Abs(p.Y - start.Y) > 0.01f || Math.Abs(p.Z - start.Z) > 0.01f);
        }
    }

    [Fact]
    public void WeatherValuesOutsideRainAndSnowAreClear()
    {
        Assert.Equal(0, FxMath.NormalizeWeather(-1));
        Assert.Equal(1, FxMath.NormalizeWeather(1));
        Assert.Equal(2, FxMath.NormalizeWeather(2));
        Assert.Equal(0, FxMath.NormalizeWeather(7));
    }

    // ---- server payloads the client relies on ----

    sealed class Recorder : IWorldOutput
    {
        public readonly List<(string Kind, uint Subject, JsonElement Data)> Notes = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Notes.Add((kind, subject?.Id ?? GameObject.InvalidId, JsonSerializer.SerializeToElement(data)));
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    static (World World, Area Area, Creature Pc, Recorder Output) Town()
    {
        var rm = ResMan.ForModule(TestData.Paths, TestData.Config!.Module);
        rm.Add(new DirectorySource(ScriptsDir()));
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        world.Factions.Load(world.Resources);
        var area = new AreaLoader(world).Load("ss_town");
        world.Areas.Add(area);
        var output = new Recorder();
        world.Output = output;
        var (_, serverEnd) = LoopbackConnection.CreatePair();
        var pc = new Creature { Tag = "FX_PC", Player = new PlayerSession { Connection = serverEnd } };
        var wp = area.ObjectsOf<Waypoint>().First();
        world.AddToArea(pc, area, wp.Position, 0);
        return (world, area, pc, output);
    }

    [DataFact]
    public void DetectedTrapTriggersSendTheirPolygon()
    {
        var (world, area, pc, output) = Town();
        var trap = TrapSystem.CreateAt(world, 0, new Location(area.Id, pc.Position + new Vector3(4, 0, 0), 0), 3f, "FX_TRAP", 1, "", "")!;
        TrapSystem.SetDetected(world, trap, pc, true);
        var note = Assert.Single(output.Notes, n => n.Kind == "trap-detected");
        Assert.Equal(trap.Id, note.Subject);
        Assert.True(note.Data.GetProperty("detected").GetBoolean());
        var geometry = note.Data.GetProperty("geometry");
        Assert.Equal(4, geometry.GetArrayLength());
        Assert.Equal(trap.Geometry[0].X, geometry[0][0].GetSingle());
        Assert.Equal(trap.Geometry[2].Y, geometry[2][1].GetSingle());
    }

    [DataFact]
    public void WeatherTellsClientsTheLightningChance()
    {
        var (world, area, _, _) = Town();
        var env = AreaSettings.Of(world, area);
        env.ChanceLightning = 35;
        env.WeatherOverride = 1;
        var data = JsonSerializer.SerializeToElement(WeatherSystem.Data(area, env));
        Assert.Equal(1, data.GetProperty("weather").GetInt32());
        Assert.Equal(35, data.GetProperty("lightning").GetInt32());
    }

    [DataFact]
    public void FogAndLightColoursKeepAreaByteOrder()
    {
        var (world, area, pc, output) = Town();
        world.Scripts!.Run("fx_fogrgb", pc.Id);
        // Scripts see what they set; the area (sent to entering clients as BGR) is stored BGR.
        Assert.Equal(0x102030, world.Module.Locals.Ints["fog_rgb"]);
        Assert.Equal(0x405060, world.Module.Locals.Ints["light_rgb"]);
        Assert.Equal(0x302010u, (uint)area.Are.GetInt("SunFogColor"));
        Assert.Equal(0x605040u, (uint)area.Are.GetInt("SunDiffuseColor"));
        var fog = Assert.Single(output.Notes, n => n.Kind == "fog-color").Data;
        Assert.Equal(0x102030u, fog.GetProperty("color").GetUInt32());
    }
}
