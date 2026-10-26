using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Rules;

/// <summary>
/// What a creature's death gives the killer's party: the engine's kill experience and the "Aluvian Darkstar killed
/// Goblin" line (dialog.tlk 10474). Measured on the original with the killxp, killxp2, killxp3 and killmsg scenes; the rules
/// and what is not measured are in docs/KILL_XP.md.
/// <list type="bullet">
/// <item>Only a death with a killer in a player's party counts: the player, or an associate whose master chain ends at
/// one. A bystander's kill, the module's or an area's EffectDeath, or a script with no creature behind it, give nothing
/// and print nothing, except that a player killed that way is told "Someone killed ..." (docs/DEATH_AND_DYING.md).</item>
/// <item>The party members that count are the killer and every party member (players and their associates) in the
/// victim's area within <see cref="ShareRange"/> of the victim, dead and dying ones left out. Each counted player gets
/// xptable.2da[the players' average level][challenge rating + 0.49, truncated, 0..40] x (module XP scale / 10), divided
/// by (players + associates / 4), in single precision and truncated, then the multiclass penalty (as GiveXPToCreature
/// applies it); after the victim's OnDeath. Associates take a share but gain nothing (killxp3).</item>
/// <item>No experience for killing a member of the killer's own party (the line still prints), a summoned creature,
/// familiar or animal companion, nor at scale 0.</item>
/// <item>Each counted player is told "Experience Points Gained:  N" (when N &gt; 0) and then the kill line; a player
/// who is killed is told the kill line too, whoever killed her (a henchman's master is not).
/// Both come after the attack and damage lines of the blow that killed (the coup de grace's come before its attack line).</item>
/// </list>
/// </summary>
public sealed class KillRewards : IWorldSystem
{
    /// <summary>
    /// A party member farther than this from the victim takes no share (killxp2: a henchman 40 m from the victim
    /// shared and 41 m did not; a player 54 m from a victim her henchman killed got nothing).
    /// </summary>
    public const float ShareRange = 40f;

    /// <summary>dialog.tlk 10474.</summary>
    const int KilledStrref = 10474;

    readonly World _world;
    /// <summary>Deaths whose rewards are still to be told (they wait for the killing blow's own log lines).</summary>
    readonly Dictionary<uint, Award> _pending = new();

    public KillRewards(World world) => _world = world;

    public void Step(float dt) { }

    sealed record Award(string Line, List<(Creature Player, int Xp)> Players);

    /// <summary>A creature died: works out who is rewarded now and tells them once the current blow has been logged.</summary>
    public void Died(Creature victim, uint killerId)
    {
        if (Compute(victim, killerId) is not { } award) return;
        _pending[victim.Id] = award;
        _world.After(0, () => Flush(victim));
    }

    /// <summary>Tells a death's rewards now, if they are still waiting (the coup de grace prints them before its attack).</summary>
    public void Flush(GameObject victim)
    {
        if (!_pending.Remove(victim.Id, out var award)) return;
        foreach (var (pc, xp) in award.Players)
        {
            if (xp > 0) CreatureRules.GiveXP(_world, pc, xp, multiclassPenalty: true);
            _world.Output?.SendFeedback(pc, award.Line, FeedbackKind.Death);
        }
    }

