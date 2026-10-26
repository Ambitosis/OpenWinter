using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Skills;

/// <summary>
/// Stealth and detect modes and the Spot / Listen checks against Hide / Move Silently, measured on nwserver (parity
/// suites stealth_modes, stealth_rules and the stealth_check_* suites; docs/STEALTH.md has the figures):
/// <list type="bullet">
/// <item>Stealth and detect are independent modes (both can be on) and either one makes the creature walk.</item>
/// <item>A creature that is attacking can't enter stealth ("You cannot enter stealth mode while in combat"), with or
/// without Hide in Plain Sight; one that is only being attacked can.</item>
/// <item>Nobody rolls per pair: every creature caches a Hide, a Move Silently, a Spot and a Listen die
/// (<see cref="StealthDice"/>), refreshed at most every <see cref="DiceRefreshSeconds"/> inside its own perception
/// pass when it evaluates someone (so an NPC's dice change on every second pass, about every 8 s). Hide and Move
/// Silently are re-rolled (d20) only while it is in stealth; Spot and Listen are a d20 for an active observer
/// (detect mode or Keen Sense) and a d10 otherwise. A new creature's dice start at 1-10.</item>
/// <item>Every pass compares the cached dice: the hider is seen when the observer's Spot term + die + the area's
/// ModSpotCheck - the hider's die - Hide + modifiers is above 0 (<see cref="SpotTotal"/>), heard likewise with
/// Listen against Move Silently (<see cref="ListenTotal"/>). The term is the full skill when active, half
/// (towards zero) when not.</item>
/// <item>A hider that isn't in stealth, or whose Hide (Move Silently) total is exactly 0, is seen (heard) without a
/// check; so it is by an observer with True Seeing (sight only), its master or associates, DMs and (for players)
/// their party.</item>
/// <item>An attack ends stealth and detect mode when it swings, a spell when its casting starts, an item when it
/// is used. Damage, being attacked, invisibility and sanctuary don't.</item>
/// </list>
/// <see cref="Society.PerceptionSystem"/> decides when a check is made (ranges, sight lines, invisibility and what
/// stays perceived) and asks <see cref="SpotSucceeds"/> / <see cref="ListenSucceeds"/>.
/// </summary>
public sealed partial class SkillSystem
{
    /// <summary>A creature's dice are refreshed at most this often (in its own perception pass).</summary>
    public const double DiceRefreshSeconds = 6;
    /// <summary>Spot and Listen modifiers no table carries (nwserver stealth_check_rates, _mods, _detail).</summary>
    public const int StillHiderPenalty = 5, StillObserverBonus = 5, InCombatPenalty = 10;
    /// <summary>visualeffects.2da rows of the VFX_DUR_LIGHT_* effects and of VFX_DUR_DARKVISION.</summary>
    public const int FirstLightVfx = 153, LastLightVfx = 180, DarkvisionVfx = 182;

    /// <summary>A creature's cached stealth dice and when they were last refreshed (world time).</summary>
    public sealed class StealthDice
    {
        public int Hide, MoveSilently, Spot, Listen;
        public double RefreshedAt;
    }

    readonly Dictionary<uint, StealthDice> _dice = new();
    /// <summary>Rolls the stealth dice (1..sides); kept apart from <see cref="Dice"/> so perception doesn't use up a test's queued rolls.</summary>
    public Func<int, int> StealthDie = sides => Random.Shared.Next(1, Math.Max(1, sides) + 1);
    /// <summary>What each player was last told about a stealthy creature: (player creature, target) → (seen, heard).</summary>
    readonly Dictionary<(uint Pc, uint Target), (bool Seen, bool Heard)> _shown = new();

    /// <summary>A creature that is attacking can't enter stealth, Hide in Plain Sight or not (nwserver: stealth_modes).</summary>
    public bool CanEnterStealth(Creature c) => !_world.System<Combat.CombatSystem>().IsInCombat(c);

