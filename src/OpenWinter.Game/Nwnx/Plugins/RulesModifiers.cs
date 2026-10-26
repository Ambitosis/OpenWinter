using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// The effects NWNX_Feat and NWNX_Race give creatures (parity suites nwnx_feat and nwnx_race). Each modifier that
/// works through an effect becomes a supernatural innate effect made by the module, out of the scripts' effect list
/// and untouched by rest, dispelling and death, tagged "NWNX_Feat_FeatMod_&lt;feat&gt;" or "NWNX_Race_RacialMod".
/// </summary>
public static class RulesModifiers
{
    /// <summary>Unset optional parameter (the headers' default).</summary>
    public const int Unset = unchecked((int)0xDEADBEEF);
    const int NoRace = 28; // RACIAL_TYPE_INVALID as the effects store it
    const int AllPhysical = 4103; // AC versus every damage type

    public sealed record Modifier(int Type, int P1, int P2, int P3, int P4)
    {
        public int Get(int i, int fallback = 0) => (i switch { 1 => P1, 2 => P2, 3 => P3, _ => P4 }) is var v && v != Unset ? v : fallback;
    }

    sealed class Tracked
    {
        public HashSet<int>? Feats;
        public bool RaceApplied, RaceSuppressed, PlayerSeen;
        public int Level = -1;
    }

    sealed class Data
    {
        public readonly Dictionary<int, List<Modifier>> Feats = new();
        public readonly Dictionary<int, Dictionary<string, Modifier>> Races = new();
        public readonly Dictionary<int, int> FavoredEnemyFeats = new();
        public readonly Dictionary<uint, Tracked> Creatures = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, Data> _data = new();

    /// <summary>The plugins' tables, made on first use: creatures that exist by then keep what they have.</summary>
    static Data Of(NwnxCall n) => _data.GetValue(n.Registry, _ =>
    {
        var d = new Data();
        foreach (var c in n.World.AllObjects.OfType<Creature>().Where(c => !c.IsDestroyed))
            d.Creatures[c.Id] = new Tracked { Feats = c.Feats.ToHashSet(), Level = c.TotalLevel, PlayerSeen = c.IsPC };
        return d;
    });
    static Data? Of(World w) => w.Systems.OfType<NwnxRegistry>().FirstOrDefault() is { } r && _data.TryGetValue(r, out var d) ? d : null;

    /// <summary>Follows creatures as they appear and change, once the plugins have modifiers.</summary>
    public static void Attach(World world, NwnxRegistry registry)
    {
        world.ObjectAdded += o => { if (o is Creature c) Changed(world, registry, c); };
        world.ObjectChanged += o => { if (o is Creature c) Changed(world, registry, c); };
        world.ObjectRemoved += o => { if (o.IsDestroyed && _data.TryGetValue(registry, out var d)) d.Creatures.Remove(o.Id); };
        registry.Called += (call, subject) => { if (subject is Creature c) Changed(world, registry, c); };
    }

    static void Changed(World w, NwnxRegistry r, Creature c)
    {
        if (!_data.TryGetValue(r, out var d) || c.IsDestroyed) return;
        if (!d.Creatures.TryGetValue(c.Id, out var t)) d.Creatures[c.Id] = t = new Tracked();
        // Feats gained or lost bring or take their modifiers (a new creature gains all it has).
        var feats = c.Feats.ToHashSet();
        var before = t.Feats ?? [];
        t.Feats = feats;
        foreach (int lost in before.Where(f => !feats.Contains(f))) RemoveTagged(w, c, FeatTag(lost));
        foreach (int gained in feats.Where(f => !before.Contains(f)))
            if (d.Feats.TryGetValue(gained, out var mods)) ApplyAll(w, c, FeatEffects(mods), FeatTag(gained));
        // Levelling up grants the race's feats for the levels reached; players get the race's effects again.
        int level = c.TotalLevel;
        if (t.Level >= 0 && level > t.Level)
        {
            GrantRaceFeats(w, d, c, t.Level + 1, level);
            if (c.IsPC) { t.RaceSuppressed = false; ApplyRace(w, d, c, t); }
        }
        t.Level = level;
        // Players carry their race's effects from when they enter.
        if (c.IsPC && !t.PlayerSeen)
        {
            t.PlayerSeen = true;
            if (!t.RaceSuppressed) ApplyRace(w, d, c, t);
        }
    }

    static string FeatTag(int feat) => "NWNX_Feat_FeatMod_" + feat.ToString(System.Globalization.CultureInfo.InvariantCulture);
    const string RaceTag = "NWNX_Race_RacialMod";

