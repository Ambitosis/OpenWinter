using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Applies, times and removes effects (ApplyEffectToObject / ApplyEffectAtLocation /
/// RemoveEffect), runs interval effects (regeneration, run-script, dying), expires
/// temporary item properties, and drives area-of-effect objects (enter / exit /
/// heartbeat). Linked effects are applied as separate effects sharing a LinkId;
/// removing any of them removes the whole link.
/// </summary>
public sealed partial class EffectSystem : IWorldSystem
{
    readonly World _world;
    long _nextId;
    readonly HashSet<GameObject> _tracked = new();
    readonly HashSet<Item> _tempProperties = new();
    readonly List<AreaOfEffect> _aoes = new();

    public EffectSystem(World world) { _world = world; }

    public int Order => 10;

    EffectConstants K => EffectConstants.Of(_world);

    public long NextId() => ++_nextId;
    public string NewLinkId() => NextId().ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Area-of-effect objects alive in the world.</summary>
    public IReadOnlyList<AreaOfEffect> AreasOfEffect => _aoes;

    /// <summary>Objects that have (or recently had) effects on them.</summary>
    internal IEnumerable<GameObject> TrackedObjects => _tracked;

    /// <summary>Visual effects and beams don't count when deciding whether immunity rejected a whole link.</summary>
    static bool IsCosmetic(EffectConstants k, Effect e) => e.Type == k.VisualEffect || e.Type == k.Beam || e.Type == k.Icon;

    /// <summary>Effects that act once and are never stored on the target.</summary>
    static bool IsInstantKind(EffectConstants k, int type) =>
        type == k.Heal || type == k.Damage || type == k.Death || type == k.Resurrection
        || type == k.DispelAll || type == k.DispelBest || type == k.SummonCreature;

    /// <summary>
    /// Effects that death removes even when unyielding (parity suite death_keeps): the conditions that take control,
    /// deafness, silence, entanglement, petrification, invisibility (not improved invisibility), sanctuary and
    /// etherealness. Blindness, darkness, slow, poison and disease stay.
    /// </summary>
    public static bool IsStateEffect(EffectConstants k, Effect e)
    {
        if (e.Type == k["EFFECT_TYPE_INVISIBILITY"] && e.Ints[0] == k["INVISIBILITY_TYPE_IMPROVED"]) return false;
        foreach (var n in StateEffects) if (e.Type == k["EFFECT_TYPE_" + n]) return true;
        return false;
    }
    static readonly string[] StateEffects =
    {
        "STUNNED", "KNOCKDOWN", "DEAF", "PARALYZE", "SLEEP", "DAZED", "CONFUSED", "FRIGHTENED",
        "CHARMED", "DOMINATED", "ENTANGLE", "PETRIFY", "SILENCE", "TURNED", "CUTSCENE_PARALYZE", "POLYMORPH",
        "INVISIBILITY", "SANCTUARY", "ETHEREAL", "CUTSCENEIMMOBILIZE", "CUTSCENE_DOMINATED",
    };

    // ---------------------------------------------------------------- applying

    /// <summary>ApplyEffectToObject.</summary>
    public void Apply(Effect effect, int durationType, GameObject target, float duration)
    {
        if (!effect.IsValid || target.IsDestroyed) return;
        var k = K;
        var leaves = effect.Leaves.Select(l => l.Copy(false)).ToList();
        // Every part of a link answers to the link (RemoveEffect with any of them removes them all).
        if (effect.IsLinked) foreach (var leaf in leaves) leaf.ScriptId = effect.ScriptId;

        if (durationType == k.Instant)
        {
            // Lasting effects applied as instant change the target for good without joining its effect list.
            var residue = new List<Effect>();
            var afflictions = new List<Effect>();
            foreach (var leaf in leaves)
            {
                if (Refused(target, leaf)) continue;
                // A hit point change when dying applied as instant becomes a temporary effect that changes a dying
                // player's hit points by one every |6 / change| seconds (nwmain bleedinst: -1 every
                // 6 s, -2 every 3 s, -0.5 every 12 s, +2 every 3 s up to 1). Scripts see and remove it like any other.
                if (leaf.Type == k.HitPointChangeWhenDying)
                {
                    if (leaf.Floats[0] == 0) continue;
                    leaf.FromInstant = true;
                    Land([leaf], target, k.Temporary, MathF.Abs(6f / leaf.Floats[0]), hidden: false);
                    continue;
                }
                if (IsInstantAction(k, leaf.Type)) ApplyInstant(leaf, target, durationType, 0);
                // Poison and disease applied as instant are kept as permanent.
                else if (IsAffliction(k, leaf.Type)) afflictions.Add(leaf);
                else if (LeavesResidue(k, leaf.Type)) residue.Add(leaf);
            }
            if (afflictions.Count > 0) Land(afflictions, target, k.Permanent, 0, hidden: false);
            if (residue.Count > 0) Land(residue, target, k.Permanent, 0, hidden: true);
            return;
        }
        if (durationType != k.Temporary && durationType != k.Permanent) return;

        var instants = leaves.Where(l => IsInstantKind(k, l.Type)).ToList();
        var lasting = leaves.Where(l => !IsInstantKind(k, l.Type)).ToList();
        var landed = lasting.Where(l => !Refused(target, l)).ToList();
        // A link whose every real effect was resisted loses its visuals too.
        if (lasting.Any(l => !IsCosmetic(k, l)) && !landed.Any(l => !IsCosmetic(k, l))) landed.Clear();
        // Native never retains fire-and-forget or projectile visual rows, even when
        // scripts request a temporary/permanent duration. Keep link-immunity filtering.
        instants.AddRange(landed.Where(IsTransientVisual));
        landed.RemoveAll(IsTransientVisual);
        Land(landed, target, durationType, duration, hidden: false);
        foreach (var leaf in instants)
            if (!EffectImmunity.Blocks(_world, target, leaf)) ApplyInstant(leaf, target, durationType, duration);
    }

    /// <summary>Whether the target refuses an effect; a player kept safe by an immunity is told so.</summary>
    bool Refused(GameObject target, Effect e)
    {
        int by = EffectImmunity.BlockedBy(_world, target, e);
        if (by == EffectImmunity.NotBlocked) return false;
        if (by >= 0 && target is Creature { IsPC: true } pc
            && EffectImmunity.ImmuneMessage(_world, K, by, pc.DisplayName(_world.Resources.Talk) is { Length: > 0 } n ? n : pc.Tag) is { } line)
            _world.Output?.SendMessage(pc, line);
        return true;
    }

    bool IsTransientVisual(Effect effect) => effect.Type == K.VisualEffect
        && _world.Resources.TwoDA("visualeffects").Get(effect.Ints[0], "Type_FD") is "F" or "P";

    /// <summary>
    /// Effects whose application is under way (an EffectRunScript's OnApplied script, a poison's poison.2da Script_1).
    /// A death they cause doesn't strip them (parity suite death_keeps: the poison whose Script_1 kills and the
    /// EffectRunScript whose OnApplied script kills stay on the corpse, while the rest goes, link-mates included).
    /// </summary>
    readonly HashSet<Effect> _applying = new();

    /// <summary>Death: removes the effects <paramref name="match"/> picks, with their links, but never one being applied.</summary>
    public void RemoveOnDeath(GameObject target, Func<Effect, bool> match)
    {
        var seeds = target.Effects.Where(e => !e.Hidden && !_applying.Contains(e) && match(e)).ToList();
        if (seeds.Count == 0) return;
        var links = seeds.Select(s => s.LinkId).Where(l => l.Length > 0).ToHashSet();
        var gone = target.Effects.Where(x => !_applying.Contains(x) && (seeds.Contains(x) || (x.LinkId.Length > 0 && links.Contains(x.LinkId)))).ToList();
        foreach (var e in gone) target.Effects.Remove(e);
        foreach (var e in gone)
        {
            e.Target = GameObject.InvalidId;
            OnRemoved(e, target);
        }
        _world.MarkChanged(target);
    }

    void WhileApplying(Effect e, Action run)
    {
        bool added = _applying.Add(e);
        try { run(); }
        finally { if (added) _applying.Remove(e); }
    }

    /// <summary>Adds effects to the target's list (in the original's order) and starts them.</summary>
    void Land(List<Effect> leaves, GameObject target, int durationType, float duration, bool hidden)
    {
        var k = K;
        string linkId = NewLinkId();
        double now = _world.Time;
        var added = new List<Effect>();
        var extra = new List<Effect>();
        var scripts = new List<(string Script, Effect Poison)>();
        foreach (var leaf in leaves)
        {
            leaf.DurationType = durationType;
            leaf.Duration = durationType == k.Temporary ? Math.Max(0, duration) : 0;
            leaf.UniqueId = NextId();
            leaf.LinkId = linkId;
            leaf.Target = target.Id;
            leaf.AppliedAt = now;
            leaf.ExpiresAt = durationType == k.Temporary ? now + leaf.Duration : 0;
            leaf.Hidden = hidden;
            if (!Admit(k, leaf, target)) continue;
            // Native keeps the curse's original payload, but suppresses penalties
            // permanently if ability-decrease immunity is present on application.
            // Constitution is applied separately on the following update.
            if (leaf.Type == NwRules.EffectCurse)
                leaf.CurseBlockedAbilities = CurseAbilityImmune(leaf, target) ? 0x3f : 0;
            if (IsAffliction(k, leaf.Type) && !Afflict(k, leaf, target, extra, scripts)) continue;
            target.Effects.Insert(EffectOrder.InsertAt(target.Effects, leaf.Type), leaf);
            _tracked.Add(target);
            added.Add(leaf);
        }
        // Clients show some conditions on the body (GameServer.ConditionFlags).
        if (added.Count > 0) _world.MarkChanged(target);
        foreach (var leaf in added)
        {
            if (!target.Effects.Contains(leaf)) continue; // removed by an earlier leaf's side effects
            WhileApplying(leaf, () => OnApplied(leaf, target));
            if (!hidden) Notify("effect-applied", target, Describe(k, leaf));
            if (!hidden) NotStacking(leaf, target);
        }
        // poison.2da Script_1 runs on the poisoned creature as the poison lands.
        foreach (var (script, poison) in scripts)
            if (target is Creature { IsDestroyed: false, IsDead: false })
                WhileApplying(poison, () => _world.Scripts?.Run(script, target.Id));
        if (extra.Count > 0) Land(extra, target, durationType, duration, hidden);
    }

    /// <summary>
    /// Whether an effect takes hold. An ability decrease only goes as far as a score of 3 (and doesn't land
    /// at all on a score already there); dying hit point changes only land on players; time stop never lands.
    /// </summary>
    bool Admit(EffectConstants k, Effect e, GameObject target)
    {
        // The dedicated server doesn't keep a time stop at all, and nothing freezes (nwserver, cond_timestop).
        if (e.Type == k["EFFECT_TYPE_TIMESTOP"]) return false;
        if (e.Type == k.HitPointChangeWhenDying) return target is Creature { IsPC: true };
        // A decrease by a negative amount doesn't land (Shadow Essence's EffectAbilityDecrease(STR, -1): cond_poison).
        if (e.Type == k["EFFECT_TYPE_ABILITY_DECREASE"] && e.Ints[1] < 0) return false;
        if (e.Type == k["EFFECT_TYPE_ABILITY_DECREASE"] && target is Creature c && e.Ints[1] > 0)
        {
            int room = CreatureRules.AbilityScore(_world, c, e.Ints[0]) - 3;
            if (room <= 0) return false;
            e.Ints[1] = Math.Min(e.Ints[1], room);
        }
        return true;
    }

    bool CurseAbilityImmune(Effect e, GameObject target) => !e.IgnoreImmunity
        && EffectImmunity.IsImmune(_world, target, NwRules.ImmunityAbilityDecrease, _world.Get(e.Creator));

    /// <summary>Native reload reapplies curse suppression against the restored immunity state.</summary>
    public void RestoreCursePenalties(Creature target)
    {
        bool changed = false;
        foreach (var e in target.Effects)
        {
            if (e.Type != NwRules.EffectCurse) continue;
            int blocked = CurseAbilityImmune(e, target) ? 0x3f : 0;
            changed |= e.CurseBlockedAbilities != blocked;
            e.CurseBlockedAbilities = blocked;
        }
        if (changed) CreatureRules.SyncHitPoints(_world, target);
    }

    /// <summary>Instant effects ApplyInstant carries out.</summary>
    static bool IsInstantAction(EffectConstants k, int type) =>
        IsInstantKind(k, type) || type == k.VisualEffect || type == k.Beam || type == k.RunScript
        || type == k.Disappear || type == k.DisappearAppear || type == k.Appear;

    /// <summary>Lasting effects that stay on as hidden residue when applied as instant.</summary>
    static bool LeavesResidue(EffectConstants k, int type) =>
        type != k.AreaOfEffect && type != k.Regenerate && type != k.HitPointChangeWhenDying && type != k.Icon
        && type != k["EFFECT_TYPE_POLYMORPH"] && type != k["EFFECT_TYPE_SWARM"] && type != k["EFFECT_TYPE_TIMESTOP"];

    /// <summary>ApplyEffectAtLocation: visual effects, areas of effect and summons.</summary>
    public void ApplyAtLocation(Effect effect, int durationType, Location location, float duration)
    {
        if (!effect.IsValid || _world.Get(location.Area) is not Area area) return;
        var k = K;
        foreach (var leaf in effect.Leaves.Select(l => l.Copy(false)))
        {
            if (leaf.Type == k.VisualEffect)
                Notify("vfx", area, new
                {
                    vfx = leaf.Ints[0], creator = leaf.Creator, miss = leaf.Ints[1] != 0, scale = leaf.Floats[0],
                    pos = Vec(location.Position), facing = location.FacingDegrees,
                    translate = Vec(leaf.Vectors[0]), rotate = Vec(leaf.Vectors[1]),
                    duration = durationType == k.Temporary ? duration : durationType == k.Permanent ? -1f : 0f, id = 0L,
                });
            else if (leaf.Type == k.AreaOfEffect && durationType != k.Instant)
                CreateAreaOfEffect(leaf, area, location.Position, location.FacingDegrees * MathF.PI / 180f, null,
                    durationType == k.Temporary ? _world.Time + Math.Max(0, duration) : 0);
            else if (leaf.Type == k.SummonCreature)
                Summon(leaf, location, _world.Get<Creature>(leaf.Creator), durationType, duration);
            else if (leaf.Type == k.DispelAll || leaf.Type == k.DispelBest)
            {
                // TODO(spells): area dispels are done by the spell scripts (they iterate the shape); nothing to do here.
            }
        }
    }

    /// <summary>
    /// An instant EffectDamage, and the line the original prints for it: "Bandit damages Aluvian: 4 (4 Fire)", or
    /// "Someone damages ..." when no creature caused it, 0 against a plot target (convoend, chatsplit2). The
    /// damaged player and a player who caused it see it, and the nearby master of an associate on either side.
    /// </summary>
    void ApplyDamage(Effect e, GameObject target)
    {
        int amount = e.Ints[0] < 0 ? 1 : e.Ints[0], type = e.Ints[1];
        if (HitPoints.IsDead(target) || target is not (Creature or Placeable or Door)) return;
        var breakdown = new DamageBreakdown();
        HitPoints.Damage(_world, target, e.Creator, new Dictionary<int, int> { [type] = amount }, e.Ints[2], breakdown: breakdown);
        var source = _world.Get(e.Creator) as Creature;
        // Players involved, and a nearby master of an associate involved (killxp: "Bandit damages Goblin" from a henchman).
        var audience = Society.AssociateService.FeedbackPlayers(_world, target, target as Creature, source);
        if (audience.Count == 0) return;
        string Name(GameObject o) => o.DisplayName(_world.Resources.Talk) is { Length: > 0 } n ? n : o.Tag;
        var dealt = target.Plot ? new Dictionary<int, int> { [type] = 0 } : breakdown.Dealt;
        var parts = dealt.OrderBy(kv => (kv.Key & 7) != 0 ? 0 : kv.Key)
            .GroupBy(kv => Combat.CombatSystem.DamageTypeName(kv.Key)).Select(g => $"{g.Sum(kv => kv.Value)} {g.Key}");
        var line = $"{(source != null ? Name(source) : "Someone")} damages {Name(target)}: {(target.Plot ? 0 : dealt.Values.Sum())} ({string.Join(" ", parts)})";
        foreach (var who in audience) _world.Output?.SendMessage(who, line);
    }

    /// <summary>
    /// EffectHeal, and the original's "Aluvian Darkstar : Healed 5 hit points." (dialog.tlk 62504) to the players
    /// involved, even when nothing was missing ("Healed 0 hit points."; dying scenes' chat logs).
    /// </summary>
    void ApplyHeal(Effect e, GameObject target)
    {
        if (HitPoints.IsDead(target) || target is not Creature healed) { HitPoints.Heal(_world, target, e.Ints[0]); return; }
        int amount = HitPoints.Heal(_world, target, e.Ints[0]);
        var audience = Society.AssociateService.FeedbackPlayers(_world, target, healed, _world.Get(e.Creator) as Creature);
        if (audience.Count == 0) return;
        string name = target.DisplayName(_world.Resources.Talk) is { Length: > 0 } n ? n : target.Tag;
        string line = _world.TlkStringOverridden(62504).Replace("<CUSTOM0>", name).Replace("<CUSTOM1>", amount.ToString());
        foreach (var who in audience) _world.Output?.SendMessage(who, line);
    }

    void ApplyInstant(Effect e, GameObject target, int durationType, float duration)
    {
        var k = K;
        if (e.Type == k.Heal) ApplyHeal(e, target);
        // A negative amount deals 1 point, like the original.
        else if (e.Type == k.Damage) ApplyDamage(e, target);
        else if (e.Type == k.Death) HitPoints.Kill(_world, target, e.Creator, e.Ints[0] != 0);
        else if (e.Type == k.Resurrection)
        {
            // SetIsDestroyable's raiseable flag governs spell resurrection (native resurrection_flags).
            if (target is not Creature { IsRaiseable: false }) HitPoints.Resurrect(_world, target);
        }
        else if (e.Type == k.VisualEffect)
            Notify("vfx", target, VfxData(e, 0f, 0));
        else if (e.Type == k.Beam)
            Notify("beam", target, BeamData(e, 0f, 0));
        else if (e.Type == k.RunScript)
            RunScriptEvent(e, target, e.Strings[1], "ON_APPLIED");
        else if (e.Type == k.DispelAll || e.Type == k.DispelBest) Dispel(e, target, best: e.Type == k.DispelBest);
        else if (e.Type == k.SummonCreature) Summon(e, target.Location, target, durationType, duration);
        else if (e.Type == k.Disappear) Disappear(target, e, permanent: true);
        else if (e.Type == k.DisappearAppear) DisappearAppear(target, e);
        else if (e.Type == k.Appear) Notify("appear", target, new { animation = e.Ints[0] });
        // Other effect types do nothing when applied instantly, like the original.
    }

    void OnApplied(Effect e, GameObject target)
    {
        var k = K;
        double now = _world.Time;
        if (e.Type == k.VisualEffect)
            Notify("vfx", target, VfxData(e, e.DurationType == k.Temporary ? e.Duration : -1f, e.UniqueId));
        else if (e.Type == k.Beam)
            Notify("beam", target, BeamData(e, e.DurationType == k.Temporary ? e.Duration : -1f, e.UniqueId));
        else if (e.Type == k.AreaOfEffect && target.Area != null)
        {
            var aoe = CreateAreaOfEffect(e, target.Area, target.Position, target.Facing, target, e.ExpiresAt);
            e.AreaObject = aoe?.Id ?? GameObject.InvalidId;
        }
        else if (e.Type == k.RunScript)
        {
            if (e.Strings[3].Length > 0 && e.Floats[0] > 0) e.NextTick = now + e.Floats[0];
            RunScriptEvent(e, target, e.Strings[1], "ON_APPLIED");
        }
        else if (e.Type == k.Regenerate) e.NextTick = now + Interval(e.Floats[0]);
        // A temporary one changes a dying player's hit points once per duration and stays on; a permanent one never
        // does while it lasts (see DyingChangeRemoved).
        else if (e.Type == k.HitPointChangeWhenDying) e.NextTick = e.DurationType == k.Temporary && e.Duration > 0 ? now + e.Duration : 0;
        else if (e.Type == k.Disappear) Disappear(target, e, permanent: e.DurationType == k.Permanent);
        else if (e.Type == k.DisappearAppear)
        {
            if (e.DurationType == k.Permanent) DisappearAppear(target, e);
            else Notify("disappear", target, new { animation = e.Ints[0] });
        }
        else if (e.Type == k.Appear) Notify("appear", target, new { animation = e.Ints[0] });
        bool underHigherState = target is Creature sc && StateScripts.RowOf(e.Type) is > 0 and var row
            && sc.Effects.Any(o => o != e && StateScripts.RowOf(o.Type) >= row);
        if (!underHigherState) Lock(k, e, target);
        if (e.Type == k["EFFECT_TYPE_POLYMORPH"] && target is Creature pc && !e.Hidden) StartPolymorph(k, e, pc);
        // Hit points follow a Constitution change at once (a curse's a moment later, like its Constitution loss).
        if (target is Creature hc && AffectsHitPoints(k, e))
        {
            CreatureRules.SyncHitPoints(_world, hc);
            if (e.Type == NwRules.EffectCurse) _world.After(0, () =>
            {
                if (hc.IsDestroyed || !hc.Effects.Contains(e)) return;
                int con = 1 << NwRules.AbilityCon;
                e.CurseBlockedAbilities = (e.CurseBlockedAbilities & ~con) | (CurseAbilityImmune(e, hc) ? con : 0);
                CreatureRules.SyncHitPoints(_world, hc);
            });
        }
        // A creature dominated by a player joins her service as a dominated associate. Measured: EffectDominated applied
        // by the player made the goblin ASSOCIATE_TYPE_DOMINATED in her faction, reputation 100 (associateradial).
        // Dominated by an NPC it gets no master and no change of reputation, and just stands (nwserver, cond_charm).
        if (e.Type == k["EFFECT_TYPE_DOMINATED"] && target is Creature slave && !slave.IsDead
            && _world.Get<Creature>(e.Creator) is { IsPC: true } lord && lord != slave)
            Society.AssociateService.Add(_world, lord, slave, Society.AssociateService.Dominated);
        // Charm, confusion, fear and the other states bring the creature's next heartbeat forward, so its
        // statescripts.2da script runs at once (StateScripts; nwserver state_scripts).
        // A state applied under an equal or higher one (confusion on a dazed creature) does nothing visible until that
        // one ends (nwserver state_commandable dazeconf: still commandable, its queued ActionWait still running).
        if (target is Creature ruled && StateScripts.ResetsHeartbeat(e.Type) && !underHigherState) ruled.HeartbeatNow = true;
        if (e.Type == k["EFFECT_TYPE_SWARM"] && target is Creature swarmer) StartSwarm(e, swarmer);
    }

    void OnRemoved(Effect e, GameObject target)
    {
        var k = K;
        if (e.Type == k.HitPointChangeWhenDying && target is Creature { IsDestroyed: false } holder) DyingChangeRemoved(holder, e);
        if (e.Type == k.SummonCreature && _world.Get<Creature>(e.SummonedObject) is { } summon) Unsummon(summon, e.Ints[2]);
        else if (e.Type == k["EFFECT_TYPE_SWARM"]) EndSwarm(e);
        else if (e.Type == k.AreaOfEffect && _world.Get(e.AreaObject) is AreaOfEffect aoe) DestroyAreaOfEffect(aoe);
        else if (e.Type == k.RunScript) RunScriptEvent(e, target, e.Strings[2], "ON_REMOVED");
        else if (e.Type == k.DisappearAppear && e.DurationType == k.Temporary && !target.IsDestroyed) DisappearAppear(target, e);
        else if (e.Type == k.Disappear && e.DurationType == k.Temporary && !target.IsDestroyed)
            Notify("appear", target, new { animation = e.Ints[0] });
        Unlock(k, e, target);
        // The last domination by its master ends: the creature leaves the service (and the master reads TLK 61909).
        if (e.Type == k["EFFECT_TYPE_DOMINATED"] && target is Creature freed && freed.AssociateType == Society.AssociateService.Dominated
            && freed.Master == e.Creator && _world.Get<Creature>(e.Creator) is { } master
            && !freed.Effects.Any(o => o != e && o.Type == e.Type && o.Creator == master.Id))
            Society.AssociateService.EndDomination(_world, master, freed);
        if (e.Type == k["EFFECT_TYPE_POLYMORPH"] && target is Creature pc) EndPolymorph(pc, e);
        // When one ends, hit points follow on the next update.
        if (target is Creature hc && AffectsHitPoints(k, e) && _hpPending.Add(hc.Id))
            _world.After(0, () => { _hpPending.Remove(hc.Id); if (!hc.IsDestroyed) CreatureRules.SyncHitPoints(_world, hc); });
        if (!e.Hidden) Notify("effect-removed", target, new { id = e.UniqueId, type = e.Type, linkId = e.LinkId, icon = IconOf(k, e) });
    }

    readonly HashSet<uint> _hpPending = new();

    // ---------------------------------------------------------------- hit point change when dying

    sealed class DyingDrain { public float Change; }
    readonly Dictionary<uint, DyingDrain> _dyingDrain = new();
    const float DyingDrainRate = 125f;
    /// <summary>A dying change being taken off without its removal change (its own killing tick, or its end above 0).</summary>
    Effect? _silentEnd;

    void EndSilently(Creature c, Effect e)
    {
        _silentEnd = e;
        RemoveOnly(c, e);
        _silentEnd = null;
    }

    /// <summary>
    /// A hit point change when dying ending (dying, dyingbleed, dyingfx, bleedinst on nwmain).
    /// <list type="bullet">
    /// <item>On a player still dying, a temporary one (a script's, or one made from an instant application) ticks at once
    /// and, if she is still dying, comes back with the same period, so removing it can't stop a bleed (bleedinst I1: -4
    /// to -5 within 0.05 s, the next tick 6 s later; T1, a 4 s one removed after 2 s: a tick at once, then every 4 s).</item>
    /// <item>A permanent one runs on from the next update, about once every 8 ms, until she is at 1 or dead
    /// (dyingbleed).</item>
    /// <item>On anyone else (healed above 0, which can pass the maximum, or dead) its change lands once as it goes,
    /// unless it is ending silently.</item>
    /// </list>
    /// </summary>
    void DyingChangeRemoved(Creature c, Effect e)
    {
        if (ReferenceEquals(e, _silentEnd)) return;
        var k = K;
        if (c.IsDying && e.DurationType == k.Temporary)
        {
            // After the removing script (bleedinst: the script still read -4 and no effect; 0.05 s later -5 and the effect).
            _world.After(0, () =>
            {
                if (c.IsDestroyed || !c.IsDying) return;
                ChangeDying(c, e.Floats[0]);
                if (c.IsDying) Land([e.Copy(false)], c, k.Temporary, e.Duration, hidden: false);
                // A death by that tick still takes the instant-made one's change once more (bleedinst D3: removed at -9, -12).
                else if (c.IsDead && e.FromInstant) ChangeDying(c, e.Floats[0], anyState: true);
            });
            return;
        }
        if (c.IsDying) { _dyingDrain[c.Id] = new DyingDrain { Change = e.Floats[0] }; return; }
        ChangeDying(c, e.Floats[0], anyState: true);
    }

    /// <summary>Moves hit points one point in the change's direction, never more; -10 kills.</summary>
    void ChangeDying(Creature c, float change, bool anyState = false)
    {
        if (!anyState && !c.IsDying) return;
        int whole = Math.Sign(change);
        if (whole == 0) return;
        int before = c.CurrentHitPoints;
        // A dying player rising this way stops at 1 (and stays uncommandable: only healing gives control back).
        c.CurrentHitPoints = !c.IsDead && before <= 0 && whole > 0 ? Math.Min(1, before + whole) : before + whole;
        _world.MarkChanged(c);
        if (whole < 0 && !c.IsDead) HitPoints.CheckDeath(_world, c, GameObject.InvalidId, before);
    }

    /// <summary>Whether a creature's maximum hit points are waiting to follow an effect that ended.</summary>
    public bool HitPointsPending(Creature c) => _hpPending.Contains(c.Id);

    static bool AffectsHitPoints(EffectConstants k, Effect e) =>
        e.Type == k["EFFECT_TYPE_ABILITY_INCREASE"] || e.Type == k["EFFECT_TYPE_ABILITY_DECREASE"] || e.Type == k["EFFECT_TYPE_CURSE"]
        || e.Type == k["EFFECT_TYPE_POLYMORPH"];

    // ---------------------------------------------------------------- commandable

    readonly HashSet<uint> _locked = new();

    /// <summary>
    /// States that make a creature uncommandable (measured on nwserver, parity suite condition_actions: the flag is
    /// clear 0.5 s after the effect lands; at once except for fear, charm and turning, see Lock).
    /// </summary>
    static readonly string[] LockNow =
    {
        "PARALYZE", "STUNNED", "SLEEP", "PETRIFY", "KNOCKDOWN", "CONFUSED", "DOMINATED", "CUTSCENE_PARALYZE", "CUTSCENE_DOMINATED",
        "FRIGHTENED", "CHARMED", "TURNED",
    };

    /// <summary>States whose arrival empties the action queue (premove readings): the locking ones and daze.</summary>
    static readonly string[] ClearOnLand =
    {
        "PARALYZE", "STUNNED", "SLEEP", "PETRIFY", "KNOCKDOWN", "CONFUSED", "DOMINATED", "CUTSCENE_PARALYZE",
        "FRIGHTENED", "CHARMED", "TURNED", "DAZED",
    };

    static bool Is(EffectConstants k, int type, string[] names)
    {
        foreach (var n in names) if (type == k["EFFECT_TYPE_" + n]) return true;
        return false;
    }

    /// <summary>Like the original, these states stop what the creature was doing and clear its commandable flag until the last of them ends.</summary>
    /// <summary>
    /// A player given an AC bonus of a kind she already has from another effect is told it won't stack (dialog.tlk
    /// 66247 natural, 66248 armour, 66249 shield, 66250 deflection; dodge bonuses stack and say nothing). Measured on
    /// nwmain (visual scene castlines3): every further EffectACIncrease of the kind, larger or smaller, and Barkskin
    /// cast on herself twice print it once as the effect lands; nothing is said for an NPC.
    /// </summary>
    void NotStacking(Effect e, GameObject target)
    {
        if (e.Type != NwRules.EffectAcIncrease || target is not Creature { IsPC: true } pc || e.Ints[1] is < 1 or > 4) return;
        if (!target.Effects.Any(o => o != e && !o.Hidden && o.Type == NwRules.EffectAcIncrease && o.Ints[1] == e.Ints[1])) return;
        _world.Output?.SendMessage(pc, _world.TlkStringOverridden(NotStackingStrref + e.Ints[1] switch { 1 => 0, 2 => 1, 3 => 2, _ => 3 }));
    }

    public const int NotStackingStrref = 66247;

    void Lock(EffectConstants k, Effect e, GameObject target)
    {
        if (target is not Creature) return;
        if (Is(k, e.Type, ClearOnLand) && target.Actions.Count > 0)
        {
            ActionQueues.Clear(_world, target);
            if (target is Creature c) Movement.Stop(c);
        }
        // A daze that becomes the creature's state makes it commandable, even if a script had made it uncommandable
        // (nwserver state_commandable daze.preset).
        if (e.Type == k["EFFECT_TYPE_DAZED"] && !target.Effects.Any(o => o != e && Is(k, o.Type, LockNow) && StateScripts.RowOf(o.Type) < 0))
        {
            _locked.Remove(target.Id);
            if (target is not Creature { IsDying: true }) target.Commandable = true;
            return;
        }
        if (!Is(k, e.Type, LockNow)) return;
        // Fear, charm and turning leave the creature commandable until the script that applied them is over: still so
        // in that script, no longer 0.1 s later, whatever the state script does (nwserver state_scripts, with a
        // statescripts.2da whose scripts never call SetCommandable). The others lock at once.
        if (Is(k, e.Type, LockLater))
            _world.After(LockDelay, () => { if (!target.IsDestroyed && target.Effects.Contains(e)) LockNowOn(target); });
        else LockNowOn(target);
    }

    static readonly string[] LockLater = { "FRIGHTENED", "CHARMED", "TURNED" };
    const double LockDelay = 0;

    void LockNowOn(GameObject target)
    {
        if (!target.Commandable) return;
        target.Commandable = false;
        _locked.Add(target.Id);
    }

    /// <summary>
    /// An effect ends. Measured on nwserver (state_commandable): when the creature's state (its highest
    /// statescripts.2da row) ends and no other remains, it is made commandable once the removing script is over (still
    /// not inside it), even if a script had made it uncommandable; when a lower state remains, that one takes over as
    /// if it had just landed (frightened + stunned, the stun removed: uncommandable at once and the fear script runs
    /// straight away; a confusion applied under a daze clears the queue, locks and runs its script when the daze ends).
    /// </summary>
    void Unlock(EffectConstants k, Effect e, GameObject target)
    {
        int row = StateScripts.RowOf(e.Type);
        if (row > 0 && target is Creature c && !c.IsDestroyed && !c.Effects.Any(o => StateScripts.RowOf(o.Type) >= row))
        {
            var next = c.Effects.Where(o => StateScripts.RowOf(o.Type) > 0).OrderByDescending(o => StateScripts.RowOf(o.Type)).FirstOrDefault();
            if (next == null)
            {
                if (c.Effects.Any(x => Is(k, x.Type, LockNow))) return;
                _world.After(0, () =>
                {
                    if (c.IsDestroyed || c.Effects.Any(x => Is(k, x.Type, LockNow) || StateScripts.RowOf(x.Type) > 0)) return;
                    _locked.Remove(c.Id);
                    if (!c.IsDying) c.Commandable = true;
                });
                return;
            }
            // The next state takes over at once: its queue clear, its lock (fear, charm and turning included) and a
            // heartbeat now.
            if (Is(k, next.Type, ClearOnLand) && c.Actions.Count > 0) { ActionQueues.Clear(_world, c); Movement.Stop(c); }
            if (next.Type == k["EFFECT_TYPE_DAZED"]) { _locked.Remove(c.Id); if (!c.IsDying) c.Commandable = true; }
            else if (Is(k, next.Type, LockNow)) LockNowOn(c);
            if (StateScripts.ResetsHeartbeat(next.Type)) c.HeartbeatNow = true;
            return;
        }
        if (!_locked.Contains(target.Id)) return;
        if (target.Effects.Any(x => Is(k, x.Type, LockNow))) return;
        _locked.Remove(target.Id);
        // A dying player stays uncommandable until it is healed.
        if (target is not Creature { IsDying: true }) target.Commandable = true;
    }

    static float Interval(float f) => f > 0 ? f : World.HeartbeatInterval;

    void RunScriptEvent(Effect e, GameObject target, string script, string kind)
    {
        if (script.Length == 0 || _world.Scripts == null || target.IsDestroyed) return;
        var ctx = new EventContext().Set("RunScriptEffect", e).Set("RunScriptEffectScriptType", K["RUNSCRIPT_EFFECT_SCRIPT_TYPE_" + kind]);
        _world.Scripts.Run(script, target.Id, ctx);
    }

    // ---------------------------------------------------------------- removing

    /// <summary>
    /// RemoveEffect from a script: every listed effect with <paramref name="e"/>'s identity (the same value applied
    /// more than once, any copy of it, any part of a link), with everything linked to them.
    /// </summary>
    public bool RemoveScripted(GameObject target, Effect e)
    {
        if (e.ScriptId == 0) return Remove(target, e);
        var seeds = target.Effects.Where(x => !x.Hidden && (ReferenceEquals(x, e) || x.ScriptId == e.ScriptId)).ToList();
        return RemoveLinks(target, seeds) > 0;
    }

    /// <summary>Removes the applied effect matching <paramref name="e"/> and everything linked to it.</summary>
    public bool Remove(GameObject target, Effect e)
    {
        if (e.UniqueId == 0) return false;
        var stored = target.Effects.FirstOrDefault(x => x.UniqueId == e.UniqueId);
        if (stored == null) return false;
        RemoveLinks(target, new[] { stored });
        return true;
    }

    /// <summary>Removes one effect without the rest of its link.</summary>
    public void RemoveOnly(GameObject target, Effect e)
    {
        if (!target.Effects.Remove(e)) return;
        e.Target = GameObject.InvalidId;
        OnRemoved(e, target);
        _world.MarkChanged(target);
    }

    /// <summary>Removes every listed effect matching <paramref name="match"/> (with their links). Returns how many effects went.</summary>
    public int RemoveWhere(GameObject target, Func<Effect, bool> match) => RemoveLinks(target, target.Effects.Where(e => !e.Hidden && match(e)).ToList());

    int RemoveLinks(GameObject target, IReadOnlyCollection<Effect> seeds)
    {
        if (seeds.Count == 0) return 0;
        var links = seeds.Select(s => s.LinkId).Where(l => l.Length > 0).ToHashSet();
        var gone = target.Effects.Where(x => seeds.Contains(x) || (x.LinkId.Length > 0 && links.Contains(x.LinkId))).ToList();
        foreach (var e in gone) target.Effects.Remove(e);
        foreach (var e in gone)
        {
            e.Target = GameObject.InvalidId;
            OnRemoved(e, target);
        }
        _world.MarkChanged(target);
        return gone.Count;
    }

    // ---------------------------------------------------------------- item properties

    /// <summary>AddItemProperty: temporary and permanent only.</summary>
    public void AddItemProperty(Item item, ItemProperty ip, int durationType, float duration)
    {
        var k = K;
        if (!ip.Valid || item.IsDestroyed || (durationType != k.Temporary && durationType != k.Permanent)) return;
        var copy = ip.Clone();
        copy.DurationType = durationType;
        copy.Duration = durationType == k.Temporary ? Math.Max(0, duration) : 0;
        copy.ExpiresAt = durationType == k.Temporary ? _world.Time + copy.Duration : 0;
        item.Properties.Add(copy);
        if (durationType == k.Temporary) _tempProperties.Add(item);
        _world.MarkChanged(item);
    }

    /// <summary>RemoveItemProperty: the stored property (same instance, or the first with identical fields).</summary>
    public bool RemoveItemProperty(Item item, ItemProperty ip)
    {
        int i = item.Properties.IndexOf(ip);
        if (i < 0) i = item.Properties.FindIndex(p => SameProperty(p, ip));
        // A property value that was never added itself (the script kept its own copy) matches by content.
        if (i < 0 && ip.DurationType == 0 && ip.ExpiresAt == 0) i = item.Properties.FindIndex(p => SameProperty(p, ip, ignoreDuration: true));
        if (i < 0) return false;
        item.Properties.RemoveAt(i);
        _world.MarkChanged(item);
        return true;
    }

    public static bool SameProperty(ItemProperty a, ItemProperty b, bool ignoreDuration = false) =>
        a.Type == b.Type && a.SubType == b.SubType && a.CostTable == b.CostTable && a.CostTableValue == b.CostTableValue
        && a.Param1 == b.Param1 && a.Param1Value == b.Param1Value && a.Tag == b.Tag
        && (ignoreDuration || (a.DurationType == b.DurationType && Math.Abs(a.ExpiresAt - b.ExpiresAt) < 1e-6));

    // ---------------------------------------------------------------- ticking

    public void Step(float dt)
    {
        double now = _world.Time;
        var k = K;
        foreach (var o in _tracked.ToList())
        {
            if (o.IsDestroyed || o.Effects.Count == 0) { _tracked.Remove(o); continue; }
            var expired = o.Effects.Where(e => e.DurationType == k.Temporary && now >= e.ExpiresAt && e.Type != k.HitPointChangeWhenDying).ToList();
            // An expired polymorph stays listed (with its link) until the creature changes back.
            foreach (var p in expired.Where(e => e.Type == k["EFFECT_TYPE_POLYMORPH"] && e.Ints[5] == 0 && !e.Hidden).ToList())
                DeferPolymorphEnd(o, o.Effects.Where(x => x.LinkId == p.LinkId).ToList(), now + UnpolymorphDelay);
            if (expired.Count > 0)
            {
                var waiting = o.Effects.Where(x => x.Type == k["EFFECT_TYPE_POLYMORPH"] && x.Ints[5] != 0).Select(x => x.LinkId).ToHashSet();
                if (waiting.Count > 0) expired.RemoveAll(e => waiting.Contains(e.LinkId));
            }
            if (expired.Count > 0) RemoveLinks(o, expired);
            if (o is Creature { IsDead: false } drained && drained.TotalLevel > 0
                && o.Effects.Where(e => e.Type == k["EFFECT_TYPE_NEGATIVELEVEL"]).Sum(e => e.Ints[0]) >= drained.TotalLevel)
                HitPoints.Kill(_world, drained, GameObject.InvalidId);
            foreach (var e in o.Effects.Where(e => e.NextTick > 0 && now >= e.NextTick).ToList())
            {
                if (!o.Effects.Contains(e) || o.IsDestroyed) continue;
                if (e.Type == k.Regenerate)
                {
                    e.NextTick += Interval(e.Floats[0]);
                    HitPoints.Heal(_world, o, e.Ints[0]);
                }
                else if (e.Type == k.RunScript)
                {
                    e.NextTick += Interval(e.Floats[0]);
                    RunScriptEvent(e, o, e.Strings[3], "ON_INTERVAL");
                }
                else if (e.Type == k["EFFECT_TYPE_DISEASE"]) DiseaseTick(k, e, o);
                else if (e.Type == k["EFFECT_TYPE_POISON"])
                {
                    e.NextTick = 0;
                    PoisonSecondary(k, e, o);
                }
                else if (e.Type == k.HitPointChangeWhenDying)
                {
                    e.NextTick += Math.Max(0.05, e.Duration);
                    if (o is Creature { IsDying: true } c)
                    {
                        // The tick that kills spends a script's temporary effect: death takes it off without its last
                        // change (dyingfx G4: -11). One made from an instant application takes its change once more as
                        // death removes it (bleedinst D2: -12).
                        if (!e.FromInstant) _silentEnd = e;
                        ChangeDying(c, e.Floats[0]);
                        _silentEnd = null;
                        // Raised above 0 by its own ticks, it ends there (bleedinst I3: +2 stopped at 1, the effect gone).
                        if (!c.IsDead && c.CurrentHitPoints > 0 && o.Effects.Contains(e)) EndSilently(c, e);
                    }
                    // A player no longer dying when it is due: it ends, changing nothing.
                    else if (o is Creature { IsDead: false } alive && alive.CurrentHitPoints > 0) EndSilently(alive, e);
                }
                else e.NextTick = 0;
            }
        }

        // About one change per 8 ms, as the original's hit points ran on (dyingbleed: -9 to -4 in 0.04 s).
        int drainSteps = Math.Max(1, (int)MathF.Round(dt * DyingDrainRate));
        foreach (var (id, drain) in _dyingDrain.ToList())
            for (int i = 0; i < drainSteps; i++)
            {
                if (_world.Get<Creature>(id) is not { IsDying: true } c) { _dyingDrain.Remove(id); break; }
                // One point each time (dyingbleed D: +0.5 ran from -9 to 1 as fast as +1 did).
                ChangeDying(c, drain.Change);
            }

        foreach (var item in _tempProperties.ToList())
        {
            if (item.IsDestroyed) { _tempProperties.Remove(item); continue; }
            int removed = item.Properties.RemoveAll(p => p.DurationType == k.Temporary && now >= p.ExpiresAt);
            if (removed > 0) _world.MarkChanged(item);
            if (!item.Properties.Any(p => p.DurationType == k.Temporary)) _tempProperties.Remove(item);
        }

        foreach (var aoe in _aoes.ToList()) StepAreaOfEffect(aoe, now);
        StepSwarms();
    }

    // ---------------------------------------------------------------- areas of effect

    /// <summary>Creates the AoE object for an EffectAreaOfEffect (vfx_persistent.2da row in Ints[0]; script overrides in Strings: enter, heartbeat, exit).</summary>
    public AreaOfEffect? CreateAreaOfEffect(Effect e, Area area, Vector3 position, float facing, GameObject? attachedTo, double expiresAt)
    {
        var table = _world.Resources.TwoDA("vfx_persistent");
        int row = e.Ints[0];
        if (row < 0 || row >= table.RowCount) return null;
        string shape = table.Get(row, "SHAPE");
        var aoe = new AreaOfEffect
        {
            AoeId = row,
            Tag = table.Get(row, "LABEL"),
            Shape = shape.Length > 0 ? char.ToUpperInvariant(shape[0]) : 'C',
            Radius = table.GetFloat(row, "RADIUS"),
            Width = table.GetFloat(row, "WIDTH"),
            Length = table.GetFloat(row, "LENGTH"),
            // An area of effect the module made has no creator (GetAreaOfEffectCreator is invalid), like the original.
            Creator = _world.Get(e.Creator) is Module ? GameObject.InvalidId : e.Creator,
            AttachedTo = attachedTo?.Id ?? GameObject.InvalidId,
            EffectId = e.UniqueId,
            SpellId = e.SpellId,
            CasterLevel = e.CasterLevel,
            ExpiresAt = expiresAt,
        };
        aoe.Scripts["OnObjEnter"] = e.Strings[0].Length > 0 ? e.Strings[0] : table.Get(row, "ONENTER");
        aoe.Scripts["OnHeartbeat"] = e.Strings[1].Length > 0 ? e.Strings[1] : table.Get(row, "HEARTBEAT");
        aoe.Scripts["OnObjExit"] = e.Strings[2].Length > 0 ? e.Strings[2] : table.Get(row, "ONEXIT");
        aoe.Scripts["OnUserDefined"] = "";
        _world.AddToArea(aoe, area, position, facing);
        if (aoe.LastSpell is { } context)
        {
            // Native fills the applied AoE effect's script-visible spell context at creation.
            // Constructor-only effects keep zeroes; the final sentinel's meaning is unmeasured.
            e.Ints[1] = 1;
            e.Ints[3] = context.MetaMagic;
            e.Ints[4] = context.SaveDC;
            e.Ints[5] = 65535;
        }
        aoe.NextHeartbeat = _world.Time + World.HeartbeatInterval;
        _aoes.Add(aoe);
        Notify("aoe-created", area, new
        {
            id = aoe.Id, aoe = row, shape = aoe.Shape.ToString(), radius = aoe.Radius, width = aoe.Width, length = aoe.Length,
            pos = Vec(position), facing = aoe.FacingDegrees, attachedTo = aoe.AttachedTo,
        });
        StepAreaOfEffect(aoe, _world.Time, heartbeat: false);
        return aoe;
    }

    void StepAreaOfEffect(AreaOfEffect aoe, double now, bool heartbeat = true)
    {
        if (aoe.IsDestroyed || aoe.Area == null) { Forget(aoe); return; }
        if (aoe.AttachedTo != GameObject.InvalidId)
        {
            var host = _world.Get(aoe.AttachedTo);
            if (host == null || host.Area == null) { DestroyAreaOfEffect(aoe); return; }
            if (host.Area != aoe.Area) _world.AddToArea(aoe, host.Area, host.Position, host.Facing);
            else aoe.Position = host.Position;
        }
        if (aoe.ExpiresAt > 0 && now >= aoe.ExpiresAt) { DestroyAreaOfEffect(aoe); return; }

        // Membership changes only when the area of effect appears or moves, or when the object moves: a creature
        // created inside a fog already there, and standing still, never enters it (parity suite aoe_subarea, LATE).
        bool fresh = aoe.LastPosition != aoe.Position;
        aoe.LastPosition = aoe.Position;
        var present = new HashSet<uint>();
        var inside = new List<uint>();
        foreach (var o in aoe.Area.Objects)
        {
            if (o.IsDestroyed || o == aoe || (o.Type & aoe.Tracks) == 0) continue;
            present.Add(o.Id);
            bool moved = aoe.LastSeen.TryGetValue(o.Id, out var last) && last != o.Position;
            aoe.LastSeen[o.Id] = o.Position;
            if (fresh || moved ? aoe.Contains(o.Position) : aoe.Inside.Contains(o.Id)) inside.Add(o.Id);
        }
        foreach (var id in aoe.LastSeen.Keys.Where(id => !present.Contains(id)).ToList()) aoe.LastSeen.Remove(id);
        foreach (var id in aoe.Inside.Where(id => !inside.Contains(id)).ToList())
        {
            aoe.Inside.Remove(id);
            _world.RunEvent(aoe, "OnObjExit", new EventContext().Set("Exiting", id));
            if (aoe.IsDestroyed) return;
        }
        foreach (var id in inside.Where(id => !aoe.Inside.Contains(id)))
        {
            aoe.Inside.Add(id);
            _world.RunEvent(aoe, "OnObjEnter", new EventContext().Set("Entering", id));
            if (aoe.IsDestroyed) return;
        }
        if (heartbeat && now >= aoe.NextHeartbeat)
        {
            aoe.NextHeartbeat = Math.Max(aoe.NextHeartbeat + World.HeartbeatInterval, now);
            _world.RunEvent(aoe, "OnHeartbeat", new EventContext());
        }
    }

    /// <summary>Ends an area of effect: exit events for everything inside, then the object goes (and its effect, if applied to an object).</summary>
    public void DestroyAreaOfEffect(AreaOfEffect aoe)
    {
        if (aoe.IsDestroyed) { Forget(aoe); return; }
        _world.Destroy(aoe);
    }

    // DestroyObject and expiration both fire exits while the AoE still exists.
    internal void BeforeAreaObjectDestroyed(AreaOfEffect aoe)
    {
        foreach (var id in aoe.Inside.ToList())
        {
            aoe.Inside.Remove(id);
            _world.RunEvent(aoe, "OnObjExit", new EventContext().Set("Exiting", id));
        }
    }

    /// <summary>Every destruction path, including script DestroyObject, ends the lasting client visual.</summary>
    internal void AreaObjectDestroyed(AreaOfEffect aoe, Area? previousArea)
    {
        if (previousArea != null) Notify("aoe-removed", previousArea, new { id = aoe.Id });
        Forget(aoe);
    }

    /// <summary>Stops tracking an AoE that is gone, removing the effect that created it (DestroyObject on an AoE ends its effect).</summary>
    void Forget(AreaOfEffect aoe)
    {
        if (!_aoes.Remove(aoe)) return;
        if (_world.Get(aoe.AttachedTo) is { } host)
            RemoveWhere(host, e => e.AreaObject == aoe.Id || (aoe.EffectId != 0 && e.UniqueId == aoe.EffectId));
    }

    // ---------------------------------------------------------------- special effects

    /// <summary>
    /// EffectDispelMagicAll/Best, as measured on the original: every magical effect (with or without a spell) can go,
    /// one link at a time, on d20 + caster level against 11 + the effect's caster level. Best takes the first one
    /// in list order that fails to resist. USE_CREATURE_LEVEL means the caster level of the spell that made the
    /// dispel, and outside a spell nothing is dispelled. The effects go on the next update, not at once.
    /// </summary>
    void Dispel(Effect e, GameObject target, bool best)
    {
        var k = K;
        int level = e.Ints[0] > 0 ? e.Ints[0] : e.CasterLevel;
        if (level <= 0) return;
        double due = _world.Time + UnpolymorphDelay;
        _world.After(0, () =>
        {
            if (target.IsDestroyed) return;
            var groups = target.Effects
                .Where(x => !x.Hidden && x.SubType == k.Magical)
                .GroupBy(x => x.LinkId)
                .Select(g => (Level: g.Max(x => x.CasterLevel), Effects: g.ToList()))
                .ToList();
            foreach (var g in groups)
            {
                if (Random.Shared.Next(1, 21) + level < 11 + g.Level) continue;
                // A dispelled polymorph stays listed until the creature changes back.
                if (IsPolymorphLink(k, g.Effects)) DeferPolymorphEnd(target, target.Effects.Where(x => x.LinkId == g.Effects[0].LinkId).ToList(), due);
                else RemoveLinks(target, g.Effects);
                if (best) break;
            }
        });
    }

    void Summon(Effect e, Location location, GameObject? owner, int durationType, float duration)
    {
        if (_world.Get(location.Area) is not Area area || owner == null) return;
        string resref = e.Strings[0];
        bool adoption = resref.Length == 0;
        var adopted = adoption ? _world.Get<Creature>(e.Objects[0]) : null;
        // oSummonToAdd is only used with a blank resource name and an unowned
        // creature. Rejected adoption must not replace a current summon.
        if (adoption && (adopted == null || _world.Get<Creature>(adopted.Master) != null)) return;
        e.SummonedObject = GameObject.InvalidId;
        bool stored = durationType != K.Instant;
        if (stored)
        {
            // The native effect is listed on the master before arrival. Its lifetime
            // starts now, and its identity survives replacement or detachment.
            Land([e], owner, durationType, duration, hidden: false);
            if (!owner.Effects.Contains(e)) return;
            // A destroyed master is no longer ticked, but its summoned creature
            // still lasts only until this effect's original expiry.
            if (durationType == K.Temporary)
                _world.After(Math.Max(0, duration), () =>
                {
                    if (owner.IsDestroyed) RemoveOnly(owner, e);
                });
        }
        // Native non-creature object targets retain the effect but never create
        // a creature. Location effects require a creature creator as their owner.
        if (owner is not Creature master) return;
        if (adopted?.Area is { } existingArea)
        {
            area = existingArea;
            location = adopted.Location;
        }
        double delay = Math.Max(0, e.Floats[0]);
        if (e.Ints[0] >= 0)
            Notify("vfx", area, new { vfx = e.Ints[0], miss = false, scale = 1f, pos = Vec(location.Position), facing = location.FacingDegrees, translate = Vec(default), rotate = Vec(default), duration = 0f, id = 0L });
        void Complete()
        {
            if (master.IsDestroyed || (stored && (!master.Effects.Contains(e)
                || (e.DurationType == K.Temporary && _world.Time >= e.ExpiresAt)))) return;
            int summoned = K["ASSOCIATE_TYPE_SUMMONED"];
            // A new summon replaces the master's current one.
            foreach (var old in _world.AllObjects.OfType<Creature>().Where(x => x.Master == master.Id && x.AssociateType == summoned && !x.IsDestroyed).ToList())
                Unsummon(old, e.Ints[2]);
            Creature? c = adopted ?? Scripting.Functions.ObjectFunctions.Spawn(_world,
                (int)ObjectType.Creature, resref, area, location.Position, location.FacingDegrees * MathF.PI / 180f) as Creature;
            if (c == null) return;
            if (c != master) { c.Master = master.Id; c.AssociateType = summoned; c.Faction = master.Faction; }
            if (!adoption && e.Ints[1] != 0) Notify("appear", c, new { animation = 1 });
            if (!adoption && c != master) CreatureRules.SummonedLine(_world, master);
            e.SummonedObject = c.Id;
        }
        // Adoption keeps the creature in place and ignores arrival delay/animation.
        // Native saves retain the caster effect but discard an arrival that has
        // not happened yet (instant, temporary and permanent controls).
        if (adoption) Complete(); else _world.After(delay, Complete, persist: false);
    }

    void Unsummon(Creature c, int vfx)
    {
        if (vfx >= 0) Notify("vfx", c, new { vfx, miss = false, scale = 1f, translate = Vec(default), rotate = Vec(default), duration = 0f, id = 0L });
        _world.Destroy(c);
    }

    void Disappear(GameObject target, Effect e, bool permanent)
    {
        Notify("disappear", target, new { animation = e.Ints[0] });
        if (permanent)
        {
            uint id = target.Id;
            _world.After(2.5, () => { if (_world.Get(id) is { } o) _world.Destroy(o); });
        }
    }

    /// <summary>EffectDisappearAppear: Objects[0]/Vectors[0]/Floats[0] hold the destination area, position and facing.</summary>
    void DisappearAppear(GameObject target, Effect e)
    {
        Notify("disappear", target, new { animation = e.Ints[0] });
        var dest = new Location(e.Objects[0], e.Vectors[0], e.Floats[0]);
        uint id = target.Id;
        _world.After(2.5, () =>
        {
            if (_world.Get(id) is not { } o) return;
            _world.Jump(o, dest);
            Notify("appear", o, new { animation = e.Ints[0] });
        });
    }

    // ---------------------------------------------------------------- client data

    /// <summary>Tells clients in the subject's area; objects that aren't in an area (items in inventories) have no one to show it to.</summary>
    void Notify(string kind, GameObject subject, object data)
    {
        if (subject is Area || subject.Area != null) _world.Output?.Notify(kind, subject, data);
    }

    static float[] Vec(Vector3 v) => new[] { v.X, v.Y, v.Z };

    /// <summary>
    /// The lasting visual effects and beams on an object, as the notifies that start them: sent to a player
    /// who sees the object for the first time (entering the area, logging in later), so a statue's aura
    /// applied at module load shows for everyone, not only for those present when it was applied.
    /// </summary>
    public IEnumerable<(string Kind, object Data)> LastingVisuals(GameObject o)
    {
        var k = K;
        double now = _world.Time;
        foreach (var e in o.Effects)
        {
            if (e.DurationType == k.Instant) continue;
            float left = e.DurationType == k.Temporary ? (float)Math.Max(0, e.ExpiresAt - now) : -1f;
            if (e.DurationType == k.Temporary && left <= 0) continue;
            if (e.Type == k.VisualEffect) yield return ("vfx", VfxData(e, left, e.UniqueId));
            else if (e.Type == k.Beam) yield return ("beam", BeamData(e, left, e.UniqueId));
        }
    }

    static object VfxData(Effect e, float duration, long id) => new
    {
        vfx = e.Ints[0], creator = e.Creator, miss = e.Ints[1] != 0, scale = e.Floats[0],
        translate = Vec(e.Vectors[0]), rotate = Vec(e.Vectors[1]), duration, id,
    };

    static object BeamData(Effect e, float duration, long id) => new
    {
        vfx = e.Ints[0], from = e.Objects[0], bodyPart = e.Ints[1], miss = e.Ints[2] != 0, scale = e.Floats[0],
        translate = Vec(e.Vectors[0]), rotate = Vec(e.Vectors[1]), duration, id,
    };

    Dictionary<string, int>? _iconRows;
    Dictionary<int, string>? _typeNames;

    /// <summary>
    /// The effecticons.2da row shown for an effect: EffectIcon's own id, otherwise the row whose
    /// label is the effect's EFFECT_TYPE_* name (ability effects add _STR, _DEX, ...).
    /// </summary>
    int IconOf(EffectConstants k, Effect e)
    {
        if (e.Type == k.Icon) return e.Ints.Length > 0 ? e.Ints[0] : 0;
        if (_iconRows == null)
        {
            var t = _world.Resources.TwoDA("effecticons");
            _iconRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int row = 1; row < t.RowCount; row++)
                if (t.Get(row, "Label") is { Length: > 0 } label) _iconRows.TryAdd(label, row);
            _typeNames = new Dictionary<int, string>();
            if (_world.Scripts?.Definitions is { } defs)
                foreach (var (name, (_, value)) in defs.Constants)
                    if (name.StartsWith("EFFECT_TYPE_", StringComparison.Ordinal) && int.TryParse(value, out int v))
                        _typeNames.TryAdd(v, name["EFFECT_TYPE_".Length..]);
        }
        if (!_typeNames!.TryGetValue(e.Type, out var typeName)) return 0;
        // Rows whose label isn't the effect type's name (the original's portrait, parity/visual scene conditions):
        // blindness shows BLIND (BLINDNESS has no icon), etherealness ETHEREALNESS, petrification PETRIFIED, see
        // invisibility SEEINVISIBILITY (visual scene conditionai).
        typeName = typeName switch
        {
            "BLINDNESS" => "BLIND",
            "ETHEREAL" => "ETHEREALNESS",
            "PETRIFY" => "PETRIFIED",
            "NEGATIVELEVEL" => "LEVELDRAIN",
            "SEEINVISIBLE" => "SEEINVISIBILITY",
            _ => typeName,
        };
        if (typeName is "ABILITY_INCREASE" or "ABILITY_DECREASE" && e.Ints.Length > 0)
            typeName += e.Ints[0] switch { 0 => "_STR", 1 => "_DEX", 2 => "_CON", 3 => "_INT", 4 => "_WIS", _ => "_CHA" };
        return _iconRows.TryGetValue(typeName, out int icon) ? icon : 0;
    }

    /// <summary>Authoritative portrait icons, including effects present at login/area changes.
    /// Identical icons share a tile until the last contributing effect is removed. A polymorph's bonus hit points
    /// (hidden from the effect list) still show the temporary hit points icon after the polymorph's own
    /// (visual scene polymorphpc: the Rakshasa form shows both, the water elemental, without HPBONUS, only one).</summary>
    public int[] PortraitIcons(GameObject target) => target.Effects
        .Where(e => (!e.Hidden || IsPolymorphBonus(e, target)) && (e.DurationType != K.Temporary || e.ExpiresAt > _world.Time))
        .OrderBy(e => e.UniqueId).SelectMany(e => IconsOf(K, e)).Where(icon => icon > 0).Distinct().ToArray();

    /// <summary>
    /// The icons an effect puts on the portrait: a dazed player also shows the movement-speed-decrease icon before
    /// the daze icon (parity/visual scene conditions, p03), as daze slows her to a walk.
    /// </summary>
    IEnumerable<int> IconsOf(EffectConstants k, Effect e)
    {
        int icon = IconOf(k, e); // also reads the table the first time
        if (e.Type == k["EFFECT_TYPE_DAZED"] && _iconRows!.TryGetValue("MOVEMENT_SPEED_DECREASE", out int slowed)) yield return slowed;
        yield return icon;
    }

    object Describe(EffectConstants k, Effect e) => new
    {
        id = e.UniqueId, type = e.Type, durationType = e.DurationType, duration = e.Duration,
        spellId = e.SpellId, creator = e.Creator, linkId = e.LinkId, icon = IconOf(k, e), subType = e.SubType,
    };
}
