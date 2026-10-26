using System.Text;
using System.Text.RegularExpressions;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Conversations;

/// <summary>One player's active conversation.</summary>
public sealed class ConversationSession
{
    public required Creature Pc;
    public required GameObject Owner;
    public required Dialog Dialog;
    public required string ResRef;
    public int Entry = -1;
    /// <summary>Reply indexes offered for the current entry, in display order.</summary>
    public readonly List<int> Offered = new();
    public bool Paused;
    public bool Private;
    /// <summary>A reply's action script is running (OpenStore there leaves the conversation open).</summary>
    public bool InReply;
    /// <summary>A line's action script is running; <see cref="CloseWhenShown"/> asks for the window to close once it is shown.</summary>
    public bool InEntryAction, CloseWhenShown;
    /// <summary>A one-liner's action script is running: GetPCSpeaker has no one (store_oneliner).</summary>
    public bool NoSpeaker;
    /// <summary>Participants playing a looping animation a line asked for (see <see cref="ConversationService"/>).</summary>
    public readonly HashSet<uint> Looping = new();
    /// <summary>Node texts a script replaced in this conversation (NWNX_Dialog_SetCurrentNodeText), by (node type, id).</summary>
    public readonly Dictionary<(int Type, int Id), string> TextOverrides = new();
    /// <summary>The node whose scripts are running, for NWNX_Dialog: its type (0 start, 1 entry, 2 reply), id and
    /// position in its parent's list of links.</summary>
    public (int Type, int Id, int Index) Node = (-1, -1, 0);
}

/// <summary>What a player sees for one conversation step.</summary>
public sealed record DialogView(uint Owner, uint Speaker, string SpeakerName, string Text, IReadOnlyList<(int Index, string Text)> Replies, bool Ended, string Sound);

/// <summary>
/// Runs DLG conversations: conditions, actions, tokens, replies and end scripts.
/// As in the original (measured with parity/visual/scenes/talk and talkanim):
/// <list type="bullet">
/// <item>When a conversation begins, the player and a creature owning it turn to face each other at once, whoever
/// started it, even a creature sitting or not commandable; a placeable or door owner doesn't turn, the player still
/// faces it. Scripts go on reading the old facing (GetFacing) until the conversation ends, when the turn is
/// committed: <see cref="ShownFacing"/> is what players see meanwhile. The stationary owner keeps following the
/// player's position; a participant moving or being turned by a script drops their own override.</item>
/// <item>A line's Animation field plays on its speaker (an NPC line) or the player (a reply), mapped by
/// <see cref="Animations.ConversationLine"/>; the default (0) plays nothing. A looping one (the talk animations,
/// listen, meditate...) holds while later lines are shown, until a later line of that speaker with no looping
/// animation, or the end of the conversation; AnimLoop made no difference.</item>
/// </list>
/// How conversations stop (measured with parity/visual/scenes/convoend, convoend2-4 and loginconvo):
/// <list type="bullet">
/// <item>A line is the <em>last line</em> when it offers no answers, or exactly one answer that leads nowhere. A stop
/// at a last line counts as finishing it (the end script); anywhere else it is an abort.</item>
/// <item>The player and talker 10 m apart (either moving), the talker leaving the area, or a new conversation
/// started over this one stop it. A talker destroyed mid-conversation runs neither script. ClearAllActions and
/// damage don't stop it.</item>
/// <item>The player closing the window (Esc): at a line with one answer that leads nowhere, that answer is chosen
/// (posted to chat, its action runs); at a line with no answers it finishes; elsewhere it aborts.</item>
/// <item>OpenStore while a line is shown closes the window as Esc does. From a line's own action script it closes
/// once the line is reached (posted to chat, no window), by the last-line rule with no answer chosen; the original
/// varies between runs when that is the first line. From a reply's action script the conversation goes on beside
/// the store.</item>
/// <item>A conversation whose first line has no answers is a one-liner: spoken over the talker, no window, its
/// action script runs with no PC speaker, then the end script.</item>
/// <item>Every line shown and every answer chosen is posted to the player's chat window.</item>
/// <item>Either side starting an attack or casting a spell stops it (end at a last line, abort elsewhere). Being
/// attacked, walking, animations, waits and ClearAllActions don't (convobusy2, convobusy3).</item>
/// </list>
/// A request to talk (a click, ActionStartConversation) is refused while the creature asked is in combat or dead; see
/// <see cref="Refuses"/>. BeginConversation is never refused (convobusy, convobusy3).
/// </summary>
public sealed class ConversationService : IWorldSystem
{
    readonly World _world;
    readonly Dictionary<uint, ConversationSession> _byPc = new();
    /// <summary>Conversations whose end or abort script is running: IsInConversation still reports both sides (convobusy, convoend).</summary>
    readonly List<ConversationSession> _ending = new();
    /// <summary>Participants turned toward each other: object id → (the facing shown, the facing and position at the turn).</summary>
    readonly Dictionary<uint, (float Shown, float Facing, System.Numerics.Vector3 Position)> _turned = new();
    /// <summary>How long a looping conversation animation is asked to hold: until it is stopped.</summary>
    const float Hold = 1e6f;
    readonly Dictionary<string, Dialog?> _dialogs = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>SetCustomToken values (CUSTOMnnn).</summary>
    public readonly Dictionary<int, string> CustomTokens = new();
    /// <summary>Tokens set for one player (NWNX_Player_SetCustomToken), until SetCustomToken sets that token for everyone.</summary>
    public readonly Dictionary<(uint Pc, int Token), string> PlayerTokens = new();

