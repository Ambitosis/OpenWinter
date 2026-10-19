using System.Numerics;
using System.Runtime.CompilerServices;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Society;

/// <summary>
/// Creature perception. NPCs update every four seconds from creation; PCs use a responsive update budget. Each works out
/// which creatures in its area it sees and hears, and its OnPerception (ScriptOnNotice) script runs once per
/// change with GetLastPerceived and one of GetLastPerceptionSeen / Heard / Vanished / Inaudible set, in the order
/// <see cref="Apply"/> describes.
/// <para>
/// Measured on nwserver (stealth_rules, stealth_sanctuary and the stealth_check_* suites; docs/STEALTH.md):
/// sight needs the sight range (ranges.2da PrimaryRange of the creature's PerceptionRange row, or of appearance.2da
/// PERCEPTIONDIST; in combat row PercepRngLng or, if that is longer, PercepRngMonster), a clear
/// <see cref="LineOfSight"/>, an observer that isn't blind (blindness, or darkness without ultravision; True Seeing
/// cancels both) and a target not invisible to it (invisibility, darkness on it, sanctuary), and a stealthy target
/// has to be spotted (<see cref="Skills.SkillSystem.SpotSucceeds"/>). Hearing needs the hearing range
/// (SecondaryRange), no deafness or silence, and, when the target is invisible or the observer blind, the close
/// range (<see cref="CloseRange"/>); a stealthy target has to be heard (<see cref="Skills.SkillSystem.ListenSucceeds"/>).
/// What is perceived sticks: a creature seen and heard isn't checked again while it stays in sight (invisibility
/// takes the sight away at the next pass, the hearing one pass later); one only seen stays seen, one only heard
/// stays heard, while in sight. DMs and dead observers perceive nothing new.
/// </para>
/// </summary>
public sealed class PerceptionSystem : IWorldSystem
{
    public const float Interval = 4f;
    // Native creaturevispc callbacks change within 0.061-0.212s of door commands.
    // 0.1s is our responsive budget, not a measured native scheduler period.
    public const float PlayerInterval = 0.1f;
    /// <summary>
    /// At AI_LEVEL_VERY_LOW (an area without players) an NPC's perception updates every six seconds, the first about
    /// 7.8 s after it appears (nwserver ai_events: seen at 7.6-7.9 s, a target jumping out of or back into range noticed
    /// 1.6-2.1 s after jumps at 12 and 24 s).
    /// </summary>
    public const float VeryLowInterval = 6f, VeryLowFirst = 7.8f;

    // EFFECT_TYPE_* (nwscript.nss).
    public const int EffectDeaf = 13, EffectSilence = 34, EffectInvisibility = 56, EffectImprovedInvisibility = 57, EffectDarkness = 58,
        EffectSanctuary = 63, EffectTrueSeeing = 64, EffectSeeInvisible = 65, EffectBlindness = 67, EffectUltravision = 70, EffectEthereal = 81;
    public const int InvisibilityNormal = 1, InvisibilityDarkness = 2, InvisibilityImproved = 4;
    // skills.2da rows.
    public const int SkillHide = 5, SkillListen = 6, SkillMoveSilently = 8, SkillSpot = 17;

    readonly World _world;
    /// <summary>
    /// Sanctuary saves already made: (observer, sanctuary effect) → <see cref="SanctuarySaved"/>, or how far a
    /// failed observer has lost the warded creature (0 not yet, 1 sight, 2 sight and hearing).
    /// </summary>
    readonly Dictionary<(uint, long), int> _sanctuarySaves = new();
    const int SanctuarySaved = -1;

    public PerceptionSystem(World world)
    {
        _world = world;
        world.ObjectChanged += InvalidateBlocker;
        world.CreatureLeftArea += Left;
        world.ObjectRemoved += o =>
        {
            InvalidateBlocker(o);
            if (o is not Creature gone) return;
            // A destroyed creature leaves everyone's lists at once, as one leaving the area does: "inaudible", then
            // "vanished" (nwserver perception_leave: both in the frame of the DestroyObject).
            foreach (var other in world.AllObjects.ToList())
            {
                if (other is not Creature c || c == gone) continue;
                bool heard = c.Society.Heard.Remove(o.Id), seen = c.Society.Seen.Remove(o.Id);
                c.Society.Listed.Remove(o.Id);
                if (c.IsDestroyed) continue;
                if (heard) Notice(c, o.Id, "PerceptionInaudible");
                if (seen && !c.IsDestroyed) Notice(c, o.Id, "PerceptionVanished");
            }
        };
    }

