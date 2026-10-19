using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Client.Gameplay;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Requests;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class QuickbarPersistenceTests
{
    static GffStruct Aluvian() => Gff.Read(File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot,
        "data", "lcv", "aluviandarkstar.bic")));

    [DataFact]
    public void SlotEditsSurviveServerVaultLogoutAndAnotherClientLogin()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        string player = "QuickbarTest" + Guid.NewGuid().ToString("N");
        var root = world.System<PersistenceSystem>().UserRoot;
        var vault = Path.Combine(root, "servervault", player);
        Directory.CreateDirectory(vault);
        var path = Path.Combine(vault, "aluvian.bic");
        var original = Aluvian();
        original.GetList("QBList")[20].SetByte("QBObjectType", 99).SetString("UnknownField", "keep this");
        File.WriteAllBytes(path, Gff.Write(original, "BIC "));
        var server = new GameServer(world, new ServerOptions { CharacterSelection = true, Vault = VaultMode.Server, StartArea = "ss_town" });
        var (client, pc) = Login();
        try
        {
            var before = QuickbarRequests.Read(world, pc, _ => { })!;
            var dagger = world.Get<Item>(before[5]!.Item)!;
            var potion = world.Get<Item>(before[6]!.Item)!;
            // Property export sorts cast-spell properties first; quickbar indices must follow the same order.
            potion.Properties.Insert(0, new ItemProperty { Type = 0, DurationType = 2 });
            Send(0, null);
            Send(15, new() { Kind = "spell", Id = 102, Class = 10, Meta = 1 });
            Send(24, new() { Kind = "item", Item = dagger.Id });
            Send(25, new() { Kind = "item", Item = potion.Id, Prop = 1 });
            Send(26, new() { Kind = "emote", Arg = "greeting", Name = "Wave" });
            Send(27, new() { Kind = "command", Arg = "rest" });
            Send(35, new() { Kind = "mode", Id = 9 });
            dagger.InvX = 3;
            dagger.InvY = 6;
            client.Disconnect();
            server.Tick(1f / 30);

            var saved = Gff.Read(File.ReadAllBytes(path));
            var qb = saved.GetList("QBList");
            Assert.Equal(36, qb.Count);
            Assert.Equal(0, qb[0].GetInt("QBObjectType"));
            Assert.Equal(2, qb[15].GetInt("QBObjectType"));
            Assert.Equal(0, qb[15].GetInt("QBMultiClass"));
            Assert.Equal(1, qb[15].GetInt("QBMetaType"));
            Assert.Equal(3, qb[24].GetInt("QBItemReposX"));
            Assert.Equal(6, qb[24].GetInt("QBItemReposY"));
            Assert.Equal(0, qb[25].GetInt("QBCastPropIndex"));
            Assert.Equal("keep this", qb[20].GetString("UnknownField"));
            Assert.Equal(99, qb[20].GetInt("QBObjectType"));
            Assert.True(BicQuickbar.HasSavedEdits(saved));
            Assert.Equal(0u, JsonSerializer.Deserialize<QuickbarSlot>(qb[24].GetString("ElySlot"))!.Item);

            (client, pc) = Login();
            client.Send(new ClientRequestMessage { Kind = "quickbar" }.Write());
            server.Tick(1f / 30);
            JsonElement reply = default;
            while (client.TryReceive(out var bytes))
            {
                var r = new MessageReader(bytes);
                if (r.Type != MessageType.Notify) continue;
                var notify = NotifyMessage.Read(r);
                if (notify.Kind == "quickbar") reply = JsonSerializer.Deserialize<JsonElement>(notify.Json);
            }
            Assert.True(reply.GetProperty("authoritative").GetBoolean());
            var restored = reply.GetProperty("slots").Deserialize<QuickbarSlot?[]>()!;
            Assert.Null(restored[0]);
            Assert.Equal((102, 10, 1), (restored[15]!.Id, restored[15]!.Class, restored[15]!.Meta));
            var newDagger = world.Get<Item>(restored[24]!.Item)!;
            Assert.NotEqual(dagger.Id, newDagger.Id);
            Assert.Equal("Dagger", newDagger.Name.Resolve(world.Resources.Talk));
            Assert.Equal(newDagger.Id, restored[5]!.Item); // the untouched original shortcut followed the moved item too
            Assert.Equal(0, restored[25]!.Prop);
            Assert.Equal("greeting", restored[26]!.Arg);
            Assert.Equal("rest", restored[27]!.Arg);
            Assert.Equal(("mode", 9), (restored[35]!.Kind, restored[35]!.Id));
        }
        finally
        {
            client.Disconnect();
            server.Tick(1f / 30);
            File.Delete(path);
            Directory.Delete(vault);
        }

        void Send(int index, QuickbarSlot? slot)
        {
            client.Send(new ClientRequestMessage { Kind = "quickbar-set", Json = JsonSerializer.Serialize(new { index, slot }) }.Write());
            server.Tick(1f / 30);
        }

        (IConnection, Creature) Login()
        {
            var (connection, serverEnd) = LoopbackConnection.CreatePair();
            server.Accept(serverEnd);
            connection.Send(new HelloMessage { PlayerName = player }.Write());
            server.Tick(1f / 30);
            connection.Send(new CharacterChoiceMessage { File = "aluvian" }.Write());
            server.Tick(1f / 30);
            return (connection, server.Sessions.Single(s => s.PlayerName == player && s.Creature != null).Creature!);
        }
    }

    [DataFact]
    public void ConsumedPowerShortcutRetainsItsIconAcrossSaveWithoutBindingAnotherStack()
    {
        var world = new World(TestData.Module);
        var pc = world.Creatures.FromTemplate(Aluvian());
        world.Register(pc);
        world.Inventory.Load(pc);
        var original = QuickbarRequests.Read(world, pc, _ => { })![6]!;
        var potion = world.Get<Item>(original.Item)!;
        Assert.True(original.ItemHasPower);
        Assert.NotEmpty(original.ItemIcons);
        Assert.Equal(64, original.ItemKey.Length);
        // Default inventory drag selects the first power (Prop=-1), just like the reported slot.
        ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(
            new { index = 24, slot = new QuickbarSlot { Kind = "item", Item = potion.Id, Prop = -1 } }));
        world.Destroy(potion);
        var exhausted = QuickbarRequests.Read(world, pc, _ => { })![24]!;
        Assert.Equal(0u, exhausted.Item);
        Assert.True(exhausted.ItemHasPower);
        Assert.Equal(original.ItemIcons, exhausted.ItemIcons);
        var bic = ObjectSerializer.CreatureGff(world, pc, uint.MaxValue, true);
        Assert.Equal(0, bic.GetList("QBList")[24].GetInt("QBObjectType")); // Intentional OpenWinter-only empty shortcut.
        var restored = world.Creatures.FromTemplate(bic);
        world.Register(restored);
        Assert.Equal(exhausted.ItemKey, QuickbarRequests.Read(world, restored, _ => { })![24]!.ItemKey);
        var wrong = world.Items.FromTemplate(potion.Template);
        wrong.Tag += "_different";
        world.Register(wrong); wrong.Possessor = restored.Id; restored.Inventory.Add(wrong);
        Assert.Equal(0u, QuickbarRequests.Read(world, restored, _ => { })![24]!.Item);
        var replacement = world.Items.FromTemplate(potion.Template);
        world.Register(replacement); replacement.Possessor = restored.Id; restored.Inventory.Add(replacement);
        Assert.Equal(0u, QuickbarRequests.Read(world, restored, _ => { })![24]!.Item);
        Assert.NotEqual(wrong.Id, replacement.Id);
        // Explicitly placing another stack on the bar is the only way to replace the exhausted binding.
        ClientRequests.Handle(world, restored, "quickbar-set", JsonSerializer.Serialize(new
            { index = 24, slot = new QuickbarSlot { Kind = "item", Item = replacement.Id } }));
        Assert.Equal(replacement.Id, QuickbarRequests.Read(world, restored, _ => { })![24]!.Item);
    }

    [DataFact]
    public void ExistingOwnedShortcutRefreshesIdentityAndFollowsItsSelectedPower()
    {
        var world = new World(TestData.Module);
        var pc = world.Creatures.FromTemplate(Aluvian());
        world.Register(pc); world.Inventory.Load(pc);
        var original = QuickbarRequests.Read(world, pc, _ => { })![6]!;
        var potion = world.Get<Item>(original.Item)!;
        ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(new
            { index = 24, slot = new QuickbarSlot { Kind = "item", Item = potion.Id, Prop = original.Prop } }));
        var before = QuickbarRequests.Read(world, pc, _ => { })![24]!;
        var selected = potion.Properties[before.Prop];
        potion.Identified = !potion.Identified;
        potion.Plot = !potion.Plot;
        potion.Properties.Insert(0, new OpenWinter.Game.Scripting.ItemProperty { Type = 1 });
        var changed = QuickbarRequests.Read(world, pc, _ => { })![24]!;
        Assert.Equal(potion.Id, changed.Item);
        Assert.NotEqual(before.ItemKey, changed.ItemKey);
        Assert.Equal(before.ItemPowerKey, changed.ItemPowerKey);
        Assert.Same(selected, potion.Properties[changed.Prop]);
        Assert.Equal(before.Prop + 1, changed.Prop);
        potion.Properties.Remove(selected);
        var removed = QuickbarRequests.Read(world, pc, _ => { })![24]!;
        Assert.Equal(before.ItemPowerKey, removed.ItemPowerKey); // Never substitutes the other property.
        Assert.DoesNotContain(potion.Properties, p => QuickbarItemState.PowerKey(p) == removed.ItemPowerKey);
    }

    [DataFact]
    public void ForeignItemsAndMalformedEditsAreRefused()
    {
        var world = new World(TestData.Module);
        var pc = world.Creatures.FromTemplate(Aluvian());
        world.Register(pc);
        var other = world.Creatures.FromTemplate(Aluvian());
        world.Register(other);
        world.Inventory.Load(pc);
        world.Inventory.Load(other);
        void Send(object data) => ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(data));
        Send(new { index = 0, slot = new QuickbarSlot { Kind = "item", Item = other.Inventory[0].Id } });
        Send(new { index = 36, slot = new QuickbarSlot { Kind = "command", Arg = "attack" } });
        Send(new { index = -1, slot = (QuickbarSlot?)null });
        Send(new { index = 0, slot = new QuickbarSlot { Kind = "execute-script", Arg = "anything" } });
        Send(new { index = 0, slot = new QuickbarSlot { Kind = "item", Item = pc.Inventory[0].Id, Prop = 254 } });
        Send(new { index = 0, slot = new QuickbarSlot { Kind = "command", Arg = null! } });
        Assert.True(ClientRequests.Handle(world, pc, "quickbar-set", "{\"index\":0,\"slot\":5}"));
        Assert.Empty(pc.QuickbarEdits);
        Assert.Equal("attack", QuickbarRequests.Read(world, pc, _ => { })![0]!.Arg);
    }

    [DataFact]
    public void SnapshotKeepsEditedAndOriginalItemBindingsAndClearedSlots()
    {
        var world = new World(TestData.Module);
        var pc = world.Creatures.FromTemplate(Aluvian());
        world.Register(pc);
        world.Inventory.Load(pc);
        var dagger = world.Get<Item>(QuickbarRequests.Read(world, pc, _ => { })![5]!.Item)!;
        Assert.True(ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(new { index = 34, slot = new QuickbarSlot { Kind = "item", Item = dagger.Id } })));
        Assert.True(ClientRequests.Handle(world, pc, "quickbar-set", "{\"index\":0,\"slot\":null}"));
        var snapshot = WorldSnapshot.Capture(world, out var warnings);
        Assert.Empty(warnings);
        var restoredWorld = new World(TestData.Module);
        WorldSnapshot.Apply(restoredWorld, snapshot);
        var restored = restoredWorld.Get<Creature>(pc.Id)!;
        var slots = QuickbarRequests.Read(restoredWorld, restored, _ => { })!;
        Assert.Null(slots[0]);
        Assert.Equal(dagger.Id, slots[34]!.Item);
        Assert.Same(restored.QuickbarItems[5], restored.QuickbarItems[34]);
        var item = restored.QuickbarItems[34];
        restored.Inventory.Remove(item);
        restored.Equipment[EquipSlot.RightHand] = item;
        var bic = ObjectSerializer.CreatureGff(restoredWorld, restored, uint.MaxValue, true);
        Assert.Equal(EquipSlot.RightHand, bic.GetList("QBList")[34].GetInt("QBItemInvSlot"));
    }

    [Fact]
    public void SyncPreservesUnknownSlotsAndSendsBothSidesOfSwaps()
    {
        var sync = new QuickbarSync();
        var slots = new QuickbarSlot?[36];
        slots[0] = new() { Kind = "command", Arg = "attack" };
        slots[8] = new() { Kind = "command", Arg = "talk" };
        sync.Receive(slots);
        Assert.Empty(sync.Changes(slots));
        (slots[0], slots[8]) = (slots[8], slots[0]);
        Assert.Equal(new[] { 0, 8 }, sync.Changes(slots).Select(c => c.Index));
        Assert.Empty(sync.Changes(slots));
        // A visually empty unsupported original entry is preserved unless explicitly cleared.
        Assert.Equal(20, Assert.Single(sync.Changes(slots, force: 20)).Index);
        sync.Reset();
        Assert.Empty(sync.Changes(slots, force: 21));
        sync.Receive(slots);
        Assert.Equal(21, Assert.Single(sync.Changes(slots)).Index);
    }
}
