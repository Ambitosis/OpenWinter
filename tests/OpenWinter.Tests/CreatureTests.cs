using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Creature rules: abilities, BAB and saves from the class tables, armour class, feats, skills, XP, alignment, saves.</summary>
public class CreatureTests
{
    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    /// <summary>Base game rules data (no module), plus the compiled test scripts.</summary>
    static World NewWorld(bool scripts = false)
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        if (scripts) world.EnableScripts();
        world.Register(world.Module);
        return world;
    }

    static Creature LoadBic(World w, string name)
    {
        var path = Path.Combine(TestData.Config!.UserRoot, "localvault", name + ".bic");
        var c = w.Creatures.FromTemplate(Gff.Read(File.ReadAllBytes(path)));
        w.Register(c);
        return c;
    }

    /// <summary>A bare creature with the given classes (class, level) and ability scores.</summary>
    static Creature Synthetic(World w, params (int Class, int Level)[] classes)
    {
        var c = w.Creatures.FromTemplate(new GffStruct());
        foreach (var (cls, lvl) in classes) c.Classes.Add(new CreatureClass { Class = cls, Level = lvl });
        c.Race = 6; // human: no racial adjustments
        w.Register(c);
        return c;
    }

    static Effect Fx(int type, params int[] ints)
    {
        var e = new Effect { Type = type, DurationType = 1 };
        for (int i = 0; i < ints.Length; i++) e.Ints[i] = ints[i];
        return e;
    }

    [DataFact]
    public void LoadedDomainFieldsDefaultIndependentlyAndIgnoreNonDomainClasses()
    {
        var w = NewWorld();
        foreach (var (cls, first, second, expectedFirst, expectedSecond) in new[]
        {
            (2, -1, -1, 0, 1), (2, 13, -1, 13, 1), (2, -1, 18, 0, 18),
            (2, 255, -1, 255, 1), (2, -1, 255, 0, 255), (2, 13, 13, 13, 13),
            (10, -1, -1, -1, -1), (10, 13, 18, -1, -1), (4, 13, 18, -1, -1),
        })
        {
            var entry = new GffStruct(2).SetInt("Class", cls).SetShort("ClassLevel", 20);
            if (first >= 0) entry.SetByte("Domain1", first);
            if (second >= 0) entry.SetByte("Domain2", second);
            var creature = w.Creatures.FromTemplate(new GffStruct().SetList("ClassList", [entry]));
            Assert.Equal(expectedFirst, creature.Classes[0].Domain1);
            Assert.Equal(expectedSecond, creature.Classes[0].Domain2);
        }
    }

    [DataFact]
    public void MulticlassCharacterMatchesTheSavedCharacterSheet()
    {
        // carmenvoltaire1: elf rogue 1 / fighter 1. The character file carries the engine's own
        // BAB, saves and AC; the rules must reproduce them from the class tables.
        var w = NewWorld();
        var c = LoadBic(w, "carmenvoltaire1");
        var t = c.Template;
        Assert.Equal(2, c.Classes.Count);
        Assert.Equal((int)t.GetInt("BaseAttackBonus"), CreatureRules.BaseAttackBonus(w, c));
        Assert.Equal((int)t.GetInt("FortSaveThrow"), CreatureRules.SavingThrow(w, c, NwRules.SaveFort));
        Assert.Equal((int)t.GetInt("RefSaveThrow"), CreatureRules.SavingThrow(w, c, NwRules.SaveReflex));
        Assert.Equal((int)t.GetInt("WillSaveThrow"), CreatureRules.SavingThrow(w, c, NwRules.SaveWill));
        Assert.Equal((int)t.GetInt("ArmorClass"), CreatureRules.ArmorClass(w, c).Total);
        // Stored scores are before racial adjustments (elf: +2 Dex, -2 Con).
        Assert.Equal((int)t.GetInt("Dex") + 2, CreatureRules.AbilityScore(w, c, NwRules.AbilityDex, baseOnly: true));
        Assert.Equal((int)t.GetInt("Con") - 2, CreatureRules.AbilityScore(w, c, NwRules.AbilityCon));
        // Hit points: max from the file, current restored from PregameCurrent.
        Assert.Equal((int)t.GetInt("MaxHitPoints"), CreatureRules.MaxHitPoints(w, c));
        Assert.Equal((int)t.GetInt("PregameCurrent"), c.CurrentHitPoints);
    }

    [DataFact]
    public void AbilityEffectsAndItemsRespectBonusLimits()
    {
        var w = NewWorld();
        var c = Synthetic(w, (NwRules.ClassFighter, 1));
        c.Abilities[NwRules.AbilityStr] = 14;
        Assert.Equal(2, CreatureRules.AbilityModifier(w, c, NwRules.AbilityStr));

        c.Effects.Add(Fx(NwRules.EffectAbilityIncrease, NwRules.AbilityStr, 4));
        Assert.Equal(18, CreatureRules.AbilityScore(w, c, NwRules.AbilityStr));
        Assert.Equal(14, CreatureRules.AbilityScore(w, c, NwRules.AbilityStr, baseOnly: true));
        Assert.Equal(4, CreatureRules.AbilityModifier(w, c, NwRules.AbilityStr));

        // An equipped item's ability bonus stacks with the effect, up to the bonus limit (default 12).
        var ring = new Item { BaseItem = 52 };
        ring.Properties.Add(new ItemProperty { Type = NwRules.IpAbilityBonus, SubType = NwRules.AbilityStr, CostTable = 1, CostTableValue = 10 });
        c.Equipment[EquipSlot.LeftRing] = ring;
        Assert.Equal(26, CreatureRules.AbilityScore(w, c, NwRules.AbilityStr));
        w.Limits.AbilityBonus = 5;
        Assert.Equal(19, CreatureRules.AbilityScore(w, c, NwRules.AbilityStr));

        // Penalties: odd scores round the modifier down, and scores never drop below 3.
        c.Equipment.Clear();
        c.Effects.Clear();
        c.Effects.Add(Fx(NwRules.EffectAbilityDecrease, NwRules.AbilityStr, 5));
        Assert.Equal(9, CreatureRules.AbilityScore(w, c, NwRules.AbilityStr));
        Assert.Equal(-1, CreatureRules.AbilityModifier(w, c, NwRules.AbilityStr));
        c.Effects.Add(Fx(NwRules.EffectAbilityDecrease, NwRules.AbilityStr, 20));
        Assert.Equal(3, CreatureRules.AbilityScore(w, c, NwRules.AbilityStr));

        // Linked effects count even when only the parent is in the list.
        c.Effects.Clear();
        var parent = Fx(NwRules.EffectAbilityIncrease, NwRules.AbilityDex, 2);
        parent.Linked.Add(Fx(NwRules.EffectAbilityIncrease, NwRules.AbilityStr, 2));
        c.Effects.Add(parent);
        Assert.Equal(16, CreatureRules.AbilityScore(w, c, NwRules.AbilityStr));
    }

    [DataFact]
    public void BabAndSavesForMulticlassAndEpicLevels()
    {
        var w = NewWorld();
        // Fighter 4 / wizard 3: fighter BAB +4, wizard +1; fort 4 + 1, ref 1 + 1, will 1 + 3.
        var c = Synthetic(w, (NwRules.ClassFighter, 4), (NwRules.ClassWizard, 3));
        Assert.Equal(5, CreatureRules.BaseAttackBonus(w, c));
        Assert.Equal(5, CreatureRules.BaseSave(w, c, NwRules.SaveFort));
        Assert.Equal(2, CreatureRules.BaseSave(w, c, NwRules.SaveReflex));
        Assert.Equal(4, CreatureRules.BaseSave(w, c, NwRules.SaveWill));
        Assert.Equal(1, CreatureRules.AttacksPerRound(w, c));

        // Ability and feat bonuses: Con 14 (+2) and Great Fortitude (+2).
        c.Abilities[NwRules.AbilityCon] = 14;
        c.Feats.Add(NwRules.FeatGreatFortitude);
        Assert.Equal(9, CreatureRules.SavingThrow(w, c, NwRules.SaveFort));
        // A fortitude bonus effect, and one that only applies against poison.
        c.Effects.Add(Fx(NwRules.EffectSavingThrowIncrease, NwRules.SaveFort, 2, NwRules.SaveTypeAll));
        c.Effects.Add(Fx(NwRules.EffectSavingThrowIncrease, NwRules.SaveAll, 3, NwRules.SaveTypePoison));
        Assert.Equal(11, CreatureRules.SavingThrow(w, c, NwRules.SaveFort));
        Assert.Equal(14, CreatureRules.SavingThrow(w, c, NwRules.SaveFort, NwRules.SaveTypePoison));
        Assert.Equal(5, CreatureRules.SavingThrow(w, c, NwRules.SaveWill, NwRules.SaveTypePoison) - 2);

        // Fighter 15 / wizard 10: levels 16-20 are wizard 1-5, then 5 epic levels.
        var epic = Synthetic(w, (NwRules.ClassFighter, 15), (NwRules.ClassWizard, 10));
        Assert.Equal(15 + 2 + 3, CreatureRules.BaseAttackBonus(w, epic));
        Assert.Equal(9 + 1 + 2, CreatureRules.BaseSave(w, epic, NwRules.SaveFort));
        Assert.Equal(4, CreatureRules.AttacksPerRound(w, epic));
        epic.BaseAttackOverride = 2;
        Assert.Equal(2, CreatureRules.AttacksPerRound(w, epic));
        Assert.Equal(4, CreatureRules.AttacksPerRound(w, epic, checkOverride: false));
    }

    [DataFact]
    public void ArmourClassStacksByType()
    {
        var w = NewWorld();
        var c = Synthetic(w, (NwRules.ClassFighter, 1));
        c.Abilities[NwRules.AbilityDex] = 14;
        Assert.Equal(12, CreatureRules.ArmorClass(w, c).Total);
        // Two deflection bonuses don't stack; dodge bonuses do; penalties apply to their own type.
        c.Effects.Add(Fx(NwRules.EffectAcIncrease, 2, NwRules.AcDeflection, NwRules.AcVsDamageTypeAll));
        c.Effects.Add(Fx(NwRules.EffectAcIncrease, 3, NwRules.AcDeflection, NwRules.AcVsDamageTypeAll));
        c.Effects.Add(Fx(NwRules.EffectAcIncrease, 1, NwRules.AcDodge, NwRules.AcVsDamageTypeAll));
        c.Effects.Add(Fx(NwRules.EffectAcIncrease, 1, NwRules.AcDodge, NwRules.AcVsDamageTypeAll));
        c.Effects.Add(Fx(NwRules.EffectAcDecrease, 1, NwRules.AcDodge, NwRules.AcVsDamageTypeAll));
        var ac = CreatureRules.ArmorClass(w, c);
        Assert.Equal(3, ac.Deflection);
        Assert.Equal(2 - 1, ac.Dodge);
        Assert.Equal(0, ac.Penalty);
        Assert.Equal(12 + 3 + 2 - 1, ac.Total);
        // Monks add Wisdom unarmoured.
        var monk = Synthetic(w, (NwRules.ClassMonk, 5));
        monk.Abilities[NwRules.AbilityWis] = 16;
        Assert.Equal(10 + 3 + 1, CreatureRules.ArmorClass(w, monk).Total);
    }

    [DataFact]
    public void FeatsFromListItemsAndEffectsWithUses()
    {
        var w = NewWorld();
        var c = LoadBic(w, "carmenvoltaire1");
        // The creature skin grants a bonus feat through an item property.
        var skin = c.Equipment[EquipSlot.CreatureArmour];
        var bonus = skin.Properties.First(p => p.Type == NwRules.IpBonusFeat);
        int feat = w.Resources.TwoDA("iprp_feats").GetInt(bonus.SubType, "FeatIndex");
        c.Feats.Remove(feat); // character files also list item-granted feats
        Assert.True(CreatureRules.HasFeat(w, c, feat));
        c.Equipment.Remove(EquipSlot.CreatureArmour);
        Assert.False(CreatureRules.HasFeat(w, c, feat));
        Assert.False(CreatureRules.HasFeat(w, c, NwRules.FeatTurnUndead));
        c.Effects.Add(Fx(NwRules.EffectBonusFeat, NwRules.FeatTurnUndead));
        Assert.True(CreatureRules.HasFeat(w, c, NwRules.FeatTurnUndead));

        // Turn undead: 3 + Cha modifier uses, + 6 with Extra Turning; uses run out.
        var cleric = Synthetic(w, (NwRules.ClassCleric, 3));
        cleric.Abilities[NwRules.AbilityCha] = 14;
        cleric.Feats.Add(NwRules.FeatTurnUndead);
        Assert.Equal(5, CreatureRules.RemainingFeatUses(w, cleric, NwRules.FeatTurnUndead));
        cleric.Feats.Add(NwRules.FeatExtraTurning);
        Assert.Equal(11, CreatureRules.RemainingFeatUses(w, cleric, NwRules.FeatTurnUndead));
        for (int i = 0; i < 11; i++) CreatureRules.AdjustFeatUses(w, cleric, NwRules.FeatTurnUndead, -1);
        Assert.False(CreatureRules.HasFeat(w, cleric, NwRules.FeatTurnUndead));
        Assert.True(CreatureRules.HasFeat(w, cleric, NwRules.FeatTurnUndead, ignoreUses: true));
        CreatureRules.AdjustFeatUses(w, cleric, NwRules.FeatTurnUndead, +1);
        Assert.Equal(1, CreatureRules.RemainingFeatUses(w, cleric, NwRules.FeatTurnUndead));
        CreatureRules.Rest(w, cleric);
        Assert.Equal(11, CreatureRules.RemainingFeatUses(w, cleric, NwRules.FeatTurnUndead));
        // Feats without a use limit report int.MaxValue.
        cleric.Feats.Add(NwRules.FeatToughness);
        Assert.Equal(int.MaxValue, CreatureRules.RemainingFeatUses(w, cleric, NwRules.FeatToughness));
    }

    [DataFact]
    public void SongSuccessorsShareUsesAcrossMissingRanks()
    {
        var w = NewWorld();
        var bard = Synthetic(w, (NwRules.ClassBard, 20));
        bard.Feats.Add(367); // Native stock Bard14 UTC stores this rank only.
        Assert.True(CreatureRules.HasFeat(w, bard, NwRules.FeatBardSongs));
        Assert.False(CreatureRules.HasFeat(w, bard, 368, ignoreUses: true));
        Assert.Equal(14, CreatureRules.RemainingFeatUses(w, bard, 355));
        CreatureRules.AdjustFeatUses(w, bard, NwRules.FeatBardSongs, -1);
        Assert.Equal(13, CreatureRules.RemainingFeatUses(w, bard, 367));
        Assert.Equal(13, bard.FeatUses[367]); // UTC serialization writes the owned rank.
        Assert.False(bard.FeatUses.ContainsKey(NwRules.FeatBardSongs));
        CreatureRules.AdjustFeatUses(w, bard, 355, +1); // Inherited aliases cannot restore uses.
        Assert.Equal(13, CreatureRules.RemainingFeatUses(w, bard, 367));
        CreatureRules.AdjustFeatUses(w, bard, 367, +1);
        Assert.Equal(14, CreatureRules.RemainingFeatUses(w, bard, 257));
        CreatureRules.AdjustFeatUses(w, bard, 360, -100);
        Assert.False(CreatureRules.HasFeat(w, bard, 257));
        Assert.True(CreatureRules.HasFeat(w, bard, 257, ignoreUses: true));
        CreatureRules.RestoreFeatUses(w, bard);
        Assert.Equal(14, CreatureRules.RemainingFeatUses(w, bard, 257));
        bard.Feats.Add(257);
        CreatureRules.AdjustFeatUses(w, bard, 367, -1);
        CreatureRules.AdjustFeatUses(w, bard, 257, +1); // Exact base rank now exists.
        Assert.Equal(14, CreatureRules.RemainingFeatUses(w, bard, 367));
        bard.Feats.Add(NwRules.FeatExtraMusic);
        Assert.Equal(18, CreatureRules.MaxFeatUses(w, bard, 367));
    }

    [DataFact]
    public void BonusSongSuccessorIsUnlimitedAndDoesNotEraseOwnedPool()
    {
        var w = NewWorld();
        var bard = Synthetic(w, (NwRules.ClassBard, 14));
        bard.Feats.Add(367);
        CreatureRules.AdjustFeatUses(w, bard, 257, -1);
        var bonus = Fx(NwRules.EffectBonusFeat, 373);
        bard.Effects.Add(bonus);
        Assert.Equal(int.MaxValue, CreatureRules.RemainingFeatUses(w, bard, 257));
        CreatureRules.AdjustFeatUses(w, bard, 257, -1);
        bard.Effects.Remove(bonus);
        Assert.Equal(13, CreatureRules.RemainingFeatUses(w, bard, 257));
    }

    [DataFact]
    public void SkillsAddAbilityFeatsAndEffects()
    {
        var w = NewWorld();
        var c = Synthetic(w, (NwRules.ClassRogue, 4));
        const int hide = 5, spot = 17;
        c.Skills[hide] = 7;
        c.Abilities[NwRules.AbilityDex] = 16;
        Assert.Equal(10, CreatureRules.SkillRank(w, c, hide));
        Assert.Equal(7, CreatureRules.SkillRank(w, c, hide, baseOnly: true));
        c.Feats.Add(178); // Skill Focus (Hide)
        Assert.Equal(13, CreatureRules.SkillRank(w, c, hide));
        c.Effects.Add(Fx(NwRules.EffectSkillIncrease, NwRules.SkillAll, 2));
        Assert.Equal(15, CreatureRules.SkillRank(w, c, hide));
        c.Feats.Add(NwRules.FeatAlertness);
        Assert.Equal(2 + 2, CreatureRules.SkillRank(w, c, spot));
        // Trained-only skill without ranks can't be used.
        Assert.Equal(-1, CreatureRules.SkillRank(w, c, 19)); // use magic device
        Assert.False(CreatureRules.HasSkill(w, c, 19));
    }

    [DataFact]
    public void AlignmentShifts()
    {
        var w = NewWorld();
        var c = Synthetic(w, (NwRules.ClassFighter, 1));
        c.LawfulChaotic = 10;
        c.GoodEvil = 80;
        CreatureRules.AdjustAlignment(w, c, NwRules.AlignNeutral, 15, false);
        Assert.Equal(25, c.LawfulChaotic);
        Assert.Equal(65, c.GoodEvil);
        c.LawfulChaotic = 40;
        c.GoodEvil = 70;
        CreatureRules.AdjustAlignment(w, c, NwRules.AlignNeutral, 15, false);
        Assert.Equal(50, c.LawfulChaotic);
        Assert.Equal(55, c.GoodEvil);
        CreatureRules.AdjustAlignment(w, c, NwRules.AlignEvil, 40, false);
        Assert.Equal(15, c.GoodEvil);
        Assert.Equal(NwRules.AlignEvil, CreatureRules.GoodEvil(c.GoodEvil));
        CreatureRules.AdjustAlignment(w, c, NwRules.AlignAll, -30, false);
        Assert.Equal(20, c.LawfulChaotic);
        Assert.Equal(0, c.GoodEvil);
        Assert.Equal(NwRules.AlignChaotic, CreatureRules.LawChaos(c.LawfulChaotic));
    }

    sealed class XpLog : IWorldOutput
    {
        public readonly List<string> Messages = new();
        public readonly List<string> Notes = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Messages.Add(text);
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null)
        {
            if (kind == "xp-changed") Notes.Add(System.Text.Json.JsonSerializer.Serialize(data));
        }
    }

    [DataFact]
    public void ExperienceFeedbackMatchesTheOriginal()
    {
        // Measured with the levelup scene: GiveXPToCreature and SetXP both report the change, and the chat line,
        // the "*Level Up!*" text and gui_level_up come only when a level first becomes available.
        var w = NewWorld();
        var log = new XpLog();
        w.Output = log;
        var c = Synthetic(w, (10, 1));
        c.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
        CreatureRules.GiveXP(w, c, 500);
        CreatureRules.GiveXP(w, c, 600);
        CreatureRules.SetXP(w, c, 3500);
        CreatureRules.SetXP(w, c, 0);
        CreatureRules.SetXP(w, c, 1000);
        Assert.Equal(new[]
        {
            "Experience Points Gained:  500",
            "Experience Points Gained:  600", "Your character has gained enough experience to advance a level.",
            "Experience Points Gained:  2400",
            "Experience Points Lost:  3500",
            "Experience Points Gained:  1000", "Your character has gained enough experience to advance a level.",
        }, log.Messages);
        Assert.Equal(new[] { false, true, false, false, true }, log.Notes.Select(n => n.Contains("\"levelUpReady\":true")).ToArray());
        Assert.Contains("\"feedback\":\"Level Up!\"", log.Notes[1]);
    }

    [DataFact]
    public void ExperienceLevelsUpOnlyByChoiceAndLosesLevelsFromHistory()
    {
        var w = NewWorld();
        var c = LoadBic(w, "freddiepharkasf1"); // barbarian 8, 30862 XP
        Assert.Equal(8, c.TotalLevel);
        Assert.False(CreatureRules.CanLevelUp(w, c));
        int next = CreatureRules.XPForLevel(w, 9);
        CreatureRules.GiveXP(w, c, next - c.Experience);
        Assert.Equal(next, c.Experience);
        Assert.Equal(8, c.TotalLevel); // no automatic level-up
        Assert.True(CreatureRules.CanLevelUp(w, c));
        CreatureRules.GiveXP(w, c, -500); // ignored
        Assert.Equal(next, c.Experience);

        // Dropping XP below the level's requirement removes levels using the level history.
        int maxBefore = CreatureRules.MaxHitPoints(w, c);
        int feats = c.Feats.Count;
        CreatureRules.SetXP(w, c, CreatureRules.XPForLevel(w, 3));
        Assert.Equal(3, c.TotalLevel);
        Assert.Equal(3, c.Template.GetList("LvlStatList").Count);
        Assert.True(CreatureRules.MaxHitPoints(w, c) < maxBefore);
        Assert.True(c.Feats.Count < feats);
        Assert.Equal(3, CreatureRules.BaseAttackBonus(w, c));
        CreatureRules.SetXP(w, c, -10);
        Assert.Equal(0, c.Experience);
        Assert.Equal(1, c.TotalLevel);
    }

    [DataFact]
    public void SavingThrowRollsHandleImmunityAndNaturalRolls()
    {
        var w = NewWorld();
        var c = Synthetic(w, (NwRules.ClassFighter, 10));
        Assert.Equal(CreatureRules.SaveFailed, CreatureRules.RollSave(w, c, NwRules.SaveFort, 2, random: _ => 0));      // natural 1
        Assert.Equal(CreatureRules.SaveSucceeded, CreatureRules.RollSave(w, c, NwRules.SaveFort, 200, random: _ => 19)); // natural 20
        Assert.Equal(CreatureRules.SaveSucceeded, CreatureRules.RollSave(w, c, NwRules.SaveFort, 17, random: _ => 9));  // 10 + 7 vs 17
        Assert.Equal(CreatureRules.SaveFailed, CreatureRules.RollSave(w, c, NwRules.SaveFort, 18, random: _ => 9));
        // Poison immunity from a feat (Diamond Body) or an effect makes poison saves automatic.
        c.Feats.Add(NwRules.FeatDiamondBody);
        Assert.Equal(CreatureRules.SaveImmune, CreatureRules.RollSave(w, c, NwRules.SaveFort, 50, NwRules.SaveTypePoison, random: _ => 0));
        Assert.Equal(CreatureRules.SaveFailed, CreatureRules.RollSave(w, c, NwRules.SaveFort, 50, NwRules.SaveTypeDisease, random: _ => 0));
        c.Effects.Add(Fx(NwRules.EffectImmunity, NwRules.ImmunityDisease));
        Assert.Equal(CreatureRules.SaveImmune, CreatureRules.RollSave(w, c, NwRules.SaveFort, 50, NwRules.SaveTypeDisease));
        Assert.True(CreatureRules.IsImmune(w, c, NwRules.ImmunityDisease));
        Assert.False(CreatureRules.IsImmune(w, c, NwRules.ImmunityFear));
    }

    [DataFact]
    public void HitPointsFollowConstitution()
    {
        var w = NewWorld();
        var c = LoadBic(w, "carmenvoltaire1");
        int max = CreatureRules.MaxHitPoints(w, c);
        c.Effects.Add(Fx(NwRules.EffectAbilityIncrease, NwRules.AbilityCon, 4)); // +2 modifier x 2 levels
        CreatureRules.SyncHitPoints(w, c);
        Assert.Equal(max + 4, c.MaxHitPoints);
        Assert.Equal(max + 4, c.CurrentHitPoints);
        c.Effects.Clear();
        CreatureRules.SyncHitPoints(w, c);
        Assert.Equal(max, c.MaxHitPoints);
        Assert.Equal(max, c.CurrentHitPoints);
    }

    [DataFact]
    public void SpellSlotsMemorisedAndSpontaneous()
    {
        var w = NewWorld();
        var wiz = Synthetic(w, (NwRules.ClassWizard, 3));
        wiz.Abilities[NwRules.AbilityInt] = 16;
        // Level 3 wizard: 2 first-level slots + 1 bonus for Int 16.
        Assert.Equal(3, CreatureRules.MemorizedSlotCount(w, wiz, NwRules.ClassWizard, 1));
        Assert.True(CreatureRules.EnsureSlot(w, wiz, NwRules.ClassWizard, 1, 2));
        wiz.Classes[0].Memorized[1][2] = (107, 0, true);
        Assert.Equal(1, CreatureRules.SpellCount(w, wiz, 107));
        CreatureRules.SpendSpell(w, wiz, 107);
        Assert.Equal(0, CreatureRules.SpellCount(w, wiz, 107));
        CreatureRules.ReadySpellLevel(w, wiz, 1, NwRules.ClassInvalid, 255);
        Assert.Equal(1, CreatureRules.SpellCount(w, wiz, 107));
        Assert.False(CreatureRules.EnsureSlot(w, wiz, NwRules.ClassWizard, 1, 3));

        var sorc = Synthetic(w, (NwRules.ClassSorcerer, 1));
        sorc.Abilities[NwRules.AbilityCha] = 12;
        int magicMissile = 107;
        int level = CreatureRules.SpellLevelForClass(w, NwRules.ClassSorcerer, magicMissile);
        Assert.Equal(1, level);
        sorc.Classes[0].KnownSpells[1].Add(magicMissile);
        int slots = CreatureRules.SpellSlots(w, sorc, NwRules.ClassSorcerer, 1);
        Assert.Equal(slots, CreatureRules.SpellCount(w, sorc, magicMissile));
        CreatureRules.SpendSpell(w, sorc, magicMissile);
        Assert.Equal(slots - 1, CreatureRules.SpellUsesLeft(w, sorc, NwRules.ClassSorcerer, magicMissile));
    }

    [DataFact]
    public void ScriptFunctionsReportCreatureRules()
    {
        var w = NewWorld(scripts: true);
        var c = LoadBic(w, "carmenvoltaire1");
        c.Effects.Add(Fx(NwRules.EffectImmunity, NwRules.ImmunityPoison));
        var logs = new List<string>();
        w.Scripts!.Log = logs.Add;
        w.Scripts.Run("creature_rules", c.Id);
        var v = c.Locals.Ints;
        Assert.True(v.ContainsKey("done"), string.Join("\n", logs));

        Assert.Equal(CreatureRules.AbilityScore(w, c, NwRules.AbilityStr), v["str"]);
        Assert.Equal(20, v["dexbase"]); // 18 + elf
        Assert.Equal(5, v["dexmod"]);
        Assert.Equal(1, v["bab"]);
        Assert.Equal((int)c.Template.GetInt("FortSaveThrow"), v["fort"]);
        Assert.Equal((int)c.Template.GetInt("RefSaveThrow"), v["ref"]);
        Assert.Equal((int)c.Template.GetInt("WillSaveThrow"), v["will"]);
        Assert.Equal((int)c.Template.GetInt("ArmorClass"), v["ac"]);
        Assert.Equal(2, v["hd"]);
        Assert.Equal(NwRules.ClassRogue, v["cls1"]);
        Assert.Equal(NwRules.ClassFighter, v["cls2"]);
        Assert.Equal(NwRules.ClassInvalid, v["cls3"]);
        Assert.Equal(1, v["lvl2"]);
        Assert.Equal(1, v["fighter"]);
        Assert.Equal(1, v["race"]);
        Assert.Equal(20, v["maxhp"]);
        Assert.Equal(20, v["hp"]);

        // GoodEvil 15 -> 45 (neutral), then toward 50; LawfulChaotic 85 -> 70 (still lawful).
        Assert.Equal(45, v["ge1"]);
        Assert.Equal(NwRules.AlignNeutral, v["align1"]);
        Assert.Equal(50, v["ge2"]);
        Assert.Equal(70, v["lc2"]);
        Assert.Equal(NwRules.AlignLawful, v["lcalign"]);

        Assert.Equal(1, v["hasfeat"]);
        Assert.Equal(0, v["nofeat"]);
        Assert.Equal(1, v["tvalid"]);
        Assert.Equal(NwRules.TalentFeat, v["ttype"]);
        Assert.Equal(1, v["thas"]);

        Assert.Equal(CreatureRules.SaveImmune, v["immune"]);
        Assert.Equal(1, v["isimmune"]);
        Assert.Equal(3, v["apr"]);
        Assert.Equal(1, v["aprbase"]);
        Assert.Equal(1, v["apr2"]);
        Assert.Equal(4, v["abilitylimit"]);
        Assert.Equal(50, v["xpscale2"]);
        Assert.Equal(250, v["xpgain"]);
        Assert.Equal(250, v["xpneg"]);
        Assert.Equal(42, v["hair"]);
        Assert.Equal(7, v["head"]);
        Assert.Equal("Tymora", c.Locals.Strings["deity"]);
        Assert.Equal(0, v["pa"]); // expertise replaced power attack
        Assert.Equal(1, v["exp"]);
        Assert.Equal(CreatureRules.SkillRankBase(c, 5), v["skillbase"]);
        Assert.Equal(v["skillbase"] + 5, v["skill"]);
        Assert.DoesNotContain(logs, l => l.Contains("not implemented"));
    }
    [Theory]
    [InlineData(3, 6, 6)]
    [InlineData(10, 6, 10)]
    [InlineData(6, 10, 10)]
    public void CompanionUsesHigherClassButFamiliarAddsClasses(int first, int second, int companionLevel)
    {
        var companionMaster = new Creature();
        companionMaster.Classes.Add(new CreatureClass { Class = NwRules.ClassDruid, Level = first });
        companionMaster.Classes.Add(new CreatureClass { Class = NwRules.ClassRanger, Level = second });
        Assert.Equal(companionLevel, CreatureRules.AssociateLevel(companionMaster, NwRules.AssociateCompanion));
        var familiarMaster = new Creature();
        familiarMaster.Classes.Add(new CreatureClass { Class = NwRules.ClassWizard, Level = first });
        familiarMaster.Classes.Add(new CreatureClass { Class = NwRules.ClassSorcerer, Level = second });
        Assert.Equal(first + second, CreatureRules.AssociateLevel(familiarMaster, NwRules.AssociateFamiliar));
    }

}
