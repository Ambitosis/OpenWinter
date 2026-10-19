using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Effect behaviour measured on the original server (parity suites fx_*), checked without scripts.</summary>
public class EffectParityTests
{
    sealed record Setup(World World, Area Area)
    {
        public EffectConstants K => EffectConstants.Of(World);
        public EffectSystem Fx => World.System<EffectSystem>();

        public Creature Spawn(int dex = 13, int level = 1)
        {
            var c = new Creature { Tag = "t", MaxHitPoints = 20, CurrentHitPoints = 20 };
            c.Abilities[NwRules.AbilityDex] = dex;
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = level });
            World.AddToArea(c, Area, new Vector3(5, 5, 0), 0);
            return c;
        }

        public Effect New(string type, params int[] ints)
        {
            var e = new Effect { Type = K["EFFECT_TYPE_" + type], SubType = K.Magical, ScriptId = Fx.NextId() };
            for (int i = 0; i < ints.Length; i++) e.Ints[i] = ints[i];
            return e;
        }

        public void Step(float seconds) { for (float t = 0; t < seconds; t += 0.25f) World.Step(0.25f); }
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        var area = new Area { Tag = "fx_area", ResRef = "fx_area" };
        world.Register(area);
        world.Areas.Add(area);
        return new Setup(world, area);
    }

    [DataFact]
    public void EffectListKeepsTheOriginalOrder()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        s.Fx.Apply(s.New("SKILL_INCREASE", 17, 1), k.Permanent, c, 0);
        s.Fx.Apply(s.New("AC_INCREASE", 1, 0, 4103), k.Permanent, c, 0);
        s.Fx.Apply(s.New("SAVING_THROW_INCREASE", 0, 1, 0), k.Permanent, c, 0);
        s.Fx.Apply(s.New("ABILITY_INCREASE", 0, 1), k.Permanent, c, 0);
        s.Fx.Apply(s.New("HASTE"), k.Permanent, c, 0);
        s.Fx.Apply(s.New("AC_INCREASE", 2, 0, 4103), k.Permanent, c, 0);
        // Haste, saves, ability, AC (in application order), skills.
        Assert.Equal(new[] { 36, 50, 38, 46, 46, 54 }, c.Effects.Select(e => e.Type));
        Assert.Equal(new[] { 1, 2 }, c.Effects.Where(e => e.Type == 46).Select(e => e.Ints[0]));
    }

    [DataFact]
    public void LastingEffectsAppliedAsInstantStayHidden()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        int ac = CreatureRules.ArmorClass(s.World, c).Total;
        var deflect = s.New("AC_INCREASE", 2, NwRules.AcDeflection, 4103);
        s.Fx.Apply(deflect, k.Instant, c, 0);
        Assert.Equal(ac + 2, CreatureRules.ArmorClass(s.World, c).Total);
        Assert.All(c.Effects, e => Assert.True(e.Hidden));

        // Scripts can't remove it, and rest doesn't either.
        Assert.False(s.Fx.RemoveScripted(c, deflect));
        CreatureRules.Rest(s.World, c);
        Assert.Equal(ac + 2, CreatureRules.ArmorClass(s.World, c).Total);
        Assert.False(EffectQueries.HasSpell(c, -1));
    }

    [DataFact]
    public void AbilityDecreasesStopAtThree()
    {
        var s = Make();
        var c = s.Spawn(dex: 13);
        var k = s.K;
        s.Fx.Apply(s.New("ABILITY_DECREASE", NwRules.AbilityDex, 20), k.Permanent, c, 0);
        Assert.Equal(3, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityDex));
        Assert.Equal(10, c.Effects.Single().Ints[1]);
        // Nothing left to take: the next one doesn't land.
        s.Fx.Apply(s.New("ABILITY_DECREASE", NwRules.AbilityDex, 2), k.Permanent, c, 0);
        Assert.Single(c.Effects);
    }

    [DataFact]
    public void HelplessCreaturesHaveDexterityThree()
    {
        var s = Make();
        var c = s.Spawn(dex: 23);
        var k = s.K;
        s.Fx.Apply(s.New("ABILITY_INCREASE", NwRules.AbilityDex, 4), k.Permanent, c, 0);
        Assert.Equal(27, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityDex));
        s.Fx.Apply(s.New("PARALYZE", 8), k.Permanent, c, 0);
        Assert.Equal(3, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityDex));
        Assert.False(c.Commandable);
        s.Fx.RemoveWhere(c, e => e.Type == k["EFFECT_TYPE_PARALYZE"]);
        Assert.Equal(27, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityDex));
        // Commandable again once the removing script is over (nwserver state_commandable).
        s.World.Step(0.05f);
        Assert.True(c.Commandable);
    }

    [DataFact]
    public void HasteAndSlowChangeArmourClassAndCancelOut()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        int ac = CreatureRules.ArmorClass(s.World, c).Total, reflex = CreatureRules.SavingThrow(s.World, c, NwRules.SaveReflex);
        s.Fx.Apply(s.New("HASTE"), k.Permanent, c, 0);
        Assert.Equal(ac + 4, CreatureRules.ArmorClass(s.World, c).Total);
        s.Fx.Apply(s.New("SLOW"), k.Permanent, c, 0);
        Assert.Equal(ac, CreatureRules.ArmorClass(s.World, c).Total);
        s.Fx.RemoveWhere(c, e => e.Type == k["EFFECT_TYPE_HASTE"]);
        Assert.Equal(ac - 2, CreatureRules.ArmorClass(s.World, c).Total);
        Assert.Equal(reflex - 2, CreatureRules.SavingThrow(s.World, c, NwRules.SaveReflex));
    }

    [DataFact]
    public void BonusesFromOneSpellDontStack()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        Effect Str(int n, int spell) { var e = s.New("ABILITY_INCREASE", NwRules.AbilityStr, n); e.SpellId = spell; return e; }
        s.Fx.Apply(Str(4, 50), k.Permanent, c, 0);
        s.Fx.Apply(Str(3, 50), k.Permanent, c, 0);
        Assert.Equal(14, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityStr));
        s.Fx.Apply(Str(3, 51), k.Permanent, c, 0);
        s.Fx.Apply(Str(1, -1), k.Permanent, c, 0);
        Assert.Equal(18, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityStr));
    }

    [DataFact]
    public void RestEndsAllButPermanentSupernaturalAndUnyieldingEffects()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        void Add(int subType, int duration, int skill)
        {
            var e = s.New("SKILL_INCREASE", skill, 1);
            e.SubType = subType;
            s.Fx.Apply(e, duration, c, 300);
        }
        Add(k.Magical, k.Permanent, 1);
        Add(k.Extraordinary, k.Permanent, 2);
        Add(k.Supernatural, k.Permanent, 3);
        Add(k.Unyielding, k.Permanent, 4);
        Add(k.Supernatural, k.Temporary, 5);
        Add(k.Unyielding, k.Temporary, 6);
        CreatureRules.Rest(s.World, c);
        Assert.Equal(new[] { 3, 4, 6 }, c.Effects.Select(e => e.Ints[0]));
    }

    [DataFact]
    public void AsManyNegativeLevelsAsHitDiceKill()
    {
        var s = Make();
        var c = s.Spawn(level: 3);
        var k = s.K;
        s.Fx.Apply(s.New("NEGATIVELEVEL", 2), k.Permanent, c, 0);
        s.Step(1);
        Assert.False(c.IsDead);
        s.Fx.Apply(s.New("NEGATIVELEVEL", 1), k.Permanent, c, 0);
        s.Step(1);
        Assert.True(c.IsDead);
        Assert.Equal(-11, c.CurrentHitPoints);
    }

    [DataFact]
    public void RemovingAValueRemovesEveryApplicationOfIt()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        var ac = s.New("AC_INCREASE", 3, 0, 4103);
        s.Fx.Apply(ac, k.Permanent, c, 0);
        s.Fx.Apply(s.New("SKILL_INCREASE", 17, 1), k.Permanent, c, 0);
        s.Fx.Apply(ac, k.Permanent, c, 0);
        Assert.True(s.Fx.RemoveScripted(c, ac.Copy()));
        Assert.Equal(new[] { 54 }, c.Effects.Select(e => e.Type));
    }

    [DataFact]
    public void DeathImmunityDoesntMakeDeathSavesAutomatic()
    {
        var s = Make();
        var c = s.Spawn();
        s.Fx.Apply(s.New("IMMUNITY", s.K["IMMUNITY_TYPE_DEATH"]), s.K.Permanent, c, 0);
        Assert.Equal(CreatureRules.SaveFailed, CreatureRules.RollSave(s.World, c, NwRules.SaveFort, 30, NwRules.SaveTypeDeath, random: _ => 0));
        s.Fx.Apply(s.New("IMMUNITY", s.K["IMMUNITY_TYPE_MIND_SPELLS"]), s.K.Permanent, c, 0);
        Assert.Equal(CreatureRules.SaveImmune, CreatureRules.RollSave(s.World, c, NwRules.SaveWill, 30, NwRules.SaveTypeMind, random: _ => 0));
    }

    [DataFact]
    public void NothingResistsOutsideASpell()
    {
        var s = Make();
        var caster = s.Spawn();
        var target = s.Spawn();
        s.Fx.Apply(s.New("SPELL_RESISTANCE_INCREASE", 50), s.K.Permanent, target, 0);
        Assert.Equal(OpenWinter.Game.Spells.SpellDefense.NotResisted, OpenWinter.Game.Spells.SpellDefense.Resist(s.World, caster, target, null));
    }

    [DataFact]
    public void PoisonTakesASaveThenDamagesAgainAMinuteLater()
    {
        var s = Make();
        var k = s.K;
        var c = s.Spawn(dex: 14);
        c.Abilities[NwRules.AbilityCon] = 14;
        // Saves roll a natural 1; damage dice roll their highest.
        s.Fx.Dice = n => n == 20 ? 0 : n - 1;
        s.Fx.Apply(s.New("POISON", 0), k.Permanent, c, 0); // nightshade: 1d1 Dex, then 1d2 Con
        Assert.Equal(new[] { 31, 39 }, c.Effects.Select(e => e.Type));
        Assert.Equal(13, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityDex));
        // Already poisoned: a second poison doesn't take.
        s.Fx.Apply(s.New("POISON", 1), k.Permanent, c, 0);
        Assert.Single(c.Effects, e => e.Type == 31);
        s.Step(59);
        Assert.Contains(c.Effects, e => e.Type == 31);
        s.Step(2);
        Assert.DoesNotContain(c.Effects, e => e.Type == 31);
        // 16 with the dwarf's racial bonus, less 2.
        Assert.Equal(14, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityCon));

        // A creature that makes the save isn't poisoned.
        var d = s.Spawn();
        s.Fx.Dice = n => n - 1;
        s.Fx.Apply(s.New("POISON", 0), k.Permanent, d, 0);
        Assert.Empty(d.Effects);
    }

    [DataFact]
    public void DamageImmunityAlwaysTakesAPoint()
    {
        var s = Make();
        var c = s.Spawn();
        c.MaxHitPoints = c.CurrentHitPoints = 500;
        s.Fx.Apply(s.New("DAMAGE_IMMUNITY_INCREASE", 32, 50), s.K.Permanent, c, 0); // cold
        int Hit(int n) { int hp = c.CurrentHitPoints; HitPoints.Damage(s.World, c, GameObject.InvalidId, n, 32); return hp - c.CurrentHitPoints; }
        Assert.Equal(new[] { 0, 1, 2, 2, 3 }, new[] { 1, 2, 3, 4, 5 }.Select(Hit));
    }

    [DataFact]
    public void ConstitutionMovesCurrentHitPointsAndCanKill()
    {
        var s = Make();
        var c = s.Spawn(level: 10);
        c.Abilities[NwRules.AbilityCon] = 12; // dwarf: 14, +2 a level
        CreatureRules.EnsureInit(s.World, c);
        CreatureRules.SyncHitPoints(s.World, c);
        int max = c.MaxHitPoints;
        s.Fx.Apply(s.New("ABILITY_INCREASE", NwRules.AbilityCon, 4), s.K.Permanent, c, 0);
        Assert.Equal(max + 20, c.MaxHitPoints);
        Assert.Equal(max + 20, c.CurrentHitPoints);
        c.CurrentHitPoints = 10;
        s.Fx.Apply(s.New("ABILITY_DECREASE", NwRules.AbilityCon, 10), s.K.Permanent, c, 0);
        Assert.True(c.IsDead);
        Assert.True(c.CurrentHitPoints <= -11);
    }

    [DataFact]
    public void SpeedIncreaseUsesNativeAbsoluteBoundaryAndNegativeFactors()
    {
        var s = Make();
        var c = s.Spawn();
        // Measured movement ratios in speed_absolute, including the discontinuity at 100.
        foreach (var (amount, factor) in new[] { (-150, 0.125f), (-100, 0.125f), (-10, 0.9f),
            (0, 1f), (50, 1.5f), (99, 1.5f), (100, 1f), (110, 1.1f), (150, 1.5f), (200, 1.5f) })
        {
            c.Effects.Clear();
            s.Fx.Apply(s.New("MOVEMENT_SPEED_INCREASE", amount), s.K.Permanent, c, 0);
            Assert.Equal(factor, CreatureRules.MovementSpeedFactor(s.World, c), 3);
        }
    }

    [DataFact]
    public void SpeedEffectsMultiplyWithinTheCaps()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        float F() => CreatureRules.MovementSpeedFactor(s.World, c);
        Assert.Equal(1f, F());
        s.Fx.Apply(s.New("MOVEMENT_SPEED_INCREASE", 200), k.Permanent, c, 0);
        Assert.Equal(1.5f, F(), 3);
        s.Fx.Apply(s.New("MOVEMENT_SPEED_DECREASE", 50), k.Permanent, c, 0);
        Assert.Equal(0.75f, F(), 3);
        s.Fx.Apply(s.New("MOVEMENT_SPEED_DECREASE", 90), k.Permanent, c, 0);
        Assert.Equal(0.125f, F(), 3); // 1.5 x 0.5 x 0.1, but never below an eighth
        c.Effects.RemoveAll(e => e.Ints[0] == 50);
        Assert.Equal(0.15f, F(), 3);  // 1.5 x 0.1 (movement_speed: +50% with -90%)
        s.Fx.Apply(s.New("ENTANGLE"), k.Permanent, c, 0);
        Assert.Equal(0f, F());
    }

    [DataFact]
    public void DispelTakesMagicalEffectsOnTheNextUpdate()
    {
        var s = Make();
        var c = s.Spawn();
        var k = s.K;
        var plain = s.New("SKILL_INCREASE", 17, 1);
        var extra = s.New("SKILL_INCREASE", 17, 1);
        extra.SubType = k.Extraordinary;
        s.Fx.Apply(plain, k.Permanent, c, 0);
        s.Fx.Apply(extra, k.Permanent, c, 0);
        s.Fx.Apply(s.New("DISPELMAGICALL", 60), k.Instant, c, 0);
        Assert.Equal(2, c.Effects.Count);
        s.Step(0.5f);
        Assert.Equal(k.Extraordinary, c.Effects.Single().SubType);
        // Without a caster level (not cast from a spell) nothing goes.
        s.Fx.Apply(s.New("SKILL_INCREASE", 17, 1), k.Permanent, c, 0);
        s.Fx.Apply(s.New("DISPELMAGICALL", 0), k.Instant, c, 0);
        s.Step(0.5f);
        Assert.Equal(2, c.Effects.Count);
    }

    [DataFact]
    public void PolymorphTakesTheFormsStatsAndGivesThemBack()
    {
        var s = Make();
        var c = s.Spawn(dex: 14);
        var k = s.K;
        int appearance = c.AppearanceType, race = c.Race;
        s.Fx.Apply(s.New("POLYMORPH", 21), k.Temporary, c, 10); // brown bear: Str 27, Con 19, Dex 13, natural AC 2, 10 hit points
        Assert.Equal(27, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityStr));
        Assert.Equal(13, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityDex));
        Assert.Equal(13, c.AppearanceType);
        Assert.Equal(8, c.Race);
        Assert.Equal(10, CreatureRules.TemporaryHitPoints(c));
        Assert.Single(c.Effects, e => !e.Hidden);
        Assert.Equal(10 + 1 + 2, CreatureRules.ArmorClass(s.World, c).Total);
        s.Step(11);
        Assert.Empty(c.Effects);
        Assert.Equal(appearance, c.AppearanceType);
        Assert.Equal(race, c.Race);
        Assert.Equal(14, CreatureRules.AbilityScore(s.World, c, NwRules.AbilityDex));
    }
}
