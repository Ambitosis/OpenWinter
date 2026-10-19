using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using Xunit;

namespace OpenWinter.Tests;

public class StartingEquipmentTests
{
    static World NewWorld()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        return new World(resources);
    }

    static GffStruct Template(World world, int[] feats, params (int Slot, string Item)[] equipment) =>
        world.Resources.ReadGff("nw_bandit001", ResType.Utc)!.DeepClone()
            .SetList("FeatList", feats.Select(f => new GffStruct(1).SetWord("Feat", f)).ToList())
            .SetList("Equip_ItemList", equipment.Select(e =>
                new GffStruct((uint)e.Slot).SetResRef("EquippedRes", e.Item)).ToList())
            .SetList("ItemList", []);

    [DataFact]
    public void InvalidStartingWeaponIsRetainedAndSavedEquipmentIsNotRevalidated()
    {
        var world = NewWorld();
        var template = Template(world, [], (EquipSlot.RightHand, "nw_wswls001"));
        var spawned = world.Creatures.FromTemplate(template, validateStartingEquipment: true);
        world.Register(spawned);
        world.Inventory.Load(spawned);
        Assert.Empty(spawned.Equipment);
        var sword = Assert.Single(spawned.Inventory);
        Assert.Equal("nw_wswls001", sword.ResRef);
        Assert.Same(spawned, world.Inventory.Owner(sword));
        Assert.True(sword.InvX >= 0);

        var restored = world.Creatures.FromTemplate(template);
        Assert.Equal("nw_wswls001", restored.Equipment[EquipSlot.RightHand].ResRef);
        Assert.Empty(restored.Inventory);
        var proficient = world.Creatures.FromTemplate(
            Template(world, [45], (EquipSlot.RightHand, "nw_wswls001")), validateStartingEquipment: true);
        Assert.Single(proficient.Equipment);
        Assert.Empty(proficient.Inventory);
    }

    [DataFact]
    public void BlueprintHandPlacementDoesNotApplyOrdinaryEquipHandConflicts()
    {
        var world = NewWorld();
        var offhand = world.Creatures.FromTemplate(
            Template(world, [45], (EquipSlot.LeftHand, "nw_wswgs001")), validateStartingEquipment: true);
        Assert.Equal("nw_wswgs001", offhand.Equipment[EquipSlot.RightHand].ResRef);
        Assert.False(offhand.Equipment.ContainsKey(EquipSlot.LeftHand));
        var pair = world.Creatures.FromTemplate(Template(world, [45, 32],
            (EquipSlot.RightHand, "nw_wswgs001"), (EquipSlot.LeftHand, "nw_ashsw001")), validateStartingEquipment: true);
        Assert.Equal(2, pair.Equipment.Count);
        Assert.Empty(pair.Inventory);
    }

    [DataFact]
    public void BlueprintUsesAppearanceSizeBeforeRulesInitialization()
    {
        var world = NewWorld();
        var template = Template(world, [45], (EquipSlot.RightHand, "nw_wswgs001"));
        var rat = world.Resources.ReadGff("nw_rat001", ResType.Utc)!;
        template.SetWord("Appearance_Type", (int)rat.GetInt("Appearance_Type"));
        var small = world.Creatures.FromTemplate(template, validateStartingEquipment: true);
        Assert.Equal(1, small.Size);
        Assert.Empty(small.Equipment);
        Assert.Equal("nw_wswgs001", Assert.Single(small.Inventory).ResRef);
    }
}
