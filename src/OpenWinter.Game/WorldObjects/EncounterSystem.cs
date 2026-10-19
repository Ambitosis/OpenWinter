using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;

namespace OpenWinter.Game.WorldObjects;

/// <summary>Runtime state of an encounter (from the UTE/GIT fields).</summary>
public sealed class EncounterState
{
    public sealed record Entry(string ResRef, float CR, bool SingleSpawn);

    public readonly List<Entry> Creatures = new();
    /// <summary>Spawn points in area coordinates, with facing in radians.</summary>
    public readonly List<(Vector3 Position, float Facing)> SpawnPoints = new();
    /// <summary>Row in encdifficulty.2da (ENCOUNTER_DIFFICULTY_*).</summary>
    public int DifficultyIndex = 2;
    public int MaxCreatures = 8, RecCreatures = 1;
    /// <summary>Respawns field: how many times a continuous encounter may respawn (-1 = without end). GetEncounterSpawnsMax.</summary>
    public int SpawnsMax = -1;
    /// <summary>Respawns so far (the first wave doesn't count). GetEncounterSpawnsCurrent.</summary>
    public int SpawnsCurrent;
    /// <summary>A continuous encounter respawns ResetTime seconds after it was exhausted only when Reset is set.</summary>
    public bool Reset = true;
    public float ResetTime = 60;
    /// <summary>0 = continuous, 1 = single shot (measured; the stock groups are 1, single shot).</summary>
    public int SpawnOption;
    public bool PlayerOnly = true;
    public int Faction = 1;

    /// <summary>Creatures currently inside the polygon (OnEntered has fired for them).</summary>
    public readonly HashSet<uint> Inside = new();
    /// <summary>Creatures spawned by the current wave.</summary>
    public readonly List<uint> Spawned = new();
    /// <summary>A wave is out: nothing more spawns until every creature of it is dead or gone.</summary>
    public bool Triggered;
    /// <summary>When the last wave was exhausted, and whether a respawn is waiting for ResetTime.</summary>
    public double ExhaustedAt = double.NegativeInfinity;
    public bool RespawnPending;
    /// <summary>The creature that set off the last wave.</summary>
    public uint LastTrigger = GameObject.InvalidId;
    /// <summary>
    /// The last creature of any kind to step into the polygon: the party strength of a wave is measured around it
    /// (a reset's respawn included).
    /// </summary>
    public uint LastEntered = GameObject.InvalidId;
    /// <summary>The point pool: each wave adds its budget, each spawned creature takes its list value. Not saved.</summary>
    public float Pool;
    /// <summary>The list values of the wave's creatures, less each dead one's own challenge-rating value.</summary>
    public float PointsInPlay;
    /// <summary>Unique (SingleSpawn) entries used since the encounter was last exhausted.</summary>
    public readonly HashSet<int> UsedUnique = new();
    /// <summary>The last challenge rating seen of each creature of the wave still counted (a destroyed one keeps it).</summary>
    public readonly Dictionary<uint, float> Counted = new();

    public void Load(GffStruct s)
    {
        Creatures.Clear();
        foreach (var c in s.GetList("CreatureList"))
            Creatures.Add(new Entry(c.GetString("ResRef"), c.GetFloat("CR", c.GetInt("CR")), c.GetInt("SingleSpawn") != 0));
        SpawnPoints.Clear();
        foreach (var p in s.GetList("SpawnPointList"))
            SpawnPoints.Add((new Vector3(p.GetFloat("X"), p.GetFloat("Y"), p.GetFloat("Z")), p.GetFloat("Orientation")));
        DifficultyIndex = (int)s.GetInt("DifficultyIndex", 2);
        MaxCreatures = (int)Math.Max(1, s.GetInt("MaxCreatures", 8));
        RecCreatures = (int)Math.Max(1, s.GetInt("RecCreatures", 1));
        SpawnsMax = (int)s.GetInt("Respawns", -1);
        SpawnsCurrent = (int)s.GetInt("NumberSpawned", 0);
        Reset = s.GetInt("Reset", 1) != 0;
        ResetTime = s.GetInt("ResetTime", 60);
        SpawnOption = (int)s.GetInt("SpawnOption");
        PlayerOnly = s.GetInt("PlayerOnly", 1) != 0;
        Faction = (int)s.GetInt("Faction", 1);
    }