    /// <summary>
    /// Turns stealth (ACTION_MODE_STEALTH) or active detect (ACTION_MODE_DETECT) on or off: SetActionMode,
    /// and what a client's mode toggle should call. Returns false when the change isn't allowed
    /// (stealth while attacking); the player is told why.
    /// </summary>
    public bool SetActionMode(Creature c, int mode, bool on)
    {
        CreatureRules.EnsureInit(_world, c);
        switch (mode)
        {
            case SkillIds.ModeStealth:
                if (on == c.StealthMode) break;
                if (on && !CanEnterStealth(c))
                {
                    Tell(c, Tlk(SkillIds.StrNoStealthInCombat, "You cannot enter stealth mode while in combat."));
                    return false;
                }
                {
                    // NWNX_ON_STEALTH_ENTER / _EXIT (nwnx_events_creature): a skip keeps the mode; a result of "1" or
                    // "0" decides Hide in Plain Sight instead of the feat.
                    var ev = Nwnx.NwnxEvent.Begin(_world, on ? "NWNX_ON_STEALTH_ENTER" : "NWNX_ON_STEALTH_EXIT", c);
                    if (ev?.Before() == true) { ev.After(); return false; }
                    c.StealthMode = on;
                    if (on) EndRevealsOutOfSight(c);
                    if (on && (ev is { HasResult: true } ? ev.Result == "1" : HasFeat(c, SkillIds.FeatHideInPlainSight))) HideInPlainSight(c);
                    ModeChanged(c, mode, on);
                    ev?.After();
                }
                break;
            case SkillIds.ModeDetect:
                // Keen Sense supplies active searching continuously, without the walking restriction.
                if (HasFeat(c, SkillIds.FeatKeenSense)) return false;
                if (on == c.DetectMode) break;
                {
                    var ev = Nwnx.NwnxEvent.Begin(_world, on ? "NWNX_ON_DETECT_ENTER" : "NWNX_ON_DETECT_EXIT", c);
                    if (ev?.Before() == true) { ev.After(); return false; }
                    c.DetectMode = on;
                    ModeChanged(c, mode, on);
                    ev?.After();
                }
                break;
            default:
                return false;
        }
        if (on) c.ActionModes.Add(mode); else c.ActionModes.Remove(mode);
        return true;
    }

    /// <summary>Ends a creature's stealth and detect modes (an attack swinging, a spell or item being used; measured for both).</summary>
    public void BreakStealth(Creature c)
    {
        if (c.StealthMode) SetActionMode(c, SkillIds.ModeStealth, false);
        if (c.DetectMode) SetActionMode(c, SkillIds.ModeDetect, false);
    }

    void ModeChanged(Creature c, int mode, bool on)
    {
        _world.MarkChanged(c);
        Requests.AbilityRequests.NotifyMode(_world, c, mode, on);
    }

    /// <summary>
    /// Entering stealth with Hide in Plain Sight: whoever perceives the creature has to find it again at its next
    /// pass (nwserver stealth_rules hips: the observer gets inaudible and vanished events). Without it, entering
    /// stealth in view hides nothing: a creature that is seen and heard isn't checked while it stays in sight.
    /// </summary>
    void HideInPlainSight(Creature c)
    {
        if (c.Area == null) return;
        foreach (var o in c.Area.Objects)
            if (o is Creature watcher && watcher != c) watcher.Society.MustRecheck.Add(c.Id);
    }

    /// <summary>Stealth and active detect mode move at walking speed or slower (Movement reads <see cref="ModeStages"/>).</summary>
    public static bool ForcesWalk(World world, Creature c) => ModeStages(world, c) > 0;

    /// <summary>
    /// Movement stages the modes cost: stealth one, detect mode switched on one more, unless the creature has Keen Sense
    /// (visual scene encmodes: without Keen Sense, detect alone and stealth alone each walked at 2,
    /// both at 1 m/s; with Keen Sense detect cost nothing).
    /// </summary>
    public static int ModeStages(World world, Creature c) => (c.StealthMode ? 1 : 0)
        + (c.DetectMode && !CreatureRules.HasFeat(world, c, SkillIds.FeatKeenSense, ignoreUses: true) ? 1 : 0);

