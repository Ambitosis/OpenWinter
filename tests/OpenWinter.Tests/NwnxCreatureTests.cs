using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Progression;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// NWNX_Creature behaviour the NWNX parity lab can't show (players, combat rolls) or that is cheaper to pin here:
/// the settings reach the rules that use them.
/// </summary>
public class NwnxCreatureTests
{
    const int Fighter = 4, Wizard = 10, Rogue = 8, Longsword = 1;

    sealed class NullConnection : IConnection
    {
        public int Id => 0;
        public bool IsConnected => true;
        public void Send(byte[] message, bool reliable = true) { }
        public bool TryReceive(out byte[] message) { message = Array.Empty<byte>(); return false; }
        public void Disconnect() { }
    }

    sealed record Fx(World W, Area Area, NwnxRegistry Nwnx);

    static Fx Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var w = new World(rm);
        w.Register(w.Module);
        var area = new Area { ResRef = "nwnx_area", Tag = "nwnx_area" };
        w.Register(area);
        w.Areas.Add(area);
        w.Factions.SetFactionReputation(FactionTable.Hostile, FactionTable.Defender, 0);
        w.Factions.SetFactionReputation(FactionTable.Defender, FactionTable.Hostile, 0);
        return new Fx(w, area, w.System<NwnxRegistry>());
    }

    static Creature Creature(Fx f, int cls, int level, Vector3 pos, int faction = FactionTable.Defender)
    {
        var c = f.W.Creatures.FromTemplate(new GffStruct());
        c.Classes.Add(new CreatureClass { Class = cls, Level = level });
        c.Race = 6;
        c.MaxHitPoints = c.CurrentHitPoints = 50;
        c.Faction = faction;
        c.Feats.AddRange(new[] { 44, 45, 46 });
        f.W.Register(c);
        CreatureRules.EnsureInit(f.W, c);
        c.Size = 3;
        f.W.AddToArea(c, f.Area, pos, 0);
        return c;
    }

    static Game.Scripting.Effect Effect(int type, int value)
    {
        var e = new Game.Scripting.Effect { Type = type };
        e.Ints[0] = value;
        return e;
    }

    /// <summary>Calls a function with its arguments in the order the plugin takes them (the creature first).</summary>
    static void Call(Fx f, string function, params object[] args)
    {
        foreach (var a in args.Reverse())
            f.Nwnx.Stack.Push(a switch
            {
                GameObject o => NwnxValue.Object(o.Id),
                uint id => NwnxValue.Object(id),
                float x => NwnxValue.Float(x),
                string s => NwnxValue.String(s),
                bool b => NwnxValue.Int(b ? 1 : 0),
                int i => NwnxValue.Int(i),
                _ => throw new ArgumentException(a.ToString()),
            });
        Assert.True(f.Nwnx.Call("NWNX_Creature", function));
    }

    static int Int(Fx f, string function, params object[] args)
    {
        Call(f, function, args);
        return (int)f.Nwnx.PopAs(NwnxType.Int)!;
    }

    [DataFact]
    public void CriticalSettingsChangeTheThreatRangeAndMultiplier()
    {
        var f = Make();
        var c = Creature(f, Fighter, 5, new Vector3(5, 5, 0));
        var t = Creature(f, Fighter, 1, new Vector3(6, 5, 0), FactionTable.Hostile);
        var sword = new Item { BaseItem = Longsword };
        f.W.Register(sword);
        c.Equipment[EquipSlot.RightHand] = sword;
        var rules = (CreatureCombatRules)f.W.System<CombatSystem>().Rules;
        var a = rules.Context(c, t, sword, false, new ScriptedDice());
        Assert.Equal((19, 2), rules.Critical(a));
        // A modifier for every attack and one for longswords add up; an override for the main hand wins.
        Call(f, "SetCriticalRangeModifier", c, -2, 0, 0, -1);
        Call(f, "SetCriticalRangeModifier", c, -1, 0, 0, Longsword);
        Assert.Equal(16, rules.Critical(a).Threat);
        Call(f, "SetCriticalMultiplierOverride", c, 4, 1, 0, -1);
        Assert.Equal(4, rules.Critical(a).Multiplier);
        Call(f, "SetCriticalRangeOverride", c, 12, 2, 0, -1);
        Assert.Equal(16, rules.Critical(a).Threat); // off hand only
        var off = rules.Context(c, t, sword, true, new ScriptedDice());
        Assert.Equal(12, rules.Critical(off).Threat);
        Assert.Equal(2, rules.Critical(off).Multiplier);
    }

    [DataFact]
    public void BonusAttackLimitCapsModifyAttacks()
    {
        var f = Make();
        var c = Creature(f, Fighter, 1, new Vector3(5, 5, 0));
        c.Effects.Add(Effect(CombatK.EffectModifyAttacks, 5));
        var rules = f.W.System<CombatSystem>().Rules;
        Assert.Equal(6, rules.PlanRound(c).Count);
        Call(f, "SetMaximumBonusAttacks", c, 2, 0);
        Assert.Equal(3, rules.PlanRound(c).Count);
        c.Effects.Add(Effect(CombatK.EffectModifyAttacks, 5));
        Call(f, "SetMaximumBonusAttacks", c, 8, 0);
        Assert.Equal(9, rules.PlanRound(c).Count);
        Call(f, "SetMaximumBonusAttacks", c, -1, 0);
        Assert.Equal(5, Int(f, "GetMaximumBonusAttacks", c));
        Assert.Equal(6, rules.PlanRound(c).Count);
    }

    [DataFact]
    public void AttackRollOverrideTakesAwayTheAutomaticHit()
    {
        var f = Make();
        var c = Creature(f, Fighter, 1, new Vector3(5, 5, 0));
        var t = Creature(f, Fighter, 1, new Vector3(6, 5, 0), FactionTable.Hostile);
        t.NaturalAC = 40;
        var combat = f.W.System<CombatSystem>();
        combat.Dice = new ScriptedDice(1, 20);
        Assert.True(combat.Resolve(combat.Rules.Context(c, t, null, false, combat.Dice)).Hit);
        Call(f, "SetAttackRollOverride", c, 20, 0);
        combat.Dice = new ScriptedDice(1, 20);
        Assert.False(combat.Resolve(combat.Rules.Context(c, t, null, false, combat.Dice)).Hit);
        // For everyone: a natural 1 with +100 hits.
        Call(f, "SetAttackRollOverride", GameObject.InvalidId, 1, 100);
        combat.Dice = new ScriptedDice(1, 1);
        Assert.True(combat.Resolve(combat.Rules.Context(t, c, null, false, combat.Dice)).Hit);
    }

    [DataFact]
    public void ImmunityBypassWorksBothWays()
    {
        var f = Make();
        var source = Creature(f, Fighter, 1, new Vector3(5, 5, 0));
        var target = Creature(f, Fighter, 1, new Vector3(6, 5, 0));
        const int Stun = 15;
        target.Effects.Add(Effect(NwRules.EffectImmunity, Stun));
        Assert.True(CreatureRules.IsImmune(f.W, target, Stun, source));
        Call(f, "SetBypassEffectImmunity", source, Stun, 100, 0);
        Assert.False(CreatureRules.IsImmune(f.W, target, Stun, source));
        Assert.True(CreatureRules.IsImmune(f.W, target, Stun, null));
        // The target's own incoming chance to be immune disagrees: both are ignored.
        Call(f, "SetBypassEffectImmunity", target, -Stun, -100, 0);
        Assert.True(CreatureRules.IsImmune(f.W, target, Stun, source));
        // 255 stands for every type.
        Assert.False(CreatureRules.IsImmune(f.W, source, 2, target));
        Call(f, "SetBypassEffectImmunity", target, 255, -100, 0);
        Assert.True(CreatureRules.IsImmune(f.W, source, 2, target));
    }

    [DataFact]
    public void CasterLevelAndBonusSpellsReachTheSpellbook()
    {
        var f = Make();
        var c = Creature(f, Wizard, 10, new Vector3(5, 5, 0));
        c.Abilities[NwRules.AbilityInt] = 16;
        Assert.Equal(10, SpellSlots.ClassCasterLevel(f.W, c, Wizard));
        Call(f, "SetCasterLevelModifier", c, Wizard, 3, 0);
        Assert.Equal(13, SpellSlots.ClassCasterLevel(f.W, c, Wizard));
        Call(f, "SetCasterLevelOverride", c, Wizard, 5, 0);
        Assert.Equal(5, SpellSlots.ClassCasterLevel(f.W, c, Wizard));
        Call(f, "SetCasterLevelOverride", c, Wizard, -1, 0);
        Assert.Equal(13, SpellSlots.ClassCasterLevel(f.W, c, Wizard));
        int before = CreatureRules.SpellSlots(f.W, c, Wizard, 1);
        Call(f, "ModifyNumberBonusSpells", c, 0, 1, 2);
        Assert.Equal(before + 2, CreatureRules.SpellSlots(f.W, c, Wizard, 1));
        Assert.Equal(2, Int(f, "GetNumberOfBonusSpells", c, 0, 1));
    }

    [DataFact]
    public void MulticlassLimitAppliesToPlayersOnly()
    {
        var f = Make();
        var npc = Creature(f, Fighter, 2, new Vector3(5, 5, 0));
        Call(f, "SetMulticlassLimit", npc, 1, 0);
        Assert.Equal(0, Int(f, "GetMulticlassLimit", npc));
        var pc = Creature(f, Fighter, 2, new Vector3(7, 5, 0));
        pc.Player = new PlayerSession { Connection = new NullConnection(), PlayerName = "pc" };
        Assert.DoesNotContain(ProgressionRules.ClassErrors(f.W, pc, Rogue, player: true), e => e.Code == "class");
        Call(f, "SetMulticlassLimit", pc, 1, 0);
        Assert.Equal(1, Int(f, "GetMulticlassLimit", pc));
        Assert.Contains(ProgressionRules.ClassErrors(f.W, pc, Rogue, player: true), e => e.Code == "class");
        Assert.DoesNotContain(ProgressionRules.ClassErrors(f.W, pc, Fighter, player: true), e => e.Code == "class");
    }

    [DataFact]
    public void StorePriceLimitReplacesTheStores()
    {
        var f = Make();
        var c = Creature(f, Fighter, 1, new Vector3(5, 5, 0));
        var store = new Store { MarkDown = 100, MaxBuyPrice = 5 };
        f.W.Register(store);
        var item = new Item { BaseItem = Longsword, Cost = 100 };
        f.W.Register(item);
        var inv = f.W.Inventory;
        int capped = inv.BuyPrice(store, item, c);
        Assert.True(capped <= 5);
        Call(f, "SetMaxSellToStorePriceOverride", c, store, -1);
        int free = inv.BuyPrice(store, item, c);
        Assert.True(free > capped);
        Call(f, "SetMaxSellToStorePriceOverride", c, store, 3);
        Assert.Equal(3, inv.BuyPrice(store, item, c));
        Assert.Equal(3, Int(f, "GetMaxSellToStorePriceOverride", c, store));
        Call(f, "SetMaxSellToStorePriceOverride", c, store, -2);
        Assert.Equal(-2, Int(f, "GetMaxSellToStorePriceOverride", c, store));
        Assert.Equal(capped, inv.BuyPrice(store, item, c));
    }

    [DataFact]
    public void WalkRateCapHoldsWalkingButNotRunning()
    {
        var f = Make();
        var c = Creature(f, Fighter, 1, new Vector3(5, 5, 0));
        c.WalkRate = 2f; c.RunRate = 4f;
        c.WalkRequested = true;
        Assert.Equal(2f, Movement.MaxSpeed(f.W, c, out _), 3);
        Call(f, "SetWalkRateCap", c, 1500f);
        Assert.Equal(1.5f, Movement.MaxSpeed(f.W, c, out _), 3);
        c.WalkRequested = false;
        Assert.Equal(4f, Movement.MaxSpeed(f.W, c, out _), 3);
        Call(f, "SetWalkRateCap", c, -1f);
        c.WalkRequested = true;
        Assert.Equal(2f, Movement.MaxSpeed(f.W, c, out _), 3);
    }

    [DataFact]
    public void LastKillerReadsOutsideTheDeathEvent()
    {
        var f = Make();
        var c = Creature(f, Fighter, 1, new Vector3(5, 5, 0));
        var k = Creature(f, Fighter, 1, new Vector3(6, 5, 0));
        Call(f, "SetLastKiller", c, k);
        Assert.Equal(k.Id, c.LastKiller);
        HitPoints.Kill(f.W, c, k.Id);
        Assert.Equal(k.Id, c.LastKiller);
    }

    [DataFact]
    public void BlueprintCreaturesGetAOneEntryHistoryThatLevelsFollow()
    {
        var f = Make();
        var c = Creature(f, Fighter, 1, new Vector3(5, 5, 0));
        Assert.Equal(Fighter, Int(f, "GetClassByLevel", c, 1));
        Assert.Equal(-1, Int(f, "GetClassByLevel", c, 2));
        Call(f, "LevelUp", c, Rogue, 2, 255);
        Assert.Equal(3, c.TotalLevel);
        Assert.Equal(Rogue, Int(f, "GetClassByLevel", c, 3));
        Assert.Equal(6, Int(f, "GetMaxHitPointsByLevel", c, 2));
        Call(f, "LevelDown", c, 1);
        Assert.Equal(2, c.TotalLevel);
        Assert.Equal(-1, Int(f, "GetClassByLevel", c, 3));
    }
}
