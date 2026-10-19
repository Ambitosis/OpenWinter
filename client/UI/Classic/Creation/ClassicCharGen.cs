using System.Text.Json;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.Progression;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// Character creation in the original's look and order (measured with tools/visual/menudrive.py on the original,
/// shots c00-c51). The main screen (cg_main) lists Gender, Race, Portrait, Class, Alignment, Abilities,
/// Packages and Customize beside the character record; only the steps already taken and the next one (its frame
/// pulsing) can be pressed, and Play opens when all are done. Each step is its own screen with OK and Cancel: OK
/// keeps the choice and makes every later step to be taken again (the values stay), Cancel changes nothing.
/// Packages either take the class package as it is (OK) or walk through skills, feats, school or domains, spells and
/// familiar or companion (Configure Packages); Customize is the appearance, then name, age, voice and description.
/// The server builds, checks and saves the character (the same "chargen-*" requests as the modern wizard).
/// </summary>
public partial class ClassicCharGen : CgFlow
{
    public enum MainStep { Gender, Race, Portrait, Class, Alignment, Abilities, Packages, Customize }
    static readonly string[] MainTags = { "GenderButton", "RaceButton", "PortraitButton", "ClassButton", "AlignButton", "AbilitiesButton", "PackagesButton", "CustomizeButton" };

    /// <summary>The player left creation (back to character selection).</summary>
    public Action? Cancelled;
    NewCharacter _n = new();
    NewCharacter? _snapshot;
    int _done;
    LevelPlanView? _plan;
    List<(string Code, string Message)> _identity = new();
    JsonElement? _record;
    string? _pendingAuto;
    Action? _afterAuto;
    readonly List<(int Id, string ResRef)> _portraits = new();
    (int Race, int Gender) _portraitsFor = (-1, -1);
    bool _creating;
    ColorRect _black = null!;
    CgRecord? _mainRecord;

    public NewCharacter Character => _n;
    public int StepsDone => _done;
    protected override LevelChoice Choice => _n.Level;
    protected override LevelPlanView? Plan => _plan;
    protected override int AssociateLevel => 1;
    protected override int[] AbilitiesAfter => _n.Abilities;
    protected override int[] AbilitiesBefore => _n.Abilities;
    protected override int[] RacialAdjustments => Model?.Races.GetValueOrDefault(_n.Race)?.Ints("adjust") is { Length: 6 } a ? a : new int[6];
    protected override JsonElement? Record => _record;

    public static ClassicCharGen Create(Gameplay.ClientSession session) => new() { Session = session, Name = "ClassicCharGen" };