    public void CopyFrom(EncounterState o)
    {
        Creatures.AddRange(o.Creatures);
        SpawnPoints.AddRange(o.SpawnPoints);
        DifficultyIndex = o.DifficultyIndex; MaxCreatures = o.MaxCreatures; RecCreatures = o.RecCreatures;
        SpawnsMax = o.SpawnsMax; SpawnsCurrent = o.SpawnsCurrent; Reset = o.Reset; ResetTime = o.ResetTime;
        SpawnOption = o.SpawnOption; PlayerOnly = o.PlayerOnly; Faction = o.Faction;
    }
}

/// <summary>
/// Standard encounters, as measured on the original (docs/ENCOUNTERS.md; visual scenes encounterspawn, encounterrules,
/// encounterbudget, encounterparty, encountermix; parity suite encounter_events). Spawning is native: the stock
/// encounter blueprints have no scripts.
/// - OnEntered / OnExit fire for every creature crossing the polygon, active or not; OnHeartbeat only while active.
/// - A wave spawns right after OnEntered when the encounter is active and has no wave out, the creature may set it
///   off (a player, or with PlayerOnly off any creature), the encounter's faction is hostile to it, and a player is
///   in the area (nothing spawns on a server with no player in the area).
/// - Its size (matching the measured wave sizes and encounterwaves): the party strength S is 1.5^level summed over the
///   players and their associates within 30 m along each axis of the last creature to enter; party level =
///   trunc(log1.5(S / 4) + 0.5); the wave's budget, 1.5^(party level + encdifficulty VALUE - 1), goes into a point
///   pool. Creatures are bought from the pool less the points still in play, highest challenge rating first, with a
///   tenth of it as slack (<see cref="ChooseEntries"/>), at least RecCreatures, at most MaxCreatures. A dying creature
///   takes its own CR's value out of the points in play, so a list CR above the blueprint's leaves points behind that
///   shrink the respawn.
/// - OnExhausted fires once every creature of the wave is dead or gone; the encounter is then inactive. A continuous
///   encounter (SpawnOption 0) with Reset set comes back ResetTime seconds later and respawns (counting
///   GetEncounterSpawnsCurrent) while respawns are left (Respawns, -1 = without end), then clears the pool and the
///   points in play. A single-shot one stays off.
/// </summary>
public sealed class EncounterSystem : IWorldSystem
{
    readonly World _world;
    public int Order => 20;

    public EncounterSystem(World world) { _world = world; }

    /// <summary>The encdifficulty.2da VALUE of a difficulty row (GetEncounterDifficulty returns this).</summary>
    public static int DifficultyValue(World world, int index) =>
        world.Resources.TwoDA("encdifficulty").GetInt(index, "VALUE", index switch { 0 => 0, 1 => 1, 2 => 2, 3 => 5, _ => 9 });

    public void Step(float dt)
    {
        foreach (var area in _world.Areas.ToArray())
        {
            List<Encounter>? encounters = null;
            foreach (var o in area.Objects)
                if (o is Encounter e && !e.IsDestroyed) (encounters ??= new()).Add(e);
            if (encounters == null) continue;
            var creatures = area.Objects.OfType<Creature>().Where(c => !c.IsDestroyed && !c.IsDead).ToList();
            bool playerHere = creatures.Any(c => c.IsPC && !c.IsDMAvatar);
            foreach (var e in encounters) StepEncounter(area, e, creatures, playerHere);
        }
    }

