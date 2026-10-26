using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// Applied effects as the original writes them in an object's state (the EffectList of ObjectToJson with
/// bSaveObjectState, StoreCampaignObject, SqlBindObject and the GIT/BIC structs of its save games), and back.
/// <para>
/// An entry carries the original's own effect number (Type), the subtype ORed with the duration type
/// (SubType: SUBTYPE_MAGICAL + DURATION_TYPE_TEMPORARY = 9), the duration in seconds, the calendar day and
/// millisecond of day it expires (ExpireDay/ExpireTime), its creator, spell, caster level (-1 when made outside a
/// spell), tag, and eight integers in the order GetEffectInteger reports them, four floats, six strings, four
/// objects and two vectors. Measured with parity suite persist_effects (80 effect constructors).
/// </para>
/// <para>
/// OpenWinter keeps an effect's exact state beside the native fields ("OWEffect", the save game's reflection
/// encoding), so its own data reads back unchanged. Entries without it (the original's data) are mapped from the
/// measured table below; effects of types not in it are skipped and reported.
/// </para>
/// </summary>
public static class NativeEffects
{
    public const string OpenWinterField = "OWEffect";

    /// <summary>
    /// EFFECT_TYPE_* name to the original's effect number. State effects all share number 8 and are told apart by
    /// their first integer (the state code). Sanctuary and ethereal share 63; ethereal has no difficulty class.
    /// </summary>
    static readonly (string Name, int Native, int State)[] Table =
    {
        ("HASTE", 1, -1), ("DAMAGE_RESISTANCE", 2, -1), ("SLOW", 3, -1), ("DISEASE", 5, -1), ("REGENERATE", 7, -1),
        ("CHARMED", 8, 1), ("CONFUSED", 8, 2), ("FRIGHTENED", 8, 3), ("TURNED", 8, 4), ("DAZED", 8, 5), ("STUNNED", 8, 6),
        ("DOMINATED", 8, 7), ("PARALYZE", 8, 8), ("SLEEP", 8, 9), ("PETRIFY", 8, 15), ("CUTSCENE_PARALYZE", 8, 20),
        ("ATTACK_INCREASE", 10, -1), ("ATTACK_DECREASE", 11, -1), ("DAMAGE_REDUCTION", 12, -1), ("DAMAGE_INCREASE", 13, -1),
        ("DAMAGE_DECREASE", 14, -1), ("TEMPORARY_HITPOINTS", 15, -1), ("DAMAGE_IMMUNITY_INCREASE", 16, -1),
        ("DAMAGE_IMMUNITY_DECREASE", 17, -1), ("ENTANGLE", 18, -1), ("DEAF", 21, -1), ("IMMUNITY", 22, -1),
        ("ENEMY_ATTACK_BONUS", 24, -1), ("SAVING_THROW_INCREASE", 26, -1), ("SAVING_THROW_DECREASE", 27, -1),
        ("MOVEMENT_SPEED_INCREASE", 28, -1), ("MOVEMENT_SPEED_DECREASE", 29, -1), ("VISUALEFFECT", 30, -1), ("BEAM", 32, -1),
        ("SPELL_RESISTANCE_INCREASE", 33, -1), ("SPELL_RESISTANCE_DECREASE", 34, -1), ("ABILITY_INCREASE", 36, -1),
        ("ABILITY_DECREASE", 37, -1), ("MODIFY_ATTACKS", 44, -1), ("CURSE", 45, -1), ("SILENCE", 46, -1), ("INVISIBILITY", 47, -1),
        ("AC_INCREASE", 48, -1), ("AC_DECREASE", 49, -1), ("SPELL_IMMUNITY", 50, -1), ("SKILL_INCREASE", 55, -1),
        ("SKILL_DECREASE", 56, -1), ("FORCE_WALK", 59, -1), ("ELEMENTALSHIELD", 61, -1), ("SANCTUARY", 63, -1),
        ("ETHEREAL", 63, -1), ("SPELLLEVELABSORPTION", 65, -1), ("ICON", 67, -1), ("SEEINVISIBLE", 70, -1),
        ("ULTRAVISION", 71, -1), ("TRUESEEING", 72, -1), ("BLINDNESS", 73, -1), ("DARKNESS", 74, -1), ("MISS_CHANCE", 75, -1),
        ("CONCEALMENT", 76, -1), ("TURN_RESISTANCE_INCREASE", 77, -1), ("POISON", 35, -1), ("NEGATIVELEVEL", 82, -1), ("BONUS_FEAT", 83, -1),
        ("TURN_RESISTANCE_DECREASE", 88, -1), ("SPELL_FAILURE", 92, -1), ("CUTSCENEGHOST", 93, -1), ("CUTSCENEIMMOBILIZE", 94, -1),
        ("RUNSCRIPT", 96, -1), ("PACIFY", 97, -1), ("TIMESTOP_IMMUNITY", 98, -1),
        // Instant and engine-driven kinds, measured through NWNX_Effect_UnpackEffect (parity suite nwnx_effect).
        ("RESURRECTION", 4, -1), ("SUMMON_CREATURE", 6, -1), ("DEATH", 19, -1), ("KNOCKDOWN", 20, -1),
        ("AREA_OF_EFFECT", 31, -1), ("DAMAGE", 38, -1), ("HEAL", 39, -1), ("DISPELMAGICALL", 51, -1),
        ("DISPELMAGICBEST", 52, -1), ("HITPOINTCHANGEWHENDYING", 57, -1), ("POLYMORPH", 62, -1), ("TIMESTOP", 64, -1),
        ("DISAPPEARAPPEAR", 79, -1), ("DISAPPEAR", 80, -1), ("APPEAR", 81, -1), ("SWARM", 85, -1),
        ("ARCANE_SPELL_FAILURE", 25, -1),
    };

