using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Combat;

/// <summary>
/// Attacks of opportunity: a creature fighting in melee gets a free attack (NUM_ATTACKS_OF_OPPORTUNITY per
/// round, more with Combat Reflexes when the module has that feat) when a hostile creature it can see that is
/// attacking it walks out of its reach, a creature casts a spell next to it (an NPC wizard casting beside its attacker
/// draws none: ai_cast_rounds), or a creature shoots next to it (a player only at D&amp;D Hardcore and above: <see cref="ProvokeShot"/>; an
/// archer's step back draws none, archer_aoo). A plot creature never makes one (aoo_land). As measured on the original, drinking a potion and walking away from a
/// creature one isn't attacking draw none (parity suite combat_aoo; visual scene difficulty, at every difficulty).
/// Mobility gives +4 AC against them; Spring Attack avoids the ones for moving.
/// </summary>
public sealed partial class CombatSystem
{
    int _combatReflexes = -2;

    /// <summary>Attacks of opportunity a creature may make per round.</summary>
    public int MaxAttacksOfOpportunity(Creature c)
    {
        int n = Math.Max(0, RI("NUM_ATTACKS_OF_OPPORTUNITY", 1));
        if (_combatReflexes == -2) _combatReflexes = Creature?.FeatByLabel("CombatReflexes") ?? -1;
        if (_combatReflexes >= 0 && HasFeat(c, _combatReflexes))
            n += Math.Max(0, CreatureRules.AbilityModifier(_world, c, NwRules.AbilityDex));
        return n;
    }

    /// <summary>
    /// Whether <paramref name="c"/> threatens <paramref name="other"/>: fighting, able to attack, melee-armed, hostile,
    /// sees it, within reach, and not plot (nwserver aoo_land: a plot creature in combat beside a caster made no attack
    /// of opportunity, idle 0 of 4 or fighting 0 of 4; an immortal one did, 4 of 4).
    /// </summary>
    public bool Threatens(Creature c, Creature other)
    {
        if (c == other || c.IsDead || other.IsDead || c.Plot || c.Area == null || c.Area != other.Area) return false;
        if (!c.InCombat || !CanAttack(c)) return false;
        if (Creature is { } r && r.Weapons(c).Main is { } main && r.Stats(main).Ranged) return false;
        if (!_world.Factions.IsEnemyOf(c, other)) return false;
        if (!CombatHooks.CanSee(_world, c, other)) return false;
        return Distance2(c, other) <= MeleeReach(c, other);
    }

    /// <summary>
    /// A spell cast without Defensive Casting: everyone threatening <paramref name="provoker"/> owes it an attack of
    /// opportunity (nwserver aoo_land, a caster's Ray of Frost 1.5 m from a stripped bandit in combat facing it). One
    /// that wasn't fighting is ordered to attack the caster, and its next round opens with the attack of opportunity on
    /// top of its own attacks: the swing came 0.09-0.47 s after the cast started and its normal swing 2.2-2.3 s later,
    /// the round lasting 5.5 s. One already fighting, the caster or anyone else, makes one extra swing at the caster in
    /// its running round as soon as its current swing is over: it came 0.58-1.16 s after the cast started, 1.25 s after
    /// its previous swing in four of six, and the next round wasn't delayed (ai_cast_rounds' "a goblin attacking the
    /// wizard drew none" was that extra swing). Returns the creatures that owe one.
    /// </summary>
    public List<Creature> ProvokeAttacksOfOpportunity(Creature provoker, string reason)
    {
        var results = new List<Creature>();
        if (provoker.Area == null) return results;
        foreach (var o in provoker.Area.Objects.ToList())
        {
            if (o is not Creature c || !Threatens(c, provoker) || !OpportunityFaces(c, provoker) || c.ActionModes.Contains(CombatK.ModeStealth)
                || c.ActionModes.Contains(CombatK.ModeParry) || Creature is null) continue;
            var s = State(c);
            Counters(s);
            if (s.AttacksOfOpportunity >= MaxAttacksOfOpportunity(c)) continue;
            s.AttacksOfOpportunity++;
            if (TargetOf(c) == GameObject.InvalidId)
            {
                s.OpportunityNextRound = provoker.Id;
                if (!(c.Actions.Count > 0 && c.Actions[0] is AttackAction))
                    c.Actions.Insert(0, new AttackAction { Target = provoker.Id });
            }
            else s.ExtraOpportunities.Add((Math.Max(_world.Time, s.LastSwingAt + OpportunitySwingWait), provoker.Id));
            results.Add(c);
        }
        return results;
    }

    /// <summary>How long a fighting creature's current swing holds back an extra attack of opportunity (one attack's swing animation).</summary>
    public const float OpportunitySwingWait = 1.25f;

