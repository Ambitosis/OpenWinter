using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Combat;

/// <summary>
/// Combat rounds. Every creature with an attack target fights in rounds: at the start of a round it plans
/// its attacks (<see cref="ICombatRules.PlanRound"/>) and makes them at the original's times in the round
/// (<see cref="AttackTimes"/>), closing to weapon range first. As in the original, a round of attacks lasts
/// 5 seconds with one attack and 5.5 with more for an NPC attacking a passive target; a player in melee
/// adds half a second. A creature that is only defending has six-second rounds. Each attack is resolved by <see cref="Resolve"/> (attack roll,
/// concealment, parry, deflection, critical confirmation, damage through <see cref="HitPoints.Damage"/>,
/// special attack side effects, on-hit properties, cleave). Attacks of opportunity, special attacks
/// (ActionUseFeat), whirlwind attacks and touch attacks are here too. Raises OnPhysicalAttacked
/// (every attack), OnEndCombatRound and, via the hit point rules, OnDamaged and OnDeath,
/// and sends clients an <c>attack</c> notify per swing (docs/notify/combat.md).
/// </summary>
public sealed partial class CombatSystem : IWorldSystem
{
    public const float RoundSeconds = 6f;
    // Native ordinary ranged attacks begin without closing at 40 m, but approach at 40.3 m.
    // The ranges.2da weapon rows and baseitems MaxRange do not control this boundary.
    const float RangedReach = 40f;
    readonly World _world;
    readonly Dictionary<uint, CombatState> _states = new();
    public ICombatRules Rules;
    /// <summary>Dice for every combat roll; tests inject <see cref="ScriptedDice"/>.</summary>
    public IDice Dice = new RandomDice();
    /// <summary>Independent presentation rolls: voice selection must not consume scripted combat dice.</summary>
    public IDice VoiceDice = new RandomDice(new Random());

    /// <summary>Raised after every resolved attack (normal, opportunity, cleave, whirlwind, riposte).</summary>
    public event Action<AttackOutcome>? AttackResolved;

    /// <summary>
    /// How long a creature stays in combat (GetIsInCombat) after its last attack or the last attack on it, with or
    /// without an attack order: about eight seconds on nwserver (ai_queries "cs"), as for script hostility.
    /// </summary>
    public const double HostilitySeconds = 8;

    public sealed class CombatState
    {
        public uint Target = GameObject.InvalidId;
        /// <summary>GetAttackTarget: the target of the creature's last swing, until its attack order is cleared or the
        /// target dies (nwserver ai_queries: invalid while it walks up and after ClearAllActions, set again at the next swing).</summary>
        public uint SwingTarget = GameObject.InvalidId;
        /// <summary>Start of the current round of attacks (-1 for none); it ends after <see cref="RoundLength"/>.</summary>
        public double RoundStart = -1;
        public float RoundLength = RoundSeconds;
        public float PlayerMeleeExtra;
        // A cast's eligibility interval survives recovery and action-queue clearing.
        // It does not hold up movement, waits, or other noncombat actions.
        public double SpellRoundEnd;
        /// <summary>Defensive Casting turned on by script while this non-player creature's round was running: it takes
        /// effect when its next attack round starts (<see cref="SetDefensiveCasting"/>).</summary>
        public bool PendingDefensiveCast;
        /// <summary>When the current spell round's OnEndCombatRound falls (-1 for none).</summary>
        public double SpellEorDue = -1;
        /// <summary>Start of the current defending round (in combat without attacking), or -1.</summary>
        public double DefenceStart = -1;
        public List<PlannedAttack> Plan = new();
        public int NextAttack;
        public uint LastAttacker = GameObject.InvalidId;
        public AttackOutcome? LastAttack;
        public uint LastWeapon = GameObject.InvalidId;
        public int LastAttackType, LastAttackMode;
        /// <summary>Last time this object attacked or was attacked (InCombat lasts a round after).</summary>
        public double LastHostile = -100;
        /// <summary>Following its target into another area (AttackAction): no rounds meanwhile (measured: the original
        /// fires no OnEndCombatRound while a pursuer is on its way).</summary>
        public bool Pursuing;
        /// <summary>Separate lifetime for harmful script events and direct damage, without an attack target.</summary>
        public double ScriptHostilityUntil;
        /// <summary>Feat attack queued by ActionUseFeat for the next attack (FEAT_*), or -1.</summary>
        public int PendingFeat = -1;
        public int PendingSubFeat;
        // Per-round allowances.
        public double CounterStart = -100;
        public int AttacksOfOpportunity, Parries, Deflections, Cleaves, CircleKicks;
        public bool EpicDodgeUsed;
        /// <summary>Hostile creatures in melee reach at the last step (for attacks of opportunity when they move away).</summary>
        public HashSet<uint> Threatened = new();
        /// <summary>A cast's attack of opportunity owed by a creature that wasn't fighting: the first swing of its next round.</summary>
        public uint OpportunityNextRound = GameObject.InvalidId;
        /// <summary>A cast's attack of opportunity owed by a creature already fighting: one extra swing, due at <c>Due</c>.</summary>
        public readonly List<(double Due, uint Target)> ExtraOpportunities = new();
        /// <summary>When its last swing of a round started.</summary>
        public double LastSwingAt = -100;
        /// <summary>
        /// The facing it had before fighting turned it toward its target where it stands, and where it stood (null once it
        /// moves): the original doesn't turn a creature attacking in place, so attacks of opportunity judge "facing away"
        /// by this (<see cref="OpportunityFacing"/>).
        /// </summary>
        public float? StandFacing;
        /// <summary>Where it was when it last stood still with <see cref="StandFacing"/> (combat steps move it from there).</summary>
        public Vector3 StandPos, StandOrigin;
        public MoveAction? Approach;
        // Sideways steps between rounds of melee (CombatSystem.Steps).
        public bool StepsPlanned;
        public readonly List<double> StepTimes = new();
        public int StepSign = 1, StepsTaken;
        /// <summary>This round's step is an archer's step back (<see cref="RangedBackStepRange"/>).</summary>
        public bool BackStep;
    }