    /// <summary>SetCustomToken: the value every player sees from now on, replacing any player's own.</summary>
    public void SetCustomToken(int token, string value)
    {
        CustomTokens[token] = value;
        if (PlayerTokens.Count > 0)
            foreach (var key in PlayerTokens.Keys.Where(k => k.Token == token).ToList()) PlayerTokens.Remove(key);
    }

    /// <summary>The text of a node as this conversation shows it.</summary>
    string NodeText(ConversationSession s, int type, int id, DialogNode n) =>
        s.TextOverrides.Count > 0 && s.TextOverrides.TryGetValue((type == 0 ? 1 : type, id), out var text) ? text : n.Text.Resolve(_world.Resources.Talk);

    /// <summary>NWNX_Dialog_SetCurrentNodeText from a script of <paramref name="s"/>: replaces the current node's text.</summary>
    public static void SetNodeText(ConversationSession s, string text)
    {
        if (s.Node.Id >= 0) s.TextOverrides[(s.Node.Type == 0 ? 1 : s.Node.Type, s.Node.Id)] = text;
    }

    /// <summary>The current node's text in the conversation of <paramref name="s"/> (for NWNX_Dialog_GetCurrentNodeText).</summary>
    public string CurrentNodeText(ConversationSession s, int language, int gender)
    {
        if (s.Node.Id < 0) return "";
        var list = s.Node.Type == 2 ? s.Dialog.Replies : s.Dialog.Entries;
        if (s.Node.Id >= list.Count) return "";
        if (s.TextOverrides.TryGetValue((s.Node.Type == 0 ? 1 : s.Node.Type, s.Node.Id), out var text)) return text;
        var loc = list[s.Node.Id].Text;
        return loc.Strings.TryGetValue(language * 2 + gender, out var own) && own.Length > 0 ? own : loc.Resolve(_world.Resources.Talk, language);
    }

    /// <summary>NWNX_Dialog_End: aborts the conversation <paramref name="o"/> takes part in (its abort script runs).</summary>
    public void Abort(GameObject o)
    {
        if (SessionWith(o) is { } s) End(s.Pc, aborted: true);
    }

    /// <summary>Raised when a player's dialogue window should change (the server sends it).</summary>
    public event Action<Creature, DialogView>? Show;
    /// <summary>A line or chosen answer for the player's chat window: (player, who said it, the text).</summary>
    public event Action<Creature, GameObject, string>? Line;

    /// <summary>How far apart the player and talker may get before the conversation stops (walk_away: 10.00 m).</summary>
    public const float MaxDistance = 10f;

    /// <summary>"The object is busy and can not talk to you right now." and "You are too excited to talk right now."</summary>
    public const uint BusyMessage = 6625, TooExcitedMessage = 63229;

    public ConversationService(World world) { _world = world; }

