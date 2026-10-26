using OpenWinter.Game.Inventory;
using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Spells;

/// <summary>
/// Casting a spell (ActionCastSpellAtObject/Location, fake casts, feat spells, item spell properties):
/// pick the slot, walk into range (spells.2da Range), face the target, conjure (ConjTime, ConjAnim and
/// Conj* visuals), cast (CastTime, CastAnim and Cast* visuals, projectile), then run the impact script.
/// Each hit taken while conjuring needs a Concentration check (DC 10 + damage + spell level; failing stops the
/// cast without using the copy being cast up, but costs a plain copy from the previous committed cast's class); defensive casting
/// needs DC 15 + spell level at the end of conjuring, even away from enemies; arcane classes roll arcane spell failure for somatic spells.
/// The slot is spent when conjuring ends, or when the spell is lost (failed, countered, entangled, defensive casting).
/// Recovery and eligibility for the next cast/attack are separate. Script instant casts bypass both.
/// </summary>
public sealed partial class CastSpellAction : GameAction
{
    public int Spell = -1;
    public uint Target = GameObject.InvalidId;
    public Location TargetLocation = Location.Invalid;
    /// <summary>METAMAGIC_* (255 = any).</summary>
    public int MetaMagic = SpellSlots.MetaAny;
    public bool Cheat, Instant, Fake, Spontaneous;
    const int CheatCasterLevel = 10;
    public int DomainLevel;
    /// <summary>PROJECTILE_PATH_TYPE_* override (0 = spells.2da).</summary>
    public int ProjectilePathType;
    /// <summary>CLASS_TYPE_* to cast as; -1 any class (then spell-like abilities); 255 spell-like abilities only.</summary>
    public int Class = -1;
    /// <summary>Feat the spell comes from (ActionUseFeat): no slot is spent and GetSpellFeatId reports it.</summary>
    public int FeatId = -1;
    /// <summary>The player cast it from the spellbook or quickbar (cast-spell request), not a script.</summary>
    public bool FromPlayer;
    /// <summary>One of the polymorphed player's form abilities (polymorph.2da SPELL1-3) from the radial: no slot.</summary>
    public bool FormAbility;
    /// <summary>Caster level for slotless casts (feat spells), or -1 to work it out.</summary>
    public int CasterLevel = -1;
    /// <summary>Item whose cast-spell property is being used (ActionUseItemOnObject/AtLocation).</summary>
    public uint ItemId = GameObject.InvalidId;
    public ItemProperty? ItemProperty;
    public bool DecrementCharges = true;
    /// <summary>ACTION_* reported by GetCurrentAction (ACTION_CASTSPELL, ACTION_ITEMCASTSPELL).</summary>
    public int Type = 4;
    public override int ActionType => Type;

    enum Phase { Start, Approach, Conjure, Cast, Recover }
    Phase _phase;
    float _timer, _round, _sinceConjure, _flight;
    MoveAction? _move;
    SpellSlot? _slot;
    /// <summary>Cast by a polymorphed NPC: the slot isn't spent.</summary>
    bool _polymorphFree;
    SpellCastInfo? _info;
    /// <summary>Damage each hit dealt since the last step (<see cref="TookDamage"/>), checked one by one.</summary>
    List<int>? _hits;
    bool _startingConjure;
    bool _spent, _released, _cancelledReleasePending, _potion, _timedSpell, _potionReady, _scroll, _itemCadence;
    float _conjureSeconds, _initialConjureLifetime;
    bool _selfConjureLifetime, _waitingReady;
    long _presentationId;

    const int EntangledConcentrationDC = 15;
    const int SkillConcentration = 1, ActionModeDefensiveCast = 10, FeatCombatCasting = 7, FeatEpicImprovedCombatCasting = 696;

    bool IsItem => ItemId != GameObject.InvalidId;
    bool UsesSlot => _slot != null && _slot.Kind != SlotKind.Ability;

