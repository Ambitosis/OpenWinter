using Vector3 = System.Numerics.Vector3;
using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI;
using OpenWinter.Client.Views;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Godot;
using GVector2 = Godot.Vector2;
using GVector3 = Godot.Vector3;

namespace OpenWinter.Client.Playtest;

/// <summary>What the autopilot needs from the running game (set up by Main.Playtest.cs).</summary>
public sealed class PlaytestHost
{
    public required Func<GameServer?> Server;
    public required ClientSession Session;
    public required OrbitCamera Camera;
    public required Interaction Interaction;
    public required Func<bool> Loading;
    public required DialogPanel Dialog;
    public required ContainerPanel? Loot;
    public required Action Quit;
}

/// <summary>
/// Playtesting autopilot (--playtest outdir): plays the module like a player would, through the client's own
/// input and request paths (clicks on objects, number keys in conversations, the loot window's Take all, the
/// radial menu's attack request), and writes a bug report with screenshots. It reads the server only for what a
/// player could see on screen (names, positions, hostility, where an exit leads) and to detect problems.
/// </summary>
public partial class Autopilot : Node
{
    public PlaytestHost Host = null!;
    public string OutDir = "out/playtest";
    public double Minutes = 20;
    public bool God;
    public string ModulePath = "", Character = "";

    readonly PlaytestLog _log = new();
    PlaytestReport _report = new();
    int _shots;
    const int MaxShots = 300;
    bool _finished;
    readonly Random _rng = new(1234);

    // What the player has seen.
    DialogMessage? _dialog;
    int _dialogSeq;
    double _dialogAt;
    bool _cutscene;
    string _cutsceneArea = "";
    double _cutsceneSince;
    bool _fadedBlack;
    double _fadedSince;
    bool _deathPanel;
    string _areaResRef = "";
    double _areaSince, _areaClock; // entered the area; last time its seconds were counted
    int _areaSeq;
    PlaytestArea? _area;

    // Exploration memory.
    readonly Dictionary<uint, int> _attempts = new();        // object id -> interactions tried (this area visit)
    readonly HashSet<uint> _done = new();                     // finished with, until the journal changes
    readonly HashSet<uint> _locked = new();                   // locked exits and doors, until something changes
    readonly Dictionary<uint, int> _lockedTimes = new();      // times found locked, until the journal changes
    readonly Dictionary<uint, int> _talks = new();            // conversations started with each creature, whole run
    readonly Dictionary<uint, int> _doorRetries = new();      // doors opened on the way to each target, whole run
    readonly HashSet<uint> _unreachable = new();              // failed to reach, this area visit
    readonly Dictionary<string, int> _exitsTaken = new();     // "area>target" -> times
    readonly HashSet<string> _visitedAreas = new(StringComparer.OrdinalIgnoreCase);
    readonly List<string> _keywords = new();
    int _journalEpoch;

    public override void _Ready()
    {
        Directory.CreateDirectory(OutDir);
        Directory.CreateDirectory(Path.Combine(OutDir, "shots"));
        _log.OpenFile(Path.Combine(OutDir, "log.txt"));
        OS.AddLogger(new PlaytestLog.GodotSink { Log = _log });
        Console.SetOut(new PlaytestLog.ConsoleTee(Console.Out, _log));
        _report = new PlaytestReport
        {
            Module = System.IO.Path.GetFileNameWithoutExtension(ModulePath.TrimEnd('/', '\\')),
            Character = Character.Length > 0 ? Character : "(selection screen)",
            Started = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            GodMode = God,
        };
        // Quiet: a long unattended run should not blast the speakers.
        AudioServer.SetBusMute(0, true);
        Host.Session.DialogReceived += OnDialog;
        Host.Session.NotifyReceived += OnNotify;
        Host.Session.AreaLoaded += OnAreaLoaded;
        Host.Session.ChatReceived += m =>
        {
            _log.Add("chat", $"{m.SpeakerName}: {m.Text}");
            if (m.SpeakerName.Length > 0 && m.Speaker != Host.Session.ControlledId) _chatFrom[m.Speaker] = _chatFrom.GetValueOrDefault(m.Speaker) + 1;
        };
        GD.Print($"[playtest] {ModulePath} for {Minutes} min, report in {OutDir}");
        _ = Run();
    }

    double Now => _log.Now;

    GameServer? Server => Host.Server();
    World? World => Server?.World;
    Creature? Me => World?.Get<Creature>(Host.Session.ControlledId);
    ObjectView? MyView => Host.Session.Controlled;
    string AreaName => Host.Session.Area?.Info.Name is { Length: > 0 } n ? n : _areaResRef;

    // ------------------------------------------------------------------ events from the client

    void OnDialog(DialogMessage m)
    {
        _dialogSeq++;
        _dialogAt = Now;
        _dialog = m.Ended ? null : m;
        if (!m.Ended) _log.Add("dialog", $"{m.SpeakerName}: {Short(m.Text, 160)} [{string.Join(" | ", m.Replies.Select(r => Short(r.Text, 40)))}]");
        else { _log.Add("dialog", "(conversation ended)"); EndConvo(); }
    }