    public CombatSystem(World world)
    {
        _world = world;
        Rules = new CreatureCombatRules(world);
    }

    public int Order => 10;

    public CombatState State(GameObject o)
    {
        if (!_states.TryGetValue(o.Id, out var s)) _states[o.Id] = s = new CombatState();
        return s;
    }

    public CombatState? TryState(GameObject o) => _states.GetValueOrDefault(o.Id);

    /// <summary>The object a creature is attacking (OBJECT_INVALID when none).</summary>
    public uint TargetOf(GameObject o) => _states.TryGetValue(o.Id, out var s) && _world.Get(s.Target) != null ? s.Target : GameObject.InvalidId;

    public bool IsInCombat(Creature c) => _states.TryGetValue(c.Id, out var s) && _world.Get(s.Target) != null;

    /// <summary>Earliest next ordinary cast, preserving the current attack round's measured length.</summary>
    public double NextCastTime(Creature c)
    {
        if (TryState(c) is not { } s) return 0;
        return Math.Max(s.SpellRoundEnd, s.RoundStart < 0 ? 0 : s.RoundStart + s.RoundLength);
    }

    /// <summary>
    /// A cast's round. Unless <paramref name="endRoundEvent"/> is false, OnEndCombatRound fires when it ends, in combat or
    /// not, and a creature still in combat without an attack has its next round end three seconds later, then every six
    /// (nwserver ai_cast_rounds: Mage Armor out of combat ends a round 6 s after the cast began; Magic Missile at an
    /// enemy, 6 and 9 s; two spells in a row, 6, 12 and 15 s).
    /// </summary>
    public void BeginSpellRound(Creature c, float seconds, bool endRoundEvent = true)
    {
        var s = State(c);
        s.SpellRoundEnd = _world.Time + seconds;
        if (endRoundEvent) s.SpellEorDue = s.SpellRoundEnd;
    }

    /// <summary>Whether the creature's own combat round (an attack round or a cast's or item use's round) is running.</summary>
    public bool RoundRunning(Creature c) => TryState(c) is { } s
        && (_world.Time < s.SpellRoundEnd || s.RoundStart >= 0 && _world.Time - s.RoundStart < s.RoundLength);

    /// <summary>
    /// SetActionMode(ACTION_MODE_DEFENSIVE_CAST) on a non-player creature. Turned on while its round is running, the
    /// mode only takes effect when its next attack round starts (a cast's round doesn't apply it); until then
    /// GetActionMode reads FALSE and casts provoke (nwserver cast_provoke defmid).
    /// Returns false when the mode was deferred.
    /// </summary>
    public bool SetDefensiveCasting(Creature c, bool on)
    {
        var s = State(c);
        if (!on) { s.PendingDefensiveCast = false; return true; }
        if (c.IsPC || c.ActionModes.Contains(CombatK.ModeDefensiveCast) || !RoundRunning(c)) return true;
        s.PendingDefensiveCast = true;
        return false;
    }

