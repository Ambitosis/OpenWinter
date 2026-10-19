using Vector3 = System.Numerics.Vector3;
using System.Text.RegularExpressions;
using OpenWinter.Client.UI;
using Godot;

namespace OpenWinter.Client.Playtest;

/// <summary>Watchdogs: things a playtester would write up (stuck screens, errors, missing content, windows left open, hitches).</summary>
public partial class Autopilot
{
    double _loadingSince = -1, _noControlSince = -1, _blackSince = -1, _lastMoved;
    double _dialogMismatchSince = -1, _lastLumaAt;
    Vector3 _lastPos;
    float _luma = 1;
    int _skipFrames;
    double _sinceAreaLoad;
    readonly HashSet<string> _bugKeys = new();

    /// <summary>Frame-time hitches (outside loading).</summary>
    public override void _Process(double delta)
    {
        if (_finished) return;
        if (_skipFrames > 0) { _skipFrames--; return; }
        if (Host.Loading() || Now - _areaSince < 4 || Host.Session.Controlled == null) return;
        if (delta > 0.25)
        {
            _report.Count("frame spikes");
            string key = "spike:" + _areaResRef;
            string text = $"{delta * 1000:0} ms frame";
            if (_bugKeys.Contains(key)) { Recount(key, text); return; }
            _ = Bug("frame-spike", $"hitches in {AreaName}", $"{text} (first seen; later ones in this area are counted).", shot: false, key: key);
        }
    }

    void Recount(string key, string detail)
    {
        var b = _report.Bugs.FirstOrDefault(x => x.Key == key);
        if (b == null) return;
        b.Count++;
        if (b.Kind == "frame-spike") b.Detail = $"{b.Count} frames over 250 ms; latest {detail}.";
    }

    /// <summary>Records a bug (or counts a repeat of one already recorded), with a screenshot and the recent log.</summary>
    async Task Bug(string kind, string title, string detail, bool shot, string? key = null)
    {
        key ??= kind + ":" + Normalise(title);
        if (!_bugKeys.Add(key)) { Recount(key, ""); return; }
        var bug = new PlaytestBug { Kind = kind, Key = key, Title = title, Detail = detail, Area = AreaName, Time = Math.Round(Now, 1), Log = _log.Tail(20) };
        _report.Bugs.Add(bug);
        _report.Event(Now, "bug", $"[{kind}] {title}");
        GD.Print($"[playtest] bug [{kind}] {title}");
        if (shot && _report.Bugs.Count(b => b.Screenshot != null) < 120) bug.Screenshot = await Shot("bug_" + kind);
    }

    static readonly Regex Digits = new(@"\b(0x)?[0-9A-Fa-f]{4,}\b|\d+", RegexOptions.Compiled);
    static string Normalise(string s) => Digits.Replace(s, "#").Trim();