    readonly HashSet<string> _notifyKinds = new();

    void OnNotify(NotifyMessage m)
    {
        if (_notifyKinds.Add(m.Kind)) _log.Add("notify", $"first {m.Kind}: {Short(m.Json, 200)}");
        try
        {
            using var doc = JsonDocument.Parse(m.Json);
            var d = doc.RootElement;
            switch (m.Kind)
            {
                case "cutscene":
                    bool on = d.GetProperty("enabled").GetBoolean();
                    if (on != _cutscene) { _cutscene = on; _cutsceneSince = Now; _cutsceneArea = AreaName; _report.Event(Now, "cutscene", on ? "cutscene mode on" : "cutscene mode off"); }
                    break;
                case "fade":
                    string op = d.GetProperty("op").GetString() ?? "";
                    bool black = op is "to-black" or "black";
                    if (black != _fadedBlack) { _fadedBlack = black; _fadedSince = Now; }
                    _log.Add("fx", "fade " + op);
                    break;
                case "gui-death":
                    _deathPanel = true;
                    _report.Event(Now, "death", "the player died");
                    _report.Count("deaths");
                    break;
                case "rest" when m.Subject == Host.Session.ControlledId:
                    _restState = d.GetProperty("state").GetString() ?? "";
                    break;
                case "journal": // only ever sent to this player (also before it controls a creature: OnClientEnter)
                    OnJournal(d);
                    break;
            }
        }
        catch (Exception e) { _log.Add("bot", $"notify {m.Kind}: {e}"); }
    }

