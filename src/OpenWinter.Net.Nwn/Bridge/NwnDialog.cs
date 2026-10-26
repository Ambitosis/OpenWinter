using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.ChatMessages;
using static OpenWinter.Net.Nwn.Messages.DialogMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using static OpenWinter.Net.Nwn.Messages.JournalMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Conversations, one-liners, the journal and cutscene mode.
/// <list type="bullet">
/// <item>A conversation step arrives as the line (DialogEntry), its chat copy with the voice-over
/// (ChatTokenTalkNoBubble) and the answers (DialogReplies); together they become one Dialog message. The player's
/// pick goes back as DialogReply with the server's reply index; Esc as an escape. Picks after the first are ignored
/// until the next answers arrive. The server's DialogClose closes the window.</item>
/// <item>Text comes as written or as talk table references, with the object its tokens describe: the server leaves
/// the tokens to the client, and they are filled in here, with the custom tokens the server sent.</item>
/// <item>Journal changes become "journal" notifies; the server's journal-updated message prints the journal line.
/// The client's journal window opening and closing open and close the quest screen; on opening, the server sends
/// the whole journal.</item>
/// </list>
/// </summary>
public sealed class NwnDialog(NwnWorldMirror world, INwnBridgeData data, Action<byte[]> emit)
{
    /// <summary>The window's labels for an empty answer and for a line with no answers.</summary>
    public const string Continue = "[Continue]", EndDialog = "[End Dialog]";

    sealed class Quest
    {
        public string Tag = "", Name = "", Text = "";
        public int State;
        public uint Priority, Day, Time;
        public int Picture = -1;
        public bool Completed;
    }

    readonly Dictionary<string, Quest> _quests = new(StringComparer.Ordinal);
    uint _owner, _speaker, _entryIndex;
    string _line = "", _sound = "";
    bool _open, _pending, _lastLine, _answered, _journalOpen;
    readonly List<uint> _offered = [];
    int _dawn = 6, _dusk = 18;

    /// <summary>The token values each creature reads (names, race, classes, gender) and the custom tokens.</summary>
    public NwnTokens Tokens { get; } = new(data);

    /// <summary>Whether a conversation window is open.</summary>
    public bool Open => _open;

    /// <summary>Handles a server message that belongs here; returns false for everything else (some are only read).</summary>
    public bool ToClient(NwnMessage message)
    {
        switch (message)
        {
            case DialogEntry m:
                // The line's chat copy and its answers follow.
                _owner = m.Owner;
                _speaker = m.Speaker;
                _line = Tokens.Resolve(Text(m.Entry), m.TokenTarget, gender: m.Entry.Gender);
                _sound = "";
                _pending = true;
                return true;
            case DialogReplies m:
                Replies(m);
                return true;
            case DialogReplyChosen:
                // The chosen answer reaches the chat window as the player's own chat line.
                return true;
            case DialogClose:
                Close();
                return true;
            case ChatTokenTalkNoBubble m:
                // A conversation line in the chat window; its sound is the line's voice-over.
                if (_pending && m.SpeakerId == _speaker) _sound = m.Sound;
                Chat(ChatChannel.Dialog, m.SpeakerId, Tokens.Resolve(Text(m.Message), m.TargetId, markup: false, m.Message.Gender));
                return true;
            case ChatTokenTalk m:
                // A one-liner: spoken over the talker's head.
                Chat(ChatChannel.Talk, m.SpeakerId, Tokens.Resolve(Text(m.Message), m.TargetId, markup: false, m.Message.Gender));
                return true;
            case CustomTokenMessages.CustomTokenSingle m:
                Tokens.SetCustom(m.TokenNumber, m.TokenValue);
                return true;
            case CustomTokenMessages.CustomTokenList m:
                foreach (var t in m.Tokens) Tokens.SetCustom(t.TokenNumber, t.TokenValue);
                return true;
            case JournalFullUpdate m:
                Journal(m);
                return true;
            case JournalAddQuest m:
                Store(new Quest
                {
                    Tag = m.PlotId, Name = Text(m.Name), Text = Text(m.Text), State = m.State, Priority = m.Priority,
                    Day = m.CalendarDay, Time = m.TimeOfDay, Picture = Picture(m.PictureIndex), Completed = m.QuestCompleted,
                });
                return true;
            case JournalRemoveQuest m:
                Remove(m.PlotId, update: false);
                return true;
            case JournalUpdated m:
                JournalLine(Text(m.Name));
                return true;
            case CutsceneMessages.CutsceneStatus m:
                Notify("cutscene", new JsonObject { ["enabled"] = m.Active, ["leftClick"] = m.LeftMouseButtonEnabled });
                return true;
            case CutsceneMessages.CutsceneFadeToBlack m:
                Notify("fade", new JsonObject { ["op"] = "to-black", ["speed"] = Finite(m.Speed) });
                return true;
            case CutsceneMessages.CutsceneFadeFromBlack m:
                Notify("fade", new JsonObject { ["op"] = "from-black", ["speed"] = Finite(m.Speed) });
                return true;
            case CutsceneMessages.CutsceneStopFade:
                Notify("fade", new JsonObject { ["op"] = "stop", ["speed"] = 0 });
                return true;
            case CutsceneMessages.CutsceneBlackScreen:
                Notify("fade", new JsonObject { ["op"] = "black", ["speed"] = 0 });
                return true;
            // Read only: what the tokens need, while other parts handle the messages.
            case GameObjUpdateMessages.GameObjUpdateObjList list:
                Learn(list);
                return false;
            case PlayerListMessages.PlayerListFull m:
                foreach (var p in m.Players) Learn(p);
                return false;
            case PlayerListMessages.PlayerListAdd m:
                Learn(m.Player);
                return false;
            case AreaMessages.AreaClientArea a:
                (_dawn, _dusk) = (a.Dawn, a.Dusk);
                Clock(a.Hour, a.Month, a.Year);
                return false;
            case ModuleMessages.ModuleTime t:
                Clock(t.Hour, t.Month, t.Year);
                return false;
        }
        return false;
    }