    public void Step(float dt)
    {
        // Stop conversations when the player and talker get too far apart or the talker disappears.
        foreach (var s in _byPc.Values.ToList())
        {
            if (s.Owner.IsDestroyed) End(s.Pc, aborted: true, runScript: false);
            else if (s.Pc.IsDestroyed || s.Owner.Area != s.Pc.Area ||
                (s.Owner != s.Pc && System.Numerics.Vector3.Distance(s.Owner.Position, s.Pc.Position) >= MaxDistance))
                Stop(s.Pc);
        }
        // A participant that moved or was turned by something else since it turned to its partner shows that instead.
        // Being shoved aside while idle (PersonalSpace) isn't moving: the original keeps it facing its partner.
        if (_turned.Count > 0)
            foreach (var (id, t) in _turned.ToList())
                if (_world.Get(id) is not { IsDestroyed: false } o) _turned.Remove(id);
                else if (o.Facing != t.Facing || System.Numerics.Vector3.DistanceSquared(o.Position, t.Position) > 0.01f
                    && !(o is Creature c && PersonalSpace.IsIdle(c)))
                {
                    _turned.Remove(id);
                    _world.MarkChanged(o);
                }
        // A stationary NPC keeps facing its moving partner. The player's movement facing is left alone.
        // With several partners (a merchant) the first keeps it, so the facing doesn't flip every tick.
        HashSet<uint>? followed = null;
        foreach (var s in _byPc.Values)
            if (s.Owner is Creature { IsDead: false } && s.Owner != s.Pc && _turned.ContainsKey(s.Owner.Id)
                && (followed ??= new()).Add(s.Owner.Id))
                Turn(s.Owner, s.Pc);
    }

    /// <summary>The facing (radians) players should see for an object turned toward its conversation partner, if any.</summary>
    public float? ShownFacing(GameObject o) => _turned.Count > 0 && _turned.TryGetValue(o.Id, out var t) ? t.Shown : null;

    /// <summary>Turns <paramref name="o"/> toward <paramref name="toward"/> for players; committed when the conversation ends.</summary>
    void Turn(GameObject o, GameObject toward)
    {
        var d = toward.Position - o.Position;
        if (d.X * d.X + d.Y * d.Y < 1e-6f) return;
        float facing = MathF.Atan2(d.Y, d.X) - o.BearingOffset;
        if (_turned.TryGetValue(o.Id, out var previous) && Math.Abs(previous.Shown - facing) < 0.001f) return;
        _turned[o.Id] = (facing, o.Facing, o.Position);
        _world.MarkChanged(o);
    }

    void Commit(GameObject o)
    {
        if (!_turned.Remove(o.Id, out var t)) return;
        o.Facing = t.Shown;
        _world.MarkChanged(o);
    }

    /// <summary>A line's Animation field on whoever says it (see the class notes).</summary>
    void LineAnimation(ConversationSession s, GameObject who, int value)
    {
        var (animation, looping) = Animations.ConversationLine(value);
        if (animation < 0)
        {
            if (s.Looping.Remove(who.Id)) _world.Output?.PlayAnimation(who, 0, 1, 0);
            return;
        }
        _world.Output?.PlayAnimation(who, animation, 1, looping ? Hold : 0);
        if (looping) s.Looping.Add(who.Id);
    }

    /// <summary>
    /// Whether a request to talk from <paramref name="asker"/> to <paramref name="target"/> (a click, or
    /// ActionStartConversation once the asker has reached it) is refused, telling the player why. As in the original
    /// (parity suite convo_busy between creatures, visual scenes convobusy to convobusy4 with a player):
    /// <list type="bullet">
    /// <item>A creature in combat (GetIsInCombat: fighting, or attacked lately) can't be asked. A player asking hears
    /// "The object is busy and can not talk to you right now." (her own private conversation too); a player asked by a
    /// creature hears "You are too excited to talk right now."; between creatures nothing is said. The creature's
    /// OnConversation doesn't run and no dialog opens, whether one was named or not.</item>
    /// <item>A dead creature can't be asked either, and nothing is said.</item>
    /// <item>The asker's own combat doesn't matter, nor the creature casting, walking, already talking, being
    /// uncommandable (the stock OnConversation script decides that) or hostile. Placeables and doors are never busy.</item>
    /// </list>
    /// </summary>
    public bool Refuses(GameObject asker, GameObject target)
    {
        if (target is not Creature t) return false;
        if (Society.SocietyService.IsDead(t)) return true;
        if (!t.InCombat) return false;
        if (asker is Creature { IsPC: true } pc) _world.Output?.SendMessage(pc, _world.Resources.TlkString(BusyMessage));
        else if (t.IsPC) _world.Output?.SendMessage(t, _world.Resources.TlkString(TooExcitedMessage));
        return true;
    }

    /// <summary>
    /// <paramref name="c"/> starts an attack or casts a spell: the conversations it is part of stop, by the last-line
    /// rule (convobusy2 open_attack_inreach, open_cast and last_attack; convobusy3 open_pc_attacks and open_pc_casts).
    /// </summary>
    public void Fights(Creature c)
    {
        if (_byPc.Count == 0) return;
        foreach (var s in _byPc.Values.Where(s => s.Pc == c || s.Owner == c).ToList()) Stop(s.Pc);
    }