    /// <summary>
    /// An active observer (d20 and its full Spot and Listen): detect mode or Keen Sense. Standing idle doesn't make
    /// an NPC active (nwserver stealth_check_rates: idle 43% against 79% in detect mode).
    /// </summary>
    public static bool ActiveDetect(World world, Creature c) => c.DetectMode
        || CreatureRules.HasFeat(world, c, SkillIds.FeatKeenSense, ignoreUses: true);

    /// <summary>
    /// Whether an observer checks as active (a d20 and its full Spot and Listen): in detect mode, with Keen Sense, or
    /// standing in the idle stance out of combat (<see cref="Creature.IdleStance"/>). nwserver stealth_check_stance:
    /// observers made by CreateObject that never moved saw a still hider with Spot 21 against Hide 1 in 68.6% of passes
    /// (passive: 67.5%), the same observers jumped once into their spot 100% (154 of 154), and with Spot 10 against
    /// Hide 1 jumped 79.5% and walked once 86.4% (active: 83.5%, passive: 42.5%).
    /// </summary>
    public static bool ActiveObserver(World world, Creature c) => ActiveDetect(world, c) || c.IdleStance && !c.InCombat && Still(c);

    /// <summary>
    /// GetDetectMode: 1 in active detect mode; otherwise 2 once the creature has cast a spell or been hurt
    /// (nwserver, stealth_modes: lasts at least 90 s), else 0.
    /// </summary>
    public static int DetectModeValue(World world, Creature c) => ActiveDetect(world, c) ? 1 : c.DetectAlert ? 2 : 0;

    /// <summary>Friends who always see through a creature's stealth: itself, its master or associates, DMs, and a player's party.</summary>
    public bool AlwaysPerceives(Creature observer, Creature target)
    {
        if (observer == target || observer.Player?.IsDM == true) return true;
        if (observer.Master == target.Id || target.Master == observer.Id) return true;
        if (observer.Master != GameObject.InvalidId && observer.Master == target.Master) return true;
        // Players see their party members (not measurable on nwserver; NPC factions are measured not to).
        if (PlayerOf(observer) == null || PlayerOf(target) == null) return false;
        int? of = _world.Factions.FactionOf(observer), tf = _world.Factions.FactionOf(target);
        return of != null && of == tf;
    }

    // ------------------------------------------------------------------ dice

    /// <summary>
    /// A creature's cached dice. A new creature's start at 1-10 and are first refreshed at its first pass that evaluates
    /// someone (nwserver stealth_checks: observers made with their hiders spot them at their first pass as often as
    /// later; stealth_check_mods: a hider out of everyone's reach keeps a 1-10 die).
    /// </summary>
    public StealthDice DiceOf(Creature c)
    {
        if (!_dice.TryGetValue(c.Id, out var d))
        {
            d = new StealthDice { Hide = StealthDie(10), MoveSilently = StealthDie(10), Spot = StealthDie(10), Listen = StealthDie(10), RefreshedAt = double.NegativeInfinity };
            // A destroyed creature's dice are forgotten (ForgetStealth); don't keep new ones for it.
            if (!c.IsDestroyed) _dice[c.Id] = d;
        }
        return d;
    }

    /// <summary>Whether the creature has cached stealth dice (they go when it is destroyed).</summary>
    public bool HasStealthDice(uint id) => _dice.ContainsKey(id);

