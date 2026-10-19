using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>
/// ACTION_ATTACKOBJECT: fights a target for as long as the attack lasts. Scripts queue it with ActionAttack and
/// players start it by attacking something; because the fight lives on the action queue, anything that clears the
/// queue (a new order from the player, ClearAllActions) also ends the attack.
/// </summary>
public sealed class AttackAction : GameAction
{
    /// <summary>The target; a new attack order given while this one is current retargets it (see <see cref="Retarget"/>).</summary>
    public required uint Target;
    /// <summary>
    /// ActionAttack(o, TRUE), or a target picked when a passive attack lost its own: such an attack doesn't follow a
    /// target to another area (nwserver attack_leave).
    /// </summary>
    public bool Passive;
    /// <summary>Following a target that left the attacker's area (docs/INTER_AREA_MOVEMENT.md).</summary>
    WorldObjects.InterAreaMoveAction? _pursuit;
    WorldObjects.KnockOpenAction? _knock;
    bool _pursued;
    bool _pvpChecked;
    // ACTION_ATTACKOBJECT; measured: a pursuer reads ACTION_MOVETOPOINT once it has gone through a transition.
    public override int ActionType => _pursuit is { } p && (p.Transitioned || _pursued) ? p.ActionType : 3;

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature c || world.Get(Target) is not { } t) return ActionStatus.Failed;
        // A broken door closed again is knocked open rather than fought (a deliberate difference from the original,
        // where nothing happens: docs/INTER_AREA_MOVEMENT.md, "Killed doors").
        if (t is Door { Broken: true, IsOpen: false } || _knock != null)
        {
            _knock ??= new WorldObjects.KnockOpenAction { Target = Target };
            return _knock.Step(world, c, dt);
        }
        if (Rules.HitPoints.IsDead(t)) return ActionStatus.Done;
        // A player's attack the PvP setting forbids (her own summon in a party-PvP area) is refused with feedback.
        if (!_pvpChecked)
        {
            _pvpChecked = true;
            if (world.RefusesPvp(c, t)) return ActionStatus.Failed;
            // Starting an attack ends a conversation the attacker is part of (ConversationService.Fights).
            world.System<Conversations.ConversationService>().Fights(c);
            // An NPC's attack on a member of its own faction that it regards as a friend is dropped at once (nwserver
            // attack_mate: within the standard Hostile faction, reputation 100, no attack and ACTION_INVALID; members of
            // a faction at 50 to each other, and a personal friend in another faction, are attacked). This is why a
            // confused creature whose nearest creature is such a faction mate does nothing (NW_G0_confuse, cond_confuse).
            if (!c.IsPC && t is Creature mate && !mate.IsPC && world.Factions.FactionOf(mate) == world.Factions.FactionOf(c)
                && world.Factions.IsFriendOf(c, mate)) return ActionStatus.Failed;
        }
        var combat = world.System<CombatSystem>();
        // A target behind a Sanctuary its attacker failed to save against ends the attack: as the sanctuary lands for an
        // attacker that has perceived already (nwserver ai_caster: the fighter stops at once, never swings, and idles
        // until the sanctuary ends), at its first perception update for one that hasn't (cond_misc: attackers ordered
        // before then swing once).
        if (t is Creature tc && c.Society.PerceivedOnce && tc.Effects.Any(e => e.Type == CombatK.EffectSanctuary)
            && world.System<Society.PerceptionSystem>().SanctuaryFailedNow(c, tc))
        {
            combat.Stop(c, leaveCombat: false);
            return ActionStatus.Done;
        }
        // Another attack queued behind this one takes over at once (nwserver ai_attack_queue: a second ActionAttack or a
        // combat feat queued behind an attack ends it; other queued actions wait for it to finish).
        // The target went to another area: an NPC attacker ordered with ActionAttack(o, FALSE) follows it through the
        // transitions, whether or not it saw it go and however far away it is, then fights on (measured on nwserver
        // with and without the default AI scripts, parity suite interarea_pursuit), encounter creatures too, also after
        // a player target still loading the next area (nwmain playerai). A player target that has just taken a
        // transition counts as in the area it is loading.
        var targetArea = world.Whereabouts(t).Area;
        // A player's attack doesn't follow its target out of the area, ordered or passive: it turns to the nearest enemy
        // within her reach, or ends (nwmain playerai: alone, her attack action ended in the update
        // after the target jumped away, and she stood still; with a second enemy 1.4 m away her target cleared at once
        // and she took that enemy up at her next swing).
        if (c.IsPC && c.Area != null && targetArea != c.Area)
        {
            if (NearestEnemyInReach(world, c, t) is not { } next) { combat.Stop(c, leaveCombat: false); return ActionStatus.Done; }
            Target = next.Id;
            Passive = true;
            combat.Stop(c, leaveCombat: false);
            return ActionStatus.InProgress;
        }
        // A passive attack whose target went to another area switches to the nearest enemy within the attacker's
        // reach, or ends (nwserver attack_leave: ended in the update after the jump; with a second enemy 1.5 m away it
        // went on against that one).
        if (Passive && !c.IsPC && targetArea != c.Area)
        {
            if (NearestEnemyInReach(world, c, t) is not { } next) { combat.Stop(c, leaveCombat: false); return ActionStatus.Done; }
            Target = next.Id;
            combat.Stop(c, leaveCombat: false);
            return ActionStatus.InProgress;
        }
        if (!c.IsPC && c.Area != null && targetArea != null)
        {
            if (targetArea != c.Area || _pursuit is { Transitioned: true })
            {
                // Taken up again after a transition, the pursuit walks (measured).
                _pursuit ??= new WorldObjects.InterAreaMoveAction
                {
                    Kind = WorldObjects.InterAreaMoveAction.Goal.Pursuit, TargetObject = Target, Run = !_pursued, FirstPlan = !_pursued,
                };
                combat.State(c).Pursuing = true;
                if (_pursuit.Step(world, c, dt) == ActionStatus.InProgress) return ActionStatus.InProgress;
                _pursued |= _pursuit.Transitioned;
                _pursuit = null;
                combat.State(c).Pursuing = false;
                if (targetArea != c.Area) return ActionStatus.InProgress;
            }
            else if (_pursuit != null) { _pursuit.Cancel(world, c); _pursuit = null; combat.State(c).Pursuing = false; }
        }
        // An attacker that failed its Will save against the target's sanctuary drops the attack (nwserver, cond_misc).
        if (t is Creature protectedTarget && world.System<Society.PerceptionSystem>().SanctuaryBars(c, protectedTarget))
        { combat.Stop(c); return ActionStatus.Failed; }
        // A dazed creature (still commandable) drops an attack order (nwserver, condition_actions daze.attack.action).
        if (c.Player?.IsAreaLoading != true && !CombatSystem.CanAttack(c)) { combat.Stop(c, leaveCombat: false); return ActionStatus.Failed; }
        // A player target still loading this area is waited for, not given up.
        if (t is Creature { Area: null, Player.IsAreaLoading: true } && targetArea == c.Area) return ActionStatus.InProgress;
        combat.Attack(c, t);
        // The action stays current while the fight lasts, like the original's attack action.
        return combat.IsInCombat(c) && combat.State(c).Target == Target ? ActionStatus.InProgress : ActionStatus.Done;
    }

    /// <summary>
    /// The nearest living enemy, other than <paramref name="old"/>, within the attacker's maximum melee range (where it
    /// stands to fight plus <see cref="CombatSystem.ShotThreatReach"/>).
    /// </summary>
    static Creature? NearestEnemyInReach(World world, Creature c, GameObject old)
    {
        if (c.Area is not { } area) return null;
        var combat = world.System<CombatSystem>();
        Creature? best = null;
        float bestDist = float.MaxValue;
        foreach (var o in area.Objects)
        {
            if (o is not Creature e || e == c || e == old || e.IsDestroyed || Rules.HitPoints.IsDead(e) || !world.Factions.IsEnemyOf(c, e)) continue;
            float d = System.Numerics.Vector2.Distance(new(c.Position.X, c.Position.Y), new(e.Position.X, e.Position.Y));
            if (d > combat.MeleeDistance(c, e) + CombatSystem.ShotThreatReach || d >= bestDist) continue;
            best = e;
            bestDist = d;
        }
        return best;
    }

    /// <summary>
    /// ActionAttack (or a combat feat) while an attack is the current action: the original changes that attack instead of
    /// queuing another (nwserver ai_attack_queue: the attack target clears at once and is set again at the next swing on
    /// the round's schedule; actions queued behind the attack still wait).
    /// </summary>
    public static bool Retarget(World world, GameObject owner, uint target, bool passive = false)
    {
        if (owner is not Creature c || owner.Actions.Count == 0 || owner.Actions[0] is not { } head) return false;
        var attack = head as AttackAction ?? (head as Scripting.Functions.UseFeatAction)?.Attack;
        if (attack == null) return false;
        var combat = world.System<CombatSystem>();
        if (attack._pursuit != null) { attack._pursuit.Cancel(world, c); attack._pursuit = null; combat.State(c).Pursuing = false; }
        attack.Target = target;
        attack.Passive = passive;
        combat.Stop(c, leaveCombat: false);
        return true;
    }

    public override void Cancel(World world, GameObject owner)
    {
        _knock?.Cancel(world, owner);
        _pursuit?.Cancel(world, owner);
        if (_pursuit != null && owner is Creature pursuer) world.System<CombatSystem>().State(pursuer).Pursuing = false;
        if (owner is Creature c) world.System<CombatSystem>().Stop(c, leaveCombat: false);
    }
}