    public override void _Ready()
    {
        // The original shows creation over black (c00).
        _black = new ColorRect { Color = Colors.Black, MouseFilter = MouseFilterEnum.Stop };
        _black.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_black);
        base._Ready();
    }

    /// <summary>Opens creation at its main screen with a new character.</summary>
    public void Open()
    {
        Visible = true;
        _creating = false;
        GetParent()?.MoveChild(this, -1);
        Gameplay.PlayerInput.Blocked = true;
        if (Model == null) { Session.Request("chargen-data"); return; }
        Reset();
    }

    protected override void OnModel()
    {
        if (Visible) Reset();
    }

    void Reset()
    {
        _n = new NewCharacter
        {
            Gender = 0,
            Race = Model!.Races.ContainsKey(6) ? 6 : Model.Races.Keys.DefaultIfEmpty(-1).First(),
            BodyParts = CharacterFactory.DefaultBodyParts(),
        };
        _n.Abilities = Enumerable.Repeat(Model.AbilityMin, 6).ToArray();
        _n.SoundSet = DefaultSoundSet();
        _done = 0;
        _plan = null;
        _record = null;
        Validate();
        ShowMain();
    }

    int DefaultSoundSet() => Model?.SoundSets.Values.Where(s => s.Int("gender") == _n.Gender).Select(s => s.Id).DefaultIfEmpty(-1).First() ?? -1;

    public void Close()
    {
        Visible = false;
        ClosePopup();
        Gameplay.PlayerInput.Blocked = false;
    }

    // ------------------------------------------------------------------ server

    protected override void Validate() =>
        Session.Request("chargen-validate", new { character = _n.ToJson(), auto = _pendingAuto != null, sheet = true });

    protected override void Recommend(string part)
    {
        _pendingAuto = part;
        if (part is "skills" or "all") _n.Level.Skills.Clear();
        if (part is "feats" or "all") _n.Level.Feats.Clear();
        if (part is "spells" or "all") _n.Level.Spells.Clear();
        Validate();
    }

    protected override void OnNotify(NotifyMessage m)
    {
        base.OnNotify(m);
        if (!Visible && m.Kind != "chargen-data") return;
        switch (m.Kind)
        {
            case "chargen-plan":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var root = doc.RootElement;
                _plan = LevelPlanView.Parse(root.GetProperty("plan"));
                _identity = root.GetProperty("identity").EnumerateArray().Select(e => (e.GetProperty("code").GetString() ?? "", e.GetProperty("message").GetString() ?? "")).ToList();
                _record = root.TryGetProperty("sheet", out var sh) && sh.ValueKind == JsonValueKind.Object ? sh.Clone() : null;
                if (_plan.Auto is { } auto && _pendingAuto != null)
                {
                    var c = LevelChoice.FromJson(auto);
                    string part = _pendingAuto;
                    _pendingAuto = null;
                    var l = _n.Level;
                    if (part is "skills" or "all") l.Skills = c.Skills;
                    if (part is "feats" or "all") l.Feats = c.Feats;
                    if (part is "spells" or "all") l.Spells = c.Spells;
                    if (part is "school" or "all") l.School = c.School;
                    if (part is "domains" or "all") { l.Domain1 = c.Domain1; l.Domain2 = c.Domain2; }
                    if (part is "familiar" or "all") l.Familiar = c.Familiar;
                    if (part is "companion" or "all") l.Companion = c.Companion;
                    Validate();
                    var then = _afterAuto;
                    _afterAuto = null;
                    then?.Invoke();
                }
                RefreshScreen?.Invoke();
                break;
            }
            case "chargen-portraits":
            {
                using var doc = JsonDocument.Parse(m.Json);
                _portraits.Clear();
                foreach (var p in doc.RootElement.GetProperty("portraits").EnumerateArray())
                {
                    var resref = p.GetProperty("resref").GetString() ?? "";
                    // Portraits listed but not installed are left out (po_carp_ for a female elf: c07).
                    if (ChargenModel.Portrait(Session.Content, resref, "s") != null) _portraits.Add((p.GetProperty("id").GetInt32(), resref));
                }
                RefreshScreen?.Invoke();
                break;
            }
            case "chargen-result":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var r = doc.RootElement;
                _creating = false;
                if (r.GetProperty("ok").GetBoolean()) { Close(); return; }
                var errors = r.GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("message").GetString() ?? "").ToList();
                ClassicPopup.Ok(this, A, string.Join("\n", errors.Take(4)));
                RefreshScreen?.Invoke();
                break;
            }
        }
    }

    // ------------------------------------------------------------------ main screen

    /// <summary>The main screen: the steps (taken ones pressable, the next one encouraged, the rest disabled) and the record.</summary>
    public void ShowMain()
    {
        CurrentStep = null;
        if (ShowScreen("cg_main") is not { } p) return;
        for (int i = 0; i < MainTags.Length; i++)
        {
            int step = i;
            On(MainTags[i], () => OpenMain((MainStep)step));
        }
        On("PlayButton", Play);
        On("ResetButton", () => { Reset(); });
        On("CancelButton", () => { Close(); Cancelled?.Invoke(); });
        // The record hangs from the panel model's CharacterSheet node; its pages end at the panel's inner edge.
        var at = p.Node("CharacterSheet") ?? p.ModelOrigin + new Vector2(9, -221);
        _mainRecord = CgRecord.Create(A, at, p.ModelOrigin.Y + 263 - 2);
        _mainRecord.CreationSaves = true;
        p.AddChild(_mainRecord);
        RefreshScreen = RefreshMain;
        RefreshMain();
    }

    void RefreshMain()
    {
        if (Screen == null || Model == null) return;
        for (int i = 0; i < MainTags.Length; i++)
        {
            if (Btn(MainTags[i]) is not { } b) continue;
            b.Disabled = i > _done;
            b.ForcedState = i == _done ? "encouraged" : null;
        }
        if (Btn("PlayButton") is { } play)
        {
            play.Disabled = _done < MainTags.Length || _creating;
            play.ForcedState = _done >= MainTags.Length && !_creating ? "encouraged" : null;
        }
        if (_mainRecord == null) return;
        _mainRecord.Identity = IdentityLines();
        _mainRecord.ShowAbilities = _done > (int)MainStep.Abilities;
        _mainRecord.Portrait = _done > (int)MainStep.Portrait ? PortraitResRef() : "gui_po_nwnlogo_";
        _mainRecord.Fill(_record, _n.FullName);
    }

    /// <summary>The record's identity box as the original fills it: the race (and alignment once chosen), then the class (c15, c22).</summary>
    string[] IdentityLines()
    {
        var m = Model!;
        var lines = new List<string>();
        if (_done > (int)MainStep.Race)
        {
            string race = m.Races.GetValueOrDefault(_n.Race)?.Name ?? "";
            if (_done > (int)MainStep.Alignment) race += ", " + AlignmentName(_n.LawChaos, _n.GoodEvil);
            lines.Add(race);
        }
        if (_done > (int)MainStep.Class) lines.Add($"{m.ClassName(_n.Level.Class)} (1)");
        return lines.ToArray();
    }

    string AlignmentName(int lc, int ge) => A.Tlk(AlignmentRefs[AlignmentIndex(lc, ge)]);

    string PortraitResRef() =>
        _n.PortraitId >= 0 ? _portraits.FirstOrDefault(p => p.Id == _n.PortraitId).ResRef ?? PortraitFromRow(_n.PortraitId) : _n.Portrait;

    string PortraitFromRow(int row) => Session.Content?.Resources.TwoDA("portraits").Get(row, "BaseResRef") is { Length: > 0 } b ? "po_" + b.ToLowerInvariant() : "";

    /// <summary>Opens a main step's screen; the character is kept so Cancel can put it back.</summary>
    public void OpenMain(MainStep step)
    {
        _snapshot = Clone(_n);
        switch (step)
        {
            case MainStep.Gender: OpenGender(); break;
            case MainStep.Race: OpenRace(); break;
            case MainStep.Portrait: OpenPortrait(); break;
            case MainStep.Class: OpenClass(); break;
            case MainStep.Alignment: OpenAlignment(); break;
            case MainStep.Abilities: OpenAbilities(); break;
            case MainStep.Packages: OpenPackages(); break;
            case MainStep.Customize: OpenAppearance(); break;
        }
    }

    static NewCharacter Clone(NewCharacter n) => NewCharacter.FromJson(JsonSerializer.SerializeToElement(n.ToJson()));

    /// <summary>OK on a main step: its choice stands and every later step is to be taken again.</summary>
    void Taken(MainStep step)
    {
        _done = (int)step + 1;
        _snapshot = null;
        Validate();
        ShowMain();
    }

    /// <summary>Cancel on a main step: the character as it was.</summary>
    void Abandon()
    {
        if (_snapshot != null) _n = _snapshot;
        _snapshot = null;
        Validate();
        ShowMain();
    }

    void WireMain(MainStep step, Func<bool>? canTake = null)
    {
        On("OkButton", () => { if (canTake?.Invoke() != false) Taken(step); });
        On("CancelButton", Abandon);
        if (Model?.Recommended == false && Btn("RecommendButton") is { } r) r.Visible = false;
    }

    void Play()
    {
        if (_done < MainTags.Length || _creating) return;
        var errors = _identity.Select(e => e.Message).Concat(_plan?.Errors.Select(e => e.Message) ?? Enumerable.Empty<string>()).Distinct().ToList();
        if (errors.Count > 0) { ClassicPopup.Ok(this, A, string.Join("\n", errors.Take(4))); return; }
        _creating = true;
        RefreshMain();
        Session.Request("chargen-create", new { character = _n.ToJson() });
    }

    // ------------------------------------------------------------------ level steps (Configure Packages)

    protected override void LevelStepsDone() => Taken(MainStep.Packages);
    protected override void LevelStepsCancelled() => Abandon();

    protected override int PackageSchool() =>
        Session.Content?.Resources.TwoDA("packages").GetInt(_n.Level.Package, "School", 0) is int s and >= 0 ? s : 0;

    protected override int DefaultAssociate(bool companion)
    {
        int a = Session.Content?.Resources.TwoDA("packages").GetInt(_n.Level.Package, "Associate", -1) ?? -1;
        return a >= 0 ? a : 0;
    }
}
