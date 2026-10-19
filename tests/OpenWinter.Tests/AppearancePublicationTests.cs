using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class AppearancePublicationTests
{
    [DataFact]
    public void StationaryEquipAndUnequipReachClientEvenInsideLookThrottle()
    {
        foreach (int delay in new[] { 1, 40 })
        foreach (bool observerArrives in new[] { false, true })
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            var world = new World(resources);
            world.Register(world.Module);
            var area = new Area { ResRef = "appearancepub", Width = 4, Height = 4 };
            world.Register(area); world.Areas.Add(area);
            var server = new GameServer(world, new ServerOptions { StartArea = area.ResRef, DefaultAppearance = 1, Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); server.Accept(pair.Server);
            pair.Client.Send(new HelloMessage { PlayerName = "Appearance tester" }.Write());
            void Tick(int count) { for (int i = 0; i < count; i++) server.Tick(1f / 30); }
            uint observed = GameObject.InvalidId;
            List<Appearance> Looks()
            {
                var looks = new List<Appearance>();
                while (pair.Client.TryReceive(out var bytes))
                {
                    var reader = new MessageReader(bytes);
                    if (reader.Type == MessageType.Appearance)
                    {
                        var message = AppearanceMessage.Read(reader);
                        if (message.ObjectId == observed) looks.Add(message.Appearance);
                    }
                }
                return looks;
            }
            Tick(1);
            var pc = server.Sessions.Single().Creature!;
            observed = pc.Id;
            pc.Feats.Add(46);
            var dagger = world.Inventory.CreateOn("nw_wswdg001", pc, 1, "test_dagger")!;
            Assert.NotNull(dagger);
            Tick(delay); Assert.Empty(Looks());
            var position = pc.Position;
            pair.Client.Send(new ClientRequestMessage { Kind = "equip", Json = JsonSerializer.Serialize(new { item = dagger.Id, slot = 4 }) }.Write());
            Tick(1);
            if (observerArrives)
            {
                var other = LoopbackConnection.CreatePair(); server.Accept(other.Server);
                other.Client.Send(new HelloMessage { PlayerName = "Late observer" }.Write());
                Tick(1);
                var spawns = new List<SpawnMessage>();
                while (other.Client.TryReceive(out var bytes))
                {
                    var reader = new MessageReader(bytes);
                    if (reader.Type == MessageType.Spawn) spawns.Add(SpawnMessage.Read(reader));
                }
                Assert.Contains(Assert.Single(spawns, x => x.ObjectId == pc.Id).Appearance.Equipment,
                    e => e.Slot == 0x10 && e.BaseItem == 22);
            }
            Tick(40);
            Assert.Equal(dagger, pc.Equipment[0x10]);
            Assert.Equal(position, pc.Position);
            Assert.Contains(Looks(), a => a.Equipment.Any(e => e.Slot == 0x10 && e.BaseItem == 22));
            // A second mutation inside the next throttle interval must also survive
            // without movement or any unrelated future dirty update.
            world.MarkChanged(pc); Tick(1); Looks();
            pair.Client.Send(new ClientRequestMessage { Kind = "unequip", Json = JsonSerializer.Serialize(new { item = dagger.Id }) }.Write());
            Tick(40);
            Assert.False(pc.Equipment.ContainsKey(0x10));
            Assert.Contains(Looks(), a => a.Equipment.All(e => e.Slot != 0x10));
            Tick(40);
            Assert.Empty(Looks());
        }
    }
}