    /// <summary>The original's effect number for an effect (0 when not measured) and the state code (-1 for none).</summary>
    public static (int Native, int State) NativeType(EffectConstants k, Effect e)
    {
        foreach (var (name, native, state) in Table)
            if (k["EFFECT_TYPE_" + name] == e.Type) return (native, state);
        return (0, -1);
    }

    static int ScriptType(EffectConstants k, int native, int[] ints)
    {
        if (native == 8) return Table.FirstOrDefault(t => t.Native == 8 && t.State == ints[0]) is { Name: { } n } ? k["EFFECT_TYPE_" + n] : 0;
        if (native == 63) return k[ints[5] == 1 ? "EFFECT_TYPE_SANCTUARY" : "EFFECT_TYPE_ETHEREAL"];
        return Table.FirstOrDefault(t => t.Native == native) is { Name: { } name } ? k["EFFECT_TYPE_" + name] : 0;
    }

    // ---------------------------------------------------------------- writing

    /// <summary>An EffectList entry for an applied effect.</summary>
    public static GffStruct Write(World world, Effect e)
    {
        var k = EffectConstants.Of(world);
        var (native, state) = NativeType(k, e);
        int[] ints = state >= 0 ? new[] { state, 0, 0, 0, 0, 0, 0, 0 } : EffectFunctions.ReportedInts(k, e);
        var floats = (float[])e.Floats.Clone();
        var clock = world.Clock;
        long dayMs = 24 * clock.HourMs;
        (uint Day, uint Time) Calendar(double worldTime)
        {
            long at = clock.TotalMs + (long)Math.Round(Math.Max(0, worldTime - world.Time) * 1000);
            return ((uint)(at / dayMs), (uint)(at % dayMs));
        }
        uint day = 0, time = 0;
        if (e.DurationType == k.Temporary && e.ExpiresAt > 0) (day, time) = Calendar(e.ExpiresAt);
        // Interval effects keep the interval in milliseconds and the calendar time of their next tick in integers.
        if (e.Type == k.Regenerate || e.Type == k.RunScript)
        {
            float interval = e.Floats[0];
            var (tickDay, tickTime) = Calendar(e.NextTick > 0 ? e.NextTick : e.AppliedAt + interval);
            if (e.Type == k.RunScript) { ints[0] = (int)MathF.Round(interval * 1000); ints[1] = 0; }
            ints[2] = (int)tickDay;
            ints[3] = (int)tickTime;
            floats[0] = 0;
        }
        var s = new GffStruct(2)
            .Set("Id", GffType.DWord64, (ulong)Math.Max(0, e.UniqueId))
            .SetWord("Type", native)
            .SetWord("SubType", (e.SubType & ~7) | (e.DurationType & 7))
            .SetFloat("Duration", e.DurationType == k.Temporary ? e.Duration : 0f)
            .SetDWord("ExpireDay", day)
            .SetDWord("ExpireTime", time)
            .SetDWord("CreatorId", e.Creator)
            .SetDWord("SpellId", unchecked((uint)e.SpellId))
            .SetInt("IsExposed", e.Hidden ? 0 : 1)
            .SetInt("IsIconShown", e.Hidden ? 0 : 1)
            .SetByte("SkipOnLoad", 0)
            .SetDWord("CasterLevel", e.CasterLevel > 0 ? (uint)e.CasterLevel : 0xFFFFFFFF)
            .SetString("CustomTag", e.Tag)
            .SetInt("IgnoreImmunity", e.IgnoreImmunity ? 1 : 0)
            .SetInt("NumIntegers", 8);
        s.SetList("IntList", ints.Select(v => new GffStruct(3).SetInt("Value", v)).ToList());
        s.SetList("FloatList", Enumerable.Range(0, 4).Select(i => new GffStruct(4).SetFloat("Value", i < floats.Length ? floats[i] : 0f)).ToList());
        s.SetList("StringList", Enumerable.Range(0, 6).Select(i => new GffStruct(5).SetString("Value", i < e.Strings.Length ? e.Strings[i] ?? "" : "")).ToList());
        s.SetList("ObjectList", Enumerable.Range(0, 4).Select(i => new GffStruct(6).SetDWord("Value", i < e.Objects.Length ? e.Objects[i] : GameObject.InvalidId)).ToList());
        s.SetList("VectorList", Enumerable.Range(0, 2).Select(i =>
        {
            var v = i < e.Vectors.Length ? e.Vectors[i] : Vector3.Zero;
            return new GffStruct(7).SetFloat("ValueX", v.X).SetFloat("ValueY", v.Y).SetFloat("ValueZ", v.Z);
        }).ToList());
        if (DetachedState.Write(world, e, typeof(Effect)) is { } exact) s.SetString(OpenWinterField, exact);
        return s;
    }

