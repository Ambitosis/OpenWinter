using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Item properties as the toolset stores them (PropertiesList: PropertyName, Subtype,
/// CostTable, CostValue, Param1, Param1Value) plus the few lookups the damage and
/// immunity rules need. Properties built by script follow the original server: no subtype,
/// cost value or param is -1, Param1 (the param table) is always -1, and the duration type
/// is permanent until the property is added.
/// </summary>
public static class ItemPropertyRules
{
    public const int NoParam = 255;

    /// <summary>
    /// Builds a property as ItemPropertyCustom does: the cost table from itempropdef.2da, and an invalid
    /// property when a subtype, cost value or param isn't a row of its table (subtype and cost rows also
    /// need a Name). Without a cost table the cost value must be -1; without a subtype or param table
    /// any value is kept.
    /// </summary>
    public static ItemProperty Make(World world, int type, int subType = -1, int costValue = -1, int param1Value = -1)
    {
        if (type < 0) return new ItemProperty();
        var res = world.Resources;
        var def = res.TwoDA("itempropdef");
        bool known = type < def.RowCount && !def.IsEmpty(type, "Label");
        var ip = new ItemProperty { Type = type, SubType = subType, CostTable = -1, CostTableValue = -1, Param1 = -1, Param1Value = param1Value, DurationType = 2 };

        string subTable = known ? def.Get(type, "SubTypeResRef") : "";
        TwoDA? sub = subTable.Length > 0 ? res.TwoDA(subTable.ToLowerInvariant()) : null;
        if (sub != null && !NamedRow(sub, subType)) return new ItemProperty();

        if (known && !def.IsEmpty(type, "CostTableResRef")) ip.CostTable = def.GetInt(type, "CostTableResRef", -1);
        if (ip.CostTable > 0)
        {
            var costTables = res.TwoDA("iprp_costtable");
            var cost = ip.CostTable < costTables.RowCount ? res.TwoDA(costTables.Get(ip.CostTable, "Name").ToLowerInvariant()) : null;
            if (cost == null || !NamedRow(cost, costValue)) return new ItemProperty();
            ip.CostTableValue = costValue;
        }
        else if (costValue != -1) return new ItemProperty();

        // The param table is itempropdef's, or else the subtype row's (where -1 means none).
        int param = known && !def.IsEmpty(type, "Param1ResRef") ? def.GetInt(type, "Param1ResRef", -1) : -1;
        bool fromSubtype = false;
        if (param < 0 && sub != null && sub.ColumnIndex("Param1ResRef") >= 0 && !sub.IsEmpty(subType, "Param1ResRef"))
        {
            param = sub.GetInt(subType, "Param1ResRef", -1);
            fromSubtype = true;
        }
        if (param >= 0 && !(fromSubtype && param1Value == -1))
        {
            var paramTables = res.TwoDA("iprp_paramtable");
            int rows = param < paramTables.RowCount ? res.TwoDA(paramTables.Get(param, "TableResRef").ToLowerInvariant()).RowCount : 0;
            if (param1Value < 0 || param1Value >= rows) return new ItemProperty();
        }
        return ip;
    }

    /// <summary>A row inside the table whose Name (when the table has one) isn't blank.</summary>
    static bool NamedRow(TwoDA t, int row) =>
        row >= 0 && row < t.RowCount && (t.ColumnIndex("Name") < 0 || !t.IsEmpty(row, "Name"));

    /// <summary>iprp_damagetype.2da row (IP_CONST_DAMAGETYPE_*) to DAMAGE_TYPE_* bits (physical = all three weapon types).</summary>
    public static int DamageTypeBits(int iprpRow) => iprpRow switch
    {
        0 => 1,
        1 => 2,
        2 => 4,
        4 => 7,
        >= 5 and <= 13 => 8 << (iprpRow - 5),
        _ => 0,
    };

    /// <summary>iprp_protection.2da row (+1, +2, ...) to the DAMAGE_POWER_* it represents (+6 and up skip DAMAGE_POWER_ENERGY).</summary>
    public static int ProtectionPower(int row) => row < 5 ? row + 1 : row + 2;

    public static bool GrantsImmunity(EffectConstants k, ItemProperty ip, int immunityType)
    {
        if (ip.Type == k["ITEM_PROPERTY_MIND_BLANK"]) return immunityType == k["IMMUNITY_TYPE_MIND_SPELLS"];
        if (ip.Type == k["ITEM_PROPERTY_FREEDOM_OF_MOVEMENT"])
            return immunityType == k["IMMUNITY_TYPE_PARALYSIS"] || immunityType == k["IMMUNITY_TYPE_ENTANGLE"]
                || immunityType == k["IMMUNITY_TYPE_SLOW"] || immunityType == k["IMMUNITY_TYPE_MOVEMENT_SPEED_DECREASE"];
        if (ip.Type != k["ITEM_PROPERTY_IMMUNITY_MISCELLANEOUS"]) return false;
        string misc = ip.SubType switch
        {
            0 => "SNEAK_ATTACK",
            1 => "NEGATIVE_LEVEL",
            2 => "MIND_SPELLS",
            3 => "POISON",
            4 => "DISEASE",
            5 => "FEAR",
            6 => "KNOCKDOWN",
            7 => "PARALYSIS",
            8 => "CRITICAL_HIT",
            9 => "DEATH",
            _ => "",
        };
        if (misc.Length == 0) return false;
        if (ip.SubType == 1 && immunityType == k["IMMUNITY_TYPE_ABILITY_DECREASE"]) return true;
        return immunityType == k["IMMUNITY_TYPE_" + misc];
    }

    static IEnumerable<ItemProperty> Equipped(GameObject o) =>
        o is Creature c ? c.Equipment.Values.SelectMany(i => i.Properties) : Enumerable.Empty<ItemProperty>();

    static int Cost(World w, string table, string column, int row) => w.Resources.TwoDA(table).GetInt(row, column, 0);

    /// <summary>Net damage immunity percent from equipped items for one DAMAGE_TYPE_* bit (immunity minus vulnerability).</summary>
    public static int ItemDamageImmunity(World w, GameObject o, int damageType)
    {
        var k = EffectConstants.Of(w);
        int imm = k["ITEM_PROPERTY_IMMUNITY_DAMAGE_TYPE"], vul = k["ITEM_PROPERTY_DAMAGE_VULNERABILITY"], pct = 0;
        foreach (var ip in Equipped(o))
        {
            if ((DamageTypeBits(ip.SubType) & damageType) == 0) continue;
            if (ip.Type == imm) pct += Cost(w, "iprp_immuncost", "Value", ip.CostTableValue);
            else if (ip.Type == vul) pct -= Cost(w, "iprp_damvulcost", "Value", ip.CostTableValue);
        }
        return pct;
    }

    /// <summary>Best damage resistance (HP per hit) from equipped items for one damage type.</summary>
    public static int ItemDamageResistance(World w, GameObject o, int damageType)
    {
        int prop = EffectConstants.Of(w)["ITEM_PROPERTY_DAMAGE_RESISTANCE"], best = 0;
        foreach (var ip in Equipped(o))
            if (ip.Type == prop && (DamageTypeBits(ip.SubType) & damageType) != 0)
                best = Math.Max(best, Cost(w, "iprp_resistcost", "Amount", ip.CostTableValue));
        return best;
    }

    /// <summary>Best damage reduction (soak) from equipped items that a hit of <paramref name="power"/> doesn't overcome.</summary>
    public static int ItemDamageReduction(World w, GameObject o, int power)
    {
        int prop = EffectConstants.Of(w)["ITEM_PROPERTY_DAMAGE_REDUCTION"], best = 0;
        foreach (var ip in Equipped(o))
            if (ip.Type == prop && power < ProtectionPower(ip.SubType))
                best = Math.Max(best, Cost(w, "iprp_soakcost", "Amount", ip.CostTableValue));
        return best;
    }

    /// <summary>Uses per day a Cast Spell property allows (0 when it isn't a per-day property).</summary>
    public static int MaxUsesPerDay(World w, ItemProperty ip)
    {
        var k = EffectConstants.Of(w);
        if (ip.Type != k["ITEM_PROPERTY_CAST_SPELL"]) return 0;
        // iprp_chargecost rows IP_CONST_CASTSPELL_NUMUSES_1_USE_PER_DAY .. _5_USES_PER_DAY.
        int first = k["IP_CONST_CASTSPELL_NUMUSES_1_USE_PER_DAY"], last = k["IP_CONST_CASTSPELL_NUMUSES_5_USES_PER_DAY"];
        return ip.CostTableValue >= first && ip.CostTableValue <= last ? ip.CostTableValue - first + 1 : 0;
    }
}
