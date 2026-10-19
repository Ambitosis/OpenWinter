using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Players;

/// <summary>A quest category from module.jrl.</summary>
public sealed class JournalCategory
{
    public required string Tag;
    public LocString Name = new();
    public int XP, Priority, Picture;
    public string Comment = "";
    /// <summary>Entries by ID (the quest state).</summary>
    public readonly SortedDictionary<int, JournalEntry> Entries = new();
}

public sealed class JournalEntry
{
    public int Id;
    public bool End;
    public LocString Text = new();
}

/// <summary>
/// The module journal (module.jrl) and per-player quest state (docs/JOURNAL.md). The engine keeps a quest's state in
/// the local int "NW_JOURNAL_ENTRY" + tag of the creature, which the stock include scripts read; quest experience is
/// scripted (GetJournalQuestExperience + GiveXPToCreature), never given by the engine.
/// </summary>
public sealed class JournalService : IWorldSystem
{
    public const string LocalPrefix = "NW_JOURNAL_ENTRY";

    readonly World _world;
    Dictionary<string, JournalCategory>? _categories;

    /// <summary>Party membership for bAllPartyMembers (tests can replace it); defaults to the player's party.</summary>
    public static Func<World, Creature, IEnumerable<Creature>>? PartyMembers;

    public JournalService(World world) { _world = world; }

    public void Step(float dt) { }

    public IReadOnlyDictionary<string, JournalCategory> Categories => _categories ??= Load(_world.Resources);

    public static Dictionary<string, JournalCategory> Load(ResMan rm)
    {
        // Quest tags are case-sensitive (journal_funcs: GetJournalQuestExperience("Q_A") of "q_a" reads 0).
        var result = new Dictionary<string, JournalCategory>(StringComparer.Ordinal);
        var jrl = rm.ReadGff("module", ResType.Jrl);
        if (jrl == null) return result;
        foreach (var c in jrl.GetList("Categories"))
        {
            var cat = new JournalCategory
            {
                Tag = c.GetString("Tag"),
                Name = c.GetLocString("Name"),
                XP = (int)c.GetInt("XP"),
                Priority = (int)c.GetInt("Priority"),
                Picture = (int)c.GetInt("Picture", 65535),
                Comment = c.GetString("Comment"),
            };
            foreach (var e in c.GetList("EntryList"))
            {
                int id = (int)e.GetInt("ID");
                cat.Entries[id] = new JournalEntry { Id = id, End = e.GetInt("End") != 0, Text = e.GetLocString("Text") };
            }
            result.TryAdd(cat.Tag, cat);
        }
        return result;
    }

    public int Experience(string tag) => Categories.TryGetValue(tag, out var c) ? c.XP : 0;

    IEnumerable<Creature> Party(Creature pc)
    {
        if (PartyMembers != null) return PartyMembers(_world, pc);
        var parties = _world.System<Society.PartyService>();
        return parties.Members(parties.Ensure(pc));
    }

    /// <summary>
    /// Who an update reaches (journal_funcs, journalpc): with bAllPlayers every player and nobody else; otherwise the
    /// creature itself (a non-player creature too: it gets the local), and with bAllPartyMembers a player's party's
    /// players. A henchman's update reaches only the henchman (its master's journal and local stayed as they were).
    /// </summary>
    IEnumerable<Creature> Targets(Creature? who, bool party, bool all)
    {
        if (all) return _world.ConnectedPCs.ToList();
        if (who == null) return Array.Empty<Creature>();
        var list = new List<Creature> { who };
        if (party && who.IsPC) list.AddRange(Party(who).Where(p => p.IsPC));
        return list.Distinct().ToList();
    }