    public bool IsInConversation(GameObject o) =>
        _byPc.ContainsKey(o.Id) || _byPc.Values.Any(s => s.Owner == o) || _ending.Count > 0 && _ending.Any(s => s.Pc == o || s.Owner == o);

    public ConversationSession? SessionOf(Creature pc) => _byPc.GetValueOrDefault(pc.Id);

    /// <summary>The conversation an object is part of: the one it owns (an NPC talking), else the one it is the player of.</summary>
    public ConversationSession? SessionWith(GameObject o) =>
        _byPc.Values.FirstOrDefault(s => s.Owner == o) ?? _byPc.GetValueOrDefault(o.Id);

    Dialog? Load(string resref)
    {
        if (_dialogs.TryGetValue(resref, out var d)) return d;
        var g = _world.Resources.ReadGff(resref, ResType.Dlg);
        d = g == null ? null : Dialog.Load(g);
        _dialogs[resref] = d;
        return d;
    }

    /// <summary>EE object OnDialog can start or decline a requested conversation.</summary>
    public bool RunObjectDialog(GameObject owner, Creature pc, string resref = "")
    {
        if (owner is not (Door or Placeable) || owner.Script("OnDialog").Length == 0) return false;
        // The native callback has an initiating player for BeginConversation's default
        // target, but GetLastSpeaker/GetPCSpeaker do not expose it before a dialog starts.
        _world.RunEvent(owner, "OnDialog", new EventContext().Set("DialogInitiator", pc.Id).Set("DialogResRef", resref));
        return true;
    }

    /// <summary>Starts a conversation between an owner (NPC, placeable, the PC itself) and a player character.</summary>
    public bool Start(GameObject owner, Creature pc, string resref, bool isPrivate = false)
    {
        if (string.IsNullOrEmpty(resref)) resref = DefaultConversation(owner);
        if (string.IsNullOrEmpty(resref) || !pc.IsPC) return false;
        var dialog = Load(resref);
        if (dialog == null) return false;
        if (_byPc.ContainsKey(pc.Id)) Stop(pc);
        var s = new ConversationSession { Pc = pc, Owner = owner, Dialog = dialog, ResRef = resref, Private = isPrivate };
        _byPc[pc.Id] = s;
        int start = FirstPassing(s, dialog.Starts, 0);
        if (start < 0) { _byPc.Remove(pc.Id); return false; }
        // Starting a conversation ends the player's barter (not measured).
        _world.System<Requests.BarterService>().Interrupt(pc);
        if (dialog.Entries[start].Links.Select((l, i) => (l, i)).All(x => x.l.Index < 0 || x.l.Index >= dialog.Replies.Count || !Passes(s, x.l, 2, x.i)))
        {
            SpeakFirstLine(s, start);
            return true;
        }
        if (owner != pc && owner.Area == pc.Area)
        {
            Turn(pc, owner);
            if (owner is Creature { IsDead: false }) Turn(owner, pc);
        }
        // Players see who is talking (Messages ObjectFlags.Talking).
        _world.MarkChanged(pc);
        _world.MarkChanged(owner);
        ShowEntry(s, start);
        return true;
    }

    /// <summary>A conversation whose first line has no answers: spoken over the talker, then finished (store_oneliner).</summary>
    void SpeakFirstLine(ConversationSession s, int start)
    {
        var entry = s.Dialog.Entries[start];
        // The action script sees no PC speaker; a store it opens doesn't change how it ends.
        s.NoSpeaker = s.InEntryAction = true;
        s.Node = (0, start, LinkPosition(s.Dialog.Starts, start));
        if (entry.Script.Length > 0) _world.Scripts?.Run(entry.Script, s.Owner.Id, NodeContext(new EventContext(), s, 2));
        s.NoSpeaker = s.InEntryAction = false;
        // Measured (journalpc): a first line without answers, spoken over the talker, doesn't update the journal.
        if (_byPc.GetValueOrDefault(s.Pc.Id) != s) return;
        _byPc.Remove(s.Pc.Id);
        _world.Speak(s.Owner, Tokens(NodeText(s, 1, start, entry), s.Pc, s.Owner, markup: false), 0);
        if (s.Dialog.EndScript.Length > 0) _world.Scripts?.Run(s.Dialog.EndScript, s.Owner.Id, Context(s));
    }

