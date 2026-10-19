using OpenWinter.Game.Objects;
using OpenWinter.Game.Skills;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>ActionUseSkill (the rest of the skill functions live with the creature rules).</summary>
static class SkillFunctions
{
    // void ActionUseSkill(int nSkill, object oTarget, int nSubSkill=0, object oItemUsed=OBJECT_INVALID)
    [NwFunc("ActionUseSkill")] static object? UseSkill(Call c)
    {
        if (c.Self is not Creature self || !self.Commandable) return null;
        int skill = c.I(0);
        // Set Trap goes through a trap kit (ActionUseItem*): ActionUseSkill(SKILL_SET_TRAP) does nothing (trap_set SKILL*).
        if (skill == SkillIds.SetTrap) return null;
        // Skills that act on the user or a spot (stealth/detect toggles, setting a trap where one stands) accept OBJECT_INVALID.
        var target = c.Obj(1) ?? (skill is SkillIds.SetTrap or SkillIds.Hide or SkillIds.MoveSilently or SkillIds.Search
            or SkillIds.Spot or SkillIds.Listen ? self : null);
        if (target == null || skill is SkillIds.Spot or SkillIds.Listen) return null;
        if (ApplyMode(c, skill)) return null;
        if (skill == SkillIds.Heal && c.Obj(3) is not Item) return null;
        // Lore on a carried item: identify it on the spot (there is nowhere to walk to).
        if (skill == SkillIds.Lore && target is Item item && c.World.Inventory.Owner(item) == self)
        {
            c.World.System<SkillSystem>().TryIdentify(self, item);
            return null;
        }
        self.Actions.Add(new UseSkillAction
        {
            Skill = skill, SubSkill = c.I(2), ItemId = c.O(3), Target = target.Id, Range = UseSkillAction.RangeFor(skill),
        });
        return null;
    }

    // Native script mode skills apply immediately without replacing or appending to the queue.
    internal static bool ApplyMode(Call c, int skill)
    {
        int mode = skill switch
        {
            SkillIds.Hide or SkillIds.MoveSilently => SkillIds.ModeStealth,
            SkillIds.Search => SkillIds.ModeDetect,
            _ => -1,
        };
        if (mode < 0) return false;
        if (c.Self is Creature { Commandable: true } actor)
            c.World.System<SkillSystem>().SetActionMode(actor, mode, mode == SkillIds.ModeStealth ? !actor.StealthMode : !actor.DetectMode);
        return true;
    }
}
