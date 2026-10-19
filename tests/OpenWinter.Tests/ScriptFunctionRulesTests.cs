using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// GetFactionBestAC/WorstAC's armour class and membership order, ActionEquipMostDamaging* and areas of effect as
/// spheres (parity suites faction_bestac, equip_mostdamaging, aoe_subarea). Base game data.
/// </summary>
public class ScriptFunctionRulesTests
{
    const int Fighter = 4;
    const int Longsword = 1, Warhammer = 5, Greataxe = 18, LightCrossbow = 7, HeavyCrossbow = 6, Bolt = 25;

    static (World W, Area Area) Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var w = new World(rm);
        w.Register(w.Module);
        var area = new Area { ResRef = "scriptrules", Tag = "scriptrules" };
        w.Register(area);
        w.Areas.Add(area);
        return (w, area);
    }

    static Creature Human(World w, Area area, Vector3 pos, int dex = 10, params int[] feats)
    {
        var c = w.Creatures.FromTemplate(new GffStruct());
        c.Classes.Add(new CreatureClass { Class = Fighter, Level = 1 });
        c.Race = 6;
        c.Abilities[1] = dex;
        c.MaxHitPoints = c.CurrentHitPoints = 50;
        c.Faction = FactionTable.Merchant;
        c.Feats.AddRange(new[] { 2, 3, 4, 44, 45, 46 });
        c.Feats.AddRange(feats);
        w.Register(c);
        CreatureRules.EnsureInit(w, c);
        c.Size = 3;
        w.AddToArea(c, area, pos, 0);
        return c;
    }

    static Item Carry(World w, Creature c, int baseItem, params ItemProperty[] props)
    {
        var item = new Item { BaseItem = baseItem, StackSize = baseItem == Bolt ? 20 : 1, ResRef = "t" + baseItem, Tag = "t" + baseItem, Identified = true };
        item.Properties.AddRange(props);
        w.Register(item);
        c.Inventory.Add(item);
        item.Possessor = c.Id;
        return item;
    }

    static Effect Fx(int type, params int[] ints)
    {
        var e = new Effect { Type = type, DurationType = 2 };
        for (int i = 0; i < ints.Length; i++) e.Ints[i] = ints[i];
        return e;
    }

    [DataFact]
    public void FactionArmourClassLeavesOutDexterityAndDodgeUnlessUncannyDodge()
    {
        var (w, area) = Make();
        var nimble = Human(w, area, new Vector3(2, 2, 0), dex: 30);
        var uncanny = Human(w, area, new Vector3(4, 2, 0), dex: 30, 195);
        var dodger = Human(w, area, new Vector3(6, 2, 0));
        dodger.Effects.Add(Fx(NwRules.EffectAcIncrease, 9, NwRules.AcDodge));
        var clumsy = Human(w, area, new Vector3(8, 2, 0), dex: 3);
        Assert.Equal(10, Game.Society.SocietyService.FactionArmorClass(w, nimble));
        Assert.Equal(20, Game.Society.SocietyService.FactionArmorClass(w, uncanny));
        Assert.Equal(10, Game.Society.SocietyService.FactionArmorClass(w, dodger));
        Assert.Equal(6, Game.Society.SocietyService.FactionArmorClass(w, clumsy));
    }

    [DataFact]
    public void FactionMembersComeInTheOrderTheyJoined()
    {
        var (w, area) = Make();
        var first = Human(w, area, new Vector3(2, 2, 0));
        var second = Human(w, area, new Vector3(4, 2, 0));
        first.Faction = FactionTable.Commoner;
        second.Faction = FactionTable.Commoner;
        first.Faction = FactionTable.Defender;
        second.Faction = FactionTable.Defender;
        second.Faction = FactionTable.Commoner;
        var members = w.System<Game.Society.SocietyService>().FactionMembers(FactionTable.Commoner, false);
        Assert.Equal(new[] { second }, members.Where(m => m == first || m == second));
        second.Faction = FactionTable.Defender;
        members = w.System<Game.Society.SocietyService>().FactionMembers(FactionTable.Defender, false);
        Assert.Equal(new[] { first, second }, members.Where(m => m == first || m == second));
    }

    [DataFact]
    public void MostDamagingMeleeScoresMaximumDamageAfterTheTargetsDefences()
    {
        var (w, area) = Make();
        var c = Human(w, area, new Vector3(2, 2, 0));
        var sword = Carry(w, c, Longsword, new ItemProperty { Type = 6, CostTableValue = 1 }); // +1: 9
        var axe = Carry(w, c, Greataxe); // 12
        Assert.Equal(axe, Assert.Single(MostDamagingWeapon.Melee(w, c, null, false)).Item);

        // Damage reduction 10/+1: the +1 longsword's 9 passes, the greataxe keeps 2.
        var armoured = Human(w, area, new Vector3(6, 2, 0));
        armoured.Effects.Add(Fx(EffectConstants.Of(w).DamageReduction, 10, 1));
        Assert.Equal(sword, Assert.Single(MostDamagingWeapon.Melee(w, c, armoured, false)).Item);

        // Immune to slashing: a warhammer (8) over the greataxe (0).
        var c2 = Human(w, area, new Vector3(2, 6, 0));
        Carry(w, c2, Greataxe);
        var hammer = Carry(w, c2, Warhammer);
        var immune = Human(w, area, new Vector3(6, 6, 0));
        immune.Effects.Add(Fx(EffectConstants.Of(w).DamageImmunityIncrease, CombatK.DamageSlashing, 100));
        Assert.Equal(hammer, Assert.Single(MostDamagingWeapon.Melee(w, c2, immune, false)).Item);
    }

    [DataFact]
    public void MostDamagingMeleeTakesTheLaterOfTwoEqualWeapons()
    {
        var (w, area) = Make();
        var c = Human(w, area, new Vector3(2, 2, 0));
        var older = Carry(w, c, Longsword);
        var newer = Carry(w, c, Longsword);
        // GetFirstItemInInventory lists the newer first; the later in that order (the older) wins.
        Assert.Equal(newer, w.Inventory.Items(c)[0]);
        Assert.Equal(older, Assert.Single(MostDamagingWeapon.Melee(w, c, null, false)).Item);
    }

    [DataFact]
    public void MostDamagingRangedGoesByAttackBonusThenFallsBackToMelee()
    {
        var (w, area) = Make();
        var c = Human(w, area, new Vector3(2, 2, 0));
        var light = Carry(w, c, LightCrossbow, new ItemProperty { Type = 56, CostTableValue = 3 }); // attack +3
        Carry(w, c, HeavyCrossbow);
        var bolts = Carry(w, c, Bolt);
        var choice = MostDamagingWeapon.Ranged(w, c, null);
        Assert.Equal(new[] { light, bolts }, choice.Select(x => x.Item));

        var c2 = Human(w, area, new Vector3(2, 6, 0));
        Carry(w, c2, LightCrossbow);
        var sword = Carry(w, c2, Longsword);
        Assert.Equal(sword, Assert.Single(MostDamagingWeapon.Ranged(w, c2, null)).Item);
    }

    [Fact]
    public void AreaOfEffectCirclesAreSpheres()
    {
        var aoe = new AreaOfEffect { Shape = 'C', Radius = 5, Position = new Vector3(0, 0, 4) };
        Assert.True(aoe.Contains(new Vector3(2, 0, 0)));   // 4.47 m
        Assert.False(aoe.Contains(new Vector3(3.1f, 0, 0))); // 5.06 m
        Assert.True(aoe.Contains(new Vector3(4.9f, 0, 4)));
    }
}