    /// <summary>A one-liner (SpeakOneLinerConversation): the first valid entry is spoken as a floating line, no window.</summary>
    public void OneLiner(GameObject owner, string resref, Creature? tokenTarget)
    {
        if (string.IsNullOrEmpty(resref)) resref = DefaultConversation(owner);
        var dialog = Load(resref);
        if (dialog == null) return;
        var fake = new ConversationSession { Pc = tokenTarget ?? new Creature(), Owner = owner, Dialog = dialog, ResRef = resref };
        int start = FirstPassing(fake, dialog.Starts, 0);
        if (start < 0) return;
        var entry = dialog.Entries[start];
        fake.Node = (0, start, LinkPosition(dialog.Starts, start));
        RunAction(fake, entry);
        _world.Speak(owner, Tokens(NodeText(fake, 1, start, entry), fake.Pc, owner, markup: false), 0);
    }

    static string DefaultConversation(GameObject owner) => owner switch
    {
        Creature c => c.Conversation,
        Placeable p => p.Template.GetString("Conversation"),
        Door d => d.Template.GetString("Conversation"),
        _ => owner.Script("Conversation"),
    };

    /// <summary>The player picked reply <paramref name="choice"/> (an index into the offered list).</summary>
    public void Choose(Creature pc, int choice)
    {
        if (!_byPc.TryGetValue(pc.Id, out var s) || s.Paused) return;
        if (s.Offered.Count == 0 || choice < 0 || choice >= s.Offered.Count) { End(pc, aborted: false); return; }
        int replyId = s.Offered[choice];
        var reply = s.Dialog.Replies[replyId];
        LineAnimation(s, pc, reply.Animation);
        var said = Tokens(NodeText(s, 2, replyId, reply), s.Pc, s.Owner, markup: false);
        if (said.Length > 0) Line?.Invoke(pc, pc, said);
        s.InReply = true;
        s.Node = (2, replyId, s.Entry >= 0 ? LinkPosition(s.Dialog.Entries[s.Entry].Links, replyId) : 0);
        RunAction(s, reply);
        s.InReply = false;
        UpdateJournal(s, reply);
        if (!_byPc.ContainsKey(pc.Id)) return; // the action script ended the conversation
        int next = FirstPassing(s, reply.Links, 1);
        if (next < 0) End(pc, aborted: false);
        else ShowEntry(s, next);
    }

    /// <summary>The line is a last line: no answers, or one answer that leads nowhere (see the class notes).</summary>
    static bool AtLastLine(ConversationSession s) =>
        s.Entry >= 0 && (s.Offered.Count == 0 || s.Offered.Count == 1 && s.Dialog.Replies[s.Offered[0]].Links.Count == 0);

    /// <summary>Stopped from outside (distance, area, a new conversation): finished at a last line, else aborted.</summary>
    public void Stop(Creature pc)
    {
        if (_byPc.TryGetValue(pc.Id, out var s)) End(pc, aborted: !AtLastLine(s));
    }

    /// <summary>
    /// The player closed the window (Esc, the end choice, a store opening over it): a single answer that leads
    /// nowhere is chosen, a line with no answers finishes, anything else aborts.
    /// </summary>
    public void Dismiss(Creature pc)
    {
        if (!_byPc.TryGetValue(pc.Id, out var s)) return;
        if (s.Entry >= 0 && s.Offered.Count == 1 && s.Dialog.Replies[s.Offered[0]].Links.Count == 0 && !s.Paused) Choose(pc, 0);
        else End(pc, aborted: !(s.Entry >= 0 && s.Offered.Count == 0));
    }

    /// <summary>OpenStore for a player who may be talking: closes the conversation unless a reply opened the store.</summary>
    public void StoreOpened(Creature pc)
    {
        if (!_byPc.TryGetValue(pc.Id, out var s) || s.InReply) return;
        if (s.InEntryAction) s.CloseWhenShown = true;
        else Dismiss(pc);
    }

    public void End(Creature pc, bool aborted, bool runScript = true)
    {
        if (!_byPc.Remove(pc.Id, out var s)) return;
        foreach (var id in s.Looping)
            if (_world.Get(id) is { IsDestroyed: false } o) _world.Output?.PlayAnimation(o, 0, 1, 0);
        s.Looping.Clear();
        Commit(pc);
        if (!IsInConversation(s.Owner)) Commit(s.Owner);
        _world.MarkChanged(pc);
        _world.MarkChanged(s.Owner);
        var script = aborted ? s.Dialog.AbortScript : s.Dialog.EndScript;
        if (runScript && script.Length > 0)
        {
            // The original still counts both sides as talking while the script runs (IsInConversation 1 in every end
            // and abort script of convoend and convobusy).
            _ending.Add(s);
            try { _world.Scripts?.Run(script, s.Owner.Id, Context(s)); }
            finally { _ending.Remove(s); }
        }
        Show?.Invoke(pc, new DialogView(s.Owner.Id, s.Owner.Id, "", "", Array.Empty<(int, string)>(), true, ""));
    }

