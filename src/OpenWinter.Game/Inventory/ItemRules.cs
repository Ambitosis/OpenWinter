using System.Globalization;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>Item rules read from baseitems.2da and the item property tables: stacking, slots, weight, AC and value.</summary>
public sealed class ItemRules
{
    public const int BaseItemInvalid = 256, BaseItemArmor = 16, BaseItemGold = 76;
    public const int PropAcBonus = 1, PropEnhancement = 6, PropCastSpell = 15, PropWeightReduction = 11, PropAttackBonus = 56,
        PropContainerWeight = 32, PropWeightIncrease = 81;
    public const int DurationPermanent = 2;

    /// <summary>baseitems.2da's spell scroll row; the original keys scroll behaviour (preparation, icon) by this id.</summary>
    public const int SpellScrollBaseItem = 75;

    /// <summary>Preparation style of an item cast; eligibility and resource use are independent.</summary>
    public enum SpellItemStyle { Ordinary, Potion, Scroll, CraftedScroll }

    /// <summary>
    /// Native preparation is keyed by base-item ID: custom clones retain ordinary casting.
    /// Crafted scrolls (105) do not share scroll 75's two-second preparation; crafted wands
    /// (106), rods, staves and custom rows use the ordinary item phase. Use restrictions
    /// and the need to equip are separate, table-driven rules.
    /// </summary>
    public static SpellItemStyle SpellStyle(int baseItem) => baseItem switch
    {
        49 or 104 => SpellItemStyle.Potion,
        SpellScrollBaseItem => SpellItemStyle.Scroll,
        105 => SpellItemStyle.CraftedScroll,
        _ => SpellItemStyle.Ordinary,
    };

    public static bool IsPotion(int baseItem) => SpellStyle(baseItem) == SpellItemStyle.Potion;

    readonly World _world;
    public ItemRules(World world) { _world = world; }

    TwoDA BaseItems => _world.Resources.TwoDA("baseitems");

    bool Valid(int baseItem) => baseItem >= 0 && baseItem < BaseItems.RowCount && BaseItems.Get(baseItem, "label").Length > 0;

    /// <summary>Largest stack for a base item (baseitems Stacking), at least 1.</summary>
    public int MaxStack(int baseItem) => Valid(baseItem) ? Math.Max(1, BaseItems.GetInt(baseItem, "Stacking", 1)) : 1;