    public int Order => 10;

    /// <summary>
    /// A creature jumped or went through a transition to another area: every creature in the area it left drops it at
    /// once, with OnPerception "inaudible" (if it heard it) and then "vanished" (if it saw it), and its own lists are
    /// emptied without events of its own. Measured on nwserver (stealth_check suites: dropped about 0.3 s after the
    /// jump order, not at the next pass; perception_leave for the order).
    /// </summary>
    void Left(Creature leaver, Area from)
    {
        // Measured for creatures that aren't players; a player's character going through a transition is left to the
        // next pass (not measurable on nwserver; a pursuer's stock scripts would otherwise give up the chase on
        // "vanished" before the original's pursuers do, PlayerTransitionFollowerTests).
        if (leaver.IsPC) return;
        leaver.Society.Seen.Clear();
        leaver.Society.Heard.Clear();
        leaver.Society.Listed.Clear();
        foreach (var o in from.Objects.ToList())
        {
            if (o is not Creature c || c == leaver || c.IsDestroyed) continue;
            var s = c.Society;
            bool heard = s.Heard.Remove(leaver.Id), seen = s.Seen.Remove(leaver.Id);
            s.Listed.Remove(leaver.Id);
            if (heard) Notice(c, leaver.Id, "PerceptionInaudible");
            if (seen && !c.IsDestroyed) Notice(c, leaver.Id, "PerceptionVanished");
        }
    }

    void Notice(Creature c, uint target, string key)
    {
        var ctx = new EventContext()
            .Set("Perceived", target)
            .Set("PerceptionSeen", key == "PerceptionSeen" ? 1 : 0)
            .Set("PerceptionHeard", key == "PerceptionHeard" ? 1 : 0)
            .Set("PerceptionVanished", key == "PerceptionVanished" ? 1 : 0)
            .Set("PerceptionInaudible", key == "PerceptionInaudible" ? 1 : 0)
            .Set("EventScriptType", SocietyService.EventScriptCreatureOnNotice);
        _world.RunEvent(c, "ScriptOnNotice", ctx);
    }

    readonly List<Creature> _creatures = new(), _due = new();
    const double SightCacheSeconds = 2;
    readonly Dictionary<(uint, uint), (Vector3 A, Vector3 B, double At, bool Clear)> _sightCache = new();
    // These derived caches are host/runtime state, excluded by the snapshot serializer.
    readonly ConditionalWeakTable<GameObject, Area> _blockerAreas = new();
    readonly ConditionalWeakTable<Area, PlayerSightArea> _playerSightAreas = new();
    sealed class PlayerSightArea
    {
        public readonly Dictionary<(uint, uint), PlayerSight> Pairs = new();
        public readonly Dictionary<Door, bool> Doors = new();
        public long Version;
    }
    readonly record struct PlayerSight(Vector3 A, Vector3 B, int WalkmeshVersion,
        long SightVersion, double UsedAt, bool Clear);
    /// <summary>Actual PC perception LOS queries, excluding cache hits (for simulation profiling).</summary>
    public long PlayerSightQueries { get; private set; }
    double _sightCachePruned;
    List<Creature>? _areaCreatures;

    void InvalidateBlocker(GameObject o)
    {
        if (o is not (Door or Placeable)) return;
        // Remember the previous area: destroyed objects have already been detached, and
        // transitions must invalidate both areas. Ordinary actor notifications don't flush pairs.
        _blockerAreas.TryGetValue(o, out var old);
        if (old != null && _playerSightAreas.TryGetValue(old, out var previous))
        {
            previous.Version++;
            if (o is Door removed) previous.Doors.Remove(removed);
        }
        _blockerAreas.Remove(o);
        if (!o.IsDestroyed && o.Area is { } area)
        {
            if (_playerSightAreas.TryGetValue(area, out var current))
            {
                if (area != old) current.Version++;
                if (o is Door added) current.Doors[added] = added.IsOpen;
            }
            _blockerAreas.Add(o, area);
        }
    }

