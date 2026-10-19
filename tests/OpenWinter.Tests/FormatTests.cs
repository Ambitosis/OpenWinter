using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Textures;
using Xunit;

namespace OpenWinter.Tests;

public class FormatTests
{
    [Fact]
    public void GffRoundTripsEveryFieldType()
    {
        var child = new GffStruct(7).SetInt("Child", 42);
        var loc = new LocString { StrRef = 1234 };
        loc.Strings[0] = "Hello";
        var root = new GffStruct()
            .SetByte("B", 200).Set("C", GffType.Char, (sbyte)-5).SetWord("W", 60000).SetShort("S", -1200)
            .SetDWord("DW", 4000000000).SetInt("I", -7).Set("DW64", GffType.DWord64, 1UL << 40).Set("I64", GffType.Int64, -(1L << 40))
            .SetFloat("F", 1.5f).Set("D", GffType.Double, 2.25).SetString("Str", "text é").SetResRef("Ref", "nw_c2_default9")
            .SetLocString("Loc", loc).Set("V", GffType.Void, new byte[] { 1, 2, 3 }).SetStruct("Child", child)
            .SetList("List", new List<GffStruct> { new GffStruct(1).SetInt("A", 1), new GffStruct(2), new GffStruct(3).SetString("X", "y").SetInt("Z", 9) })
            .Set("Vec", GffType.Vector, new Vector3(1, 2, 3)).Set("Ori", GffType.Orientation, new Quaternion(0, 0, 0, 1));

        var back = Gff.Read(Gff.Write(root, "UTC "), out var type);

        Assert.Equal("UTC ", type);
        Assert.Equal(200, back.GetInt("B"));
        Assert.Equal(-5, back.GetInt("C"));
        Assert.Equal(60000, back.GetInt("W"));
        Assert.Equal(-1200, back.GetInt("S"));
        Assert.Equal(4000000000, back.GetInt("DW"));
        Assert.Equal(-7, back.GetInt("I"));
        Assert.Equal(1L << 40, back.GetInt("DW64"));
        Assert.Equal(-(1L << 40), back.GetInt("I64"));
        Assert.Equal(1.5f, back.GetFloat("F"));
        Assert.Equal(2.25f, back.GetFloat("D"));
        Assert.Equal("text é", back.GetString("Str"));
        Assert.Equal("nw_c2_default9", back.GetString("Ref"));
        Assert.Equal(1234, back.GetLocString("Loc").StrRef);
        Assert.Equal("Hello", back.GetLocString("Loc").FirstString);
        Assert.Equal(new byte[] { 1, 2, 3 }, back.GetBytes("V"));
        Assert.Equal(7u, back.GetStruct("Child")!.StructType);
        Assert.Equal(42, back.GetStruct("Child")!.GetInt("Child"));
        Assert.Equal(3, back.GetList("List").Count);
        Assert.Equal(9, back.GetList("List")[2].GetInt("Z"));
        Assert.Equal(new Vector3(1, 2, 3), back.GetVector("Vec"));
    }

    [Fact]
    public void TwoDAParsesQuotesAndEmptyCells()
    {
        var text = "2DA V2.0\n\n   LABEL   NAME   VALUE\n0  First  \"Two words\"  5\n1  ****  x  ****\n3  Gap  y  7\n";
        var t = TwoDA.Parse(System.Text.Encoding.ASCII.GetBytes(text));
        Assert.Equal(4, t.RowCount);
        Assert.Equal("Two words", t.Get(0, "NAME"));
        Assert.Equal("", t.Get(1, "LABEL"));
        Assert.Equal(7, t.GetInt(3, "value"));
        Assert.Equal(3, t.Find("LABEL", "gap"));
    }

    [DataFact]
    public void ReadsModuleAndBaseGameResources()
    {
        var rm = TestData.Module;
        Assert.NotNull(rm.ReadGff("module", ResType.Ifo));
        Assert.True(rm.TwoDA("appearance").RowCount > 800);
        Assert.False(string.IsNullOrEmpty(rm.TlkString(1)));
    }

    [DataFact]
    public void BinaryModelSkinMapsToNodes()
    {
        var models = new ModelCache(TestData.Module);
        var horse = models.Get("c_horse1")!;
        var body = horse.Parts.First(p => p.Name.Equals("HorseBody", StringComparison.OrdinalIgnoreCase)).Mesh!;
        Assert.True(body.IsSkinned);
        for (int i = 0; i < body.BoneParts.Length; i++)
            if (body.BoneWeights[i] > 0) Assert.InRange(body.BoneParts[i], 0, horse.Parts.Count - 1);
    }

    [DataFact]
    public void CreatureInheritsSupermodelAnimations()
    {
        var models = new ModelCache(TestData.Module);
        var bug = models.Get("c_bugbeara")!;
        Assert.NotNull(models.FindAnimation(bug, "walk"));
        Assert.NotNull(models.FindAnimation(bug, "run"));
        Assert.True(models.AllAnimations(bug).Count() > 100);
    }

    [DataFact]
    public void DecodesBioWareDds()
    {
        var tex = new TextureCache(TestData.Module);
        var dds = tex.Get("c_bugbeara");
        Assert.NotNull(dds);
        Assert.True(dds!.Width > 0 && dds.MipCount > 1);
    }
}
