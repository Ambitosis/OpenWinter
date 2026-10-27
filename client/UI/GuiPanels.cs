using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Classic.Sheets;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Built-in panels as scripts control them, in both interfaces: SetGuiPanelDisabled ("gui-disable") keeps a panel
/// shut and closes it if open, PopUpGUIPanel ("gui-panel") opens it anyway, and the player's attempts to open a
/// disabled panel, along with the other built-in GUI events, go to the module's OnPlayerGuiEvent ("gui-event").
/// Measured on the original in the visual scene guipanels: a disabled panel's key or button raises
/// GUIEVENT_DISABLED_PANEL_ATTEMPT_OPEN with the panel and, for the character sheet, its owner; a forced panel's key
/// closes it as usual; the journal and map raise their open and close events however they open or close.
/// </summary>
public partial class GuiPanels : Node
{
    // GUI_PANEL_* values.
    public const int Minimap = 2, Compass = 3, Inventory = 4, PlayerList = 5, Journal = 6, Spellbook = 7, CharacterSheet = 8,
        LevelUp = 9, GoldInventory = 10, GoldBarter = 11, ExamineCreature = 12, ExamineItem = 13, ExaminePlaceable = 14, ExamineDoor = 15;
    // GUIEVENT_* values.
    public const int ChatFocus = 1, ChatUnfocus = 2, MinimapOpen = 8, MinimapClose = 9, JournalOpen = 10, JournalClose = 11,
        AttemptOpen = 14, CompassClick = 15, LoadScreenFinished = 17, OptionsOpen = 24, OptionsClose = 25, RadialOpen = 26;
    const uint Invalid = 0x7F000000;
    // The game menu and the examine windows: not GUI panels the gate keeps shut, but watched all the same.
    const int Options = -1, Examine = -2;

    /// <summary>The game's panel gate; null outside a game.</summary>
    public static GuiPanels? Current { get; private set; }

    public ClientSession Session = null!;
    public PlayerInput Input_ = null!;

    readonly Dictionary<int, uint> _disabled = new();
    readonly Dictionary<Control, int> _windows = new();
    readonly HashSet<Control> _rejected = new();
    readonly Dictionary<int, ulong> _forced = new(), _closedFrame = new();
    readonly Dictionary<int, bool> _reported = new();
    (int Panel, uint Object) _examined = (0, Invalid);
    bool _evaluating, _loadPending;

    public override void _Ready()
    {
        Current = this;
        Session.NotifyReceived += OnNotify;
        Session.AreaPresented += AreaLoaded;
        Session.ModuleCleared += Forget;
        Session.ControlledChanged += ControlledChanged;
        DM.ChatInput.FocusChanged += Chat;
        ClassicCompass.Clicked = CompassClicked;
        GetTree().NodeAdded += Watch;
        WatchAll(GetTree().Root);
        TreeExiting += () =>
        {
            Session.NotifyReceived -= OnNotify;
            Session.AreaPresented -= AreaLoaded;
            Session.ModuleCleared -= Forget;
            Session.ControlledChanged -= ControlledChanged;
            DM.ChatInput.FocusChanged -= Chat;
            if (ClassicCompass.Clicked == CompassClicked) ClassicCompass.Clicked = null;
            GetTree().NodeAdded -= Watch;
            if (Current == this) Current = null;
        };
    }

    /// <summary>The GUI panel a window shows (0 for any other node).</summary>
    static int PanelOf(Node n) => n switch
    {
        InventoryPanel or ClassicInventoryWindow => Inventory,
        CharacterPanel or ClassicCharacterSheet => CharacterSheet,
        JournalPanel or ClassicJournal => Journal,
        MapPanel or ClassicMiniMap or ClassicAreaMap => Minimap,
        SpellbookPanel or ClassicSpellbookWindow or ClassicMemorizeWindow => Spellbook,
        PlayerListPanel or ClassicPlayerList => PlayerList,
        FrontEnd.GameMenu or ClassicGameMenu => Options,
        ExaminePanel or ClassicExamine or ClassicExamineItem => Examine,
        _ => 0,
    };

