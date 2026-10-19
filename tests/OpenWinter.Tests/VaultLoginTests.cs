using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class VaultLoginTests
{
    readonly ITestOutputHelper _out;
    public VaultLoginTests(ITestOutputHelper output) { _out = output; }

    /// <summary>
    /// Server vault: the player sees their characters, picks one, plays, and the character is
    /// saved back to their vault when they log out.
    /// </summary>
    [DataFact]
    public void ChooseServerVaultCharacterAndSaveOnLogout()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        // Seed the (sandboxed) vault of player "VaultTester" with a real character.
        var vault = Path.Combine(world.System<OpenWinter.Game.Persistence.PersistenceSystem>().UserRoot, "servervault", "VaultTester");
        Directory.CreateDirectory(vault);
        File.Copy(Path.Combine(TestData.Config!.UserRoot, "localvault", "freddiepharkasf1.bic"), Path.Combine(vault, "freddie.bic"), overwrite: true);

        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", CharacterSelection = true, Vault = VaultMode.Server });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "VaultTester", PlayerKey = "TESTKEY" }.Write());
        server.Tick(1f / 30);

        CharacterListMessage? list = null;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.CharacterList) list = CharacterListMessage.Read(r);
        }
        Assert.NotNull(list);
        var entry = Assert.Single(list!.Characters, c => c.File == "freddie");
        _out.WriteLine($"{entry.Name}: {entry.Classes}, level {entry.Level}, portrait {entry.Portrait}");
        Assert.Contains("Freddie", entry.Name);
        Assert.True(entry.Level > 0);
        Assert.Empty(world.AllObjects.OfType<Creature>().Where(c => c.IsPC));

        client.Send(new CharacterChoiceMessage { File = "freddie" }.Write());
        server.Tick(1f / 30);
        var pc = Assert.Single(world.AllObjects.OfType<Creature>().Where(c => c.IsPC));
        Assert.Contains("Freddie", pc.Name.Resolve(world.Resources.Talk));

        // Play: gain some gold, then log out.
        int gold = pc.Gold + 1234;
        pc.Gold = gold;
        OpenWinter.Game.Rules.CreatureRules.EnsureInit(world, pc);
        int hp = Math.Max(1, pc.CurrentHitPoints - 7); // wounded
        pc.CurrentHitPoints = hp;
        client.Disconnect();
        server.Tick(1f / 30);
        var saved = Gff.Read(File.ReadAllBytes(Path.Combine(vault, "freddie.bic")));
        Assert.Equal(gold, (int)saved.GetInt("Gold"));
        // Loading the saved character gives back the wounded hit points (PregameCurrent), not a stale value.
        var reloaded = world.Creatures.FromTemplate(saved);
        OpenWinter.Game.Rules.CreatureRules.EnsureInit(world, reloaded);
        Assert.Equal(hp, reloaded.CurrentHitPoints);
    }

    [DataFact]
    public void PathsOutsideTheVaultAreRefused()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        Assert.Null(OpenWinter.Game.Persistence.ServerVault.Load(world, "VaultTester", "../../localvault/freddiepharkasf1"));
        Assert.Null(OpenWinter.Game.Persistence.ServerVault.Load(world, "VaultTester", ""));
    }
}