    static void RemoveTagged(World w, Creature c, string tag)
    {
        var sys = w.System<EffectSystem>();
        foreach (var e in c.Effects.Where(e => e.Innate && e.Tag == tag).ToList()) sys.RemoveOnly(c, e);
    }

    static void ApplyAll(World w, Creature c, IEnumerable<(int Native, int[] Ints)> effects, string tag)
    {
        var sys = w.System<EffectSystem>();
        foreach (var (native, ints) in effects)
            if (Make(w, native, ints, tag) is { } e) sys.ApplyInnate(e, c);
    }

    /// <summary>An effect from the original's record of it: its number and integers.</summary>
    static Effect? Make(World w, int native, int[] ints, string tag)
    {
        var all = new int[8];
        Array.Copy(ints, all, Math.Min(8, ints.Length));
        var s = new GffStruct(2)
            .SetWord("Type", native)
            .SetWord("SubType", 16 | 2) // supernatural, lasting
            .SetDWord("CreatorId", w.Module.Id)
            .SetDWord("SpellId", 0xFFFFFFFF)
            .SetDWord("CasterLevel", 0xFFFFFFFF)
            .SetString("CustomTag", tag);
        s.SetList("IntList", all.Select(v => new GffStruct(3).SetInt("Value", v)).ToList());
        var e = NativeEffects.Read(w, s, out _);
        if (e == null) return null;
        // A slower speed is kept as a smaller factor of the same kind of effect (nwnx_race: -20 reads 80).
        if (native == 28) e.Ints[0] = ints[0] - 100;
        if (native == 30) e.Floats[0] = 1;
        e.Innate = true;
        return e;
    }

    // ---------------------------------------------------------------- feats

    /// <summary>A feat's effects, by modifier kind, the same modifier given twice counting once (nwnx_feat).</summary>
    static IEnumerable<(int, int[])> FeatEffects(IEnumerable<Modifier> mods)
    {
        foreach (var m in mods.Distinct().OrderBy(m => m.Type))
        {
            int p1 = m.Get(1), p2 = m.Get(2), p3 = m.Get(3), p4 = m.Get(4);
            switch (m.Type)
            {
                case 1: yield return Signed(10, 11, p1, v => [v, 0, NoRace]); break;                         // AB
                case 2: yield return Signed(36, 37, p2, v => [p1, v]); break;                                // ABILITY
                case 3: yield return Signed(10, 11, p2, v => [v, 0, p1]); break;                             // ABVSRACE
                case 4: yield return Signed(48, 49, p1, v => [0, v, NoRace, 0, 0, AllPhysical]); break;      // AC (dodge)
                case 5: yield return Signed(48, 49, p2, v => [0, v, p1, 0, 0, AllPhysical]); break;          // ACVSRACE
                case 6: yield return (25, [p1]); break;                                                      // ARCANESPELLFAILURE
                case 7: yield return (76, [p1, NoRace]); break;                                              // CONCEALMENT
                case 8: yield return Signed(16, 17, p2, v => [p1, v]); break;                                // DMGIMMUNITY
                case 9: yield return (12, [p1, p2]); break;                                                  // DMGREDUCTION
                case 10: yield return (2, [p1, p2]); break;                                                  // DMGRESIST
                case 11: yield return (22, [p1, NoRace]); break;                                             // IMMUNITY
                case 12: yield return (28, [100 + p1]); break;                                               // MOVEMENTSPEED
                case 13: yield return (7, [p1, p2 * 1000]); break;                                           // REGENERATION
                case 14: yield return Signed(26, 27, p2, v => [v, p1, 0, NoRace]); break;                    // SAVE
                case 15: yield return Signed(26, 27, p3, v => [v, p1, 0, p2]); break;                        // SAVEVSRACE
                case 16: yield return Signed(26, 27, p3, v => [v, p1, p2, NoRace]); break;                   // SAVEVSTYPE
                case 17: yield return Signed(26, 27, p4, v => [v, p1, p2, p3]); break;                       // SAVEVSTYPERACE
                case 18: yield return (50, [p1]); break;                                                     // SPELLIMMUNITY
                case 19: yield return (33, [p1]); break;                                                     // SRCHARGEN
                case 23: yield return (72, []); break;                                                       // TRUESEEING
                case 24: yield return (70, []); break;                                                       // SEEINVISIBLE
                case 25: yield return (71, []); break;                                                       // ULTRAVISION
                case 26: yield return (1, []); break;                                                        // HASTE
                case 27: yield return (30, [p1]); break;                                                     // VISUALEFFECT
                case 30: yield return Signed(13, 14, p2, v => [v, p1, NoRace]); break;                       // DAMAGE
            }
        }
    }