    PlayerSightArea PlayerSightFor(Area area)
    {
        if (_playerSightAreas.TryGetValue(area, out var cached)) return cached;
        var state = new PlayerSightArea();
        // Also seed after a saved-world restore, which need not raise ObjectAdded/Changed.
        foreach (var o in area.Objects)
            if (o is Door or Placeable)
            {
                _blockerAreas.AddOrUpdate(o, area);
                if (o is Door door) state.Doors.Add(door, door.IsOpen);
            }
        _playerSightAreas.Add(area, state);
        return state;
    }

    static void RefreshDoors(PlayerSightArea state)
    {
        // IsOpen is public save-game state, so direct/script writes need not emit a notification.
        // Check cheap metadata once per observer update, never once per candidate sight line.
        foreach (var (door, wasOpen) in state.Doors)
            if (door.IsOpen != wasOpen)
            {
                state.Version++;
                state.Doors[door] = door.IsOpen;
            }
    }

    public void Step(float dt)
    {
        double now = _world.Time;
        foreach (var area in _world.Areas)
        {
            if (area.Objects.Count == 0) continue;
            // Only creatures perceive and are perceived; areas hold far more placeables than creatures.
            _creatures.Clear();
            foreach (var o in area.Objects) if (o is Creature cr) _creatures.Add(cr);
            if (_creatures.Count == 0) continue;
            _areaCreatures = _creatures;
            bool players = false;
            foreach (var c in _creatures) if (c.IsPC) { players = true; break; }
            _due.Clear();
            foreach (var c in _creatures)
            {
                if (c.IsDestroyed) continue;
                var s = c.Society;
                int level = _world.EffectiveAILevel(c, players);
                _world.NoteAILevel(c, level);
                if (!c.IsPC)
                {
                    // An NPC's update is due an interval after its last one, by its AI level at the time (a creature
                    // attacked at very low AI is in combat at the normal level, and perceives at the normal pace).
                    if (s.NextPerception < 0) { s.NextPerception = s.PerceptionFrom = now; s.PerceivedOnce = false; continue; }
                    double wait = level == 0 ? (s.PerceivedOnce ? VeryLowInterval : VeryLowFirst) : Interval;
                    if (now < s.PerceptionFrom + wait) continue;
                    s.PerceptionFrom = s.NextPerception = now;
                    s.PerceivedOnce = true;
                    s.PlayersAtPass = players ? area.Id : GameObject.InvalidId;
                    _due.Add(c);
                    continue;
                }
                bool newPlayerArea = s.PerceptionArea != area.Id;
                if (!newPlayerArea && now < s.NextPerception) continue;
                s.NextPerception = now + PlayerInterval;
                _due.Add(c);
            }
            // Creatures due together are served by AI level, highest first (player characters at VERY_HIGH), then in
            // the order they joined their level (nwserver sched_percept).
            if (_due.Count > 1) _due.Sort(static (a, b) =>
            {
                int la = a.IsPC ? 4 : a.AILevelNow, lb = b.IsPC ? 4 : b.AILevelNow;
                return la != lb ? lb.CompareTo(la) : a.AILevelJoin.CompareTo(b.AILevelJoin);
            });
            // What one update queued with no delay runs before the next one (World.RunEventsBeforeUpdate).
            var due = _due.ToArray();
            for (int i = 0; i < due.Length; i++)
            {
                if (i > 0) _world.RunEventsBeforeUpdate();
                if (!due[i].IsDestroyed && due[i].Area == area) Update(due[i]);
            }
        }
        _areaCreatures = null;
        if (now - _sightCachePruned > 10)
        {
            _sightCachePruned = now;
            foreach (var (k, v) in _sightCache.ToList()) if (now - v.At >= SightCacheSeconds) _sightCache.Remove(k);
            foreach (var (_, state) in _playerSightAreas)
                foreach (var (k, v) in state.Pairs.ToList())
                    if (now - v.UsedAt >= SightCacheSeconds) state.Pairs.Remove(k);
        }
    }

