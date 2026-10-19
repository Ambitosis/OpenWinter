namespace OpenWinter.Game.Rules;

/// <summary>
/// The order of an object's effect list. The original keeps effects grouped by kind (GetFirstEffect /
/// GetNextEffect walk them in this order whatever order they were applied in), and in application order
/// within a kind. The order was measured on the original server (parity suite fx_order); the few kinds
/// it couldn't place (poison, disease, time stop, swarm, ...) sit next to their likeliest neighbours.
/// </summary>
public static class EffectOrder
{
    /// <summary>EFFECT_TYPE_* values from first to last. Values on one line share a rank.</summary>
    static readonly int[][] Groups =
    {
        new[] { 36 },                                         // haste
        new[] { 1 },                                          // damage resistance
        new[] { 37 },                                         // slow
        new[] { 32 },                                         // disease
        new[] { 3 },                                          // regenerate
        new[] { 23, 24, 25, 26, 27, 28, 29, 30, 35, 79, 80, 92 }, // states: charm ... petrify, cutscene paralyze/dominated
        new[] { 40 }, new[] { 41 },                           // attack
        new[] { 7 },                                          // damage reduction
        new[] { 42 }, new[] { 43 },                           // damage bonus
        new[] { 9 },                                          // temporary hit points
        new[] { 44 }, new[] { 45 },                           // damage immunity
        new[] { 11 },                                         // entangle
        new[] { 94 },                                         // death
        new[] { 98 },                                         // knockdown
        new[] { 13 },                                         // deaf
        new[] { 15 },                                         // immunity
        new[] { 17 },                                         // enemy attack bonus
        new[] { 18 },                                         // arcane spell failure
        new[] { 50 }, new[] { 51 },                           // saving throws
        new[] { 48 }, new[] { 49 },                           // movement speed
        new[] { 74 },                                         // visual effect
        new[] { 20 },                                         // area of effect
        new[] { 21 },                                         // beam
        new[] { 52 }, new[] { 53 },                           // spell resistance
        new[] { 31 },                                         // poison
        new[] { 38 }, new[] { 39 },                           // ability
        new[] { 99 },                                         // modify attacks
        new[] { 33 },                                         // curse
        new[] { 34 },                                         // silence
        new[] { 56, 57 },                                     // invisibility
        new[] { 46 }, new[] { 47 },                           // armour class
        new[] { 73 },                                         // spell immunity
        new[] { 54 }, new[] { 55 },                           // skills
        new[] { 97 },                                         // hit point change when dying
        new[] { 90 },                                         // force walk
        new[] { 60 },                                         // damage shield
        new[] { 62 },                                         // polymorph
        new[] { 63, 81 },                                     // sanctuary, ethereal
        new[] { 66 },                                         // time stop
        new[] { 68 },                                         // spell level absorption
        new[] { 86 },                                         // icon
        new[] { 65 },                                         // see invisible
        new[] { 70 },                                         // ultravision
        new[] { 64 },                                         // true seeing
        new[] { 67 },                                         // blindness
        new[] { 58 },                                         // darkness
        new[] { 71 },                                         // miss chance
        new[] { 72 },                                         // concealment
        new[] { 78 },                                         // turn resistance increase
        new[] { 75 }, new[] { 95 }, new[] { 91 },             // disappear/appear
        new[] { 61 },                                         // negative level
        new[] { 88 },                                         // bonus feat
        new[] { 76 },                                         // swarm
        new[] { 77 },                                         // turn resistance decrease
        new[] { 82 },                                         // spell failure
        new[] { 83 },                                         // cutscene ghost
        new[] { 84 },                                         // cutscene immobilize
        new[] { 85 },                                         // run script
        new[] { 87 },                                         // pacify
        new[] { 89 },                                         // time stop immunity
    };

    static readonly Dictionary<int, int> Ranks = Build();

    static Dictionary<int, int> Build()
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < Groups.Length; i++)
            foreach (int t in Groups[i]) map[t] = i;
        return map;
    }

    /// <summary>Where an effect type sorts; kinds not in the table go last.</summary>
    public static int Rank(int effectType) => Ranks.TryGetValue(effectType, out int r) ? r : Groups.Length;

    /// <summary>Index to insert an effect of <paramref name="effectType"/> at: after everything of its rank or lower.</summary>
    public static int InsertAt(IReadOnlyList<Scripting.Effect> list, int effectType)
    {
        int rank = Rank(effectType);
        int i = list.Count;
        while (i > 0 && Rank(list[i - 1].Type) > rank) i--;
        return i;
    }
}
