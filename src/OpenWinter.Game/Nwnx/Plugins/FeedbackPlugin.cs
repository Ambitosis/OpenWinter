using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Feedback: feedback messages, combat log lines and the journal-updated line hidden globally or per player.
/// A personal state (set, or -1) overrides the global one. In whitelist mode a kind of message is hidden unless its
/// global state was set to shown. The getters report the stored states as nwserver's do (parity suite
/// nwnx_feedback); hiding itself is applied where OpenWinter sends the lines (<see cref="Hidden"/>).
/// </summary>
public static class FeedbackPlugin
{
    const string P = "Feedback";
    /// <summary>Message kinds: feedback messages, combat log lines, the journal-updated line.</summary>
    public const int Feedback = 0, CombatLog = 1, Journal = 2;

    sealed class FeedbackState
    {
        public readonly Dictionary<(int Type, int Message), bool> Global = new();
        public readonly Dictionary<(uint Pc, int Type, int Message), bool> Personal = new();
        public readonly bool[] Whitelist = new bool[3];
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, FeedbackState> _state = new();
    static FeedbackState State(World w) => _state.GetValue(w.System<NwnxRegistry>(), _ => new FeedbackState());

    /// <summary>Whether a message of <paramref name="type"/> (<see cref="Feedback"/>, <see cref="CombatLog"/> or
    /// <see cref="Journal"/>) and id is hidden from <paramref name="pc"/>.</summary>
    public static bool Hidden(World world, Creature pc, int type, int message)
    {
        var st = State(world);
        if (st.Global.Count == 0 && st.Personal.Count == 0 && !st.Whitelist[type]) return false;
        var player = Players.FamiliarPossession.SessionOf(pc)?.PlayerCreature ?? pc;
        if (st.Personal.TryGetValue((player.Id, type, message), out var own)) return own;
        if (st.Global.TryGetValue((type, message), out var global)) return st.Whitelist[type] ? !global : global;
        return st.Whitelist[type];
    }

    /// <summary>
    /// A feedback or combat log line about to go to a player: hidden by its combat log kind (NWNX_FEEDBACK_COMBATLOG_*;
    /// 11 covers every feedback message and SendMessageToPC) or, for a feedback message, by its own id.
    /// </summary>
    public static bool Hides(World world, Creature pc, int combatLog, int message) =>
        Hidden(world, pc, CombatLog, combatLog) || message >= 0 && Hidden(world, pc, Feedback, message);

    [NwnxFunc(P, "SetMessageHidden")]
    static void SetHidden(NwnxCall n)
    {
        uint pc = n.PopObjectId(); int type = n.PopInt(); int message = n.PopInt(); bool hidden = n.PopInt() != 0;
        if (type is < Feedback or > Journal) return;
        var st = State(n.World);
        if (pc == GameObject.InvalidId) st.Global[(type, message)] = hidden;
        else if (n.World.Get(pc) != null) st.Personal[(pc, type, message)] = hidden;
    }

    [NwnxFunc(P, "GetMessageHidden")]
    static void GetHidden(NwnxCall n)
    {
        uint pc = n.PopObjectId(); int type = n.PopInt(); int message = n.PopInt();
        var st = State(n.World);
        if (pc == GameObject.InvalidId) n.Push(st.Global.TryGetValue((type, message), out var g) && g);
        else n.Push(st.Personal.TryGetValue((pc, type, message), out var own) ? own ? 1 : 0 : -1);
    }

    [NwnxFunc(P, "SetFeedbackMode")]
    static void SetMode(NwnxCall n)
    {
        int type = n.PopInt(); bool whitelist = n.PopInt() != 0;
        if (type is Feedback or CombatLog) State(n.World).Whitelist[type] = whitelist;
    }
}