    public void Pause(Creature pc, bool paused)
    {
        if (_byPc.TryGetValue(pc.Id, out var s)) s.Paused = paused;
    }

    EventContext Context(ConversationSession s, Dictionary<string, string>? parameters = null)
    {
        var ctx = new EventContext().Set("PCSpeaker", s.Pc.Id).Set("Speaker", s.Pc.Id);
        if (parameters != null) foreach (var kv in parameters) ctx.Set("param:" + kv.Key, kv.Value);
        return ctx;
    }

    /// <summary>Event context keys NWNX_Dialog reads in a conversation's condition and action scripts.</summary>
    public const string NodeSessionKey = "nwnx:dialog:session", NodeTypeKey = "nwnx:dialog:nodeType", ScriptTypeKey = "nwnx:dialog:scriptType",
        NodeIdKey = "nwnx:dialog:nodeId", NodeIndexKey = "nwnx:dialog:nodeIndex";

    /// <param name="scriptType">NWNX_DIALOG_SCRIPT_TYPE_*: 1 a StartingConditional, 2 an action.</param>
    static EventContext NodeContext(EventContext ctx, ConversationSession s, int scriptType) =>
        ctx.Set(NodeSessionKey, s).Set(NodeTypeKey, s.Node.Type).Set(ScriptTypeKey, scriptType).Set(NodeIdKey, s.Node.Id).Set(NodeIndexKey, s.Node.Index);

    static int LinkPosition(List<DialogLink> links, int index) => Math.Max(0, links.FindIndex(l => l.Index == index));

    /// <param name="type">The kind of node the links lead to: 0 a starting entry, 1 an entry, 2 a reply.</param>
    int FirstPassing(ConversationSession s, List<DialogLink> links, int type)
    {
        for (int i = 0; i < links.Count; i++)
            if (Passes(s, links[i], type, i)) return links[i].Index;
        return -1;
    }

    bool Passes(ConversationSession s, DialogLink l, int type, int position)
    {
        if (l.Active.Length == 0 || _world.Scripts == null) return true;
        var previous = s.Node;
        s.Node = (type, l.Index, position);
        try { return _world.Scripts.Run(l.Active, s.Owner.Id, NodeContext(Context(s, l.ConditionParams), s, 1)) != 0; }
        finally { s.Node = previous; }
    }

    void RunAction(ConversationSession s, DialogNode n)
    {
        if (n.Script.Length > 0) _world.Scripts?.Run(n.Script, s.Owner.Id, NodeContext(Context(s, n.ActionParams), s, 2));
    }

    /// <summary>
    /// A line's Quest / QuestEntry (scene journalpc): shown in the conversation window it updates the player's journal
    /// before the line is printed, as AddJournalQuestEntry does (no experience). The flags (party, override) are the
    /// script function's defaults; not measured.
    /// </summary>
    void UpdateJournal(ConversationSession s, DialogNode n)
    {
        if (n.Quest.Length > 0 && n.QuestEntry >= 0 && s.Pc.IsPC)
            _world.System<Players.JournalService>().Add(n.Quest, n.QuestEntry, s.Pc, true, false, false);
    }

    void ShowEntry(ConversationSession s, int index)
    {
        if (index < 0 || index >= s.Dialog.Entries.Count) { End(s.Pc, aborted: false); return; }
        // The links that led here: the starting list, or the chosen reply's.
        int parentType = s.Entry < 0 ? 0 : 1;
        var parentLinks = s.Entry >= 0 && s.Node is { Type: 2, Id: >= 0 } chosen && chosen.Id < s.Dialog.Replies.Count ? s.Dialog.Replies[chosen.Id].Links : s.Dialog.Starts;
        s.Entry = index;
        var entry = s.Dialog.Entries[index];
        var speaker = s.Owner;
        if (entry.Speaker.Length > 0 && _world.ByTag(entry.Speaker).FirstOrDefault(o => o.Area == s.Owner.Area) is { } tagged) speaker = tagged;
        s.InEntryAction = true;
        s.Node = (parentType, index, LinkPosition(parentLinks, index));
        RunAction(s, entry);
        s.InEntryAction = false;
        UpdateJournal(s, entry);
        if (!_byPc.ContainsKey(s.Pc.Id)) return;
        LineAnimation(s, speaker, entry.Animation);

        s.Offered.Clear();
        var replies = new List<(int, string)>();
        for (int i = 0; i < entry.Links.Count; i++)
        {
            var l = entry.Links[i];
            if (!Passes(s, l, 2, i) || l.Index < 0 || l.Index >= s.Dialog.Replies.Count) continue;
            s.Offered.Add(l.Index);
            var text = NodeText(s, 2, l.Index, s.Dialog.Replies[l.Index]);
            replies.Add((s.Offered.Count - 1, text.Length == 0 ? "[Continue]" : Tokens(text, s.Pc, s.Owner)));
        }
        if (replies.Count == 0) replies.Add((-1, "[End Dialog]"));
        var line = Tokens(NodeText(s, 1, index, entry), s.Pc, s.Owner);
        var said = Tokens(NodeText(s, 1, index, entry), s.Pc, s.Owner, markup: false);
        if (said.Length > 0) Line?.Invoke(s.Pc, speaker, said);
        // A store opened by the line's own action: closed by the last-line rule, no answer chosen (rep_tail_end).
        if (s.CloseWhenShown)
        {
            s.CloseWhenShown = false;
            End(s.Pc, aborted: !AtLastLine(s));
            return;
        }
        Show?.Invoke(s.Pc, new DialogView(s.Owner.Id, speaker.Id, speaker.Name.Resolve(_world.Resources.Talk), line, replies, false, Voice(entry)));
    }

