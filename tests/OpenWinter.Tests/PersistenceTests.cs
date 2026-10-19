using System.Diagnostics;
using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using ItemProperty = OpenWinter.Game.Scripting.ItemProperty;
using JsonValue = System.Text.Json.Nodes.JsonValue;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public sealed class PersistenceTests : IDisposable
{
    static string NwnGff => TestData.NimTool("nwn_gff.exe");

    readonly ITestOutputHelper _out;
    readonly string _root = Path.Combine(Path.GetTempPath(), "openwinter-persist-" + Guid.NewGuid().ToString("N"));
    readonly List<World> _worlds = new();

    public PersistenceTests(ITestOutputHelper output) { _out = output; }

    public void Dispose()
    {
        foreach (var w in _worlds) w.System<PersistenceSystem>().Dispose();
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
    }

    sealed class NullConnection : IConnection
    {
        public int Id => 1;
        public bool IsConnected => true;
        public void Send(byte[] message, bool reliable = true) { }
        public bool TryReceive(out byte[] message) { message = Array.Empty<byte>(); return false; }
        public void Disconnect() { }
    }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    sealed record Fixture(World World, Area Area, Creature Pc, Item Item)
    {
        public LocalVariables Vars => World.Module.Locals;
        public int Run(string script) => World.Scripts!.Run(script, World.Module.Id);
    }

    Fixture NewWorld()
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        _worlds.Add(world);
        world.EnableScripts();
        world.Scripts!.Log = _out.WriteLine;
        world.Register(world.Module);
        world.Module.Tag = "test_module";
        var sys = world.System<PersistenceSystem>();
        sys.RootOverride = _root;
        sys.Log = _out.WriteLine;

        var area = new Area { ResRef = "testarea", Tag = "TestArea" };
        world.Register(area);
        world.Areas.Add(area);

        var pc = new Creature
        {
            Tag = "pc",
            Name = new LocString("Test Hero"),
            Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "tester" },
        };
        world.AddToArea(pc, area, new Vector3(10, 10, 0), 0);
        pc.Gold = 77;
        pc.Abilities[0] = 18;

        var item = world.Items.FromBlueprint("nw_wswls001")!;
        item.Properties.Add(new ItemProperty { Type = 6, SubType = 0, CostTable = 2, CostTableValue = 3, Param1 = 255, DurationType = 2, Tag = "enh" });
        item.Properties.Add(new ItemProperty { Type = 16, SubType = 4, CostTable = 4, CostTableValue = 7, Param1 = 255, DurationType = 2, UsesPerDay = 3 });
        item.Locals.Ints["charges_left"] = 3;
        item.Locals.Floats["weight"] = 1.25f;
        item.Locals.Strings["owner"] = "Test Hero";
        item.Locals.Jsons["data"] = "{\"k\":[1,2]}";
        item.Locals.Locations["home"] = new Location(area.Id, new Vector3(1, 2, 3), 45);
        item.Identified = false;
        item.Charges = 5;
        item.Name = new LocString("Hero's Blade");
        world.Register(item);
        Assert.True(ObjectSerializer.GiveItem(world, pc, item));

        world.Module.Locals.Objects["area"] = area.Id;
        world.Module.Locals.Objects["pc"] = pc.Id;
        world.Module.Locals.Objects["item"] = item.Id;
        return new Fixture(world, area, pc, item);
    }

    static void AssertSameItem(Item expected, Item actual)
    {
        Assert.Equal(expected.Tag, actual.Tag);
        Assert.Equal(expected.ResRef, actual.ResRef);
        Assert.Equal(expected.BaseItem, actual.BaseItem);
        Assert.Equal(expected.StackSize, actual.StackSize);
        Assert.Equal(expected.Charges, actual.Charges);
        Assert.Equal(expected.Identified, actual.Identified);
        Assert.Equal(expected.Name.FirstString, actual.Name.FirstString);
        Assert.Equal(expected.Name.StrRef, actual.Name.StrRef);
        Assert.Equal(expected.Properties.Count, actual.Properties.Count);
        for (int i = 0; i < expected.Properties.Count; i++)
        {
            var a = expected.Properties[i];
            var b = actual.Properties[i];
            Assert.Equal((a.Type, a.SubType, a.CostTable, a.CostTableValue, a.Param1, a.Param1Value, a.UsesPerDay, a.Tag),
                         (b.Type, b.SubType, b.CostTable, b.CostTableValue, b.Param1, b.Param1Value, b.UsesPerDay, b.Tag));
        }
        Assert.Equal(expected.Locals.Ints, actual.Locals.Ints);
        Assert.Equal(expected.Locals.Floats, actual.Locals.Floats);
        Assert.Equal(expected.Locals.Strings, actual.Locals.Strings);
        Assert.Equal(expected.Locals.Jsons, actual.Locals.Jsons);
        Assert.Equal(expected.Locals.Locations.Keys, actual.Locals.Locations.Keys);
        foreach (var (k, l) in expected.Locals.Locations)
        {
            Assert.Equal(l.Area, actual.Locals.Locations[k].Area);
            Assert.Equal(l.Position, actual.Locals.Locations[k].Position);
            Assert.Equal(l.FacingDegrees, actual.Locals.Locations[k].FacingDegrees, 3);
        }
    }

    [DataFact]
    public void CampaignVariablesRoundTrip()
    {
        var f = NewWorld();
        f.Run("persistence_cmp");
        var v = f.Vars;
        Assert.Equal(42, v.Ints["i"]);
        Assert.Equal(1.5f, v.Floats["f"]);
        Assert.Equal("hello world", v.Strings["s"]);
        Assert.Equal((1f, 2.5f, -3f), (v.Floats["vx"], v.Floats["vy"], v.Floats["vz"]));
        Assert.Equal(1, v.Ints["larea"]);
        Assert.Equal(4f, v.Floats["lx"]);
        Assert.Equal(6f, v.Floats["lz"]);
        Assert.Equal(90f, v.Floats["lfacing"], 3);
        Assert.Equal("{\"a\":[1,2]}", v.Strings["j"]);
        Assert.Equal(7, v.Ints["pi"]);
        Assert.Equal(0, v.Ints["pi_global"]);
        Assert.Equal(0, v.Ints["gone"]);
        Assert.Equal(0, v.Ints["mismatch"]);
        Assert.Equal(0, v.Ints["over_int"]);
        Assert.Equal("replaced", v.Strings["over"]);
        Assert.Equal(0, v.Ints["missing_db"]);
        Assert.False(File.Exists(Path.Combine(_root, "database", "nosuchcampaign.sqlite3")));

        Assert.Equal(1, v.Ints["stored"]);
        var copy = f.World.Get<Item>(v.Objects["copy"]);
        Assert.NotNull(copy);
        Assert.NotSame(f.Item, copy);
        Assert.Contains(copy, f.Pc.Inventory);
        Assert.Equal(f.Pc.Id, copy!.Possessor);
        AssertSameItem(f.Item, copy);
        var ground = f.World.Get<Item>(v.Objects["ground"]);
        Assert.NotNull(ground);
        Assert.Same(f.Area, ground!.Area);
        Assert.Equal(new Vector3(2, 3, 0), ground.Position);
        Assert.Equal(0, v.Ints["store_invalid"]);
        Assert.Equal(0, v.Ints["retrieve_missing"]);

        // The data is on disk, in the original game's layout: a fresh world (a server restart) sees it.
        Assert.True(File.Exists(Path.Combine(_root, "database", "testcampaign.sqlite3")));
        var campaign = f.World.System<PersistenceSystem>().Campaign("testcampaign")!;
        using (var q = campaign.PrepareInternal("SELECT vartype, payload, compressed, playerid FROM db WHERE varname = 'pi'"))
        {
            Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
            Assert.Equal('I', (char)q.Int(0));
            Assert.Equal(7, q.Int(1));
            Assert.Equal(0, q.Int(2));
            Assert.Equal("testerTest Hero", q.Text(3));
        }
        using (var q = campaign.PrepareInternal("SELECT migration_label FROM migrations ORDER BY migration_label"))
        {
            Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
            Assert.Equal("20191203190521", q.Text(0));
        }
        var g = NewWorld();
        Assert.Equal(42, CampaignDatabase.Get(g.World, "TESTCAMPAIGN", "i", null, CampaignDatabase.TypeInt, v => (int)v.Int(), 0));
        Assert.Equal(7, CampaignDatabase.Get(g.World, "testcampaign", "pi", g.Pc, CampaignDatabase.TypeInt, v => (int)v.Int(), 0));
        g.World.System<PersistenceSystem>().Dispose(); // only one server owns a campaign file at a time

        f.Run("persistence_del");
        Assert.Equal(0, v.Ints["after_destroy"]);
        Assert.Equal("", v.Strings["after_destroy_s"]);
        Assert.False(File.Exists(Path.Combine(_root, "database", "testcampaign.sqlite3")));
    }

    [DataFact]
    public void SqlQueries()
    {
        var f = NewWorld();
        f.Run("persistence_sql");
        var v = f.Vars;
        foreach (var kv in v.Strings.Where(kv => kv.Key.EndsWith("err"))) _out.WriteLine($"{kv.Key} = {kv.Value}");
        Assert.Equal("", v.Strings["create_err"]);
        Assert.Equal(0, v.Ints["insert_step"]);
        Assert.Equal("", v.Strings["insert_err"]);
        Assert.Equal(6, v.Ints["cols"]);
        Assert.Equal("label", v.Strings["col1"]);
        Assert.Equal("", v.Strings["colbad"]);
        Assert.NotEqual("", v.Strings["colbad_err"]);
        Assert.Equal(0, v.Ints["norow"]);
        Assert.Equal("", v.Strings["norow_err"]); // reading before a step is quietly 0, as in the original
        Assert.Equal(3, v.Ints["rows"]);
        Assert.Equal("alpha;beta;gamma;", v.Strings["names"]);
        Assert.Equal(1, v.Ints["id"]);
        Assert.Equal(2.5f, v.Floats["f"]);
        Assert.Equal(2f, v.Floats["vy"]);
        Assert.Equal("[1,\"x\"]", v.Strings["j"]);
        Assert.Equal(0, v.Ints["badindex"]);
        Assert.Equal("", v.Strings["badindex_err"]); // so is a column out of range
        Assert.Equal("", v.Strings["ok_err"]);
        // Binds were cleared for the third row.
        Assert.Equal(1, v.Ints["cleared_f_null"]);
        Assert.Equal("", v.Strings["cleared_v"]);
        var sqlCopy = f.World.Get<Item>(v.Objects["sqlcopy"]);
        Assert.NotNull(sqlCopy);
        AssertSameItem(f.Item, sqlCopy!);
        Assert.Contains(sqlCopy, f.Pc.Inventory);

        Assert.Contains("syntax error", v.Strings["syntax_err"]);
        Assert.Equal(0, v.Ints["syntax_step"]);
        Assert.Contains("no such table", v.Strings["missing_err"]);
        Assert.NotEqual("", v.Strings["badobj_err"]);
        Assert.NotEqual("", v.Strings["param_err"]);
        Assert.Equal(0, v.Ints["constraint_step"]);
        Assert.Contains("UNIQUE", v.Strings["constraint_err"]);

        Assert.Equal(99, v.Ints["csum"]);
        Assert.Contains("not authorized", v.Strings["protected_err"]);
        Assert.Equal(1, v.Ints["builtin_read"]);

        Assert.Equal("aGk=", v.Strings["b64"]);
        Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", v.Strings["sha1"]);
        Assert.Equal(0x02CC5D05, v.Ints["xxh32"]);
        Assert.Equal(1, v.Ints["regexp"]);
        Assert.Equal("hi", v.Strings["b64d"]);
        Assert.Equal("", v.Strings["pc_err"]);
        Assert.Equal(3, v.Ints["count"]);

        // End of the step releases half-read queries; the module database lives in memory (as in the original).
        f.World.Step(0.05f);
        Assert.False(File.Exists(Path.Combine(_root, "database", "module_test_module.sqlite3")));

        // The character database travels inside the exported BIC.
        var bic = ServerVault.Export(f.World, f.Pc);
        Assert.Equal(Path.Combine(_root, "servervault", "tester", "testhero.bic"), bic);
        var root = Gff.Read(File.ReadAllBytes(bic), out var type);
        Assert.Equal("BIC ", type);
        var sqlite = root.GetStruct("SQLite");
        Assert.NotNull(sqlite);
        var reloaded = f.World.Creatures.FromTemplate(root);
        reloaded.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "tester" };
        f.World.AddToArea(reloaded, f.Area, Vector3.Zero, 0);
        using (var q = f.World.System<PersistenceSystem>().CharacterDb(reloaded).PrepareInternal("SELECT v FROM pcdata WHERE k = 'gold'"))
        {
            Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
            Assert.Equal(123, q.Int(0));
        }

        // The reference tools can read the embedded database back out.
        if (File.Exists(NwnGff))
        {
            var extracted = Path.Combine(_root, "extracted.sqlite3");
            var p = Process.Start(new ProcessStartInfo(NwnGff, $"-i \"{bic}\" --out-sqlite \"{extracted}\" -o \"{Path.Combine(_root, "x.json")}\" -k json") { UseShellExecute = false, RedirectStandardError = true })!;
            p.WaitForExit();
            using var db = SqlDb.Open(extracted);
            using var q = db.PrepareInternal("SELECT v FROM pcdata");
            Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
            Assert.Equal(123, q.Int(0));
        }

        f.Run("persistence_clr");
        Assert.Equal(0, v.Ints["schema_after"]);
        Assert.Equal("", v.Strings["schema_after_err"]);
        Assert.Contains("no such table", v.Strings["gone_err"]);
    }

    [DataFact]
    public void ZstdCharacterDatabaseIsRead()
    {
        if (!File.Exists(NwnGff) || VaultBic() is not { } source) { _out.WriteLine("needs nwn_gff and a localvault character"); return; }
        var f = NewWorld();
        Directory.CreateDirectory(_root);
        // The reference tool embeds databases zstd-compressed, as the game does.
        var dbFile = Path.Combine(_root, "pc.sqlite3");
        using (var db = SqlDb.Open(dbFile)) db.Exec("CREATE TABLE kept (x); INSERT INTO kept VALUES (1);");
        var bic = Path.Combine(_root, "zstd.bic");
        Process.Start(new ProcessStartInfo(NwnGff, $"-i \"{source}\" --in-sqlite \"{dbFile}\" -o \"{bic}\" -k gff") { UseShellExecute = false })!.WaitForExit();
        var root = Gff.Read(File.ReadAllBytes(bic));
        var stored = root.GetStruct("SQLite")!.GetBytes("Data");

        Assert.Equal(CompressedBuffer.AlgorithmZstd, (int)BitConverter.ToUInt32(stored, 8));

        var c = f.World.Creatures.FromTemplate(root);
        c.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "tester" };
        f.World.AddToArea(c, f.Area, Vector3.Zero, 0);
        var pcDb = f.World.System<PersistenceSystem>().CharacterDb(c);
        using (var q = pcDb.PrepareInternal("SELECT x FROM kept"))
        {
            Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
            Assert.Equal(1, q.Int(0));
        }
        // OpenWinter writes it back zstd-compressed too, and the reference tool reads it.
        var exported = Gff.Read(File.ReadAllBytes(ServerVault.Export(f.World, c)));
        var written = exported.GetStruct("SQLite")!.GetBytes("Data");
        Assert.Equal(CompressedBuffer.AlgorithmZstd, (int)BitConverter.ToUInt32(written, 8));
        using var back = SqlDb.OpenMemory();
        back.Load(CompressedBuffer.Unwrap("SQL3", written));
        using (var q = back.PrepareInternal("SELECT x FROM kept"))
            Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
    }

    [DataFact]
    public void UnreadableCharacterDatabaseIsKept()
    {
        if (VaultBic() is not { } source) { _out.WriteLine("needs a localvault character"); return; }
        var f = NewWorld();
        var root = Gff.Read(File.ReadAllBytes(source));
        // A damaged zstd buffer: the character keeps it unchanged rather than exporting an empty database.
        var damaged = CompressedBuffer.Wrap("SQL3", new byte[5000]);
        for (int i = 30; i < damaged.Length; i++) damaged[i] ^= 0x5A;
        root.SetStruct("SQLite", new GffStruct(10).Set("Data", GffType.Void, damaged).SetDWord("Size", (uint)damaged.Length));
        var c = f.World.Creatures.FromTemplate(root);
        c.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "tester" };
        f.World.AddToArea(c, f.Area, Vector3.Zero, 0);
        f.World.System<PersistenceSystem>().CharacterDb(c);
        var exported = Gff.Read(File.ReadAllBytes(ServerVault.Export(f.World, c)));
        Assert.Equal(damaged, exported.GetStruct("SQLite")!.GetBytes("Data"));
    }

    [Fact]
    public void SqlBuiltinHashes()
    {
        using var db = SqlDb.OpenMemory();
        using var q = db.PrepareInternal("SELECT xxh64(''), xxh64('abc'), xxh32('abc'), xxh32('Nobody inspects the spammish repetition'), nwbufdecompress('TEST', nwbufcompress('TEST', 'payload'))");
        Assert.Equal(SQLitePCL.raw.SQLITE_ROW, q.Step());
        Assert.Equal(unchecked((long)0xEF46DB3751D8E999UL), q.Int(0));
        Assert.Equal(unchecked((long)0x44BC2CF5AD770999UL), q.Int(1));
        Assert.Equal(unchecked((int)0x32D153FFu), (int)q.Int(2));
        Assert.Equal(unchecked((int)0xE2293B2Fu), (int)q.Int(3));
        Assert.Equal("payload", q.Text(4));
    }

    [DataFact]
    public void JsonTemplatesUuidsAndExport()
    {
        var f = NewWorld();
        f.Run("persistence_jsn");
        var v = f.Vars;
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", v.Strings["uuid"]);
        Assert.Equal(1, v.Ints["uuid_stable"]);
        Assert.Equal(1, v.Ints["byuuid"]);
        Assert.Equal("", v.Strings["uuid_module"]);
        Assert.Equal(0, v.Ints["byuuid_bad"]);
        Assert.Equal("UTI ", v.Strings["data_type"]);
        var json = JsonNode.Parse(v.Strings["item_json"])!.AsObject();
        Assert.Equal(v.Strings["uuid"], json["UUID"]!["value"]!.GetValue<string>());
        Assert.Equal("cexolocstring", json["LocalizedName"]!["type"]!.GetValue<string>());
        var copy = f.World.Get<Item>(v.Objects["json_copy"]);
        Assert.NotNull(copy);
        AssertSameItem(f.Item, copy!);
        Assert.Contains(copy, f.Pc.Inventory);
        Assert.Equal(1, v.Ints["copy_uuid_differs"]);
        Assert.Equal(0, v.Ints["bad_json"]);
        Assert.NotEqual("", v.Strings["module_json_err"]);
        Assert.Equal(1, v.Ints["refreshed"]);
        Assert.Equal(1, v.Ints["old_uuid_gone"]);
        Assert.Equal("UTI ", v.Strings["template_type"]);
        Assert.Equal(1, v.Ints["to_template"]);
        Assert.Equal("TEMPLATED", v.Strings["templated_tag"]);
        Assert.NotEqual("", v.Strings["missing_template_err"]);

        // ExportSingleCharacter wrote the vault file, with the character's UUID.
        var bic = Path.Combine(_root, "servervault", "tester", "testhero.bic");
        Assert.True(File.Exists(bic));
        var root = Gff.Read(File.ReadAllBytes(bic));
        Assert.Equal(f.World.System<PersistenceSystem>().GetUuid(f.Pc), root.GetString("UUID"));
        Assert.Equal(1, root.GetInt("IsPC"));
        Assert.Equal(18, root.GetInt("Str"));
        Assert.Equal(77, root.GetInt("Gold"));
        Assert.Equal("Test Hero", root.GetLocString("FirstName").FirstString);
        // Exporting again reuses the same file; a different character with the same name gets a new one.
        Assert.Equal(bic, ServerVault.Export(f.World, f.Pc));
        var twin = new Creature { Name = new LocString("Test Hero"), Player = f.Pc.Player };
        f.World.Register(twin);
        Assert.Equal(Path.Combine(_root, "servervault", "tester", "testhero1.bic"), ServerVault.Export(f.World, twin));

        // A UUID stored in a loaded object's template is found by GetObjectByUUID.
        var stored = root.GetString("UUID");
        var sys = f.World.System<PersistenceSystem>();
        f.World.Destroy(f.Pc);
        var loaded = f.World.Creatures.FromTemplate(root);
        f.World.AddToArea(loaded, f.Area, Vector3.Zero, 0);
        Assert.Same(loaded, sys.ByUuid(stored));
    }

    [DataFact]
    public void ItemSerializationRoundTrip()
    {
        var f = NewWorld();
        var bag = f.World.Items.FromBlueprint("nw_it_contain001") ?? f.World.Items.FromBlueprint("nw_wswls001")!;
        f.World.Register(bag);
        var inner = f.World.Items.FromBlueprint("nw_it_mring021")!;
        inner.Locals.Ints["inner"] = 9;
        f.World.Register(inner);
        bag.Inventory.Add(inner);
        f.Item.Inventory.Add(bag); // the serializer does not care whether a longsword can hold items

        var bytes = ObjectSerializer.ToBytes(f.World, f.Item)!;
        var back = (Item)ObjectSerializer.FromBytes(f.World, bytes, f.Pc.Location, f.Pc)!;
        AssertSameItem(f.Item, back);
        Assert.Single(back.Inventory);
        Assert.Equal(bag.ResRef, back.Inventory[0].ResRef);
        Assert.Equal(9, back.Inventory[0].Inventory[0].Locals.Ints["inner"]);
        Assert.Equal(back.Id, back.Inventory[0].Possessor);

        // Serializing the copy gives the same GFF (apart from the UUID it does not carry).
        var again = ObjectSerializer.Serialize(f.World, back)!;
        Assert.Equal(GffJson.ToJson(Gff.Read(bytes), "UTI ").ToJsonString(), GffJson.ToJson(again, "UTI ").ToJsonString());
    }

    static string? VaultBic()
    {
        var dir = Path.Combine(TestData.Config!.UserRoot, "localvault");
        if (!Directory.Exists(dir)) return null;
        // The largest character has the most inventory, classes and feats to round-trip.
        return Directory.GetFiles(dir, "*.bic").OrderByDescending(p => new FileInfo(p).Length).FirstOrDefault();
    }

    [DataFact]
    public void CreatureFromBicRoundTrip()
    {
        var path = VaultBic();
        if (path == null) { _out.WriteLine("no localvault characters"); return; }
        _out.WriteLine(path);
        var f = NewWorld();
        var original = Gff.Read(File.ReadAllBytes(path));
        var c = f.World.Creatures.FromTemplate(original);
        f.World.AddToArea(c, f.Area, new Vector3(5, 6, 0), 1f);

        // Unchanged, the creature serializes back to its own file (positions aside) as a character file. (Script
        // serialization writes inventories newest first, like the original, so its order is reversed.)
        var same = ObjectSerializer.CreatureGff(f.World, c, 0xFFFFFFFF, asCharacter: true);
        foreach (var label in new[] { "XPosition", "YPosition", "ZPosition", "XOrientation", "YOrientation" })
        {
            same.Remove(label);
            original.Remove(label);
        }
        Assert.Equal(GffJson.ToJson(original, "BIC ").ToJsonString(), GffJson.ToJson(same, "BIC ").ToJsonString());

        // Change state, then serialize -> deserialize.
        c.Gold += 1234;
        c.Experience += 50;
        c.Abilities[2] += 2;
        c.Skills[1] = 7;
        c.Feats.Add(9999);
        c.Name = new LocString("Renamed Hero");
        c.Locals.Strings["quest"] = "done";
        c.CurrentHitPoints = 3;
        c.Colors[1] = 12;
        c.BodyParts[0] = 300;
        c.Classes[0].KnownSpells[1].Add(123);
        // With the object state, so the local variables go along.
        var bytes = ObjectSerializer.ToBytes(f.World, c, saveObjectState: true)!;
        var back = (Creature)ObjectSerializer.FromBytes(f.World, bytes, new Location(f.Area.Id, new Vector3(7, 8, 0), 90), null, loadObjectState: true)!;

        Assert.Equal("Renamed Hero", back.Name.Resolve(f.World.Resources.Talk));
        Assert.Equal(c.Gold, back.Gold);
        Assert.Equal(c.Experience, back.Experience);
        Assert.Equal(c.Abilities, back.Abilities);
        Assert.Equal(c.Skills.Take(c.Skills.Length), back.Skills.Take(c.Skills.Length));
        Assert.Equal(c.Feats, back.Feats);
        Assert.Equal(c.Classes.Select(x => (x.Class, x.Level, x.Domain1, x.Domain2, x.School)), back.Classes.Select(x => (x.Class, x.Level, x.Domain1, x.Domain2, x.School)));
        Assert.Equal(c.Classes.SelectMany(x => x.KnownSpells.SelectMany(k => k)), back.Classes.SelectMany(x => x.KnownSpells.SelectMany(k => k)));
        Assert.Equal(c.Classes.SelectMany(x => x.Memorized.SelectMany(k => k)), back.Classes.SelectMany(x => x.Memorized.SelectMany(k => k)));
        Assert.Equal("done", back.Locals.Strings["quest"]);
        Assert.Equal(3, back.CurrentHitPoints);
        Assert.Equal(c.MaxHitPoints, back.MaxHitPoints);
        Assert.Equal(c.Colors, back.Colors);
        Assert.Equal(c.BodyParts, back.BodyParts);
        Assert.Equal((c.AppearanceType, c.Gender, c.Race, c.Phenotype, c.Portrait), (back.AppearanceType, back.Gender, back.Race, back.Phenotype, back.Portrait));
        Assert.Equal((c.LawfulChaotic, c.GoodEvil, c.Deity, c.Subrace, c.Age), (back.LawfulChaotic, back.GoodEvil, back.Deity, back.Subrace, back.Age));
        Assert.Equal(c.Equipment.Keys, back.Equipment.Keys);
        foreach (var slot in c.Equipment.Keys)
        {
            AssertSameItem(c.Equipment[slot], back.Equipment[slot]);
            Assert.Equal(back.Id, back.Equipment[slot].Possessor);
        }
        // The inventory comes back the other way round (written newest first, read in order), as in the original.
        Assert.Equal(c.Inventory.Count, back.Inventory.Count);
        for (int i = 0; i < c.Inventory.Count; i++) AssertSameItem(c.Inventory[c.Inventory.Count - 1 - i], back.Inventory[i]);
        Assert.Same(f.Area, back.Area);
        Assert.Equal(90f, back.FacingDegrees, 3);
    }

    /// <summary>Runs the reference GFF tool; null when it is not installed.</summary>
    static JsonNode? NimJson(string file)
    {
        if (!File.Exists(NwnGff)) return null;
        var p = Process.Start(new ProcessStartInfo(NwnGff, $"-i \"{file}\" -k json") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 })!;
        var text = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return JsonNode.Parse(text);
    }

    /// <summary>Same keys in the same order, same types, numbers equal as numbers.</summary>
    static void AssertJsonEquivalent(JsonNode? expected, JsonNode? actual, string path = "$")
    {
        switch (expected)
        {
            case JsonObject eo:
                var ao = Assert.IsType<JsonObject>(actual);
                Assert.True(eo.Select(kv => kv.Key).SequenceEqual(ao.Select(kv => kv.Key)),
                    $"{path}: keys differ\n expected {string.Join(",", eo.Select(kv => kv.Key))}\n actual   {string.Join(",", ao.Select(kv => kv.Key))}");
                foreach (var (k, ev) in eo) AssertJsonEquivalent(ev, ao[k], $"{path}.{k}");
                break;
            case JsonArray ea:
                var aa = Assert.IsType<JsonArray>(actual);
                Assert.True(ea.Count == aa.Count, $"{path}: array length {ea.Count} vs {aa.Count}");
                for (int i = 0; i < ea.Count; i++) AssertJsonEquivalent(ea[i], aa[i], $"{path}[{i}]");
                break;
            case JsonValue ev when ev.GetValueKind() == System.Text.Json.JsonValueKind.Number:
                Assert.True(actual is JsonValue av && av.GetValueKind() == System.Text.Json.JsonValueKind.Number
                    && double.Parse(av.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture) == double.Parse(ev.ToJsonString(), System.Globalization.CultureInfo.InvariantCulture),
                    $"{path}: {ev.ToJsonString()} vs {actual?.ToJsonString()}");
                break;
            default:
                Assert.Equal(expected?.ToJsonString(), actual?.ToJsonString());
                break;
        }
    }

    [DataFact]
    public void JsonShapeMatchesReferenceTool()
    {
        if (!File.Exists(NwnGff)) { _out.WriteLine("nwn_gff not installed; skipped"); return; }
        var f = NewWorld();
        Directory.CreateDirectory(_root);

        // A blueprint item: ObjectToJson of the unmodified item is exactly the tool's json of its UTI.
        var uti = Path.Combine(_root, "nw_wswls001.uti");
        File.WriteAllBytes(uti, f.World.Resources.Read("nw_wswls001", ResType.Uti)!);
        var item = f.World.Items.FromBlueprint("nw_wswls001")!;
        f.World.Register(item);
        // Items always carry their state (variables, effects, actions, transition), as the original's do
        // (parity suite persist_objects); the rest is the blueprint.
        var json = GffJson.ToJson(ObjectSerializer.Serialize(f.World, item)!, "UTI ").AsObject();
        foreach (var state in new[] { "VarTable", "EffectList", "ActionList", "Transition" }) Assert.True(json.Remove(state), state);
        AssertJsonEquivalent(NimJson(uti), json);

        // A whole character file through the GFF <-> json converter, both directions.
        if (VaultBic() is { } bic)
        {
            var reference = NimJson(bic);
            var ours = GffJson.ToJson(Gff.Read(File.ReadAllBytes(bic)), "BIC ");
            AssertJsonEquivalent(reference, ours);
            var rebuilt = GffJson.FromJson(reference, out var type);
            Assert.Equal("BIC ", type);
            var written = Path.Combine(_root, "rebuilt.bic");
            File.WriteAllBytes(written, Gff.Write(rebuilt, type));
            AssertJsonEquivalent(reference, NimJson(written));
        }
    }
}
