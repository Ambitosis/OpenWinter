using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>
/// What the client's item tooltips, inventory, store and loot windows show about an item: its base
/// type and category, weapon damage or armour figures, properties, charges, weight, value, flags and
/// the requirements the viewer does or doesn't meet. Sent as the "info" member of every item entry.
/// </summary>
public sealed partial class InventorySystem
{
    /// <summary>Tooltip categories (the client's backpack filter tabs group them).</summary>
    public static class ItemCategory
    {
        public const string Weapon = "weapon", Ammo = "ammo", Armor = "armor", Shield = "shield", Apparel = "apparel",
            Jewelry = "jewelry", Consumable = "consumable", Container = "container", Misc = "misc";
    }

    // itempropdef rows that restrict who may use an item (UseLimitation*).
    const int PropUseLimitAlignmentGroup = 62, PropUseLimitSpecificAlignment = 65, PropUseLimitTerrain = 66;
    const int PropThievesTools = 55, PropTrap = 70, PropHealersKit = 80;

    /// <summary>The item's tooltip details as seen by <paramref name="viewer"/> (requirements are checked against them).</summary>
    public object ItemInfo(Item item, Creature? viewer = null)
    {
        var res = _world.Resources;
        var bi = res.TwoDA("baseitems");
        bool valid = item.BaseItem >= 0 && item.BaseItem < bi.RowCount;
        string Tlk(int strref) => strref >= 0 ? res.TlkString(unchecked((uint)strref)) : "";

        string type = valid ? Tlk(bi.GetInt(item.BaseItem, "Name", -1)) : "";
        if (type.Length == 0 && valid) type = bi.Get(item.BaseItem, "label");
        type = type.TrimEnd('*', ' ');
        string category = Category(item.BaseItem);

        // Weapons: dice, damage type, critical range.
        string damage = "", damageType = "", critical = "";
        if (category is ItemCategory.Weapon or ItemCategory.Ammo && valid)
        {
            int dice = bi.GetInt(item.BaseItem, "NumDice"), die = bi.GetInt(item.BaseItem, "DieToRoll");
            if (dice > 0 && die > 0)
            {
                int enh = item.Identified ? Rules.Enhancement(item) : 0;
                damage = $"{dice}d{die}" + (enh > 0 ? $" +{enh}" : "");
                damageType = bi.GetInt(item.BaseItem, "WeaponType") switch
                {
                    1 => "Piercing",
                    2 => "Bludgeoning",
                    3 => "Slashing",
                    4 => "Piercing / Slashing",
                    5 => "Bludgeoning / Piercing",
                    _ => "",
                };
                int threat = Math.Max(1, bi.GetInt(item.BaseItem, "CritThreat", 1)), mult = Math.Max(2, bi.GetInt(item.BaseItem, "CritHitMult", 2));
                critical = (threat > 1 ? $"{21 - threat}–20" : "20") + $" / ×{mult}";
            }
        }

        // Armour class: armour, shields and anything else with AC (helmets, bracers with AC bonus).
        int ac = item.Identified ? Rules.ACValue(item) : Rules.BaseAC(item);
        bool showAc = category is ItemCategory.Armor or ItemCategory.Shield || ac > 0;
        object? armor = null;
        if (item.BaseItem == ItemRules.BaseItemArmor)
        {
            var t = res.TwoDA("armor");
            int baseAc = Rules.BaseAC(item);
            int maxDex = t.GetInt(baseAc, "DEXBONUS", 100);
            armor = new
            {
                weight = baseAc == 0 ? "Clothing" : baseAc <= 3 ? "Light" : baseAc <= 5 ? "Medium" : "Heavy",
                maxDex = maxDex >= 100 ? -1 : maxDex,
                checkPenalty = t.GetInt(baseAc, "ACCHECK", 0),
                spellFailure = t.GetInt(baseAc, "ARCANEFAILURE%", 0),
            };
        }
        else if (category == ItemCategory.Shield && valid)
            armor = new
            {
                weight = "Shield",
                maxDex = -1,
                checkPenalty = -Math.Abs(bi.GetInt(item.BaseItem, "ArmorCheckPen", 0)),
                spellFailure = bi.GetInt(item.BaseItem, "ArcaneSpellFailure", 0),
            };

        // Properties (identified only), with use limitations listed as requirements instead.
        var properties = new List<object>();
        var requirements = new List<object>();
        bool usable = false;
        foreach (var p in item.Properties)
        {
            if (p.DurationType is not (1 or 2)) continue;
            if (p.Type is ItemRules.PropCastSpell or PropHealersKit or PropThievesTools or PropTrap && p.Useable) usable = true;
            if (!item.Identified) continue;
            string text = Requests.Examine.PropertyText(_world, p);
            if (text.Length == 0) continue;
            if (p.Type is >= PropUseLimitAlignmentGroup and <= PropUseLimitTerrain)
            {
                requirements.Add(new { text, met = viewer == null || MeetsLimitation(viewer, item, p.Type) });
                continue;
            }
            int uses = MaxUsesPerDay(p);
            if (uses > 0) text += $" ({Math.Min(p.UsesPerDay, uses)}/{uses} today)";
            properties.Add(new { text, bad = Rules.PropertyCost(p) < 0, temporary = p.DurationType == 1 });
        }

