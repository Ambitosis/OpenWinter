using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.Progression;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// What the classic character creation (<see cref="ClassicCharGen"/>) and level-up (<see cref="ClassicLevelUp"/>) share:
/// one screen at a time (a cg_*.gui layout on pnl_chr_genm, its model origin on the middle of the screen, as the
/// original shows them), the help panel, popups, descriptions from the server, and the level steps both walk through
/// in the original's order: ability increase, skills, feats, school or domains, spells, familiar or companion.
/// Rules stay the server's: every choice is checked by its plan ("chargen-plan" / "levelup-plan").
/// </summary>
public abstract partial class CgFlow : Control
{
    public ClientSession Session = null!;
    protected ClassicAssets A => ClassicAssets.For(Session.Content!);
    protected ChargenModel? Model;
    protected GuiPanel? Screen;
    protected CgHelp? Help;
    /// <summary>Rebuilds the current screen's changing parts (a new plan or description arrived).</summary>
    protected Action? RefreshScreen;
    readonly Dictionary<(string, int), (string Name, string Text)> _descriptions = new();
    readonly List<(string Kind, int Id, Action<string, string> Then)> _waiting = new();
    Control? _popup;

    /// <summary>The level being chosen and the server's plan for it.</summary>
    protected abstract LevelChoice Choice { get; }
    protected abstract LevelPlanView? Plan { get; }
    /// <summary>Asks the server for a new plan of <see cref="Choice"/>.</summary>
    protected abstract void Validate();
    /// <summary>Asks the server to fill one part ("skills", "feats", "spells", "options", "all") from the package.</summary>
    protected abstract void Recommend(string part);
    /// <summary>The character's level for its familiar or companion after this level.</summary>
    protected abstract int AssociateLevel { get; }
    /// <summary>A screen title (level-up prefixes the character's name and classes).</summary>
    protected virtual string Title(string title) => title;
    /// <summary>The level steps were all taken (OK on the last) or abandoned (Cancel on any).</summary>
    protected abstract void LevelStepsDone();
    protected abstract void LevelStepsCancelled();
    /// <summary>The character's base ability scores after the level (the skill and spell screens read them).</summary>
    protected abstract int[] AbilitiesAfter { get; }
    /// <summary>The record of the character as built so far (skill modifiers), if any.</summary>
    protected virtual JsonElement? Record => null;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Resized += () => { if (Screen != null) CgKit.Centre(Screen, Size); if (_popup is GuiPanel p) CgKit.Centre(p, Size); };
    }

    // The HUD these windows live in leaves the tree and comes back (entering the game): listen while in it.
    public override void _EnterTree() => Session.NotifyReceived += OnNotify;
    public override void _ExitTree() => Session.NotifyReceived -= OnNotify;

    protected virtual void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "chargen-data" when Model == null:
                Model = ChargenModel.Parse(m.Json);
                OnModel();
                break;
            case "chargen-describe":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var r = doc.RootElement;
                var key = (r.GetProperty("kind").GetString() ?? "", r.GetProperty("id").GetInt32());
                _descriptions[key] = (r.GetProperty("name").GetString() ?? "", r.GetProperty("text").GetString() ?? "");
                foreach (var w in _waiting.Where(x => (x.Kind, x.Id) == key).ToList())
                {
                    _waiting.Remove(w);
                    w.Then(_descriptions[key].Name, _descriptions[key].Text);
                }
                break;
            }
            case "chargen-associate":
                _associate = JsonDocument.Parse(m.Json).RootElement.Clone();
                if (Visible) RefreshScreen?.Invoke();
                break;
        }
    }

    protected virtual void OnModel() { }

    /// <summary>An entry's name and description from the server (cached).</summary>
    protected void Describe(string kind, int id, Action<string, string> then)
    {
        if (_descriptions.TryGetValue((kind, id), out var d)) { then(d.Name, d.Text); return; }
        _waiting.RemoveAll(w => w.Kind == kind);
        _waiting.Add((kind, id, then));
        Session.Request("chargen-describe", new { kind, id });
    }

    // ------------------------------------------------------------------ screens

    /// <summary>Shows a layout as the current screen (the previous one goes), its title replaced when given.</summary>
    protected GuiPanel? ShowScreen(string layout, string? title = null)
    {
        ClosePopup();
        if (Screen != null) { RemoveChild(Screen); Screen.QueueFree(); }
        Screen = null;
        Help = null;
        RefreshScreen = null;
        var p = GuiPanel.FromLayout(A, layout);
        if (p == null) return null;
        AddChild(p);
        CgKit.Centre(p, Size);
        Screen = p;
        if (title != null && (p.Get<GuiLabel>("Title") ?? p.Get<GuiLabel>("TitleLabel")) is { } t) t.Text = title;
        // The original draws a layout's labels over its lists ("Remaining Spells" lies inside the spell list's frame: c34).
        foreach (var label in p.GetChildren().OfType<GuiLabel>().ToList()) p.MoveChild(label, -1);
        foreach (var tag in new[] { "HelpBox", "HelpEdit" })
            if (CgHelp.Attach(p, tag) is { } h) { Help = h; break; }
        // Buttons whose models place their caption at a single "text" node (the ability rows' ctl_cg_btn_att: c19)
        // draw it there.
        foreach (var b in p.GetChildren().OfType<GuiButton>().Where(b => b is not CgButton && b.Caption.Length > 0 && b.Model?.Node("text") != null && b.Model.Node("text0") == null).ToList())
            CgButton.From(b);
        return p;
    }

    protected GuiButton? Btn(string tag) => Screen?.Get<GuiButton>(tag);

    /// <summary>Wires a layout button (no-op when the layout has none).</summary>
    protected void On(string tag, Action pressed)
    {
        if (Btn(tag) is { } b) b.Pressed += pressed;
    }

    protected void Enable(string tag, bool on)
    {
        if (Btn(tag) is { } b) b.Disabled = !on;
    }

    protected void SetHelp(string title, string text) => Help?.Show(title, text);

    /// <summary>A modal popup layout over the screen, centred (feat and spell information, the colour grid, the deity box).</summary>
    protected GuiPanel? ShowPopup(string layout)
    {
        ClosePopup();
        var p = GuiPanel.FromLayout(A, layout);
        if (p == null) return null;
        var veil = new Control { MouseFilter = MouseFilterEnum.Stop, Name = "PopupVeil" };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        veil.AddChild(p);
        CgKit.Centre(p, Size);
        _popup = veil;
        return p;
    }

    protected void ClosePopup()
    {
        if (_popup != null && IsInstanceValid(_popup)) { RemoveChild(_popup); _popup.QueueFree(); }
        _popup = null;
    }

    /// <summary>The information popup of a feat or spell (cg_spellinfo: its icon, name and description, OK).</summary>
    protected void ShowInfo(string kind, int id, string icon)
    {
        var p = ShowPopup("cg_spellinfo");
        if (p == null) return;
        var title = p.Get<GuiLabel>("Title");
        if (p.Get<GuiList>("EditBox") is { Frame: { } frame } box)
        {
            box.Bar.Visible = false;
            foreach (var c in box.GetChildren().OfType<Control>()) if (c != frame) c.Visible = false;
            var text = frame.Node("text0") is { } t0 && frame.Node("text1") is { } t1 ? new Rect2(t0, Vector2.Zero).Expand(t1) : new Rect2(60, 10, 320, 300);
            var bar = CgHelp.BarRect(A, frame, text);
            var body = CgText.Create(A, new Rect2(box.Position + text.Position, text.Size), new Rect2(box.Position + bar.Position, bar.Size), p);
            body.Justify = false;
            if (p.Node("icon") is { } at && A.Texture(icon) is { } tex)
                p.AddChild(new TextureRect { Texture = tex, Position = (at - new Vector2(16, 16)).Floor(), Size = new Vector2(32, 32), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore });
            Describe(kind, id, (name, desc) => { if (title != null) title.Text = CgKit.Clean(name); body.Text = desc; });
        }
        foreach (var tag in new[] { "CloseButton", "OkButton" })
            if (p.Get<GuiButton>(tag) is { } b) b.Pressed += ClosePopup;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible) return;
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            if (_popup != null) ClosePopup();
            else Btn("CancelButton")?.EmitSignal(BaseButton.SignalName.Pressed);
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventKey) GetViewport().SetInputAsHandled();
    }

    // ------------------------------------------------------------------ level steps

    protected enum LevelStep { Ability, Skills, Feats, School, Domains, Spells, Familiar, Companion }

    /// <summary>Whether a level step is part of this level (by the latest plan).</summary>
    protected virtual bool StepWanted(LevelStep s)
    {
        var p = Plan;
        return s switch
        {
            LevelStep.Ability => p?.AbilityIncrease == true,
            LevelStep.Skills => true,
            // A level with no feat to choose has no feat screen (l06: a wizard 2 goes from skills to spells).
            LevelStep.Feats => p == null || p.GeneralSlots + p.BonusSlots > 0,
            LevelStep.School => p?.PickSchool == true,
            LevelStep.Domains => p?.PickDomains == true,
            LevelStep.Spells => p?.HasSpells == true,
            LevelStep.Familiar => p?.PickFamiliar == true,
            LevelStep.Companion => p?.PickCompanion == true,
            _ => false,
        };
    }

    protected LevelStep? CurrentStep;

    /// <summary>Opens the first wanted level step at or after <paramref name="from"/>; past the last, the steps are done.</summary>
    protected void GoStep(LevelStep from)
    {
        for (var s = from; s <= LevelStep.Companion; s++)
            if (StepWanted(s)) { OpenStep(s); return; }
        CurrentStep = null;
        LevelStepsDone();
    }

    protected void NextStep()
    {
        if (CurrentStep is not { } s) { LevelStepsDone(); return; }
        if (s == LevelStep.Companion) { CurrentStep = null; LevelStepsDone(); return; }
        GoStep(s + 1);
    }

    protected virtual void OpenStep(LevelStep s)
    {
        CurrentStep = s;
        switch (s)
        {
            case LevelStep.Ability: OpenAbilityIncrease(); break;
            case LevelStep.Skills: OpenSkills(); break;
            case LevelStep.Feats: OpenFeats(); break;
            case LevelStep.School: OpenSchool(); break;
            case LevelStep.Domains: OpenDomains(); break;
            case LevelStep.Spells: OpenSpells(); break;
            case LevelStep.Familiar: OpenAssociate(false); break;
            case LevelStep.Companion: OpenAssociate(true); break;
        }
    }

    /// <summary>OK / Cancel of a level step screen.</summary>
    protected void WireStepButtons(Func<bool>? canGo = null)
    {
        On("OkButton", () => { if (canGo?.Invoke() != false) NextStep(); });
        On("CancelButton", LevelStepsCancelled);
    }

    // ------------------------------------------------------------------ scripted presses (captures)

    /// <summary>Whether a button of the screen or its popup can be pressed now.</summary>
    public bool CanPress(string tag)
    {
        var host = _popup is { } pop && IsInstanceValid(pop) ? pop.GetChildren().OfType<GuiPanel>().FirstOrDefault() : null;
        var b = host?.Get<GuiButton>(tag) ?? Screen?.Get<GuiButton>(tag);
        return b != null && !b.Disabled && b.IsVisibleInTree();
    }

    /// <summary>Presses a button of the screen or of the popup over it, by layout tag (captures drive the screens like a player).</summary>
    public bool Press(string tag)
    {
        var host = _popup is { } pop && IsInstanceValid(pop) ? pop.GetChildren().OfType<GuiPanel>().FirstOrDefault() : null;
        var b = host?.Get<GuiButton>(tag) ?? Screen?.Get<GuiButton>(tag);
        if (b == null || b.Disabled || !b.IsVisibleInTree()) return false;
        b.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>Presses the first visible button anywhere on the screen whose caption starts with a text (a list row, a choice).</summary>
    public bool PressCaption(string caption, int skip = 0)
    {
        if (Screen == null) return false;
        foreach (var b in Screen.FindChildren("*", "", true, false).OfType<GuiButton>())
        {
            if (!b.IsVisibleInTree() || !NwnText.Strip(b.Caption).StartsWith(caption, StringComparison.OrdinalIgnoreCase)) continue;
            if (skip-- > 0) continue;
            b.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }
        return false;
    }

    /// <summary>Presses a child button (arrow, help) of the row captioned so: 0 the first child button, 1 the second ...</summary>
    public bool PressRowButton(string caption, int child)
    {
        if (Screen == null) return false;
        foreach (var b in Screen.FindChildren("*", "", true, false).OfType<CgButton>())
        {
            if (!b.IsVisibleInTree() || !NwnText.Strip(b.Caption).StartsWith(caption, StringComparison.OrdinalIgnoreCase)) continue;
            var kids = b.GetChildren().OfType<GuiButton>().ToList();
            if (child >= kids.Count) return false;
            kids[child].EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }
        return false;
    }

    /// <summary>Scrolls the first (or a named) list of the screen to a row.</summary>
    public void ScrollList(int top, string? tag = null)
    {
        var list = Screen?.FindChildren("*", "", true, false).OfType<CgList>().FirstOrDefault(l => tag == null || l.Name == tag + "_rows");
        list?.ScrollTo(top);
    }

    public string Tlk(uint strref) => A.Tlk(strref);

    /// <summary>Shows a tab of the record on the screen (0 Character ... 3 Spells).</summary>
    public void RecordTab(int tab) => Screen?.FindChildren("*", "", true, false).OfType<CgRecord>().FirstOrDefault()?.ShowTab(tab);

    protected static string Signed(int v) => v >= 0 ? $"+{v}" : v.ToString();
    protected static int Mod(int score) => (int)Math.Floor((score - 10) / 2.0);

    JsonElement? _associate;
    protected JsonElement? AssociateRecord(string table, int id, int level)
    {
        if (_associate is { } a && a.GetProperty("table").GetString() == table && a.GetProperty("id").GetInt32() == id && a.GetProperty("level").GetInt32() == level)
            return a.TryGetProperty("record", out var r) && r.ValueKind == JsonValueKind.Object ? r : null;
        return null;
    }
}