    /// <summary>
    /// Called by perception when <paramref name="c"/> evaluates someone in its own pass: at least
    /// <see cref="DiceRefreshSeconds"/> after the last refresh it rolls new Spot and Listen dice (a d20 when active,
    /// a d10 otherwise) and, in stealth, new Hide and Move Silently d20s. nwserver stealth_check_cadence: all of an
    /// idle NPC's sightings fall on every second pass; stealth_check_mods: a hider out of everyone's reach keeps its
    /// die.
    /// </summary>
    public void RefreshDice(Creature c)
    {
        var d = DiceOf(c);
        if (_world.Time - d.RefreshedAt < DiceRefreshSeconds - 1e-6) return;
        d.RefreshedAt = _world.Time;
        if (c.StealthMode) { d.Hide = StealthDie(20); d.MoveSilently = StealthDie(20); }
        int die = ActiveObserver(_world, c) ? 20 : 10;
        d.Spot = StealthDie(die);
        d.Listen = StealthDie(die);
    }

    // ------------------------------------------------------------------ checks

    /// <summary>
    /// Whether <paramref name="observer"/> spots stealthy <paramref name="hider"/> now (perception has made sure it is
    /// in Spot range and sight, not invisible to it, and the observer isn't blind). A tie goes to the hider. There is no
    /// natural 20: with a pass made while the hider was in place, a check the numbers can't fail never failed
    /// (nwserver stealth_check_stance: 166 of 166 and 154 of 154; the 94% of stealth_check_natural were readings taken
    /// before the observer's pass).
    /// </summary>
    public bool SpotSucceeds(Creature observer, Creature hider) => SpotTotal(observer, hider) > 0;

    /// <summary>Whether <paramref name="observer"/> hears stealthy <paramref name="hider"/> <paramref name="distance"/> metres away now.</summary>
    public bool ListenSucceeds(Creature observer, Creature hider, float distance) => ListenTotal(observer, hider, distance) > 0;

    /// <summary>
    /// The Spot check's total (seen when above 0): the observer's Spot (full when active, half otherwise) + its
    /// cached die + the area's ModSpotCheck - the hider's cached die and Hide; -5 for a still hider, +5 for a still
    /// observer, -10 for an observer in combat, the size difference (<see cref="SpotSizeModifier"/>) and, at night,
    /// light (<see cref="NightModifier"/>).
    /// </summary>
    public int SpotTotal(Creature observer, Creature hider)
    {
        int spot = Rank(observer, SkillIds.Spot);
        int total = (ActiveObserver(_world, observer) ? spot : spot / 2) + DiceOf(observer).Spot
            + (int)(observer.Area?.Are.GetInt("ModSpotCheck") ?? 0)
            - DiceOf(hider).Hide - Rank(hider, SkillIds.Hide);
        total += Situation(observer, hider);
        total += SpotSizeModifier(CreatureRules.CreatureSize(_world, hider) - CreatureRules.CreatureSize(_world, observer));
        return total + NightModifier(observer, hider);
    }

    /// <summary>
    /// The Listen check's total (heard when above 0): Listen (full or half) + die + ModListenCheck - the hider's die
    /// and Move Silently - a point per whole 3 m of distance, the still and combat modifiers of Spot, and the
    /// hider's own size (<see cref="ListenSizeModifier"/>). No night or light modifier.
    /// </summary>
    public int ListenTotal(Creature observer, Creature hider, float distance)
    {
        int listen = Rank(observer, SkillIds.Listen);
        int total = (ActiveObserver(_world, observer) ? listen : listen / 2) + DiceOf(observer).Listen
            + (int)(observer.Area?.Are.GetInt("ModListenCheck") ?? 0)
            - DiceOf(hider).MoveSilently - Rank(hider, SkillIds.MoveSilently)
            - (int)(distance / 3f);
        total += Situation(observer, hider);
        return total + ListenSizeModifier(CreatureRules.CreatureSize(_world, hider));
    }

    int Situation(Creature observer, Creature hider)
        => (Still(hider) ? -StillHiderPenalty : 0) + (Still(observer) ? StillObserverBonus : 0) + (observer.InCombat ? -InCombatPenalty : 0);

    /// <summary>Not moving at the moment of the check.</summary>
    static bool Still(Creature c) => c.Speed <= 0.05f;

