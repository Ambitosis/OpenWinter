using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Skills;

/// <summary>
/// Skills that work with items and locks: Open Lock (with thieves' tools), Lore (identifying items),
/// Use Magic Device, Appraise, Parry, and using healer's kits, thieves' tools and trap kits from the
/// inventory.
/// </summary>
public sealed partial class SkillSystem
{
    // ------------------------------------------------------------------ open lock

    /// <summary>Thieves' tools bonus for the attempt in progress: (creature, bonus, attempted?).</summary>
    (uint Creature, int Bonus, bool Attempted) _tools = (GameObject.InvalidId, 0, false);

    /// <summary>
    /// The Open Lock check behind <see cref="DoorService.OpenLockCheck"/> (unlocking and relocking without a key):
    /// d20 + Open Lock (+ thieves' tools in use) against the lock DC, taking 20 out of combat. Creatures
    /// without the skill (it needs training) can't pick locks.
    /// </summary>
    public bool OpenLockCheck(Creature c, GameObject target, int dc)
    {
        if (!CanUse(c, SkillIds.OpenLock)) return false;
        int tools = 0;
        if (_tools.Creature == c.Id) { tools = _tools.Bonus; _tools.Attempted = true; }
        return Check(c, SkillIds.OpenLock, dc, tools, TakesTwenty(c), target).Success;
    }

    /// <summary>
    /// ActionUseSkill(SKILL_OPEN_LOCK): unlocks a door or placeable (a carried key works first; a lock that needs
    /// its key can't be picked). Thieves' tools add their bonus and are used up by the attempt.
    /// </summary>
    public bool OpenLock(Creature c, GameObject target, Item? tools)
    {
        if (target is not ILockable l || target.IsDestroyed) return false;
        if (!l.IsLocked) return true;
        int bonus = tools != null && !tools.IsDestroyed ? ToolBonus(tools, SkillIds.IpThievesTools) : 0;
        _tools = (c.Id, bonus, false);
        try
        {
            bool ok = DoorService.Unlock(_world, target, c);
            if (_tools.Attempted && tools != null) UseUp(tools);
            return ok;
        }
        finally { _tools = (GameObject.InvalidId, 0, false); }
    }

    /// <summary>The bonus an item property of <paramref name="propertyType"/> grants (its cost table value), or 0.</summary>
    public int ToolBonus(Item item, int propertyType)
    {
        int best = 0;
        foreach (var p in item.Properties)
            if (p.Valid && p.Type == propertyType) best = Math.Max(best, CreatureRules.PropertyValue(_world, p));
        return best;
    }

    // ------------------------------------------------------------------ inventory items that use skills

    /// <summary>
    /// Item activation for properties that are skill uses rather than spells (InventorySystem.UseItemProperty
    /// hands these over): healer's kits (Heal on the target), thieves' tools (Open Lock on the target) and
    /// trap kits (Set Trap on a door/placeable or at the location). Returns whether the property was handled.
    /// </summary>
    public bool UseSkillItem(GameObject user, Item item, ItemProperty prop, uint target, Location loc)
    {
        if (user is not Creature c) return false;
        var obj = _world.Get(target);
        return prop.Type switch
        {
            SkillIds.IpHealersKit => obj is Creature patient && Heal(c, patient, item),
            SkillIds.IpThievesTools => obj != null && OpenLock(c, obj, item),
            SkillIds.IpTrap => SetTrap(c, item, obj, obj?.Location ?? loc),
            _ => false,
        };
    }

    // ------------------------------------------------------------------ lore

    /// <summary>
    /// Lore identification: an item is identified when its value is within the skillvsitemcost.2da DeviceCostMax
    /// of the row for the creature's Lore rank. The original does this when a player acquires an item; call it
    /// again when the player examines an item or gains Lore.
    /// </summary>
    public bool TryIdentify(Creature c, Item item)
    {
        if (item.Identified || item.IsDestroyed) return item.Identified;
        if (ItemValue(item) > LoreLimit(c)) return false;
        item.Identified = true;
        _world.MarkChanged(item);
        if (PlayerOf(c) is { } pc) _world.Output?.Notify("item-identified", item, new { item = item.Id, by = c.Id }, pc);
        return true;
    }