    /// <summary>Recomputes one creature's seen/heard lists now and fires its perception events.</summary>
    public void Update(Creature c)
    {
        var s = c.Society;
        var area = c.Area;
        if (area == null || c.IsDestroyed) return;
        if (SocietyService.IsDead(c)) return; // the dead perceive nothing new
        if (c.IsPC) RefreshDoors(PlayerSightFor(area));
        var skills = _world.System<Skills.SkillSystem>();
        if (c.DetectAlertPending) { c.DetectAlertPending = false; c.DetectAlert = true; }

        var seen = new HashSet<uint>();
        var heard = new HashSet<uint>();
        Ranges(c, out float sight, out float hearing);
        bool trueSeeing = Has(c, EffectTrueSeeing);
        bool blind = Blind(c, trueSeeing);
        bool canHear = !Has(c, EffectDeaf) && !Has(c, EffectSilence);
        bool refreshed = false;
        var rooms = RoomVisibility.For(_world, area);
        var soundRooms = RoomVisibility.ForSound(_world, area);
        IEnumerable<GameObject> candidates = _areaCreatures is { } list && list.Count > 0 && list[0].Area == area ? list : area.Objects;
        foreach (var o in candidates)
        {
            if (o is not Creature t || t == c || t.IsDestroyed) continue;
            if (t.IsDMUndetectable) continue; // hidden DM avatars are never perceived
            // Ethereal creatures are hidden from foes only: the original's player sees an ethereal merchant
            // (parity/visual scene conditions, row f).
            if (Has(t, EffectEthereal) && _world.Factions.ReputationOf(c, t) <= FactionTable.HostileTo) continue;
            float dist = Vector3.Distance(c.Position, t.Position);
            if (dist > sight && dist > hearing) continue;
            // In an interior area a creature in a room sealed off from the observer's is neither seen nor heard;
            // sound still carries to the neighbouring tiles (SoundCarries).
            bool roomsAllow = rooms == null || rooms.AllowsPerception(area, c.Position, t.Position);
            bool soundAllowed = soundRooms == null || soundRooms.AllowsPerception(area, c.Position, t.Position) || SoundCarries(area, c, t);
            if (!roomsAllow && !soundAllowed) continue;
            var ward = Sanctuary(c, t);
            if (ward == SanctuaryHidesAll) continue;
            // Evaluating someone refreshes this creature's stealth dice (at most every 6 s).
            if (!refreshed) { skills.RefreshDice(c); refreshed = true; }
            bool recheck = s.MustRecheck.Remove(t.Id);
            bool wasSeen = !recheck && s.Seen.Contains(t.Id), wasHeard = !recheck && s.Heard.Contains(t.Id);
            bool invisible = InvisibleTo(c, t, trueSeeing);
            // creaturevisrange: an unobstructed PC target at exactly row12's 35m is unseen.
            // Keep the separately measured NPC comparison unchanged.
            bool inSight = c.IsPC ? dist < sight : dist <= sight;
            bool lookable = !blind && !invisible && ward == SanctuaryNone && inSight && roomsAllow;
            bool los = (lookable || wasSeen || wasHeard) && (c.IsPC ? PlayerSees(c, t) : NpcSees(c, t));
            // Interior and underground areas: behind an obstruction it is heard only along a sound path.
            bool audible = soundAllowed || los;
            bool silenced = Has(t, EffectSilence);
            bool sees, hears;
            if (wasSeen && wasHeard && los && inSight && canHear && !silenced)
            {
                // Seen and heard: no checks while it stays in sight range and view. Invisibility (or blindness) takes
                // the sight away now and the hearing at the next pass, unless it is near enough (stealth_check_invis
                // transitions).
                sees = lookable;
                hears = true;
            }
            else
            {
                sees = lookable && los && (wasSeen || !t.StealthMode || trueSeeing || skills.AlwaysPerceives(c, t)
                    || skills.Rank(t, Skills.SkillIds.Hide) == 0 || skills.SpotSucceeds(c, t));
                // An invisible creature, or anything near a blind observer, is heard only at close range.
                bool closeOnly = blind || invisible;
                hears = canHear && !silenced && audible && dist <= hearing && (!closeOnly || dist <= CloseRange(c, t))
                    && (wasHeard && los && !closeOnly || !t.StealthMode || skills.AlwaysPerceives(c, t)
                        || skills.Rank(t, Skills.SkillIds.MoveSilently) == 0 || skills.ListenSucceeds(c, t, dist));
            }
            // A player's character doesn't hear an invisible creature, whatever its Move Silently (visual scenes
            // stealthview and stealthself).
            if (hears && c.IsPC && invisible && (Has(t, EffectInvisibility) || Has(t, EffectImprovedInvisibility))) hears = false;
            if (sees) seen.Add(t.Id);
            if (hears) heard.Add(t.Id);
        }
        s.PerceptionArea = area.Id;
        Apply(c, seen, heard, MathF.Max(sight, hearing));
    }

