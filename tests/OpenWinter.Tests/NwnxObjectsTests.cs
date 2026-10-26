using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>NWNX_Object, NWNX_Item, NWNX_Store and NWNX_Appearance logic that the parity lab can't reach or is cheaper here.</summary>
public class NwnxObjectsTests
{
    sealed class NullConnection : IConnection
    {
        public int Id => 0;
        public bool IsConnected => true;
        public void Send(byte[] message, bool reliable = true) { }
        public bool TryReceive(out byte[] message) { message = Array.Empty<byte>(); return false; }
        public void Disconnect() { }
    }

    static (World World, NwnxRegistry Registry) Setup(bool baseGame = false)
    {
        var resources = new ResMan(TestData.Paths);
        if (baseGame) resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var registry = world.System<NwnxRegistry>();
        registry.Log = _ => { };
        return (world, registry);
    }

    static void Call(NwnxRegistry r, string plugin, string function, params NwnxValue[] pushed)
    {
        foreach (var v in pushed) r.Stack.Push(v);
        Assert.True(r.Call(plugin, function));
    }

    [Fact]
    public void LocalsAreListedNewestFirstOncePerNameWithoutDefaults()
    {
        var v = new LocalVariables();
        void Int(string name, int value) { v.Ints[name] = value; v.Touch(name); }
        Int("a", 1); Int("b", 2); Int("c", 3);
        Assert.Equal(["c", "b", "a"], v.NewestFirst().Select(x => x.Name));
        Int("a", 4); // overwriting keeps the place
        Assert.Equal(["c", "b", "a"], v.NewestFirst().Select(x => x.Name));
        Int("b", 0); // a default value is not set...
        Assert.Equal(["c", "a"], v.NewestFirst().Select(x => x.Name));
        Int("b", 5); // ...and setting it again makes it the newest
        Assert.Equal(["b", "c", "a"], v.NewestFirst().Select(x => x.Name));
        v.Strings["a"] = "x"; v.Touch("a");
        Assert.Equal(1, v.NewestFirst().Single(x => x.Name == "a").Type); // the lowest type reports
        v.Ints.Remove("a"); v.Touch("a");
        Assert.Equal(3, v.NewestFirst().Single(x => x.Name == "a").Type);
    }

    [DataFact]
    public void PersistentVariablesRoundTripThroughTheirGffText()
    {
        var (_, r) = Setup();
        NwnxPersistence.Set(r, 5, "NWNX_Object!PERINT!pi", NwnxValue.Int(5), persist: true);
        NwnxPersistence.Set(r, 5, "NWNX_Object!PERINT!ti", NwnxValue.Int(6), persist: false);
        NwnxPersistence.Set(r, 5, "NWNX_Object!PERSTR!ps", NwnxValue.String("a b;c"), persist: true);
        NwnxPersistence.Set(r, 5, "NWNX_Object!PERFLT!pf", NwnxValue.Float(2.5f), persist: true);
        string text = NwnxPersistence.Write(r, 5);
        Assert.Equal("[INTMAP:1]<21>NWNX_Object!PERINT!pi = 5;[FLTMAP:1]<21>NWNX_Object!PERFLT!pf = 2.5;[STRMAP:1]<21>NWNX_Object!PERSTR!ps = <5>a b;c;", text);
        Assert.True(NwnxPersistence.Read(r, 9, text));
        Assert.Equal(5, NwnxPersistence.Get(r, 9, "NWNX_Object!PERINT!pi")?.Value);
        Assert.Equal("a b;c", NwnxPersistence.Get(r, 9, "NWNX_Object!PERSTR!ps")?.Value);
        Assert.Equal(2.5f, NwnxPersistence.Get(r, 9, "NWNX_Object!PERFLT!pf")?.Value);
        Assert.Null(NwnxPersistence.Get(r, 9, "NWNX_Object!PERINT!ti"));
        Assert.False(NwnxPersistence.Read(r, 9, "[INTMAP:1]<99>broken"));
    }