    /// <summary>
    /// AddJournalQuestEntry. Measured on nwserver (journal_funcs): the engine keeps the state in the local int
    /// "NW_JOURNAL_ENTRY" + szPlotID (the tag as given) of every creature it reaches, players or not, whether or not
    /// module.jrl has the quest or entry; without bAllowOverrideHigher a state not above that local is ignored (a
    /// script-set local counts). A player's journal shows the entry when module.jrl has it.
    /// </summary>
    public void Add(string tag, int state, Creature? who, bool allPartyMembers, bool allPlayers, bool allowOverrideHigher)
    {
        Categories.TryGetValue(tag, out var cat);
        JournalEntry? entry = null;
        cat?.Entries.TryGetValue(state, out entry);
        foreach (var target in Targets(who, allPartyMembers, allPlayers))
        {
            string key = LocalPrefix + tag;
            if (!allowOverrideHigher && target.Locals.Ints.GetValueOrDefault(key) >= state) continue;
            target.Locals.Ints[key] = state;
            // A player's journal changes when module.jrl has the quest; an entry it lacks still updates it (journalpc:
            // state 7 of a three-entry quest printed "Your journal has been updated.").
            if (!target.IsPC || cat == null) continue;
            target.Journal.TryGetValue(cat.Tag, out var current);
            var next = new JournalState { Tag = cat.Tag, State = state, Day = DayStamp(), Time = TimeStamp() };
            if (current != null && current.State != state)
            {
                next.Earlier.AddRange(current.Earlier.Where(e => e.State != state));
                next.Earlier.Add((current.State, current.Day, current.Time));
            }
            else if (current != null) next.Earlier.AddRange(current.Earlier);
            target.Journal[cat.Tag] = next;
            _world.Output?.Notify("journal", target, Describe(target, cat, entry, next, update: true), target);
        }
    }

    /// <summary>RemoveJournalQuestEntry: clears the local (exact tag) and the player's journal entry.</summary>
    public void Remove(string tag, Creature? who, bool allPartyMembers, bool allPlayers)
    {
        foreach (var target in Targets(who, allPartyMembers, allPlayers))
        {
            target.Locals.Ints.Remove(LocalPrefix + tag);
            if (!target.IsPC || !target.Journal.Remove(tag)) continue;
            // The original prints the journal line for a removal too, with the quest's name.
            string name = Categories.TryGetValue(tag, out var cat) ? Name(target, cat) : tag;
            _world.Output?.Notify("journal", target, new { tag, name, removed = true, update = true }, target);
        }
    }

    /// <summary>Sends a player every quest in their journal (the client's journal window asks on open).</summary>
    public void SendAll(Creature pc)
    {
        _world.Output?.Notify("journal-reset", pc, new { }, pc);
        foreach (var state in pc.Journal.Values)
            if (Categories.TryGetValue(state.Tag, out var cat))
                _world.Output?.Notify("journal", pc, Describe(pc, cat, cat.Entries.GetValueOrDefault(state.State), state, update: false), pc);
    }

    string Name(Creature pc, JournalCategory cat) =>
        _world.System<Conversations.ConversationService>().Tokens(cat.Name.Resolve(_world.Resources.Talk), pc, pc, markup: false);

    /// <param name="update">A script or conversation changed it (the client prints the journal line and flashes the
    /// journal button), rather than a resend of the whole journal.</param>
    object Describe(Creature pc, JournalCategory cat, JournalEntry? entry, JournalState state, bool update)
    {
        var tlk = _world.Resources.Talk;
        // Journal text takes the same tokens as conversations (<FirstName>, <CUSTOM100>, ...).
        var conversations = _world.System<Conversations.ConversationService>();
        string T(LocString text) => conversations.Tokens(text.Resolve(tlk), pc, pc, markup: false);
        return new
        {
            tag = cat.Tag,
            state = state.State,
            name = T(cat.Name),
            text = entry != null ? T(entry.Text) : "",
            end = entry?.End == true,
            update,
            priority = cat.Priority,
            picture = cat.Picture,
            xp = cat.XP,
            day = state.Day,
            time = state.Time,
            // Earlier entries, newest first, for the journal's history.
            history = state.Earlier.AsEnumerable().Reverse()
                .Where(e => cat.Entries.ContainsKey(e.State))
                .Select(e => new { state = e.State, text = T(cat.Entries[e.State].Text), day = e.Day, time = e.Time })
                .ToArray(),
            removed = false,
        };
    }

    int DayStamp()
    {
        var k = _world.Clock;
        return (k.Year * 12 + (k.Month - 1)) * 28 + (k.Day - 1);
    }

    int TimeStamp()
    {
        var k = _world.Clock;
        return (int)k.TimeOfDayMs;
    }
}