    /// <summary>A fighting creature's owed extra swing at a caster, once it is due (<see cref="ProvokeAttacksOfOpportunity"/>).</summary>
    void ExtraOpportunity(Creature c, CombatState s, double now, float swing)
    {
        for (int i = 0; i < s.ExtraOpportunities.Count; i++)
        {
            var (due, id) = s.ExtraOpportunities[i];
            if (now < due) continue;
            s.ExtraOpportunities.RemoveAt(i--);
            if (_world.Get(id) is not Creature caster || caster.IsDead || caster.Area != c.Area || Distance2(c, caster) > MeleeReach(c, caster)
                || Creature is not { } r || !CanAttack(c)) continue;
            var a = Rules.Context(c, caster, r.Weapons(c).Main, false, Dice);
            a.Kind = AttackKind.Opportunity;
            a.AttackNumber = s.NextAttack + ++s.OpportunitySwings;
            s.LastSwingAt = now;
            Resolve(a, swing, ImpactDelay);
        }
    }

    /// <summary>
    /// A shot fired by a creature that isn't a player provokes an attack of opportunity from every creature that
    /// threatens the shooter as for a spell (in combat, hostile, sees it, melee-armed, not facing away, not already
    /// fighting), within its preferred attack distance + 1.5 m. Measured on nwserver (parity suites archer_aoo at
    /// D&amp;D Hardcore and archer_aoo_normal at Normal): a script-less bandit that saw the archer and was in combat
    /// took up the fight 0.15-0.5 s after a shot from 1.5, 2.2, 2.3, 3.0 or 3.1 m, never after one from 3.8 or 4.0 m,
    /// never facing away, and never when the archer only stepped out of its reach.
    /// <para>A player's shots provoke only where the difficulty's diffsettings.2da NoAoOWithRanged switch is off
    /// (stock: D&amp;D Hardcore and Very Difficult), and a threatener already fighting her makes its attack of
    /// opportunity too, as an extra swing (nwmain plotshot: a player shooting a dummy between two hostile bandits in
    /// combat facing her, at Hardcore the immortal one's "Attack Of Opportunity" came at each of her shots, the first
    /// starting its attack on her, while the plot one made none; at Normal neither).</para>
    /// </summary>
    public List<AttackOutcome> ProvokeShot(Creature shooter)
    {
        var results = new List<AttackOutcome>();
        if (shooter.Area == null || shooter.IsPC && Difficulty.Setting(_world, "NoAoOWithRanged") != 0) return results;
        foreach (var o in shooter.Area.Objects.ToList())
        {
            if (o is not Creature c || c == shooter || c.IsDead || c.Plot || !c.InCombat || !CanAttack(c)) continue;
            // An NPC's shot leaves out creatures already fighting (nwserver archer_aoo could not tell their extra swing
            // from an ordinary one; kept as it matched).
            if (!shooter.IsPC && TargetOf(c) != GameObject.InvalidId) continue;
            if (Creature is { } r && r.Weapons(c).Main is { } main && r.Stats(main).Ranged) continue;
            // It must have perceived the shooter: none came before a new creature's first perception pass
            // (archer_aoo g15pre: a bandit put in combat, shot from 1.5 m 3.4 s after both were made, drew nothing).
            if (!_world.Factions.IsEnemyOf(c, shooter) || !c.Society.Seen.Contains(shooter.Id) || !CombatHooks.CanSee(_world, c, shooter)) continue;
            if (Distance2(c, shooter) > MeleeDistance(c, shooter) + ShotThreatReach || !FacesTowards(c, shooter)) continue;
            if (c.ActionModes.Contains(CombatK.ModeStealth) || c.ActionModes.Contains(CombatK.ModeParry)) continue;
            if (AttackOfOpportunity(c, shooter) is { } outcome) results.Add(outcome);
        }
        return results;
    }

    /// <summary>How far beyond its preferred attack distance a creature threatens a shooter (archer_aoo).</summary>
    public const float ShotThreatReach = 1.5f;

    /// <summary>
    /// Not facing away: the angle between its facing and the provoker is under 135 degrees (nwserver strike_back: a caster
    /// behind its victim drew nothing).
    /// </summary>
    static bool FacesTowards(Creature c, GameObject other, float? facing = null, System.Numerics.Vector3? from = null)
    {
        var p = from ?? c.Position;
        float dx = other.Position.X - p.X, dy = other.Position.Y - p.Y;
        if (dx * dx + dy * dy < 1e-6f) return true;
        float off = MathF.Abs(MathF.IEEERemainder(MathF.Atan2(dy, dx) - (facing ?? c.Facing), 2 * MathF.PI));
        return off < 135f * MathF.PI / 180f;
    }

