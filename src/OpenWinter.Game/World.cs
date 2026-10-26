using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game;

/// <summary>
/// The authoritative game state: the module, its areas and every object, plus
/// the simulation step and the script events it raises. Owned by the server;
/// nothing here touches rendering.
/// </summary>
public sealed partial class World
{
    public const float HeartbeatInterval = 6f;

    readonly Dictionary<uint, GameObject> _objects = new();
    readonly Dictionary<string, List<GameObject>> _byTag = new(StringComparer.OrdinalIgnoreCase);
    uint _nextId = 1;

    public ResMan Resources { get; }
    public ModelCache Models { get; }
    public CreatureFactory Creatures { get; }
    public ItemFactory Items { get; }
    public Module Module { get; } = new();
    public readonly List<Area> Areas = new();
    /// <summary>Seconds since the module started.</summary>
    public double Time { get; private set; }
    /// <summary>The world timer delayed commands run on: Time plus the jumps SetTime and SetCalendar made.</summary>
    public double GameTime => Time + _skipped;
    double _skipped;

    /// <summary>The clock jumped forward (SetTime, SetCalendar): everything due on the way comes due now.</summary>
    public void SkipTime(double seconds)
    {
        if (seconds > 0) _skipped += seconds;
    }
    public ScriptHost? Scripts { get; private set; }
    public IWorldOutput? Output;
    public AreaLoader? Loader { get; private set; }

    public event Action<GameObject>? ObjectChanged;
    public event Action<GameObject>? ObjectRemoved;
    /// <summary>An object entered an area (spawned, created or transitioned).</summary>
    public event Action<GameObject>? ObjectAdded;