    /// <summary>
    /// Sound through walls in an interior or underground area reaches the tiles next to the observer's, diagonals
    /// included, but no further unless the rooms connect, and not past a closed door standing in either tile. Measured on
    /// nwserver (stealth_check_interior_far: tde01 tile 121, walled corners with no walkable way between them; plain
    /// creatures in the tiles one step east, north and diagonal were heard, none two or more tiles away, in interior and
    /// underground areas alike; all heard in an exterior area).
    /// </summary>
    static bool SoundCarries(Area area, Creature c, Creature t)
    {
        int cx = (int)MathF.Floor(c.Position.X / 10), cy = (int)MathF.Floor(c.Position.Y / 10);
        int tx = (int)MathF.Floor(t.Position.X / 10), ty = (int)MathF.Floor(t.Position.Y / 10);
        if (Math.Abs(cx - tx) > 1 || Math.Abs(cy - ty) > 1) return false;
        foreach (var d in area.Doors)
        {
            if (d.IsOpen) continue;
            int dx = (int)MathF.Floor(d.Position.X / 10), dy = (int)MathF.Floor(d.Position.Y / 10);
            if (dx == cx && dy == cy || dx == tx && dy == ty) return false;
        }
        return true;
    }

    /// <summary>
    /// Blind for perception: blindness, or darkness around it without ultravision; True Seeing cancels both
    /// (stealth_check_blind).
    /// </summary>
    static bool Blind(Creature c, bool trueSeeing) => !trueSeeing
        && (Has(c, EffectBlindness) || Has(c, EffectDarkness) && !Has(c, EffectUltravision));