    [DataFact]
    public void ItemAppearanceStringsRoundTrip()
    {
        var (world, r) = Setup();
        var a = new Item();
        world.Register(a);
        a.Template.SetByte("Cloth1Color", 22);
        a.Template.SetByte("ModelPart1", 5);
        a.Template.SetByte("ArmorPart_Torso", 32);
        a.PartColorOverrides[7 * 6 + 0] = 40;
        string text = ItemPlugin.EntireAppearance(a);
        Assert.Equal(328, text.Length);
        Assert.Equal("0000160000000005", text[..16]);
        var b = new Item();
        world.Register(b);
        Call(r, "Item", "RestoreItemAppearance", NwnxValue.String(text), NwnxValue.Object(b.Id));
        Assert.Equal(text, ItemPlugin.EntireAppearance(b));
        Assert.Equal(40, b.PartColorOverrides[42]);
        Call(r, "Item", "RestoreItemAppearance", NwnxValue.String("garbage"), NwnxValue.Object(b.Id));
        Assert.Equal(text, ItemPlugin.EntireAppearance(b));
    }

    [DataFact]
    public void StoresReportRestrictedBaseItems()
    {
        var (world, r) = Setup();
        var s = new Store();
        world.Register(s);
        int Restricted(int baseItem)
        {
            Call(r, "Store", "GetIsRestrictedBuyItem", NwnxValue.Int(baseItem), NwnxValue.Object(s.Id));
            return (int)r.PopAs(NwnxType.Int)!;
        }
        Assert.Equal(0, Restricted(1));
        s.WillNotBuy.Add(1);
        Assert.Equal(1, Restricted(1));
        s.WillOnlyBuy.Add(2);
        Assert.Equal(1, Restricted(3));
        Assert.Equal(0, Restricted(2));
    }

    [DataFact]
    public void AppearanceOverridesChangeOnlyThatPlayersView()
    {
        var (world, r) = Setup();
        var pc = new Creature { Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "pc" } };
        var other = new Creature { Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "other" } };
        var npc = new Creature { AppearanceType = 6, Gender = 0, Colors = [1, 2, 3, 4], BodyParts = new ushort[19] };
        world.Register(pc); world.Register(other); world.Register(npc);
        void Set(int type, int value) => Call(r, "Appearance", "SetOverride", NwnxValue.Int(value), NwnxValue.Int(type), NwnxValue.Object(npc.Id), NwnxValue.Object(pc.Id));
        int Get(int type)
        {
            Call(r, "Appearance", "GetOverride", NwnxValue.Int(type), NwnxValue.Object(npc.Id), NwnxValue.Object(pc.Id));
            return (int)r.PopAs(NwnxType.Int)!;
        }
        Assert.Equal(-1, Get(0));
        Set(0, 85); Set(1, 1); Set(3, 9); Set(6, 12);
        Assert.Equal(85, Get(0));
        var look = new Appearance { Type = npc.AppearanceType, Gender = npc.Gender, Colors = npc.Colors, BodyParts = npc.BodyParts };
        string portrait = "";
        var seen = AppearancePlugin.ForViewer(world, pc, npc, look, ref portrait);
        Assert.NotNull(seen);
        Assert.Equal((85, (byte)1, (byte)9, (ushort)12), (seen!.Type, seen.Gender, seen.Colors[1], seen.BodyParts[0]));
        Assert.Equal(2, npc.Colors[1]); // the creature itself is unchanged
        Assert.Null(AppearancePlugin.ForViewer(world, other, npc, look, ref portrait));
        Set(0, -1);
        Assert.Equal(-1, Get(0));
        Set(-1, 0);
        Assert.Equal(-1, Get(1));
        Assert.Null(AppearancePlugin.ForViewer(world, pc, npc, look, ref portrait));
    }

    [DataFact]
    public void FeatModifiersFollowTheFeatsACreatureGainsAndLoses()
    {
        var (world, r) = Setup(baseGame: true);
        Call(r, "Feat", "SetFeatModifier", NwnxValue.Int(RulesModifiers.Unset), NwnxValue.Int(RulesModifiers.Unset), NwnxValue.Int(RulesModifiers.Unset),
            NwnxValue.Int(3), NwnxValue.Int(4), NwnxValue.Int(10)); // feat 10: AC +3
        var c = new Creature();
        world.Register(c);
        Call(r, "Creature", "AddFeat", NwnxValue.Int(10), NwnxValue.Object(c.Id));
        var innate = c.Effects.Where(e => e.Innate).ToList();
        Assert.Single(innate);
        Assert.Equal("NWNX_Feat_FeatMod_10", innate[0].Tag);
        Assert.True(innate[0].Hidden);
        Call(r, "Creature", "RemoveFeat", NwnxValue.Int(10), NwnxValue.Object(c.Id));
        Assert.DoesNotContain(c.Effects, e => e.Innate);
    }
}
