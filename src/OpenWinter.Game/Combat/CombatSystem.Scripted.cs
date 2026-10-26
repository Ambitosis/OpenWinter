using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>Combat steps scripts can start directly (NWNX_Creature).</summary>
public sealed partial class CombatSystem
{
    /// <summary>
    /// A cleave (or great cleave) attack on the nearest enemy in melee reach, for a creature that has the feat and is
    /// fighting. Returns whether an attack was made.
    /// </summary>
    public bool CleaveAttack(Creature c)
    {
        if (Creature is not { } r || c.IsDead || !IsInCombat(c) || !CanAttack(c)) return false;
        bool great = HasFeat(c, CombatK.FeatGreatCleave);
        if (!great && !HasFeat(c, CombatK.FeatCleave)) return false;
        var main = r.Weapons(c).Main;
        if (main != null && r.Stats(main).Ranged) return false;
        var next = NearestEnemy(c, null, (a, t) => MeleeReach(a, t));
        if (next == null) return false;
        var ca = Rules.Context(c, next, main, false, Dice);
        ca.Kind = great ? AttackKind.GreatCleave : AttackKind.Cleave;
        Resolve(ca);
        return true;
    }

    /// <summary>
    /// Everyone threatening <paramref name="provoker"/> (or only <paramref name="single"/>) makes an attack of
    /// opportunity on it, within their per-round allowance.
    /// </summary>
    public void BroadcastAttackOfOpportunity(Creature provoker, Creature? single)
    {
        if (provoker.Area == null) return;
        var threats = single != null ? new List<GameObject> { single } : provoker.Area.Objects.ToList();
        foreach (var o in threats)
            if (o is Creature c && c != provoker && Threatens(c, provoker)) AttackOfOpportunity(c, provoker);
    }

    /// <summary>Whether <paramref name="c"/> would flank <paramref name="target"/> attacking it now.</summary>
    public bool IsFlanking(Creature c, Creature target)
    {
        if (Creature is not { } r) return false;
        var a = Rules.Context(c, target, r.Weapons(c).Main, false, Dice);
        return a.Flanked;
    }

    /// <summary>Not attacking anyone, so caught flat-footed (as combat has it: <see cref="CreatureCombatRules.Prepare"/>).</summary>
    public bool IsFlatFooted(Creature c) => TargetOf(c) == GameObject.InvalidId;
}