    // ---------------------------------------------------------------- reading

    /// <summary>
    /// The effect an EffectList entry describes, with the seconds it has left (0 for permanent effects), or null
    /// when it can't be read. Entries the original rebuilds itself when it loads (SkipOnLoad: equipment) are skipped.
    /// </summary>
    /// <param name="map">Translates the object ids of the original's data (creator, object parameters) to this world's.</param>
    public static Effect? Read(World world, GffStruct s, out float remaining, ICollection<string>? warnings = null, Func<uint, uint>? map = null)
    {
        remaining = 0;
        if (s.GetInt("SkipOnLoad") != 0) return null;
        var k = EffectConstants.Of(world);
        var clock = world.Clock;
        long dayMs = 24 * clock.HourMs;
        long expiresMs = (long)s.GetInt("ExpireDay") * dayMs + s.GetInt("ExpireTime");
        if (s.GetString(OpenWinterField) is { Length: > 0 } exact && DetachedState.Read<Effect>(world, exact, warnings) is { } own)
        {
            own.Target = GameObject.InvalidId;
            if (own.DurationType == k.Temporary) remaining = expiresMs > 0 ? Math.Max(0, (expiresMs - clock.TotalMs) / 1000f) : own.Duration;
            return own;
        }

        int native = (int)s.GetInt("Type");
        var ints = new int[8];
        var intList = s.GetList("IntList");
        for (int i = 0; i < 8 && i < intList.Count; i++) ints[i] = (int)intList[i].GetInt("Value");
        int type = ScriptType(k, native, ints);
        if (type == 0)
        {
            warnings?.Add($"effect type {native} of the original is not mapped yet; skipped");
            return null;
        }
        int subType = (int)s.GetInt("SubType");
        var e = new Effect
        {
            Type = type,
            SubType = subType & ~7,
            DurationType = subType & 7,
            Duration = s.GetFloat("Duration"),
            Creator = (uint)s.GetInt("CreatorId", GameObject.InvalidId),
            SpellId = unchecked((int)(uint)s.GetInt("SpellId", 0xFFFFFFFF)),
            Tag = s.GetString("CustomTag"),
            IgnoreImmunity = s.GetInt("IgnoreImmunity") != 0,
            UniqueId = (long)Math.Min(long.MaxValue, (ulong)s.GetInt("Id")),
        };
        long cl = s.GetInt("CasterLevel");
        e.CasterLevel = cl is > 0 and < 0xFFFF ? (int)cl : 0;
        var floats = s.GetList("FloatList");
        for (int i = 0; i < e.Floats.Length && i < floats.Count; i++) e.Floats[i] = floats[i].GetFloat("Value");
        var strings = s.GetList("StringList");
        for (int i = 0; i < e.Strings.Length && i < strings.Count; i++) e.Strings[i] = strings[i].GetString("Value");
        var objects = s.GetList("ObjectList");
        for (int i = 0; i < e.Objects.Length && i < objects.Count; i++) e.Objects[i] = (uint)objects[i].GetInt("Value", GameObject.InvalidId);
        var vectors = s.GetList("VectorList");
        for (int i = 0; i < e.Vectors.Length && i < vectors.Count; i++)
            e.Vectors[i] = new Vector3(vectors[i].GetFloat("ValueX"), vectors[i].GetFloat("ValueY"), vectors[i].GetFloat("ValueZ"));
        if (map != null)
        {
            e.Creator = map(e.Creator);
            for (int i = 0; i < e.Objects.Length; i++) e.Objects[i] = map(e.Objects[i]);
        }
        Unreport(k, e, ints);
        if (e.DurationType == k.Temporary) remaining = expiresMs > 0 ? Math.Max(0, (expiresMs - clock.TotalMs) / 1000f) : e.Duration;
        return e;
    }