    /// <summary>The spell context this cast will run with (null until conjuring starts).</summary>
    public SpellCastInfo? Info => _info;

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (_phase >= Phase.Conjure)
        {
            _timer -= dt;
            _sinceConjure += dt;
            if (_timedSpell)
            {
                // Native live-Haste controls move the absolute phase boundaries,
                // even after release; a released impact is never repeated.
                float previousConjure = _conjureSeconds;
                _conjureSeconds = ConjureSeconds(world, owner, _info!.MetaMagic);
                _round = _conjureSeconds + _castSeconds + _recoverySeconds;
                _timer = (_phase == Phase.Conjure ? _conjureSeconds
                    : _phase == Phase.Cast ? _conjureSeconds + _castSeconds : _round) - _sinceConjure;
                // Keep the current preparation clip alive when Haste is removed.
                // Changing its deadline must not restart the authored animation or chant.
                if (_phase == Phase.Conjure && _timer > 0 && previousConjure != _conjureSeconds)
                    world.System<SpellSystem>().Notify("spell-conjure-timing", owner,
                        new { spell = Spell, presentation = _presentationId, remaining = _timer });
                // Native self-cast hands have an initial-duration cap, but track the
                // current total beneath it. A later flip can extend them again up to
                // that cap; this is deliberately not an earliest-ever deadline.
                if (_selfConjureLifetime && previousConjure != _conjureSeconds)
                    world.System<SpellSystem>().Notify("spell-conjure-lifetime", owner,
                        new
                        {
                            spell = Spell, presentation = _presentationId,
                            remaining = Math.Max(0, Math.Min(_initialConjureLifetime, _round) - _sinceConjure)
                        });
                // The measured self-cast body pose follows current recovery, independently
                // of the initial cap on hand models. Do not restart its release movement.
                if (_selfConjureLifetime && _phase is Phase.Cast or Phase.Recover && previousConjure != _conjureSeconds)
                    world.System<SpellSystem>().Notify("spell-cast-timing", owner,
                        new
                        {
                            spell = Spell, presentation = _presentationId,
                            remaining = Math.Max(0, _round - _sinceConjure)
                        });
            }
        }
        for (int guard = 0; guard < 8; guard++)
        {
            switch (_phase)
            {
                case Phase.Start:
                    if (!Begin(world, owner)) return End(world, owner, ActionStatus.Failed);
                    // Casting a spell ends a conversation the caster is part of (ConversationService.Fights; items not measured).
                    if (!IsItem && owner is Creature caster) world.System<Conversations.ConversationService>().Fights(caster);
                    _phase = Phase.Approach;
                    continue;
                case Phase.Approach:
                {
                    var s = Approach(world, owner, dt);
                    if (s == ActionStatus.Failed) return End(world, owner, ActionStatus.Failed);
                    if (s == ActionStatus.InProgress) return s;
                    if ((UsesCadence(owner) || _itemCadence && owner is Creature) && world.Time < world.System<Combat.CombatSystem>().NextCastTime((Creature)owner))
                    {
                        if (!_waitingReady)
                        {
                            _waitingReady = true;
                            world.System<SpellSystem>().Notify("spell-ready", owner, new { active = true });
                        }
                        return ActionStatus.InProgress;
                    }
                    if (!StartConjure(world, owner)) return End(world, owner, ActionStatus.Failed);
                    _phase = Phase.Conjure;
                    continue;
                }
                case Phase.Conjure:
                    if (!Concentrating(world, owner) && InterruptedEvent(world, owner)) return Interrupt(world, owner, "concentration");
                    if (_timer > 0) return ActionStatus.InProgress;
                    if (!StartCast(world, owner)) return End(world, owner, ActionStatus.Failed);
                    _phase = Phase.Cast;
                    continue;
                case Phase.Cast:
                    if (!Concentrating(world, owner) && InterruptedEvent(world, owner)) return Interrupt(world, owner, "concentration");
                    if (_timer > 0) return ActionStatus.InProgress;
                    Release(world, owner);
                    _phase = Phase.Recover;
                    _timer = _round - _sinceConjure;
                    continue;
                case Phase.Recover:
                    if (_timer > 0) return ActionStatus.InProgress;
                    RestartItemRound(world, owner);
                    return End(world, owner, ActionStatus.Done);
            }
        }
        return ActionStatus.InProgress;
    }

    // Native cast_clear: a committed cast retains release and recovery even when
    // ClearAllActions discards successors; newly queued commands wait behind it.
    public override bool RetainsAfterQueueClear(World world, GameObject owner) =>
        (_phase is Phase.Cast or Phase.Recover || _scroll && _phase == Phase.Conjure) && CanAct(world, owner);

    public override void Cancel(World world, GameObject owner)
    {
        _move?.Cancel(world, owner);
        if (_phase == Phase.Cast && !_released)
        {
            // A forced removal outside ordinary queue clearing still preserves the
            // committed release boundary. Normal ClearAllActions retains this action
            // through recovery. Death before release suppresses the spell.
            if (!_cancelledReleasePending)
            {
                _cancelledReleasePending = true;
                world.After(Math.Max(0, _timer), () =>
                {
                    if (_released || world.Get(owner.Id) != owner || !CanAct(world, owner)) return;
                    if (Concentrating(world, owner)) Release(world, owner);
                    else Lose(world, owner, "concentration");
                });
            }
        }
        else if (_phase == Phase.Conjure && !_released)
            world.System<SpellSystem>().Notify("spell-interrupted", owner, new { spell = Spell, reason = "cancelled" });
        if (_phase != Phase.Cast) FailedEvent(world, owner, 0);
        End(world, owner, ActionStatus.Failed);
    }

    /// <summary>
    /// An item use (not a potion) whose round has run out by its last update starts a fresh round there, held for the
    /// use's whole length, so the next queued use waits that long again (nwserver queue_cadence: scrolls with CastTime
    /// 4000, 5000 and 7000 are used 13, 15 and 19 s apart, a wand with CastTime 7000 every 15 s; spell casts don't do
    /// this).
    /// <para>
    /// Single or doubled: the round is still there, and the gap stays single, when the use's last
    /// update lands within its window: from D to D + L whole milliseconds, L being 1 ms when D is longer than the round
    /// by up to a second, else 0. The original's updates fall at arbitrary times, so the window is a 1-2 ms chance per
    /// use (nwserver queue_single: 1 and 13 single gaps in 144 in two runs for CastTime 3500-7000, none of them at 4000). OpenWinter's updates come every 33 ms
    /// aligned with the use's start, so how late the last one lands is drawn evenly within one update instead.
    /// </para>
    /// </summary>
    void RestartItemRound(World world, GameObject owner)
    {
        if (!_itemCadence || owner is not Creature user || user.IsDead) return;
        var combat = world.System<Combat.CombatSystem>();
        if (combat.TryState(user) is not { } state || state.SpellRoundEnd > world.Time) return;
        float round = Combat.CombatSystem.RoundSeconds, over = _round - round;
        if (over >= 0)
        {
            int leftoverMs = over > 0.0005f && over <= 1.0005f ? 1 : 0;
            int lateMs = (int)(UpdateLateness() * 1000f / GameServer.TickRate);
            if (lateMs <= leftoverMs) return;
        }
        combat.BeginSpellRound(user, Math.Max(round, _round));
    }

    /// <summary>Where within one server update an item use's last update lands, 0..1 (tests replace it).</summary>
    public static Func<float> UpdateLateness = () => Random.Shared.NextSingle();

    ActionStatus End(World world, GameObject owner, ActionStatus status)
    {
        if (_waitingReady)
        {
            _waitingReady = false;
            world.System<SpellSystem>().Notify("spell-ready", owner, new { active = false });
        }
        if (_potionReady)
        {
            _potionReady = false;
            world.System<SpellSystem>().Notify("potion-ready", owner, new { active = false });
        }
        owner.Locals.Objects.Remove(AttemptedTargetLocal);
        return status;
    }

    /// <summary>Local the combat package reads for GetAttemptedSpellTarget.</summary>
    public const string AttemptedTargetLocal = "__attempted_spell_target";

    // ------------------------------------------------------------------ phases

    bool Begin(World world, GameObject owner)
    {
        if (!SpellTable.Exists(world, Spell) || owner.Area == null) return false;
        if (owner is Creature { IsDead: true }) return false;
        // bCheat is ignored when a class is given; placeables and other non-creatures have no spellbook and always cheat.
        if (Class != -1) Cheat = false;
        if (owner is not Creature) Cheat = true;
        if (IsItem)
        {
            if (world.Get<Item>(ItemId) is not { } item || world.Inventory.Owner(item) != owner || ItemProperty is not { Useable: true }) return false;
            var style = ItemRules.SpellStyle(item.BaseItem);
            _potion = style == ItemRules.SpellItemStyle.Potion;
            _scroll = style == ItemRules.SpellItemStyle.Scroll;
            _itemCadence = !_potion;
            if (!world.Inventory.CanUseItemProperty(item, ItemProperty)) return Feedback(world, owner, "That item has no uses left.");
        }

        var target = world.Get(Target);
        if (Target != GameObject.InvalidId && target == null) return false;
        if ((target == null && TargetLocation.Area == GameObject.InvalidId) || SpellTable.IsPersonal(world, Spell))
        {
            target = owner;
            Target = owner.Id;
        }
        if (target != null)
        {
            var where = target is Item { Area: null } ti ? world.Inventory.Owner(ti) : target;
            if (where == null || where.Area != owner.Area) return false;
            // Like the original, a creature can only cast at another creature it sees (measured on NPCs;
            // players pick their targets from what they see).
            if (owner is Creature { IsPC: false } seer && target is Creature other && other != seer && !seer.Society.Seen.Contains(other.Id)) return false;
        }
        else if (TargetLocation.Area != owner.Area.Id) return false;

        if (FormAbility && (owner is not Creature formed || !Rules.EffectSystem.FormAbilities(world, formed).Contains(Spell))) return false;
        if (!Fake && !Cheat && FeatId < 0 && !IsItem && !FormAbility)
        {
            if (owner is not Creature c) return false;
            // A polymorphed player casts nothing from its spellbook, silently (visual scene polymorphpc: assigned
            // casts of a spell she has and of the form's Dispel Magic, and a greyed quickbar spell).
            if (c.IsPC && c.PolymorphForm != null) return false;
            _slot = SpellSlots.Find(world, c, Spell, MetaMagic, Class, DomainLevel, Spontaneous, anyMetaInClass: FromPlayer);
            if (_slot == null) return Feedback(world, owner, "You cannot cast that spell now.");
        }
        if (!Fake && owner is Creature cc && UsesSlot)
        {
            var k = EffectConstants.Of(world);
            if (SpellTable.Verbal(world, Spell) && _slot!.Meta != NwRules.MetaSilent && EffectQueries.Has(cc, k["EFFECT_TYPE_SILENCE"]))
                return Feedback(world, owner, "You cannot cast spells while silenced.");
            // A polymorphed NPC still casts the spells it has, and doesn't use them up (parity suite
            // fx_poly_abilities: bear102); a player was refused above.
            if (EffectQueries.Has(cc, k["EFFECT_TYPE_POLYMORPH"]))
            {
                if (cc.IsPC) return false;
                _polymorphFree = true;
            }
        }
        if (!CanAct(world, owner)) return false;
        owner.Locals.Objects[AttemptedTargetLocal] = Target;
        return true;
    }

    GameObject? TargetObject(World world) => world.Get(Target);

    /// <summary>Where the spell is aimed (the holder of a targeted item in an inventory).</summary>
    Vector3 AimPoint(World world)
    {
        var t = TargetObject(world);
        if (t is Item { Area: null } item && world.Inventory.Owner(item) is { } holder) return holder.Position;
        return t?.Position ?? TargetLocation.Position;
    }

    ActionStatus Approach(World world, GameObject owner, float dt)
    {
        if (owner is not Creature || Instant || Target == owner.Id) return ActionStatus.Done;
        var point = AimPoint(world);
        float range = SpellTable.Range(world, Spell);
        float dist = Vector2.Distance(new(owner.Position.X, owner.Position.Y), new(point.X, point.Y));
        var t = TargetObject(world);
        // A creature is cast at from the spell's range plus both creatures' CREPERSPACE, less 0.2 m (parity suite
        // cast_reach: Light, touch 2.25 m, bandit at bandit 3.05 m, ogre at fire giant 4.65 m; Haste 8.8 m).
        float margin = t == null ? 0.3f
            : t is Creature tc ? MoveAction.PersonalSpace(world, (Creature)owner) + MoveAction.PersonalSpace(world, tc) - 0.2f
            : 0.25f;
        if (dist <= range + margin + 0.001f)
        {
            Movement.Stop((Creature)owner);
            _move = null;
            return ActionStatus.Done;
        }
        // A location move ignores MoveAction.Range, so it walks (and paths) toward the spell's
        // centre; the range check above stops it at the measured range + margin each tick.
        // Aiming at a computed stopping point instead could leave it on unwalkable ground.
        _move ??= new MoveAction
        {
            TargetObject = t is Item { Area: null } ? world.Inventory.Owner((Item)t)?.Id ?? GameObject.InvalidId : t?.Id ?? GameObject.InvalidId,
            // A creature is walked up to exactly the casting distance, so the caster brakes to a stop there as from
            // any move: on nwserver the cast after a walk starts as late as an ActionMoveToObject to that distance
            // would end (breath_timing: a red dragon 12 m out walks 7.55 m in 2.1 s, then breathes 2.5 s later).
            Target = point, Range = t is Creature ? range + margin : Math.Max(0.5f, range * 0.9f), Timeout = 30f,
            // Casters run into range (cast_crowd: about 4 m/s from 8 m out, the Light landing 1.8 s after arrival).
            Run = true, RetryWait = false,
        };
        var s = _move.Step(world, owner, dt);
        if (s != ActionStatus.InProgress) _move = null;
        // Blocked by another creature on the way (casters crowding one target): wait a moment and go round, as the
        // original's casters do (cast_crowd: six casters abreast all reach a touch target), rather than give up.
        if (s == ActionStatus.Failed && owner is Creature { } blocked && blocked.LastBlockedAt == world.GameTime && t != null && !t.IsDestroyed
            && (_blockedSince ??= world.Time) > world.Time - 30)
            return ActionStatus.InProgress;
        return s;
    }

    double? _blockedSince;

    // Native turn_queue measures Turn Undead alongside ordinary spell cadence.
    // Do not infer timing for every spell-like feat from this one control.
    bool UsesCadence(GameObject owner) => owner is Creature && !Instant && !Fake && !IsItem
        && (FeatId < 0 || FeatId == NwRules.FeatTurnUndead);

    float ConjureSeconds(World world, GameObject owner, int meta)
    {
        float authored = SpellTable.ConjureSeconds(world, Spell);
        // The stock rule's "minimum" name is misleading: measured 0/200/1500/4000ms
        // controls cap Quicken at 500ms and preserve authored times below the cap.
        if (meta == NwRules.MetaQuicken)
            return Math.Min(authored, Ruleset.Float(world, "QUICKENED_SPELL_MINIMUM_CONJURE_TIME", 500) / 1000f);
        return owner is Creature c && CreatureRules.IsHasted(c)
            ? authored * Ruleset.Float(world, "HASTED_SPELL_CONJURE_TIME_MODIFIER", 0.5f) : authored;
    }

    bool StartConjure(World world, GameObject owner)
    {
        if (!CanAct(world, owner)) return false;
        if (FeatId == NwRules.FeatTurnUndead && owner is Creature featUser
            && !CreatureRules.HasFeat(world, featUser, FeatId)) return false;
        BroadcastEvent(world, owner);
        // Casting a spell ends stealth as the casting starts, and GetDetectMode reads 2 from then on
        // (nwserver, stealth_modes; an item's spell ends it when used, in StartCast).
        if (!IsItem && owner is Creature stealthy)
        {
            stealthy.DetectAlert = true;
            world.System<Skills.SkillSystem>().BreakStealth(stealthy);
        }
        var target = TargetObject(world);
        Face(world, owner, AimPoint(world));
        var spells = world.Resources;
        // A cast without a slot (bCheat, feats, items) has no metamagic, whatever the script asked for.
        int meta = _slot?.Meta ?? NwRules.MetaNone;
        int cls = _slot?.Class ?? (Class >= 0 && !Cheat ? Class : NwRules.ClassInvalid);
        int level = _slot?.SpellLevel ?? -1;
        if (level < 0 && DomainLevel > 0 && FeatId < 0 && !IsItem) level = DomainLevel;
        if (level < 0 && cls != NwRules.ClassInvalid) level = CreatureRules.SpellLevelForClass(world, cls, Spell);
        if (level < 0) level = SpellTable.Innate(world, Spell);
        // A cheat cast is at caster level 10 with a save DC of 13 + the innate level, whoever casts it.
        bool cheat = _slot == null && Cheat && FeatId < 0 && !IsItem && owner is Creature;
        int casterLevel = CasterLevel >= 0 ? CasterLevel : _slot?.CasterLevel
            ?? (cheat ? CheatCasterLevel : SpellSlots.CheatCasterLevel(world, owner, Spell));
        _info = new SpellCastInfo
        {
            SpellId = Spell,
            CasterLevel = Math.Max(1, casterLevel),
            MetaMagic = meta,
            SaveDC = cheat ? 13 + SpellTable.Innate(world, Spell) : -1,
            FeatId = FeatId,
            ClassType = cls,
            SpellLevel = level,
            Spontaneous = _slot?.Spontaneous ?? false,
            DomainLevel = _slot is { Domain: true } ? _slot.SpellLevel : 0,
            Instant = Instant,
            CastItem = IsItem ? ItemId : GameObject.InvalidId,
            Target = target?.Id ?? GameObject.InvalidId,
            TargetLocation = target != null ? world.System<SpellSystem>().TargetLocationOf(target) : TargetLocation,
        };

        _timedSpell = UsesCadence(owner);
        // Correlate release with its preparation even if a later action starts first.
        if (_timedSpell) _presentationId = world.NextEventSequence(0);
        bool quick = Instant || (!_timedSpell && meta == NwRules.MetaQuicken);
        // Native potion_times/potionpc: drinking ignores spell-table times. The first
        // second is cancellable; release is at 1.75s and the action ends at 2.5s, even in combat.
        float conjure = _timedSpell ? ConjureSeconds(world, owner, meta) : _potion ? 1f : _scroll ? 2f : quick || IsItem ? 0f : SpellTable.ConjureSeconds(world, Spell);
        _conjureSeconds = conjure;
        float cast = _potion ? 0.75f : quick ? 0f : SpellTable.CastSeconds(world, Spell);
        bool inCombat = owner is Creature cr && world.System<Combat.CombatSystem>().IsInCombat(cr);
        // Native cast_clock varies authored ConjTime/CastTime independently: impact is always
        // ConjTime + 0.5s, then CastTime is spent recovering. Blank times are zero, not defaults.
        float release = _potion ? 0.75f : quick ? 0 : 0.5f;
        _round = quick ? 0 : conjure + release + cast;
        if (inCombat && !_timedSpell && !quick && !Fake && !_potion) _round = Math.Max(_round, Combat.CombatSystem.RoundSeconds);
        if (_timedSpell)
        {
            var c = (Creature)owner;
            bool fast = meta == NwRules.MetaQuicken || CreatureRules.IsHasted(c);
            float length = fast ? Ruleset.Float(world, "FAST_SPELLCAST_ROUND", 3000) / 1000f : Combat.CombatSystem.RoundSeconds;
            // A cast longer than its round stretches the round to its end (nwserver queue_cadence: ConjTime 1500 +
            // CastTime 5000 ends its rounds 7 s apart, and the next cast starts then).
            world.System<Combat.CombatSystem>().BeginSpellRound(c, Math.Max(length, _round));
        }
        else if (_itemCadence && owner is Creature itemUser)
        {
            // Native item_use_clock/more: scrolls, wands, equipped staves and rods
            // reserve the ordinary six-second round even when Haste is present, stretched to the use's end when the
            // use is longer, and it ends with OnEndCombatRound (queue_cadence).
            world.System<Combat.CombatSystem>().BeginSpellRound(itemUser, Math.Max(Combat.CombatSystem.RoundSeconds, _round));
        }
        _sinceConjure = 0;
        _hits = null;

        if (!Fake && !IsItem)
        {
            var counter = world.System<SpellSystem>().TryCounter(owner, _info);
            if (counter != null) return Lose(world, owner, "countered");
            // Casting a spell provokes attacks of opportunity, for players and NPCs alike, from a class slot or as a
            // cheat cast (nwserver strike_back: an NPC wizard's second Magic Missile next to a creature its first had
            // hurt drew its attack, cheat cast or not), but not in Defensive Casting mode (strike_back adj.def), nor a
            // spell-like ability, a feat, an item or anything that isn't a real spell (spells.2da UserType 1), nor with
            // Epic Improved Combat Casting, nor quickened unless the caster is hasted.
            if (owner is Creature caster && (UsesSlot || _slot == null && Cheat) && FeatId < 0 && !FormAbility
                && SpellTable.Table(world).GetInt(Spell, "UserType", 1) == 1 && !caster.ActionModes.Contains(ActionModeDefensiveCast)
                && !CreatureRules.HasFeat(world, caster, FeatEpicImprovedCombatCasting, ignoreUses: true)
                && (meta != NwRules.MetaQuicken || CreatureRules.IsHasted(caster)))
            {
                _startingConjure = true;
                try { world.System<Combat.CombatSystem>().ProvokeAttacksOfOpportunity(caster, "spell"); }
                finally { _startingConjure = false; }
            }
        }

        var row = SpellTable.Table(world);
        _selfConjureLifetime = _timedSpell && Target == owner.Id && conjure > 0 && row.Get(Spell, "CastAnim").Equals("self", StringComparison.OrdinalIgnoreCase);
        _initialConjureLifetime = _round;
        bool silent = meta == NwRules.MetaSilent;
        string voice = silent ? "" : row.Get(Spell, owner is Creature { Gender: 1 } ? "ConjSoundFemale" : "ConjSoundMale");
        int anim = SpellTable.ConjureAnimation(row.Get(Spell, "ConjAnim"));
        // Native scrollphase: the two-second read clip is preparation, followed
        // by castout even for a self-targeted Mage Armor scroll.
        if (_scroll) world.Output?.PlayAnimation(owner, 112, 1, conjure);
        if (conjure > 0 && !IsItem)
        {
            if (anim >= 0) world.Output?.PlayAnimation(owner, anim, 1, conjure);
            world.System<SpellSystem>().Notify("spell-conjure", owner, new
            {
                spell = Spell, presentation = _presentationId, target = Target, pos = SpellSystem.Vec(AimPoint(world)), duration = conjure,
                conjureLifetime = _selfConjureLifetime ? (float?)_initialConjureLifetime : null,
                animation = row.Get(Spell, "ConjAnim"), headVfx = row.Get(Spell, "ConjHeadVisual"), handVfx = row.Get(Spell, "ConjHandVisual"),
                groundVfx = row.Get(Spell, "ConjGrndVisual"), sound = row.Get(Spell, "ConjSoundVFX"), voice, metaMagic = meta, fake = Fake,
            });
        }
        if (_potion)
        {
            // Native potionstance: weapon-ready pose during preparation/drinking, while
            // GetIsInCombat remains false. This is presentation, not combat state.
            _potionReady = true;
            world.System<SpellSystem>().Notify("potion-ready", owner, new { active = true });
        }
        _timer = conjure;
        _castSeconds = release;
        _recoverySeconds = cast;
        // "X casting <spell>" to the players near, each by her Spellcraft (castlines); instant casts, items, creature
        // abilities and feats (spells.2da UserType 2 and 3) print none (castlines3).
        if (owner is Creature && !Fake && !IsItem && !Instant && !_potion && !_scroll && FeatId < 0 && SpellTable.UserType(world, Spell) == 1)
            _castingLines = CastingLines.Start(world, owner, Spell);
        return true;
    }

    /// <summary>Who identified the spell when its "casting" line was printed (null: the cast prints no lines).</summary>
    Dictionary<uint, bool>? _castingLines;

    float _castSeconds, _recoverySeconds;

    bool StartCast(World world, GameObject owner)
    {
        if (!CanAct(world, owner)) return Lose(world, owner, "interrupted");
        // A target destroyed while the caster conjured: the cast stops as conjuring ends, with nothing spent
        // (cast_provoke selfat: the victim destroyed mid-conjure took no impact).
        if (Target != GameObject.InvalidId && world.Get(Target) == null) return false;
        if (IsItem && owner is Creature user) world.System<Skills.SkillSystem>().BreakStealth(user);
        if (FeatId == NwRules.FeatTurnUndead && owner is Creature featUser
            && !CreatureRules.HasFeat(world, featUser, FeatId)) return false;
        // Measured with a real PC and memorized spells (visual scene defcast): this check happens
        // after conjuring, even with no enemy in melee range. Failure still spends the spell slot.
        if (!Fake && !IsItem && UsesSlot && owner is Creature caster && caster.ActionModes.Contains(ActionModeDefensiveCast))
        {
            int dc = Ruleset.Int(world, "DEFENSIVE_CASTING_BASE_DC", 15) + _info!.SpellLevel;
            // Native feedback raises the DC by four near an enemy unless Combat Casting is owned.
            if (!CreatureRules.HasFeat(world, caster, FeatCombatCasting, ignoreUses: true) && EnemyNear(world, caster))
                dc += Ruleset.Int(world, "CONCENTRATION_CHECK_MELEE_PENALTY", 4);
            if (!CreatureRules.SkillCheck(world, caster, SkillConcentration, dc))
                return Lose(world, owner, "defensive-casting");
            if (caster.IsPC) world.Output?.SendMessage(caster, world.Resources.TlkString(12332));
        }
        // An entangled caster makes one Concentration check against a flat 15 as conjuring ends, for spells that
        // use concentration; failing uses the spell up (nwserver cast_concentration ent*:
        // Concentration -1 cast 2 of 14 level-9 spells, so the spell's level isn't added; Concentration 36 always
        // and a UseConcentration 0 spell always).
        if (!Fake && !IsItem && owner is Creature tangled && SpellTable.UsesConcentration(world, Spell)
            && EffectQueries.Has(tangled, NwRules.EffectEntangle)
            && !CreatureRules.SkillCheck(world, tangled, SkillConcentration, EntangledConcentrationDC))
            return Lose(world, owner, "entangled");
        if (!Fake && owner is Creature c && UsesSlot && FailureRoll(world, c))
            return Lose(world, owner, "failure");
        if (!Fake && owner is Creature deaf && DeafFailure(world, deaf))
            return Lose(world, owner, "deaf");
        Spend(world, owner);

        var row = SpellTable.Table(world);
        int anim = SpellTable.CastAnimation(row.Get(Spell, "CastAnim"));
        if (IsItem && world.Get<Item>(ItemId) is { } item)
            anim = ItemRules.IsPotion(item.BaseItem) ? 113 : _scroll ? SpellTable.CastAnimation("out")
                : ItemRules.SpellStyle(item.BaseItem) == ItemRules.SpellItemStyle.CraftedScroll ? 112 : anim;
        float cast = _castSeconds;
        if (anim >= 0) world.Output?.PlayAnimation(owner, anim, 1, cast + _recoverySeconds);
        // Native Seeker Arrow uses the equipped bow's ordinary shot clip once.
        // Resolve from authoritative equipment at release preparation, so an equip
        // immediately before casting cannot race the client's appearance update.
        // Other attack-style wield families have not yet been measured.
        string attackAnimation = "";
        if (cast > 0 && !IsItem && row.Get(Spell, "CastAnim").Equals("attack", StringComparison.OrdinalIgnoreCase)
            && owner is Creature weaponCaster
            && world.System<Combat.CombatSystem>().Rules is Combat.CreatureCombatRules weaponRules
            && weaponRules.Stats(weaponRules.Weapons(weaponCaster).Main).IsBow)
            attackAnimation = "bowshot";
        var aim = AimPoint(world);
        world.System<SpellSystem>().Notify("spell-cast", owner, new
        {
            spell = Spell, presentation = _presentationId, target = Target, pos = SpellSystem.Vec(aim), duration = cast, recovery = _recoverySeconds,
            animation = row.Get(Spell, "CastAnim"), attackAnimation, headVfx = row.Get(Spell, "CastHeadVisual"), handVfx = row.Get(Spell, "CastHandVisual"),
            groundVfx = row.Get(Spell, "CastGrndVisual"), sound = row.Get(Spell, "CastSound"), item = ItemId, fake = Fake,
        });
        _flight = 0;
        int pathOverride = Nwnx.Plugins.ObjectPlugin.ProjectilePathOverride(world, owner, Spell);
        string path = SpellTable.ProjectilePath(row.Get(Spell, "ProjType"), pathOverride >= 0 ? pathOverride : ProjectilePathType);
        if (SpellTable.HasProjectile(world, Spell) && (Target != owner.Id || path == "highballistic"))
        {
            float dist = Vector3.Distance(owner.Position, aim);
            _flight = SpellTable.ProjectileFlightTime(dist, path);
            world.System<SpellSystem>().Notify("spell-projectile", owner, new
            {
                spell = Spell, presentation = _presentationId, target = Target, from = SpellSystem.Vec(owner.Position), to = SpellSystem.Vec(aim),
                model = row.Get(Spell, "ProjModel"), path,
                showModel = row.GetInt(Spell, "HasProjectile", 0) != 0,
                spawnPoint = row.Get(Spell, "ProjSpwnPoint"), sound = row.Get(Spell, "ProjSound"), orientation = row.Get(Spell, "ProjOrientation"),
                delay = cast, travel = _flight,
            });
        }
        _timer = _timedSpell ? _conjureSeconds + cast - _sinceConjure : cast;
        return true;
    }

    /// <summary>The spell goes off: the impact script runs (after the projectile's flight), or the item is used.</summary>
    void Release(World world, GameObject owner)
    {
        if (_released) return;
        _released = true;
        // Native live-Haste PCM follows the actual release boundary, including an
        // immediate release when Haste moves that boundary into the past.
        if (_presentationId != 0)
            world.System<SpellSystem>().Notify("spell-release", owner, new { spell = Spell, presentation = _presentationId });
        if (Fake || _info == null) return;
        if (!CastEvent(world, owner)) return;
        // "X casts <spell>" at the release, before the impact's own lines (castlines).
        if (_castingLines != null) CastingLines.Release(world, owner, Spell, _castingLines);
        // A creature ability or a feat (and a form ability) says spells.2da AltMessage as it goes off instead ("X is
        // surrounded by an aura.", "X summons familiar.": castlines3; "000 Polymorph Probe Two uses psionic ability.":
        // polymorphpc2).
        if (!IsItem && (FormAbility || SpellTable.UserType(world, Spell) != 1)) CastingLines.AltMessage(world, owner, Spell);
        var info = _info;
        uint casterId = owner.Id;
        void Impact()
        {
            if (world.Get(casterId) is not { } caster) return;
            if (IsItem)
            {
                if (world.Get<Item>(ItemId) is not { } item || world.Inventory.Owner(item) != caster || ItemProperty == null) return;
                var prop = item.Properties.FirstOrDefault(p => ReferenceEquals(p, ItemProperty))
                    ?? item.Properties.FirstOrDefault(p => p.Type == ItemProperty.Type && p.SubType == ItemProperty.SubType && p.CostTableValue == ItemProperty.CostTableValue);
                if (prop != null) world.Inventory.UseItemProperty(caster, item, prop, Target, info.TargetLocation, DecrementCharges);
                return;
            }
            world.System<SpellSystem>().RunImpact(caster, info);
        }
        // The original's 1 ms flight (a speed of zero or less) lands in the same update as the release.
        SafeProjectile(world, owner);
        if (_flight > 0.001f) world.After(_flight, Impact);
        else Impact();
    }

    // ------------------------------------------------------------------ rules

    void Spend(World world, GameObject owner)
    {
        if (_spent) return;
        _spent = true;
        // The cast reached its commit point: its class is what a later failed Concentration check costs.
        if (!Fake && !IsItem && owner is Creature committed)
            committed.LastCommittedCastClass = _slot?.Kind == SlotKind.Ability ? NwRules.ClassInvalid : _slot?.Class ?? -1;
        // A subscriber can keep the slot (NWNX_ON_DECREMENT_SPELL_COUNT): only a slot really spent is given back later.
        _slotSpent = owner is Creature c && _slot != null && !_polymorphFree && DecrementEvent(world, c);
        if (!Fake && FeatId == NwRules.FeatTurnUndead && owner is Creature featUser)
            CreatureRules.AdjustFeatUses(world, featUser, FeatId, -1);
    }

    /// <summary>The spell is lost: the slot is spent and casting stops (unless <paramref name="spend"/> is false:
    /// the slot is kept, or given back if conjuring already ended).</summary>
    bool Lose(World world, GameObject owner, string reason, bool spend = true)
    {
        if (spend) Spend(world, owner);
        else Refund(world, owner);
        if (reason != "concentration") FailedEvent(world, owner, FailReason(reason));
        world.System<SpellSystem>().Notify("spell-interrupted", owner, new { spell = Spell, reason });
        if (owner is Creature { IsPC: true } pc)
            world.Output?.SendMessage(pc, reason switch
            {
                "failure" or "deaf" => "Spell failed!",
                "countered" => "Your spell was countered.",
                "defensive-casting" => world.Resources.TlkString(1768),
                _ => "Spell interrupted.",
            });
        return false;
    }

    void Refund(World world, GameObject owner)
    {
        if (!_spent) return;
        _spent = false;
        if (_slotSpent && owner is Creature c && _slot != null) SpellSlots.Restore(world, c, _slot);
        _slotSpent = false;
    }

    ActionStatus Interrupt(World world, GameObject owner, string reason)
    {
        // A failed Concentration check doesn't use up the copy being cast: nothing has been spent before the commit
        // point. Instead a plain copy of the spell is taken from the class of the creature's previous committed cast
        // (ConcentrationCost).
        Lose(world, owner, reason, spend: reason != "concentration");
        if (_failedCheck && owner is Creature c) ConcentrationCost(world, c);
        return End(world, owner, ActionStatus.Failed);
    }

    bool _failedCheck;

    /// <summary>
    /// What a failed damage Concentration check costs (nwserver conc_previous: nothing on a creature's first cast, nor
    /// after only interrupted casts; after a completed wizard cast, one plain copy of the spell un-readied; an
    /// empowered copy being cast stays prepared and a plain copy is lost instead, or nothing if there is none). The
    /// class is the one the creature's previous committed cast came from: a preparing class loses one ready copy of the
    /// spell at its normal level with no metamagic, a spontaneous one a use of that level, and after a spell-like
    /// ability the creature's ability of this spell (the last two not measured).
    /// </summary>
    void ConcentrationCost(World world, Creature c)
    {
        int cls = c.LastCommittedCastClass;
        if (cls < 0) return;
        if (cls == NwRules.ClassInvalid)
        {
            if (c.SpellAbilities.FirstOrDefault(a => a.Spell == Spell && a.Ready) is { } ability) ability.Ready = false;
            return;
        }
        int ci = c.Classes.FindIndex(x => x.Class == cls);
        int level = CreatureRules.SpellLevelForClass(world, cls, Spell);
        if (ci < 0 || level < 0 || level > 9) return;
        if (CreatureRules.MemorizesSpells(world, cls))
        {
            var list = c.Classes[ci].Memorized[level];
            for (int i = 0; i < list.Count; i++)
                if (list[i].Spell == Spell && list[i].Ready && list[i].Meta == NwRules.MetaNone)
                {
                    list[i] = (list[i].Spell, list[i].Meta, false);
                    return;
                }
            return;
        }
        if (!c.Classes[ci].KnownSpells[level].Contains(Spell)) return;
        int left = CreatureRules.SlotsLeft(world, c, ci, level);
        if (left > 0) c.SpellSlotsLeft[(ci, level)] = left - 1;
    }

    /// <summary>
    /// The spell level a damage Concentration check adds: the spell's level for the class of the creature's previous
    /// committed cast, else its innate level; metamagic doesn't raise it.
    /// </summary>
    int ConcentrationLevel(World world, Creature c)
    {
        int cls = c.LastCommittedCastClass;
        int level = cls >= 0 && cls != NwRules.ClassInvalid ? CreatureRules.SpellLevelForClass(world, cls, Spell) : -1;
        return level >= 0 ? level : SpellTable.Innate(world, Spell);
    }

    /// <summary>A hit landed on the caster while this cast is its current action (HitPoints.Damage).</summary>
    public void TookDamage(int damage)
    {
        // Attacks of opportunity the cast itself provokes land as conjuring begins and count too (nwserver
        // cast_provoke: an NPC bandit's cast that drew one was mostly lost).
        if (damage > 0 && (_phase == Phase.Conjure || _startingConjure)) (_hits ??= new()).Add(damage);
    }

    /// <summary>
    /// Every hit taken while conjuring needs its own Concentration check against CONCENTRATION_BASE_DC + that hit's
    /// damage + the spell's level (nwserver cast_concentration: two 25-point hits on Concentration 36 both pass, 20
    /// damage on Concentration 19 lets about 40% of level-1 spells through and 5-15% of level-9 ones; a hit after
    /// conjuring ends never stops the spell). Being disabled ends the spell.
    /// </summary>
    bool Concentrating(World world, GameObject owner)
    {
        if (!CanAct(world, owner)) return false;
        if (_hits == null) return true;
        var hits = _hits;
        _hits = null;
        // Native potion_damage: damage never requests spell concentration for drinking.
        if (_potion || owner is not Creature c || Fake || !SpellTable.UsesConcentration(world, Spell)) return true;
        int level = Math.Max(0, ConcentrationLevel(world, c));
        foreach (int damage in hits)
        {
            int dc = Math.Max(1, Ruleset.Int(world, "CONCENTRATION_BASE_DC", 10) + damage + level);
            if (!CreatureRules.SkillCheck(world, c, SkillConcentration, dc)) { _failedCheck = true; return false; }
        }
        return true;
    }

    /// <summary>Dice (0..n-1) for deafness; tests replace it.</summary>
    public static Func<int, int> Dice = n => Random.Shared.Next(n);

    /// <summary>
    /// Deafness: one verbal spell in five fails (nwserver, parity suite cond_misc deaf.*: 0.73-0.87 of verbal Ray of
    /// Frost and 0.67-0.8 of verbal Light arrived, every somatic-only Lesser Dispel), cheat casts included. Spells
    /// from items and feats are left alone (not measured).
    /// </summary>
    bool DeafFailure(World world, Creature c)
    {
        if (IsItem || FeatId >= 0 || !SpellTable.Verbal(world, Spell) || _slot?.Meta == NwRules.MetaSilent) return false;
        if (!EffectQueries.Has(c, EffectConstants.Of(world)["EFFECT_TYPE_DEAF"])) return false;
        return Dice(100) < 20;
    }

    /// <summary>Arcane spell failure (arcane classes, somatic spells, not stilled) plus EffectSpellFailure.</summary>
    bool FailureRoll(World world, Creature c)
    {
        int chance = 0;
        var classes = world.Resources.TwoDA("classes");
        bool arcane = _slot != null && _slot.Class != NwRules.ClassInvalid && classes.GetInt(_slot.Class, "Arcane") != 0;
        if (arcane && classes.GetInt(_slot!.Class, "ASF", 1) != 0 && SpellTable.Somatic(world, Spell) && _slot.Meta != NwRules.MetaStill)
            chance += CreatureRules.ArcaneSpellFailure(world, c);
        var k = EffectConstants.Of(world);
        int school = SpellTable.School(world, Spell);
        foreach (var e in EffectQueries.Active(c, k["EFFECT_TYPE_SPELL_FAILURE"]))
        {
            bool applies = e.Ints[2] == k["SPELL_FAILURE_TYPE_ARCANE"] ? arcane : e.Ints[1] == SpellTable.SchoolGeneral || e.Ints[1] == school;
            if (applies) chance += e.Ints[0];
        }
        chance = Math.Clamp(chance, 0, 100);
        return chance > 0 && Random.Shared.Next(100) < chance;
    }

    static bool EnemyNear(World world, Creature c)
    {
        if (c.Area == null) return false;
        float range = Ruleset.Float(world, "CONCENTRATION_CHECK_RANGE", 4f);
        return c.Area.Objects.OfType<Creature>().Any(o => o != c && !o.IsDead
            && Vector3.Distance(o.Position, c.Position) <= range && (world.Factions.IsEnemyOf(o, c) || world.Factions.IsEnemyOf(c, o)));
    }

    /// <summary>Whether an object can act (not dead, stunned, paralysed, asleep, petrified, dazed, knocked down).</summary>
    public static bool CanAct(World world, GameObject o)
    {
        if (o.IsDestroyed || o is Creature { IsDead: true } or Creature { Player.IsAreaLoading: true }) return false;
        if (o is not Creature) return true;
        var k = EffectConstants.Of(world);
        foreach (var e in o.Effects)
        {
            int t = e.Type;
            if (t == k["EFFECT_TYPE_STUNNED"] || t == k["EFFECT_TYPE_PARALYZE"] || t == k["EFFECT_TYPE_SLEEP"] || t == k["EFFECT_TYPE_PETRIFY"]
                || t == k["EFFECT_TYPE_DAZED"] || t == k["EFFECT_TYPE_CUTSCENE_PARALYZE"] || t == k.Knockdown)
                return false;
        }
        return true;
    }

    static void Face(World world, GameObject o, Vector3 point)
    {
        var d = new Vector2(point.X - o.Position.X, point.Y - o.Position.Y);
        if (d.LengthSquared() < 1e-4f) return;
        o.Facing = MathF.Atan2(d.Y, d.X);
        world.MarkChanged(o);
    }

    static bool Feedback(World world, GameObject owner, string text)
    {
        if (owner is Creature { IsPC: true } pc) world.Output?.SendMessage(pc, text);
        return false;
    }
}
