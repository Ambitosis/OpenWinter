using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Spell scrolls show their spell's iprp_spells icon wherever an item icon is drawn, as measured on the original
/// (visual scene scrollicons).
/// </summary>
public class ScrollIconTests
{
    static World BaseWorld()
    {
        var resources = new OpenWinter.Aurora.Resources.ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        return new World(resources);
    }

    static ItemProperty Cast(int row) => new() { Type = ItemRules.PropCastSpell, SubType = row, CostTable = 3, CostTableValue = 1 };

    [DataFact]
    public void StockScrollShowsItsSpellIcon()
    {
        var world = BaseWorld();
        var scroll = world.Items.FromBlueprint("nw_it_sparscr211")!;
        Assert.Equal(["iss_blinddeaf"], world.Inventory.Icon(scroll));
        var divine = world.Items.FromBlueprint("x1_it_spdvscr301")!;
        Assert.Equal(["iss_x1confla"], world.Inventory.Icon(divine));
    }

    [DataFact]
    public void TheCurrentFirstCastSpellPropertyDecidesIdentifiedOrNot()
    {
        var world = BaseWorld();
        // A custom scroll: its blueprint's Magic Circle replaced by Fireball, after its use limitations.
        var custom = world.Items.FromBlueprint("x2_it_sparscrmc")!;
        custom.Properties.RemoveAll(p => p.Type == ItemRules.PropCastSpell);
        custom.Properties.Add(Cast(108));
        Assert.Equal(["iss_fireball"], world.Inventory.Icon(custom));
        custom.Identified = false;
        Assert.Equal(["iss_fireball"], world.Inventory.Icon(custom));
        // Two Cast Spell properties: the first in property order, not the lowest row.
        var two = world.Items.FromBlueprint("nw_it_sparscr101")!;
        two.Properties.RemoveAll(p => p.Type == ItemRules.PropCastSpell);
        two.Properties.Add(Cast(151));
        two.Properties.Add(Cast(1));
        Assert.Equal(["iss_invisib"], world.Inventory.Icon(two));
    }

    [DataFact]
    public void ScrollsWithoutASpellIconAndOtherItemsKeepTheirOwnIcon()
    {
        var world = BaseWorld();
        var bare = world.Items.FromBlueprint("nw_it_sparscr101")!;
        bare.Properties.RemoveAll(p => p.Type == ItemRules.PropCastSpell);
        Assert.Equal(["iit_scroll_001"], world.Inventory.Icon(bare));
        // iprp_spells row 12 (Bless Weapon) has no Icon.
        bare.Properties.Add(Cast(12));
        Assert.Equal(["iit_scroll_001"], world.Inventory.Icon(bare));
        // A blank scroll (base item 102) with a Cast Spell property.
        var blank = world.Items.FromBlueprint("x2_it_cfm_bscrl")!;
        blank.Properties.Add(Cast(108));
        Assert.Equal(["iit_scroll_002"], world.Inventory.Icon(blank));
        foreach (var resref in new[] { "nw_it_mpotion008", "nw_wmgwn002", "nw_wmgrd002", "nw_it_mring001" })
        {
            var item = world.Items.FromBlueprint(resref)!;
            if (item.Properties.All(p => p.Type != ItemRules.PropCastSpell)) item.Properties.Add(Cast(108));
            Assert.DoesNotContain(world.Inventory.Icon(item), icon => icon.StartsWith("iss_"));
        }
    }
}