    CreatureCombatRules? Creature => Rules as CreatureCombatRules;

    /// <summary>Starts (or retargets) attacks against a target. Retargeting keeps the current round's timing.</summary>
    public void Attack(Creature attacker, GameObject target)
    {
        // The dead and the absent can't be fought: taking them as a target would end the round at once, every tick.
        if (attacker == target || attacker.IsDead || target.IsDestroyed || HitPoints.IsDead(target) || target.Area != attacker.Area) return;
        if (!_world.PvpAllows(attacker, target)) return; // the server's PvP setting forbids it
        var s = State(attacker);
        s.Target = target.Id;
        s.DefenceStart = -1;
        // The first round starts when the attacker is in reach (Step), not at the order: on nwserver the round clock
        // of an attacker that walks up starts at its arrival (ai_queries "act").
        EnterCombat(attacker);
    }

    /// <summary>Puts a creature in combat (a player sees her initiative roll) if it isn't already.</summary>
    public void EnterCombat(Creature attacker)
    {
        if (attacker.InCombat) return;
        State(attacker).LastHostile = _world.Time;
        attacker.InCombat = true;
        _world.MarkChanged(attacker);
        if (attacker.IsPC) TellInitiative(attacker);
    }

    /// <summary>A player going into combat sees an initiative roll, as in the original: d20 + Dexterity + initiative feats.</summary>
    void TellInitiative(Creature c)
    {
        int bonus = CreatureRules.AbilityModifier(_world, c, NwRules.AbilityDex);
        if (HasFeat(c, FeatImprovedInitiative)) bonus += RI("IMPROVED_INITIATIVE_BONUS", 4);
        if (HasFeat(c, FeatEpicSuperiorInitiative)) bonus += RI("EPIC_SUPERIOR_INITIATIVE_BONUS", 8);
        if (HasFeat(c, FeatBlooded)) bonus += RI("BLOODED_INITIATIVE_BONUS", 2);
        if (HasFeat(c, FeatThug)) bonus += RI("THUG_INITIATIVE_BONUS", 2);
        bonus += c.Overrides?.InitiativeModifier ?? 0;
        int roll = Dice.Roll(20);
        _world.Output?.SendMessage(c, $"{Name(c)} : Initiative Roll : {roll + bonus} : ({roll} + {bonus} = {roll + bonus})");
    }

    const int FeatImprovedInitiative = 377, FeatBlooded = 379, FeatThug = 402, FeatEpicSuperiorInitiative = 753;

    /// <summary>
    /// Ends an attack order. Unless <paramref name="leaveCombat"/>, the creature stays in combat until the hostility
    /// times out (nwserver ai_queries: ClearAllActions keeps GetIsInCombat true, ClearAllActions(TRUE) ends it).
    /// </summary>
    public void Stop(Creature attacker, bool leaveCombat = true)
    {
        if (_states.TryGetValue(attacker.Id, out var s))
        {
            s.Target = GameObject.InvalidId;
            s.SwingTarget = GameObject.InvalidId;
            s.PendingFeat = -1;
            if (s.Approach != null) { s.Approach.Cancel(_world, attacker); s.Approach = null; }
        }
        if (leaveCombat && attacker.InCombat) { attacker.InCombat = false; _world.MarkChanged(attacker); }
    }

    /// <summary>
    /// GetAttemptedAttackTarget: the target of the current attack order, except while walking up to it (nwserver
    /// ai_queries: valid as the order starts, invalid during the walk, valid again on arrival).
    /// </summary>
    public uint AttemptedTarget(GameObject o)
    {
        var s = TryState(o);
        if (s?.Approach != null) return GameObject.InvalidId;
        if (o.Actions.Count > 0 && o.Actions[0] is AttackAction order && _world.Get(order.Target) != null) return order.Target;
        return s != null && _world.Get(s.Target) != null ? s.Target : GameObject.InvalidId;
    }

    /// <summary>
    /// GetAttackTarget (see <see cref="CombatState.SwingTarget"/>); also none for a creature that can't move whose target
    /// is out of its reach (<see cref="ReportedTarget"/>).
    /// </summary>
    public uint SwingTargetOf(GameObject o)
    {
        if (!_states.TryGetValue(o.Id, out var s) || _world.Get(s.SwingTarget) == null) return GameObject.InvalidId;
        return o is Creature c && s.SwingTarget == s.Target && ReportedTarget(c) == GameObject.InvalidId ? GameObject.InvalidId : s.SwingTarget;
    }