    /// <summary>
    /// The target is invisible to the observer: invisibility it doesn't see through (See Invisibility or True Seeing;
    /// for darkness-type invisibility Ultravision or True Seeing), or darkness on it without ultravision.
    /// </summary>
    static bool InvisibleTo(Creature c, Creature t, bool trueSeeing)
    {
        bool seeInvisible = trueSeeing || Has(c, EffectSeeInvisible);
        bool ultravision = trueSeeing || Has(c, EffectUltravision);
        foreach (var e in Effects(t))
        {
            switch (e.Type)
            {
                case EffectInvisibility or EffectImprovedInvisibility:
                {
                    int kind = e.Ints.Length > 0 && e.Ints[0] != 0 ? e.Ints[0] : (e.Type == EffectImprovedInvisibility ? InvisibilityImproved : InvisibilityNormal);
                    if (kind == InvisibilityDarkness ? !ultravision : !seeInvisible) return true;
                    break;
                }
                case EffectDarkness when !ultravision:
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// How near an invisible creature (or anything, to a blind observer) must be to be heard: the observer's
    /// appearance.2da PREFATCKDIST + the target's HITDIST + 1.5 m (stealth_check_invis: 3.5 m for two humans, 2.9 m
    /// for a halfling hearing a human, 3.45 m for a human hearing a halfling, 4.4 m for a human hearing a bear).
    /// </summary>
    public float CloseRange(Creature observer, Creature target)
    {
        var app = _world.Resources.TwoDA("appearance");
        return app.GetFloat(observer.AppearanceType, "PREFATCKDIST", 1.7f) + app.GetFloat(target.AppearanceType, "HITDIST", 0.3f) + 1.5f;
    }

    /// <summary>
    /// Records a pass's result and runs its OnPerception events. Measured on nwserver (sched_percept): the creatures
    /// already on the perception list come first, the most recently noticed first, then the newly noticed ones west to
    /// east (increasing X; nwserver ai_melee as well), newest first at equal X. A newly noticed creature gives "heard"
    /// then "seen"; one on the list that gains senses "seen" then "heard"; one that loses them "inaudible" then
    /// "vanished". An entry stays on the list with neither sense while it is within range (an invisible, silenced
    /// creature 7 m away came back "seen" then "heard", in list order) and leaves it once out of range (one jumped 38 m
    /// away came back "heard" then "seen", after the list).
    /// </summary>
    void Apply(Creature c, HashSet<uint> seen, HashSet<uint> heard, float range)
    {
        var s = c.Society;
        var listed = s.Listed;
        // Anything perceived but missing from the list (an older save) counts as its oldest entries.
        foreach (var id in s.Heard.Concat(s.Seen))
            if (!listed.Contains(id)) listed.Insert(0, id);
        var events = new List<(uint Target, string Key)>();
        var wasListed = new HashSet<uint>(listed);
        for (int i = listed.Count - 1; i >= 0; i--)
        {
            uint id = listed[i];
            if (seen.Contains(id) && !s.Seen.Contains(id)) events.Add((id, "PerceptionSeen"));
            if (heard.Contains(id) && !s.Heard.Contains(id)) events.Add((id, "PerceptionHeard"));
            if (s.Heard.Contains(id) && !heard.Contains(id)) events.Add((id, "PerceptionInaudible"));
            if (s.Seen.Contains(id) && !seen.Contains(id)) events.Add((id, "PerceptionVanished"));
            if (_world.Get(id) is not Creature t || t.IsDestroyed || t.Area != c.Area || Vector3.Distance(c.Position, t.Position) > range)
                listed.RemoveAt(i);
        }
        foreach (var id in seen.Union(heard).Where(id => !wasListed.Contains(id))
                     .OrderBy(id => _world.Get(id) is { } t ? MathF.Round(t.Position.X, 2) : float.MaxValue).ThenByDescending(id => id))
        {
            if (heard.Contains(id)) events.Add((id, "PerceptionHeard"));
            if (seen.Contains(id)) events.Add((id, "PerceptionSeen"));
            listed.Add(id);
        }
        s.Seen.Clear(); s.Seen.UnionWith(seen);
        s.Heard.Clear(); s.Heard.UnionWith(heard);
        foreach (var (target, key) in events)
        {
            if (c.IsDestroyed) break;
            if (_world.Get(target) == null) continue;
            Notice(c, target, key);
        }
    }

    bool NpcSees(Creature c, Creature t)
    {
        // Sight lines are symmetric, and most creatures stand still: reuse a pair's answer while neither
        // has moved (half a metre) for up to SightCacheSeconds (doors opening show up within that).
        var key = c.Id < t.Id ? (c.Id, t.Id) : (t.Id, c.Id);
        var (a, b) = c.Id < t.Id ? (c.Position, t.Position) : (t.Position, c.Position);
        double now = _world.Time;
        if (_sightCache.TryGetValue(key, out var cached) && now - cached.At < SightCacheSeconds
            && Vector3.DistanceSquared(cached.A, a) < 0.25f && Vector3.DistanceSquared(cached.B, b) < 0.25f)
            return cached.Clear;
        bool clear = LineOfSight.Between(_world, c, t);
        _sightCache[key] = (a, b, now, clear);
        return clear;
    }

    bool PlayerSees(Creature c, Creature t)
    {
        if (c.Area is not { } area || t.Area != area) return false;
        var key = c.Id < t.Id ? (c.Id, t.Id) : (t.Id, c.Id);
        var (a, b) = c.Id < t.Id ? (c.Position, t.Position) : (t.Position, c.Position);
        double now = _world.Time;
        var state = PlayerSightFor(area);
        if (state.Pairs.TryGetValue(key, out var cached)
            && cached.A == a && cached.B == b && cached.WalkmeshVersion == area.Walkmesh.Version
            && cached.SightVersion == state.Version)
        {
            state.Pairs[key] = cached with { UsedAt = now };
            return cached.Clear;
        }
        PlayerSightQueries++;
        bool clear = LineOfSight.Between(_world, c, t);
        state.Pairs[key] = new(a, b, area.Walkmesh.Version, state.Version, now, clear);
        return clear;
    }

    /// <summary>
    /// Whether an attacker has failed its Will save against the target's sanctuary: it can't attack the target.
    /// Measured on nwserver (cond_misc sanct.*, cond_sprobe): the save is made at the attacker's perception update,
    /// so an attack ordered before the first update (4 s after it appeared) lands once; at the update the attack
    /// action is dropped, and later orders do nothing.
    /// </summary>
    public bool SanctuaryBars(Creature attacker, Creature target)
    {
        foreach (var e in Effects(target))
        {
            if (e.Type != EffectSanctuary) continue;
            var key = (attacker.Id, e.UniqueId != 0 ? e.UniqueId : e.GetHashCode());
            if (_sanctuarySaves.TryGetValue(key, out int state) && state != SanctuarySaved) return true;
        }
        return false;
    }

    const int SanctuaryNone = 0, SanctuaryHidesSight = 1, SanctuaryHidesAll = 2;

    /// <summary>
    /// Sanctuary, measured on nwserver (parity suite stealth_sanctuary, Prelude factions): a hostile observer makes
    /// one Will save. One that saves treats the warded creature as invisible: never seen, heard as usual. One that
    /// fails perceives it at the update where it first comes into its perception (seen and heard), loses sight of it
    /// at its next update and hearing at the one after; already perceiving it when the sanctuary lands, it loses
    /// sight at its next update and hearing at the one after. Only foes are affected.
    /// </summary>
    /// Players are never kept away (visual scene conditionai: the player attacked a goblin under sanctuary DC 100, no
    /// save shown, and earlier still saw a sanctuaried merchant). A creature's save is shown to its master, as the
    /// original showed the player her summoned badger's: "Summoned Dire Badger : Will Save : *failure* : (16 + 2 = 18
    /// vs. DC: 100)".
    int Sanctuary(Creature c, Creature t)
    {
        if (c.IsPC) return SanctuaryNone;
        int ward = SanctuaryNone;
        foreach (var e in Effects(t))
        {
            if (e.Type != EffectSanctuary) continue;
            if (_world.Factions.ReputationOf(c, t) > FactionTable.HostileTo) continue; // only foes are kept away
            var key = (c.Id, e.UniqueId != 0 ? e.UniqueId : e.GetHashCode());
            if (!_sanctuarySaves.TryGetValue(key, out int state)) state = SanctuarySave(c, t, e, key);
            else if (state == 0 || state == 1)
            {
                // A failed observer loses the creature a step at a time, from the update after it first perceived it.
                if (state == 1 || c.Society.Seen.Contains(t.Id) || c.Society.Heard.Contains(t.Id)) _sanctuarySaves[key] = ++state;
            }
            ward = Math.Max(ward, state == SanctuarySaved ? SanctuaryHidesSight : state);
        }
        return ward;
    }

    /// <summary>
    /// Whether a creature attacking the target has failed its Will save against the target's sanctuary, making the save
    /// now if it hasn't yet (a sanctuary landing on the creature it is fighting: nwserver ai_caster, the fighter stops at
    /// once). Doesn't advance what the observer still perceives.
    /// </summary>
    public bool SanctuaryFailedNow(Creature c, Creature t)
    {
        if (c.IsPC) return false;
        bool failed = false;
        foreach (var e in Effects(t))
        {
            if (e.Type != EffectSanctuary) continue;
            if (_world.Factions.ReputationOf(c, t) > FactionTable.HostileTo) continue;
            var key = (c.Id, e.UniqueId != 0 ? e.UniqueId : e.GetHashCode());
            if (!_sanctuarySaves.TryGetValue(key, out int state)) state = SanctuarySave(c, t, e, key);
            failed |= state != SanctuarySaved;
        }
        return failed;
    }

    int SanctuarySave(Creature c, Creature t, Effect e, (uint, long) key)
    {
        int dc = e.Ints.Length > 0 ? e.Ints[0] : 10;
        int will = Rules.CreatureRules.SavingThrow(_world, c, Rules.NwRules.SaveWill, Rules.NwRules.SaveTypeAll, t);
        int roll = Random.Shared.Next(1, 21);
        bool saved = roll == 20 || (roll != 1 && roll + will >= dc);
        bool perceived = c.Society.Seen.Contains(t.Id) || c.Society.Heard.Contains(t.Id);
        int state = _sanctuarySaves[key] = saved ? SanctuarySaved : perceived ? 1 : 0;
        if (_world.Get<Creature>(c.Master) is { IsPC: true } master)
            _world.Output?.SendMessage(master, $"{c.Name.Resolve(_world.Resources.Talk)} : Will Save : *{(saved ? "success" : "failure")}* : ({roll} + {will} = {roll + will} vs. DC: {dc})");
        return state;
    }

    /// <summary>Applied effects flattened through links.</summary>
    public static IEnumerable<Effect> Effects(GameObject o)
    {
        foreach (var e in o.Effects)
        {
            yield return e;
            foreach (var l in Linked(e)) yield return l;
        }
    }

    static IEnumerable<Effect> Linked(Effect e)
    {
        foreach (var l in e.Linked)
        {
            yield return l;
            foreach (var x in Linked(l)) yield return x;
        }
    }

    public static bool Has(GameObject o, int effectType)
    {
        foreach (var e in Effects(o)) if (e.Type == effectType) return true;
        return false;
    }

    /// <summary>ranges.2da rows the engine uses besides the creatures' own (PercepRngLng, PercepRngPlayer, PercepRngMonster).</summary>
    public const int RangesLong = 10, RangesPlayer = 12, RangesMonster = 13;

    /// <summary>
    /// Sight and hearing ranges in metres: ranges.2da PrimaryRange / SecondaryRange of the creature's PerceptionRange
    /// row (a player's: PercepRngPlayer), the default row deferring to appearance.2da PERCEPTIONDIST. A creature in
    /// combat (not a player) uses PercepRngLng if its own sight range is at least as long, otherwise PercepRngMonster:
    /// short and medium perception gain sight and lose hearing (nwserver stealth_check_ranges: 28/15 m in combat
    /// for rows 8, 9 and the default, 35/20 for row 10).
    /// </summary>
    public void Ranges(Creature c, out float sight, out float hearing)
    {
        var s = c.Society;
        int row = c.IsPC ? RangesPlayer : c.PerceptionRange;
        bool combat = !c.IsPC && c.InCombat;
        int key = (row & 0xFF) | ((c.AppearanceType & 0x3FFFFF) << 8) | (combat ? 1 << 30 : 0);
        if (s.RangesRow != key || float.IsNaN(s.SightRange))
        {
            var (sr, hr) = RowRanges(c, row);
            if (combat)
            {
                var lng = RowRanges(c, RangesLong);
                (sr, hr) = sr >= lng.Sight ? lng : RowRanges(c, RangesMonster);
            }
            s.SightRange = sr;
            s.HearingRange = hr;
            s.RangesRow = key;
        }
        sight = s.SightRange;
        hearing = s.HearingRange;
    }

    (float Sight, float Hearing) RowRanges(Creature c, int row)
    {
        var ranges = _world.Resources.TwoDA("ranges");
        if (ranges.RowCount == 0) return (20, 20);
        float sr = ranges.GetFloat(row, "PrimaryRange", -1), hr = ranges.GetFloat(row, "SecondaryRange", -1);
        if (sr < 0 || hr < 0)
        {
            // "Default" row: the appearance decides.
            int app = _world.Resources.TwoDA("appearance").GetInt(c.AppearanceType, "PERCEPTIONDIST", 9);
            float asr = ranges.GetFloat(app, "PrimaryRange", 20), ahr = ranges.GetFloat(app, "SecondaryRange", 20);
            if (sr < 0) sr = asr;
            if (hr < 0) hr = ahr;
        }
        return (sr, hr);
    }
}