    /// <summary>The most valuable item a creature's Lore identifies (skillvsitemcost.2da DeviceCostMax at its Lore rank).</summary>
    public int LoreLimit(Creature c)
    {
        var t = _world.Resources.TwoDA("skillvsitemcost");
        if (t.RowCount == 0) return 0;
        int row = Math.Clamp(Bonus(c, SkillIds.Lore), 0, t.RowCount - 1);
        while (row > 0 && t.IsEmpty(row, "DeviceCostMax")) row--;
        return t.GetInt(row, "DeviceCostMax");
    }

    /// <summary>The value Lore and Use Magic Device compare: one unit with its properties, plus AddCost.</summary>
    int ItemValue(Item item) => (int)Math.Min(int.MaxValue, _world.Inventory.Rules.UnitValue(item, includeProperties: true) + item.AddCost);

    /// <summary>InventorySystem calls this when a creature acquires an item: players auto-identify by Lore.</summary>
    public void OnAcquired(Creature by, Item item)
    {
        if (by.IsPC && !item.Identified) TryIdentify(by, item);
    }

    // ------------------------------------------------------------------ use magic device

    public enum Emulation { Class, Race, Alignment }

    /// <summary>The Use Magic Device rank needed to emulate a class, race or alignment for an item (skillvsitemcost.2da SkillReq_*), or -1 if none can.</summary>
    public int UseMagicDeviceRequirement(Item item, Emulation kind)
    {
        var t = _world.Resources.TwoDA("skillvsitemcost");
        int value = ItemValue(item);
        string column = kind switch { Emulation.Class => "SkillReq_Class", Emulation.Race => "SkillReq_Race", _ => "SkillReq_Align" };
        for (int row = 0; row < t.RowCount; row++)
            if (!t.IsEmpty(row, "DeviceCostMax") && value <= t.GetInt(row, "DeviceCostMax"))
                return t.IsEmpty(row, column) ? -1 : t.GetInt(row, column);
        return -1;
    }

    /// <summary>
    /// Whether a creature may use or equip an item with use limitations (class, race, alignment item properties),
    /// counting Use Magic Device: for each limitation the creature doesn't meet, its UMD rank must reach the
    /// skillvsitemcost.2da requirement (a static comparison, as in the original, not a roll). For the spells
    /// and items packages; <paramref name="required"/> is the highest rank that was needed (0 when no emulation).
    /// </summary>
    public bool CanUseItem(Creature c, Item item, out int required) => CheckItemLimitations(c, item, out required);

    /// <summary>
    /// NWNX_ON_VALIDATE_USE_ITEM around the use limitation check when an item is activated, not when it is
    /// equipped (nwnx_events_items): a skip with a result decides alone ("0" unusable).
    /// </summary>
    bool ValidateUse(Creature c, Item item, Func<bool> check)
    {
        var ev = Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_VALIDATE_USE_ITEM", c);
        if (ev == null) return check();
        ev.Add("ITEM_OBJECT_ID", item);
        bool ok = ev.Before() ? int.TryParse(ev.Result, out var r) && r != 0 : check();
        ev.Add("BEFORE_RESULT", ok).After();
        return ok;
    }

    /// <summary>
    /// Spell-item activation requires wearing equippable items and satisfying their
    /// actual use limitations. PropColumn/itemprops controls which limitations can
    /// be added; it does not exempt an existing property at activation time.
    /// </summary>
    public bool CanActivateSpellItem(Creature c, Item item)
    {
        if (_world.Inventory.Rules.EquipableSlots(item.BaseItem) != 0 && Inventory.InventorySystem.SlotOf(c, item) < 0) return false;
        return ValidateUse(c, item, () => CheckItemLimitations(c, item, out _));
    }

