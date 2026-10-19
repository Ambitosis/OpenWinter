using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Polymorph and equipment (parity suite fx_polyitems): the creature's own items are put away and come back, the
/// form's are made and destroyed, and nothing is lost or duplicated whichever way the polymorph ends or the creature
/// is saved, copied or goes away meanwhile.
/// </summary>
public class PolymorphEquipmentTests
{
    const int Drow = 59, Bear = 21, Werewolf = 0;

    sealed record Setup(World World, Area Area)
    {
        public EffectConstants K => EffectConstants.Of(World);
        public EffectSystem Fx => World.System<EffectSystem>();

        public Creature Spawn(string tag = "t")
        {
            var c = new Creature { Tag = tag, MaxHitPoints = 20, CurrentHitPoints = 20, AppearanceType = 6, Race = 6 };
            c.Abilities[NwRules.AbilityCon] = 12;
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 1 });
            World.AddToArea(c, Area, new Vector3(5, 5, 0), 0);
            CreatureRules.EnsureInit(World, c);
            return c;
        }

        /// <summary>Armour, sword, helmet, ring and boots worn, and a potion in the backpack.</summary>
        public Dictionary<int, Item> Dress(Creature c)
        {
            var worn = new Dictionary<int, Item>
            {
                [EquipSlot.Chest] = Make("nw_aarcl009"), [EquipSlot.RightHand] = Make("nw_wswls001"), [EquipSlot.Head] = Make("nw_arhe001"),
                [EquipSlot.LeftRing] = Make("nw_it_mring001"), [EquipSlot.Boots] = Make("nw_it_mboots001"),
            };
            foreach (var (slot, item) in worn) { c.Equipment[slot] = item; item.Possessor = c.Id; }
            World.Inventory.MoveTo(Make("nw_it_mpotion001"), c);
            return worn;
        }

        public Item Make(string resref)
        {
            var item = World.Items.FromBlueprint(resref) ?? throw new InvalidOperationException(resref);
            World.Register(item);
            return item;
        }

        public Effect Polymorph(Creature c, int row, float duration = 30, bool locked = false)
        {
            var e = new Effect { Type = K["EFFECT_TYPE_POLYMORPH"], SubType = K.Magical, ScriptId = Fx.NextId() };
            e.Ints[0] = row;
            e.Ints[1] = locked ? 1 : 0;
            Fx.Apply(e, duration > 0 ? K.Temporary : K.Permanent, c, duration);
            return c.Effects.First(x => x.Type == e.Type && !x.Hidden);
        }

        public void Step(float seconds) { for (float t = 0; t < seconds; t += 0.05f) World.Step(0.05f); }

        /// <summary>Every live item in the world that names <paramref name="c"/> as its possessor.</summary>
        public List<Item> ItemsOf(Creature c) =>
            World.AllObjects.OfType<Item>().Where(i => !i.IsDestroyed && i.Possessor == c.Id).ToList();
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        var area = new Area { Tag = "poly_area", ResRef = "poly_area" };
        world.Register(area);
        world.Areas.Add(area);
        return new Setup(world, area);
    }

    /// <summary>The creature wears exactly <paramref name="worn"/> again (the same objects), and nothing else is around.</summary>
    static void AssertRestored(Setup s, Creature c, Dictionary<int, Item> worn, int backpack = 1)
    {
        Assert.Null(c.PolymorphForm);
        Assert.Equal(worn.OrderBy(kv => kv.Key), c.Equipment.OrderBy(kv => kv.Key));
        Assert.Equal(backpack, c.Inventory.Count);
        Assert.Equal(worn.Count + backpack, s.ItemsOf(c).Count);
        Assert.Equal(6, c.AppearanceType);
    }

    [DataFact]
    public void OwnItemsArePutAwayAndTheFormsAreWorn()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        s.Polymorph(c, Drow);

        Assert.Equal(new[] { EquipSlot.RightHand, EquipSlot.CreatureArmour }, c.Equipment.Keys.ToArray());
        Assert.Equal("x2_wdrowls004", c.Equipment[EquipSlot.RightHand].ResRef, ignoreCase: true);
        Assert.Equal("nw_it_creitem058", c.Equipment[EquipSlot.CreatureArmour].ResRef, ignoreCase: true);
        foreach (var item in worn.Values)
        {
            Assert.Same(c, s.World.Inventory.Possessor(item, returnBags: false)); // still possessed
            Assert.DoesNotContain(item, s.World.Inventory.Items(c));             // but not in the backpack
            Assert.False(c.Equipment.ContainsValue(item));                       // nor in a slot
        }
        // Nothing can be equipped or unequipped meanwhile.
        var potion = c.Inventory[0];
        Assert.False(s.World.Inventory.Equip(c, worn[EquipSlot.Head], 0));
        Assert.False(s.World.Inventory.Unequip(c, c.Equipment[EquipSlot.CreatureArmour]));
        Assert.Same(potion, c.Inventory.Single());
    }

    [DataFact]
    public void ExpiryRestoresTheSameItemsHalfASecondLater()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        var e = s.Polymorph(c, Bear, 2);
        var form = c.Equipment.Values.ToList();
        Assert.Equal(4, form.Count);
        s.Step(2.2f);
        Assert.Contains(e, c.Effects);   // still listed, marked as ending
        Assert.Equal(1, e.Ints[5]);
        Assert.NotNull(c.PolymorphForm);
        s.Step(0.5f);
        Assert.DoesNotContain(e, c.Effects);
        AssertRestored(s, c, worn);
        Assert.All(form, i => Assert.True(i.IsDestroyed));
    }

    [DataFact]
    public void RemoveEffectAndDispelRestoreAfterHalfASecond()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        var e = s.Polymorph(c, Drow);
        s.Fx.RemoveScripted(c, e);
        Assert.NotNull(c.PolymorphForm);
        s.Step(0.6f);
        AssertRestored(s, c, worn);

        var dispel = new Effect { Type = s.K.DispelAll, SubType = s.K.Magical };
        dispel.Ints[0] = 40;
        var again = s.Polymorph(c, Drow);
        again.CasterLevel = 1;
        for (int tries = 0; tries < 20 && c.PolymorphForm is { Ending: false }; tries++)
        {
            s.Fx.Apply(dispel, s.K.Instant, c, 0);
            s.Step(0.1f);
        }
        s.Step(0.6f);
        AssertRestored(s, c, worn);
    }

    [DataFact]
    public void DeathAndRestRestoreAtOnce()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        s.Polymorph(c, Drow);
        CreatureRules.Rest(s.World, c);
        AssertRestored(s, c, worn);

        s.Polymorph(c, Bear);
        HitPoints.Kill(s.World, c, GameObject.InvalidId);
        AssertRestored(s, c, worn);
        Assert.True(c.IsDead);
    }

    [DataFact]
    public void ANewPolymorphReplacesTheOldOneWithoutLosingAnything()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        s.Polymorph(c, Drow);
        var drowItems = c.Equipment.Values.ToList();
        s.Polymorph(c, Werewolf, 2);
        Assert.All(drowItems, i => Assert.True(i.IsDestroyed));
        Assert.Single(c.Effects, x => x.Type == s.K["EFFECT_TYPE_POLYMORPH"]);
        Assert.Equal(4, c.Equipment.Count);
        Assert.Equal(171, c.AppearanceType);
        s.Step(3);
        AssertRestored(s, c, worn);
    }

    [DataFact]
    public void SavingWhilePolymorphedSavesTheCreatureInItsOwnForm()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        s.Polymorph(c, Drow);

        // As a character file (logging out, server vault) and as an object (campaign database, SQL, save game).
        var bic = ObjectSerializer.CreatureGff(s.World, c, 0xFFFFFFFF, asCharacter: true);
        Assert.Equal(6, (int)bic.GetInt("Appearance_Type"));
        var saved = bic.GetList("Equip_ItemList").Select(x => ((int)x.StructType, x.GetString("TemplateResRef").ToLowerInvariant())).OrderBy(x => x.Item1).ToList();
        Assert.Equal(worn.Select(kv => (kv.Key, kv.Value.ResRef.ToLowerInvariant())).OrderBy(x => x.Item1).ToList(), saved);
        Assert.Single(bic.GetList("ItemList"));

        var bytes = ObjectSerializer.ToBytes(s.World, c)!;
        var back = (Creature)ObjectSerializer.FromBytes(s.World, bytes, new Location(s.Area.Id, new Vector3(8, 8, 0), 0), null)!;
        Assert.Null(back.PolymorphForm);
        Assert.DoesNotContain(back.Effects, x => x.Type == s.K["EFFECT_TYPE_POLYMORPH"]);
        Assert.Equal(6, back.AppearanceType);
        Assert.Equal(worn.Keys.OrderBy(k => k), back.Equipment.Keys.OrderBy(k => k));
        Assert.All(back.Equipment.Values, i => Assert.DoesNotContain(i, worn.Values)); // copies, not the originals

        // Saving doesn't disturb the live creature: still polymorphed, and it gets its own items back later.
        Assert.NotNull(c.PolymorphForm);
        s.Fx.EndPolymorphNow(c);
        AssertRestored(s, c, worn);
    }

    [DataFact]
    public void CopyingAPolymorphedCreatureCopiesItsOwnForm()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        s.Polymorph(c, Drow);
        var copy = (Creature)s.World.Inventory.CopyObject(c, new Location(s.Area.Id, new Vector3(9, 9, 0), 0), null, "copy", false)!;
        Assert.Null(copy.PolymorphForm);
        Assert.Equal(6, copy.AppearanceType);
        Assert.Equal(worn.Keys.OrderBy(k => k), copy.Equipment.Keys.OrderBy(k => k));
        Assert.All(copy.Equipment.Values, i => Assert.DoesNotContain(i, worn.Values));
    }

    [DataFact]
    public void LoggingOutOrBeingDestroyedWhilePolymorphedLeavesNoItemsBehind()
    {
        var s = Make();
        var c = s.Spawn();
        var worn = s.Dress(c);
        s.Polymorph(c, Drow);
        var form = c.Equipment.Values.ToList();
        var potion = c.Inventory[0];
        s.World.Destroy(c);
        Assert.All(worn.Values.Concat(form).Append(potion), i => Assert.True(i.IsDestroyed));
        Assert.Empty(s.ItemsOf(c));
        s.Step(1); // nothing pending blows up after the creature is gone
    }

    [DataFact]
    public void ItemsThatLeaveWhilePolymorphedStayGone()
    {
        var s = Make();
        var c = s.Spawn();
        var other = s.Spawn("other");
        var worn = s.Dress(c);
        s.Polymorph(c, Drow);

        // Taken by someone else: theirs now, and not put back in the slot (the original duplicates it here).
        var armour = worn[EquipSlot.Chest];
        Assert.NotNull(s.World.Inventory.MoveTo(armour, other));
        // Destroyed: gone.
        var helmet = worn[EquipSlot.Head];
        s.World.Destroy(helmet);
        // The form's own items can't leave.
        var hide = c.Equipment[EquipSlot.CreatureArmour];
        Assert.Null(s.World.Inventory.MoveTo(hide, other));
        Assert.False(s.World.Inventory.Drop(hide, c.Location));

        s.Step(31);
        Assert.Null(c.PolymorphForm);
        Assert.False(c.Equipment.ContainsValue(armour));
        Assert.Same(other, s.World.Inventory.Possessor(armour, returnBags: false));
        Assert.False(c.Equipment.ContainsKey(EquipSlot.Head));
        Assert.True(hide.IsDestroyed);
        Assert.Equal(3, c.Equipment.Count);
        Assert.Equal(4, s.ItemsOf(c).Count); // sword, ring, boots, potion
    }

    [DataFact]
    public void HitPointsFollowTheFormLikeTheOriginal()
    {
        var s = Make();
        var c = s.Spawn();
        c.MaxHitPoints = CreatureRules.MaxHitPoints(s.World, c);
        c.CurrentHitPoints = c.MaxHitPoints;
        int max = c.MaxHitPoints;
        // A drow (no Constitution of its own, the elf's -2) loses no hit points going in and gains one coming back.
        var e = s.Polymorph(c, Drow);
        Assert.Equal(max - 1, c.MaxHitPoints);
        Assert.Equal(max, c.CurrentHitPoints);
        s.Fx.RemoveScripted(c, e);
        s.Step(0.6f);
        Assert.Equal(max, c.MaxHitPoints);
        Assert.Equal(max + 1, c.CurrentHitPoints);
        // A bear's bonus hit points running out doesn't end the polymorph.
        CreatureRules.Rest(s.World, c);
        s.Polymorph(c, Bear);
        HitPoints.Damage(s.World, c, GameObject.InvalidId, 10, 1, 0);
        Assert.NotNull(c.PolymorphForm);
        Assert.Contains(c.Effects, x => x.Type == s.K["EFFECT_TYPE_POLYMORPH"]);
    }
}