    public World(ResMan resources)
    {
        Resources = resources;
        Models = new ModelCache(resources);
        Creatures = new CreatureFactory(this);
        Items = new ItemFactory(this);
        foreach (var type in typeof(World).Assembly.GetTypes())
            if (typeof(IWorldSystem).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(new[] { typeof(World) }) != null)
                Systems.Add((IWorldSystem)Activator.CreateInstance(type, this)!);
        Systems.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    /// <summary>Feature systems (perception, effects, encounters, ...) discovered by reflection.</summary>
    public readonly List<IWorldSystem> Systems = new();

    public T System<T>() where T : IWorldSystem => Systems.OfType<T>().First();

    /// <summary>Turns on scripting. Call before LoadModule so load-time events run.</summary>
    public void EnableScripts(NssDefinitions? definitions = null)
    {
        Scripts = new ScriptHost(this, definitions ?? ScriptHost.LoadDefinitions(Resources));
    }

    public void Register(GameObject o)
    {
        if (o.Id != 0) return;
        o.Id = _nextId++;
        _objects[o.Id] = o;
        IndexTag(o);
    }

    void IndexTag(GameObject o)
    {
        if (string.IsNullOrEmpty(o.Tag)) return;
        if (!_byTag.TryGetValue(o.Tag, out var list)) _byTag[o.Tag] = list = new List<GameObject>();
        list.Add(o);
    }

    /// <summary>Changes an object's tag, keeping the tag index current.</summary>
    public void SetTag(GameObject o, string tag)
    {
        if (_byTag.TryGetValue(o.Tag, out var list)) list.Remove(o);
        o.Tag = tag;
        IndexTag(o);
    }

    public GameObject? Get(uint id) => _objects.TryGetValue(id, out var o) && !o.IsDestroyed ? o : null;
    public T? Get<T>(uint id) where T : GameObject => Get(id) as T;
    public IEnumerable<GameObject> AllObjects => _objects.Values;

    /// <summary>Objects with a tag, in creation order (GetObjectByTag nth).</summary>
    public IReadOnlyList<GameObject> ByTag(string tag) =>
        _byTag.TryGetValue(tag, out var list) ? list.Where(o => !o.IsDestroyed).ToList() : Array.Empty<GameObject>();

    /// <summary>Called for every object LoadModule reads from an area's GIT, with its struct (see <see cref="AreaLoader.Loaded"/>).</summary>
    public Action<GameObject, GffStruct>? ObjectLoaded;

    public void LoadModule()
    {
        var ifo = Resources.ReadGff("module", ResType.Ifo) ?? throw new FileNotFoundException("module.ifo");
        Module.Ifo = ifo;
        Module.Name = ifo.GetLocString("Mod_Name");
        Module.Tag = ifo.GetString("Mod_Tag");
        Module.EntryArea = ifo.GetString("Mod_Entry_Area");
        Module.EntryPosition = new Vector3(ifo.GetFloat("Mod_Entry_X"), ifo.GetFloat("Mod_Entry_Y"), ifo.GetFloat("Mod_Entry_Z"));
        Module.EntryDirection = new Vector3(ifo.GetFloat("Mod_Entry_Dir_X"), ifo.GetFloat("Mod_Entry_Dir_Y"), 0);
        foreach (var f in ifo.Fields)
            if (f.Label.StartsWith("Mod_On", StringComparison.Ordinal) && f.Value is string s) Module.Scripts[f.Label] = s;
        Module.Locals.LoadFrom(ifo);
        Register(Module);
        Clock.Load(ifo);
        Factions.Load(Resources);

        Loader = new AreaLoader(this) { Loaded = ObjectLoaded };
        foreach (var entry in ifo.GetList("Mod_Area_list"))
        {
            var resref = entry.GetString("Area_Name");
            try { Areas.Add(Loader.Load(resref)); }
            catch (FileNotFoundException e) { Console.Error.WriteLine($"[world] {e.Message}"); }
        }

        // Like the original: for each placed creature OnAcquireItem for what it carries and the area's OnEnter, then
        // OnModuleLoad, the events it queued, then OnSpawn.
        foreach (var area in Areas)
            foreach (var c in area.Objects.OfType<Creature>().ToList())
            {
                Inventory.FireSpawnAcquires(c);
                After(0, () => RunEvent(area, "OnEnter", new EventContext().Set("Entering", c.Id)));
                QueueSpawn(c);
            }
        RunDueEvents();
        RunEvent(Module, "Mod_OnModLoad", new EventContext());
        RunDueEvents();
        RunPendingSpawns();
    }

    public readonly GameClock Clock = new();
    public readonly FactionTable Factions = new();

    public Area? FindArea(string resrefOrTag) =>
        Areas.FirstOrDefault(a => a.ResRef.Equals(resrefOrTag, StringComparison.OrdinalIgnoreCase))
        ?? Areas.FirstOrDefault(a => a.Tag.Equals(resrefOrTag, StringComparison.OrdinalIgnoreCase));

    static readonly HashSet<string> DeadSkip = new(StringComparer.OrdinalIgnoreCase)
    {
        "ScriptSpellAt", "ScriptHeartbeat", "ScriptOnNotice", "ScriptAttacked", "ScriptDamaged", "ScriptEndRound", "ScriptOnBlocked",
    };

    /// <summary>Runs an object's event script (the script named by one of its event fields).</summary>
    public int RunEvent(GameObject o, string field, EventContext ctx)
    {
        if (o is Area entered && !o.IsDestroyed) entered.NoteEntryEvent(field, ctx);
        if (Scripts == null || o.IsDestroyed) return 0;
        // A dead creature runs none of its combat and awareness events (nwserver ai_caster: an aura signalling the
        // corpses of the creatures it killed runs no OnSpellCastAt; ai_melee: corpses fire no OnHeartbeat).
        if (o is Creature { IsDead: true } && DeadSkip.Contains(field)) return 0;
        if (field != "ScriptSpawn") SpawnIfPending(o);
        var script = o.Script(field);
        // A caller may reuse its context for several objects/events, including nested callbacks.
        // Stamp a copy so the event identity cannot leak back into its caller.
        int type = EventScripts.Type(o, field);
        if (script.Length == 0) return 0;
        var scoped = ctx.Clone().Set("EventScriptType", type).Set("DirectEventScriptType", type);
        try { return Scripts.Run(script, o.Id, scoped); }
        finally { ctx.CopyPayloadFrom(scoped); }
    }

    /// <summary>
    /// Places an object in an area, firing the area's OnEnter for creatures (unless <paramref name="enterEvent"/> is
    /// false: creatures loaded from a database or JSON arrive without one). A creature goes to the nearest free spot
    /// (<see cref="PersonalSpace.FindSpot"/>) unless <paramref name="exact"/> (restoring saved positions).
    /// </summary>
    public Func<GameObject, Area, Vector3, float, bool, bool, bool>? DeferAreaEntry;

    public void AddToArea(GameObject o, Area area, Vector3 position, float facing, bool exact = false, bool enterEvent = true)
    {
        Register(o);
        if (DeferAreaEntry?.Invoke(o, area, position, facing, exact, enterEvent) == true) return;
        var old = o.Area;
        if (old != null && old != area) RemoveFromArea(o);
        if (o is Creature placed && !exact) position = PersonalSpace.FindSpot(this, area, placed, position);
        o.Area = area;
        o.Position = position;
        o.Facing = facing;
        if (!area.Objects.Contains(o)) area.Objects.Add(o);
        if (o is Creature c)
        {
            area.CreatureGrid.Update(c);
            NoteAILevel(c, EffectiveAILevel(c));
        }
        if (o is Trigger) area.TriggerCache = null;
        if (o is Door) area.DoorCache = null;
        ObjectAdded?.Invoke(o);
        ObjectChanged?.Invoke(o);
        // Queued like the original's events: it runs after the current script.
        if (old != area && o is Creature && enterEvent)
            After(0, () => { if (o.Area == area) RunEvent(area, "OnEnter", new EventContext().Set("Entering", o.Id)); });
    }

    readonly List<Creature> _spawnQueue = new();

    /// <summary>
    /// A created creature's OnSpawn runs after the creating script (so it sees locals set after CreateObject):
    /// as soon as an event or command is sent to it, or else once this frame's queued events have run.
    /// </summary>
    public void QueueSpawn(Creature c)
    {
        c.SpawnPending = true;
        _spawnQueue.Add(c);
        if (!c.IsPC && Nwnx.Plugins.NwnxTweaks.Of(this).FireEquipEventsForAllCreatures) Nwnx.Plugins.NwnxTweaks.EquippedAtCreation(this, c);
    }

    /// <summary>Runs a creature's pending OnSpawn before anything else happens to it.</summary>
    public void SpawnIfPending(GameObject o)
    {
        if (o is not Creature { SpawnPending: true } c) return;
        c.SpawnPending = false;
        _spawnQueue.Remove(c);
        RunEvent(c, "ScriptSpawn", new EventContext());
    }

    void RunPendingSpawns()
    {
        while (_spawnQueue.Count > 0) SpawnIfPending(_spawnQueue[0]);
    }

    /// <summary>Take a loading player out of simulation and other clients' views until its ready acknowledgement.</summary>
    public void SuspendForAreaLoad(Creature c)
    {
        RemoveFromArea(c);
        Movement.Stop(c);
    }

    void RemoveFromArea(GameObject o, bool exitEvent = true)
    {
        var area = o.Area;
        if (area == null) return;
        foreach (var t in area.Objects.OfType<Trigger>())
            if (t.Inside.Remove(o.Id)) RunEvent(t, "ScriptOnExit", new EventContext().Set("Exiting", o.Id));
        area.Objects.Remove(o);
        if (o is Creature gone) area.CreatureGrid.Remove(gone);
        if (o is Trigger) area.TriggerCache = null;
        if (o is Door) area.DoorCache = null;
        o.Area = null;
        if (o is Creature && exitEvent) After(0, () => RunEvent(area, "OnExit", new EventContext().Set("Exiting", o.Id)));
    }

    /// <summary>Moves an object to a location, possibly in another area (JumpToLocation).</summary>
    public void Jump(GameObject o, Location loc)
    {
        if (Get(loc.Area) is not Area area) return;
        if (o is Creature { IsPC: true } jumper) System<Requests.BarterService>().Interrupt(jumper);
        // Landing from a jump is a standing stop: the idle stance out of combat (Creature.IdleStance).
        if (o is Creature lander) lander.IdleStance = !lander.InCombat;
        var from = o.Area;
        var pos = loc.Position;
        if (o is Creature && area.Walkmesh.HighestGround(pos.X, pos.Y) is { } z && MathF.Abs(z - pos.Z) > 0.5f) pos.Z = z;
        if (o.Area == area)
        {
            if (o is Creature c)
            {
                pos = PersonalSpace.FindSpot(this, area, c, pos);
                c.Position = pos;
                area.CreatureGrid.Update(c);
            }
            o.Position = pos;
            o.FacingDegrees = loc.FacingDegrees;
            ObjectChanged?.Invoke(o);
        }
        else AddToArea(o, area, pos, loc.FacingDegrees * MathF.PI / 180f);
        AreaTransition?.Invoke(o, area);
        if (o is Creature mover && from != null && from != area) CreatureLeftArea?.Invoke(mover, from);
    }

    /// <summary>A creature jumped from an area to another (its player's client may still be loading the new one).</summary>
    public event Action<Creature, Area>? CreatureLeftArea;

    /// <summary>
    /// Where an object is for the rules that follow it from area to area: its area and position, or, for a player
    /// whose client is still loading the area it is moving to (out of every area until the client is ready, see
    /// GameServer.AreaLoading), that area and the spot it will stand on. Pursuers and associates go by this, so a
    /// player taking a transition is followed as soon as it leaves, not only once its client has finished loading.
    /// </summary>
    public (Area? Area, Vector3 Position) Whereabouts(GameObject o) =>
        o.Area == null && o is Creature { Player: { IsAreaLoading: true, PendingEntry: { } entry } } && entry.Creature == o
            ? (entry.Area, entry.Position) : (o.Area, o.Position);

    /// <summary>A creature changed area (the server sends the new area to its player).</summary>
    public event Action<GameObject, Area>? AreaTransition;

    public void Destroy(GameObject o)
    {
        if (o.IsDestroyed || o.Destroying) return;
        o.Destroying = true;
        if (o is AreaOfEffect endingArea) System<Rules.EffectSystem>().BeforeAreaObjectDestroyed(endingArea);
        if (o is Creature c)
        {
            // Its form's items and the equipment it put away for a polymorph go with it.
            if (c.PolymorphForm != null) System<Rules.EffectSystem>().ForgetPolymorph(c);
            foreach (var item in c.Equipment.Values.Concat(c.Inventory).ToList()) Destroy(item);
        }
        // The area's OnExit runs after the object is gone (GetExitingObject is invalid then).
        if (o is Creature && o.Area is { } area)
        {
            uint id = o.Id;
            After(0, () => RunEvent(area, "OnExit", new EventContext().Set("Exiting", id)));
        }
        var removedFrom = o.Area;
        RemoveFromArea(o, exitEvent: false);
        o.IsDestroyed = true;
        if (o is AreaOfEffect aoe) System<Rules.EffectSystem>().AreaObjectDestroyed(aoe, removedFrom);
        if (_byTag.TryGetValue(o.Tag, out var list)) list.Remove(o);
        ObjectRemoved?.Invoke(o);
        _objects.Remove(o.Id);
    }

    public void MarkChanged(GameObject o) => ObjectChanged?.Invoke(o);

    readonly PriorityQueue<(Action Action, bool Persist), (double, long)> _timers = new();
    long _timerSeq;

    /// <summary>
    /// Runs engine code after a delay (DestroyObject delays, signalled events), on the game clock, in one order
    /// with DelayCommand and AssignCommand, like the original's event queue.
    /// </summary>
    /// <param name="persist">Whether this pending engine event survives a saved-game reload.</param>
    public void After(double seconds, Action action, bool persist = true) =>
        _timers.Enqueue((action, persist), (GameTime + Math.Max(0, seconds), NextEventSequence(seconds)));

    /// <summary>
    /// Order among queued events due at the same time. Like the original, events queued with no delay run first in
    /// first out, after any delayed ones due then; delayed events due at the same time run last queued first.
    /// </summary>
    public long NextEventSequence(double delay) => delay > 0 ? -++_timerSeq : ++_timerSeq;

    /// <summary>
    /// Runs the events already due before the next creature's AI update: the original runs its event queue before each
    /// single update, so what one creature's update queued with no delay (a shout's OnConversation, say) happens before
    /// the next creature's update (nwserver ai_associate: a henchman whose perception pass came a frame after its
    /// master's got the master's shout first).
    /// </summary>
    public void RunEventsBeforeUpdate() => RunDueEvents();

    /// <summary>Runs engine timers and script commands that are due, in due-time then queued order.</summary>
    void RunDueEvents()
    {
        for (int guard = 0; guard < 20000; guard++)
        {
            bool timer = _timers.TryPeek(out var action, out var at) && at.Item1 <= GameTime;
            if (Scripts != null && Scripts.TryPeekDue(out var key) && (!timer || key.CompareTo(at) < 0)) Scripts.RunNext();
            else if (timer) { _timers.Dequeue(); action.Action(); }
            else return;
        }
    }

    /// <summary>Advances the simulation by <paramref name="dt"/> seconds.</summary>
    public void Step(float dt)
    {
        Time += dt;
        Clock.Advance(dt);
        // Movement covers the time since the last step, so scripts and actions see where creatures are now.
        // Like the original, the most recently created creature moves first (it is the one that bumps).
        foreach (var area in Areas)
        {
            area.CreatureGrid.Sync(area);
            var objects = area.Objects.ToArray();
            for (int i = objects.Length - 1; i >= 0; i--)
                if (objects[i] is Creature c && !c.IsDestroyed && c.Area == area)
                {
                    var before = c.Position;
                    Movement.Step(this, c, dt);
                    if (c.Position != before)
                    {
                        Triggers(area, c);
                        if (c.Area == area) CreatureMoved?.Invoke(c, before);
                    }
                }
        }
        RunDueEvents();
        RunPendingSpawns();
        long t0 = Profile != null ? global::System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        Heartbeats();
        if (Profile != null) t0 = Measure("heartbeats", t0);
        foreach (var system in Systems)
        {
            system.Step(dt);
            if (Profile != null) t0 = Measure(system.GetType().Name, t0);
        }
        foreach (var area in Areas)
        {
            var objects = area.Objects.ToArray();
            foreach (var o in objects)
            {
                if (o.IsDestroyed) continue;
                ActionQueues.Step(this, o, dt);
            }
        }
        if (Profile != null) Measure("actions+movement", t0);
    }

    /// <summary>When set, accumulates wall time per world step phase and system (diagnostics: run-module --profile).</summary>
    public Dictionary<string, double>? Profile;

    long Measure(string name, long since)
    {
        long now = global::System.Diagnostics.Stopwatch.GetTimestamp();
        Profile![name] = Profile.GetValueOrDefault(name) + (now - since) * 1000.0 / global::System.Diagnostics.Stopwatch.Frequency;
        return now;
    }

    void Heartbeats()
    {
        if (Scripts == null) return;
        Beat(Module, "Mod_OnHeartbeat");
        foreach (var area in Areas)
        {
            Beat(area, "OnHeartbeat");
            foreach (var o in area.Objects.ToArray())
            {
                switch (o)
                {
                    case Creature: Beat(o, "ScriptHeartbeat"); break;
                    case Trigger: Beat(o, "ScriptHeartbeat"); break;
                    case Placeable or Door: Beat(o, "OnHeartbeat"); break;
                    // Measured (encounter_events): an inactive encounter's OnHeartbeat doesn't run.
                    case Encounter { Active: true }: Beat(o, "OnHeartbeat"); break;
                }
            }
        }
    }

    void Beat(GameObject o, string field)
    {
        if (o.IsDestroyed) return;
        // On the game clock: a SetTime jump brings every heartbeat due at once (one beat each).
        if (o is Creature cr)
        {
            // The dead have no heartbeat (nwserver ai_melee: a corpse kept with SetIsDestroyable fires none).
            if (cr.IsDead) { cr.HeartbeatNow = false; return; }
            // A creature under a condition with a state script beats whatever its own scripts are (Rules.StateScripts).
            int state = Rules.StateScripts.Row(cr);
            bool own = o.Script(field).Length > 0;
            if (!own && state < 0 && !cr.HeartbeatNow) return;
            if (cr.HeartbeatNow)
            {
                // A condition just landed: the beat is due now (nwserver state_scripts: OnHeartbeat and the state
                // script run in the frame the effect lands), and the next one a period later.
                cr.HeartbeatNow = false;
                o.HeartbeatFirst = false;
            }
            else
            {
                // A creature's beat is due a period after its last one (or its appearance), the period following its AI
                // level at the time: one attacked at very low AI goes into combat at the normal level and beats at the
                // normal pace from then on (nwserver ai_events: first beat 7.7 s after appearing, not 9).
                if (o.NextHeartbeat < 0) { o.NextHeartbeat = GameTime; o.HeartbeatRoll = Random.Shared.NextDouble(); o.HeartbeatFirst = true; return; }
                int level = EffectiveAILevel(cr);
                double due = o.NextHeartbeat + HeartbeatInterval + (level == 0 ? 1.45 + o.HeartbeatRoll * 1.5 + (o.HeartbeatFirst ? 0.9 : 0) : o.HeartbeatRoll * 1.2);
                if (GameTime < due) return;
                o.HeartbeatFirst = false;
            }
            o.NextHeartbeat = GameTime;
            o.HeartbeatRoll = Random.Shared.NextDouble();
            // First its own OnHeartbeat, then the state script; an associate skips its own while it has a state
            // (nwserver state_scripts: a confused henchman of an NPC ran no OnHeartbeat until the confusion ended).
            if (own && !(state >= 0 && Get<Creature>(cr.Master) != null)) RunEvent(o, field, new EventContext());
            if (state >= 0) Rules.StateScripts.Run(this, cr);
            return;
        }
        if (o.Script(field).Length == 0) return;
        if (o.NextHeartbeat < 0)
        {
            // The first beat comes one period after the object appears.
            o.NextHeartbeat = GameTime + HeartbeatPeriod(o);
            return;
        }
        if (GameTime < o.NextHeartbeat) return;
        o.NextHeartbeat = GameTime + HeartbeatPeriod(o);
        RunEvent(o, field, new EventContext());
    }

    /// <summary>
    /// Six seconds; a creature's beat waits for its AI update, which in the original adds 0-1.2 s,
    /// or about 1.5-3 s at AI_LEVEL_VERY_LOW (an area without players).
    /// </summary>
    double HeartbeatPeriod(GameObject o) => o is not Creature c ? HeartbeatInterval
        : HeartbeatInterval + (EffectiveAILevel(c) == 0 ? 1.45 + Random.Shared.NextDouble() * 1.5 : Random.Shared.NextDouble() * 1.2);

    /// <summary>
    /// AI_LEVEL_*: the level set by script; else a player's creature VERY_HIGH and a player's associate HIGH (wherever
    /// it is), normal in combat, low in an area with players and very low without. Players arriving raise the level at
    /// once; after the last one leaves, a creature stays low until its next perception pass (nwmain playerai:
    /// GetAILevel read 4 for her and 3 for her henchman, also in another area; an NPC in the area she jumped to read 1
    /// at once, and went back to 0 about 2 s after she left).
    /// </summary>
    public int EffectiveAILevel(Creature c) =>
        EffectiveAILevel(c, c.IsPC || c.Area != null && c.Area.Objects.Any(x => x is Creature { IsPC: true }));

    /// <summary><see cref="EffectiveAILevel(Creature)"/> when the caller already knows whether the area has players.</summary>
    public int EffectiveAILevel(Creature c, bool players) =>
        c.AILevel >= 0 ? c.AILevel : c.IsPC ? 4 : Get<Creature>(c.Master) is { IsPC: true } ? 3 : c.InCombat ? 2
        : players || c.Area != null && c.Society.PlayersAtPass == c.Area.Id ? 1 : 0;

    long _aiLevelJoins;

    /// <summary>
    /// Records the creature's AI level; a creature whose level changed joins the end of that level's turn order
    /// (nwserver sched_percept: four observers made in one order and put on AI_LEVEL_NORMAL in another ran their
    /// first perception passes in the SetAILevel order, one frame apart).
    /// </summary>
    public void NoteAILevel(Creature c, int level)
    {
        if (c.AILevelNow == level) return;
        c.AILevelNow = level;
        c.AILevelJoin = ++_aiLevelJoins;
    }

    void Triggers(Area area, Creature c)
    {
        // Cached per area (rebuilt when a trigger is added or removed); an array, so events may change the area safely.
        var triggers = area.TriggerCache ??= area.Objects.OfType<Trigger>().ToArray();
        foreach (var t in triggers)
        {
            if (t.IsDestroyed || t.Area != area) continue;
            bool inside = t.Contains(c.Position);
            if (inside && t.TriggerType == Trigger.TypeTrap && !t.Inside.Contains(c.Id))
            {
                // A trap trigger notices only creatures it goes off for: the trap's script first, then OnEnter,
                // both while it is still armed; a one-shot trap trigger then goes away (trap_trigger, traprules).
                if (!WorldObjects.TrapSystem.Notices(this, t, c)) continue;
                t.Inside.Add(c.Id);
                WorldObjects.TrapSystem.Fire(this, t, c);
                if (!t.IsDestroyed) RunEvent(t, "ScriptOnEnter", new EventContext().Set("Entering", c.Id));
                TriggerEntered?.Invoke(t, c);
                WorldObjects.TrapSystem.Spent(this, t);
            }
            else if (inside && t.Inside.Add(c.Id))
            {
                RunEvent(t, "ScriptOnEnter", new EventContext().Set("Entering", c.Id));
                TriggerEntered?.Invoke(t, c);
            }
            else if (!inside && t.Inside.Remove(c.Id))
                RunEvent(t, "ScriptOnExit", new EventContext().Set("Exiting", c.Id));
        }
    }

    /// <summary>A creature moved this step (after its triggers ran), with where it stood before.</summary>
    public event Action<Creature, Vector3>? CreatureMoved;

    /// <summary>A creature walked into a trigger (area transitions are handled by the server).</summary>
    public event Action<Trigger, Creature>? TriggerEntered;
}