    void StartRound(Creature c, CombatState s)
    {
        s.RoundStart = _world.Time;
        s.Plan = Rules.PlanRound(c).ToList();
        // An attack of opportunity owed from a cast is this round's first swing, on top of its own attacks.
        if (_world.Get(s.OpportunityNextRound) is Creature owed && owed.Area == c.Area && !owed.IsDead)
            s.Plan.Insert(0, new PlannedAttack { Weapon = Creature?.Weapons(c).Main, OpportunityTarget = owed.Id });
        s.OpportunityNextRound = GameObject.InvalidId;
        s.NextAttack = 0;
        s.StepsPlanned = false;
        s.StepTimes.Clear();
        s.StepsTaken = 0;
        s.RoundLength = MutualRoundLength(c, s);
        if (s.PendingDefensiveCast)
        {
            s.PendingDefensiveCast = false;
            Requests.AbilityRequests.ApplyCombatMode(_world, c, CombatK.ModeDefensiveCast, true);
        }
    }

    /// <summary>
    /// Two creatures attacking each other slow each other down in the original (their swings and reactions take
    /// turns) and fall into step: measured on nwserver, a round lasts about 0.4 seconds longer against a foe making
    /// one attack a round (0.15 when both make one), and a second longer against one making more; both take the
    /// longer of the two.
    /// </summary>
    float MutualRoundLength(Creature c, CombatState s)
    {
        int mine = s.Plan.Count;
        s.PlayerMeleeExtra = 0;
        if (_world.Get(s.Target) is not Creature foe || !_states.TryGetValue(foe.Id, out var fs) || fs.Target != c.Id)
        {
            // The same native character as a PC takes half a second longer than
            // its copied NPC in passive-target melee (one, two and six attacks).
            // Ranged and mutually attacking pairs retain their measured timing.
            bool melee = s.Plan.All(a => a.Weapon == null || !(Creature?.Stats(a.Weapon).Ranged ?? false));
            s.PlayerMeleeExtra = c.IsPC && mine > 0 && melee ? 0.5f : 0f;
            return RoundLength(mine) + s.PlayerMeleeExtra;
        }
        int theirs = fs.RoundStart >= 0 ? fs.Plan.Count : Rules.PlanRound(foe).Count;
        if (mine <= 0 || theirs <= 0) return RoundLength(mine);
        static float Slowed(int self, int foe) => RoundLength(self) + (foe >= 2 ? 1f : self <= 1 ? 0.15f : 0.4f);
        return Math.Max(Slowed(mine, theirs), Slowed(theirs, mine));
    }

    /// <summary>How much earlier in its round a hasted creature swings (<see cref="Step"/>).</summary>
    public const float HasteSwingLead = 0.2f;

    /// <summary>Base NPC/passive-target round: 5 seconds for one attack, 5.5 for more, 6 for none.</summary>
    public static float RoundLength(int attacks) => attacks <= 0 ? RoundSeconds : attacks == 1 ? 5f : 5.5f;

    /// <summary>
    /// When each of <paramref name="count"/> attacks falls in the round, in seconds, as measured on the original:
    /// up to three swings about 1.9 seconds apart, each with one or two attacks (the second 0.35 seconds later). Two
    /// attacks (or one, hasted) come 2.25 s apart: nwserver round_idle, 48 gaps each, 2.236 s (two attacks) and 2.27 /
    /// 2.30 s (hasted) on average, range 1.5-2.8.
    /// </summary>
    public static float[] AttackTimes(int count) => count switch
    {
        <= 0 => Array.Empty<float>(),
        1 => new[] { 0.2f },
        2 => new[] { 0.3f, 2.55f },
        3 => new[] { 0.15f, 1.95f, 3.8f },
        4 => new[] { 0.25f, 0.6f, 2.4f, 4.15f },
        5 => new[] { 0.2f, 0.55f, 2.15f, 2.5f, 4.2f },
        6 => new[] { 0.2f, 0.5f, 2.05f, 2.4f, 3.9f, 4.25f },
        _ => Enumerable.Range(0, count).Select(i => 0.2f + i % 3 * 1.9f + i / 3 * 0.35f).OrderBy(t => t).ToArray(),
    };

