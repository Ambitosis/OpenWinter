using System.Globalization;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Creature settings scripts asked to persist (NWNX_Creature's bPersist), kept where NWNX:EE keeps them: the
/// creature's NWNX_POS, as INTMAP entries "NWNX_Creature!NAME" (nwserver nwnx_creature_persist), in the store every
/// plugin shares (<see cref="Nwnx.NwnxPersistence"/>). They travel with the creature's GFF (character files,
/// CopyObject, ObjectToJson), so a character moved between servers keeps them. Other plugins' entries are kept.
/// </summary>
public static class CreatureOverridesPersistence
{
    public const string Field = Nwnx.NwnxPersistence.Field;
    const string Prefix = "NWNX_Creature!";

    /// <summary>Records a setting as persisted (or, with null, as not persisted any more).</summary>
    public static void Persist(Creature c, string name, int? value)
    {
        if (value is int v) Nwnx.NwnxPersistence.Set(c, Prefix + name, Nwnx.NwnxValue.Int(v), persist: true);
        else Nwnx.NwnxPersistence.Delete(c, Prefix + name);
    }

    public static string CritKey(string kind, int hand, int baseItem) =>
        $"CRITICAL_{kind}!{hand}" + (baseItem >= 0 ? $"!BI{baseItem}" : "");

    public static string BypassKey(int type) => type < 0 ? $"BYPASS_EFF_IMM_IN{-type}" : $"BYPASS_EFF_IMM_OUT{type}";

    /// <summary>Restores the persisted settings from a creature's NWNX_POS, when it has some.</summary>
    public static void Load(Creature c)
    {
        if (c.PluginValues == null && c.Template.GetString(Field).Length == 0) return;
        foreach (var key in Nwnx.NwnxPersistence.Keys(c))
            if (key.StartsWith(Prefix, StringComparison.Ordinal) && Nwnx.NwnxPersistence.Get(c, key) is { Value: int value })
                Apply(CreatureOverrides.Of(c), key[Prefix.Length..], value);
    }

    static bool Apply(CreatureOverrides o, string name, int value)
    {
        if (name == "ORPVFX") o.RangedProjectileVfx = value;
        else if (name == "INITIATIVE_MOD") o.InitiativeModifier = value;
        else if (name == "MAXIMUM_BONUS_ATTACKS") o.MaxBonusAttacks = value;
        else if (TrailingInt(name, "CASTERLEVEL_OVERRIDE") is int clo) o.CasterLevelOverride[clo] = value;
        else if (TrailingInt(name, "CASTERLEVEL_MODIFIER") is int clm) o.CasterLevelModifier[clm] = value;
        else if (TrailingInt(name, "BYPASS_EFF_IMM_OUT") is int bo) o.BypassImmunity[bo] = value;
        else if (TrailingInt(name, "BYPASS_EFF_IMM_IN") is int bi) o.BypassImmunity[-bi] = value;
        else if (name.StartsWith("CRITICAL_", StringComparison.Ordinal))
        {
            var parts = name.Split('!');
            if (parts.Length < 2 || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hand)) return false;
            int item = parts.Length > 2 && parts[2].StartsWith("BI", StringComparison.Ordinal)
                && int.TryParse(parts[2][2..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b) ? b : -1;
            var table = parts[0] switch
            {
                "CRITICAL_MULTIPLIER_MODIFIER" => o.CritMultiplierModifier,
                "CRITICAL_MULTIPLIER_OVERRIDE" => o.CritMultiplierOverride,
                "CRITICAL_RANGE_MODIFIER" => o.CritRangeModifier,
                "CRITICAL_RANGE_OVERRIDE" => o.CritRangeOverride,
                _ => null,
            };
            if (table == null) return false;
            table[(hand, item)] = value;
        }
        else return false;
        return true;
    }

    static int? TrailingInt(string name, string prefix) =>
        name.StartsWith(prefix, StringComparison.Ordinal)
        && int.TryParse(name[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null;

    /// <summary>Writes the creature's persisted settings (with every other plugin's) into a serialised copy of it.</summary>
    public static void Save(Creature c, GffStruct s) => Nwnx.NwnxPersistence.WriteField(c, s);
}