        // Armor uses its physical weight proficiency; weapons/shields use any ReqFeat column.
        if (valid)
        {
            var feats = Enumerable.Range(0, 5).Select(i => bi.ColumnIndex("ReqFeat" + i) >= 0 && !bi.IsEmpty(item.BaseItem, "ReqFeat" + i) ? bi.GetInt(item.BaseItem, "ReqFeat" + i, -1) : -1)
                .Where(f => f >= 0).ToList();
            if (item.BaseItem == ItemRules.BaseItemArmor)
            {
                feats.Clear();
                int armorFeat = ArmorProficiency(item);
                if (armorFeat >= 0) feats.Add(armorFeat);
            }
            if (feats.Count > 0)
            {
                var featTable = res.TwoDA("feat");
                string names = string.Join(" or ", feats.Select(f => Tlk(featTable.GetInt(f, "FEAT", -1))).Where(s => s.Length > 0).Distinct());
                bool met = viewer == null || feats.Any(f => ViewerHasFeat(viewer, f));
                if (names.Length > 0) requirements.Add(new { text = names, met });
            }
        }

        // Item level restriction, when the server enforces it.
        int value = Rules.GoldValue(item);
        if (_world.ItemLevelRestrictions && item.Identified)
        {
            var t = res.TwoDA("itemvalue");
            int level = 0;
            for (int row = 0; row < t.RowCount; row++)
                if (t.GetInt(row, "MAXSINGLEITEMVALUE", int.MaxValue) >= value) { level = row + 1; break; }
            if (level > 1) requirements.Add(new { text = $"Character level {level}", met = viewer == null || !viewer.IsPC || _world.CanEquipByLevel(viewer, item) });
        }

        int maxCharges = valid ? bi.GetInt(item.BaseItem, "ChargesStarting", 0) : 0;
        bool showCharges = item.Charges > 0 || maxCharges > 0 || item.Properties.Any(p => p.Type == ItemRules.PropCastSpell && p.CostTable == 3 && p.CostTableValue is >= 2 and <= 6);
        return new
        {
            type,
            category,
            panel = Rules.StorePanel(item.BaseItem),
            inventorySound = Rules.InventorySound(item),
            damage,
            damageType,
            critical,
            ac = showAc ? ac : -1,
            armor,
            properties,
            requirements,
            charges = showCharges ? item.Charges : -1,
            weight = Rules.WeightTenths(item),
            value = item.Identified ? value : -1,
            plot = item.Plot,
            stolen = item.Stolen,
            cursed = item.Cursed,
            usable,
            maxStack = Rules.MaxStack(item.BaseItem),
            width = valid ? Math.Max(1, bi.GetInt(item.BaseItem, "InvSlotWidth", 1)) : 1,
            height = valid ? Math.Max(1, bi.GetInt(item.BaseItem, "InvSlotHeight", 1)) : 1,
            slots = Rules.EquipableSlots(item.BaseItem),
        };
    }

    bool ViewerHasFeat(Creature c, int feat) => global::OpenWinter.Game.Rules.CreatureRules.HasFeat(_world, c, feat, ignoreUses: true);

    /// <summary>Use limitations: class (63) and race (64) are checked; alignment and terrain are shown as met.</summary>
    bool MeetsLimitation(Creature c, Item item, int type)
    {
        var limits = item.Properties.Where(p => p.Type == type).Select(p => p.SubType).ToList();
        return type switch
        {
            63 => c.Classes.Any(cl => limits.Contains(cl.Class)),
            64 => limits.Contains(c.Race),
            _ => true,
        };
    }

    /// <summary>The tooltip category of a base item (from baseitems Category and StorePanel).</summary>
    public string Category(int baseItem)
    {
        var bi = _world.Resources.TwoDA("baseitems");
        if (baseItem < 0 || baseItem >= bi.RowCount) return ItemCategory.Misc;
        if (Rules.IsContainer(baseItem)) return ItemCategory.Container;
        if (baseItem == ItemRules.BaseItemArmor) return ItemCategory.Armor;
        int slots = Rules.EquipableSlots(baseItem);
        if ((slots & (EquipSlot.Neck | EquipSlot.LeftRing | EquipSlot.RightRing)) != 0) return ItemCategory.Jewelry;
        // Gloves, bracers, cloaks, belts, boots and helmets (gloves are listed with melee weapons for monks).
        if ((slots & (EquipSlot.Head | EquipSlot.Arms | EquipSlot.Cloak | EquipSlot.Belt | EquipSlot.Boots)) != 0) return ItemCategory.Apparel;
        return bi.GetInt(baseItem, "Category", 0) switch
        {
            1 or 2 or 7 => ItemCategory.Weapon,
            3 => ItemCategory.Shield,
            6 => ItemCategory.Ammo,
            8 => Rules.IsWeapon(baseItem) ? ItemCategory.Weapon : ItemCategory.Consumable,
            9 or 10 or 11 or 15 or 19 => ItemCategory.Consumable,
            _ => ItemCategory.Misc,
        };
    }
}
