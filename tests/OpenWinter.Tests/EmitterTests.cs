using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

public class EmitterTests
{
    [Fact]
    public void KeyTrackInterpolatesAndHolds()
    {
        var k = new KeyTrack { Times = new[] { 0f, 1f, 3f }, Values = new[] { 0f, 10f, 30f } };
        Assert.Equal(0f, k.Sample(-1));
        Assert.Equal(5f, k.Sample(0.5f), 3);
        Assert.Equal(20f, k.Sample(2f), 3);
        Assert.Equal(30f, k.Sample(9f));
        Assert.Equal(30f, k.Max());
        var c = new KeyTrack { Times = new[] { 0f, 2f }, Values = new[] { 0f, 0f, 1f, 1f, 1f, 0f }, Width = 3 };
        Assert.Equal(0.5f, c.Sample(1f, 0), 3);
        Assert.Equal(0.5f, c.Sample(1f, 2), 3);
    }

    [Fact]
    public void EveryControllerRoundTrips()
    {
        var e = new EmitterData();
        foreach (var p in Enum.GetValues<EmitterParam>())
        {
            e.Set(p, 0.25f, 1);
            Assert.Equal(0.25f, e.Get(p, EmitterData.IsColor(p) ? 1 : 0));
        }
    }

    static ResMan Base => ResMan.ForClient(TestData.Paths, Array.Empty<string>(), "");

    [DataFact]
    public void CompiledEmitterHeaderAndAnimatedControllers()
    {
        var m = new ModelCache(Base).Get("vdr_spellres")!;
        var dom = m.Parts.First(p => p.Name == "dom3").Emitter!;
        Assert.Equal("Single", dom.Update);
        Assert.Equal("Billboard_to_Local_Z", dom.Render);
        Assert.Equal("Lighten", dom.Blend);
        Assert.Equal(2, dom.GridX);
        Assert.True(dom.Inherit);
        Assert.True(dom.TwoSided);
        Assert.Equal(2f, dom.FrameStart);
        // The glyphs fade in through their animations' alpha keys.
        var impact = m.Animations["impact"].Tracks["dom3"];
        Assert.True(impact.EmitterKeys.TryGetValue(EmitterParam.AlphaStart, out var alpha));
        Assert.Equal(0.5f, alpha!.Sample(0.3f), 2);

        var hell = new ModelCache(Base).Get("vff_hellball")!;
        var rocks = hell.Parts.First(p => p.Name == "Rocks").Emitter!;
        Assert.True(rocks.Random);
        Assert.Equal(1f, rocks.Mass);
        Assert.Equal(0.2f, rocks.BounceCo, 3);
        Assert.True(hell.Animations["impact"].Tracks["Blast_wave"].EmitterKeys[EmitterParam.BirthRate].Max() > 0);
        var column = hell.Parts.First(p => p.Name == "maincollum").Emitter!;
        Assert.Equal(5f, column.SizeEndY);
        Assert.False(column.P2PBezier);
    }

    [DataFact]
    public void TextModelEmittersAndKeys()
    {
        // vdr_magearmor ships as a text model: its ring sparkles are switched on by keyed birthrates and its
        // shield panels faded in by mesh alpha keys.
        var m = new ModelCache(Base).Get("vdr_magearmor")!;
        var ring = m.Parts.First(p => p.Name == "Ring01").Emitter!;
        Assert.Equal("Fountain", ring.Update);
        Assert.True(ring.InheritLocal);
        Assert.Equal(120f, ring.XSize);
        var blue = m.Parts.First(p => p.Name == "blue").Emitter!;
        Assert.Equal("Billboard_to_World_Z", blue.Render);
        Assert.Equal(3, blue.RenderOrder);
        var duration = m.Animations["duration"];
        Assert.Equal(100f, duration.Tracks["Ring01"].EmitterKeys[EmitterParam.BirthRate].Max());
        Assert.Contains(duration.Tracks.Values, t => t.AlphaTimes.Length > 0);
        var bolt = m.Parts.First(p => p.Name == "OmenEmitter04").Emitter!;
        Assert.Equal("Lightning", bolt.Update);
        Assert.Equal(0.12f, bolt.LightningDelay, 3);
    }

    [DataFact]
    public void SurveyFindsOnlyKnownControllers()
    {
        // Every controller id on the base game's compiled emitters maps to a parsed field.
        var rm = Base;
        var unknown = new HashSet<string>();
        int seen = 0;
        foreach (var key in rm.All(ResType.Mdl).Where(k => k.ResRef.StartsWith("vff_", StringComparison.OrdinalIgnoreCase)))
        {
            var buf = rm.Read(key);
            if (buf == null || !MdlBinaryReader.IsBinary(buf)) continue;
            foreach (var e in MdlBinaryReader.SurveyEmitters(buf))
            {
                seen++;
                foreach (var c in e.Controllers.Keys) if (c.StartsWith("ctrl")) unknown.Add(c);
            }
        }
        Assert.True(seen > 100);
        Assert.Empty(unknown);
    }
}