    void StepEncounter(Area area, Encounter e, List<Creature> creatures, bool playerHere)
    {
        var st = e.State;
        // Exits (including creatures that left the area or died).
        foreach (var id in st.Inside.ToList())
        {
            var c = _world.Get<Creature>(id);
            if (c == null || c.Area != area || c.IsDead || !Contains(e, c.Position))
            {
                st.Inside.Remove(id);
                _world.RunEvent(e, "OnExit", new EventContext().Set("Exiting", id));
            }
        }
        // Entries: every creature fires OnEntered; one that qualifies sets off a wave.
        foreach (var c in creatures)
        {
            if (st.Inside.Contains(c.Id) || !Contains(e, c.Position)) continue;
            st.Inside.Add(c.Id);
            st.LastEntered = c.Id;
            _world.RunEvent(e, "OnEntered", new EventContext().Set("Entering", c.Id));
            if (e.IsDestroyed) return;
            if (playerHere && CanTrigger(e, c)) TrySpawn(e, c);
        }

        // A creature of the wave that died or is gone takes its own challenge rating's value out of the points in play.
        foreach (var id in st.Counted.Keys.ToList())
        {
            var c = _world.Get<Creature>(id);
            if (c != null && !c.IsDead && !c.IsDestroyed) { st.Counted[id] = c.ChallengeRating; continue; }
            float cr = c?.ChallengeRating ?? st.Counted[id];
            st.Counted.Remove(id);
            st.PointsInPlay = Math.Max(0f, st.PointsInPlay - PointValue(cr));
        }

        // Exhausted: every creature of the wave is dead or gone. The encounter turns itself off.
        if (st.Triggered && st.Spawned.All(id => _world.Get<Creature>(id) is not { IsDead: false }))
        {
            st.Triggered = false;
            st.Spawned.Clear();
            st.UsedUnique.Clear();
            e.Active = false;
            st.ExhaustedAt = _world.Time;
            st.RespawnPending = st.SpawnOption == 0 && st.Reset && (st.SpawnsMax < 0 || st.SpawnsCurrent < st.SpawnsMax);
            _world.RunEvent(e, "OnExhausted", new EventContext());
            if (e.IsDestroyed) return;
        }

        // A continuous encounter comes back after ResetTime (counting a respawn, whether or not anyone is there:
        // measured with the player in another area) and spawns at once if someone who may set it off is inside, else
        // on the next entry. Straight after that attempt the pool and the points in play are cleared.
        if (st.RespawnPending && _world.Time - st.ExhaustedAt >= st.ResetTime)
        {
            st.RespawnPending = false;
            e.Active = true;
            st.SpawnsCurrent++;
            var who = st.Inside.Select(id => _world.Get<Creature>(id)).FirstOrDefault(c => c != null && CanTrigger(e, c));
            if (who != null && playerHere) TrySpawn(e, who);
            st.Pool = 0;
            st.PointsInPlay = 0;
        }
    }

    /// <summary>Whether <paramref name="c"/> sets off <paramref name="e"/> (active, no wave out, may trigger, hostile).</summary>
    public bool CanTrigger(Encounter e, Creature c)
    {
        if (!e.Active || e.State.Triggered || c.IsDMAvatar || c.IsDead) return false;
        if (e.State.PlayerOnly && !c.IsPC) return false;
        // Measured: an encounter of the commoner faction never spawned for the player, and NPCs set off only
        // encounters whose faction is hostile to them.
        return _world.Factions.Standing(e.State.Faction, c) <= FactionTable.HostileTo;
    }