    /// <summary>
    /// "Not facing away" for a cast's attack of opportunity. A creature attacking from where it stands keeps the facing
    /// it had (nwserver aoo_land fightaway: bandits made facing away from the caster they then attacked in place made no
    /// extra swing at its cast, 0 of 2, the two that had walked did; ai_cast_rounds: goblins made facing away from the
    /// wizard they attacked, 0 of 6). Its combat steps don't turn it either; they are judged from where it stood, as
    /// OpenWinter's steps keep going round the same way and the original's didn't bring those goblins round.
    /// </summary>
    bool OpportunityFaces(Creature c, GameObject other) =>
        TryState(c) is { StandFacing: { } stand } s ? FacesTowards(c, other, stand, s.StandOrigin) : FacesTowards(c, other);

    /// <summary>
    /// One attack of opportunity with the main weapon at full attack bonus, if the creature has one left this round. A
    /// creature that had no attack target is also ordered to attack the provoker, and goes on fighting it like any
    /// ActionAttack (nwserver strike_back: a victim with no event scripts kept attacking the NPC caster whose spell
    /// provoked it; archer_backoff: the passive targets of archers that stepped back out of their reach all took up
    /// the fight). The server orders no other counterattack.
    /// </summary>
    public AttackOutcome? AttackOfOpportunity(Creature c, Creature target)
    {
        var s = State(c);
        Counters(s);
        if (s.AttacksOfOpportunity >= MaxAttacksOfOpportunity(c) || Creature is not { } r) return null;
        s.AttacksOfOpportunity++;
        var a = Rules.Context(c, target, r.Weapons(c).Main, false, Dice);
        a.Kind = AttackKind.Opportunity;
        bool idle = TargetOf(c) == GameObject.InvalidId && !(c.Actions.Count > 0 && c.Actions[0] is AttackAction);
        var outcome = Resolve(a);
        if (idle && !c.IsDead && !target.IsDead && !HitPoints.IsDead(target))
            c.Actions.Insert(0, new AttackAction { Target = target.Id });
        return outcome;
    }

    /// <summary>
    /// A bare-handed attack draws an attack of opportunity from its target, armed or not and fighting back or not,
    /// unless the attacker has Improved Unarmed Strike (a player attacking an armed goblin that fought back drew one a
    /// round in the original, with a dagger none: visual scene difficultypc). Parity suite combat_unarmed, re-measured
    /// on nwserver with Prelude's faction table so that the pairs are really hostile: a passive stripped bandit took
    /// about six swings at its bare-handed attacker in 24 s whether it held a longsword or nothing; with a dagger or
    /// Improved Unarmed Strike none. (Without a faction table the pairs were friends, which had read as "a passive
    /// armed bandit takes none".) A player target is not measured: it keeps the older rule, an armed player fighting
    /// the attacker.
    /// </summary>
    void ProvokeUnarmed(AttackContext a)
    {
        if (!a.Unarmed || a.Ranged || a.Kind != AttackKind.Normal || a.Target is not Creature t || HasFeat(a.Attacker, CombatK.FeatImprovedUnarmedStrike)) return;
        if (t.IsPC && (TargetOf(t) != a.Attacker.Id || Creature is not { } r || r.Weapons(t).Main is null)) return;
        if (Threatens(t, a.Attacker)) AttackOfOpportunity(t, a.Attacker);
    }

    /// <summary>Movement: hostile creatures that were in a fighter's reach last step and are now moving out of it provoke.</summary>
    void StepThreats()
    {
        foreach (var (id, s) in _states.ToList())
        {
            if (_world.Get(id) is not Creature c || c.Area == null || !c.InCombat || c.IsDead)
            {
                if (s.Threatened.Count > 0) s.Threatened.Clear();
                continue;
            }
            var now = new HashSet<uint>();
            foreach (var o in c.Area.Objects)
                if (o is Creature other && Threatens(c, other)) now.Add(other.Id);
            foreach (var prev in s.Threatened)
            {
                if (now.Contains(prev) || _world.Get(prev) is not Creature mover) continue;
                if (mover.Area != c.Area || mover.IsDead || mover.MoveDirection == System.Numerics.Vector3.Zero) continue;
                // Only a creature walking away provokes, not one following a fighter that stepped back (measured on
                // nwserver: a pursuer chasing its fleeing target is never attacked by it, parity suite interarea_pursuit).
                if (mover.MoveDirection.X * (mover.Position.X - c.Position.X) + mover.MoveDirection.Y * (mover.Position.Y - c.Position.Y) <= 0) continue;
                // Only a creature attacking this fighter provokes it by leaving (combat_aoo walk: none otherwise).
                if (TargetOf(mover) != c.Id) continue;
                if (HasFeat(mover, CombatK.FeatSpringAttack)) continue;
                if (!CanAttack(c) || !_world.Factions.IsEnemyOf(c, mover)) continue;
                AttackOfOpportunity(c, mover);
            }
            s.Threatened = now;
        }
    }
}
