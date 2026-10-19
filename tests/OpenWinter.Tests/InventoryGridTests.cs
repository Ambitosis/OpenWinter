using System.Numerics;
using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// The inventory grid: footprints, first-fit placement, a full backpack, moving within the grid
/// (free cell, swap, stack, refusals) and positions surviving a save.
/// </summary>
public class InventoryGridTests
{
    const int Potion = 49, MiscSmall = 24, Halberd = 10, Longsword = 1, Dagger = 22, Arrow = 20, LargeBox = 66;

    static (World World, GameServer Server, IConnection Client, Creature Pc) Login(bool empty = true)
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.ControlObject) me = ControlObjectMessage.Read(r).ObjectId;
        }
        var pc = world.Get<Creature>(me)!;
        if (empty) foreach (var i in pc.Inventory.ToList()) world.Destroy(i);
        return (world, server, client, pc);
    }

    static Item Make(World w, int baseItem, string resref)
    {
        var i = new Item { BaseItem = baseItem, ResRef = resref, Tag = resref };
        w.Register(i);
        return i;
    }

    static Item Give(World w, Creature pc, int baseItem, string resref) =>
        w.Inventory.MoveTo(Make(w, baseItem, resref), pc) ?? throw new InvalidOperationException("no room");

    [Fact]
    public void ShapeKeepsItemsOnOnePage()
    {
        var g = InventorySystem.BackpackGrid;
        Assert.True(g.Contains(0, 0, 2, 6));
        Assert.True(g.Contains(8, 4, 2, 2));
        Assert.False(g.Contains(9, 0, 2, 1));   // past the right edge
        Assert.False(g.Contains(0, 4, 1, 3));   // rows 4..6 cross into page 2
        Assert.True(g.Contains(0, 6, 1, 3));    // top of page 2
        Assert.False(g.Contains(0, 34, 1, 3));  // below the last page
        Assert.True(InventorySystem.PlaceableGrid.Contains(0, 400, 1, 1)); // containers grow
    }

    [DataFact]
    public void NewItemsGoToTheFirstFreePlaceRowByRow()
    {
        var (w, _, _, pc) = Login();
        Assert.Equal((1, 2), w.Inventory.Footprint(Make(w, Potion, "x")));
        var a = Give(w, pc, Potion, "p0");
        var b = Give(w, pc, Potion, "p1");
        Assert.Equal((0, 0), (a.InvX, a.InvY));
        Assert.Equal((1, 0), (b.InvX, b.InvY));
        for (int i = 2; i < 10; i++) Give(w, pc, Potion, "p" + i);
        // Row 0-1 full of potions: a dagger (1x1) goes to the next row.
        var d = Give(w, pc, Dagger, "d");
        Assert.Equal((0, 2), (d.InvX, d.InvY));
        // A halberd (2x5) can't straddle rows 5/6 (page edge), so it opens page 2.
        var h = Give(w, pc, Halberd, "h");
        Assert.Equal((0, 6), (h.InvX, h.InvY));
        // Taking one potion out frees its cells for the next potion.
        w.Inventory.Drop(a, pc.Location);
        var again = Give(w, pc, Potion, "p-again");
        Assert.Equal((0, 0), (again.InvX, again.InvY));
    }

    [DataFact]
    public void AFullBackpackRefusesWhatDoesNotFitButStacksStillMerge()
    {
        var (w, _, _, pc) = Login();
        var inv = w.Inventory;
        // 6 pages x 60 cells; arrows (1x2, stack 99) fill 180 places.
        var arrows = new List<Item>();
        for (int i = 0; i < 180; i++) arrows.Add(Give(w, pc, Arrow, i == 0 ? "arrow" : "arrow" + i));
        Assert.Null(inv.FirstFit(pc, Make(w, MiscSmall, "s")));
        var extra = Make(w, MiscSmall, "s");
        Assert.False(inv.HasRoomFor(pc, extra));
        Assert.Null(inv.MoveTo(extra, pc));
        Assert.Null(inv.Holder(extra));
        // A stack that merges into an existing one needs no new place.
        arrows[0].StackSize = 10;
        var more = Make(w, Arrow, "arrow");
        more.StackSize = 5;
        Assert.True(inv.HasRoomFor(pc, more));
        Assert.Same(arrows[0], inv.MoveTo(more, pc));
        Assert.Equal(15, arrows[0].StackSize);
        // Unequipping into a full backpack fails.
        var sword = Make(w, Longsword, "sword");
        pc.Equipment[EquipSlot.RightHand] = sword;
        sword.Possessor = pc.Id;
        Assert.False(inv.Unequip(pc, sword));
        Assert.Same(sword, pc.Equipment[EquipSlot.RightHand]);
    }

    [DataFact]
    public void MovingWithinTheGridChecksBoundsOverlapAndSwapsEqualSizes()
    {
        var (w, _, _, pc) = Login();
        var inv = w.Inventory;
        var p1 = Give(w, pc, Potion, "p1");          // (0,0) 1x2
        var p2 = Give(w, pc, Potion, "p2");          // (1,0) 1x2
        var sword = Give(w, pc, Longsword, "sword"); // (2,0) 1x4
        Assert.True(inv.MoveToCell(p1, pc, 5, 0));
        Assert.Equal((5, 0), (p1.InvX, p1.InvY));
        Assert.False(inv.MoveToCell(p1, pc, 9, 5));  // would cross onto page 2
        Assert.False(inv.MoveToCell(p1, pc, 10, 0)); // outside
        Assert.True(inv.MoveToCell(sword, pc, 1, 0)); // occupied: first fit uses the freed first column
        Assert.Equal((0, 0), (sword.InvX, sword.InvY));
        // Same size onto the other potion's cell: they swap.
        Assert.True(inv.MoveToCell(p1, pc, 1, 0));
        Assert.Equal((1, 0), (p1.InvX, p1.InvY));
        Assert.Equal((5, 0), (p2.InvX, p2.InvY));
        // Onto a matching stack: merges.
        var a1 = Give(w, pc, Arrow, "arrow");
        var a2 = Make(w, Arrow, "arrow");
        a2.StackSize = 7;
        Assert.Null(inv.MoveTo(a2, pc, merge: false, cell: (a1.InvX, a1.InvY))); // occupied cell, no merge asked
        Assert.True(inv.MoveTo(a2, pc, merge: false) != null);
        Assert.True(inv.MoveToCell(a2, pc, a1.InvX, a1.InvY));
        Assert.Equal(8, a1.StackSize);
        Assert.True(a2.IsDestroyed);
    }

    [Theory]
    [InlineData(-1, 7, 0, 7)]
    [InlineData(9, 7, 8, 7)]
    [InlineData(4, 5, 4, 6)]
    [InlineData(4, 11, 4, 10)]
    public void DropFootprintClampsWithinTheHoveredPage(int x, int y, int expectedX, int expectedY)
    {
        Assert.Equal(((int X, int Y)?)(expectedX, expectedY),
            InventoryGridPlacement.ClampToPage(InventorySystem.BackpackGrid, 2, 2, x, y, 1));
        Assert.Null(InventoryGridPlacement.ClampToPage(InventorySystem.BagGrid, 8, 2, x, y, 0));
    }

    [DataFact]
    public void OccupiedDropRelocatesWithoutMovingBlockersAndStillRejectsOutsideRequests()
    {
        var (w, _, _, pc) = Login();
        var first = Give(w, pc, Dagger, "first");
        var second = Give(w, pc, Dagger, "second");
        var incoming = Make(w, Halberd, "incoming");
        Assert.True(w.Inventory.MoveToCell(incoming, pc, 0, 0));
        Assert.Equal((2, 0), (incoming.InvX, incoming.InvY));
        Assert.Equal((0, 0), (first.InvX, first.InvY));
        Assert.Equal((1, 0), (second.InvX, second.InvY));
        Assert.Same(pc, w.Inventory.Holder(incoming));
        Assert.False(w.Inventory.MoveToCell(incoming, pc, 9, 0));
        Assert.Equal((2, 0), (incoming.InvX, incoming.InvY));
    }

    [DataFact]
    public void OccupiedDropsOpenChestPagesButCannotExpandBags()
    {
        var (w, _, _, pc) = Login();
        var chest = new Placeable { HasInventory = true };
        w.Register(chest);
        var bag = Give(w, pc, LargeBox, "bag");
        foreach (GameObject holder in new GameObject[] { chest, bag })
            for (int i = 0; i < 35; i++)
                Assert.NotNull(w.Inventory.MoveTo(Make(w, Dagger, "filler" + holder.Id + "_" + i), holder));
        var incoming = Give(w, pc, Dagger, "incoming");
        var sourceCell = (incoming.InvX, incoming.InvY);
        Assert.False(w.Inventory.MoveToCell(incoming, bag, 0, 0));
        Assert.Same(pc, w.Inventory.Holder(incoming));
        Assert.Equal(sourceCell, (incoming.InvX, incoming.InvY));
        Assert.Equal(35, bag.Inventory.Count);
        Assert.True(w.Inventory.MoveToCell(incoming, chest, 0, 0));
        Assert.Same(chest, w.Inventory.Holder(incoming));
        Assert.Equal((0, 5), (incoming.InvX, incoming.InvY));
        Assert.Equal(36, chest.Inventory.Count);
    }

    [DataFact]
    public void MoveRequestPlacesTheItemAndTheInventoryViewReportsCells()
    {
        var (w, server, client, pc) = Login();
        var p = Give(w, pc, Potion, "p");
        client.Send(new ClientRequestMessage { Kind = "item-move", Json = $"{{\"item\":{p.Id},\"x\":7,\"y\":3}}" }.Write());
        for (int i = 0; i < 4; i++) server.Tick(1f / 30);
        Assert.Equal((7, 3), (p.InvX, p.InvY));
        JsonElement? view = null;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type != MessageType.Notify) continue;
            var n = NotifyMessage.Read(r);
            if (n.Kind == "inventory-open") view = JsonDocument.Parse(n.Json).RootElement;
        }
        Assert.NotNull(view);
        var e = view!.Value.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetUInt32() == p.Id);
        Assert.Equal(7, e.GetProperty("x").GetInt32());
        Assert.Equal(3, e.GetProperty("y").GetInt32());
        Assert.Equal(10, view.Value.GetProperty("grid").GetProperty("width").GetInt32());
        Assert.Equal(6, view.Value.GetProperty("grid").GetProperty("pages").GetInt32());
    }

    [DataFact]
    public void AutoArrangePacksLargestFirstWithoutOverlap()
    {
        var (w, _, _, pc) = Login();
        var inv = w.Inventory;
        var small = Give(w, pc, Dagger, "d");
        var potion = Give(w, pc, Potion, "p");
        var halberd = Give(w, pc, Halberd, "h");
        inv.AutoArrange(pc);
        Assert.Equal((0, 0), (halberd.InvX, halberd.InvY));
        Assert.Equal((2, 0), (potion.InvX, potion.InvY));
        Assert.Equal((3, 0), (small.InvX, small.InvY));
    }

    [DataFact]
    public void LoadedCharactersKeepValidNonOverlappingCells()
    {
        var (w, _, _, pc) = Login(empty: false);
        var inv = w.Inventory;
        Assert.NotEmpty(pc.Inventory);
        var cells = new HashSet<(int, int)>();
        foreach (var item in pc.Inventory)
        {
            Assert.True(item.InvX >= 0 && item.InvY >= 0, $"{item.ResRef} unplaced");
            var (fw, fh) = inv.Footprint(item);
            Assert.True(InventorySystem.BackpackGrid.Contains(item.InvX, item.InvY, fw, fh));
            for (int i = 0; i < fw; i++) for (int j = 0; j < fh; j++) Assert.True(cells.Add((item.InvX + i, item.InvY + j)), "overlap");
        }
    }

    [DataFact]
    public void PositionsSurviveSavingIncludingBagContents()
    {
        var (w, _, _, pc) = Login();
        var inv = w.Inventory;
        var p = Give(w, pc, Potion, "potion_a");
        var s = Give(w, pc, Longsword, "sword_a");
        var bag = Give(w, pc, LargeBox, "bag_a");
        var gem = inv.MoveTo(Make(w, MiscSmall, "gem_a"), bag)!;
        Assert.True(inv.MoveToCell(p, pc, 8, 7));
        Assert.True(inv.MoveToCell(s, pc, 4, 0));
        Assert.True(inv.MoveToCell(gem, bag, 6, 2));

        // As written to a character file / save.
        var gff = ObjectSerializer.CreatureGff(w, pc, 0, asCharacter: true);
        var entries = gff.GetList("ItemList");
        var pe = entries.Single(e => e.GetString("TemplateResRef") == "potion_a");
        Assert.Equal((8, 7), ((int)pe.GetInt("Repos_PosX"), (int)pe.GetInt("Repos_Posy")));
        var bagEntry = entries.Single(e => e.GetString("TemplateResRef") == "bag_a");
        var ge = bagEntry.GetList("ItemList").Single();
        Assert.Equal((6, 2), ((int)ge.GetInt("Repos_PosX"), (int)ge.GetInt("Repos_Posy")));

        // And read back.
        var bytes = ObjectSerializer.ToBytes(w, pc)!;
        var back = (Creature)ObjectSerializer.FromBytes(w, bytes, new Location(pc.Area!.Id, pc.Position + new Vector3(1, 0, 0), 0), null)!;
        inv.Load(back);
        var p2 = back.Inventory.Single(i => i.ResRef == "potion_a");
        var s2 = back.Inventory.Single(i => i.ResRef == "sword_a");
        var bag2 = back.Inventory.Single(i => i.ResRef == "bag_a");
        Assert.Equal((8, 7), (p2.InvX, p2.InvY));
        Assert.Equal((4, 0), (s2.InvX, s2.InvY));
        inv.Load(bag2);
        Assert.Equal((6, 2), (bag2.Inventory.Single().InvX, bag2.Inventory.Single().InvY));
    }
}
