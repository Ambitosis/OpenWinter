using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Combat;

/// <summary>
/// Sight for combat purposes: whether an attacker can see its target (concealment from invisibility
/// and the target's darkness) and whether a defender can see its attacker (denied Dexterity, +2 to hit).
/// Hiding creatures count as unseen until the observer's perception has noticed them.
/// </summary>
public static class CombatVision
{
    public static bool Has(GameObject o, int effectType)
    {
        foreach (var e in o.Effects) if (e.Type == effectType) return true;
        return false;
    }

    /// <param name="darkness">Whether darkness on the target hides it (it does for concealment, not for the +2 of an unseen attacker).</param>
    public static bool CanSee(World world, Creature observer, GameObject target, bool darkness = true)
    {
        if (target is not Creature t) return !Has(observer, CombatK.EffectBlindness);
        if (Has(observer, CombatK.EffectBlindness)) return false;
        bool trueSeeing = Has(observer, CombatK.EffectTrueSeeing);
        bool seeInvisible = trueSeeing || Has(observer, CombatK.EffectSeeInvisible);
        bool ultravision = trueSeeing || Has(observer, CombatK.EffectUltravision);
        // (Darkness on the observer itself doesn't stop it fighting, as on nwserver.)
        foreach (var e in t.Effects)
        {
            switch (e.Type)
            {
                case CombatK.EffectInvisibility or CombatK.EffectImprovedInvisibility:
                {
                    int kind = e.Ints[0] != 0 ? e.Ints[0] : e.Type == CombatK.EffectImprovedInvisibility ? CombatK.InvisibilityImproved : CombatK.InvisibilityNormal;
                    if (kind == CombatK.InvisibilityDarkness ? !ultravision : !seeInvisible) return false;
                    break;
                }
                case CombatK.EffectDarkness when !ultravision && darkness:
                    return false;
                case CombatK.EffectEthereal:
                    return false;
            }
        }
        // A hiding creature is unseen until perception has spotted it.
        if (t.StealthMode && !trueSeeing && !observer.Society.Seen.Contains(t.Id)) return false;
        return true;
    }

    /// <summary>A helpless creature: paralysed, stunned, asleep, petrified, frozen in time.</summary>
    public static bool Helpless(GameObject o)
    {
        foreach (var e in o.Effects)
            if (e.Type is CombatK.EffectParalyze or CombatK.EffectStunned or CombatK.EffectSleep or CombatK.EffectPetrify
                or CombatK.EffectTimeStop or CombatK.EffectCutsceneParalyze) return true;
        return false;
    }

    /// <summary>Has any of the Uncanny Dodge feats (keeps Dexterity when flat-footed or against unseen attackers).</summary>
    public static bool UncannyDodge(World w, Creature c)
    {
        if (CreatureRules.HasFeat(w, c, CombatK.FeatUncannyDodge1, ignoreUses: true)) return true;
        for (int f = CombatK.FeatUncannyDodge2; f <= CombatK.FeatUncannyDodge6; f++)
            if (CreatureRules.HasFeat(w, c, f, ignoreUses: true)) return true;
        return false;
    }

    /// <summary>Uncanny Dodge 2 or better: can't be flanked (except by much stronger rogues).</summary>
    public static bool ImprovedUncannyDodge(World w, Creature c)
    {
        for (int f = CombatK.FeatUncannyDodge2; f <= CombatK.FeatUncannyDodge6; f++)
            if (CreatureRules.HasFeat(w, c, f, ignoreUses: true)) return true;
        return false;
    }
}
