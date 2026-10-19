using System.Text.Json;
using OpenWinter.Aurora.Formats;

/// <summary>
/// Module data authored as JSON for parity suites and visual scenes (the project's own text, never game content):
/// a module journal and encounters.
/// </summary>
static class ModuleFixtures
{
    /// <summary>
    /// module.jrl from "journal": [{ "tag", "name", "xp", "priority", "picture", "comment",
    /// "entries": [{ "id", "text", "end" }] }]. Null when the json has no journal.
    /// </summary>
    public static GffStruct? Journal(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("journal", out var list)) return null;
        var categories = new List<GffStruct>();
        uint index = 0;
        foreach (var c in list.EnumerateArray())
        {
            string S(JsonElement e, string k, string d = "") => e.TryGetProperty(k, out var v) ? v.GetString() ?? d : d;
            int I(JsonElement e, string k, int d = 0) => e.TryGetProperty(k, out var v) ? v.GetInt32() : d;
            var entries = new List<GffStruct>();
            uint n = 0;
            if (c.TryGetProperty("entries", out var es))
                foreach (var e in es.EnumerateArray())
                    entries.Add(new GffStruct(n++).SetDWord("ID", (uint)I(e, "id")).SetWord("End", I(e, "end"))
                        .SetLocString("Text", new LocString(S(e, "text"))));
            categories.Add(new GffStruct(index++)
                .SetLocString("Name", new LocString(S(c, "name", S(c, "tag"))))
                .SetDWord("XP", (uint)I(c, "xp")).SetDWord("Priority", (uint)I(c, "priority", 4))
                .SetWord("Picture", I(c, "picture", 65535)).SetString("Comment", S(c, "comment"))
                .SetString("Tag", S(c, "tag")).SetList("EntryList", entries));
        }
        return new GffStruct().SetList("Categories", categories);
    }

    /// <summary>"creatures": [{ "resref", "cr", "single", "appearance" }] replaces an encounter blueprint's CreatureList.</summary>
    public static void Creatures(GffStruct ute, JsonElement spec)
    {
        if (!spec.TryGetProperty("creatures", out var list)) return;
        ute.SetList("CreatureList", list.EnumerateArray().Select(c => new GffStruct(0)
            .SetInt("Appearance", c.TryGetProperty("appearance", out var a) ? a.GetInt32() : 0)
            .SetFloat("CR", c.TryGetProperty("cr", out var cr) ? cr.GetSingle() : 1)
            .SetResRef("ResRef", c.GetProperty("resref").GetString()!)
            .SetByte("SingleSpawn", c.TryGetProperty("single", out var s) && (s.ValueKind == JsonValueKind.True || s.ValueKind == JsonValueKind.Number && s.GetInt32() != 0) ? 1 : 0)).ToList());
    }

    /// <summary>
    /// Places an encounter (struct 7 of the GIT's "Encounter List"): a w x h rectangle centred on (x, y) (or offset by
    /// "ox", "oy"), its points relative to the position as the toolset writes them, and "spawnPoints": [[x, y, facingDegrees], ...]
    /// in area coordinates.
    /// </summary>
    public static void PlaceEncounter(GffStruct ute, JsonElement inst, float x, float y)
    {
        float hw = (inst.TryGetProperty("w", out var wv) ? wv.GetSingle() : 4) / 2, hh = (inst.TryGetProperty("h", out var hv) ? hv.GetSingle() : 4) / 2;
        // "ox", "oy": the rectangle's centre relative to the encounter's position (default: on it).
        float ox = inst.TryGetProperty("ox", out var oxv) ? oxv.GetSingle() : 0, oy = inst.TryGetProperty("oy", out var oyv) ? oyv.GetSingle() : 0;
        Creatures(ute, inst);
        ute.SetFloat("XPosition", x).SetFloat("YPosition", y).SetFloat("ZPosition", 0)
            .SetList("Geometry", new[] { (-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh) }
                .Select(p => new GffStruct(1).SetFloat("X", ox + p.Item1).SetFloat("Y", oy + p.Item2).SetFloat("Z", 0)).ToList());
        var points = new List<GffStruct>();
        if (inst.TryGetProperty("spawnPoints", out var sp))
            foreach (var p in sp.EnumerateArray())
            {
                var v = p.EnumerateArray().Select(n => n.GetSingle()).ToArray();
                points.Add(new GffStruct(2).SetFloat("X", v[0]).SetFloat("Y", v[1]).SetFloat("Z", 0)
                    .SetFloat("Orientation", (v.Length > 2 ? v[2] : 0) * MathF.PI / 180f));
            }
        ute.SetList("SpawnPointList", points);
        ute.StructType = 7;
    }
}