    /// <summary>EquipableSlots bit mask (slot bits as in <see cref="EquipSlot"/>).</summary>
    public int EquipableSlots(int baseItem)
    {
        if (!Valid(baseItem)) return 0;
        var s = BaseItems.Get(baseItem, "EquipableSlots");
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    public bool IsContainer(int baseItem) => Valid(baseItem) && BaseItems.GetInt(baseItem, "Container") == 1;
    public bool IsRangedWeapon(int baseItem) => Valid(baseItem) && !BaseItems.IsEmpty(baseItem, "RangedWeapon");
    public int WeaponType(int baseItem) => Valid(baseItem) ? BaseItems.GetInt(baseItem, "WeaponType") : 0;
    public int WeaponWield(int baseItem) => Valid(baseItem) ? BaseItems.GetInt(baseItem, "WeaponWield") : 0;
    public int WeaponSize(int baseItem) => Valid(baseItem) ? BaseItems.GetInt(baseItem, "WeaponSize") : 0;

    /// <summary>A hand-held weapon (melee or ranged), not a shield, torch or creature weapon.</summary>
    public bool IsWeapon(int baseItem) =>
        Valid(baseItem) && WeaponType(baseItem) > 0 && (EquipableSlots(baseItem) & EquipSlot.RightHand) != 0 && WeaponWield(baseItem) != 9;

    /// <summary>
    /// A creature can wield this item (anything that isn't a weapon): a weapon at most one size larger than the creature
    /// (baseitems WeaponSize against CREATURE_SIZE_*) and at most two smaller. Measured on nwserver (parity suite
    /// combat_equip): a large ogre or hill giant can't take a dagger, a huge giant nothing below size 3.
    /// </summary>
    public bool FitsWielder(int baseItem, int creatureSize) =>
        !IsWeapon(baseItem) || (WeaponSize(baseItem) <= creatureSize + 1 && WeaponSize(baseItem) >= creatureSize - 2);

    /// <summary>Needs both hands for a creature of this size (bows, crossbows, polearms, double weapons, or too big).</summary>
    public bool IsTwoHanded(int baseItem, int creatureSize)
    {
        if (!IsWeapon(baseItem)) return false;
        int wield = WeaponWield(baseItem);
        if (wield is 4 or 5 or 6 or 8) return true;
        return WeaponSize(baseItem) > Math.Max(1, creatureSize);
    }

    /// <summary>Average damage of one hit with the base weapon, plus enhancement and damage bonus properties (for ActionEquipMostDamaging*).</summary>
    public float AverageDamage(Item item)
    {
        if (!Valid(item.BaseItem)) return 0;
        int dice = BaseItems.GetInt(item.BaseItem, "NumDice"), die = BaseItems.GetInt(item.BaseItem, "DieToRoll");
        float avg = dice * (die + 1) / 2f;
        avg += Enhancement(item);
        foreach (var p in item.Properties)
            if (p.Type == 16) avg += DamageCost(p.CostTableValue); // ITEM_PROPERTY_DAMAGE_BONUS
        return avg;
    }

    static float DamageCost(int costValue) => costValue switch
    {
        // IPRP_DAMAGECOST rows: 1..5 flat, then dice; approximate the average.
        <= 5 => costValue,
        6 => 3.5f, 7 => 7, 8 => 10.5f, 9 => 14, 10 => 6, 11 => 4.5f, 12 => 4, 13 => 9, 14 => 5.5f,
        _ => costValue - 10,
    };

    /// <summary>Highest enhancement bonus on a weapon (unconditional only).</summary>
    public int Enhancement(Item item)
    {
        int best = 0;
        foreach (var p in item.Properties)
            if (p.Type == PropEnhancement) best = Math.Max(best, p.CostTableValue);
        return best;
    }

    /// <summary>Whether itemprops.2da lets a property type go on a base item (the base item's PropColumn).</summary>
    public bool AllowsProperty(int baseItem, int type)
    {
        var t = _world.Resources.TwoDA("itemprops");
        if (type < 0 || type >= t.RowCount) return false;
        // Rows without a PropColumn (deleted and padding rows) take anything.
        if (baseItem < 0 || baseItem >= BaseItems.RowCount || !int.TryParse(BaseItems.Get(baseItem, "PropColumn"), out int col)) return true;
        return col >= 0 && col < t.Columns.Length && t.GetInt(type, t.Columns[col], 0) == 1;
    }

    /// <summary>Inventory grid footprint in cells (InvSlotWidth x InvSlotHeight, at least 1 x 1).</summary>
    public (int W, int H) Footprint(int baseItem) => Valid(baseItem)
        ? (Math.Max(1, BaseItems.GetInt(baseItem, "InvSlotWidth", 1)), Math.Max(1, BaseItems.GetInt(baseItem, "InvSlotHeight", 1)))
        : (1, 1);

    /// <summary>Inventory grid cells the item takes (InvSlotWidth x InvSlotHeight).</summary>
    public int Cells(int baseItem) => Valid(baseItem)
        ? Math.Max(1, BaseItems.GetInt(baseItem, "InvSlotWidth", 1)) * Math.Max(1, BaseItems.GetInt(baseItem, "InvSlotHeight", 1))
        : 1;

    /// <summary>Store panel for a base item, as a UTM StoreList category id (0 armour, 1 misc, 2 potions, 3 rings, 4 weapons).</summary>
    public int StorePanel(int baseItem)
    {
        if (!Valid(baseItem)) return 1;
        // baseitems StorePanel numbers weapons 1 and miscellaneous 4; the store lists swap those two.
        return BaseItems.GetInt(baseItem, "StorePanel", 4) switch { 0 => 0, 1 => 4, 2 => 2, 3 => 3, _ => 1 };
    }

    /// <summary>Placement cue: armor uses its physical base AC, unaffected by magical AC bonuses.</summary>
    public string InventorySound(Item item)
    {
        // Native armor keeps these resources even when inventorysnds row4 is redirected.
        if (item.BaseItem == BaseItemArmor)
            return BaseAC(item) switch
            {
                <= 1 => "it_materialcloth", <= 3 => "it_armorleather", <= 5 => "it_armorchain", _ => "it_armorplate",
            };
        int row = BaseItems.GetInt(item.BaseItem, "InvSoundType", -1);
        return _world.Resources.TwoDA("inventorysnds").Get(row, "InventorySound");
    }

    // ----- armour class -----

    /// <summary>Base AC of armour (parts_chest ACBONUS of the torso part) or a shield (baseitems BaseAC).</summary>
    public int BaseAC(Item item)
    {
        if (item.BaseItem == BaseItemArmor)
        {
            int torso = (int)CreatureFactory.Wide(item.Template, "ArmorPart_Torso");
            return (int)_world.Resources.TwoDA("parts_chest").GetFloat(torso, "ACBONUS");
        }
        return Valid(item.BaseItem) ? BaseItems.GetInt(item.BaseItem, "BaseAC") : 0;
    }

    /// <summary>
    /// GetItemACValue: base AC plus every unconditional AC bonus property, added up (temporary ones too);
    /// decreased AC and AC versus something don't count.
    /// </summary>
    public int ACValue(Item item)
    {
        int ac = BaseAC(item);
        foreach (var p in item.Properties)
            if (p.Type == PropAcBonus) ac += p.CostTableValue;
        return ac;
    }

    // ----- weight -----

    /// <summary>
    /// GetWeight of an item in tenths of a pound: the base weight (armour by AC) times any base weight reduction,
    /// plus any weight increase, times the stack, plus container contents (less the container's weight reduction).
    /// </summary>
    public int WeightTenths(Item item) => WeightTenths(item, 0);

    int WeightTenths(Item item, int depth)
    {
        float unit = item.BaseItem == BaseItemArmor
            ? _world.Resources.TwoDA("armor").GetInt(BaseAC(item), "WEIGHT", 0)
            : Valid(item.BaseItem) ? BaseItems.GetInt(item.BaseItem, "TenthLBS") : 0;
        float containerReduction = 0, reduced = 1;
        foreach (var p in item.Properties)
        {
            // Several reductions don't combine: the strongest applies.
            if (p.Type == PropWeightReduction) reduced = Math.Min(reduced, CostTableFloat(p, "Value", 1));
            else if (p.Type == PropWeightIncrease) unit += _world.Resources.TwoDA("iprp_weightinc").GetInt(p.Param1Value, "Value", 0);
            else if (p.Type == PropContainerWeight) containerReduction = Math.Max(containerReduction, CostTableFloat(p, "Value", 0));
        }
        unit *= reduced;
        long total = (long)unit * Math.Max(1, item.StackSize);
        if (item.Inventory.Count > 0 && depth < 4)
        {
            long contents = item.Inventory.Sum(i => (long)WeightTenths(i, depth + 1));
            total += contents - (long)(contents * Math.Clamp(containerReduction, 0f, 1f));
        }
        return (int)Math.Min(int.MaxValue, total);
    }

    /// <summary>A column of the property's itempropdef cost table at its cost table value.</summary>
    float CostTableFloat(ItemProperty p, string column, float fallback)
    {
        int table = _world.Resources.TwoDA("itempropdef").GetInt(p.Type, "CostTableResRef", 0);
        var costTables = _world.Resources.TwoDA("iprp_costtable");
        if (table <= 0 || table >= costTables.RowCount) return fallback;
        return _world.Resources.TwoDA(costTables.Get(table, "Name")).GetFloat(p.CostTableValue, column, fallback);
    }

    /// <summary>
    /// Total carried weight of a creature in tenths of a pound (inventory and equipment; while polymorphed, the
    /// equipment it put away still weighs and the form's items don't).
    /// </summary>
    public int CarriedTenths(Creature c) =>
        (int)Math.Min(int.MaxValue, c.Inventory.Sum(i => (long)WeightTenths(i))
            + global::OpenWinter.Game.Rules.EffectSystem.OwnEquipment(c).Sum(kv => (long)WeightTenths(kv.Value)));

    /// <summary>
    /// The Strength that sets carrying capacity: the score with its effects, or a form's Strength while polymorphed
    /// (visual scene polymorphpc: the Rakshasa's 17 gives 173 lbs where the player's own 10 gave 86). Effects count
    /// (encumberdrive: Strength 10 raised to 14 carries 100 lbs unencumbered, lowered to 6 is encumbered at 65).
    /// </summary>
    public int CarryStrength(Creature c) => global::OpenWinter.Game.Rules.CreatureRules.AbilityScore(_world, c, 0);

    /// <summary>0 = normal, 1 = encumbered, 2 = overloaded (encumbrance.2da by strength; values are tenths of a pound).</summary>
    public int Encumbrance(Creature c)
    {
        var t = _world.Resources.TwoDA("encumbrance");
        int str = Math.Clamp(CarryStrength(c), 0, Math.Max(0, t.RowCount - 1));
        int carried = CarriedTenths(c);
        int normal = t.GetInt(str, "Normal", int.MaxValue), heavy = t.GetInt(str, "Heavy", int.MaxValue);
        return carried > heavy ? 2 : carried > normal ? 1 : 0;
    }

    // ----- value -----

    /// <summary>
    /// GetGoldPieceValue, as the original prices items (single precision throughout):
    /// <c>unit = int((BaseCost + int(1000 * Multiplier^2) - int(1000 * NegMultiplier^2) + int(SpellCosts)) * ItemMultiplier) + AddCost</c>,
    /// where each property costs (itempropdef Cost, or the subtype's Cost when that is blank) * the cost table
    /// value's Cost. Unidentified items are worth int(BaseCost * ItemMultiplier). A full stack is worth at least
    /// 1 and smaller stacks their share of it; plot items and creature items are worth 0.
    /// </summary>
    public int GoldValue(Item item)
    {
        if (item.HeldValue is { } held && item.HeldAt == _world.Time) return held;
        if (item.Plot || IsCreatureItem(item.BaseItem)) return 0;
        int maxStack = MaxStack(item.BaseItem);
        long unit = (long)UnitValue(item, item.Identified) + (item.Identified ? item.AddCost : 0);
        long full = Math.Max(1, unit * maxStack);
        return (int)Math.Clamp(full * Math.Max(1, item.StackSize) / maxStack, 0, int.MaxValue);
    }

    /// <summary>Creature weapons, hides and the like: base items only creature slots take.</summary>
    public bool IsCreatureItem(int baseItem)
    {
        const int creatureSlots = EquipSlot.CreatureLeft | EquipSlot.CreatureRight | EquipSlot.CreatureBite | EquipSlot.CreatureArmour;
        int slots = EquipableSlots(baseItem);
        return (slots & creatureSlots) != 0 && (slots & ~creatureSlots) == 0;
    }

    /// <summary>Value of one unit, before AddCost; without properties when <paramref name="includeProperties"/> is false (unidentified).</summary>
    public double UnitValue(Item item, bool includeProperties)
    {
        if (!Valid(item.BaseItem)) return 0;
        float baseCost = item.BaseItem == BaseItemArmor
            ? _world.Resources.TwoDA("armor").GetFloat(BaseAC(item), "COST")
            : BaseItems.GetFloat(item.BaseItem, "BaseCost");
        float itemMult = BaseItems.GetFloat(item.BaseItem, "ItemMultiplier", 0);
        if (!includeProperties) return Math.Max(0, (int)(baseCost * itemMult));
        float pos = 0, neg = 0;
        // Cast spell costs: the most expensive counts in full, one other at 75%, the rest at 50%. The 75% one is
        // the dearest seen while it wasn't the most expensive so far (a property that later loses first place drops to 50%).
        float best = 0, second = 0, rest = 0;
        int charges = item.PricedCharges ??= item.Charges; // the first read after a change fixes the value
        // RemoveItemProperty takes the property off only once the running script ends: until then a reprice
        // (AddItemProperty, SetIdentified) still counts it (items_value_charges: a stripped stock wand given a new
        // spell is worth both spells until the script returns).
        var props = item.RemovedProperties is { } removed && item.HeldAt == _world.Time
            ? removed.Concat(item.Properties) : item.Properties;
        foreach (var p in props)
        {
            if (p.DurationType != DurationPermanent) continue;
            float cost = PropertyCost(p);
            if (p.Type == PropCastSpell)
            {
                // Charge-based uses (iprp_chargecost rows 2-6) scale with the charges the item had when priced.
                if (p.CostTableValue is >= 2 and <= 6) cost *= charges / 50f;
                if (cost > best) { rest += best * 0.5f; best = cost; }
                else if (cost > second) { rest += second * 0.5f; second = cost; }
                else rest += cost * 0.5f;
            }
            else if (cost >= 0) pos += cost;
            else neg -= cost;
        }
        int quadratic = (int)(1000f * (pos * pos)) - (int)(1000f * (neg * neg));
        int spells = (int)(best + second * 0.75f + rest);
        return Math.Max(0, (int)((baseCost + quadratic + spells) * itemMult));
    }

    /// <summary>Cost contribution of one item property (negative for penalties).</summary>
    public float PropertyCost(ItemProperty p)
    {
        var def = _world.Resources.TwoDA("itempropdef");
        if (p.Type < 0 || p.Type >= def.RowCount) return 0;
        // The property's own cost, or (when itempropdef leaves it blank) the subtype's cost.
        float propCost = 0;
        if (!def.IsEmpty(p.Type, "Cost")) propCost = def.GetFloat(p.Type, "Cost");
        else if (def.Get(p.Type, "SubTypeResRef") is { Length: > 0 } subTable)
        {
            var t = _world.Resources.TwoDA(subTable);
            if (t.ColumnIndex("Cost") >= 0) propCost = t.GetFloat(p.SubType, "Cost");
        }
        // The cost table is itempropdef's, whatever the property itself names (some blueprints store another).
        float valueCost = 1;
        var costTables = _world.Resources.TwoDA("iprp_costtable");
        int table = def.GetInt(p.Type, "CostTableResRef", 0);
        if (table > 0 && table < costTables.RowCount)
        {
            var t = _world.Resources.TwoDA(costTables.Get(table, "Name"));
            if (t.ColumnIndex("Cost") >= 0) valueCost = t.GetFloat(p.CostTableValue, "Cost");
        }
        return propCost * valueCost;
    }
}