    /// <summary>
    /// Length of a round whose target died after <paramref name="made"/> of its <paramref name="planned"/> attacks, as
    /// measured on nwserver. It goes by swing (attacks less than half a second apart share one): a kill in the last
    /// swing leaves the round as it was; otherwise the round ends 4.5 s in with two swings, and 3.65 s (first swing)
    /// or 4.65 s (second) with three.
    /// </summary>
    public static float CutShortRound(int planned, int made)
    {
        var times = AttackTimes(planned);
        if (made <= 0 || made > times.Length) return RoundLength(planned);
        int swings = 0, swing = 0;
        for (int i = 0; i < times.Length; i++)
        {
            if (i == 0 || times[i] - times[i - 1] > 0.5f) swings++;
            if (i == made - 1) swing = swings;
        }
        if (swing >= swings) return RoundLength(planned);
        if (swings == 2) return 4.5f;
        return swing == 1 ? 3.65f : 4.65f;
    }

    /// <summary>A round of attacks ends: OnEndCombatRound.</summary>
    void FinishRound(Creature c, CombatState s)
    {
        s.RoundStart = -1;
        DropScriptedModes(c);
        EndRound(c);
    }

    /// <summary>
    /// A creature in combat that isn't attacking gets OnEndCombatRound every six seconds, counted from when its attack
    /// order went (or from when it came into combat), independently of a round of attacks still running out. Measured
    /// on nwserver (parity suite round_idle): an attacker cleared with ClearAllActions() 0.2 s after a swing and given
    /// nothing new gets the round's own event 4.1-4.6 s after the clear and one more 5.8-5.95 s after it; cleared with
    /// ClearAllActions(TRUE) (out of combat until its blow lands) one more 5.85-5.97 s after the blow, and none if the
    /// blow missed; a passive creature being attacked gets one every 6.0 s (combat_round defender.round).
    /// </summary>
    void DefenderRound(Creature c, CombatState s, double now)
    {
        if (!c.InCombat || c.IsDead) { s.DefenceStart = -1; return; }
        if (s.DefenceStart < 0) { s.DefenceStart = now; return; }
        // No defending rounds during a spell round (its own end fires instead).
        if (now < s.SpellRoundEnd) return;
        if (now - s.DefenceStart < RoundSeconds) return;
        s.DefenceStart += RoundSeconds;
        EndRound(c);
    }

    /// <summary>Resets a creature's per-round allowances (attacks of opportunity, parries, cleaves...) every six seconds.</summary>
    void Counters(CombatState s)
    {
        if (_world.Time - s.CounterStart < RoundSeconds) return;
        s.CounterStart = _world.Time;
        s.AttacksOfOpportunity = s.Parries = s.Deflections = s.Cleaves = s.CircleKicks = 0;
        s.EpicDodgeUsed = false;
    }