    bool CheckItemLimitations(Creature c, Item item, out int required)
    {
        required = 0;
        bool needClass = false, needRace = false, needAlign = false;
        bool classOk = false, raceOk = false, alignOk = false;
        foreach (var p in item.Properties)
        {
            if (!p.Valid) continue;
            switch (p.Type)
            {
                case SkillIds.IpUseLimitationClass:
                    needClass = true;
                    classOk |= c.Classes.Any(x => x.Class == p.SubType && x.Level > 0);
                    break;
                case SkillIds.IpUseLimitationRacialType:
                    needRace = true;
                    raceOk |= c.Race == p.SubType;
                    break;
                case SkillIds.IpUseLimitationAlignmentGroup:
                    needAlign = true;
                    alignOk |= InAlignmentGroup(c, p.SubType);
                    break;
                case SkillIds.IpUseLimitationSpecificAlignment:
                    needAlign = true;
                    alignOk |= SpecificAlignment(c) == p.SubType;
                    break;
            }
        }
        foreach (var (need, ok, kind) in new[] { (needClass, classOk, Emulation.Class), (needRace, raceOk, Emulation.Race), (needAlign, alignOk, Emulation.Alignment) })
        {
            if (!need || ok) continue;
            int req = UseMagicDeviceRequirement(item, kind);
            if (req < 0) { required = int.MaxValue; return false; }
            required = Math.Max(required, req);
        }
        return required == 0 || (CanUse(c, SkillIds.UseMagicDevice) && Bonus(c, SkillIds.UseMagicDevice) >= required);
    }

    /// <summary>IP_CONST_ALIGNMENTGROUP_*: 1 neutral, 2 lawful, 3 chaotic, 4 good, 5 evil (0 = all).</summary>
    static bool InAlignmentGroup(Creature c, int group) => group switch
    {
        0 => true,
        1 => (c.LawfulChaotic > 30 && c.LawfulChaotic < 70) || (c.GoodEvil > 30 && c.GoodEvil < 70),
        2 => c.LawfulChaotic >= 70,
        3 => c.LawfulChaotic <= 30,
        4 => c.GoodEvil >= 70,
        5 => c.GoodEvil <= 30,
        _ => false,
    };

    /// <summary>IP_CONST_ALIGNMENT_*: LG 0, LN 1, LE 2, NG 3, TN 4, NE 5, CG 6, CN 7, CE 8.</summary>
    static int SpecificAlignment(Creature c)
    {
        int law = c.LawfulChaotic >= 70 ? 0 : c.LawfulChaotic <= 30 ? 2 : 1;
        int good = c.GoodEvil >= 70 ? 0 : c.GoodEvil <= 30 ? 2 : 1;
        return law * 3 + good;
    }

    // ------------------------------------------------------------------ appraise

    /// <summary>
    /// Appraise mark-up/mark-down adjustment for a player at a store. The original engine leaves Appraise to
    /// scripts (nw_i0_plot's gplotAppraiseOpenStore passes bonus mark-ups to OpenStore), so the default is no
    /// change; a server that wants engine-side appraising sets <see cref="AppraiseModifier"/>.
    /// </summary>
    public (int MarkUp, int MarkDown) Appraise(Creature pc, Store store) => AppraiseModifier?.Invoke(pc, store) ?? (0, 0);

    public Func<Creature, Store, (int MarkUp, int MarkDown)>? AppraiseModifier;

    // ------------------------------------------------------------------ parry

    readonly Dictionary<uint, (double RoundStart, int Count)> _parries = new();

    /// <summary>
    /// For the combat package: a creature in parry mode tries to parry an incoming attack. d20 + Parry
    /// (+IMPROVED_PARRY_MODIFIER with Improved Parry) against the attack roll; at most as many parries per round
    /// as the parrier has attacks. A parry that beats the roll by PARRY_RIPOSTE_DIFFERENCE or more earns a
    /// counterattack (the combat package makes it).
    /// </summary>
    public (bool Parried, bool Riposte) TryParry(Creature defender, Creature attacker, int attackRoll)
    {
        if (!defender.ActionModes.Contains(SkillIds.ModeParry) || defender.IsDead) return (false, false);
        double now = _world.Time;
        if (!_parries.TryGetValue(defender.Id, out var p) || now - p.RoundStart >= RoundSeconds) p = (now, 0);
        if (p.Count >= Math.Max(1, CreatureRules.AttacksPerRound(_world, defender))) return (false, false);
        _parries[defender.Id] = (p.RoundStart, p.Count + 1);
        int bonus = HasFeat(defender, SkillIds.FeatImprovedParry) ? Rule("IMPROVED_PARRY_MODIFIER", 4) : 0;
        var r = Check(defender, SkillIds.Parry, attackRoll, bonus, versus: attacker);
        return (r.Success, r.Success && r.Margin >= Rule("PARRY_RIPOSTE_DIFFERENCE", 10));
    }
}