    /// <summary>Spot by the hider's size category minus the observer's: -2/-1/+1/+2 steps give -8/-4/+4/+8, anything else 0.</summary>
    public static int SpotSizeModifier(int difference) => difference switch { -2 => -8, -1 => -4, 1 => 4, 2 => 8, _ => 0 };

    /// <summary>Listen by the hider's own size category: tiny -8, small -4, medium 0, large +4, huge +8.</summary>
    public static int ListenSizeModifier(int size) => size switch { 1 => -8, 2 => -4, 4 => 4, 5 => 8, _ => 0 };

    /// <summary>
    /// At night by the module clock: ruleset.2da SPOT_NO_LIGHT_MODIFIER off for an observer with neither a light
    /// (VFX_DUR_LIGHT_*) nor darkvision on it, HIDING_LIGHT_MODIFIER on for a hider carrying a light
    /// (nwserver stealth_check_night).
    /// </summary>
    public int NightModifier(Creature observer, Creature hider)
    {
        if (!_world.Clock.IsNight) return 0;
        int m = 0;
        if (!HasVfx(observer, FirstLightVfx, LastLightVfx) && !HasVfx(observer, DarkvisionVfx, DarkvisionVfx))
            m -= Ruleset.Int(_world, "SPOT_NO_LIGHT_MODIFIER", 5);
        if (HasVfx(hider, FirstLightVfx, LastLightVfx)) m += Ruleset.Int(_world, "HIDING_LIGHT_MODIFIER", 10);
        return m;
    }

    bool HasVfx(Creature c, int first, int last)
    {
        int type = Scripting.EffectConstants.Of(_world).VisualEffect;
        foreach (var e in Society.PerceptionSystem.Effects(c))
            if (e.Type == type && e.Ints.Length > 0 && e.Ints[0] >= first && e.Ints[0] <= last) return true;
        return false;
    }

    void ForgetStealth(uint id)
    {
        ForgetReveals(id);
        _dice.Remove(id);
        foreach (var k in _shown.Keys.Where(k => k.Pc == id || k.Target == id).ToList()) _shown.Remove(k);
    }

    /// <summary>
    /// Tells each player which stealthy creatures in their area they currently see or hear
    /// (<c>stealth-visibility</c>), so the client can hide the ones they don't; and when a creature
    /// leaves stealth or the area, that it is no longer hidden.
    /// </summary>
    void StepStealthVisibility()
    {
        if (_world.Output == null) return;
        var live = new HashSet<(uint, uint)>();
        foreach (var area in _world.Areas)
        {
            List<Creature>? pcs = null, stealthy = null;
            foreach (var o in area.Objects)
            {
                if (o is not Creature c || c.IsDestroyed) continue;
                if (c.IsPC) (pcs ??= new()).Add(c);
                if (c.StealthMode) (stealthy ??= new()).Add(c);
            }
            if (pcs == null || stealthy == null) continue;
            foreach (var pc in pcs)
                foreach (var t in stealthy)
                {
                    if (t == pc) continue;
                    bool friend = AlwaysPerceives(pc, t);
                    bool seen = friend || pc.Society.Seen.Contains(t.Id), heard = friend || pc.Society.Heard.Contains(t.Id);
                    var key = (pc.Id, t.Id);
                    live.Add(key);
                    if (_shown.TryGetValue(key, out var was) && was == (seen, heard)) continue;
                    _shown[key] = (seen, heard);
                    _world.Output.Notify("stealth-visibility", t, new { target = t.Id, stealth = true, seen, heard }, pc);
                }
        }
        foreach (var key in _shown.Keys.Where(k => !live.Contains(k)).ToList())
        {
            _shown.Remove(key);
            if (_world.Get<Creature>(key.Pc) is { } pc && _world.Get(key.Target) is { } t)
                _world.Output.Notify("stealth-visibility", t, new { target = t.Id, stealth = false, seen = true, heard = true }, pc);
        }
    }
}
