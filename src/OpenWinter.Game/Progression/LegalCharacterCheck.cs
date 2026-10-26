using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>
/// Enforce Legal Characters: replays an uploaded character's level history (LvlStatList) through
/// the creation and level-up rules and checks that the result is the character in the file.
/// Prerequisite variables (VAR rows) are not checked, since their values at the time are unknown.
/// </summary>
public static class LegalCharacterCheck
{
    public static List<RuleError> Check(World w, GffStruct bic)
    {
        var errors = new List<RuleError>();
        // Load without the inventory so nothing is registered in the world.
        var template = bic.DeepClone();
        template.Remove("Equip_ItemList");
        template.Remove("ItemList");
        var c = w.Creatures.FromTemplate(template);
        CreatureRules.EnsureInit(w, c);
        var history = template.GetList("LvlStatList");
        var races = w.Resources.TwoDA("racialtypes");
        if (c.Race >= races.RowCount || races.GetInt(c.Race, "PlayerRace") != 1) errors.Add(new("race", "The race is not playable."));
        if (c.TotalLevel == 0) { errors.Add(new("class", "The character has no class levels.")); return errors; }
        if (history.Count != c.TotalLevel) { errors.Add(new("history", $"The level history has {history.Count} entries for {c.TotalLevel} levels.")); return errors; }

        // Starting scores: stored scores minus the increases and class stat gains of later levels.
        var start = (int[])c.Abilities.Clone();
        var classLevels = new Dictionary<int, int>();
        foreach (var e in history)
        {
            int cls = (int)e.GetInt("LvlStatClass");
            int lvl = classLevels[cls] = classLevels.GetValueOrDefault(cls) + 1;
            if (e.Has("LvlStatAbility") && e.GetInt("LvlStatAbility") is >= 0 and <= 5) start[e.GetInt("LvlStatAbility")]--;
            var probe = new Creature();
            ProgressionRules.ApplyStatGain(w, probe, cls, lvl);
            for (int a = 0; a < 6; a++) start[a] -= probe.Abilities[a] - 10;
        }
        var first = new NewCharacter { Race = c.Race, Abilities = start, Level = new LevelChoice { Class = (int)history[0].GetInt("LvlStatClass") } };
        errors.AddRange(CharacterFactory.AbilityErrors(w, first));

        var s = new Creature { Race = c.Race, LawfulChaotic = c.LawfulChaotic, GoodEvil = c.GoodEvil, RulesInitialised = true, Template = new GffStruct() };
        Array.Copy(start, s.Abilities, 6);
        s.Skills = new int[Math.Max(c.Skills.Length, w.Resources.TwoDA("skills").RowCount)];
        int carried = 0;
        for (int i = 0; i < history.Count; i++)
        {
            var e = history[i];
            int cls = (int)e.GetInt("LvlStatClass");
            var final = c.Classes.FirstOrDefault(x => x.Class == cls);
            var ch = new LevelChoice
            {
                Class = cls,
                Ability = e.Has("LvlStatAbility") ? (int)e.GetInt("LvlStatAbility") : -1,
                Domain1 = final?.Domain1 ?? -1, Domain2 = final?.Domain2 ?? -1, School = final?.School ?? -1,
            };
            var ranks = e.GetList("SkillList");
            for (int sk = 0; sk < ranks.Count; sk++) if (ranks[sk].GetInt("Rank") is > 0 and var r) ch.Skills[sk] = (int)r;
            var probe = ProgressionRules.Plan(w, s, ch, player: true, carried, checkVariables: false);
            var gainedFeats = e.GetList("FeatList").Select(f => (int)f.GetInt("Feat")).ToList();
            ch.Feats = gainedFeats.Where(f => !probe.GrantedFeats.Contains(f)).ToList();
            foreach (var g in probe.GrantedFeats.Where(f => !gainedFeats.Contains(f)))
                errors.Add(new("history", $"Level {i + 1}: the granted feat {ProgressionRules.FeatName(w, g)} is missing.", i + 1, g));
            var granted = probe.Spells.SelectMany(p => p.Granted.SelectMany(k => k.Value.Select(sp => (k.Key, sp)))).ToHashSet();
            for (int lvl = 0; lvl < 10; lvl++)
            {
                var learnt = e.GetList($"KnownList{lvl}").Select(k => (int)k.GetInt("Spell")).Where(sp => !granted.Contains((lvl, sp))).ToList();
                if (learnt.Count > 0) ch.Spells[lvl] = learnt;
            }
            var plan = ProgressionRules.Plan(w, s, ch, player: true, carried, checkVariables: false);
            // Alignment may have shifted in play since the level was taken.
            foreach (var err in plan.Errors.Where(x => x.Code != "alignment")) errors.Add(err with { Message = $"Level {i + 1} ({ProgressionRules.ClassName(w, cls)}): {err.Message}", Level = i + 1 });
            if (e.GetInt("LvlStatHitDie") > ProgressionRules.HitDie(w, cls)) errors.Add(new("hitpoints", $"Level {i + 1}: {e.GetInt("LvlStatHitDie")} hit points from a d{ProgressionRules.HitDie(w, cls)}.", i + 1));
            carried = Math.Max(0, plan.SkillPointsLeft);
            s = plan.After;
            s.RulesInitialised = true;
            s.Template = new GffStruct();
        }

        for (int a = 0; a < 6; a++)
            if (s.Abilities[a] != c.Abilities[a]) errors.Add(new("abilities", $"Ability {a} is {c.Abilities[a]}; the level history gives {s.Abilities[a]}."));
        foreach (var cc in c.Classes)
            if (CreatureRules.ClassLevel(s, cc.Class) != cc.Level) errors.Add(new("class", $"{ProgressionRules.ClassName(w, cc.Class)} level {cc.Level} doesn't match the level history."));
        for (int sk = 0; sk < c.Skills.Length; sk++)
            if (c.Skills[sk] != (sk < s.Skills.Length ? s.Skills[sk] : 0))
                errors.Add(new("skills", $"{ProgressionRules.SkillName(w, sk)} has {c.Skills[sk]} ranks; the level history gives {(sk < s.Skills.Length ? s.Skills[sk] : 0)}.", Skill: sk));
        foreach (var f in c.Feats.Distinct())
            if (!s.Feats.Contains(f)) errors.Add(new("feats", $"The feat {ProgressionRules.FeatName(w, f)} wasn't gained by levelling.", Feat: f));
        foreach (var cc in c.Classes)
        {
            var replayed = s.Classes.FirstOrDefault(x => x.Class == cc.Class);
            // Wizards also learn spells from scrolls; their spellbooks may hold more than levelling gave.
            bool spellbook = ProgressionRules.HasSpellbook(w, cc.Class);
            for (int lvl = 0; lvl < 10; lvl++)
                foreach (var sp in cc.KnownSpells[lvl])
                    if (replayed?.KnownSpells[lvl].Contains(sp) != true && !(spellbook && CreatureRules.SpellLevelForClass(w, cc.Class, sp) == lvl))
                        errors.Add(new("spells", $"{ProgressionRules.SpellName(w, sp)} wasn't learnt by levelling.", Spell: sp));
        }
        long hp = template.GetInt("HitPoints"), maxDice = history.Sum(e => ProgressionRules.HitDie(w, (int)e.GetInt("LvlStatClass")));
        if (hp > maxDice) errors.Add(new("hitpoints", $"{hp} base hit points; the class hit dice allow {maxDice}."));
        return errors;
    }
}