    Award? Compute(Creature victim, uint killerId)
    {
        if (_world.Get<Creature>(killerId) is not { } killer)
        {
            // A player killed with no creature behind it (module damage, EffectDeath) is still told: "Someone killed
            // Aluvian Darkstar" (dying scenes). Nobody else is.
            if (!IsPlayer(victim)) return null;
            string someone = Tlk(KilledStrref, "<CUSTOM0> killed <CUSTOM1>").Replace("<CUSTOM0>", "Someone").Replace("<CUSTOM1>", Name(victim));
            return new Award(someone, new List<(Creature, int)> { (victim, 0) });
        }
        string line = Tlk(KilledStrref, "<CUSTOM0> killed <CUSTOM1>").Replace("<CUSTOM0>", Name(killer)).Replace("<CUSTOM1>", Name(victim));
        var result = new List<(Creature, int)>();
        if (AssociateService.PlayerMaster(_world, killer) is { } credited)
        {
            var parties = PartyService.Of(_world);
            var members = new List<Creature>();
            foreach (var pc in parties.Members(parties.Ensure(credited)).DefaultIfEmpty(credited))
            {
                members.Add(pc);
                AddAssociates(pc, members);
            }
            // Dead and dying members don't count (killxp3: a dead henchman beside the victim took no share).
            var counted = members.Where(m => m != victim && !m.IsDead && !m.IsDestroyed && m.CurrentHitPoints > 0
                && (m == killer || InRange(m, victim))).ToList();
            var players = counted.Where(IsPlayer).ToList();
            int associates = counted.Count - players.Count;
            // No experience for a party member, a player, or a summoned creature, familiar or animal companion
            // (killxp3: a hostile bandit's summoned badger gave nothing, its henchman full experience).
            bool none = members.Contains(victim) || victim.IsPC
                || victim.AssociateType is AssociateService.Summoned or AssociateService.Familiar or AssociateService.AnimalCompanion;
            int partyLevel = PartyLevel(_world, players);
            foreach (var pc in players)
                result.Add((pc, none ? 0 : Amount(_world, partyLevel, victim.ChallengeRating, _world.XPScale, players.Count, associates)));
        }
        // A player who is killed is told who did it, whoever that was (killmsg: "Goblin killed Aluvian Darkstar");
        // a henchman's master is not.
        if (IsPlayer(victim) && !result.Any(r => r.Item1 == victim)) result.Add((victim, 0));
        return result.Count > 0 ? new Award(line, result) : null;
    }

    static bool IsPlayer(Creature c) => c.IsPC && c.Player?.FamiliarMaster == null;

    /// <summary>
    /// The level whose xptable.2da row a kill uses: the counted players' average level, truncated and kept to 1..40.
    /// A player's level here is the higher of her hit dice and the level her experience gives (killxp2: a level 1
    /// player with 200,000 experience took level 20's row).
    /// </summary>
    public static int PartyLevel(World w, IReadOnlyList<Creature> players)
    {
        if (players.Count == 0) return 1;
        int sum = players.Sum(p => Math.Max(p.TotalLevel, CreatureRules.LevelForXP(w, p.Experience)));
        return Math.Clamp(sum / players.Count, 1, CreatureRules.MaxLevel);
    }

    /// <summary>
    /// One player's kill experience before the multiclass penalty: xptable.2da[party level][challenge rating + 0.49,
    /// truncated, kept to 0..40] x (scale / 10), divided by (players + associates / 4) when that is at least 1, in
    /// single precision, truncated (killxp3: CR 2.505 takes C2; 50 at scale 21 gives 104, at 47 234).
    /// </summary>
    public static int Amount(World w, int partyLevel, float challengeRating, int scale, int players, int associates)
    {
        if (scale <= 0 || players <= 0) return 0;
        var table = w.Resources.TwoDA("xptable");
        if (table.RowCount == 0) return 0;
        int row = Math.Clamp(partyLevel - 1, 0, table.RowCount - 1);
        int column = Math.Clamp((int)(challengeRating + 0.49f), 0, 40);
        if (!float.TryParse(table.Get(row, "C" + column), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value)) return 0;
        float amount = value * (scale / 10f);
        float divisor = players + associates * 0.25f;
        if (divisor >= 1f) amount /= divisor;
        return (int)Math.Min(int.MaxValue, amount);
    }

    /// <summary>One player's kill experience alone (her own level), for tools and tests.</summary>
    public static int Amount(World w, Creature pc, float challengeRating, int scale, int players, int associates) =>
        Amount(w, PartyLevel(w, new[] { pc }), challengeRating, scale, players, associates);

    void AddAssociates(Creature master, List<Creature> into)
    {
        foreach (var a in AssociateService.AllAssociates(_world, master))
            if (!into.Contains(a))
            {
                into.Add(a);
                AddAssociates(a, into);
            }
    }

    static bool InRange(Creature member, Creature victim) =>
        member.Area != null && member.Area == victim.Area
        && System.Numerics.Vector3.Distance(member.Position, victim.Position) <= ShareRange;

    string Name(GameObject o) => o.DisplayName(_world.Resources.Talk) is { Length: > 0 } n ? n : o.Tag;

    string Tlk(int strref, string fallback) => _world.TlkStringOverridden(strref) is { Length: > 0 } s ? s : fallback;
}