    /// <summary>
    /// Shows a player's current line again, as it was offered, without running its action script or journal update
    /// again (a loaded game: the conversation was restored with the world, the player's window was not).
    /// </summary>
    public bool Reshow(Creature pc)
    {
        if (_byPc.GetValueOrDefault(pc.Id) is not { } s || s.Entry < 0 || s.Entry >= s.Dialog.Entries.Count) return false;
        var entry = s.Dialog.Entries[s.Entry];
        var speaker = s.Owner;
        if (entry.Speaker.Length > 0 && _world.ByTag(entry.Speaker).FirstOrDefault(o => o.Area == s.Owner.Area) is { } tagged) speaker = tagged;
        var replies = new List<(int, string)>();
        for (int i = 0; i < s.Offered.Count; i++)
        {
            int index = s.Offered[i];
            if (index < 0 || index >= s.Dialog.Replies.Count) continue;
            var text = NodeText(s, 2, index, s.Dialog.Replies[index]);
            replies.Add((i, text.Length == 0 ? "[Continue]" : Tokens(text, s.Pc, s.Owner)));
        }
        if (replies.Count == 0) replies.Add((-1, "[End Dialog]"));
        var line = Tokens(NodeText(s, 1, s.Entry, entry), s.Pc, s.Owner);
        Show?.Invoke(s.Pc, new DialogView(s.Owner.Id, speaker.Id, speaker.Name.Resolve(_world.Resources.Talk), line, replies, false, Voice(entry)));
        return true;
    }

    string Voice(DialogNode line) => line.Sound.Length > 0 ? line.Sound
        : line.Text.StrRef >= 0 ? _world.Resources.Talk.Sound((uint)line.Text.StrRef) : "";

    static readonly Regex TokenPattern = new(@"<([^<>]+)>", RegexOptions.Compiled);

