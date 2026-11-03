using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class ModuleTransitionTests
{
    internal sealed class Fixture : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "openwinter-module-" + Guid.NewGuid().ToString("N"));
        public readonly GameServer Server;
        readonly ResMan _initialResources;
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Root, "modules"));
            WriteModule("source", "old_area", 8);
            WriteModule("destination", "new_area", 13);
            var world = new World(ResMan.ForModule(new GamePaths(TestData.Paths.GameRoot, Root), Module("source")));
            _initialResources = world.Resources;
            world.EnableScripts(); world.LoadModule();
            world.System<OpenWinter.Game.Persistence.PersistenceSystem>().RootOverride = Path.Combine(Root, "writes");
            Server = new GameServer(world, new ServerOptions { ModulePath = Module("source"), ServeContent = true });
        }
        public string Module(string name) => Path.Combine(Root, "modules", name + ".mod");
        void WriteModule(string name, string area, float entry)
        {
            var ifo = new GffStruct().SetLocString("Mod_Name", new LocString(name))
                .SetResRef("Mod_Entry_Area", area).SetFloat("Mod_Entry_X", entry).SetFloat("Mod_Entry_Y", entry)
                .SetFloat("Mod_Entry_Dir_X", 1).SetList("Mod_Area_list", [new GffStruct().SetResRef("Area_Name", area)]);
            var are = new GffStruct().SetString("Tileset", "tde01").SetInt("Width", 2).SetInt("Height", 2)
                .SetString("Tag", area).SetLocString("Name", new LocString(area))
                .SetList("Tile_List", Enumerable.Range(0, 4).Select(_ => new GffStruct().SetInt("Tile_ID", 4)).ToList());
            File.WriteAllBytes(Module(name), Erf.Write([
                new ErfEntry("module", ResType.Ifo, Gff.Write(ifo, "IFO ")),
                new ErfEntry(area, ResType.Are, Gff.Write(are, "ARE ")),
                new ErfEntry(area, ResType.Git, Gff.Write(new GffStruct(), "GIT ")),
            ], "MOD "));
        }
        public (PlayerSession Session, LoopbackConnection Client) Join(string name)
        {
            var (client, connection) = LoopbackConnection.CreatePair();
            var session = Server.Accept(connection);
            client.Send(new HelloMessage { PlayerName = name, PlayerKey = "key-" + name }.Write());
            Server.Tick(.01f);
            Assert.NotNull(session.Creature);
            Drain(client);
            return (session, client);
        }
        public static List<byte[]> Drain(LoopbackConnection client)
        {
            var result = new List<byte[]>();
            while (client.TryReceive(out var data)) result.Add(data);
            return result;
        }
        public void Request(string name)
        {
            var world = Server.World;
            var code = NssCompiler.Compile("void main(){StartNewModule(\"" + name + "\");}", "module_probe",
                world.Scripts!.Definitions, _ => null, out var error);
            Assert.True(code != null, error);
            world.Scripts.Register("module_probe", new NcsProgram("module_probe", code!));
            world.Scripts.Run("module_probe", world.Module.Id);
        }
        public void Dispose()
        {
            Server.World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            Server.World.Resources.Dispose();
            _initialResources.Dispose();
            Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath(Root));
            Assert.StartsWith("openwinter-module-", Path.GetFileName(Root));
            Directory.Delete(Root, recursive: true);
        }
    }

    [DataFact]
    public void TheModuleReloadsWhenTheLastPlayerLeaves()
    {
        using var f = new Fixture();
        f.Server.Options.ReloadWhenEmpty = true;
        var before = f.Server.World;
        before.Pvp = PvpSetting.Full;
        var (_, first) = f.Join("first");
        var (_, second) = f.Join("second");
        first.Disconnect();
        f.Server.Tick(.01f);
        Assert.Same(before, f.Server.World);
        // nwserver loads the module again as the last player leaves; the server's settings stay.
        second.Disconnect();
        f.Server.Tick(.01f);
        Assert.NotSame(before, f.Server.World);
        Assert.Equal(PvpSetting.Full, f.Server.World.Pvp);
        Assert.NotNull(f.Server.World.FindArea("old_area"));
        f.Join("third");
    }

    [DataFact]
    public void ModuleTransferUsesOriginalCharacterDuringFamiliarPossession()
    {
        using var f = new Fixture();
        var (session, _) = f.Join("Possessor");
        var master = session.Creature!; master.Gold = 987;
        var w = f.Server.World;
        var familiar = w.Creatures.FromBlueprint("nw_fm_bat01")!;
        familiar.Scripts.Clear(); w.AddToArea(familiar, master.Area!, master.Position, 0);
        OpenWinter.Game.Society.AssociateService.Add(w, master, familiar, 3);
        Assert.True(w.System<OpenWinter.Game.Players.FamiliarPossession>().Possess(session, familiar));
        f.Request("destination"); f.Server.Tick(.01f);
        Assert.Equal(987, session.Creature!.Gold);
        Assert.Null(session.FamiliarMaster);
        Assert.Same(session.Creature, session.PlayerCreature);
        Assert.NotEqual(familiar.ResRef, session.Creature.ResRef);
    }

    [DataFact]
    public void ScriptTransfersBothConnectedCharactersAndResendsContentBeforeTheirAreas()
    {
        using var f = new Fixture();
        var (a, ac) = f.Join("Alice"); var (b, bc) = f.Join("Bob");
        var old = f.Server.World;
        var original = a.Creature!;
        original.Gold = 4321;
        original.Locals.Ints["old_module"] = 9;
        original.Actions.Add(new WaitAction { Remaining = 100 });
        var item = old.Items.FromTemplate(new GffStruct().SetInt("BaseItem", 49).SetInt("StackSize", 2).SetString("Tag", "transfer"));
        old.Register(item); item.Possessor = original.Id; original.Inventory.Add(item);
        item.Locals.Ints["kept"] = 91;
        bool staleTimerRan = false;
        old.After(1, () => staleTimerRan = true);
        f.Request("destination");
        Assert.Same(old, f.Server.World); // the running script finishes before replacement
        f.Server.Tick(.01f);
        Assert.NotSame(old, f.Server.World);
        Assert.NotSame(original, a.Creature);
        Assert.Equal(4321, a.Creature!.Gold);
        Assert.Empty(a.Creature.Locals.Ints);
        Assert.Empty(a.Creature.Actions);
        Assert.Empty(a.Creature.Effects);
        var transferred = Assert.Single(a.Creature.Inventory, i => i.Tag == "transfer");
        Assert.Equal(2, transferred.StackSize);
        Assert.Equal(91, transferred.Locals.Ints["kept"]);
        Assert.Same(a, a.Creature.Player);
        Assert.Same(b, b.Creature!.Player);
        Assert.Equal("new_area", a.Area!.ResRef);
        Assert.Equal("new_area", b.Area!.ResRef);
        Assert.InRange(Vector3.Distance(a.Creature.Position, new Vector3(13, 13, 0)), 0, 1);
        Assert.Equal("key-Alice", a.PublicKey);
        Assert.Equal(f.Module("destination"), f.Server.Options.ModulePath);
        foreach (var client in new[] { ac, bc })
        {
            var packets = Fixture.Drain(client);
            int welcome = packets.FindIndex(p => (MessageType)p[0] == MessageType.Welcome);
            int area = packets.FindIndex(p => (MessageType)p[0] == MessageType.LoadArea);
            int control = packets.FindIndex(p => (MessageType)p[0] == MessageType.ControlObject);
            Assert.True(welcome >= 0 && area > welcome && control > area);
            Assert.Equal("destination", WelcomeMessage.Read(new MessageReader(packets[welcome])).ModuleName);
        }
        f.Server.Tick(2);
        Assert.False(staleTimerRan);
    }

    [DataFact]
    public void MissingAndCorruptDestinationsLeaveTheCurrentWorldAndSessionsPlayable()
    {
        using var f = new Fixture();
        var (session, client) = f.Join("Alice");
        var original = f.Server.World;
        var actor = session.Creature;
        File.WriteAllText(f.Module("broken"), "invalid archive");
        // A valid archive whose IFO points a field past the end throws IndexOutOfRangeException, not an IO error.
        var ifo = Gff.Write(new GffStruct().SetResRef("Mod_Entry_Area", "x"), "IFO ");
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(
            ifo.AsSpan(System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(ifo.AsSpan(16)) + 8), 0x10000000);
        File.WriteAllBytes(f.Module("badgff"), Erf.Write([new ErfEntry("module", ResType.Ifo, ifo)], "MOD "));
        foreach (string name in new[] { "missing", "../destination", "broken", "badgff" })
        {
            f.Request(name); f.Server.Tick(.01f);
            Assert.Same(original, f.Server.World);
            Assert.Same(actor, session.Creature);
            Assert.True(client.IsConnected);
            Assert.Null(original.PendingModule);
            Assert.DoesNotContain(Fixture.Drain(client), p => (MessageType)p[0] == MessageType.Welcome);
        }
        double before = original.Time;
        f.Server.Tick(.1f);
        Assert.True(original.Time > before);
    }
}
