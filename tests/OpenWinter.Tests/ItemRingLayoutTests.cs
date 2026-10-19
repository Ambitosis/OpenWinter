using OpenWinter.Client.Gameplay.Logic;
using Xunit;
using C = OpenWinter.Client.Gameplay.Logic.ItemRingCommand;

namespace OpenWinter.Tests;

/// <summary>
/// The item ring's slots as the original shows them (nwmain scenes invitemring and storeradial): slot 0 north,
/// clockwise.
/// </summary>
public class ItemRingLayoutTests
{
    static ItemRingFacts Item(bool bag = false, bool equipped = false, bool equippable = false, bool offhand = false,
        int stack = 1, bool powers = false, bool store = false) => new(bag, equipped, equippable, offhand, stack, powers, store);

    [Fact]
    public void WithoutAStoreEquipSitsEastAndOffhandWest()
    {
        Assert.Equal([C.Examine, C.CastSpell, C.Equip, C.None, C.Drop, C.None, C.EquipOffhand, C.None],
            ItemRingLayout.Owned(Item(equippable: true, offhand: true)));
        Assert.Equal([C.Examine, C.CastSpell, C.Equip, C.None, C.Drop, C.None, C.None, C.None],
            ItemRingLayout.Owned(Item(equippable: true)));
        Assert.Equal([C.Examine, C.CastSpell, C.None, C.None, C.Drop, C.None, C.Unequip, C.None],
            ItemRingLayout.Owned(Item(equipped: true, equippable: true)));
    }

    [Fact]
    public void AStoreOpenPutsSellEastInPlaceOfEquip()
    {
        // Backpack sword and armour, gems (plain, stolen, plot, worth more than the store's gold): Sell E, nothing W.
        Assert.Equal([C.Examine, C.CastSpell, C.Sell, C.None, C.Drop, C.None, C.None, C.None],
            ItemRingLayout.Owned(Item(equippable: true, offhand: true, store: true)));
        Assert.Equal([C.Examine, C.CastSpell, C.Sell, C.None, C.Drop, C.None, C.None, C.None],
            ItemRingLayout.Owned(Item(store: true)));
        // A worn robe: Sell E and Unequip W.
        Assert.Equal([C.Examine, C.CastSpell, C.Sell, C.None, C.Drop, C.None, C.Unequip, C.None],
            ItemRingLayout.Owned(Item(equipped: true, equippable: true, store: true)));
        // A healer's kit and a scroll: their power at SE.
        Assert.Equal(C.Use, ItemRingLayout.Owned(Item(powers: true, store: true))[3]);
    }

    [Fact]
    public void StacksSplitWestAndBagsOpenEast()
    {
        Assert.Equal([C.Examine, C.CastSpell, C.Sell, C.Use, C.Drop, C.None, C.Split, C.None],
            ItemRingLayout.Owned(Item(stack: 5, powers: true, store: true)));
        Assert.Equal(C.Split, ItemRingLayout.Owned(Item(stack: 5, powers: true))[6]);
        Assert.Equal(C.Split, ItemRingLayout.Owned(Item(stack: 50, equippable: true, store: true))[6]);
        Assert.Equal([C.Examine, C.CastSpell, C.Open, C.None, C.Drop, C.None, C.None, C.None],
            ItemRingLayout.Owned(Item(bag: true, store: true)));
    }

    [Fact]
    public void AWareHasExamineNorthAndBuyEastOnly()
    {
        Assert.Equal([C.Examine, C.None, C.Buy, C.None, C.None, C.None, C.None, C.None], ItemRingLayout.Ware());
    }
}