    void OnJournal(JsonElement d)
    {
        if (d.TryGetProperty("removed", out var removed) && removed.ValueKind == JsonValueKind.True) return;
        string tag = d.GetProperty("tag").GetString() ?? "";
        int state = d.GetProperty("state").GetInt32();
        string name = d.GetProperty("name").GetString() ?? tag;
        string text = d.GetProperty("text").GetString() ?? "";
        bool end = d.TryGetProperty("end", out var e) && e.GetBoolean();
        var q = _report.Quests.FirstOrDefault(x => x.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
        if (q == null) _report.Quests.Add(q = new PlaytestQuest { Tag = tag, Name = UI.Kit.NwnText.Strip(name), FirstTime = Math.Round(Now, 1) });
        if (q.States.Count > 0 && q.States[^1] == state) return; // re-sent (journal opened)
        q.States.Add(state);
        q.LastText = UI.Kit.NwnText.Strip(text);
        q.Finished = end;
        q.LastTime = Math.Round(Now, 1);
        _report.Event(Now, "quest", $"{q.Name} → {state}{(end ? " (finished)" : "")}: {Short(q.LastText, 140)}");
        // New quest text: what it names is where to go next, and people may have new things to say.
        _keywords.Clear();
        _keywords.AddRange(Keywords(q.Name + " " + q.LastText));
        _journalEpoch++;
        _lockedTimes.Clear();
        Changed();
    }

    void OnAreaLoaded()
    {
        var info = Host.Session.Area?.Info;
        if (info == null) return;
        if (_area != null) _area.Seconds = Math.Round(_area.Seconds + Now - _areaClock, 1);
        _areaResRef = info.ResRef;
        _areaSince = Now;
        _areaClock = Now;
        _areaSeq++;
        _attempts.Clear();
        _unreachable.Clear();
        _area = _report.Areas.FirstOrDefault(a => a.ResRef.Equals(info.ResRef, StringComparison.OrdinalIgnoreCase));
        bool first = _area == null;
        if (_area == null) _report.Areas.Add(_area = new PlaytestArea { ResRef = info.ResRef, Name = UI.Kit.NwnText.Strip(info.Name), FirstEntered = Math.Round(Now, 1) });
        _area.Visits++;
        _visitedAreas.Add(info.ResRef);
        _report.Event(Now, "area", $"entered {_area.Name} ({info.ResRef}){(first ? " — first visit" : "")}");
        if (first) _ = AreaShot(_area, _areaSeq);
    }

    async Task AreaShot(PlaytestArea area, int seq)
    {
        // Once the loader is gone and the camera has settled.
        for (int i = 0; i < 300 && (Host.Loading() || Host.Session.Controlled == null); i++) await Wait(0.1);
        await Wait(2.0);
        if (seq != _areaSeq) return;
        area.Screenshot = await Shot("area_" + Safe(area.ResRef));
    }

    // ------------------------------------------------------------------ main loop

    async Task Run()
    {
        try
        {
            // Into the game: the character (--character) or the selection screen's first entry.
            double start = Now;
            while (Host.Session.Controlled == null || Host.Loading())
            {
                await Wait(0.5);
                if (Now - start > 15 && Host.Session.Controlled == null && !Host.Loading()) await PickCharacter();
                if (Now - start > 180)
                {
                    await Bug("no-control", "never got a character", "No controlled creature 3 minutes after starting.", shot: true);
                    await Finish("never entered the game");
                    return;
                }
            }
            _report.Event(Now, "start", $"in the game after {Now - start:0.0} s");
            await Wait(1.0);
            if (God) Chat("/god");
            double end = Now + Minutes * 60;
            double nextSave = Now + 30;
            while (!_finished && Now < end)
            {
                await Wait(0.25);
                if (Now > nextSave) { SaveReport(); nextSave = Now + 30; }
                try { await Step(); }
                catch (Exception e) when (e is not TaskCanceledException)
                {
                    await Bug("autopilot", "autopilot error: " + e.GetType().Name, e.ToString(), shot: false);
                    await Wait(2);
                }
            }
            if (!_finished) await Finish("time budget used");
        }
        catch (Exception e)
        {
            GD.PrintErr("[playtest] crashed: " + e);
            _report.Outcome = "autopilot crashed: " + e.Message;
            SaveReport();
            Host.Quit();
        }
    }

    async Task PickCharacter()
    {
        var select = GetTree().Root.FindChildren("*", "Control", true, false).OfType<CharacterSelectPanel>().FirstOrDefault(p => p.Visible);
        if (select == null) return;
        _report.Event(Now, "start", "choosing the first character on the selection screen");
        await Shot("select");
        select.Play();
        await Wait(5);
    }

    /// <summary>One decision: answer the conversation, deal with windows, or pick something to do.</summary>
    async Task Step()
    {
        await Watch();
        if (Host.Loading() || Host.Session.Controlled == null || Me is not { } me) return;
        // God mode: scripts may clear immortality (SetImmortal), and a long fight wears the hit points down.
        if (God && Now - _godCheck > 20 && !_cutscene)
        {
            _godCheck = Now;
            if (!me.IsImmortal) Chat("/god");
            else if (me.CurrentHitPoints < me.MaxHitPoints / 3) Chat("/heal");
        }
        if (_deathPanel || me.IsDead)
        {
            await Wait(2);
            await Shot("death");
            Host.Session.Request("respawn");
            _deathPanel = false;
            await Wait(3);
            return;
        }
        if (_dialog != null && Host.Dialog.Visible) { await Converse(); return; }
        if (await HandleWindows()) return;
        if (_cutscene) return; // the player has no control
        if (_fadedBlack && Now - _fadedSince < 15) return; // something is being staged
        if (me.Actions.Count > 0 && !(me.Actions[0] is MoveAction)) return; // busy (a script's queue)
        if (_instruction is { } todo) { _instruction = null; await FollowInstruction(todo); return; }
        // A new key in the pack (from a conversation, a body): locked doors are worth another try.
        int keys = me.Inventory.Count(i => i.BaseItem == 24 || i.Tag.Contains("key", StringComparison.OrdinalIgnoreCase)
            || i.Name.Resolve(World?.Resources.Talk).Contains("key", StringComparison.OrdinalIgnoreCase)); // BASE_ITEM_KEY or named so
        if (keys != _packCount)
        {
            if (_packCount >= 0 && keys > _packCount) { _lockedTimes.Clear(); _locked.Clear(); _report.Event(Now, "key", "got a key"); }
            _packCount = keys;
        }
        var goal = Choose(me);
        if (goal == null)
        {
            await Wander(me);
            return;
        }
        await Pursue(goal);
    }

    /// <summary>Containers get emptied with Take all; other windows a script opened (stores, ...) are closed.</summary>
    async Task<bool> HandleWindows()
    {
        if (Host.Loot is { Visible: true } loot)
        {
            await Wait(0.6);
            var take = loot.FindChildren("*", "Button", true, false).OfType<Godot.Button>().FirstOrDefault(b => b.Text == "Take all" && b.IsVisibleInTree());
            if (take != null) { take.EmitSignal(BaseButton.SignalName.Pressed); _report.Count("containers looted"); }
            await Wait(0.6);
            if (loot.Visible) loot.Close();
            return true;
        }
        var open = UI.WindowStack.Windows.Where(w => GodotObject.IsInstanceValid(w) && w.Visible && w.Closable).ToList();
        if (open.Count == 0) return false;
        _windowsOpenSince ??= Now;
        if (Now - _windowsOpenSince < 3) return true;
        foreach (var w in open)
        {
            _report.Event(Now, "window", "closing " + w.GetType().Name);
            if (w is StorePanel) await Shot("store_" + _report.Stats.GetValueOrDefault("stores"));
            if (w is StorePanel) _report.Count("stores");
            w.Close();
        }
        _windowsOpenSince = null;
        return true;
    }
    double? _windowsOpenSince;
    double _godCheck;
    int _packCount = -1;
    readonly Dictionary<uint, int> _chatFrom = new();

    // ------------------------------------------------------------------ goals

    enum GoalKind { Fight, Talk, Loot, Use, Door, PickUp, Exit }

    sealed record Goal(GoalKind Kind, GameObject Target, double Score, string Why);

    Goal? Choose(Creature me)
    {
        if (me.Area is not { } area || World is not { } world) return null;
        var candidates = new List<Goal>();
        var views = Host.Session.Objects;
        foreach (var o in area.Objects)
        {
            if (o == me || o.IsDestroyed || _unreachable.Contains(o.Id) || _done.Contains(o.Id) || _locked.Contains(o.Id) && StillLocked(o)) continue;
            int tried = _attempts.GetValueOrDefault(o.Id);
            float dist = Vector3.Distance(o.Position, me.Position);
            double near = -dist / 4;
            switch (o)
            {
                case Creature c when !c.IsDead && views.ContainsKey(c.Id) && !(c.Template.GetInt("IsDM") != 0):
                {
                    if (world.Factions.IsEnemy(c, me))
                    {
                        // Training dummies and caged plot creatures can't be beaten: leave them be unless they fight us.
                        bool fightsMe = world.System<Game.Combat.CombatSystem>().TargetOf(c) == me.Id;
                        if (dist < 30 && tried < 3 && (fightsMe || !c.Plot && !c.IsImmortal)) candidates.Add(new Goal(GoalKind.Fight, c, 200 + near, fightsMe ? "attacking us" : "hostile"));
                    }
                    else if (tried < 2 && _talks.GetValueOrDefault(c.Id) < 12 && (c.Conversation.Length > 0 || c.Script("ScriptDialogue").Length > 0) && c.Master == GameObject.InvalidId)
                        candidates.Add(new Goal(GoalKind.Talk, c, 60 + Bias(c) + near - tried * 30 + (c.Id == _talkNext ? 100 : 0), c.Id == _talkNext ? "asked to come back" : "not talked to yet"));
                    break;
                }
                case Item it when views.ContainsKey(it.Id) && tried < 2:
                    candidates.Add(new Goal(GoalKind.PickUp, it, 70 + Bias(it) + near, "on the ground"));
                    break;
                case Placeable p when views.ContainsKey(p.Id) && p.Useable && tried < 1:
                    candidates.Add(new Goal(p.HasInventory ? GoalKind.Loot : GoalKind.Use, p, (p.HasInventory ? 58 : 45) + Bias(p) + near - (p.Locked ? 30 : 0), p.Locked ? "locked" : "usable"));
                    break;
                case Door d when views.ContainsKey(d.Id) && !d.IsTransition && !d.IsOpen && tried < 1:
                    // Players open doors early: rooms behind them are otherwise out of reach.
                    candidates.Add(new Goal(GoalKind.Door, d, 55 + near - (d.Locked ? 20 : 0), d.Locked ? "locked door" : "closed door"));
                    break;
            }
        }
        // Spend a while in an area before leaving; leave anyway when there is little left to do or it has taken too long.
        double inArea = Now - _areaSince;
        var best = candidates.OrderByDescending(c => c.Score).FirstOrDefault();
        if (best != null && (best.Kind == GoalKind.Fight || inArea < 360)) return best;
        return ChooseExit(me, area) ?? best;
    }

    Goal? ChooseExit(Creature me, Game.Areas.Area area)
    {
        // Just arrived: look around before leaving again (two exhausted areas would otherwise ping-pong).
        if (Now - _areaSince < 30) return null;
        var world = World!;
        var exits = new List<Goal>();
        foreach (var o in area.Objects)
        {
            bool isExit = o is Door { IsTransition: true } || o is Trigger { TriggerType: Trigger.TypeTransition } || o is Door d2 && d2.Script("OnClick").Length > 0 && !d2.IsOpen;
            // Found locked twice: leave it until it is unlocked (the cursor shows that) or the journal moves on.
            if (!isExit || _unreachable.Contains(o.Id) || _locked.Contains(o.Id) && StillLocked(o) || _lockedTimes.GetValueOrDefault(o.Id) >= 2 && o is Door { Locked: true } || _attempts.GetValueOrDefault(o.Id) >= 3) continue;
            var target = Game.WorldObjects.TransitionService.ResolveTarget(world, o)?.Area;
            if (target == area) continue;
            string key = area.ResRef + ">" + (target?.ResRef ?? o.Tag);
            int taken = _exitsTaken.GetValueOrDefault(key);
            double score = 50 - taken * 25 - Vector3.Distance(o.Position, me.Position) / 10;
            if (target != null && !_visitedAreas.Contains(target.ResRef)) score += 60;
            if (target != null) score += BiasText(target.Name.Resolve(world.Resources.Talk) + " " + target.Tag);
            exits.Add(new Goal(GoalKind.Exit, o, score, target != null ? "to " + target.Name.Resolve(world.Resources.Talk) : "scripted exit"));
        }
        return exits.OrderByDescending(e => e.Score).FirstOrDefault();
    }

    double Bias(GameObject o) => BiasText(o.Name.Resolve(World?.Resources.Talk) + " " + o.Tag);

    double BiasText(string text)
    {
        if (_keywords.Count == 0) return 0;
        foreach (var k in _keywords)
            if (text.Contains(k, StringComparison.OrdinalIgnoreCase)) return 40;
        return 0;
    }

    static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "this", "that", "there", "their", "they", "them", "then", "than", "with", "from", "have", "your", "what", "when", "where",
        "which", "will", "would", "should", "could", "about", "into", "some", "after", "before", "been", "were", "must", "also", "once",
        "speak", "talk", "find", "return", "told", "said", "asked", "perhaps", "however", "these", "those", "other", "only", "just",
    };