    public void Step(float dt)
    {
        double now = _world.Time;
        foreach (var (id, s) in _states.ToList())
        {
            var o = _world.Get(id);
            if (o == null || o.IsDestroyed) { _states.Remove(id); continue; }
            if (o is not Creature c) continue;
            Counters(s);
            if (s.SpellEorDue >= 0 && now >= s.SpellEorDue && !c.IsDead)
            {
                s.SpellEorDue = -1;
                // A non-player creature's Defensive Casting ends with a cast's round too (cast_provoke defidle: set
                // before a cast, it reads off once that cast's round is over, and the next cast provokes).
                if (!c.IsPC && c.ActionModes.Contains(CombatK.ModeDefensiveCast))
                    Requests.AbilityRequests.ApplyCombatMode(_world, c, CombatK.ModeDefensiveCast, false);
                EndRound(c);
                if (_world.Get(s.Target) == null && c.InCombat) s.DefenceStart = now - RoundSeconds / 2;
            }
            if (c.IsDead)
            {
                s.Target = GameObject.InvalidId;
                s.SwingTarget = GameObject.InvalidId;
                if (c.InCombat) { c.InCombat = false; _world.MarkChanged(c); }
                continue;
            }
            var target = _world.Get(s.Target);
            if (target == null || target.Area != c.Area || target.IsDestroyed || HitPoints.IsDead(target))
            {
                if (s.Approach != null) { s.Approach.Cancel(_world, c); s.Approach = null; }
                s.Target = GameObject.InvalidId;
                // A dead target is no attack target any more (nwserver combat_kill); one gone to another area still is,
                // so the stock AI's next round keeps after it (interarea_pursuit).
                if (target == null || target.IsDestroyed || HitPoints.IsDead(target)) s.SwingTarget = GameObject.InvalidId;
                // Weapon hostility lasts a round; script hostility has its independently measured lifetime.
                if (c.InCombat && now - s.LastHostile >= HostilitySeconds && now >= s.ScriptHostilityUntil) { c.InCombat = false; _world.MarkChanged(c); }
                // A round of attacks in progress still ends on time (sooner when the target died with attacks left);
                // then the creature defends while in combat.
                if (s.Pursuing) { s.RoundStart = -1; continue; }
                if (s.RoundStart >= 0)
                {
                    if (s.NextAttack < s.Plan.Count) s.RoundLength = Math.Min(s.RoundLength, CutShortRound(s.Plan.Count, s.NextAttack) + s.PlayerMeleeExtra);
                    if (now - s.RoundStart >= s.RoundLength) FinishRound(c, s);
                }
                DefenderRound(c, s, now);
                continue;
            }
            // While it has an attack order the idle six seconds are held (DefenderRound).
            s.DefenceStart = now;
            if (now < s.SpellRoundEnd) continue;
            if (s.RoundStart >= 0 && now - s.RoundStart >= s.RoundLength)
            {
                DropScriptedModes(c);
                EndRound(c);
                if (_world.Get(s.Target) is not { } still || still.Area != c.Area || HitPoints.IsDead(still)) { s.RoundStart = -1; continue; }
                target = still;
                StartRound(c, s);
            }
            if (!CanAttack(c)) continue;

            var main = Creature?.Weapons(c).Main;
            bool ranged = main != null && (Creature?.Stats(main).Ranged ?? false);
            float reach = ranged ? RangedReach : MeleeReach(c, target);
            float stand = ranged ? RangedDistance(c, target) : MeleeDistance(c, target);
            float dist = Distance2(c, target);
            // Rounds then follow one another while the fight lasts, wherever the target goes.
            if (s.RoundStart < 0 && dist <= reach && s.Approach == null) StartRound(c, s);
            // Out of reach, close in; once closing in, go all the way to where it fights from (unless blocked).
            if (dist > reach || (s.Approach != null && dist > stand + 0.05f))
            {
                // Close in, unless the creature is busy with something other than fighting.
                if (c.Actions.Count == 0 || c.Actions[0].ActionType == 3)
                {
                    // Attackers come up to the two creatures' personal spaces (not the 0.3 m more of a plain move).
                    s.Approach ??= new MoveAction
                    {
                        // Reach is measured to a door's origin too: approaching its use point instead could stop out of
                        // reach of a door faced head-on (door_bashed BA), and the attacker would stand there for good.
                        TargetObject = target.Id, TargetOrigin = target is Placeable or Door, Range = stand, Run = true, Timeout = ranged ? 30 : 6,
                        MinRange = target is Creature tc && !ranged ? MoveAction.PersonalSpace(_world, c) + MoveAction.PersonalSpace(_world, tc) : null,
                    };
                    if (s.Approach.Step(_world, c, dt) != ActionStatus.InProgress) s.Approach = null;
                }
                continue;
            }
            if (s.Approach != null) { s.Approach.Cancel(_world, c); s.Approach = null; }
            var times = AttackTimes(s.Plan.Count);
            // Hasted, every swing comes 0.2 s earlier in the round: the first 0.13 s after the round's start, not 0.3
            // (nwserver round_clear re.2.haste, a one-attack bandit hasted: swings 0.0-0.2 s and 2.2-2.3 s into each
            // round).
            if (Rules is CreatureCombatRules && CreatureRules.IsHasted(c))
                times = times.Select(t => Math.Max(0.05f, t - HasteSwingLead)).ToArray();
            bool attackDue = s.NextAttack < s.Plan.Count && now >= s.RoundStart + times[s.NextAttack];
            // Between rounds the attacker steps round its target, facing it all the while.
            if (!attackDue) StepAround(c, s, target, now);
            var face = new Vector2(target.Position.X - c.Position.X, target.Position.Y - c.Position.Y);
            if (s.StandFacing != null && Vector3.DistanceSquared(c.Position, s.StandPos) > 1e-4f) s.StandFacing = null;
            if (face.LengthSquared() > 1e-4f)
            {
                // Players see the turn back to the target (after a step, or as the target moves) only if it is sent.
                float facing = MathF.Atan2(face.Y, face.X);
                bool turned = MathF.Abs(MathF.IEEERemainder(facing - c.Facing, 2 * MathF.PI)) > 0.01f;
                if (turned && s.StandFacing == null) { s.StandFacing = c.Facing; s.StandPos = s.StandOrigin = c.Position; }
                c.Facing = facing;
                if (turned) _world.MarkChanged(c);
            }

            float swing = s.RoundLength / Math.Max(1, s.Plan.Count);
            ExtraOpportunity(c, s, now, swing);
            while (s.NextAttack < s.Plan.Count && now >= s.RoundStart + times[s.NextAttack])
            {
                var planned = s.Plan[s.NextAttack++];
                PerformPlanned(c, target, planned, swing);
                if (HitPoints.IsDead(target) || _world.Get(s.Target) != target || c.IsDead) break;
            }
            if (!s.StepsPlanned && s.NextAttack >= s.Plan.Count && s.Plan.Count > 0 && !c.IsDead && _world.Get(s.Target) == target)
                PlanSteps(c, s, target, ranged);
        }
        StepThreats();
    }

