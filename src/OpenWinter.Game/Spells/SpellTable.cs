using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Spells;

/// <summary>
/// Typed reads of spells.2da: range, school, components, timing, visuals and the impact script.
/// </summary>
public static class SpellTable
{
    // SPELL_SCHOOL_*
    public const int SchoolGeneral = 0, SchoolAbjuration = 1, SchoolConjuration = 2, SchoolDivination = 3, SchoolEnchantment = 4,
        SchoolEvocation = 5, SchoolIllusion = 6, SchoolNecromancy = 7, SchoolTransmutation = 8;

    /// <summary>Touch range in metres (melee reach).</summary>
    public const float TouchRange = 2.25f;

    public static TwoDA Table(World w) => w.Resources.TwoDA("spells");

    public static string Get(World w, int spell, string column) => Table(w).Get(spell, column);

    public static bool Exists(World w, int spell) =>
        spell >= 0 && spell < Table(w).RowCount && Table(w).Get(spell, "Label").Length > 0;

    /// <summary>Casting range in metres from the Range column (P personal, T touch, S short, M medium, L long, I infinite).</summary>
    public static float Range(World w, int spell) => RangeOfCode(w, Get(w, spell, "Range"));

    /// <summary>Module-resolved ranges.2da spell rows; infinite/unknown codes retain their existing fallback.</summary>
    public static float RangeOfCode(World world, string code)
    {
        int row = code.ToUpperInvariant() switch { "P" => 0, "T" => 1, "S" => 2, "M" => 3, "L" => 4, _ => -1 };
        float fallback = RangeOfCode(code);
        if (row < 0) return fallback;
        float value = world.Resources.TwoDA("ranges").GetFloat(row, "PrimaryRange", fallback);
        return float.IsFinite(value) && value >= 0 ? value : fallback;
    }

    public static float RangeOfCode(string code) => code.ToUpperInvariant() switch
    {
        "P" => 0f,
        "T" => TouchRange,
        "S" => 8f,
        "M" => 20f,
        "L" => 40f,
        "I" => 1000f,
        _ => 8f,
    };

    public static bool IsPersonal(World w, int spell) => Get(w, spell, "Range").Equals("P", StringComparison.OrdinalIgnoreCase);

    /// <summary>SPELL_SCHOOL_* of a spell (School column letter).</summary>
    public static int School(World w, int spell) => SchoolOfLetter(Get(w, spell, "School"));

    public static int SchoolOfLetter(string letter) => letter.ToUpperInvariant() switch
    {
        "A" => SchoolAbjuration,
        "C" => SchoolConjuration,
        "D" => SchoolDivination,
        "E" => SchoolEnchantment,
        "V" => SchoolEvocation,
        "I" => SchoolIllusion,
        "N" => SchoolNecromancy,
        "T" => SchoolTransmutation,
        _ => SchoolGeneral,
    };

    public static int Innate(World w, int spell) => Table(w).GetInt(spell, "Innate", 0);
    public static bool Hostile(World w, int spell) => Table(w).GetInt(spell, "HostileSetting", 0) == 1;
    public static bool Verbal(World w, int spell) => Get(w, spell, "VS").Contains('v', StringComparison.OrdinalIgnoreCase);
    public static bool Somatic(World w, int spell) => Get(w, spell, "VS").Contains('s', StringComparison.OrdinalIgnoreCase);
    public static string ImpactScript(World w, int spell) => Get(w, spell, "ImpactScript");
    /// <summary>UserType: 1 spell, 2 special ability, 3 feat, 4 item power.</summary>
    public static int UserType(World w, int spell) => Table(w).GetInt(spell, "UserType", 1);
    /// <summary>Bit mask of METAMAGIC_* the spell accepts.</summary>
    public static int MetaMagicMask(World w, int spell) => Table(w).GetInt(spell, "MetaMagic", 0);
    public static bool SpontaneouslyCast(World w, int spell) => Table(w).GetInt(spell, "SpontaneouslyCast", 0) != 0;
    public static int Counter1(World w, int spell) => Table(w).GetInt(spell, "Counter1", -1);
    public static int Counter2(World w, int spell) => Table(w).GetInt(spell, "Counter2", -1);
    public static bool UsesConcentration(World w, int spell) => Table(w).GetInt(spell, "UseConcentration", 1) != 0;

