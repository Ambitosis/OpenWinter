using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>Item activation: charges, uses per day and the cast-spell property.</summary>
public sealed partial class InventorySystem
{
    /// <summary>iprp_chargecost rows for limited uses per day (1 to 5 uses).</summary>
    public static int MaxUsesPerDay(ItemProperty p) => MaxUsesPerDay(p.CostTable, p.CostTableValue);

    public static int MaxUsesPerDay(int costTable, int costValue) => costTable == 3 && costValue is >= 8 and <= 12 ? costValue - 7 : -1;

    /// <summary>Charges a cast-spell property spends per use (iprp_chargecost rows 2..6 = 5..1 charges), 0 otherwise.</summary>
    static int ChargesPerUse(ItemProperty p) => p.CostTable == 3 && p.CostTableValue is >= 2 and <= 6 ? 7 - p.CostTableValue : 0;

    /// <summary>
    /// SetItemCharges as the original does it: at most 250; 0 or less leaves the charges alone and destroys
    /// the item once the running script ends (whether or not it had charges).
    /// </summary>
    public void SetCharges(Item item, int charges)
    {
        if (charges <= 0) { _world.After(0, () => { if (!item.IsDestroyed) _world.Destroy(item); }); return; }
        item.Charges = Math.Min(charges, 250);
        Changed(item);
    }

    /// <summary>Whether a cast-spell property has a charge or daily use left for one activation.</summary>
    public bool CanUseItemProperty(Item item, ItemProperty prop)
    {
        int perUse = ChargesPerUse(prop), maxPerDay = MaxUsesPerDay(prop);
        if (perUse > 0 && item.Charges < perUse) return false;
        return maxPerDay <= 0 || Math.Min(prop.UsesPerDay, maxPerDay) > 0;
    }

    /// <summary>
    /// Uses one of an item's properties: spends charges or a daily use (when <paramref name="decrement"/>),
    /// then casts the property's spell. Activate Item (Unique Power) fires the module's OnActivateItem.
    /// </summary>
    public bool UseItemProperty(GameObject user, Item item, ItemProperty prop, uint target, Location loc, bool decrement)
    {
        // Healer's kits, thieves' tools and trap kits are skill uses.
        if (prop.Type != ItemRules.PropCastSpell) return _world.System<Skills.SkillSystem>().UseSkillItem(user, item, prop, target, loc);
        int perUse = ChargesPerUse(prop);
        bool singleUse = prop.CostTable == 3 && prop.CostTableValue == 1;
        int maxPerDay = MaxUsesPerDay(prop);
        if (maxPerDay > 0 && prop.UsesPerDay > maxPerDay) { prop.UsesPerDay = maxPerDay; Changed(item); } // 255 = never used yet
        if (decrement)
        {
            if (perUse > 0 && item.Charges < perUse) return false;
            if (maxPerDay > 0 && prop.UsesPerDay <= 0) return false;
        }

        // Native potion_clock and item_use_clock: surviving stacks and charges
        // are spent before the impact script; the final item remains visible.
        // consumable_types/equip also cover crafted/custom rows, an equipped dagger and a ring.
        bool spentStack = decrement && singleUse && item.StackSize > 1;
        if (spentStack) { item.StackSize--; Changed(item); }
        bool spentCharges = decrement && perUse > 0;
        if (spentCharges)
        {
            item.Charges = Math.Max(0, item.Charges - perUse); // a value already read stays (ItemRules.UnitValue)
            Changed(item);
        }

        var spells = _world.Resources.TwoDA("spells");
        int spell = _world.Resources.TwoDA("iprp_spells").GetInt(prop.SubType, "SpellIndex", -1);
        string impact = spell >= 0 ? spells.Get(spell, "ImpactScript") : "";
        // "X uses <item>" for a potion, scroll or wand (castlines3, castlines4), to the players near.
        if (user is Creature itemUser && !impact.Equals("NW_S3_ActItem01", StringComparison.OrdinalIgnoreCase))
            Spells.CastingLines.Uses(_world, itemUser, item);
        var targetObj = _world.Get(target);
        var targetLoc = targetObj?.Location ?? loc;
        if (impact.Equals("NW_S3_ActItem01", StringComparison.OrdinalIgnoreCase))
        {
            // Unique Power: the spell's script just signals the module; do that directly.
            _world.RunEvent(_world.Module, "Mod_OnActvtItem", new EventContext()
                .Set("ActivatedItem", item.Id).Set("Activator", user.Id)
                .Set("ActivatedTarget", targetObj?.Id ?? GameObject.InvalidId).Set("ActivatedLocation", targetLoc));
        }
        else if (impact.Length > 0)
        {
            // The spell system runs the impact script with the item's caster level and spell context.
            _world.System<Spells.SpellSystem>().CastFromItem(user, item, prop.SubType, targetObj, targetLoc);
        }

        if (!decrement || item.IsDestroyed) return true;
        if (singleUse)
        {
            if (!spentStack && item.StackSize > 1) item.StackSize--;
            else if (!spentStack)
            {
                // Native potion_expire/item_use_clock: the final consumable stays
                // script-visible for half a second after impact.
                // A drunk potion or read scroll goes without "Lost Item"; a single-use wand says it (castlines4). The
                // line follows whether the kind of item stacks (inferred from those three).
                item.UsedUp = Rules.MaxStack(item.BaseItem) > 1;
                _world.After(0.5f, () =>
                {
                    if (item.IsDestroyed) return;
                    _world.Destroy(item);
                });
            }
        }
        else if (spentCharges)
        {
            // A wand's last charge does say "Lost Item" (castlines4).
            if (item.Charges == 0 && !Nwnx.Plugins.NwnxTweaks.Of(_world).PreserveDepletedItems)
                _world.After(0.5f, () => { if (!item.IsDestroyed) _world.Destroy(item); });
        }
        else if (maxPerDay > 0) prop.UsesPerDay--;
        if (!item.IsDestroyed && (singleUse || maxPerDay > 0)) Changed(item);
        return true;
    }
}
