using OpenWinter.Aurora.Formats;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// The hand-built serialized objects parity suite nwnx_object_serialize gives both engines to deserialize: a
/// waypoint and an item whose fields are all chosen here (no blueprint data), in the layout NWNX_Object_Serialize
/// writes, with locals and persistent plugin variables (NWNX_POS).
/// </summary>
public class NwnxSerialProbeTests
{
    static GffStruct Locals(GffStruct s) => s.SetList("VarTable",
    [
        new GffStruct(0).SetString("Name", "probes").SetDWord("Type", 3).SetString("Value", "forty"),
        new GffStruct(0).SetString("Name", "probe").SetDWord("Type", 1).SetInt("Value", 42),
    ]);

    const string Pos = "[INTMAP:1]<25>NWNX_Object!PERINT!posvar = 11;[STRMAP:1]<23>NWNX_Object!PERSTR!poss = <3>abc;";

    public static byte[] Waypoint()
    {
        var s = new GffStruct(uint.MaxValue)
            .SetString("Tag", "SERIAL_PROBE_WP")
            .SetLocString("LocalizedName", new LocString("Probe waypoint"))
            .SetFloat("XPosition", 7).SetFloat("YPosition", 8).SetFloat("ZPosition", 0)
            .SetFloat("XOrientation", 1).SetFloat("YOrientation", 0).SetFloat("ZOrientation", 0);
        Locals(s).SetString("NWNX_POS", Pos);
        return Gff.Write(s, "UTW ");
    }

    public static byte[] Item()
    {
        var s = new GffStruct(uint.MaxValue)
            .SetInt("BaseItem", 49)
            .SetString("Tag", "SERIAL_PROBE_IT")
            .SetLocString("LocalizedName", new LocString("Probe item"))
            .SetWord("StackSize", 2)
            .SetByte("Identified", 1)
            .SetList("PropertiesList", []);
        Locals(s).SetString("NWNX_POS", Pos);
        return Gff.Write(s, "UTI ");
    }

    static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OpenWinter.slnx"))) dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void TheSuiteCarriesTheseObjects()
    {
        var nss = File.ReadAllText(Path.Combine(Repo(), "parity", "suites", "nwnx_object_serialize", "pserial_probe.nss")).Replace("\";\n    s += \"", "");
        Assert.Contains(Convert.ToBase64String(Waypoint()), nss);
        Assert.Contains(Convert.ToBase64String(Item()), nss);
    }
}
