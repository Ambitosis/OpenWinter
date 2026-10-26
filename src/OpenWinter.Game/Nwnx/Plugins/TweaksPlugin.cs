using System.Runtime.CompilerServices;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Tweaks: rule changes switched on by NWNX_TWEAKS_* settings (the plugin has no script functions). The game
/// systems ask <see cref="Of"/> for the ones they implement. Measured on NWNX's server: parity suites nwnx_tweaks
/// (all on), nwnx_tweaks_off, nwnx_tweaks_cost2 and nwnx_tweaks_cost3.
/// </summary>
public sealed class NwnxTweaks
{
    static readonly ConditionalWeakTable<World, NwnxTweaks> Cache = new();
    readonly Func<string, string?> _source;

    /// <summary>StringToInt reads "0x1A" as hex, "010" as octal and "0b11" as binary; a value outside an int is 0.</summary>
    public bool StringToIntBaseToAuto { get; }
    /// <summary>
    /// 1: charges past 50 add nothing to an item's value; 2: up to 50 as in 1.69, then less, up to twice the 50-charge
    /// value at 250; 3: 1.69's linear value throughout. In each, a charge up to 50 is worth five vanilla ones.
    /// </summary>
    public int ItemChargesCostMode { get; }
    /// <summary>Items merge into a stack only when their local variables match too.</summary>
    public bool CompareVariablesWhenMerging { get; }
    /// <summary>OnPlayerEquipItem and OnPlayerUnEquipItem fire for every creature, its equipment at creation included.</summary>
    public bool FireEquipEventsForAllCreatures { get; }
    /// <summary>An item whose last charge is used stays, with 0 charges.</summary>
    public bool PreserveDepletedItems { get; }
    /// <summary>The hit points at or below which a dying player character dies (vanilla -10); between 0 and -127.</summary>
    public int PlayerDyingHpLimit { get; }
    /// <summary>Sneak attacks land on creatures immune only to critical hits.</summary>
    public bool SneakAttackIgnoreCritImmunity { get; }
    /// <summary>Parry has no limit of parried attacks per round.</summary>
    public bool ParryAllAttacks { get; }
    /// <summary>Dexterity is never reduced to 3 by paralysis, sleep and the like.</summary>
    public bool AlwaysReturnFullDexStat { get; }
    /// <summary>Dungeon Masters cannot pause the server, nor quick-save it.</summary>
    public bool DisablePause { get; }
    public bool DisableQuicksave { get; }

    NwnxTweaks(NwnxRegistry reg)
    {
        _source = reg.EnvironmentVariable;
        StringToIntBaseToAuto = reg.EnvBool("NWNX_TWEAKS_STRINGTOINT_BASE_TO_AUTO");
        ItemChargesCostMode = Math.Clamp(reg.EnvInt("NWNX_TWEAKS_ITEM_CHARGES_COST_MODE"), 0, 3);
        CompareVariablesWhenMerging = reg.EnvBool("NWNX_TWEAKS_COMPARE_VARIABLES_WHEN_MERGING");
        FireEquipEventsForAllCreatures = reg.EnvBool("NWNX_TWEAKS_FIRE_EQUIP_EVENTS_FOR_ALL_CREATURES");
        PreserveDepletedItems = reg.EnvBool("NWNX_TWEAKS_PRESERVE_DEPLETED_ITEMS");
        PlayerDyingHpLimit = reg.Env("NWNX_TWEAKS_PLAYER_DYING_HP_LIMIT") != null ? Math.Clamp(reg.EnvInt("NWNX_TWEAKS_PLAYER_DYING_HP_LIMIT", -10), -127, 0) : -10;
        SneakAttackIgnoreCritImmunity = reg.EnvBool("NWNX_TWEAKS_SNEAK_ATTACK_IGNORE_CRIT_IMMUNITY");
        ParryAllAttacks = reg.EnvBool("NWNX_TWEAKS_PARRY_ALL_ATTACKS");
        AlwaysReturnFullDexStat = reg.EnvBool("NWNX_TWEAKS_ALWAYS_RETURN_FULL_DEX_STAT");
        DisablePause = reg.EnvBool("NWNX_TWEAKS_DISABLE_PAUSE");
        DisableQuicksave = reg.EnvBool("NWNX_TWEAKS_DISABLE_QUICKSAVE");
    }

    /// <summary>The world's tweaks, read from its NWNX settings once (again if the settings source is replaced).</summary>
    public static NwnxTweaks Of(World w)
    {
        if (Cache.TryGetValue(w, out var t) && t._source == w.System<NwnxRegistry>().EnvironmentVariable) return t;
        t = new NwnxTweaks(w.System<NwnxRegistry>());
        Cache.AddOrUpdate(w, t);
        return t;
    }

    /// <summary>A created creature's equipment fires OnPlayerEquipItem, slot by slot, after the creating script.</summary>
    public static void EquippedAtCreation(World w, Objects.Creature c)
    {
        for (int slot = 0; slot < 18; slot++)
        {
            if (!c.Equipment.TryGetValue(Objects.EquipSlot.Bit(slot), out var item)) continue;
            int s = slot;
            w.After(0, () =>
            {
                if (item.IsDestroyed || c.IsDestroyed) return;
                w.RunEvent(w.Module, "Mod_OnPlrEqItm", new Scripting.EventContext().Set("EquippedItem", item.Id).Set("EquippedBy", c.Id).Set("EquippedSlot", s));
            });
        }
    }

    /// <summary>The charge multiplier of a charged cast-spell property's value; vanilla is charges / 50.</summary>
    public float ChargeCostMultiplier(int charges) => ItemChargesCostMode switch
    {
        1 => Math.Min(charges, 50) / 10f,
        // Past 50, every four charges add one 1.69 charge and the rest a charge each at the vanilla rate.
        2 => charges <= 50 ? charges / 10f : 5f + (charges - 50) / 4 / 10f + (charges - 50) % 4 / 50f,
        3 => charges / 10f,
        _ => charges / 50f,
    };

    /// <summary>C's strtol with base 0 (and C23's "0b"), limited to an int: anything outside it is 0.</summary>
    public static int StringToIntAuto(string s)
    {
        int i = 0;
        while (i < s.Length && s[i] is ' ' or '\t' or '\n' or '\v' or '\f' or '\r') i++;
        bool negative = false;
        if (i < s.Length && s[i] is '+' or '-') { negative = s[i] == '-'; i++; }
        int radix = 10;
        if (i + 1 < s.Length && s[i] == '0' && s[i + 1] is 'x' or 'X' && i + 2 < s.Length && Uri.IsHexDigit(s[i + 2])) { radix = 16; i += 2; }
        else if (i + 1 < s.Length && s[i] == '0' && s[i + 1] is 'b' or 'B' && i + 2 < s.Length && s[i + 2] is '0' or '1') { radix = 2; i += 2; }
        else if (i < s.Length && s[i] == '0') radix = 8;
        long value = 0;
        for (; i < s.Length; i++)
        {
            int d = s[i] is >= '0' and <= '9' ? s[i] - '0' : s[i] is >= 'a' and <= 'z' ? s[i] - 'a' + 10 : s[i] is >= 'A' and <= 'Z' ? s[i] - 'A' + 10 : 99;
            if (d >= radix) break;
            value = value * radix + d;
            if (value > 1L << 33) return 0;
        }
        if (negative) value = -value;
        return value is < int.MinValue or > int.MaxValue ? 0 : (int)value;
    }
}