    void WatchAll(Node n)
    {
        Watch(n);
        foreach (var child in n.GetChildren()) WatchAll(child);
    }

    void Watch(Node n)
    {
        if (n is not Control c || PanelOf(n) is not (var panel and not 0) || _windows.ContainsKey(c)) return;
        _windows[c] = panel;
        c.VisibilityChanged += () => Changed(c);
        c.TreeExiting += () => { _windows.Remove(c); _rejected.Remove(c); };
    }

    bool Gated(int panel) => panel is Minimap or Inventory or PlayerList or Journal or Spellbook or CharacterSheet;

    /// <summary>Whose panel it is: the character sheet's owner (the controlled creature), else none.</summary>
    uint Subject(int panel) => panel == CharacterSheet ? Session.ControlledId : Invalid;

    /// <summary>
    /// Disabled for this subject: a disable without a target covers every subject, one with a target only that one
    /// (the character sheet disabled for another creature still opens the player's own).
    /// </summary>
    bool Blocked(int panel, uint subject) =>
        _disabled.TryGetValue(panel, out var target) && (target == Invalid || target == 0 || subject == Invalid || target == subject);

    bool IsOpen(int panel) => _windows.Any(w => w.Value == panel && w.Key.Visible && !_rejected.Contains(w.Key));

    void Changed(Control c)
    {
        if (!_windows.TryGetValue(c, out var panel)) return;
        if (c.Visible && Gated(panel) && !Allowed(panel, c))
        {
            // Opened by a key, button or radial entry while disabled: shut again before it draws.
            _rejected.Add(c);
            Callable.From(() => CloseWindow(c)).CallDeferred();
            Event(AttemptOpen, panel, Subject(panel));
            return;
        }
        if (!c.Visible)
        {
            if (_rejected.Remove(c)) return;
            _closedFrame[panel] = Engine.GetProcessFrames();
        }
        if (panel is Minimap or Journal or Options && !_evaluating)
        {
            _evaluating = true;
            Callable.From(Report).CallDeferred();
        }
    }

    bool Allowed(int panel, Control c)
    {
        if (!Blocked(panel, Subject(panel))) return true;
        ulong frame = Engine.GetProcessFrames();
        // PopUpGUIPanel shows it anyway; so does a window of the same panel taking over from another (the classic
        // minimap enlarged to the area map) or one that is already open (the spell book with its memorize window).
        if (_forced.TryGetValue(panel, out var until) && frame <= until) return true;
        if (_closedFrame.TryGetValue(panel, out var closed) && closed == frame) return true;
        return _windows.Any(w => w.Key != c && w.Value == panel && w.Key.Visible && !_rejected.Contains(w.Key));
    }

    /// <summary>The journal's, map's and game menu's open and close events, once their windows have settled.</summary>
    void Report()
    {
        _evaluating = false;
        foreach (var (panel, open, close) in new[] { (Minimap, MinimapOpen, MinimapClose), (Journal, JournalOpen, JournalClose), (Options, OptionsOpen, OptionsClose) })
        {
            bool now = IsOpen(panel);
            if (now == _reported.GetValueOrDefault(panel)) continue;
            _reported[panel] = now;
            Event(now ? open : close);
        }
    }