    /// <summary>The inverse of <see cref="EffectFunctions.ReportedInts(EffectConstants, Effect)"/>: constructor order and versus filters.</summary>
    static void Unreport(EffectConstants k, Effect e, int[] n)
    {
        var i = e.Ints;
        void Versus(int at)
        {
            e.VersusRace = n[at] == k.RacialInvalid ? -1 : n[at];
            e.VersusLawChaos = n[at + 1];
            int ge = n[at + 2];
            e.VersusGoodEvil = ge == 0 ? 0 : ge == k["ALIGNMENT_NEUTRAL"] ? 1 : ge == k["ALIGNMENT_GOOD"] ? 2 : ge == k["ALIGNMENT_EVIL"] ? 3 : 0;
        }
        int Miss(int t) => t == 0 ? k["MISS_CHANCE_TYPE_NORMAL"] : t - 1;
        switch (e.Type)
        {
            case 46 or 47: i[0] = n[1]; i[1] = n[0]; i[2] = n[5]; Versus(2); break;
            case 50 or 51: i[0] = n[1]; i[1] = n[0]; i[2] = n[2]; Versus(3); break;
            case 40 or 41 or 42 or 43 or 54 or 55: i[0] = n[0]; i[1] = n[1]; Versus(2); break;
            case 15 or 56 or 57: i[0] = n[0]; Versus(1); break;
            case 72: i[0] = n[0]; i[1] = Miss(n[4]); Versus(1); break;
            case 71: i[0] = n[0]; i[1] = Miss(n[1]); break;
            case 63: i[0] = n[4]; Versus(1); break;
            case 81: Versus(1); break;
            case 44 or 45: i[0] = n[0]; i[1] = n[1]; break;
            case 48: i[0] = n[0] is >= 100 and < 200 ? n[0] - 100 : n[0]; break;
            case 3: i[0] = n[0]; e.Floats[0] = n[1] / 1000f; break;
            case 62: i[0] = n[0]; i[5] = n[1]; i[1] = n[2]; i[2] = n[4]; i[3] = n[6]; i[4] = n[7]; break;
            case 100: i[0] = n[0]; i[1] = n[1]; i[2] = n[3]; break;
            case 93:
            {
                // Damage: the amount under each damage type bit it deals.
                int bits = 0, amount = 0;
                for (int b = 0; b < 8; b++) if (n[b] != -1) { bits |= 1 << b; amount = n[b]; }
                i[0] = amount; i[1] = bits;
                break;
            }
            case 85: e.Floats[0] = n[0] / 1000f; break;
            case 61: i[0] = n[0]; break;
            case 74: i[0] = n[0]; i[1] = n[2]; break;
            case 68: i[0] = n[0]; i[1] = n[1]; i[2] = n[2]; break;
            case 27 or 29 or 30 or 25 or 23 or 28 or 24 or 79 or 80 or 35 or 26: break; // the state code is the type
            default: Array.Copy(n, i, Math.Min(n.Length, i.Length)); break;
        }
    }
}
