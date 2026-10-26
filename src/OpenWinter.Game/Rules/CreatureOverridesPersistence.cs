using System.Globalization;
using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Creature settings scripts asked to persist (NWNX_Creature's bPersist), kept where NWNX:EE keeps them: the
/// creature's NWNX_POS string field, "[INTMAP:n]" then n entries "&lt;key length&gt;key = value;", newest first, keys
/// "NWNX_Creature!NAME" (nwserver nwnx_creature_persist). They travel with the creature's GFF (character files,
/// CopyObject, ObjectToJson), so a character moved between servers keeps them. Other plugins' entries are kept.
/// </summary>
public static class CreatureOverridesPersistence
{
    public const string Field = "NWNX_POS";
    const string Prefix = "NWNX_Creature!";

    /// <summary>Records a setting as persisted (or, with null, as not persisted any more).</summary>
    public static void Persist(CreatureOverrides o, string name, int? value)
    {
        o.Persisted.RemoveAll(p => p.Key == name);
        if (value is int v) o.Persisted.Insert(0, new(name, v));
    }

    public static string CritKey(string kind, int hand, int baseItem) =>
        $"CRITICAL_{kind}!{hand}" + (baseItem >= 0 ? $"!BI{baseItem}" : "");

    public static string BypassKey(int type) => type < 0 ? $"BYPASS_EFF_IMM_IN{-type}" : $"BYPASS_EFF_IMM_OUT{type}";

    /// <summary>Restores the persisted settings from a creature's NWNX_POS, when it has some.</summary>
    public static void Load(Creature c)
    {
        var raw = c.Template.GetString(Field);
        if (raw.Length == 0) return;
        var (ints, _, _) = Parse(raw);
        foreach (var (key, value) in ints.AsEnumerable().Reverse())
        {
            if (!key.StartsWith(Prefix, StringComparison.Ordinal)) continue;
            string name = key[Prefix.Length..];
            if (!Apply(CreatureOverrides.Of(c), name, value)) continue;
            Persist(c.Overrides!, name, value);
        }
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

    /// <summary>Writes the creature's persisted settings into a serialised copy of it.</summary>
    public static void Save(Creature c, GffStruct s)
    {
        var raw = c.Template.GetString(Field);
        var persisted = c.Overrides?.Persisted;
        if (raw.Length == 0 && (persisted == null || persisted.Count == 0)) return;
        var (ints, rest, whole) = Parse(raw);
        // Entries this can't read are left alone rather than rewritten.
        if (!whole) return;
        ints.RemoveAll(e => e.Key.StartsWith(Prefix, StringComparison.Ordinal));
        if (persisted != null) ints.InsertRange(0, persisted.Select(p => new KeyValuePair<string, int>(Prefix + p.Key, p.Value)));
        s.SetString(Field, ints.Count == 0 ? rest : Format(ints) + rest);
    }

    /// <summary>The INTMAP entries of an NWNX_POS string and whatever follows them.</summary>
    public static (List<KeyValuePair<string, int>> Ints, string Tail, bool Whole) Parse(string raw)
    {
        var ints = new List<KeyValuePair<string, int>>();
        const string head = "[INTMAP:";
        if (!raw.StartsWith(head, StringComparison.Ordinal)) return (ints, raw, true);
        int close = raw.IndexOf(']');
        if (close < 0 || !int.TryParse(raw.AsSpan(head.Length, close - head.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)) return (ints, raw, false);
        int at = close + 1;
        int read = 0;
        for (int i = 0; i < count && at < raw.Length && raw[at] == '<'; i++)
        {
            int gt = raw.IndexOf('>', at);
            if (gt < 0 || !int.TryParse(raw.AsSpan(at + 1, gt - at - 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int len)) break;
            int keyEnd = gt + 1 + len;
            int semi = keyEnd <= raw.Length ? raw.IndexOf(';', keyEnd) : -1;
            if (semi < 0) break;
            string key = raw.Substring(gt + 1, len);
            var text = raw[keyEnd..semi].Trim();
            if (text.StartsWith('=')) text = text[1..].Trim();
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) break;
            ints.Add(new(key, value));
            read++;
            at = semi + 1;
        }
        return (ints, raw[at..], read == count);
    }

    public static string Format(IReadOnlyList<KeyValuePair<string, int>> ints)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"[INTMAP:{ints.Count}]");
        foreach (var (key, value) in ints) sb.Append(CultureInfo.InvariantCulture, $"<{key.Length}>{key} = {value};");
        return sb.ToString();
    }
}