    /// <summary>Creatures that are helpless, dazed, frightened, turned or knocked down don't attack.</summary>
    public static bool CanAttack(Creature c)
    {
        if (c.IsDead || c.Player?.IsAreaLoading == true || CombatVision.Helpless(c)) return false;
        foreach (var e in c.Effects)
            if (e.Type is CombatK.EffectDazed or CombatK.EffectFrightened or CombatK.EffectTurned or CombatK.EffectKnockdown) return false;
        return true;
    }

    static float Distance2(GameObject a, GameObject b) =>
        Vector2.Distance(new(a.Position.X, a.Position.Y), new(b.Position.X, b.Position.Y));

    /// <summary>Rough personal-space radius from the creature size.</summary>
    static float Radius(GameObject o) => o is Creature c ? 0.15f * Math.Max(1, c.Size) : 0.5f;

    /// <summary>
    /// Where a creature stands to hit in melee, as measured on nwserver: its weapon's baseitems PrefAttackDist (four
    /// fifths of it for small creatures) or, unarmed, its appearance's PREFATCKDIST, plus the target's HITDIST.
    /// </summary>
    public float MeleeDistance(Creature c, GameObject target)
    {
        float pref = PreferredAttackDistance(c);
        if (Creature is { } r && r.Weapons(c).Main is { } w && !r.Stats(w).Ranged)
            pref = r.Stats(w).PreferredDistance * (c.Size <= 2 ? 0.8f : 1f);
        // Placeables have no creature HITDIST: native combat_placeables stops at the weapon's preferred distance.
        float hit = target is Creature t ? HitDistance(t) : target is Placeable ? 0f : 0.5f;
        return pref + hit;
    }

    /// <summary>appearance.2da HITDIST, or the creature's own (NWNX_Creature_SetHitDistance).</summary>
    public float HitDistance(Creature c) =>
        c.Overrides?.HitDistance ?? _world.Resources.TwoDA("appearance").GetFloat(c.AppearanceType, "HITDIST", 0.3f);

    /// <summary>appearance.2da PREFATCKDIST, or the creature's own (NWNX_Creature_SetPreferredAttackDistance).</summary>
    public float PreferredAttackDistance(Creature c) =>
        c.Overrides?.PreferredAttackDistance ?? _world.Resources.TwoDA("appearance").GetFloat(c.AppearanceType, "PREFATCKDIST", 1.0f);

    /// <summary>
    /// How far away a creature can attack a target (NWNX_Creature_GetMaxAttackRange): the ranged reach with a ranged
    /// weapon, else where it stands to fight plus 1.5 m (nwserver nwnx_creature_misc: a bandit with a longsword
    /// 3.3 m from a goblin, 4.1 m from a red dragon).
    /// </summary>
    public float MaxAttackRange(Creature c, GameObject target) =>
        Creature is { } r && r.Weapons(c).Main is { } w && r.Stats(w).Ranged ? RangedReach : MeleeDistance(c, target) + 1.5f;

    /// <summary>Measured ranged closing distance: authored PrefAttackDist plus target HITDIST, kept inside the ranged reach.</summary>
    public float RangedDistance(Creature c, GameObject target)
    {
        float pref = Creature?.Stats(Creature.Weapons(c).Main).PreferredDistance ?? 30f;
        if (!float.IsFinite(pref) || pref < 0) pref = 30f;
        float hit = target is Creature t ? HitDistance(t) : 0f;
        // Stand inside the ranged reach, or an authored PrefAttackDist beyond it would stall the attacker.
        return Math.Min(pref + hit, RangedReach - 0.5f);
    }