    static void CloseWindow(Control c)
    {
        if (!IsInstanceValid(c) || !c.Visible) return;
        if (c is UiTheme.Window w) w.Close();
        else if (c is ClassicWindow cw) cw.Close();
        else if (c is FrontEnd.GameMenu menu) menu.Close();
        else c.Visible = false;
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "gui-disable":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                int panel = d.GetProperty("panel").GetInt32();
                uint target = d.TryGetProperty("target", out var t) && t.TryGetUInt32(out var id) ? id : Invalid;
                if (!d.GetProperty("disabled").GetBoolean()) { _disabled.Remove(panel); break; }
                _disabled[panel] = target;
                _forced.Remove(panel);
                // An open panel closes, except the level-up and gold panels.
                if (panel is LevelUp or GoldInventory or GoldBarter) break;
                if (Gated(panel) && Blocked(panel, Subject(panel)))
                    foreach (var w in _windows.Where(w => w.Value == panel).Select(w => w.Key).ToArray()) CloseWindow(w);
                if (panel is >= ExamineCreature and <= ExamineDoor && _examined.Panel == panel && Blocked(panel, _examined.Object))
                    foreach (var w in _windows.Where(w => w.Value == Examine).Select(w => w.Key).ToArray()) CloseWindow(w);
                break;
            }
            case "gui-panel":
            {
                using var doc = JsonDocument.Parse(m.Json);
                int panel = doc.RootElement.GetProperty("panel").GetInt32();
                if (!Gated(panel) || IsOpen(panel)) break;
                // A few frames' grace: some windows show once the server has answered.
                _forced[panel] = Engine.GetProcessFrames() + 2;
                (panel switch
                {
                    Inventory => Input_.ToggleInventory,
                    CharacterSheet => Input_.ToggleCharacter,
                    Journal => Input_.ToggleJournal,
                    Minimap => Input_.ToggleMap,
                    Spellbook => Input_.ToggleSpellbook,
                    PlayerList => Input_.OpenPlayerList,
                    _ => null,
                })?.Invoke();
                break;
            }
            case "examine":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                string kind = d.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
                uint id = d.TryGetProperty("object", out var o) && o.TryGetUInt32(out var v) ? v : m.Subject;
                _examined = (ExaminePanelFor(kind), id);
                break;
            }
        }
    }

    static int ExaminePanelFor(string kind) => kind switch
    {
        "creature" => ExamineCreature,
        "item" => ExamineItem,
        "placeable" => ExaminePlaceable,
        "door" => ExamineDoor,
        _ => 0,
    };

    /// <summary>
    /// The player examining an object: false (and the attempt reported, with the object) when its examine panel is
    /// disabled. ActionExamine still shows the window: the server's answer is never gated.
    /// </summary>
    public bool AllowExamine(uint id)
    {
        int panel = Session.Objects.TryGetValue(id, out var view)
            ? view.Kind switch { ObjectKind.Creature => ExamineCreature, ObjectKind.Placeable => ExaminePlaceable, ObjectKind.Door => ExamineDoor, ObjectKind.Item => ExamineItem, _ => 0 }
            : ExamineItem;
        if (panel == 0 || !Blocked(panel, id)) return true;
        Event(AttemptOpen, panel, id);
        return false;
    }

    /// <summary>A radial menu opened on the ground: GUIEVENT_RADIAL_OPEN with 1 and the point (scene guipanels).</summary>
    public void GroundRadialOpened(System.Numerics.Vector3 point) => Event(RadialOpen, 1, Invalid, point);

    void CompassClicked()
    {
        // A disabled compass still shows but its clicks do nothing.
        if (!Blocked(Compass, Invalid)) Event(CompassClick);
    }

    void Chat(bool focused, ChatChannel channel)
    {
        // The selected channel: 0 shout, 1 whisper, 2 talk, 3 party, 4 DM.
        int integer = channel switch { ChatChannel.Shout => 0, ChatChannel.Whisper => 1, ChatChannel.Party => 3, ChatChannel.Dm => 4, _ => 2 };
        Event(focused ? ChatFocus : ChatUnfocus, integer);
    }

    // Over the NWN bridge the area can be ready before the player's creature is: the event waits for it.
    void AreaLoaded()
    {
        _loadPending = Session.ControlledId == 0;
        Event(LoadScreenFinished);
    }

    void ControlledChanged(uint id)
    {
        if (!_loadPending || id == 0) return;
        _loadPending = false;
        Event(LoadScreenFinished);
    }

    /// <summary>Disabled panels don't outlast the module (nor a relog, as the original's).</summary>
    void Forget()
    {
        _disabled.Clear();
        _forced.Clear();
        _reported.Clear();
    }

    void Event(int type, int integer = 0, uint obj = Invalid, System.Numerics.Vector3 vector = default)
    {
        if (Session.ControlledId == 0) return;
        Session.Request("gui-event", new { type, integer, @object = obj, vector = new[] { vector.X, vector.Y, vector.Z } });
    }
}