    /// <summary>An increase, or the matching decrease by the amount's size for a negative amount.</summary>
    static (int, int[]) Signed(int up, int down, int amount, Func<int, int[]> ints) => amount < 0 ? (down, ints(-amount)) : (up, ints(amount));

    [NwnxFunc("Feat", "SetFeatModifier")]
    static void SetFeatModifier(NwnxCall n)
    {
        int feat = n.PopInt(), type = n.PopInt();
        var m = new Modifier(type, n.PopInt(), n.PopInt(), n.PopInt(), n.PopInt());
        if (type <= 0) return;
        var d = Of(n);
        if (!d.Feats.TryGetValue(feat, out var list)) d.Feats[feat] = list = [];
        list.Add(m);
    }

    /// <summary>Spell save DC bonuses from the caster's feats (SPELLSAVEDC, ...FORSCHOOL, ...FORSPELL).</summary>
    public static int SpellSaveDCBonus(World w, Creature c, int spell, int school)
    {
        if (Of(w) is not { Feats.Count: > 0 } d) return 0;
        int bonus = 0;
        foreach (int feat in c.Feats)
            if (d.Feats.TryGetValue(feat, out var mods))
                foreach (var m in mods)
                    bonus += m.Type switch
                    {
                        21 => m.Get(1),
                        28 when m.Get(1) == school => m.Get(2),
                        29 when m.Get(1) == spell => m.Get(2),
                        _ => 0,
                    };
        return bonus;
    }

    // ---------------------------------------------------------------- races

    /// <summary>
    /// The key a racial modifier is kept under: one each of AB, AC, concealment, speed, regeneration, reduction,
    /// spell resistance and parent race; one per damage type, save, versus race or skill; every feat, immunity and
    /// spell immunity (nwnx_race: a second AB or AC replaces the first, a second resistance or save adds to it).
    /// </summary>
    static string RaceKey(Modifier m) => m.Type switch
    {
        1 or 3 or 5 or 7 or 12 or 13 or 14 or 15 or 21 or 22 => m.Type.ToString(System.Globalization.CultureInfo.InvariantCulture),
        2 or 4 or 6 or 8 or 16 or 19 => $"{m.Type}:{m.Get(1)}",
        10 => $"{m.Type}:{m.Get(1)}",
        17 or 18 => $"{m.Type}:{m.Get(1)}:{m.Get(2)}",
        _ => $"{m.Type}:{m.P1}:{m.P2}:{m.P3}",
    };

    [NwnxFunc("Race", "SetRacialModifier")]
    static void SetRacialModifier(NwnxCall n)
    {
        int race = n.PopInt(), type = n.PopInt();
        var m = new Modifier(type, n.PopInt(), n.PopInt(), n.PopInt(), Unset);
        if (type <= 0) return;
        var d = Of(n);
        if (!d.Races.TryGetValue(race, out var map)) d.Races[race] = map = new();
        map[RaceKey(m)] = m;
    }

    /// <summary>The race's parent (its RACE modifier), or the race itself.</summary>
    public static int ParentRace(World w, int race) =>
        Of(w) is { } d && d.Races.TryGetValue(race, out var map) && map.TryGetValue("14", out var m) ? m.Get(1, race) : race;

    [NwnxFunc("Race", "GetParentRace")]
    static void GetParentRace(NwnxCall n) => n.Push(ParentRace(n.World, n.PopInt()));

    [NwnxFunc("Race", "SetFavoredEnemyFeat")]
    static void SetFavoredEnemyFeat(NwnxCall n)
    {
        int race = n.PopInt(), feat = n.PopInt();
        Of(n).FavoredEnemyFeats[race] = feat;
    }

    /// <summary>The favoured enemy feat SetFavoredEnemyFeat gave a race, or -1.</summary>
    public static int FavoredEnemyFeat(World w, int race) => Of(w) is { } d && d.FavoredEnemyFeats.TryGetValue(race, out int f) ? f : -1;

    [NwnxFunc("Race", "SuppressCreatureRaceEffects")]
    static void Suppress(NwnxCall n)
    {
        if (n.PopObject<Creature>() is not { } c) return;
        var d = Of(n);
        var t = Track(d, c);
        RemoveTagged(n.World, c, RaceTag);
        t.RaceApplied = false;
        t.RaceSuppressed = true;
    }