    /// <summary>How far a creature can be and still hit in melee: a little beyond where it stands to fight.</summary>
    public float MeleeReach(Creature c, GameObject target) => MeleeDistance(c, target) + 0.5f;

    /// <summary>
    /// The target GetAttackTarget reports. A creature that can't move (cutscene immobilised, held, ...) and whose target
    /// is out of its reach keeps its attack action but has no attack target (nwserver, cond_misc cutimm.far).
    /// </summary>
    public uint ReportedTarget(Creature c)
    {
        var s = State(c);
        if (_world.Get(s.Target) is not { } t) return GameObject.InvalidId;
        if (CreatureRules.MovementSpeedFactor(_world, c) > 0) return s.Target;
        var main = Creature?.Weapons(c).Main;
        bool ranged = main != null && (Creature?.Stats(main).Ranged ?? false);
        return Distance2(c, t) <= (ranged ? RangedReach : MeleeReach(c, t)) ? s.Target : GameObject.InvalidId;
    }

    void EndRound(Creature c) => _world.RunEvent(c, "ScriptEndRound", new EventContext());

    /// <summary>
    /// A creature that isn't a player loses the combat modes a script gave it (SetActionMode: Power Attack, Improved
    /// Power Attack, Expertise, Improved Expertise, Parry) when a round of its attacks ends; meanwhile Power Attack does
    /// nothing for it (<see cref="CreatureCombatRules.ModeAttackModifier"/>). Measured on nwserver (parity suite
    /// combat_modes): the modes read on through the first round and off after it; a defender that isn't attacking
    /// keeps Expertise, and its AC bonus works.
    /// </summary>
    void DropScriptedModes(Creature c)
    {
        if (c.IsPC || c.ActionModes.Count == 0) return;
        bool changed = false;
        foreach (int m in AttackRoundModes) changed |= c.ActionModes.Remove(m);
        if (changed) _world.MarkChanged(c);
    }

    static readonly int[] AttackRoundModes =
        { CombatK.ModePowerAttack, CombatK.ModeImprovedPowerAttack, CombatK.ModeExpertise, CombatK.ModeImprovedExpertise, CombatK.ModeParry,
          CombatK.ModeDefensiveCast };

    /// <summary>Makes one planned attack of the round, applying a queued special attack to the first main-hand attack.</summary>
    void PerformPlanned(Creature c, GameObject target, PlannedAttack planned, float swing)
    {
        var s = State(c);
        var weapon = planned.Weapon;
        if (weapon != null && (weapon.IsDestroyed || !c.Equipment.Values.Contains(weapon)) && Creature is { } r)
        {
            // The weapon was thrown away, used up or disarmed since the round was planned.
            var (main, off, creature) = r.Weapons(c);
            weapon = planned.OffHand ? off : main ?? creature.FirstOrDefault();
            if (planned.OffHand && weapon == null) return;
        }
        if (planned.OpportunityTarget != GameObject.InvalidId)
        {
            // The owed attack of opportunity: at the caster, if it is still there and in reach.
            if (_world.Get(planned.OpportunityTarget) is not Creature caster || caster.IsDead || caster.Area != c.Area
                || Distance2(c, caster) > MeleeReach(c, caster)) return;
            target = caster;
        }
        var a = Rules.Context(c, target, weapon, planned.OffHand, Dice);
        a.Penalty = planned.Penalty;
        if (planned.OpportunityTarget != GameObject.InvalidId) a.Kind = AttackKind.Opportunity;
        if (s.PendingFeat >= 0 && !planned.OffHand)
        {
            ApplyPendingFeat(a, s.PendingFeat, s.PendingSubFeat);
            s.PendingFeat = -1;
        }
        ProvokeUnarmed(a);
        // A shot provokes those threatening the shooter, judged before the shot itself puts its target in combat.
        if (a.Ranged && a.Kind == AttackKind.Normal) ProvokeShot(c);
        if (c.IsDead) return;
        s.SwingTarget = target.Id;
        s.LastSwingAt = _world.Time;
        Resolve(a, swing, a.Ranged ? RangedImpactDelay(a.Distance) : ImpactDelay);
    }
}

/// <summary>Death from combat: delegates to the hit point rules (dying, OnDeath, module death events, effect cleanup).</summary>
public static class Death
{
    public static void Kill(World world, Creature c, GameObject? killer)
    {
        HitPoints.Kill(world, c, killer?.Id ?? GameObject.InvalidId);
        if (c.IsDead) world.System<CombatSystem>().Stop(c);
    }
}