    /// <summary>
    /// The NWN message for the player's pick: an index into the answers shown, or -1 for Esc and the end choice.
    /// -1 picks the server's end choice when it offers one; at the last line (the server marks it) with a single
    /// answer it picks that answer; a line with no answers at all is ended; anywhere else -1 is an escape. Null when
    /// no conversation is open, the index isn't one of the answers, or an answer was already sent for this line.
    /// </summary>
    public NwnMessage? Reply(int choice)
    {
        if (!_open || _answered) return null;
        if (choice < 0 && _offered.IndexOf(NoReply) is >= 0 and var end) choice = end;
        else if (choice < 0 && _lastLine && _offered.Count == 1) choice = 0;
        NwnMessage? reply = choice < 0
            ? new DialogReply { Owner = _owner, ReplyIndex = NoReply, EscapeDialog = _offered.Count == 0 ? (byte)0 : (byte)1, CurrentEntryIndex = _entryIndex }
            : choice < _offered.Count ? new DialogReply { Owner = _owner, ReplyIndex = _offered[choice], CurrentEntryIndex = _entryIndex }
            : null;
        _answered = reply != null;
        return reply;
    }

    /// <summary>The reply index of the end choice the server offers at a line with no answers.</summary>
    public const uint NoReply = uint.MaxValue;

    /// <summary>The NWN message for a client request that belongs here, or null.</summary>
    public NwnMessage? FromClient(string kind, string json)
    {
        switch (kind)
        {
            case "journal":
                // The journal window opening (asked again while open, the journal is already current): the server
                // answers with the whole journal.
                if (_journalOpen) return null;
                _journalOpen = true;
                return new JournalQuestScreenOpen();
            case "journal-closed":
                if (!_journalOpen) return null;
                _journalOpen = false;
                return new JournalQuestScreenClosed();
            case "cutscene-abort":
                return new CutsceneMessages.CutsceneCancel();
        }
        return null;
    }

    void Replies(DialogReplies m)
    {
        _entryIndex = m.CurrentEntryIndex;
        _lastLine = m.EndOfDialog;
        _answered = false;
        _offered.Clear();
        var shown = new List<(int, string)>();
        // The inactive answers that follow the active ones aren't offered.
        foreach (var r in m.Replies.Take((int)Math.Min(m.RepliesCount, int.MaxValue)))
        {
            var text = Tokens.Resolve(Text(r.Text), m.TokenTarget, gender: r.Text.Gender);
            // The end choice is shown as the client's own, which sends -1.
            if (r.ReplyIndex == NoReply && text.Length == 0)
            {
                shown.Add((-1, EndDialog));
                _offered.Add(NoReply);
                continue;
            }
            shown.Add((_offered.Count, text.Length == 0 ? Continue : text));
            _offered.Add(r.ReplyIndex);
        }
        if (shown.Count == 0) shown.Add((-1, EndDialog));
        _pending = false;
        _open = true;
        emit(new DialogMessage
        {
            Owner = _owner,
            Speaker = _speaker,
            SpeakerName = world.NameOf(_speaker),
            Text = _line,
            Sound = _sound,
            Replies = shown,
        }.Write());
    }

    void Close()
    {
        var owner = _owner;
        _open = _pending = _answered = false;
        _offered.Clear();
        emit(new DialogMessage { Owner = owner, Speaker = owner, Ended = true }.Write());
    }

    void Chat(ChatChannel channel, uint speaker, string text)
    {
        if (text.Length == 0) return;
        emit(new ChatMessage { Channel = channel, Speaker = speaker, SpeakerName = world.NameOf(speaker), Text = text }.Write());
    }

    // -- journal -------------------------------------------------------------

