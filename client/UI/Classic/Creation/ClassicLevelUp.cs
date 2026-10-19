using System.Text.Json;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.Progression;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// The level-up window in the original's look and order (measured on the original, shots l00-l27): the
/// class list (classes that can be taken by name, then the rest greyed; the class of the last level chosen), then
/// the ability increase every fourth level, skills, feats, domains or school, spells, the familiar or companion again
/// for the classes that grant it, and the Level Up Summary (class, hit points, spells added and removed, skills,
/// feats). OK goes on, Cancel on any screen leaves the level-up. Single player pauses while it is open ("PAUSED").
/// Screen titles carry the character's name and classes (l04: "Lomothrade Nulebin   Wizard (2)   Allocate Skill
/// Points"). The server plans, checks and applies the level ("levelup-data" / "levelup-plan" / "levelup-apply").
/// </summary>
public partial class ClassicLevelUp : CgFlow
{
    LevelChoice _choice = new();
    LevelPlanView? _plan;
    JsonElement? _summary;
    string? _pendingAuto;
    Action? _afterPlan;
    bool _waitingSummary;
    public bool SinglePlayer;

    public static ClassicLevelUp? Current { get; private set; }
    bool _paused;
    int _pauseState;

    protected override LevelChoice Choice => _choice;
    protected override LevelPlanView? Plan => _plan;
    protected override int AssociateLevel => Math.Max(1, _plan?.NewClassLevel ?? 1);
    protected override int[] AbilitiesAfter => _plan?.Abilities is { Length: 6 } a ? a.Select((v, i) => v - RacialAdjustments[i]).ToArray() : AbilitiesBefore;
    protected override int[] AbilitiesBefore => Ints("baseAbilities") is { Length: 6 } a ? a : new int[6];
    protected override int[] RacialAdjustments => Model?.Races.GetValueOrDefault(Int("race"))?.Ints("adjust") is { Length: 6 } a ? a : new int[6];

