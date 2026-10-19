using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Requests;
using OpenWinter.Game.SaveGames;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class QuickbarPairPersistenceTests
{
    static (World World, Creature Pc) Load()
    {
        var world = new World(TestData.Module);
        var bic = Gff.Read(File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot, "data", "lcv", "cleric.bic")));
        var pc = world.Creatures.FromTemplate(bic);
        world.Register(pc);
        world.Inventory.Load(pc);
        return (world, pc);
    }

    static QuickbarSlot Slot(World world, Creature pc, int index = 4) => QuickbarRequests.Read(world, pc, _ => { })![index]!;
    static void Set(World world, Creature pc, QuickbarSlot? slot, int index = 30) =>
        ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(new { index, slot }));

    [DataFact]
    public void SavedLocalCharacterReplyOwnsReassignedPairIdsAndClearsWhileNativeDefaultsAllowMigration()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { CharacterSelection = true, Vault = VaultMode.Local, SaveGames = true, StartArea = "ss_town" });
        var native = File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot, "data", "lcv", "cleric.bic"));
        var (connection, pc) = Login(native);
        try
        {
            Assert.False(Reply().GetProperty("authoritative").GetBoolean()); // Legacy client-only layouts can migrate once.
            var before = Slot(world, pc);
            Send(4, before);
            Send(0, null);
            Assert.True(Reply().GetProperty("authoritative").GetBoolean()); // Current-session edits own the layout too.
            var saved = Gff.Write(ObjectSerializer.CreatureGff(world, pc, uint.MaxValue, true), "BIC ");
            connection.Disconnect(); server.Tick(1f / 30);
            (connection, pc) = Login(saved);
            Assert.Empty(pc.QuickbarEdits); // Authority must come from the saved BIC marker, not previous live edits.
            var reply = Reply();
            Assert.True(reply.GetProperty("authoritative").GetBoolean());
            var restored = reply.GetProperty("slots").Deserialize<QuickbarSlot?[]>()!;
            Assert.Null(restored[0]);
            Assert.True(restored[4]!.Paired);
            Assert.NotEqual(before.Item, restored[4]!.Item);
            Assert.NotEqual(before.SecondaryItem, restored[4]!.SecondaryItem);
            Assert.Equal(pc.Equipment[EquipSlot.RightHand].Id, restored[4]!.Item);
            Assert.Equal(pc.Equipment[EquipSlot.LeftHand].Id, restored[4]!.SecondaryItem);
        }
        finally { connection.Disconnect(); server.Tick(1f / 30); }

        (IConnection, Creature) Login(byte[] bic)
        {
            var (client, serverEnd) = LoopbackConnection.CreatePair();
            server.Accept(serverEnd);
            client.Send(new HelloMessage { PlayerName = "PairLocalReload" }.Write());
            server.Tick(1f / 30);
            client.Send(new CharacterChoiceMessage { Bic = bic }.Write());
            server.Tick(1f / 30);
            return (client, server.Sessions.Single(s => s.Creature != null).Creature!);
        }
        void Send(int index, QuickbarSlot? slot)
        {
            connection.Send(new ClientRequestMessage { Kind = "quickbar-set", Json = JsonSerializer.Serialize(new { index, slot }) }.Write());
            server.Tick(1f / 30);
        }
        JsonElement Reply()
        {
            connection.Send(new ClientRequestMessage { Kind = "quickbar" }.Write());
            server.Tick(1f / 30);
            JsonElement reply = default;
            while (connection.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type != MessageType.Notify) continue;
                var notify = NotifyMessage.Read(reader);
                if (notify.Kind == "quickbar") reply = JsonSerializer.Deserialize<JsonElement>(notify.Json);
            }
            return reply;
        }
    }

    [DataFact]
    public void InstalledClericPairImportsBothHandsAndUnchangedExportPreservesUnknownFields()
    {
        var (world, pc) = Load();
        var slot = Slot(world, pc);
        Assert.True(slot.Paired);
        Assert.Equal(pc.Equipment[EquipSlot.RightHand].Id, slot.Item);
        Assert.Equal(pc.Equipment[EquipSlot.LeftHand].Id, slot.SecondaryItem);
        Assert.Equal("nw_wblmml007", world.Get<Item>(slot.Item)!.ResRef);
        Assert.Equal("nw_ashmsw010", world.Get<Item>(slot.SecondaryItem)!.ResRef);
        pc.Template.GetList("QBList")[4].SetString("UnmodeledPair", "keep");
        pc.Template.GetList("QBList")[20].SetByte("QBObjectType", 99).SetString("UnmodeledAction", "keep");
        pc.Template.GetList("QBList")[21] = new GffStruct(0).SetByte("QBObjectType", 1)
            .SetDWord("QBItemInvSlot", 16).SetDWord("QBSecItemInvSlot", 64).SetString("UnmeasuredPair", "keep");
        Set(world, pc, slot);
        var bic = ObjectSerializer.CreatureGff(world, pc, uint.MaxValue, true);
        Assert.Equal("keep", bic.GetList("QBList")[4].GetString("UnmodeledPair"));
        Assert.Equal("keep", bic.GetList("QBList")[20].GetString("UnmodeledAction"));
        Assert.Equal(32, bic.GetList("QBList")[4].GetInt("QBSecItemInvSlot"));
        Assert.Equal("keep", bic.GetList("QBList")[21].GetString("UnmeasuredPair"));
        Assert.Equal(64, bic.GetList("QBList")[21].GetInt("QBSecItemInvSlot"));
        Assert.Null(QuickbarRequests.Read(world, pc, _ => { })![21]);
        Assert.Equal(16, bic.GetList("QBList")[30].GetInt("QBItemInvSlot"));
        Assert.Equal(32, bic.GetList("QBList")[30].GetInt("QBSecItemInvSlot"));
        var savedPair = JsonSerializer.Deserialize<QuickbarSlot>(bic.GetList("QBList")[30].GetString("ElySlot"))!;
        Assert.True(savedPair.Paired);
        Assert.Equal((0u, 0u), (savedPair.Item, savedPair.SecondaryItem));
    }

    [DataFact]
    public void MovedPairRoundTripsThroughIndependentUuidsAndSnapshotReferences()
    {
        var (world, pc) = Load();
        var pair = Slot(world, pc);
        Set(world, pc, pair);
        var main = world.Get<Item>(pair.Item)!;
        var off = world.Get<Item>(pair.SecondaryItem)!;
        Assert.True(world.Inventory.Unequip(pc, main, force: true));
        Assert.True(world.Inventory.Unequip(pc, off, force: true));
        var bic = ObjectSerializer.CreatureGff(world, pc, uint.MaxValue, true);
        var entry = bic.GetList("QBList")[30];
        Assert.Equal(1, entry.GetInt("QBObjectType"));
        Assert.Equal(uint.MaxValue, entry.GetInt("QBItemInvSlot"));
        Assert.Equal(uint.MaxValue, entry.GetInt("QBSecItemInvSlot"));
        Assert.Equal(off.InvX, entry.GetInt("QBSecItemReposX"));
        Assert.NotEmpty(entry.GetString("ElyItemUuid"));
        Assert.NotEmpty(entry.GetString("ElySecItemUuid"));
        Assert.NotEqual(entry.GetString("ElyItemUuid"), entry.GetString("ElySecItemUuid"));
        var saved = JsonSerializer.Deserialize<QuickbarSlot>(entry.GetString("ElySlot"))!;
        Assert.True(saved.Paired);
        Assert.Equal(0u, saved.Item);
        Assert.Equal(0u, saved.SecondaryItem);
        // Remove OpenWinter extensions: the native backpack form alone must locate both exact items.
        var nativeBic = Gff.Read(Gff.Write(bic, "BIC "));
        var nativeEntry = nativeBic.GetList("QBList")[30];
        foreach (string field in new[] { "ElySlot", "ElyItemUuid", "ElySecItemUuid" }) nativeEntry.Remove(field);
        var nativePair = BicQuickbar.Read(nativeBic)![30];
        Assert.True(nativePair.Paired);
        Assert.False(nativePair.UnsupportedItemOptions);
        Assert.Equal(main.ResRef, nativePair.ItemTemplate!.GetString("TemplateResRef"));
        Assert.Equal(off.ResRef, nativePair.SecondaryItemTemplate!.GetString("TemplateResRef"));
        var loaded = world.Creatures.FromTemplate(Gff.Read(Gff.Write(bic, "BIC ")));
        world.Register(loaded);
        var restored = Slot(world, loaded, 30);
        Assert.NotEqual(pair.Item, restored.Item);
        Assert.NotEqual(pair.SecondaryItem, restored.SecondaryItem);
        Assert.Equal(main.ResRef, world.Get<Item>(restored.Item)!.ResRef);
        Assert.Equal(off.ResRef, world.Get<Item>(restored.SecondaryItem)!.ResRef);
        var snapshot = WorldSnapshot.Capture(world, out var warnings);
        Assert.Empty(warnings);
        var fresh = new World(TestData.Module);
        WorldSnapshot.Apply(fresh, snapshot);
        var snapPc = fresh.Get<Creature>(pc.Id)!;
        Assert.Equal(pair.SecondaryItem, Slot(fresh, snapPc, 30).SecondaryItem);
        Assert.Same(snapPc.QuickbarSecondaryItems[4], snapPc.QuickbarSecondaryItems[30]);
    }

    [DataFact]
    public void AuthenticatedPairActivationTogglesRestoresAndReplacesBothHands()
    {
        var (world, pc) = Load();
        var pair = Slot(world, pc);
        void Activate()
        {
            Assert.True(ClientRequests.Handle(world, pc, "equip-pair", JsonSerializer.Serialize(new { item = pair.Item, secondaryItem = pair.SecondaryItem })));
            ActionQueues.Step(world, pc, 1f / 30);
        }
        void Both()
        {
            Assert.Equal(pair.Item, pc.Equipment[EquipSlot.RightHand].Id);
            Assert.Equal(pair.SecondaryItem, pc.Equipment[EquipSlot.LeftHand].Id);
        }
        Both();
        Activate();
        Assert.False(pc.Equipment.ContainsKey(EquipSlot.RightHand));
        Assert.False(pc.Equipment.ContainsKey(EquipSlot.LeftHand));
        Activate();
        Both();
        Assert.True(world.Inventory.Unequip(pc, world.Get<Item>(pair.SecondaryItem)!));
        Activate();
        Both();
        Assert.True(world.Inventory.Unequip(pc, world.Get<Item>(pair.Item)!));
        Activate();
        Both();
        var otherMain = world.Inventory.CreateOn("nw_wblcl001", pc, 1, "other_main")!;
        var otherOff = world.Inventory.CreateOn("nw_ashsw001", pc, 1, "other_off")!;
        Assert.True(world.Inventory.Equip(pc, otherMain, 4));
        Assert.True(world.Inventory.Equip(pc, otherOff, 5));
        Activate();
        Both();
        Assert.Same(pc, world.Inventory.Owner(otherMain));
        Assert.Same(pc, world.Inventory.Owner(otherOff));
        Activate();
        Assert.False(pc.Equipment.ContainsKey(EquipSlot.RightHand));
        Assert.False(pc.Equipment.ContainsKey(EquipSlot.LeftHand));
    }

    [DataFact]
    public void PairActionRechecksSecondaryOwnershipWhenTheQueuedRequestExecutes()
    {
        var (world, pc) = Load();
        var pair = Slot(world, pc);
        ClientRequests.Handle(world, pc, "equip-pair", JsonSerializer.Serialize(new { item = pair.Item, secondaryItem = pair.SecondaryItem }));
        Assert.Single(pc.Actions);
        world.Destroy(world.Get<Item>(pair.SecondaryItem)!);
        ActionQueues.Step(world, pc, 1f / 30);
        Assert.Equal(pair.Item, pc.Equipment[EquipSlot.RightHand].Id);
        ClientRequests.Handle(world, pc, "equip-pair", JsonSerializer.Serialize(new { item = pair.Item, secondaryItem = pair.Item }));
        Assert.Empty(pc.Actions);
    }

    [DataFact]
    public void BindingValidatesBothOwnersAndClearsWhenMemberIsLost()
    {
        var (world, pc) = Load();
        var pair = Slot(world, pc);
        Set(world, pc, pair);
        Assert.True(Slot(world, pc, 30).Paired);
        var before = pc.QuickbarEdits[30];
        foreach (var invalid in new[]
        {
            new QuickbarSlot { Kind = "item", Paired = true, Item = pair.Item, SecondaryItem = pair.Item },
            new QuickbarSlot { Kind = "item", Paired = true, Item = pair.Item, SecondaryItem = uint.MaxValue },
            new QuickbarSlot { Kind = "item", Paired = true, Item = pair.Item, SecondaryItem = pair.SecondaryItem, Prop = 0 },
            new QuickbarSlot { Kind = "spell", Paired = true, Item = pair.Item, SecondaryItem = pair.SecondaryItem },
        })
        {
            Set(world, pc, invalid);
            Assert.Same(before, pc.QuickbarEdits[30]);
        }
        var other = world.Creatures.FromTemplate(pc.Template);
        world.Register(other);
        var foreign = Slot(world, other);
        Set(world, pc, new() { Kind = "item", Paired = true, Item = pair.Item, SecondaryItem = foreign.SecondaryItem });
        Assert.Same(before, pc.QuickbarEdits[30]);
        Set(world, pc, new() { Kind = "item", Paired = true, Item = foreign.Item, SecondaryItem = pair.SecondaryItem });
        Assert.Same(before, pc.QuickbarEdits[30]);
        var off = world.Get<Item>(pair.SecondaryItem)!;
        Assert.NotNull(world.Inventory.MoveTo(off, other, merge: false));
        Assert.Null(Slot(world, pc, 30));
        Assert.Null(Slot(world, pc));
        Assert.NotNull(world.Inventory.MoveTo(off, pc, merge: false));
        Assert.Null(Slot(world, pc, 30)); // The native export remains cleared after ownership returns.
        Set(world, pc, pair);
        world.Destroy(off);
        Assert.Null(Slot(world, pc, 30));
        Assert.Equal(0, ObjectSerializer.CreatureGff(world, pc, uint.MaxValue, true).GetList("QBList")[30].GetInt("QBObjectType"));
        Set(world, pc, null);
        Assert.False(pc.QuickbarSecondaryItems.ContainsKey(30));
        Assert.False(pc.QuickbarItems.ContainsKey(30));
    }
}