    void Journal(JournalFullUpdate m)
    {
        if (m.FullUpdate || m.ClearOldEntries)
        {
            _quests.Clear();
            Notify("journal-reset", []);
        }
        foreach (var e in m.Entries)
        {
            if ((e.Flags & JournalUpdateFlag.Remove) != 0)
            {
                Remove(e.PlotId, update: false);
                continue;
            }
            var q = _quests.GetValueOrDefault(e.PlotId) ?? new Quest { Tag = e.PlotId };
            bool Has(JournalUpdateFlag flag) => m.FullUpdate || (e.Flags & flag) != 0;
            if (Has(JournalUpdateFlag.Szname)) q.Name = Text(e.Name);
            if (Has(JournalUpdateFlag.Sztext)) q.Text = Text(e.Text);
            if (Has(JournalUpdateFlag.Ncalendarday)) q.Day = e.CalendarDay;
            if (Has(JournalUpdateFlag.Ntimeofday)) q.Time = e.TimeOfDay;
            if (Has(JournalUpdateFlag.Nstate)) q.State = e.State;
            if (Has(JournalUpdateFlag.Npriority)) q.Priority = e.Priority;
            if (Has(JournalUpdateFlag.Npictureindex)) q.Picture = Picture(e.PictureIndex);
            if (Has(JournalUpdateFlag.Bquestcompleted)) q.Completed = e.QuestCompleted;
            Store(q);
        }
    }

    void Store(Quest q)
    {
        _quests[q.Tag] = q;
        Notify("journal", Describe(q, update: false));
    }

    void Remove(string tag, bool update)
    {
        if (!_quests.Remove(tag, out var q)) return;
        Notify("journal", new JsonObject { ["tag"] = tag, ["name"] = q.Name, ["removed"] = true, ["update"] = update });
    }

    /// <summary>
    /// "Your journal has been updated." for the quest of that name: the quest is sent again as an update (the client
    /// prints the line and flashes the journal button). A quest not seen yet gets the line alone.
    /// </summary>
    void JournalLine(string name)
    {
        var q = _quests.Values.FirstOrDefault(q => q.Name == name);
        Notify("journal", q != null ? Describe(q, update: true)
            : new JsonObject { ["tag"] = "", ["name"] = name, ["removed"] = true, ["update"] = true });
    }

    JsonObject Describe(Quest q, bool update)
    {
        string T(string s) => Tokens.Resolve(s, world.Controlled, markup: false);
        return new JsonObject
        {
            ["tag"] = q.Tag, ["state"] = q.State, ["name"] = T(q.Name), ["text"] = T(q.Text), ["end"] = q.Completed,
            ["update"] = update, ["priority"] = Int(q.Priority), ["picture"] = q.Picture, ["xp"] = 0, ["day"] = Int(q.Day),
            ["time"] = Int(q.Time), ["history"] = new JsonArray(), ["removed"] = false,
        };
    }

    // -- what tokens read -----------------------------------------------------

    void Learn(GameObjUpdateMessages.GameObjUpdateObjList list)
    {
        foreach (var entry in list.Entries)
        {
            switch (entry)
            {
                case GameObjUpdateObjListAppearanceCreature { Name.Value: OriginalCreatureName n } a:
                    var who = Tokens.Of(a.ObjectId);
                    (who.FirstName, who.LastName) = (Text(n.First), Text(n.Last));
                    break;
                case GameObjUpdateObjListUpdateCreature { PrimaryAttributes: { } p } u:
                    var w = Tokens.Of(u.ObjectId);
                    w.Race = p.Race;
                    w.Subrace = p.Subrace;
                    w.Deity = p.Deity;
                    w.Gender = p.Gender;
                    w.GoodEvil = p.AlignmentGoodEvil;
                    w.LawChaos = p.AlignmentLawChaos;
                    w.Classes = p.Classes.Select(c => ((int)c.ClassId, (int)c.Level)).ToList();
                    w.Known = true;
                    break;
                case GameObjUpdateDeleteMessages.ObjListDeleteCreature d:
                    Tokens.Forget(d.ObjectId);
                    break;
            }
        }
    }

    void Learn(PlayerListMessages.Player p)
    {
        var w = Tokens.Of(p.ObjectId);
        w.PlayerName = p.PlayerName;
        if (p.PrimaryPc is { } pc)
            (w.FirstName, w.LastName) = (Text(pc.FirstName), Text(pc.LastName));
    }

    void Clock(byte? hour, byte? month, uint? year)
    {
        if (hour is { } h) Tokens.Hour = h;
        if (month is { } m) Tokens.Month = m;
        if (year is { } y) Tokens.Year = (int)y;
        Tokens.IsDay = Tokens.Hour >= _dawn && Tokens.Hour < _dusk;
    }

    void Notify(string kind, JsonObject json) =>
        emit(new NotifyMessage { Kind = kind, Subject = world.Controlled, Json = json.ToJsonString() }.Write());

    /// <summary>A journal picture index; 0xFFFF is none.</summary>
    static int Picture(ushort index) => index == ushort.MaxValue ? -1 : index;

    /// <summary>The client reads journal numbers as 32-bit signed.</summary>
    static int Int(uint value) => (int)Math.Min(value, int.MaxValue);

    /// <summary>A fade speed fit for JSON: the server's float could be anything.</summary>
    static float Finite(float speed) => float.IsFinite(speed) ? speed : 0;

    string Text(NwnLocString s) => s.IsStrRef ? data.Text(s.StrRef, s.Gender) : s.Text;
}