    int Int(string name, int fallback = -1) => _summary is { } s && s.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
    int[]? Ints(string name) => _summary is { } s && s.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetInt32()).ToArray() : null;
    string Str(string name) => _summary is { } s && s.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    public static ClassicLevelUp Create(Gameplay.ClientSession session, bool singlePlayer) => new() { Session = session, SinglePlayer = singlePlayer, Name = "ClassicLevelUp" };

    public override void _Ready()
    {
        Current = this;
        base._Ready();
        TreeExiting += () => { if (Current == this) Current = null; };
    }

    public void Open()
    {
        if (Visible) return;
        Visible = true;
        GetParent()?.MoveChild(this, -1);
        Gameplay.PlayerInput.Blocked = true;
        _choice = new LevelChoice();
        _plan = null;
        _summary = null;
        _pendingAuto = null;
        _afterPlan = null;
        if (Model == null) Session.Request("chargen-data");
        Session.Request("levelup-data");
        // Single player pauses while the window is open (the server keeps answering): the original writes "PAUSED"
        // and "UNPAUSED" in red (l00, l10). A pause the player made stays.
        _paused = SinglePlayer && (_pauseState & 2) == 0;
        if (_paused) Session.Request("pause", new { state = 1 });
    }

    public void Close()
    {
        if (!Visible) return;
        Visible = false;
        ClosePopup();
        if (Screen != null) { RemoveChild(Screen); Screen.QueueFree(); Screen = null; }
        Gameplay.PlayerInput.Blocked = false;
        if (_paused) Session.Request("pause", new { state = 0 });
        _paused = false;
    }

    protected override void Validate() => Session.Request("levelup-data", new { choice = _choice.ToJson(), auto = _pendingAuto != null });

    protected override void Recommend(string part)
    {
        _pendingAuto = part;
        if (part is "skills" or "all") _choice.Skills.Clear();
        if (part is "feats" or "all") _choice.Feats.Clear();
        if (part is "spells" or "all") _choice.Spells.Clear();
        Validate();
    }

    protected override void OnModel()
    {
        if (Visible && _summary != null && Screen == null) OpenClass();
    }

    protected override void OnNotify(NotifyMessage m)
    {
        base.OnNotify(m);
        if (m.Kind == "pause")
        {
            using var doc = JsonDocument.Parse(m.Json);
            _pauseState = doc.RootElement.GetProperty("state").GetInt32();
        }
        if (!Visible) return;
        switch (m.Kind)
        {
            case "levelup-data":
                _summary = JsonDocument.Parse(m.Json).RootElement.Clone();
                if (Model != null && Screen == null) OpenClass();
                break;
            case "levelup-plan":
            {
                using var doc = JsonDocument.Parse(m.Json);
                _plan = LevelPlanView.Parse(doc.RootElement);
                if (_plan.Auto is { } auto && _pendingAuto != null)
                {
                    var c = LevelChoice.FromJson(auto);
                    string part = _pendingAuto;
                    _pendingAuto = null;
                    if (part is "skills" or "all") _choice.Skills = c.Skills;
                    if (part is "feats" or "all") _choice.Feats = c.Feats;
                    if (part is "spells" or "all") _choice.Spells = c.Spells;
                    if (part is "school" or "all") _choice.School = c.School;
                    if (part is "domains" or "all") { _choice.Domain1 = c.Domain1; _choice.Domain2 = c.Domain2; }
                    if (part is "ability" or "all") _choice.Ability = c.Ability;
                    Validate();
                }
                var then = _afterPlan;
                _afterPlan = null;
                then?.Invoke();
                RefreshScreen?.Invoke();
                break;
            }
            case "levelup-summary" when _waitingSummary:
                _waitingSummary = false;
                ShowSummary(JsonDocument.Parse(m.Json).RootElement.Clone());
                break;
            case "levelup-result":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var r = doc.RootElement;
                if (r.GetProperty("ok").GetBoolean()) Close();
                else ClassicPopup.Ok(this, A, string.Join("\n", r.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("message").GetString() ?? "").Take(4)));
                break;
            }
        }
    }

    // ------------------------------------------------------------------ titles

    /// <summary>"Name   Class (level) ...   Step" (l04, l13: the chosen class's new level counted).</summary>
    protected override string Title(string title)
    {
        if (Model == null) return title;
        var parts = new List<string>();
        var levels = _summary is { } s && s.TryGetProperty("classLevels", out var cl) ? cl.EnumerateArray().Select(c => (Id: c.GetProperty("id").GetInt32(), Level: c.GetProperty("level").GetInt32())).ToList() : new();
        bool found = false;
        foreach (var (id, level) in levels)
        {
            int l = level + (id == _choice.Class ? 1 : 0);
            found |= id == _choice.Class;
            parts.Add($"{Model.ClassName(id)} ({l})");
        }
        if (!found && _choice.Class >= 0) parts.Add($"{Model.ClassName(_choice.Class)} (1)");
        var font = A.Font(CgKit.Font);
        float space = Math.Max(1, font.Measure(" "));
        string Gap(float px) => new string(' ', Math.Max(1, (int)MathF.Round(px / space)));
        return Str("name") + Gap(25) + string.Join(" ", parts) + Gap(65) + title;
    }

    // ------------------------------------------------------------------ class

    void OpenClass()
    {
        if (ShowScreen("cg_class") is not { } p || Model == null || _summary is not { } sum) return;
        var rows = sum.GetProperty("classes").EnumerateArray()
            .Select(c => (Id: c.GetProperty("id").GetInt32(), Available: c.GetProperty("available").GetBoolean()))
            .Where(c => Model.Classes.ContainsKey(c.Id))
            .OrderBy(c => c.Available ? 0 : 1).ThenBy(c => Model.ClassName(c.Id), StringComparer.CurrentCultureIgnoreCase).ToList();
        int selected = Int("lastClass");
        if (!rows.Any(r => r.Id == selected)) selected = rows.FirstOrDefault(r => r.Available).Id;
        bool Available(int cls) => rows.Any(r => r.Id == cls && r.Available);
        var list = CgList.Attach(p, "ClassListBox");
        var buttons = new Dictionary<int, CgButton>();
        void Select(int cls)
        {
            selected = cls;
            if (Model.Classes.GetValueOrDefault(cls) is { } c) SetHelp(c.Name, c.Description);
            foreach (var (id, b) in buttons) b.Selected = id == cls;
            Enable("OkButton", Available(cls));
        }
        foreach (var (id, available) in rows)
        {
            int cls = id;
            var c = Model.Classes[id];
            var row = CgButton.Make(A, "ctl_cg_btn_class", c.Name, ChargenModel.Icon(A.Content, c.Icon));
            row.Greyed = !available;
            row.Pressed += () => Select(cls);
            buttons[id] = row;
            list?.Add(row);
        }
        // Recommended keeps the class of the last level (l21).
        On("RecommendButton", () => { int last = Int("lastClass"); if (Available(last)) Select(last); });
        On("OkButton", () =>
        {
            if (!Available(selected)) return;
            if (_choice.Class != selected) _choice = new LevelChoice { Class = selected };
            _afterPlan = () => GoStep(LevelStep.Ability);
            Validate();
        });
        On("CancelButton", Close);
        if (!Model.Recommended && Btn("RecommendButton") is { } r) r.Visible = false;
        Select(selected);
        // The list opens with the selected class on its fifth row (l00: Wizard under Paladin, Ranger, Rogue, Sorcerer).
        list?.ScrollTo(Math.Max(0, rows.FindIndex(r => r.Id == selected) - 4));
    }

    // ------------------------------------------------------------------ steps and summary

    protected override void LevelStepsCancelled() => Close();

    protected override void LevelStepsDone()
    {
        _waitingSummary = true;
        Session.Request("levelup-data", new { choice = _choice.ToJson(), summary = true });
    }

    protected override int CurrentRanks(int skill) => Ints("ranks") is { } r && skill < r.Length ? r[skill] : 0;

    protected override int PackageSchool() => 0;

    protected override int DefaultAssociate(bool companion) => Math.Max(0, Int(companion ? "companion" : "familiar", 0));

    protected override string CurrentAssociateName(bool companion) => Str(companion ? "companionName" : "familiarName");

    /// <summary>
    /// The Level Up Summary (cg_lvlup_summary on pnl_lvlup_sum, centred): "Class : Wizard", the hit points gained, for a
    /// class that learns spells "Spells (Added) :" and "Spells (Removed) :", then "Skills :" (each raised skill with its
    /// added ranks) and "Feats :", "(none)" for an empty list (l08, l19). OK takes the level.
    /// </summary>
    void ShowSummary(JsonElement s)
    {
        if (Model == null) return;
        var p = ShowPopup("cg_lvlup_summary");
        if (p == null) return;
        // The summary stands alone: the level screen behind it goes (l08, l19).
        if (Screen != null) Screen.Visible = false;
        var lines = new List<string> { $"{A.Tlk(6388)} : {Model.ClassName(_choice.Class)}", "", $"{A.Tlk(6389)} : {Signed(s.GetProperty("hitPoints").GetInt32())}", "" };
        string none = A.Tlk(68687);
        if (_plan?.HasSpells == true)
        {
            lines.Add($"{A.Tlk(6390)} :");
            var added = _choice.Spells.SelectMany(k => k.Value).Select(Model.SpellName).ToList();
            lines.AddRange(added.Count > 0 ? added : new List<string> { none });
            lines.Add("");
            lines.Add($"{A.Tlk(10587)} :");
            lines.Add(none);
            lines.Add("");
        }
        lines.Add($"{A.Tlk(6391)} :");
        var skills = _choice.Skills.Where(k => k.Value > 0).OrderBy(k => Model.SkillName(k.Key)).Select(k => $"{Model.SkillName(k.Key)}+{k.Value}").ToList();
        lines.AddRange(skills.Count > 0 ? skills : new List<string> { none });
        lines.Add("");
        lines.Add($"{A.Tlk(6392)} :");
        // The class's feats, the chosen ones, then the domains' (l19: "... Turn Undead, Extra Turning, Healing Domain
        // Powers, Sun Domain Powers").
        var domainFeats = Model.Domains.Values.Select(d => d.Int("feat", -1)).Where(f => f >= 0).ToHashSet();
        var granted = _plan?.Granted ?? new List<int>();
        var feats = granted.Where(f => !domainFeats.Contains(f)).Concat(_choice.Feats).Concat(granted.Where(domainFeats.Contains)).Select(Model.FeatName).ToList();
        lines.AddRange(feats.Count > 0 ? feats : new List<string> { none });
        if (p.Get<GuiList>("EditBox") is { Frame: { } frame } box)
        {
            box.Bar.Visible = false;
            foreach (var c in box.GetChildren().OfType<Control>()) if (c != frame) c.Visible = false;
            var text = frame.Node("text0") is { } t0 && frame.Node("text1") is { } t1 ? new Rect2(t0, Vector2.Zero).Expand(t1) : new Rect2(10, 10, 370, 320);
            var bar = CgHelp.BarRect(A, frame, text);
            var body = CgText.Create(A, new Rect2(box.Position + text.Position, text.Size), new Rect2(box.Position + bar.Position, bar.Size), p);
            body.Justify = false;
            body.Text = string.Join("\n", lines);
        }
        if (p.Get<GuiButton>("OkButton") is { } ok) ok.Pressed += () => Session.Request("levelup-apply", new { choice = _choice.ToJson() });
        if (p.Get<GuiButton>("CancelButton") is { } cancel) cancel.Pressed += Close;
    }
}

