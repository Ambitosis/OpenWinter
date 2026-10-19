using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Rules measured on nwserver for the conditions/stealth/polymorph follow-ups: factions without a repute.fac
/// (faction_nofac), hit points of blueprints and Constitution items (hp_blueprints, hp_items), GetHasSpell of special
/// abilities (hasspell_abilities) and what death keeps (death_keeps).
/// </summary>
public class RulesFollowupTests
{
    static (World World, Area Area) Make(bool standardFactions = false)
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        if (standardFactions) world.Factions.LoadStandard();
        else world.Factions.Load(rm);
        var area = new Area { Tag = "rf_area", ResRef = "rf_area" };
        world.Register(area);
        world.Areas.Add(area);
        return (world, area);
    }

    static Creature Npc(World w, Area a, int faction)
    {
        var c = new Creature { Tag = "n", MaxHitPoints = 10, CurrentHitPoints = 10, Faction = faction };
        c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 1 });
        w.AddToArea(c, a, new Vector3(5, 5, 0), 0, exact: true);
        return c;
    }

    [DataFact]
    public void WithoutAReputeFacEveryNpcFactionIsAFriendAndTheStandardFactionsMove()
    {
        var (w, a) = Make();
        var f = w.Factions;
        Assert.True(f.Fallback);
        // STANDARD_FACTION_HOSTILE, COMMONER, MERCHANT, DEFENDER sit at ids 1, 4, 2, 3.
        Assert.Equal(new[] { 1, 4, 2, 3 }, Enumerable.Range(0, 4).Select(f.StandardFaction).ToArray());
        var hostile = Npc(w, a, 1);
        var merchant = Npc(w, a, 2);
        var defender = Npc(w, a, 3);
        var commoner = Npc(w, a, 4);
        foreach (var x in new[] { hostile, merchant, defender, commoner })
            foreach (var y in new[] { hostile, merchant, defender, commoner })
                Assert.Equal(100, f.Reputation(x, y));
        // FactionID 0 and ids past the list are the hostile faction.
        Assert.Equal(1, f.FactionOf(Npc(w, a, 0)));
        Assert.Equal(1, f.FactionOf(Npc(w, a, 6)));
        Assert.Equal(4, f.FactionOf(commoner));
        // SetStandardFactionReputation still works per faction.
        f.SetStanding(f.StandardFaction(3), hostile, 0);
        Assert.Equal(0, f.Reputation(defender, hostile));
        Assert.Equal(100, f.Reputation(commoner, hostile));
    }

    [DataFact]
    public void MaximumHitPointsComeFromTheRolledDiceAndCountConstitutionItems()
    {
        var (w, a) = Make(standardFactions: true);
        // nw_hen_bod_05: 22 rolled, Constitution 16 + 2 (gnome) = +4 over 8 levels, boots of +2 Constitution: 62.
        var bod = w.Creatures.FromBlueprint("nw_hen_bod_05")!;
        w.AddToArea(bod, a, new Vector3(5, 5, 0), 0, exact: true);
        Assert.Equal(62, CreatureRules.MaxHitPoints(w, bod));
        Assert.Equal(62, CreatureRules.CurrentHitPoints(w, bod));
        var boots = bod.Equipment.Values.First(i => i.ResRef == "nw_it_mboots006");
        w.System<InventorySystem>().Unequip(bod, boots);
        CreatureRules.SyncHitPoints(w, bod);
        Assert.Equal(54, bod.MaxHitPoints);
        Assert.Equal(54, bod.CurrentHitPoints);
        // The stored maximum leaves the boots out.
        Assert.Equal(54, CreatureRules.StoredMaxHitPoints(w, bod, bod.MaxHitPoints));
        // nw_beggmale stores MaxHitPoints 4 but reads 3: 2 rolled plus Toughness.
        var beggar = w.Creatures.FromBlueprint("nw_beggmale")!;
        w.AddToArea(beggar, a, new Vector3(8, 5, 0), 0, exact: true);
        Assert.Equal(3, CreatureRules.MaxHitPoints(w, beggar));
        Assert.Equal(3, CreatureRules.CurrentHitPoints(w, beggar));
    }

    [DataFact]
    public void ASpecialAbilityCountsOnceInGetHasSpell()
    {
        var (w, a) = Make(standardFactions: true);
        var c = Npc(w, a, 1);
        CreatureRules.EnsureInit(w, c);
        for (int i = 0; i < 3; i++) c.SpellAbilities.Add(new SpellAbility { Spell = 90, CasterLevel = 6, Ready = true });
        Assert.Equal(1, CreatureRules.SpellCount(w, c, 90));
        CreatureRules.SpendSpell(w, c, 90);
        CreatureRules.SpendSpell(w, c, 90);
        Assert.Equal(1, CreatureRules.SpellCount(w, c, 90));
        CreatureRules.SpendSpell(w, c, 90);
        Assert.Equal(0, CreatureRules.SpellCount(w, c, 90));
    }

    [DataFact]
    public void DeathKeepsTheEffectWhoseApplicationKilledAndStripsTheRest()
    {
        var (w, a) = Make(standardFactions: true);
        var k = EffectConstants.Of(w);
        var fx = w.System<EffectSystem>();
        var c = Npc(w, a, 1);
        CreatureRules.EnsureInit(w, c);
        Effect New(string type, int sub, params int[] ints)
        {
            var e = new Effect { Type = k["EFFECT_TYPE_" + type], SubType = sub, ScriptId = fx.NextId(), Creator = w.Module.Id };
            for (int i = 0; i < ints.Length; i++) e.Ints[i] = ints[i];
            return e;
        }
        fx.Apply(New("SKILL_INCREASE", k.Magical, 17, 1), k.Permanent, c, 0);
        fx.Apply(New("BLINDNESS", k.Unyielding), k.Permanent, c, 0);
        fx.Apply(New("SANCTUARY", k.Unyielding, 10), k.Permanent, c, 0);
        fx.Apply(New("INVISIBILITY", k.Unyielding, k["INVISIBILITY_TYPE_IMPROVED"]), k.Permanent, c, 0);
        HitPoints.Kill(w, c, w.Module.Id);
        Assert.True(c.IsDead);
        var left = c.Effects.Select(e => e.Type).ToHashSet();
        Assert.DoesNotContain(k["EFFECT_TYPE_SKILL_INCREASE"], left);
        Assert.DoesNotContain(k["EFFECT_TYPE_SANCTUARY"], left);
        Assert.Contains(k["EFFECT_TYPE_BLINDNESS"], left);
        Assert.Contains(k["EFFECT_TYPE_INVISIBILITY"], left); // improved invisibility stays
    }

    [DataFact]
    public void ACharmedCasterTakesUpItsDaggerProtectsItselfThenCastsAtItsEnemy()
    {
        var (w, a) = Make(standardFactions: true);
        w.EnableScripts();
        var bic = OpenWinter.Aurora.Formats.Gff.Read(File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot, "data", "lcv", "aluviandarkstar.bic")));
        var wiz = w.Creatures.FromTemplate(bic);
        wiz.Faction = FactionTable.Defender;
        w.AddToArea(wiz, a, new Vector3(20, 20, 0), 0, exact: true);
        var charmer = Npc(w, a, FactionTable.Hostile);
        w.Jump(charmer, new Location(a.Id, new Vector3(23, 20, 0), 0));
        var enemy = Npc(w, a, FactionTable.Hostile);
        w.Jump(enemy, new Location(a.Id, new Vector3(20, 29, 0), 0));
        wiz.Society.Seen.Add(enemy.Id);
        var k = EffectConstants.Of(w);
        var fx = w.System<EffectSystem>();
        fx.Apply(new Effect { Type = k["EFFECT_TYPE_CHARMED"], SubType = k.Magical, ScriptId = fx.NextId(), Creator = charmer.Id }, k.Permanent, wiz, 0);
        for (int i = 0; i < 16; i++) w.Step(0.25f);
        Assert.Equal("NW_WSWDG001", wiz.Equipment.Values.FirstOrDefault(x => x.BaseItem == 22)?.Tag);
        Assert.Contains(wiz.Effects, e => e.SpellId == 102); // Mage Armor first
    }
}
