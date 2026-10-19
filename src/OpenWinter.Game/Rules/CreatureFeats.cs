using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>Feats: ownership (incl. bonus feats from items and effects) and uses per day.</summary>
public static partial class CreatureRules
{
    /// <summary>Feats granted by equipped items (ITEM_PROPERTY_BONUS_FEAT) and EffectBonusFeat.</summary>
    public static IEnumerable<int> BonusFeats(World w, Creature c)
    {
        foreach (var e in Effects(c))
            if (e.Type == NwRules.EffectBonusFeat) yield return e.Ints[0];
        var table = w.Resources.TwoDA("iprp_feats");
        foreach (var (_, _, p) in EquippedProperties(c))
            if (p.Type == NwRules.IpBonusFeat && table.GetInt(p.SubType, "FeatIndex", -1) is int f && f >= 0) yield return f;
    }

    /// <summary>Every feat the creature has, own and bonus, without duplicates.</summary>
    public static IEnumerable<int> AllFeats(World w, Creature c)
    {
        var seen = new HashSet<int>();
        foreach (var f in c.Feats) if (seen.Add(f)) yield return f;
        foreach (var f in BonusFeats(w, c)) if (seen.Add(f)) yield return f;
    }

    // Stock UTCs can store only the highest rank. Missing intermediate ranks do not
    // break SUCCESSOR inheritance, and every alias consumes the highest rank's pool.
    static int HighestOwnedFeat(World w, Creature c, int feat)
    {
        var table = w.Resources.TwoDA("feat");
        HashSet<int>? bonus = null;
        int highest = -1;
        for (int rank = feat, guard = 0; rank >= 0 && guard < table.RowCount; guard++)
        {
            if (c.Feats.Contains(rank) || (bonus ??= BonusFeats(w, c).ToHashSet()).Contains(rank)) highest = rank;
            rank = table.GetInt(rank, "SUCCESSOR", -1);
        }
        return highest;
    }

    public static bool OwnsFeat(World w, Creature c, int feat) => HighestOwnedFeat(w, c, feat) >= 0;

    /// <summary>GetHasFeat: owns the feat and, unless <paramref name="ignoreUses"/>, has a use left.</summary>
    public static bool HasFeat(World w, Creature c, int feat, bool ignoreUses = false)
    {
        if (!OwnsFeat(w, c, feat)) return false;
        if (ignoreUses) return true;
        return RemainingFeatUses(w, c, feat) > 0;
    }

    /// <summary>
    /// Uses per day of a feat, or -1 for unlimited. feat.2da USESPERDAY, following the SUCCESSOR
    /// chain to the highest successor the creature has; engine-computed uses for turn undead,
    /// stunning fist and bard song.
    /// </summary>
    public static int MaxFeatUses(World w, Creature c, int feat)
    {
        var feats = w.Resources.TwoDA("feat");
        int f = HighestOwnedFeat(w, c, feat);
        if (f < 0) return 0;
        // A rank supplied only by EffectBonusFeat/item bonus feats has unlimited uses.
        if (!c.Feats.Contains(f)) return -1;
        var raw = feats.Get(f, "USESPERDAY");
        if (raw.Length == 0) return -1;
        int uses = feats.GetInt(f, "USESPERDAY");
        int family = HighestOwnedFeat(w, c, NwRules.FeatBardSongs) == f ? NwRules.FeatBardSongs : feat;
        switch (family)
        {
            case NwRules.FeatTurnUndead:
                uses = Ruleset.Int(w, "TURN_UNDEAD_BASE_USES_PER_DAY", uses) + AbilityModifier(w, c, NwRules.AbilityCha);
                if (OwnsFeat(w, c, NwRules.FeatExtraTurning)) uses += Ruleset.Int(w, "FEAT_EXTRA_TURNING_BONUS_TURNS", 6);
                break;
            case NwRules.FeatStunningFist:
            {
                int monk = ClassLevel(c, NwRules.ClassMonk);
                uses = monk > 0 ? monk + (c.TotalLevel - monk) / 4 : c.TotalLevel / 4;
                if (OwnsFeat(w, c, NwRules.FeatExtraStunning)) uses += Ruleset.Int(w, "FEAT_EXTRA_STUNNING_ATTACK_USES", 3);
                uses = Math.Max(1, uses);
                break;
            }
            case NwRules.FeatBardSongs:
                if (OwnsFeat(w, c, NwRules.FeatExtraMusic)) uses += Ruleset.Int(w, "EXTRA_MUSIC_BONUS_USES", 4);
                break;
        }
        if (uses < 0) return -1;
        return Math.Min(uses, 100);
    }

    /// <summary>Remaining uses: int.MaxValue for unlimited feats, 0 when the creature lacks the feat.</summary>
    public static int RemainingFeatUses(World w, Creature c, int feat)
    {
        EnsureInit(w, c);
        if (!OwnsFeat(w, c, feat)) return 0;
        int max = MaxFeatUses(w, c, feat);
        if (max < 0) return int.MaxValue;
        int rank = HighestOwnedFeat(w, c, feat);
        return c.FeatUses.TryGetValue(rank, out int left) ? Math.Clamp(left, 0, max) : max;
    }

    public static void AdjustFeatUses(World w, Creature c, int feat, int delta)
    {
        EnsureInit(w, c);
        int rank = HighestOwnedFeat(w, c, feat);
        if (rank < 0) return;
        // Native IncrementRemainingFeatUses requires the exact feat to be granted;
        // decrement accepts inherited aliases, as actual ActionUseFeat does.
        if (delta > 0 && !c.Feats.Contains(feat) && !BonusFeats(w, c).Contains(feat)) return;
        int max = MaxFeatUses(w, c, feat);
        if (max < 0) return;
        int left = c.FeatUses.TryGetValue(rank, out int l) ? Math.Clamp(l, 0, max) : max;
        c.FeatUses[rank] = Math.Clamp(left + delta, 0, max);
    }

    /// <summary>Restores every limited feat to its maximum uses.</summary>
    public static void RestoreFeatUses(World w, Creature c)
    {
        EnsureInit(w, c);
        c.FeatUses.Clear();
    }
}
