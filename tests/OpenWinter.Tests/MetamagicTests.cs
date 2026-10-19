using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

public class MetamagicTests
{
    [DataFact]
    public void LegacyCountsKeepDuplicateDomainsAndConversionSeparate()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        var caster = new Creature();
        CreatureRules.EnsureInit(world, caster);
        var cc = new CreatureClass { Class = NwRules.ClassCleric, Level = 20, Domain1 = 1, Domain2 = 1 };
        caster.Classes.Add(cc);
        cc.Memorized[3].Add((186, 0, true)); // Animal True Seeing at level 3.
        Assert.Equal(2, CreatureRules.SpellCount(world, caster, 186));
        cc.Domain2 = 10; // Knowledge adds a distinct level-4 entry.
        cc.Memorized[4].Add((186, 0, true));
        Assert.Equal(4, CreatureRules.SpellCount(world, caster, 186));
        cc.Memorized[3][0] = (6, NwRules.MetaExtend, true); // Bless converts to Cure Serious.
        Assert.Equal(1, CreatureRules.SpellCount(world, caster, 35));
        caster.DomainSlots.Add((0, 3, 0));
        Assert.Equal(0, CreatureRules.SpellCount(world, caster, 35));
        cc.Memorized[3][0] = (35, 0, true); // Even an explicitly prepared domain cure is excluded.
        Assert.Equal(0, CreatureRules.SpellCount(world, caster, 35));
    }

    [DataFact]
    public void DomainUseReportsCountRequestedLevelWithoutAuthorizingStoredPreparation()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        var caster = new Creature();
        CreatureRules.EnsureInit(world, caster);
        var cc = new CreatureClass { Class = NwRules.ClassCleric, Level = 20, Domain1 = 13, Domain2 = 0 };
        caster.Classes.Add(cc);
        cc.Memorized[1].Add((102, NwRules.MetaMaximize, true));
        cc.Memorized[2].Add((102, NwRules.MetaNone, true));
        cc.Memorized[4].Add((102, NwRules.MetaNone, true));
        Assert.Equal(0, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102));
        Assert.Equal(1, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, 0, 1));
        Assert.Equal(1, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, 255, 1));
        Assert.Equal(0, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, NwRules.MetaExtend, 1));
        caster.Feats.Add(12);
        Assert.Equal(1, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, NwRules.MetaExtend, 1));
        Assert.Null(SpellSlots.Find(world, caster, 102, NwRules.MetaExtend, cc.Class, 1, false));
        caster.DomainSlots.Add((0, 2, 0));
        Assert.Equal(1, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, NwRules.MetaExtend, 1));
        caster.Feats.Add(25);
        Assert.Equal(1, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, NwRules.MetaMaximize, 1));
        Assert.Null(SpellSlots.Find(world, caster, 102, NwRules.MetaMaximize, cc.Class, 1, false));
        cc.Memorized[2][0] = (102, NwRules.MetaNone, false);
        Assert.Equal(0, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, NwRules.MetaExtend, 1));
        cc.Domain1 = 1;
        Assert.Equal(0, CreatureRules.SpellUsesLeft(world, caster, cc.Class, 102, 0, 1));
    }

    [DataFact]
    public void ForcedPreparedSlotsRespectSpellMetamagicWhenCasting()
    {
        // Installed stock tables only: a module or personal override may intentionally change masks.
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        var area = new Area { ResRef = "metamagic", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        foreach (var (spell, cls, meta, allowed) in new[]
        {
            (431, NwRules.ClassCleric, NwRules.MetaNone, true),
            (431, NwRules.ClassCleric, NwRules.MetaMaximize, false),
            (107, NwRules.ClassWizard, NwRules.MetaNone, true),
            (107, NwRules.ClassWizard, NwRules.MetaMaximize, true),
            (107, NwRules.ClassWizard, NwRules.MetaExtend, false),
            (102, NwRules.ClassWizard, NwRules.MetaNone, true),
            (102, NwRules.ClassWizard, NwRules.MetaMaximize, false),
            (102, NwRules.ClassWizard, NwRules.MetaExtend, true),
        })
        {
            var caster = new Creature { MaxHitPoints = 100, CurrentHitPoints = 100 };
            world.AddToArea(caster, area, new Vector3(10, 10, 0), 0);
            CreatureRules.EnsureInit(world, caster);
            var cc = new CreatureClass { Class = cls, Level = 20 };
            caster.Classes.Add(cc);
            int level = CreatureRules.SpellLevelForClass(world, cls, spell) + CreatureRules.MetaMagicCost(world, meta);
            cc.Memorized[level].Add((spell, meta, true));
            var action = new CastSpellAction { Spell = spell, Class = cls, MetaMagic = meta, Target = caster.Id };
            Assert.Equal(allowed ? ActionStatus.InProgress : ActionStatus.Failed, action.Step(world, caster, .01f));
            Assert.Equal(allowed, action.Info != null);
            Assert.True(cc.Memorized[level][0].Ready);
            action.Cancel(world, caster);
        }
    }

    [DataFact]
    public void InvalidPreparedLevelsCannotCastOrHideAValidSlot()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        var area = new Area { ResRef = "slotlevels", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        foreach (var (spell, cls, meta, invalidLevel, validLevel) in new[]
        {
            (155, NwRules.ClassWizard, 0, 2, 1),
            (107, NwRules.ClassWizard, 0, 0, 1),
            (107, NwRules.ClassWizard, 0, 2, 1),
            (102, NwRules.ClassWizard, NwRules.MetaExtend, 1, 2),
            (102, NwRules.ClassWizard, NwRules.MetaExtend, 3, 2),
            (107, NwRules.ClassWizard, NwRules.MetaMaximize, 3, 4),
            (107, NwRules.ClassWizard, NwRules.MetaMaximize, 5, 4),
            (102, NwRules.ClassCleric, 0, 1, -1),
        })
        {
            var caster = new Creature { MaxHitPoints = 100, CurrentHitPoints = 100 };
            world.AddToArea(caster, area, new Vector3(10, 10, 0), 0);
            CreatureRules.EnsureInit(world, caster);
            var cc = new CreatureClass { Class = cls, Level = 20 };
            caster.Classes.Add(cc);
            cc.Memorized[invalidLevel].Add((spell, meta, true));
            var invalid = new CastSpellAction { Spell = spell, Class = cls, MetaMagic = meta, Target = caster.Id };
            Assert.Equal(ActionStatus.Failed, invalid.Step(world, caster, .01f));
            Assert.Null(invalid.Info);
            Assert.True(cc.Memorized[invalidLevel][0].Ready);
            if (validLevel < 0) continue;
            cc.Memorized[validLevel].Add((spell, meta, true));
            var slot = SpellSlots.Find(world, caster, spell, meta, cls, 0, false);
            Assert.NotNull(slot);
            Assert.Equal(validLevel, slot.SlotLevel);
            SpellSlots.Spend(world, caster, slot);
            Assert.False(cc.Memorized[validLevel][0].Ready);
            Assert.True(cc.Memorized[invalidLevel][0].Ready);
        }
    }
    [DataFact]
    public void DomainCastsUseSelectedDomainBaseLevelRegardlessOfStoredMarker()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        var area = new Area { ResRef = "domains", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        foreach (var (spell, domainId, meta, slotLevel, requestLevel, marker, allowed) in new[]
        {
            (102, 13, 0, 1, 1, true, true),
            (102, 13, 0, 1, 1, false, true),
            (102, 13, 0, 1, 0, true, false),
            (102, 1, 0, 1, 1, true, false),
            (102, 13, NwRules.MetaExtend, 2, 1, true, true),
            (102, 13, NwRules.MetaExtend, 2, 2, true, false),
            (6, 13, 0, 1, 1, true, false),
            (6, 13, 0, 1, 0, true, true),
            (62, 18, 0, 3, 3, true, true),
            (62, 18, 0, 4, 3, true, false),
            (62, 18, NwRules.MetaExtend, 4, 3, true, true),
            (62, 18, 0, 4, 0, true, true),
        })
        {
            var caster = new Creature { MaxHitPoints = 100, CurrentHitPoints = 100 };
            world.AddToArea(caster, area, new Vector3(10, 10, 0), 0);
            CreatureRules.EnsureInit(world, caster);
            caster.Abilities[NwRules.AbilityWis] = 20;
            var cc = new CreatureClass { Class = NwRules.ClassCleric, Level = 20, Domain1 = 0, Domain2 = domainId };
            caster.Classes.Add(cc);
            cc.Memorized[slotLevel].Add((spell, meta, true));
            if (marker) caster.DomainSlots.Add((0, slotLevel, 0));
            var action = new CastSpellAction { Spell = spell, Class = cc.Class, MetaMagic = meta,
                DomainLevel = requestLevel, Target = caster.Id };
            Assert.Equal(allowed ? ActionStatus.InProgress : ActionStatus.Failed, action.Step(world, caster, .01f));
            Assert.True(cc.Memorized[slotLevel][0].Ready);
            if (!allowed) { Assert.Null(action.Info); continue; }
            var info = Assert.IsType<SpellCastInfo>(action.Info);
            int baseLevel = requestLevel > 0 ? requestLevel : CreatureRules.SpellLevelForClass(world, cc.Class, spell);
            Assert.Equal(baseLevel, info.SpellLevel);
            Assert.Equal(requestLevel, info.DomainLevel);
            caster.LastSpell = info;
            Assert.Equal(10 + baseLevel + 5, CreatureRules.SpellSaveDC(world, caster));
            action.Cancel(world, caster);
        }
    }

}