    /// <summary>Runs every step: looks at the log, the screen and the game state for trouble.</summary>
    async Task Watch()
    {
        foreach (var line in _log.Drain().ToList()) await Classify(line);

        // Loading screen that never finishes.
        if (Host.Loading()) { if (_loadingSince < 0) _loadingSince = Now; }
        else _loadingSince = -1;
        if (_loadingSince >= 0 && Now - _loadingSince > 20)
            await Bug("loading", "loading screen stays up", $"Visible for more than 20 s (area {_areaResRef}).", shot: true, key: "loading:" + _areaResRef);

        // No creature to control (after we got one).
        if (Host.Session.Controlled == null && !Host.Loading() && _report.Areas.Count > 0) { if (_noControlSince < 0) _noControlSince = Now; }
        else _noControlSince = -1;
        if (_noControlSince >= 0 && Now - _noControlSince > 15)
            await Bug("no-control", "no controlled creature", "The client has had no creature to control for 15 s.", shot: true, key: "nocontrol:" + _areaResRef);

        // Cutscene mode that never ends.
        if (_cutscene && Now - _cutsceneSince > 90)
            await Bug("cutscene", $"cutscene from {_cutsceneArea} lasts over 90 s", $"Cutscene mode since {_cutsceneSince:0} s (started in {_cutsceneArea}, now in {AreaName}); dialog open: {_dialog != null}. Long intros can be legitimate; check the timeline.", shot: true, key: "cutscene:" + (int)_cutsceneSince);

        // A black screen: a fade that's never lifted, or nothing drawn.
        if (Now - _lastLumaAt > 2 && !Host.Loading() && Host.Session.Controlled != null)
        {
            _lastLumaAt = Now;
            _luma = await Luma();
        }
        // Under 0.5% of the middle of the screen lit: black (a dark dungeon still has torches and effects).
        bool dark = (_luma < 0.005f || _fadedBlack) && !Host.Loading();
        if (dark) { if (_blackSince < 0) _blackSince = Now; }
        else _blackSince = -1;
        if (_blackSince >= 0 && Now - _blackSince > 10)
            await Bug("black-screen", $"screen black in {AreaName}", $"Screen {(_fadedBlack ? "faded to black by a script" : $"black ({_luma * 100:0.00}% of the middle of the screen lit)")} for over 10 s; cutscene mode {_cutscene}, dialog open {_dialog != null}.",
                shot: true, key: "black:" + _areaResRef + ":" + (int)(_blackSince / 60));

        // Stuck: trying to move but going nowhere.
        if (Me is { } me)
        {
            if (Vector3.Distance(me.Position, _lastPos) > 0.5f) { _lastPos = me.Position; _lastMoved = Now; }
            if (_wantsToMoveSince is { } since && _dialog == null && !_cutscene && Now - since > 60 && Now - _lastMoved > 60)
            {
                _wantsToMoveSince = Now; // at most once a minute
                await Bug("stuck", $"stuck in {AreaName}", $"Trying to move for 60 s without getting anywhere (at {me.Position.X:0.0}, {me.Position.Y:0.0}).", shot: true, key: "stuck:" + _areaResRef + ":" + (int)me.Position.X + ":" + (int)me.Position.Y);
            }

            // The dialogue window and the server disagree about a conversation.
            var session = World?.System<Game.Conversations.ConversationService>().SessionOf(me);
            bool shown = Host.Dialog.Visible;
            if (shown != (session != null)) { if (_dialogMismatchSince < 0) _dialogMismatchSince = Now; }
            else _dialogMismatchSince = -1;
            if (_dialogMismatchSince >= 0 && Now - _dialogMismatchSince > 6)
            {
                await Bug("ui", shown ? "dialogue window left open after the conversation ended" : "conversation running but no dialogue window",
                    shown ? "The server has no conversation for the player, but the window is still showing." : $"The server is in conversation {session!.ResRef} (entry {session.Entry}, paused {session.Paused}) but the window is hidden.",
                    shot: true, key: "dialogmismatch:" + shown + ":" + _areaResRef);
                _dialogMismatchSince = -1;
                if (!shown && session != null && _dialog == null) _report.Count("hidden conversations");
            }
        }

        // Windows that overlap each other.
        var windows = WindowStack.Windows.Where(w => GodotObject.IsInstanceValid(w) && w.Visible && w.IsVisibleInTree()).ToList();
        for (int i = 0; i < windows.Count; i++)
            for (int j = i + 1; j < windows.Count; j++)
            {
                var a = windows[i].GetGlobalRect();
                var b = windows[j].GetGlobalRect();
                var inter = a.Intersection(b);
                if (inter.Area > 0.3f * Math.Min(a.Area, b.Area))
                    await Bug("ui", $"{windows[i].GetType().Name} and {windows[j].GetType().Name} overlap", $"{a} and {b}", shot: true, key: "overlap:" + windows[i].GetType().Name + windows[j].GetType().Name);
            }
    }

    static readonly Regex NotImplemented = new(@"not implemented: (\w+)", RegexOptions.Compiled);

    async Task Classify(LogLine l)
    {
        string t = l.Text;
        if (t.Contains("[script error]"))
            await Bug("script-error", Short(Normalise(t.Replace("[script error]", "").Trim()), 160), t, shot: false, key: "script:" + Normalise(t));
        else if (NotImplemented.Match(t) is { Success: true } ni)
            await Bug("unimplemented", "engine function not implemented: " + ni.Groups[1].Value, t, shot: false, key: "ni:" + ni.Groups[1].Value);
        else if (t.Contains("[content]"))
            await Bug("content", Short(t[(t.IndexOf("[content]") + 9)..].Trim(), 160), t, shot: false, key: "content:" + t);
        else if (t.Contains("StartNewModule("))
        {
            await Bug("module-switch", "the module asked to start the next module", t, shot: true, key: "startnewmodule");
            _report.Event(Now, "end", "module finished (StartNewModule)");
            _ = FinishSoon("module finished: " + t.Trim());
        }
        else if (l.Error && !t.StartsWith("[playtest]"))
        {
            bool exception = t.Contains("Exception") || t.Contains("   at ");
            await Bug(exception ? "exception" : "godot-error", Short(Normalise(t), 160), t, shot: exception, key: "err:" + Short(Normalise(t), 120));
        }
        else if (l.Warning && !t.Contains("[content]"))
            await Bug("godot-warning", Short(Normalise(t), 160), t, shot: false, key: "warn:" + Short(Normalise(t), 120));
        else if (l.Source == "console" && t.StartsWith("[server]") && (t.Contains("failed") || t.Contains("error", StringComparison.OrdinalIgnoreCase)))
            await Bug("server", Short(Normalise(t), 160), t, shot: false, key: "server:" + Short(Normalise(t), 120));
    }

    async Task FinishSoon(string outcome)
    {
        await Wait(8);
        await Finish(outcome);
    }

    /// <summary>The share of the middle of the screen (the HUD stays out of it) that is lit at all, from a small copy of the frame.</summary>
    async Task<float> Luma()
    {
        _skipFrames = 3;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = GetViewport().GetTexture().GetImage();
        _skipFrames = 3;
        img.Resize(80, 45, Image.Interpolation.Bilinear);
        int lit = 0, total = 0;
        for (int y = 9; y < 36; y++)
            for (int x = 16; x < 64; x++)
            {
                var c = img.GetPixel(x, y);
                if (0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B > 0.05f) lit++;
                total++;
            }
        return lit / (float)total;
    }
}
