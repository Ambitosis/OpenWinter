using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_SkillRanks over the rules' <see cref="SkillFeatTable"/>: the skill feats GetSkillRank and every skill check
/// use, the Skill Focus modifiers, area skill modifiers and the blindness penalty (parity suite nwnx_skillranks).
/// </summary>
static class SkillRanksPlugin
{
    const string P = "SkillRanks";

    static SkillFeatTable Table(NwnxCall n) => SkillFeatTable.Of(n.World);

    /// <summary>Pushes a skill feat's fields after the feat id, so the script pops the modifier first.</summary>
    static void PushFields(NwnxCall n, SkillFeat f)
    {
        n.Push(f.KeyAbilityMask);
        n.Push(f.BypassArmorCheckPenalty);
        n.Push(f.DayOrNight);
        n.Push(f.AreaFlagsForbidden);
        n.Push(f.AreaFlagsRequired);
        n.Push(f.ClassLevelMod);
        n.Push(f.Classes);
        n.Push(f.FocusFeat);
        n.Push(f.Modifier);
    }

    [NwnxFunc(P, "GetSkillFeatCountForSkill")] static void Count(NwnxCall n) => n.Push(Table(n).ForSkill(n.PopInt()).Count);

    [NwnxFunc(P, "GetSkillFeatForSkillByIndex")]
    static void ByIndex(NwnxCall n)
    {
        int skill = n.PopInt(); int index = n.PopInt();
        var list = Table(n).ForSkill(skill);
        var f = index >= 0 && index < list.Count ? list[index] : new SkillFeat { Skill = skill, Feat = -1 };
        PushFields(n, f);
        n.Push(f.Feat);
    }

    [NwnxFunc(P, "GetSkillFeat")]
    static void Get(NwnxCall n)
    {
        int skill = n.PopInt(); int feat = n.PopInt();
        PushFields(n, Table(n).Find(skill, feat) ?? new SkillFeat { Skill = skill, Feat = feat });
    }

    [NwnxFunc(P, "SetSkillFeat")]
    static void Set(NwnxCall n)
    {
        var f = new SkillFeat
        {
            Skill = n.PopInt(), Feat = n.PopInt(), Modifier = n.PopInt(), FocusFeat = n.PopInt(),
            Classes = n.PopString(), ClassLevelMod = n.PopFloat(), AreaFlagsRequired = n.PopInt(), AreaFlagsForbidden = n.PopInt(),
            DayOrNight = n.PopInt(), BypassArmorCheckPenalty = n.PopInt() != 0, KeyAbilityMask = n.PopInt(),
        };
        bool create = n.PopInt() != 0;
        // The script sends the class bits from the first set one; a string without any is "".
        if (f.Classes.IndexOf('1') < 0) f.Classes = "";
        if (f.Skill < 0 || f.Feat < 0) return;
        Table(n).Set(f, create);
    }

    [NwnxFunc(P, "SetSkillFeatFocusModifier")]
    static void FocusModifier(NwnxCall n)
    {
        int modifier = n.PopInt(); bool epic = n.PopInt() != 0;
        Table(n).SetFocusModifier(modifier, epic);
    }

    [NwnxFunc(P, "GetBlindnessPenalty")] static void GetBlind(NwnxCall n) => n.Push(Table(n).BlindnessPenalty);
    [NwnxFunc(P, "SetBlindnessPenalty")] static void SetBlind(NwnxCall n) => Table(n).BlindnessPenalty = n.PopInt();

    [NwnxFunc(P, "GetAreaModifier")]
    static void GetArea(NwnxCall n)
    {
        uint area = n.PopObjectId(); int skill = n.PopInt();
        n.Push(Table(n).AreaModifier(area, skill));
    }

    [NwnxFunc(P, "SetAreaModifier")]
    static void SetArea(NwnxCall n)
    {
        var area = n.PopObject(); int skill = n.PopInt(); int modifier = n.PopInt();
        if (area is Areas.Area) Table(n).SetAreaModifier(area.Id, skill, modifier);
    }
}