    /// <summary>Gives the creature its race's effects afresh, and the race's feats for levels it has reached.</summary>
    [NwnxFunc("Race", "ReactivateCreatureRaceEffects")]
    static void Reactivate(NwnxCall n)
    {
        if (n.PopObject<Creature>() is not { } c) return;
        var d = Of(n);
        var t = Track(d, c);
        t.RaceSuppressed = false;
        GrantRaceFeats(n.World, d, c, 1, c.TotalLevel);
        ApplyRace(n.World, d, c, t);
    }

    static Tracked Track(Data d, Creature c)
    {
        if (!d.Creatures.TryGetValue(c.Id, out var t))
            d.Creatures[c.Id] = t = new Tracked { Feats = c.Feats.ToHashSet(), Level = c.TotalLevel, PlayerSeen = c.IsPC };
        return t;
    }

    static void ApplyRace(World w, Data d, Creature c, Tracked t)
    {
        RemoveTagged(w, c, RaceTag);
        t.RaceApplied = true;
        if (!d.Races.TryGetValue(c.Race, out var map)) return;
        // By modifier kind; resistances and immunities to damage types by damage type (nwnx_race).
        var ordered = map.Values.OrderBy(m => m.Type).ThenBy(m => m.Type is 6 or 8 ? m.Get(1) : 0);
        ApplyAll(w, c, RaceEffects(ordered, c.TotalLevel), RaceTag);
    }

    static void GrantRaceFeats(World w, Data d, Creature c, int fromLevel, int toLevel)
    {
        if (!d.Races.TryGetValue(c.Race, out var map)) return;
        bool changed = false;
        foreach (var m in map.Values.Where(m => m.Type == 9 && m.Get(2) >= fromLevel && m.Get(2) <= toLevel))
            if (!c.Feats.Contains(m.Get(1))) { c.Feats.Add(m.Get(1)); changed = true; }
        if (changed) w.MarkChanged(c);
    }

    static IEnumerable<(int, int[])> RaceEffects(IEnumerable<Modifier> mods, int level)
    {
        int sr = 0, srMax = int.MaxValue;
        bool hasSr = false;
        foreach (var m in mods)
        {
            int p1 = m.Get(1), p2 = m.Get(2), p3 = m.Get(3);
            switch (m.Type)
            {
                case 1: yield return Signed(10, 11, p1, v => [v, 0, NoRace]); break;                         // AB
                case 2: yield return Signed(10, 11, p2, v => [v, 0, p1]); break;                             // ABVSRACE
                case 3: yield return Signed(48, 49, p1, v => [p2, v, NoRace, 0, 0, AllPhysical]); break;     // AC (with its type)
                case 4: yield return Signed(48, 49, p2, v => [0, v, p1, 0, 0, AllPhysical]); break;          // ACVSRACE
                case 5: yield return (76, [p1, NoRace]); break;                                              // CONCEALMENT
                case 6: yield return Signed(16, 17, p2, v => [p1, v]); break;                                // DMGIMMUNITY
                case 7: yield return (12, [p1, p2]); break;                                                  // DMGREDUCTION
                case 8: yield return (2, [p1, p2]); break;                                                   // DMGRESIST
                case 11: yield return (22, [p1, NoRace]); break;                                             // IMMUNITY
                case 13: yield return (28, [100 + p1]); break;                                               // MOVEMENTSPEED
                case 15: yield return (7, [p1, p2 * 1000]); break;                                           // REGENERATION
                case 16: yield return Signed(26, 27, p2, v => [v, p1, 0, NoRace]); break;                    // SAVE
                case 17: yield return Signed(26, 27, p3, v => [v, p1, 0, p2]); break;                        // SAVEVSRACE
                case 18: yield return Signed(26, 27, p3, v => [v, p1, p2, NoRace]); break;                   // SAVEVSTYPE
                case 20: yield return (50, [p1]); break;                                                     // SPELLIMMUNITY
                case 21: hasSr = true; sr += p1; if (m.Get(2, 0) > 0) srMax = m.Get(2); break;               // SRCHARGEN (and its cap)
                case 22:                                                                                     // SRINCLEVEL
                    hasSr = true;
                    int per = Math.Max(1, p2), start = Math.Max(1, m.Get(3, 1));
                    if (level >= start) sr += p1 * ((level - start) / per + 1);
                    break;
            }
        }
        if (hasSr) yield return (33, [Math.Min(sr, srMax)]);
    }
}
