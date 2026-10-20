using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Godot;
using Vector3 = System.Numerics.Vector3;

namespace OpenWinter.Client.Playtest;

/// <summary>Conversations: answers through the dialogue window's number keys, exploring replies it hasn't taken yet.</summary>
public partial class Autopilot
{
    sealed class ConvoState
    {
        public required PlaytestConversation Record;
        public readonly Dictionary<string, int> Visits = new(), Stale = new();
        public int Steps, NewLines;
        public double Started;
        public uint Owner;
    }

    ConvoState? _convo;
    readonly Dictionary<string, int> _repliesTaken = new();
    readonly HashSet<string> _seenLines = new();

    static readonly string[] EndingWords = ["[end", "goodbye", "good bye", "farewell", "bye.", "[leave", "i must go", "i have to go", "i'll be going", "i should go", "i'll leave", "leave now", "nothing.", "never mind", "nevermind"];

    /// <summary>Something the last speaker told the player to do (tutorials): attack them, rest, open a window.</summary>
    (string What, uint Speaker, string Name)? _instruction;

    static readonly System.Text.RegularExpressions.Regex AttackMe = new(@"\b(attack|punch|hit|strike|fight)\b[^.!?]*\bme\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    static readonly System.Text.RegularExpressions.Regex HitTarget = new(@"\b(hit|attack|shoot|strike|destroy)\b[^.!?]*\b(target|dummy|dummies)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    static readonly System.Text.RegularExpressions.Regex Ranged = new(@"\b(missile|ranged|bow|crossbow|sling|throw)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    /// <summary>A trial against a named target beats a plain "attack me" from the same speaker; ranged beats melee.</summary>
    static int Specificity(string what) => what switch { "target-ranged" => 3, "target-melee" => 2, "attack" => 1, _ => 0 };

    static string? Instruction(string text)
    {
        if (AttackMe.IsMatch(text)) return "attack";
        var t = text.ToLowerInvariant();
        // Weapon trials: "hit the target at the end of the hall with a missile weapon".
        if (HitTarget.IsMatch(text)) return Ranged.IsMatch(text) ? "target-ranged" : "target-melee";
        if (!(t.Contains("try ") || t.Contains("go ahead") || t.Contains("press ") || t.Contains("click") || t.Contains("open "))) return null;
        if (t.Contains("rest")) return "rest";
        if (t.Contains("inventory")) return "inventory";
        if (t.Contains("journal")) return "journal";
        if (t.Contains("map")) return "map";
        if (t.Contains("character sheet") || t.Contains("character record")) return "character";
        if (t.Contains("spell book") || t.Contains("spellbook")) return "spellbook";
        return null;
    }

    /// <summary>Does what a conversation asked, through the same keys and menus a player would use, then goes back to the speaker.</summary>
    async Task FollowInstruction((string What, uint Speaker, string Name) todo)
    {
        _report.Event(Now, "instruction", $"{todo.Name} asked to {todo.What}");
        _log.Add("bot", $"following instruction: {todo.What} ({todo.Name})");
        switch (todo.What)
        {
            case "attack":
                Host.Session.Request("attack", new { target = todo.Speaker });
                for (int i = 0; i < 80 && _dialog == null; i++) await Wait(0.25);
                if (_dialog == null) Stop();
                break;
            case "target-ranged" or "target-melee":
                await WeaponTrial(todo.What == "target-ranged", todo.Speaker);
                break;
            case "rest":
            {
                _restState = "";
                PressAction(Preferences.ActionCatalog.Rest);
                for (int i = 0; i < 120 && _restState is "" or "started"; i++) await Wait(0.25);
                _report.Event(Now, "rest", _restState.Length > 0 ? "rest " + _restState : "rest: nothing happened");
                break;
            }
            default:
            {
                string action = todo.What switch
                {
                    "inventory" => Preferences.ActionCatalog.Inventory,
                    "journal" => Preferences.ActionCatalog.Journal,
                    "map" => Preferences.ActionCatalog.Map,
                    "character" => Preferences.ActionCatalog.Character,
                    _ => Preferences.ActionCatalog.Spellbook,
                };
                PressAction(action);
                await Wait(1.5);
                await Shot("window_" + todo.What);
                PressAction(action);
                await Wait(0.5);
                break;
            }
        }
        _done.Remove(todo.Speaker);
        _attempts.Remove(todo.Speaker);
        _talkNext = todo.Speaker;
    }

    /// <summary>
    /// A weapon trial: for a ranged one, take a missile weapon (and ammunition) from a weapon rack if there's none in
    /// the pack and equip it; then attack the nearest training target the way the radial menu does.
    /// </summary>
    async Task WeaponTrial(bool ranged, uint instructor)
    {
        if (Me is not { } me || World is not { } world || me.Area is not { } area) return;
        var bi = world.Resources.TwoDA("baseitems");
        bool IsLauncher(Item i) => !bi.IsEmpty(i.BaseItem, "RangedWeapon");
        if (ranged && !me.Inventory.Any(IsLauncher) && !me.Equipment.Values.Any(IsLauncher))
        {
            var rack = area.Objects.OfType<Placeable>().Where(p => p.HasInventory && p.Useable && (p.Tag + p.Name.Resolve(world.Resources.Talk)).Contains("weapon", StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => Vector3.Distance(p.Position, me.Position)).FirstOrDefault();
            if (rack != null)
            {
                _log.Add("bot", "taking a missile weapon from " + Name(rack));
                Host.Session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = rack.Id }.Write());
                for (int i = 0; i < 80 && Host.Loot is not { Visible: true }; i++) await Wait(0.25);
                await HandleWindows();
            }
        }
        var weapon = ranged ? me.Inventory.FirstOrDefault(IsLauncher)
            : me.Equipment.Values.Any(i => IsLauncher(i)) ? me.Inventory.Where(i => !IsLauncher(i) && bi.GetInt(i.BaseItem, "WeaponType") > 0).OrderByDescending(i => i.Cost).FirstOrDefault() : null;
        if (weapon != null)
        {
            _log.Add("bot", "equipping " + Name(weapon));
            Host.Session.Request("equip", new { item = weapon.Id });
            if (ranged && me.Inventory.FirstOrDefault(i => i.BaseItem == bi.GetInt(weapon.BaseItem, "RangedWeapon", -1) && i != weapon) is { } ammo)
                Host.Session.Request("equip", new { item = ammo.Id });
            await Wait(2);
        }
        string[] want = ranged ? ["target"] : ["dummy", "target"];
        var target = area.Objects.OfType<Creature>().Where(c => !c.IsDead && c != me)
            .Select(c => (c, key: (c.Tag + " " + c.Name.Resolve(world.Resources.Talk)).ToLowerInvariant()))
            .Where(x => want.Any(x.key.Contains) && !x.key.Contains("spell") && !x.key.Contains("steal"))
            .OrderBy(x => Array.FindIndex(want, w => x.key.Contains(w))).ThenBy(x => Vector3.Distance(x.c.Position, (world.Get(instructor) ?? me).Position)).Select(x => x.c).FirstOrDefault();
        if (target == null) { _log.Add("bot", "no training target here"); return; }
        // Shoot from the instructor's end of the hall ("the target at the end of the hall"), well back from the target:
        // the Prelude's archery target only counts shots from more than 12 m (its instructor stands at 11.3 m).
        if (ranged && Vector3.Distance(me.Position, target.Position) < 15 && world.Get(instructor) is { } teacher)
        {
            var away = teacher.Position - target.Position;
            away.Z = 0;
            var spot = target.Position + Vector3.Normalize(away) * 16f;
            if (me.Area?.Walkmesh.HighestGround(spot.X, spot.Y) is { } z) spot.Z = z; else spot = teacher.Position;
            Host.Session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = spot, Run = true }.Write());
            for (int i = 0; i < 60 && Vector3.Distance(me.Position, spot) > 1.5f; i++) await Wait(0.25);
        }
        _log.Add("bot", $"attacking {Name(target)} for the {(ranged ? "ranged" : "melee")} trial from {Vector3.Distance(me.Position, target.Position):0.0} m");
        Host.Session.Request("attack", new { target = target.Id });
        // Until the instructor says something (or starts talking), the target breaks, or a minute passes.
        int chat = _chatFrom.GetValueOrDefault(instructor);
        for (int i = 0; i < 240 && _dialog == null && _chatFrom.GetValueOrDefault(instructor) == chat && !target.IsDead && !target.IsDestroyed; i++)
        {
            await Wait(0.25);
            if (i % 24 == 23 && Me is { Actions.Count: 0 }) Host.Session.Request("attack", new { target = target.Id });
        }
        await Wait(3);
        if (_dialog == null) Stop();
    }

    uint _talkNext;
    readonly Dictionary<string, int> _lessons = new();
    string _restState = "";

    /// <summary>Presses the key bound to a game action (the player's bindings).</summary>
    void PressAction(string action)
    {
        if (Settings.Bindings.Get(action, 0) is not { } bind || Preferences.InputBindings.ToEvent(bind) is not { } e) return;
        Input.ParseInputEvent(e);
        if (e is InputEventKey k) Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = k.PhysicalKeycode, CtrlPressed = k.CtrlPressed, ShiftPressed = k.ShiftPressed, AltPressed = k.AltPressed, Pressed = false });
    }

    static readonly string[] RepeatWords = ["again", "repeat", "go over", "once more", "start over", "from the beginning"];

    static bool IsRepeat(string text)
    {
        var t = UI.Kit.NwnText.Strip(text).ToLowerInvariant();
        return RepeatWords.Any(t.Contains);
    }

    static readonly System.Text.RegularExpressions.Regex Directions = new(@"\b(?:speak|talk|report|go|see|find|ask)\b(?:\s+(?:to|with|for))?\s+(?:the\s+)?([A-Z][a-zA-Z'\-]+(?:\s+[A-Z][a-zA-Z'\-]+)?)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>People named in "speak to Herban" style directions become where to go next.</summary>
    void LearnNames(string text)
    {
        foreach (System.Text.RegularExpressions.Match match in Directions.Matches(text))
            foreach (var word in match.Groups[1].Value.Split(' '))
                if (word.Length >= 4 && !StopWords.Contains(word) && !_keywords.Contains(word, StringComparer.OrdinalIgnoreCase))
                {
                    _keywords.Insert(0, word);
                    _log.Add("bot", "direction: " + word);
                }
        if (_keywords.Count > 24) _keywords.RemoveRange(24, _keywords.Count - 24);
    }

    static bool IsEnding(int index, string text)
    {
        if (index < 0) return true;
        var t = UI.Kit.NwnText.Strip(text).Trim().ToLowerInvariant();
        return EndingWords.Any(w => t.StartsWith(w) || t.Contains(w) && t.Length < 40);
    }

    async Task Converse()
    {
        var m = _dialog!;
        int seq = _dialogSeq;
        // Let the line show (the replies fade in after it).
        while (Now - _dialogAt < 1.0 && _dialogSeq == seq) await Wait(0.1);
        if (_dialogSeq != seq || _dialog == null) return;
        string node = Hash(m.SpeakerName + "|" + m.Text);
        // A later line ("fire away, I'll tell you if you hit the target") doesn't undo an earlier, more specific one.
        if (Instruction(UI.Kit.NwnText.Strip(m.Text)) is { } todo && !(_instruction is { } prev && prev.Speaker == m.Speaker && Specificity(prev.What) > Specificity(todo)))
        {
            // The same lesson three times is enough (tutorials repeat a step until the player does something else).
            var lesson = $"{m.Speaker}:{todo}";
            if ((_lessons[lesson] = _lessons.GetValueOrDefault(lesson) + 1) <= 3) _instruction = (todo, m.Speaker, UI.Kit.NwnText.Strip(m.SpeakerName));
        }
        if (_convo == null) await BeginConvo(m);
        var convo = _convo!;
        convo.Steps++;
        convo.Record.Nodes++;
        if (_seenLines.Add(node)) convo.NewLines++;
        int visits = convo.Visits[node] = convo.Visits.GetValueOrDefault(node) + 1;
        // Menus (hub lines) come back once per topic; it's a loop when the line keeps coming back with nothing new to pick.
        bool stale = m.Replies.All(r => IsEnding(r.Index, r.Text) || IsRepeat(r.Text) || _repliesTaken.ContainsKey(node + "|" + r.Text));
        int staleVisits = stale ? (convo.Stale[node] = convo.Stale.GetValueOrDefault(node) + 1) : convo.Stale.GetValueOrDefault(node);
        if (visits > 5 && staleVisits == 3)
            await Bug("dialog-loop", $"conversation with {UI.Kit.NwnText.Strip(m.SpeakerName)} keeps coming back to the same line",
                $"Seen {visits} times in one conversation, {staleVisits} of them with every reply already tried: “{Short(UI.Kit.NwnText.Strip(m.Text), 300)}”\nReplies: {string.Join(" | ", m.Replies.Select(r => Short(r.Text, 60)))}", shot: true, key: "loop:" + node);
        bool forceEnd = visits > 5 && staleVisits >= 3 || visits > 12 || convo.Steps > 60;
        if (convo.Steps == 61)
            await Bug("dialog-long", $"conversation with {UI.Kit.NwnText.Strip(m.SpeakerName)} ran 60+ steps", "The autopilot is ending it (probably a long tutorial or menu conversation, but worth a look).", shot: false, key: "long:" + convo.Record.Speaker);
        LearnNames(UI.Kit.NwnText.Strip(m.Text));

        int pick = PickReply(m, node, forceEnd);
        string replyText = pick >= 0 && pick < m.Replies.Count ? m.Replies[pick].Text : "(Esc)";
        if (pick >= 0 && pick < m.Replies.Count)
        {
            _repliesTaken[node + "|" + m.Replies[pick].Text] = _repliesTaken.GetValueOrDefault(node + "|" + m.Replies[pick].Text) + 1;
            convo.Record.Replies++;
        }
        _log.Add("bot", $"reply {pick + 1}: {Short(replyText, 80)}");
        _report.Count("replies");

        // The player's way first: the number key (or a click for replies past 9); Esc when no reply should be taken.
        if (pick < 0) PressKey(Key.Escape);
        else if (pick < 9) PressKey(Key.Key1 + pick);
        else if (Host.Dialog.ReplyPosition(pick) is { } at) await ClickAt(at);
        if (await DialogAdvanced(seq)) return;

        // Ignored: the server may have paused the conversation (ActionPauseConversation) while a script acts.
        var paused = Me is { } me && World?.System<Game.Conversations.ConversationService>().SessionOf(me) is { Paused: true };
        if (paused && await DialogAdvanced(seq, 40)) return;
        if (_dialogSeq != seq || _dialog == null) return;
        await Bug("dialog", "a reply did nothing", $"Picked “{Short(replyText, 80)}” (key {pick + 1}) on “{Short(UI.Kit.NwnText.Strip(m.Text), 160)}” from {m.SpeakerName}; " +
            $"nothing happened for {(paused ? 46 : 6)} s{(paused ? " (the conversation is paused on the server)" : "")}. Sending the reply directly.", shot: true, key: "noreply:" + node);
        Host.Session.Send(new DialogReplyMessage { Choice = pick >= 0 ? m.Replies[pick].Index : -1 }.Write());
        if (await DialogAdvanced(seq)) return;
        Host.Session.Send(new DialogReplyMessage { Choice = -1 }.Write());
        if (await DialogAdvanced(seq)) return;
        await Bug("dialog", "conversation cannot be left", $"Neither the reply nor ending it closed the window ({m.SpeakerName}).", shot: true, key: "stuckdialog:" + node);
        _dialog = null;
        _convo = null;
    }

    async Task<bool> DialogAdvanced(int seq, double seconds = 6)
    {
        double start = Now;
        while (Now - start < seconds)
        {
            await Wait(0.2);
            if (_dialogSeq != seq) return true;
        }
        return false;
    }

    int PickReply(DialogMessage m, string node, bool forceEnd)
    {
        // "Can you go over that again?" counts as an ending: the bot has already heard it.
        var options = m.Replies.Select((r, i) => (i, r.Index, r.Text, Taken: _repliesTaken.GetValueOrDefault(node + "|" + r.Text), End: IsEnding(r.Index, r.Text) || IsRepeat(r.Text))).ToList();
        if (options.Count == 0) return -1;
        if (forceEnd) return options.FirstOrDefault(o => o.End) is { Text: not null } e ? e.i : -1;
        // Untried replies that keep the conversation going, in the order offered (the advancing one is usually first).
        var fresh = options.Where(o => o.Taken == 0 && !o.End).ToList();
        if (fresh.Count > 0) return fresh[0].i;
        // Then untried endings, then the least-tried reply; leave once everything has been heard twice.
        var freshEnd = options.Where(o => o.Taken == 0 && o.End).ToList();
        if (freshEnd.Count > 0 && options.All(o => o.End || o.Taken > 0)) return freshEnd[0].i;
        var least = options.Where(o => !o.End).OrderBy(o => o.Taken).FirstOrDefault();
        if (least.Text != null && least.Taken < 2) return least.i;
        return options.FirstOrDefault(o => o.End) is { Text: not null } end ? end.i : options[^1].i;
    }

    async Task BeginConvo(DialogMessage m)
    {
        string speaker = UI.Kit.NwnText.Strip(m.SpeakerName);
        string first = UI.Kit.NwnText.Strip(m.Text);
        var record = _report.Conversations.FirstOrDefault(c => c.Speaker == speaker && c.FirstLine == first);
        if (record == null)
        {
            _report.Conversations.Add(record = new PlaytestConversation { Speaker = speaker, FirstLine = first, Area = AreaName, Time = Math.Round(Now, 1) });
            record.Screenshot = await Shot("talk_" + speaker);
            _report.Event(Now, "talk", $"{speaker}: “{Short(first, 100)}”");
        }
        else record.Times++;
        _convo = new ConvoState { Record = record, Started = Now, Owner = m.Owner };
        _talks[m.Owner] = _talks.GetValueOrDefault(m.Owner) + 1;
    }

    /// <summary>
    /// A conversation ended. Nothing new in it: that person is done for now. Something new: they may have more to
    /// say after it (tutorials, quest givers), and the world may have changed (doors unlocked).
    /// </summary>
    void EndConvo()
    {
        if (_convo is not { } c) return;
        _convo = null;
        if (c.NewLines == 0) _done.Add(c.Owner);
        else
        {
            Changed();
            _attempts.Remove(c.Owner);
        }
    }

    void PressKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    async Task ClickAt(Godot.Vector2 at)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
    }

    static string Hash(string s)
    {
        var h = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(h, 0, 6);
    }
}