    public static bool Contains(Encounter e, Vector3 p)
    {
        var g = e.Geometry;
        bool inside = false;
        for (int i = 0, j = g.Count - 1; i < g.Count; j = i++)
        {
            var a = g[i]; var b = g[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y + 1e-12f) + a.X) inside = !inside;
        }
        return inside;
    }

    /// <summary>
    /// Fires the encounter for <paramref name="trigger"/> if it is ready: the party strength round the last-entered
    /// creature adds a budget to the pool, and creatures are chosen from what the pool holds beyond the points still in
    /// play (<see cref="ChooseEntries"/>). Returns the number spawned.
    /// </summary>
    public int TrySpawn(Encounter e, Creature trigger)
    {
        var st = e.State;
        if (!e.Active || st.Triggered || e.Area == null || st.Creatures.Count == 0) return 0;
        st.LastTrigger = trigger.Id;
        var center = _world.Get<Creature>(st.LastEntered);
        float strength = center == null || center.IsDestroyed || center.Area != e.Area ? 0 : PartyStrength(_world, e, center);
        // Nobody counted: the pool is left as it is (an earlier wave's leftover may still buy something).
        if (strength > 0)
            st.Pool += Budget(PartyLevel(strength) + DifficultyValue(_world, st.DifficultyIndex));
        if (st.Pool <= 0) return 0;
        int alive = st.Spawned.Count(id => _world.Get<Creature>(id) is { IsDead: false });
        var picks = ChooseEntries(st, alive, Random.Shared);
        if (picks.Count == 0) return 0;
        if (st.Pool < st.Creatures.Min(c => PointValue(c.CR))) st.Pool = 0;

        var (pos, _) = SpawnPoint(e, center ?? trigger);
        int count = 0;
        foreach (int i in picks)
        {
            // Measured: every creature of a wave starts at the one spawn point (the engine moves each off the
            // others) and faces east, whatever the point's orientation.
            var o = ObjectFunctions.Spawn(_world, 1, st.Creatures[i].ResRef, e.Area, pos, 0f);
            if (o is not Creature c) continue;
            float value = PointValue(st.Creatures[i].CR);
            st.Pool = Math.Max(0f, st.Pool - value);
            st.PointsInPlay += value;
            c.SpawnedByEncounter = e.Id;
            st.Spawned.Add(c.Id);
            st.Counted[c.Id] = c.ChallengeRating;
            count++;
        }
        st.Triggered = st.Spawned.Count > 0;
        return count;
    }

    /// <summary>
    /// The party strength round <paramref name="center"/> (the encounter's last-entered creature): 1.5^level summed over
    /// every creature in a square reaching <see cref="PartyRange"/> along each axis from it (height ignored) that is
    /// alive, not dying, hostile to the encounter, and a player or an associate whose master is a player (or is gone).
    /// Plain creatures never count. Level is the higher of hit dice and the level experience gives.
    /// </summary>
    public static float PartyStrength(World world, Encounter e, Creature center)
    {
        float sum = 0;
        if (center.Area == null) return 0;
        foreach (var c in center.Area.Objects.OfType<Creature>())
        {
            if (c.IsDestroyed || c.IsDead || c.CurrentHitPoints <= 0 || c.IsDMAvatar) continue;
            if (MathF.Abs(c.Position.X - center.Position.X) > PartyRange || MathF.Abs(c.Position.Y - center.Position.Y) > PartyRange) continue;
            if (!c.IsPC && (c.Master == GameObject.InvalidId || world.Get<Creature>(c.Master) is { IsPC: false })) continue;
            if (world.Factions.Standing(e.State.Faction, c) > FactionTable.HostileTo) continue;
            sum += MathF.Pow(1.5f, MemberLevel(world, c));
        }
        return sum;
    }

    /// <summary>How far along each axis from the last-entered creature a party member still counts (a square).</summary>
    public const float PartyRange = 30f;

    static int MemberLevel(World world, Creature c) => Math.Max(1, Math.Max(c.TotalLevel, CreatureRules.LevelForXP(world, c.Experience)));

    /// <summary>
    /// The party level of a strength S: log1.5(S / 4) + 0.5, truncated toward zero (four members of level L make L,
    /// one makes L - 3).
    /// </summary>
    public static int PartyLevel(float strength) => (int)(MathF.Log(strength / 4f) / MathF.Log(1.5f) + 0.5f);

    /// <summary>The party level of members of these levels (for tests and tools).</summary>
    public static int PartyLevel(IEnumerable<int> levels) => PartyLevel(levels.Sum(l => MathF.Pow(1.5f, l)));

    /// <summary>A wave's budget for encounter level T (party level + encdifficulty VALUE): the point value of T - 1.</summary>
    public static float Budget(int level) => PointValue(level - 1);

    /// <summary>
    /// The points a challenge rating is worth: 1.5^(whole part) from 1 to 50 (1.5^CR above), 1 at 0, below 1 the
    /// fractionalcr.2da steps (0.4 and up 1, 0.3 2/3, 0.2 4/9, 0.15 8/27, less 16/81), 1.5^CR when negative.
    /// </summary>
    public static float PointValue(float cr)
    {
        if (cr >= 1) return cr <= 50 ? MathF.Pow(1.5f, MathF.Floor(cr)) : MathF.Pow(1.5f, cr);
        if (cr == 0) return 1f;
        if (cr < 0) return MathF.Pow(1.5f, cr);
        if (cr >= 0.4f) return 1f;
        if (cr >= 0.3f) return 2f / 3f;
        if (cr >= 0.2f) return 4f / 9f;
        if (cr >= 0.15f) return 8f / 27f;
        return 16f / 81f;
    }

    /// <summary>
    /// The creatures of a wave (list indices), from the pool beyond the points in play (A) with a tenth of it as slack:
    /// the list from the highest challenge rating down, as many of each as still fit A + slack (a random one among
    /// entries of the same rating, a unique entry once per cycle), while A is above 0 and MaxCreatures leaves room;
    /// CR 0 entries are left out. Short of RecCreatures, chosen creatures are broken down into cheaper ones (when the
    /// pool is above the cheapest value), then the wave is padded with the cheapest entry whatever the budget.
    /// </summary>
    public static List<int> ChooseEntries(EncounterState st, int alive, Random rng)
    {
        var result = new List<int>();
        if (st.Creatures.Count == 0) return result;
        float a = st.Pool - st.PointsInPlay, slack = a / 10f;
        int room = st.MaxCreatures - alive;
        // Highest challenge rating first; equal ratings keep their order (and form one group).
        var groups = Enumerable.Range(0, st.Creatures.Count).GroupBy(i => st.Creatures[i].CR).OrderByDescending(g => g.Key)
            .Select(g => g.ToList()).ToList();
        bool Used(int i) => st.Creatures[i].SingleSpawn && st.UsedUnique.Contains(i);
        void Take(int i)
        {
            if (st.Creatures[i].SingleSpawn) st.UsedUnique.Add(i);
        }
        foreach (var g in groups)
        {
            if (room <= 0 || a <= 0) break;
            float cr = st.Creatures[g[0]].CR;
            if (cr == 0) continue;
            float value = PointValue(cr);
            var tried = new HashSet<int>();
            while (value <= a + slack && room > 0)
            {
                int pick = g[rng.Next(g.Count)];
                tried.Add(pick);
                if (Used(pick))
                {
                    if (g.All(i => st.Creatures[i].SingleSpawn && tried.Contains(i))) break;
                    continue;
                }
                Take(pick);
                result.Add(pick);
                a -= value;
                room--;
            }
        }

        int need = Math.Min(Math.Max(1, st.RecCreatures), st.MaxCreatures) - alive;
        if (result.Count >= need) return result;
        float cheapest = st.Creatures.Min(c => PointValue(c.CR));
        // Break chosen creatures down into cheaper ones, the last chosen first.
        if (st.Pool > cheapest && result.Count > 0)
            for (int k = result.Count - 1; k >= 0 && result.Count < need; k--)
            {
                float own = PointValue(st.Creatures[result[k]].CR);
                if (own <= cheapest) continue;
                float left = own;
                var parts = new List<int>();
                foreach (var g in groups)
                {
                    float value = PointValue(st.Creatures[g[0]].CR);
                    if (value >= own) continue;
                    while (value <= left && result.Count - 1 + parts.Count < st.MaxCreatures - alive)
                    {
                        var free = g.Where(i => !Used(i)).ToList();
                        if (free.Count == 0) break;
                        int pick = free[rng.Next(free.Count)];
                        Take(pick);
                        parts.Add(pick);
                        left -= value;
                    }
                }
                if (parts.Count == 0) continue;
                result.RemoveAt(k);
                result.InsertRange(k, parts);
            }
        // Pad with the cheapest entries (a unique one used up gives way to the next cheapest, if there is one).
        var byCost = Enumerable.Range(0, st.Creatures.Count).OrderBy(i => PointValue(st.Creatures[i].CR)).ToList();
        while (result.Count < need && result.Count < st.MaxCreatures - alive)
        {
            var free = byCost.Where(i => !Used(i)).ToList();
            if (free.Count > 0)
            {
                float low = PointValue(st.Creatures[free[0]].CR);
                free = free.Where(i => PointValue(st.Creatures[i].CR) == low).ToList();
            }
            else free = byCost.Where(i => PointValue(st.Creatures[i].CR) == cheapest).ToList();
            int pick = free[rng.Next(free.Count)];
            Take(pick);
            result.Add(pick);
        }
        return result;
    }

    (Vector3, float) SpawnPoint(Encounter e, Creature trigger)
    {
        var st = e.State;
        // Measured: the spawn point farthest from the creature that set it off (the first of equally far ones).
        if (st.SpawnPoints.Count > 0)
        {
            var best = st.SpawnPoints[0];
            foreach (var sp in st.SpawnPoints)
                if (Vector3.DistanceSquared(sp.Position, trigger.Position) > Vector3.DistanceSquared(best.Position, trigger.Position) + 1e-4f) best = sp;
            return best;
        }
        // No spawn points: at the encounter's position (measured with a centred rectangle).
        var p = e.Position;
        if (e.Area!.Walkmesh.HighestGround(p.X, p.Y) is { } z) p.Z = z;
        return (p, 0f);
    }
}