    /// <summary>Names in a quest's text: capitalised words that aren't sentence starters or filler.</summary>
    static IEnumerable<string> Keywords(string text)
    {
        var words = System.Text.RegularExpressions.Regex.Matches(UI.Kit.NwnText.Strip(text), @"\b[A-Z][a-zA-Z'\-]{3,}\b").Select(m => m.Value.TrimEnd('\'', 's'));
        return words.Where(w => w.Length >= 4 && !StopWords.Contains(w)).Distinct(StringComparer.OrdinalIgnoreCase).Take(16);
    }

    /// <summary>Carries out a goal through the client's input paths and watches it finish (or not).</summary>
    async Task Pursue(Goal goal)
    {
        var target = goal.Target;
        _attempts[target.Id] = _attempts.GetValueOrDefault(target.Id) + 1;
        int areaSeq = _areaSeq, dialogSeq = _dialogSeq;
        string name = Name(target);
        _log.Add("bot", $"{goal.Kind} {name} ({goal.Why}, score {goal.Score:0})");
        switch (goal.Kind)
        {
            case GoalKind.Fight:
                await FightGoal((Creature)target);
                return;
            case GoalKind.PickUp:
                Host.Session.Request("pickup", new { item = target.Id });
                break;
            case GoalKind.Talk:
                if (!await Click(target)) Host.Session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = target.Id }.Write());
                break;
            default:
                if (!await Click(target)) Host.Session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = target.Id }.Write());
                break;
        }
        var outcome = await Follow(goal, areaSeq, dialogSeq);
        _wantsToMoveSince = null;
        if (_area != null)
        {
            if (goal.Kind == GoalKind.Talk && outcome != Outcome.Unreachable) _area.Talked++;
            if (goal.Kind is GoalKind.Use or GoalKind.Loot or GoalKind.Door or GoalKind.PickUp && outcome != Outcome.Unreachable) _area.Used++;
        }
        switch (outcome)
        {
            case Outcome.Unreachable when goal.Kind != GoalKind.Door && _doorRetries.GetValueOrDefault(target.Id) < 2 && Me is { Area: { } here } stalled
                && here.Objects.OfType<Door>().Where(d => !d.IsOpen && (!d.Locked || CanPick(d)) && !d.IsTransition && Vector3.Distance(d.Position, stalled.Position) < 5f)
                    .OrderBy(d => Vector3.Distance(d.Position, stalled.Position)).FirstOrDefault() is { } inTheWay:
                // Stopped at a closed door (doors close again behind you): open it and try once more.
                _log.Add("bot", $"opening {Name(inTheWay)} on the way to {name}");
                _report.Count("doors opened on the way");
                if (inTheWay.Locked) await PickLock(inTheWay);
                Host.Session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = inTheWay.Id }.Write());
                await Wait(2.5);
                _doorRetries[target.Id] = _doorRetries.GetValueOrDefault(target.Id) + 1;
                _attempts[target.Id] = Math.Max(0, _attempts.GetValueOrDefault(target.Id) - 1);
                break;
            case Outcome.Unreachable:
                _unreachable.Add(target.Id);
                _report.Count("unreachable");
                _log.Add("bot", $"could not reach {name}");
                break;
            case Outcome.AreaChanged when goal.Kind == GoalKind.Exit:
                if (_area != null) _area.Transitions++;
                break;
            case Outcome.Done when goal.Kind is GoalKind.Exit or GoalKind.Door or GoalKind.Loot && target is Door { Locked: true } or Placeable { Locked: true }:
                // Locked: a rogue picks it (Open Lock, from the radial's skills); otherwise come back when something changes.
                if (await PickLock(target))
                {
                    Host.Session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = target.Id }.Write());
                    await Wait(2);
                    break;
                }
                _locked.Add(target.Id);
                _lockedTimes[target.Id] = _lockedTimes.GetValueOrDefault(target.Id) + 1;
                _attempts.Remove(target.Id);
                _report.Count("locked");
                _log.Add("bot", $"{name} is locked");
                break;
            case Outcome.Done when goal.Kind == GoalKind.Exit:
                // A transition that went nowhere: doors open on the first click, so try again; the third time it is a bug.
                if (_attempts.GetValueOrDefault(target.Id) >= 3)
                    await Bug("transition", $"exit {name} in {AreaName} does nothing", $"Used it 3 times ({goal.Why}); still in {AreaName}.", shot: true, key: "transition:" + target.Tag + target.Id);
                break;
            case Outcome.Done when goal.Kind is GoalKind.Talk:
                if (_dialogSeq == dialogSeq) _done.Add(target.Id); // nothing to say (or a bark)
                break;
            case Outcome.Done when goal.Kind == GoalKind.Door:
                // An opened door can make what was out of reach reachable.
                _done.Add(target.Id);
                _unreachable.Clear();
                break;
            case Outcome.Done:
                _done.Add(target.Id);
                break;
        }
        if (goal.Kind == GoalKind.Exit && outcome == Outcome.AreaChanged)
        {
            string key = (World?.Areas.FirstOrDefault(a => a.ResRef == _previousArea)?.ResRef ?? "") + ">" + _areaResRef;
            _exitsTaken[key] = _exitsTaken.GetValueOrDefault(key) + 1;
        }
    }
    string _previousArea = "";

    enum Outcome { Done, DialogOpened, AreaChanged, Unreachable, TimedOut }

    /// <summary>Waits for the server to carry out what was asked: a conversation, a new area, or the action queue running dry.</summary>
    async Task<Outcome> Follow(Goal goal, int areaSeq, int dialogSeq)
    {
        string fromArea = _areaResRef;
        _wantsToMoveSince = null;
        double start = Now, lastMove = Now;
        var lastPos = Me?.Position ?? Vector3.Zero;
        var startPos = lastPos;
        double idleSince = -1;
        while (Now - start < 60)
        {
            await Wait(0.25);
            await Watch();
            if (_areaSeq != areaSeq || Host.Loading()) { _previousArea = fromArea; return Outcome.AreaChanged; }
            if (_dialogSeq != dialogSeq && _dialog != null) return Outcome.DialogOpened;
            if (Me is not { } me) return Outcome.Done;
            if (Vector3.Distance(me.Position, lastPos) > 0.3f) { lastPos = me.Position; lastMove = Now; }
            bool moving = me.Actions.Count > 0 && me.Actions[0] is MoveAction;
            if (moving) _wantsToMoveSince ??= Now;
            // Pressed against something for 4 s: give up rather than walk on the spot.
            if (moving && Now - lastMove > 4)
            {
                _log.Add("bot", $"no progress for 4 s at {me.Position.X:0.0},{me.Position.Y:0.0} (moved {Vector3.Distance(me.Position, startPos):0.0} m since the order; speed {me.Speed:0.00}, mode {me.Mode}, {Vector3.Distance(me.Position, goal.Target.Position):0.0} m from the target)");
                Stop();
                return Outcome.Unreachable;
            }
            if (me.Actions.Count == 0)
            {
                if (idleSince < 0) idleSince = Now;
                // Give scripts a moment to answer (conversations start a tick or two after arriving).
                if (Now - idleSince > (goal.Kind is GoalKind.Talk or GoalKind.Exit ? 2.5 : 1.2))
                {
                    _wantsToMoveSince = null;
                    if (Now - start < 1.0) continue;
                    // Never got there: the move gave up far from the target.
                    float reach = goal.Kind == GoalKind.Exit ? 6f : 4f;
                    if (goal.Target is not Trigger && Vector3.Distance(me.Position, goal.Target.Position) > reach && goal.Kind != GoalKind.PickUp) return Outcome.Unreachable;
                    return Outcome.Done;
                }
            }
            else idleSince = -1;
        }
        Stop();
        _wantsToMoveSince = null;
        return Outcome.TimedOut;
    }
    double? _wantsToMoveSince;

    void Stop() => Host.Session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = Me?.Position ?? Vector3.Zero, Run = true }.Write());

    async Task FightGoal(Creature enemy)
    {
        if (_area != null) _area.Fought++;
        _report.Count("fights");
        _report.Event(Now, "fight", $"attacking {Name(enemy)}");
        Host.Session.Request("attack", new { target = enemy.Id });
        double start = Now, lastHpChange = Now, closer = Now;
        float closest = float.MaxValue;
        int hp = enemy.CurrentHitPoints, areaSeq = _areaSeq;
        bool reported = false;
        while (!enemy.IsDead && !enemy.IsDestroyed && enemy.Area == Me?.Area && _areaSeq == areaSeq && Now - start < 150)
        {
            await Wait(0.5);
            await Watch();
            if (_dialog != null) return; // surrender, a script took over
            if (Me is not { } me || me.IsDead || World is not { } world) return;
            if (!world.Factions.IsEnemy(enemy, me)) { _report.Event(Now, "fight", $"{Name(enemy)} is no longer hostile"); return; }
            if (enemy.CurrentHitPoints != hp) { hp = enemy.CurrentHitPoints; lastHpChange = Now; }
            // Re-issue the attack if our queue ran dry (moved off by a script, target ran).
            if (me.Actions.Count == 0 && Now - start > 3) Host.Session.Request("attack", new { target = enemy.Id });
            // Can't get to it (a cage, across a chasm): give up quietly.
            float gap = Vector3.Distance(me.Position, enemy.Position);
            if (gap < closest - 0.5f) { closest = gap; closer = Now; }
            if (gap > 5 && Now - closer > 15)
            {
                _log.Add("bot", $"can't get at {Name(enemy)} ({gap:0.0} m away)");
                _unreachable.Add(enemy.Id);
                Stop();
                return;
            }
            if (!reported && Now - lastHpChange > 60 && gap < 5)
            {
                reported = true;
                await Bug("combat", $"fight with {Name(enemy)} never resolves", $"No hit point change for 60 s (hp {enemy.CurrentHitPoints}/{enemy.MaxHitPoints}, plot {enemy.Plot}, immortal {enemy.IsImmortal}, " +
                    $"our actions [{string.Join(", ", me.Actions.Select(a => a.GetType().Name))}], distance {Vector3.Distance(me.Position, enemy.Position):0.0} m).", shot: true, key: "combat:" + enemy.Tag);
                _unreachable.Add(enemy.Id);
                Stop();
                return;
            }
        }
        if (enemy.IsDead) { _report.Count("kills"); _report.Event(Now, "fight", $"{Name(enemy)} died after {Now - start:0} s"); }
    }

    /// <summary>When there's nothing obvious left: walk somewhere not yet seen.</summary>
    async Task Wander(Creature me)
    {
        if (me.Area is not { } area) return;
        _report.Count("wanders");
        if (++_wanders % 10 == 0 && _wanders >= 30 && !_deadEndReported.Contains(area.ResRef))
        {
            _deadEndReported.Add(area.ResRef);
            await Bug("dead-end", $"nothing left to do in {AreaName}", "Every reachable object has been tried and no usable exit remains.", shot: true, key: "deadend:" + area.ResRef);
        }
        var tris = area.Walkmesh.Triangles.Where(t => t.Walkable).ToList();
        if (tris.Count == 0) { await Wait(2); return; }
        // A point the player can actually walk to (walkmesh islands behind walls would only push it into them).
        Vector3? pick = null;
        for (int i = 0; i < 12 && pick == null; i++)
        {
            var t = tris[_rng.Next(tris.Count)];
            var c = (t.A + t.B + t.C) / 3;
            if (area.Pathfinder.FindPath(me.Position, c, 20000) is { Count: > 0 } path && Vector3.Distance(path[^1], c) < 1f) pick = c;
        }
        if (pick is not { } p) { await Wait(2); return; }
        Host.Session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = p, Run = true }.Write());
        int areaSeq = _areaSeq, dialogSeq = _dialogSeq;
        await Follow(new Goal(GoalKind.Use, me, 0, "wander"), areaSeq, dialogSeq);
        // Coming back to an area later: the people there may have new things to say.
        if (_wanders % 6 == 0) Changed();
    }
    int _wanders;
    readonly HashSet<string> _deadEndReported = new();

    /// <summary>
    /// Clicks an object the way the player would: a left click at its place on screen, through the GUI and the
    /// world picker. True when the server got the right request from it (the walk-up or use is queued); false when
    /// it's off screen, under a window, or the picker chose something else (the caller then asks directly).
    /// </summary>
    async Task<bool> Click(GameObject target)
    {
        if (!Host.Session.Objects.TryGetValue(target.Id, out var view) || !view.Visible || Me is not { } me) return false;
        var cam = Host.Camera;
        float height = target is Creature ? 1.1f : 0.6f;
        var world = view.GlobalPosition + GVector3.Up * height;
        if (cam.IsPositionBehind(world)) return false;
        var at = cam.UnprojectPosition(world);
        var size = GetViewport().GetVisibleRect().Size;
        if (at.X < 20 || at.Y < 20 || at.X > size.X - 20 || at.Y > size.Y - 20) { _report.Count("clicks off screen"); return false; }
        int dialogSeq = _dialogSeq, areaSeq = _areaSeq;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
        // The request reaches the server on its next tick.
        await Wait(0.15);
        bool queued = me.Actions.Any(x => x is MoveAction mv && mv.TargetObject == target.Id || x is Game.Conversations.InteractAction ia && ia.Target == target.Id
            || x is Game.WorldObjects.TransitionAction);
        if (queued || _dialogSeq != dialogSeq || _areaSeq != areaSeq) { _report.Count("mouse clicks"); return true; }
        bool walking = me.Actions.Any(x => x is MoveAction { TargetObject: GameObject.InvalidId });
        _report.Count(walking ? "clicks picked the ground" : "clicks swallowed (UI or wrong object)");
        _log.Add("bot", $"click on {Name(target)} at {at} did not reach it ({(walking ? "walked to the ground" : "nothing queued")}; hovered control {GetViewport().GuiGetHoveredControl()?.Name})");
        return false;
    }

    bool CanPick(GameObject o) => Me is { } me && World is { } w && o is Door { KeyRequired: 0 } or Placeable { KeyRequired: 0 }
        && Game.Rules.CreatureRules.HasSkill(w, me, Game.Skills.SkillIds.OpenLock) && _picks.GetValueOrDefault(o.Id) < 3;
    readonly Dictionary<uint, int> _picks = new();

    /// <summary>Open Lock on a locked door or chest, the way the radial menu's skill wedge asks for it; true once it is unlocked.</summary>
    async Task<bool> PickLock(GameObject o)
    {
        if (!CanPick(o)) return false;
        _picks[o.Id] = _picks.GetValueOrDefault(o.Id) + 1;
        _log.Add("bot", $"picking the lock of {Name(o)}");
        _report.Count("locks picked (tries)");
        Host.Session.Request("use-skill", new Dictionary<string, object> { ["skill"] = Game.Skills.SkillIds.OpenLock, ["sub"] = 0, ["target"] = (long)o.Id });
        for (int i = 0; i < 40 && StillLocked(o); i++) await Wait(0.25);
        bool open = o is Door { Locked: false } or Placeable { Locked: false };
        _log.Add("bot", open ? $"unlocked {Name(o)}" : $"could not pick {Name(o)}");
        if (open) _report.Count("locks picked");
        return open;
    }

    static bool StillLocked(GameObject o) => o is Door { Locked: true } or Placeable { Locked: true } || o is Door { IsOpen: false } d && d.Script("OnClick").Length > 0;

    /// <summary>The story moved (a quest update, something new said): people may have more to say and locked things may be open.</summary>
    void Changed()
    {
        _done.Clear();
        _locked.Clear();
        foreach (var id in _attempts.Keys.ToList())
            if (World?.Get(id) is Door or Trigger or Placeable { Locked: true } or Creature) _attempts.Remove(id);
    }

    // ------------------------------------------------------------------ helpers

    void Chat(string text) => Host.Session.Send(new ChatSendMessage { Text = text }.Write());

    string Name(GameObject o)
    {
        var n = UI.Kit.NwnText.Strip(o.Name.Resolve(World?.Resources.Talk));
        return n.Length > 0 ? $"{n} [{o.Tag}]" : $"[{o.Tag}]";
    }

    static string Short(string s, int n) { s = s.Replace('\n', ' ').Trim(); return s.Length > n ? s[..(n - 1)] + "…" : s; }
    static string Safe(string s) => string.Concat(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

    async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds, processAlways: true), SceneTreeTimer.SignalName.Timeout);

    /// <summary>Saves a screenshot (JPEG) under shots/ and returns its path relative to the report; null past the cap.</summary>
    async Task<string?> Shot(string name)
    {
        if (_shots >= MaxShots) return null;
        _shots++;
        _skipFrames = 3;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        string rel = $"shots/{_shots:000}_{Safe(name)}.jpg";
        img.SaveJpg(Path.Combine(OutDir, rel), 0.85f);
        _skipFrames = 3;
        return rel;
    }

    void SaveReport()
    {
        _report.Seconds = Math.Round(Now, 1);
        if (_area != null) _area.Seconds = Math.Round(_area.Seconds + Now - _areaClock, 1);
        _areaClock = Now;
        try { _report.Write(OutDir); }
        catch (IOException e) { GD.PrintErr("[playtest] report: " + e.Message); }
    }

    async Task Finish(string outcome)
    {
        if (_finished) return;
        // Where the story stopped moving: no new quest step or area for the last stretch of the run.
        double lastProgress = _report.Timeline.Where(e => e.Kind is "quest" or "area" && !e.Text.Contains("first visit") || e.Kind == "area" && e.Text.Contains("first visit")).Select(e => e.Time).DefaultIfEmpty(0).Max();
        if (Now - lastProgress > 300)
        {
            var quest = _report.Quests.OrderByDescending(q => q.LastTime).FirstOrDefault();
            await Bug("progress", $"no progress for {(Now - lastProgress) / 60:0} minutes in {AreaName}",
                $"Last new quest step or area at {lastProgress / 60:0.0} min. Latest quest: {(quest == null ? "none" : $"{quest.Name} ({quest.Tag} {quest.States.LastOrDefault()}): {quest.LastText}")}", shot: true, key: "progress");
        }
        _finished = true;
        _report.Outcome = outcome;
        _report.Event(Now, "end", outcome);
        await Shot("end");
        SaveReport();
        GD.Print($"[playtest] done: {outcome}; {_report.Bugs.Count} distinct bugs; report {Path.GetFullPath(Path.Combine(OutDir, "report.md"))}");
        _log.Close();
        Host.Quit();
    }
}