    /// <summary>Conjuring time in seconds (ConjTime, milliseconds).</summary>
    public static float ConjureSeconds(World w, int spell) => Math.Max(0, Table(w).GetInt(spell, "ConjTime", 0)) / 1000f;
    /// <summary>Recovery after release, in seconds (CastTime, milliseconds). Release itself takes 0.5 seconds.</summary>
    public static float CastSeconds(World w, int spell) => Math.Max(0, Table(w).GetInt(spell, "CastTime", 0)) / 1000f;

    /// <summary>Whether the spell fires a projectile (Proj column).</summary>
    public static bool HasProjectile(World w, int spell) => Table(w).GetInt(spell, "Proj", 0) != 0 && Get(w, spell, "ProjModel").Length > 0;

    /// <summary>ANIMATION_LOOPING_CONJURE1/2 for the ConjAnim column ("hand" / "head"), or -1.</summary>
    public static int ConjureAnimation(string conjAnim) => conjAnim.ToLowerInvariant() switch
    {
        "hand" => 18,
        "head" => 19,
        _ => -1,
    };

    /// <summary>ANIMATION_LOOPING_CAST* for the CastAnim column, or -1.</summary>
    public static int CastAnimation(string castAnim) => castAnim.ToLowerInvariant() switch
    {
        "out" => 23,
        "touch" or "point" => 24,
        "up" => 25,
        "self" => 26,
        "area" => 27,
        _ => -1,
    };

    /// <summary>
    /// PROJECTILE_PATH_TYPE_* name for a ProjType value (homing, ballistic, ...), honouring a script override (1-4).
    /// An empty ProjType is "default": it flies at the plain logarithmic speed, like ballistic (projectile_paths).
    /// </summary>
    public static string ProjectilePath(string projType, int overridePath) => overridePath switch
    {
        1 => "homing",
        2 => "ballistic",
        3 => "highballistic",
        4 => "accelerating",
        _ => projType.Length > 0 && projType != "****" ? projType.ToLowerInvariant() : "default",
    };

    /// <summary>Script path types the cast actions accept: 0 (spells.2da) to 4 (accelerating); above that the
    /// cast command does nothing (projectile_paths path5).</summary>
    public const int MaxScriptProjectilePath = 4;

    /// <summary>
    /// Seconds from a spell's release to its impact (measured by parity suites projectile_paths,
    /// projectile_near_boundary, projectile_clock and projectile_spiral_short):
    /// distance d (3-D, measured once at release) over a speed of 3 ln(d) + 2 m/s, doubled for homing, x1.5 for
    /// accelerating and x0.8 for bounce, in single precision and truncated to whole milliseconds. High ballistic and
    /// linked take a flat 2 s; spiral adds 2.5 s. A speed of zero or less (d &lt;= e^(-2/3), about 0.513 m) gives 1 ms,
    /// spiral included. There is no upper bound: at 0.5145 m the original took 81 s.
    /// Script-generated MIRVs have their own timing.
    /// </summary>
    public static float ProjectileFlightTime(float distance, string path)
    {
        const float immediate = 0.001f;
        if (path == "highballistic") return 2f;
        if (!float.IsFinite(distance) || distance <= 0) return immediate;
        // Linked flies at half the distance per second.
        if (path == "linked") return 2f;
        float speed = (3f * MathF.Log(distance) + 2f) * path switch
        {
            "homing" => 2f,
            "accelerating" => 1.5f,
            "bounce" => 0.8f,
            _ => 1f,
        };
        if (!(speed > 0)) return immediate;
        float ms = MathF.Floor(distance / speed * 1000f);
        if (!float.IsFinite(ms)) return immediate;
        float seconds = ms / 1000f;
        return path == "spiral" ? seconds + 2.5f : seconds;
    }

    /// <summary>Existing script/MIRV logarithmic timing, separate from spell path factors.</summary>
    public static float ProjectileFlightTime(float distance)
    {
        if (distance <= 1f) return 0.1f;
        return Math.Max(0.1f, distance / (3f * MathF.Log(distance) + 2f));
    }
}