    /// <summary>
    /// Replaces the original's text tokens: names, gendered word pairs, class and race (in the forms
    /// classes.2da and racialtypes.2da give), alignment, time, CUSTOMnnn (SetCustomToken), and the
    /// action/check/highlight markup (which <paramref name="markup"/> false drops, for text with no
    /// conversation styling such as the journal). Capitalised tokens give capitalised words.
    /// </summary>
    public string Tokens(string text, Creature pc, GameObject owner, bool markup = true)
    {
        if (text.IndexOf('<') < 0) return text;
        var rm = _world.Resources;
        bool female = pc.Gender == 1;
        string Name(int part) => part switch
        {
            0 => pc.Template.GetLocString("FirstName").Resolve(rm.Talk),
            1 => pc.Template.GetLocString("LastName").Resolve(rm.Talk),
            _ => pc.Name.Resolve(rm.Talk),
        };
        // The first class taken, as the original does.
        string ClassName(string column) => pc.Classes.Count > 0 ? rm.TlkString((uint)rm.TwoDA("classes").GetInt(pc.Classes[0].Class, column)) : "";
        string RaceName(string column) => rm.TlkString((uint)rm.TwoDA("racialtypes").GetInt(pc.Race, column));
        return TokenPattern.Replace(text, m =>
        {
            var t = m.Groups[1].Value;
            if (t.StartsWith("CUSTOM", StringComparison.OrdinalIgnoreCase) && int.TryParse(t[6..], out var n))
                return PlayerTokens.Count > 0 && PlayerTokens.TryGetValue((pc.Id, n), out var own) ? own : CustomTokens.GetValueOrDefault(n, "");
            string Cap(string v) => char.IsUpper(t[0]) && v.Length > 0 ? char.ToUpperInvariant(v[0]) + v[1..] : v;
            return t switch
            {
                "FirstName" => Name(0),
                "LastName" => Name(1),
                "FullName" => Name(2),
                "PlayerName" => pc.Player?.PlayerName ?? "",
                "Class" => ClassName("Name"),
                "class" => ClassName("Lower"),
                "Classes" => ClassName("Plural"),
                "classes" => ClassName("Plural").ToLowerInvariant(),
                // racialtypes.2da's conversation forms: "Dwarven" / "dwarven", and the plural "Dwarves".
                "Race" => RaceName("ConverName"),
                "race" => RaceName("ConverNameLower"),
                "Races" => RaceName("NamePlural"),
                "races" => RaceName("NamePlural").ToLowerInvariant(),
                "Subrace" or "subrace" => pc.Subrace,
                "Deity" or "deity" => pc.Deity,
                "Level" or "level" => pc.TotalLevel.ToString(),
                "Gender" or "gender" => Cap(female ? "female" : "male"),
                "Lord/Lady" or "lord/lady" => Cap(female ? "lady" : "lord"),
                "Sir/Madam" or "sir/madam" => Cap(female ? "madam" : "sir"),
                "Brother/Sister" or "brother/sister" => Cap(female ? "sister" : "brother"),
                "Boy/Girl" or "boy/girl" => Cap(female ? "girl" : "boy"),
                "Lad/Lass" or "lad/lass" => Cap(female ? "lass" : "lad"),
                "Mister/Missus" or "mister/missus" => Cap(female ? "missus" : "mister"),
                "Man/Woman" or "man/woman" => Cap(female ? "woman" : "man"),
                "Male/Female" or "male/female" => Cap(female ? "female" : "male"),
                "Master/Mistress" or "master/mistress" => Cap(female ? "mistress" : "master"),
                "King/Queen" or "king/queen" => Cap(female ? "queen" : "king"),
                "He/She" or "he/she" => Cap(female ? "she" : "he"),
                "Him/Her" or "him/her" => Cap(female ? "her" : "him"),
                "His/Her" or "his/her" => Cap(female ? "her" : "his"),
                "His/Hers" or "his/hers" => Cap(female ? "hers" : "his"),
                "Bitch/Bastard" or "bitch/bastard" => Cap(female ? "bitch" : "bastard"),
                "Day/Night" or "day/night" => Cap(_world.Clock.IsDay ? "day" : "night"),
                "QuarterDay" or "quarterday" => Cap(_world.Clock.Hour switch { < 6 => "night", < 12 => "morning", < 18 => "afternoon", _ => "evening" }),
                "GameTime" or "gametime" => $"{_world.Clock.Hour:00}:{_world.Clock.Minute:00}",
                "GameYear" or "gameyear" => _world.Clock.Year.ToString(),
                "GameMonth" or "gamemonth" => _world.Clock.Month.ToString(),
                "Alignment" or "alignment" => Cap(AlignmentWord(pc)),
                "Good/Evil" or "good/evil" => Cap(pc.GoodEvil >= 70 ? "good" : pc.GoodEvil <= 30 ? "evil" : "neutral"),
                "Lawful/Chaotic" or "lawful/chaotic" => Cap(pc.LawfulChaotic >= 70 ? "lawful" : pc.LawfulChaotic <= 30 ? "chaotic" : "neutral"),
                "Law/Chaos" or "law/chaos" => Cap(pc.LawfulChaotic >= 70 ? "law" : pc.LawfulChaotic <= 30 ? "chaos" : "neutrality"),
                // Markup the client renders: keep as lightweight tags.
                "StartAction" => markup ? "[[action]]" : "",
                "StartCheck" => markup ? "[[check]]" : "",
                "StartHighlight" => markup ? "[[highlight]]" : "",
                "/Start" => markup ? "[[/]]" : "",
                _ => m.Value,
            };
        });
    }

    static string AlignmentWord(Creature c)
    {
        string law = c.LawfulChaotic >= 70 ? "lawful" : c.LawfulChaotic <= 30 ? "chaotic" : "neutral";
        string good = c.GoodEvil >= 70 ? "good" : c.GoodEvil <= 30 ? "evil" : "neutral";
        return law == "neutral" && good == "neutral" ? "true neutral" : $"{law} {good}";
    }
}
