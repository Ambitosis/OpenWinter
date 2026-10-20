using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>One wedge of the radial menu: an action, or a sub-ring (<see cref="Open"/>).</summary>
public sealed class RadialEntry
{
    public string Label = "";
    /// <summary>Second line in the centre when hovered ("2 uses left", "6 feats").</summary>
    public string Detail = "";
    /// <summary>Game icon (and badge) when it has one; otherwise <see cref="Glyph"/>.</summary>
    public SlotLook? Look;
    public string Glyph = "cog";
    public bool Disabled, Active;
    /// <summary>Reserve an original fixed direction without drawing or selecting a command.</summary>
    public bool Hidden;
    public Func<List<RadialEntry>>? Open;
    public Action? Act;
    /// <summary>What dragging this wedge onto the action bar puts there.</summary>
    public BarSlot? Slot;
    /// <summary>The original's sound for choosing it, when not the radial's own (gui_quick_erase for Empty Quickslot).</summary>
    public string? Sound;
}

/// <summary>
/// The radial menu (right-click a creature, object or yourself): a ring of actions for the target (examine, talk,
/// attack, open, unlock, disarm a trap, ...) and sub-rings of spells, feats, skills, items, modes and emotes that can
/// be used on it, plus the DM's tools. Click a wedge to use it, the centre (or right-click) to go back, anywhere
/// else (or Esc) to close. Wedges can be dragged onto the action bar.
/// <para>Add to the HUD with <see cref="Session"/> and <see cref="Casting"/> set; it registers with the
/// <see cref="Interaction"/> that opens context menus.</para>
/// </summary>
public partial class RadialMenu : Control
{
    internal static ActionKind DoorMenuAction(ObjectView target) => target.Kind == ObjectKind.Door
        ? (target.Flags & ObjectFlags.Open) != 0 ? ActionKind.CloseDoor : ActionKind.OpenDoor
        : ActionKind.Use;
    public ClientSession Session = null!;
    public SpellCasting Casting = null!;
    /// <summary>Opens the examine window.</summary>
    public Action<uint>? Examine;
    AbilityBook _book = null!;
    AbilityUser _user = null!;
    Interaction Interaction => Casting.Interaction;

    const float Inner = 60, Outer = 156, CentreR = 52;
    /// <summary>Wedges per ring before it pages (the skin's fixed slots when one draws the ring).</summary>
    float MaxPerRing => Skin?.Slots ?? 10;

    /// <summary>Draws the ring in another look (the classic interface); the menu's logic, input and rings stay the same.</summary>
    public interface ISkin
    {
        /// <summary>Fixed number of positions around the centre (the ring pages beyond them).</summary>
        int Slots { get; }
        void Draw(Control canvas, RadialMenu menu);
        /// <summary>The ring was opened (the classic look plays the original's sound).</summary>
        void Selected() { }
        /// <summary>Asks for a line of text in the look's own prompt (classic: util_editpopup); false for the modern one.</summary>
        bool Prompt(string message, string text, Action<string> ok) => false;
        /// <summary>Opens a spell, feat or skill's description (Examine on a quickbar slot); false for the modern one.</summary>
        bool Describe(BarSlot slot, SlotLook look) => false;
    }

    ISkin? _skin;
    /// <summary>Set to draw the ring in another look; null for the modern one.</summary>
    public ISkin? Skin
    {
        get => _skin;
        set { _skin = value; if (_ring != null) _ring.Visible = value == null; _overlay?.QueueRedraw(); }
    }

    /// <summary>The wedges shown now, in order clockwise from the top (skins).</summary>
    public IReadOnlyList<RadialEntry> Shown => _shown;
    /// <summary>The hovered wedge: its index, -2 the centre, -1 none (skins).</summary>
    public int Hovered => _hover;
    /// <summary>The ring's centre on screen (skins).</summary>
    public Vector2 Centre => _centre;
    public Vector2 Pointer => _pointer;
    public bool IsSelf => _target?.ObjectId == Session.ControlledId;
    /// <summary>The entry shown in the ring's centre: the parent of a sub-ring, or the icon a custom menu was opened with (skins).</summary>
    public RadialEntry? ParentEntry => _stack.Count > 1 ? _stack[^1].Parent : _stack.Count == 1 ? _rootEntry : null;
    public IReadOnlyList<RadialEntry> Preview { get; private set; } = [];
    /// <summary>The object the menu is for (skins).</summary>
    public ObjectView? Target => _target;
    /// <summary>The authoritative inventory-art layers for an item target, including its palette.</summary>
    public string[] TargetItemIcons => _target?.Kind == ObjectKind.Item && Facts is { } facts
        && facts.TryGetProperty("itemIcon", out var icons) && icons.ValueKind == JsonValueKind.Array
            ? icons.EnumerateArray().Select(icon => icon.GetString() ?? "").ToArray() : [];
    /// <summary>The same inventory canvas used by item windows, without power-of-two padding.</summary>
    public ItemEntry? TargetItemArt
    {
        get
        {
            if ((_inventoryItem ?? _foreignItem) is { } owned) return owned;
            if (TargetItemIcons is not { Length: > 0 } icons || Facts is not { } facts || _target == null) return null;
            int Number(string key, int fallback) => facts.TryGetProperty(key, out var value) ? value.GetInt32() : fallback;
            return new ItemEntry(_target.ObjectId, _target.DisplayName, Number("itemBase", -1), Number("itemStack", 1), icons)
            { Info = new ItemInfo { Width = Number("itemWidth", 1), Height = Number("itemHeight", 1) } };
        }
    }
    public string TargetPortrait => _target?.Portrait is { Length: > 0 } portrait ? portrait
        : Facts is { } facts && facts.TryGetProperty("portrait", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";
    /// <summary>The titles of the rings opened so far, the target's name first (skins).</summary>
    public IReadOnlyList<string> Path => _stack.Select(x => x.Title).ToList();
    const int MaskSize = 24;

    sealed class Ring
    {
        public required string Title;
        public required Func<List<RadialEntry>> Build;
        public List<RadialEntry> Entries = new();
        public int Page;
        public RadialEntry? Parent;
    }

    readonly List<Ring> _stack = new();
    List<RadialEntry> _shown = new();
    ObjectView? _target;
    System.Numerics.Vector3? _ground;
    Vector2 _centre, _pointer;
    int _tileQuery;
    List<RadialEntry> _tileActions = [];
    int _hover = -1; // wedge index, -2 the centre
    ColorRect _ring = null!;
    ShaderMaterial _mat = null!;
    Control _overlay = null!;
    Tween? _tween;
    bool _dragging;
    bool _openingRelease;
    Vector2 _openPoint;
    uint _controlledAtOpen;

    public bool IsOpen => Visible && (_target != null || _ground != null || _inventoryItem != null || _custom);
    /// <summary>A menu with its own entries (<see cref="OpenMenu"/>, <see cref="OpenItemMenu"/>), such as the chat window's.</summary>
    bool _custom;
    /// <summary>The centre icon of a menu opened with <see cref="OpenMenu"/> (the chat menu's ir_chat).</summary>
    RadialEntry? _rootEntry;
    /// <summary>
    /// Menus opened from a window's control (the chat bar, an inventory slot) take the pointer through _Input: the
    /// control that was pressed keeps the GUI's mouse focus, so hovers and clicks would otherwise go to it and the ring
    /// would show no label or pointer line.
    /// </summary>
    bool PointerOwned => ItemContext || _custom;

    public override void _Ready()
    {
        (_book, _user) = AbilityServices.For(Session, Casting);
        _user.Examine ??= Examine;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        _mat = new ShaderMaterial { Shader = new Shader { Code = RingShader } };
        _ring = new ColorRect { Material = _mat, MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2((Outer + 28) * 2, (Outer + 28) * 2) };
        _ring.Visible = Skin == null;
        AddChild(_ring);
        _overlay = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _overlay.Draw += DrawOverlay;
        AddChild(_overlay);
        Interaction.OpenRadial = Open;
        Interaction.GroundMenu ??= OpenGround;
        Interaction.OpenMenu = OpenMenu;
        Interaction.QuickbarMenu = OpenQuickbar;
        Interaction.TrapMenu = OpenTrap;
        Interaction.CloseRadial = Close;
        Interaction.RadialOpen = () => Visible;
        // A conversation takes the number keys for its replies: a ring left open would turn "3" into Attack.
        Session.DialogReceived += CloseForDialog;
        Session.AreaLoading += CloseForArea;
        Session.AreaLoaded += Close;
        Session.NotifyReceived += TileActionsReceived;
        TreeExiting += () =>
        {
            Session.DialogReceived -= CloseForDialog;
            Session.AreaLoading -= CloseForArea;
            Session.AreaLoaded -= Close;
            Session.NotifyReceived -= TileActionsReceived;
        };
        TreeExiting += ClearInventoryContext;
        _book.Changed += () => { if (IsOpen) Rebuild(); };
        _book.TargetFactsArrived += id => { if (IsOpen && (_target?.ObjectId ?? _inventoryItem?.Id) == id) Rebuild(); };
    }

    // ------------------------------------------------------------------ opening and navigating

    /// <summary>Opens the ring for an object (or the player) centred near a screen point.</summary>
    public void Open(ObjectView target, Vector2 at)
    {
        ClearInventoryContext();
        BeginPointerSelection(at);
        _target = target;
        _ground = null;
        _controlledAtOpen = Session.ControlledId;
        _stack.Clear();
        bool self = target.ObjectId == Session.ControlledId;
        _stack.Add(new Ring { Title = Name(target), Build = () => Root(target, self) });
        var view = GetViewportRect().Size;
        float m = Outer + 34;
        // Keep clear of the screen edges and of the action bar along the bottom.
        _centre = new Vector2(Math.Clamp(at.X, m, Math.Max(m, view.X - m)), Math.Clamp(at.Y, m + 26, Math.Max(m + 26, view.Y - m - 96)));
        _pointer = at;
        // Pushed in from a screen edge, the ring takes the pointer with it to its centre, as the original does.
        if (_centre != at) RecenterPointer();
        Preview = [];
        _book.Refresh(target.ObjectId);
        if (Session.Content != null && _book.Spells.Count == 0) Session.Request("spellbook");
        _hover = -1;
        _dragging = false;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = true;
        Rebuild();
        Animate(0.86f);
    }

    /// <summary>Opens abilities for the selected walkable ground point.</summary>
    public void OpenGround(Vector2 at, System.Numerics.Vector3 point)
    {
        ClearInventoryContext();
        BeginPointerSelection(at);
        _target = null;
        _ground = point;
        _controlledAtOpen = Session.ControlledId;
        _stack.Clear();
        _stack.Add(new Ring { Title = "Ground", Build = GroundRoot });
        _tileActions = [];
        Session.Request("tile-actions", new { query = ++_tileQuery, area = Session.Area?.Info.AreaId, position = new[] { point.X, point.Y, point.Z } });
        var view = GetViewportRect().Size;
        float m = Outer + 34;
        _centre = new Vector2(Math.Clamp(at.X, m, Math.Max(m, view.X - m)), Math.Clamp(at.Y, m + 26, Math.Max(m + 26, view.Y - m - 96)));
        _pointer = at;
        // Pushed in from a screen edge, the ring takes the pointer with it to its centre, as the original does.
        if (_centre != at) RecenterPointer();
        Preview = [];
        _book.Refresh();
        if (Session.Content != null && _book.Spells.Count == 0) Session.Request("spellbook");
        _hover = -1;
        _dragging = false;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = true;
        Rebuild();
        Animate(0.86f);
    }

    /// <summary>
    /// Opens a ring of the caller's own entries (the chat window's menu) centred near a screen point. The root entry
    /// gives the title (its label) and the centre icon the classic look draws (its look).
    /// </summary>
    public void OpenMenu(RadialEntry root, Vector2 at, Func<List<RadialEntry>> build)
    {
        Close();
        BeginPointerSelection(at);
        _target = null;
        _ground = null;
        _custom = true;
        _rootEntry = root;
        _controlledAtOpen = Session.ControlledId;
        _stack.Clear();
        _stack.Add(new Ring { Title = root.Label, Build = build });
        var view = GetViewportRect().Size;
        if (Skin == null)
        {
            float m = Outer + 34;
            _centre = new Vector2(Math.Clamp(at.X, m, Math.Max(m, view.X - m)), Math.Clamp(at.Y, m + 26, Math.Max(m + 26, view.Y - m - 96)));
        }
        else
        {
            // The original's chat menu clamps its centre 154 px from the screen edges, as its item rings do (chatradial:
            // a right click on the bar at y 592 of 720 centres the ring at y 565).
            const float margin = 154;
            _centre = new Vector2(Math.Clamp(at.X, margin, Math.Max(margin, view.X - margin)), Math.Clamp(at.Y, margin, Math.Max(margin, view.Y - margin)));
        }
        _pointer = at;
        // Pushed in from a screen edge (or raised above the quickbar slot that opened it), the ring takes the pointer
        // with it to its centre, as the original's world rings and every sub-ring do.
        if (_centre != at) RecenterPointer();
        Preview = [];
        _hover = -1;
        _dragging = false;
        MouseFilter = MouseFilterEnum.Stop;
        Visible = true;
        Rebuild();
        Animate(0.86f);
    }

    /// <summary>Set while the ring assigns to a quickbar slot (<see cref="OpenQuickbar"/>): choosing what a ring offers puts it there.</summary>
    Action<BarSlot?>? _assign;
    /// <summary>Whether the ring open now assigns to a quickbar slot (tests, skins).</summary>
    public bool Assigning => _assign != null;

    /// <summary>
    /// The ring a right-click on a quickbar slot opens (visual scene quickbarradial, docs/QUICKBAR_RADIAL.md): centred
    /// 100 px above the slot's top in the classic look (the custom menus' 154 px clamp from the screen edges), the
    /// pointer taken to its centre (a deliberate difference: the original leaves it on the slot). On a
    /// filled slot its contents sit on the large centre disc, with Examine north (items; the original also examines
    /// spells), "Use : name" south-east and Empty Quickslot south. On an empty slot nothing is in the centre; Custom Text
    /// Macro N, Assign Spell NE, Attack E, Assign Special Attacks SE, Assign Other Actions S, Assign Command SW, Assign
    /// Special Abilities W and Emote NW, the self radial's rings, whose choices are assigned to the slot instead of used.
    /// </summary>
    public void OpenQuickbar(Vector2 at, BarSlot? current, Action<BarSlot?> assign)
    {
        if (Session.Controlled == null) return;
        RadialEntry root;
        // The empty slot's ring has nothing in its centre (the modern look titles it).
        if (current == null) root = new RadialEntry { Label = "Empty slot", Hidden = true };
        else
        {
            var look = _user.Look(current);
            root = new RadialEntry { Label = look.Name, Look = look, Glyph = look.Glyph, Slot = current.Kind == "macro" ? current : null };
        }
        var slot = current;
        // The original leaves the pointer on the slot here (south option hovered) but recentres it on every sub-ring;
        // this ring deliberately takes the pointer to its centre on opening too (docs/QUICKBAR_RADIAL.md).
        OpenMenu(root, at, slot == null ? QuickbarAssignRoot : () => QuickbarSlotRing(slot));
        _assign = assign;
        _book.Refresh();
        if (Session.Content != null && _book.Spells.Count == 0) Session.Request("spellbook");
    }

    List<RadialEntry> QuickbarSlotRing(BarSlot slot)
    {
        var look = _user.Look(slot);
        var assign = _assign;
        // A text macro (quickbargaps i_ring): Edit Label E, Execute Command SE, Empty Quickslot S, Edit Command W.
        if (slot.Kind == "macro")
            return FixedRing(
                (2, Icon(new RadialEntry { Label = Tlk(6903, "Edit Label"), Glyph = "talk", Act = () => EditMacro(slot, MacroField.Label, assign) }, "ir_blank")),
                (3, Icon(new RadialEntry { Label = Tlk(6629, "Execute Command"), Glyph = "talk", Act = () => _user.Use(slot) }, "ir_cast")),
                (4, Icon(new RadialEntry { Label = Tlk(1389, "Empty Quickslot"), Glyph = "cancel", Sound = "gui_quick_erase", Act = () => assign?.Invoke(null) }, "ir_emptyqs")),
                (6, Icon(new RadialEntry { Label = Tlk(6902, "Edit Command"), Glyph = "talk", Act = () => EditMacro(slot, MacroField.Command, assign) }, "ir_blank")));
        var ring = FixedRing(
            (3, new RadialEntry
            {
                Label = $"Use : {look.Name}", Look = look, Glyph = look.Glyph, Disabled = !look.Usable,
                Act = () => _user.Use(slot),
            }),
            (4, Icon(new RadialEntry { Label = Tlk(1389, "Empty Quickslot"), Glyph = "cancel", Sound = "gui_quick_erase", Act = () => assign?.Invoke(null) }, "ir_emptyqs")));
        if (slot.Kind == "item" && slot.Item != 0)
        {
            uint item = slot.Item;
            ring[0] = Icon(new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Examine?.Invoke(item) }, "ir_examine");
        }
        // Spells, feats and skills open their description (quickbargaps: Light, Summon Familiar, Taunt).
        else if (slot.Kind is "spell" or "feat" or "skill" && slot.Arg != "form")
            ring[0] = Icon(new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Describe(slot, look) }, "ir_examine");
        return ring;
    }

    enum MacroField { Both, Label, Command }

    /// <summary>
    /// A text macro's prompts: Custom Text Macro asks for the label ("Enter Label") and then the line ("Input
    /// Command"), each in the original's util_editpopup (quickbargaps g_label, g_text); Edit Label and Edit Command ask
    /// for one, starting empty as the original's do (quickbarsound m_edit_label). The modern look edits both in one
    /// window, filled in. OK puts the macro on the slot; Cancel leaves it as it was.
    /// </summary>
    void EditMacro(BarSlot? current, MacroField field, Action<BarSlot?>? assign)
    {
        if (assign == null) return;
        string label = current?.Name ?? "", text = current?.Text ?? "";
        void Put(string l, string t) => assign(new BarSlot { Kind = "macro", Name = l, Text = t });
        if (Skin is { } skin)
        {
            string enter = Tlk(6679, "Enter Label"), input = Tlk(6628, "Input Command");
            bool shown = field switch
            {
                MacroField.Label => skin.Prompt(enter, "", l => Put(l, text)),
                MacroField.Command => skin.Prompt(input, "", t => Put(label, t)),
                _ => skin.Prompt(enter, "", l => skin.Prompt(input, "", t => Put(l, t))),
            };
            if (shown) return;
        }
        MacroEditor.Show(this, label, text, Put);
    }

    /// <summary>Examine on a spell, feat or skill slot: the look's description window, else the modern one.</summary>
    void Describe(BarSlot slot, SlotLook look)
    {
        if (Skin?.Describe(slot, look) == true) return;
        DescriptionWindow.Show(this, Session, slot, look);
    }

    List<RadialEntry> QuickbarAssignRoot()
    {
        var self = Session.Controlled;
        var assign = _assign;
        RadialEntry Cat(uint strref, string fallback, string glyph, string icon, Func<List<RadialEntry>> build)
        {
            var c = Category(Tlk(strref, fallback), glyph, build);
            return Icon(c, icon);
        }
        return FixedRing(
            // The original asks for a label, then the line (two util_editpopup prompts, quickbargaps).
            (0, Icon(new RadialEntry { Label = Tlk(1159, "Custom Text Macro"), Glyph = "talk", Sound = "gui_quick_add", Act = () => EditMacro(null, MacroField.Both, assign) }, "ir_textmacro")),
            (1, Cat(1158, "Assign Spell", "magic", "ir_cast", () => Spells(self, true))),
            (2, SlotEntry(new BarSlot { Kind = "command", Arg = "attack" }, null)),
            (3, Cat(1156, "Assign Special Attacks", "combat", "ir_xattack", () => self == null ? [] : CombatAbilities(self, true))),
            (4, Cat(67564, "Assign Other Actions", "cog", "ir_action", QuickbarOtherActions)),
            (5, Cat(1157, "Assign Command", "command", "ir_command", QuickbarCommands)),
            (6, Cat(1155, "Assign Special Abilities", "feat", "ir_xability", () => self == null ? [] : SpecialAbilities(self, true))),
            (7, Icon(Category("Emote", "emote", Emotes), "ir_emotes")));
    }

    /// <summary>
    /// In a quickbar slot's sub-rings: anything the bar can hold can be assigned, ready or not (a spell with no uses
    /// left, a feat used up); Craft becomes its quickbar command; what the bar can't hold (Counterspell, Possess
    /// Familiar, the DM's tools) is greyed.
    /// </summary>
    static void ForAssigning(List<RadialEntry> entries)
    {
        foreach (var e in entries)
        {
            if (e.Hidden || e.Open != null) continue;
            if (e.Slot == null && e.Look?.Icons.Contains("ir_craft") == true) e.Slot = new BarSlot { Kind = "command", Arg = "craft" };
            e.Disabled = e.Slot == null;
        }
    }

    /// <summary>
    /// Assign Other Actions (quickbarradial, quickbargaps f_other): Assign Examine N, Assign Talk To E, Assign Barter NW,
    /// and for a character with the skill Assign Animal Empathy S and Assign Pick Pocket SW (their skill icons) and
    /// Assign Open Lock W (ir_picklock); NE and SE stay empty.
    /// </summary>
    List<RadialEntry> QuickbarOtherActions()
    {
        RadialEntry Command(string arg, uint strref, string fallback, string icon)
        {
            var e = SlotEntry(new BarSlot { Kind = "command", Arg = arg }, null, Tlk(strref, fallback));
            return Icon(e, icon);
        }
        var ring = FixedRing((0, Command("examine", 67566, "Assign Examine", "ir_examine")),
            (2, Command("talk", 67565, "Assign Talk To", "ir_dialog")),
            (7, Command("barter", 67569, "Assign Barter", "ir_barter")));
        foreach (var (skill, direction, strref, fallback, icon) in new[] { (0, 4, 67567u, "Assign Animal Empathy", ""), (13, 5, 67568u, "Assign Pick Pocket", ""), (9, 6, 68656u, "Assign Open Lock", "ir_picklock") })
        {
            if (_book.Skill(skill) == null) continue;
            var entry = SlotEntry(new BarSlot { Kind = "skill", Id = skill }, null, Tlk(strref, fallback));
            ring[direction] = icon.Length > 0 ? Icon(entry, icon) : entry;
        }
        return ring;
    }

    /// <summary>Assign Command: the Issue Command ring's orders as quickbar commands (quickbarradial).</summary>
    List<RadialEntry> QuickbarCommands()
    {
        RadialEntry Command(string arg, uint strref, string fallback, string icon) =>
            Icon(SlotEntry(new BarSlot { Kind = "command", Arg = arg }, null, Tlk(strref, fallback)), icon);
        return FixedRing((2, Command("attacknearest", 5345, "Attack Nearest", "ir_attacknearest")),
            (3, Command("follow", 682, "Follow", "ir_follow")), (4, Command("standground", 1477, "Stand Your Ground", "ir_standgrnd")),
            (5, Command("guardme", 1474, "Guard Me", "ir_guard")), (6, Command("healme", 1483, "Heal Me", "ir_healme")));
    }

    void TileActionsReceived(NotifyMessage message)
    {
        if (message.Kind != "tile-actions" || !IsOpen || _ground is not { } point || message.Subject != Session.ControlledId) return;
        using var doc = JsonDocument.Parse(message.Json);
        var data = doc.RootElement;
        if (data.GetProperty("query").GetInt32() != _tileQuery) return;
        uint? area = Session.Area?.Info.AreaId;
        _tileActions = FixedRing();
        foreach (var item in data.GetProperty("entries").EnumerateArray())
        {
            int id = item.GetProperty("id").GetInt32();
            if (id < 1 || id > 8) continue;
            _tileActions[id - 1] = Icon(new RadialEntry
            {
                Label = item.GetProperty("name").GetString() ?? "", Glyph = "cog",
                Act = () => Session.Request("tile-action", new { id, area, position = new[] { point.X, point.Y, point.Z } }),
            }, item.GetProperty("icon").GetString() ?? "");
        }
        Rebuild();
    }

    void CloseForArea(LoadAreaMessage _) => Close();

    void CloseForDialog(DialogMessage m)
    {
        if (!m.Ended) Close();
    }

    public void Close()
    {
        if (!Visible) return;
        _tween?.Kill();
        Visible = false;
        _openingRelease = false;
        _target = null;
        _ground = null;
        _custom = false;
        _rootEntry = null;
        _assign = null;
        _tileQuery++;
        _tileActions = [];
        ClearInventoryContext();
        _stack.Clear();
    }

    void Push(RadialEntry entry)
    {
        _stack.Add(new Ring { Title = entry.Label, Build = entry.Open!, Parent = entry });
        RecenterPointer();
        _hover = -2;
        Rebuild();
        Animate(0.93f);
    }

    void Back()
    {
        if (_stack.Count <= 1) { Close(); return; }
        _stack.RemoveAt(_stack.Count - 1);
        RecenterPointer();
        _hover = -2;
        Rebuild();
        Animate(1.05f);
    }

    void RecenterPointer()
    {
        _pointer = _centre;
        if (Interaction.PointerOverride != null) Interaction.PointerOverride = _centre;
        else if (DisplayServer.WindowIsFocused() && !OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--capture", StringComparison.Ordinal) || a.StartsWith("--visual", StringComparison.Ordinal)))
            GetViewport().WarpMouse(_centre);
    }

    void Rebuild()
    {
        if (_stack.Count == 0) return;
        var ring = _stack[^1];
        ring.Entries = ring.Build();
        if (_assign != null && _stack.Count > 1) ForAssigning(ring.Entries);
        int pages = PageCount(ring);
        ring.Page = Math.Clamp(ring.Page, 0, pages - 1);
        if (pages <= 1) _shown = ring.Entries;
        else
        {
            int per = (int)MaxPerRing - 1;
            _shown = ring.Entries.Skip(ring.Page * per).Take(per).ToList();
            _shown.Add(new RadialEntry
            {
                Label = "More", Detail = $"Page {ring.Page + 1} of {pages}", Glyph = "menu",
                Act = () => { ring.Page = (ring.Page + 1) % pages; _hover = -1; Rebuild(); Animate(0.97f); },
            });
        }
        RefreshPreview();
        UpdateShader();
        _overlay.QueueRedraw();
    }

    void RefreshPreview() => Preview = _hover >= 0 && _hover < _shown.Count
        ? _shown[_hover].Open?.Invoke() ?? [] : [];

    int PageCount(Ring r) => r.Entries.Count <= MaxPerRing ? 1 : (int)Math.Ceiling(r.Entries.Count / (MaxPerRing - 1));

    void Animate(float fromScale)
    {
        _tween?.Kill();
        _ring.PivotOffset = _ring.Size / 2;
        _ring.Scale = new Vector2(fromScale, fromScale);
        _ring.Modulate = new Color(1, 1, 1, 0);
        _overlay.Modulate = new Color(1, 1, 1, 0);
        _tween = CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _tween.TweenProperty(_ring, "scale", Vector2.One, 0.15);
        _tween.TweenProperty(_ring, "modulate:a", 1f, 0.12);
        _tween.TweenProperty(_overlay, "modulate:a", 1f, 0.14);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        // The target went away, or the player now controls something else (possession): close.
        if (Session.Controlled == null || (_target == null && _ground == null && _inventoryItem == null && !_custom)
            || (_target != null && !IsInstanceValid(_target))
            || (_inventoryItem != null && (_inventoryValid?.Invoke() != true || Session.Area != _inventoryArea || Session.PendingArea != null))
            || (_foreignItem != null && _foreignValid?.Invoke() != true)
            || Session.ControlledId != _controlledAtOpen) { Close(); return; }
        _ring.Position = _centre - _ring.Size / 2;
    }

    // ------------------------------------------------------------------ input

    int HitTest(Vector2 p)
    {
        var d = p - _centre;
        float r = d.Length();
        if (r < CentreR + 2) return -2;
        if (r < Inner - 6 || r > Outer + 40 || _shown.Count == 0) return -1;
        float a = MathF.Atan2(d.X, -d.Y);
        if (a < 0) a += MathF.Tau;
        float seg = MathF.Tau / Wedges;
        int i = (int)MathF.Floor((a + seg / 2) / seg) % Wedges;
        return i < _shown.Count && !_shown[i].Hidden ? i : -1;
    }

    void BeginPointerSelection(Vector2 at)
    {
        _openingRelease = true;
        _openPoint = at;
    }

    public override void _Input(InputEvent input)
    {
        if (!IsOpen || input is not (InputEventMouseMotion or InputEventMouseButton)) return;
        // Keep normal GUI left-button drag handling (e.g. dragging a wedge onto the quickbar).
        if (!PointerOwned && input is InputEventMouseButton { ButtonIndex: MouseButton.Left }) return;
        if (!PointerOwned && input is InputEventMouseMotion && !_openingRelease) return;
        // (Read before handling it: choosing a wedge closes the menu.)
        bool releaseToOpener = _openingRelease && _custom && !ItemContext && input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false };
        // The control that opened the menu can retain GUI mouse focus. Handle the
        // held release here for every target, including inventory and portraits.
        HandlePointer(input);
        // A custom menu's opener (the chat bar) lets go of the GUI's mouse focus only when it sees the release;
        // otherwise the next click anywhere would go to it.
        if (releaseToOpener) return;
        GetViewport().SetInputAsHandled();
    }

    public override void _GuiInput(InputEvent input) => HandlePointer(input);

    void HandlePointer(InputEvent input)
    {
        if (input is InputEventMouseMotion mm)
        {
            _pointer = mm.Position;
            int h = HitTest(mm.Position);
            if (h != _hover) { _hover = h; RefreshPreview(); _mat.SetShaderParameter("hovered", h); }
            _overlay.QueueRedraw();
            return;
        }
        if (input is not InputEventMouseButton mb) return;
        if (mb.ButtonIndex == MouseButton.Right)
        {
            if (mb.Pressed) { _openingRelease = false; Back(); return; }
            if (!_openingRelease) return;
            _openingRelease = false;
            int heldHit = HitTest(mb.Position);
            // Clamping the ring at a screen edge must not turn a plain click into a choice.
            if (heldHit >= 0 && mb.Position.DistanceTo(_openPoint) > 2) Choose(_shown[heldHit]);
            return;
        }
        if (mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
        int hit = HitTest(mb.Position);
        if (hit == -1) { Close(); return; }
        if (hit == -2) { Back(); return; }
        Choose(_shown[hit]);
    }

    void Choose(RadialEntry entry)
    {
        if (entry.Disabled || entry.Hidden) return;
        // Putting something on a quickbar slot sounds gui_quick_add instead of the radial's choice (quickbarsound).
        string? sound = entry.Sound ?? (_assign != null && entry.Slot != null && entry.Open == null ? "gui_quick_add" : null);
        if (sound != null && Session.Content is { } own) Classic.ClassicSounds.Play(Classic.ClassicAssets.For(own), sound);
        else if (Skin != null) Skin.Selected();
        else if (Session.Content is { } content) Classic.ClassicSounds.Play(Classic.ClassicAssets.For(content), "gui_radial_slect");
        if (entry.Open != null) { Push(entry); return; }
        var act = entry.Act;
        // Assigning to a quickbar slot: what the rings offer goes on the slot instead of being used.
        if (_assign is { } assign && entry.Slot is { } chosen) act = () => assign(chosen.Clone());
        if (entry.Label != "More") Close();
        act?.Invoke();
    }

    /// <summary>Picks the wedge with this label (screenshots, tests); false when there is none.</summary>
    public bool Choose(string label)
    {
        var e = _shown.FirstOrDefault(x => x.Label == label);
        if (e == null || e.Disabled) return false;
        Choose(e);
        return true;
    }

    /// <summary>The labels of the wedges shown now (tests).</summary>
    public IReadOnlyList<string> Labels => _shown.Select(e => e.Label).ToList();

    /// <summary>The screen point in the middle of the wedge with this label, to click it (tests); null when there is none.</summary>
    public Vector2? WedgePoint(string label)
    {
        int i = _shown.FindIndex(e => e.Label == label);
        if (i < 0) return null;
        float a = i * MathF.Tau / Wedges, r = (Inner + Outer) / 2;
        return _centre + new Vector2(MathF.Sin(a), -MathF.Cos(a)) * r;
    }

    /// <summary>Highlights a wedge as if the mouse were over it (screenshots).</summary>
    public void CaptureHover(int index)
    {
        _hover = index < _shown.Count ? index : -1;
        RefreshPreview();
        _mat.SetShaderParameter("hovered", _hover);
        _overlay.QueueRedraw();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (!Visible || e is not InputEventKey { Pressed: true } k) return;
        if (k.Keycode == Key.Escape) { Close(); GetViewport().SetInputAsHandled(); }
        else if (k.Keycode == Key.Backspace || Preferences.InputBindings.Pressed(e, Preferences.ActionCatalog.RadialBack)) { Back(); GetViewport().SetInputAsHandled(); }
        else if (NumpadDirection(e) is { } dir)
        {
            // The numpad picks the wedge in that direction (8 up, 6 right, ...), as in the original.
            int i = Gameplay.RadialNav.Wedge(Wedges, _shown.Count, dir);
            if (i >= 0) Choose(_shown[i]);
            GetViewport().SetInputAsHandled();
        }
        else if (k.Keycode is >= Key.Key1 and <= Key.Key9 && (int)(k.Keycode - Key.Key1) < _shown.Count)
        {
            Choose(_shown[(int)(k.Keycode - Key.Key1)]);
            GetViewport().SetInputAsHandled();
        }
    }

    static Preferences.RadialDirection? NumpadDirection(InputEvent e)
    {
        foreach (var (dir, _, _) in Preferences.ActionCatalog.RadialKeys)
            if (Preferences.InputBindings.Pressed(e, Preferences.ActionCatalog.Radial(dir))) return dir;
        return null;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        int h = HitTest(atPosition);
        if (h < 0 || _shown[h].Slot is not { } slot) return default;
        // Get out of the way so the action bar underneath can take the drop.
        _dragging = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _ring.Modulate = _overlay.Modulate = new Color(1, 1, 1, 0.12f);
        SetDragPreview(SlotPreview.Centered(_user.Look(slot), Session.Content));
        return slot.ToDrag();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd && _dragging) { _dragging = false; Close(); }
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>Rings show at least this many wedges (empty ones faint) so a short list still reads as a wheel.</summary>
    const int MinWedges = 6;
    int Wedges => Skin != null ? Skin.Slots : Math.Max(MinWedges, _shown.Count);

    void UpdateShader()
    {
        int n = Wedges;
        var disabled = new float[MaskSize];
        var active = new float[MaskSize];
        for (int i = 0; i < Math.Min(MaskSize, _shown.Count); i++)
        {
            disabled[i] = _shown[i].Disabled || _shown[i].Hidden ? 1 : 0;
            active[i] = _shown[i].Active ? 1 : 0;
        }
        _mat.SetShaderParameter("count", n);
        _mat.SetShaderParameter("used", _shown.Count);
        _mat.SetShaderParameter("hovered", _hover);
        _mat.SetShaderParameter("size_px", _ring.Size.X);
        _mat.SetShaderParameter("inner_px", Inner);
        _mat.SetShaderParameter("outer_px", Outer);
        _mat.SetShaderParameter("centre_px", CentreR);
        _mat.SetShaderParameter("disabled_mask", disabled);
        _mat.SetShaderParameter("active_mask", active);
        _mat.SetShaderParameter("glass", Styles.GlassEnabled ? 1f : 0f);
        _mat.SetShaderParameter("fill", Palette.Bg2 with { A = 0.86f });
        _mat.SetShaderParameter("fill_hover", Palette.Bg4 with { A = 0.95f });
        _mat.SetShaderParameter("fill_active", Palette.AccentDim with { A = 0.55f });
        _mat.SetShaderParameter("edge", Palette.LineStrong);
        _mat.SetShaderParameter("edge_hover", Palette.Accent);
        _mat.SetShaderParameter("centre_edge", Palette.AccentDim);
    }

    void DrawOverlay()
    {
        var o = _overlay;
        if (Skin != null) { Skin.Draw(o, this); return; }
        int n = _shown.Count;
        if (n == 0) return;
        if (_pointer.DistanceTo(_centre) > 2) o.DrawLine(_centre, _pointer, Colors.White, 1, true);
        float seg = MathF.Tau / Wedges;
        float mid = (Inner + Outer) / 2 + 2;
        float arc = seg * mid;
        float iconSize = Math.Clamp(arc * 0.5f, 26, 42);
        var content = Session.Content;
        var font = Fonts.UiMedium;
        for (int i = 0; i < n; i++)
        {
            var e = _shown[i];
            if (e.Hidden) continue;
            float a = i * seg;
            var dir = new Vector2(MathF.Sin(a), -MathF.Cos(a));
            var c = _centre + dir * (mid - 7);
            var rect = new Rect2(c - new Vector2(iconSize, iconSize) / 2, new Vector2(iconSize, iconSize));
            bool hovered = i == _hover;
            if (hovered) rect = rect.Grow(2);
            var mod = e.Disabled ? new Color(0.5f, 0.48f, 0.46f, 0.8f) : Colors.White;
            var look = e.Look ?? new SlotLook(e.Label, [], e.Glyph, -1, e.Active, !e.Disabled, "", "");
            bool gameIcon = look.Icons.Any(x => x.Length > 0 && content?.Textures.Get(x) != null);
            if (gameIcon) o.DrawStyleBox(Styles.Box(new Color(0, 0, 0, 0.45f), 5, hovered ? Palette.Accent : Palette.Line, 1, 0), rect.Grow(2));
            SlotArt.DrawIcon(o, content, look, rect, mod, e.Disabled ? Palette.TextMuted : hovered ? Palette.AccentBright : Palette.Accent);
            if (look.Uses >= 0) SlotArt.DrawBadge(o, look.Uses.ToString(), rect.Grow(3), look.Uses == 0 ? Palette.Danger : Palette.Text, 11);
            if (e.Open != null) DrawChevron(o, _centre + dir * (Outer - 9), a, hovered ? Palette.AccentBright : Palette.AccentDim);
            if (e.Active) o.DrawCircle(rect.Position + new Vector2(rect.Size.X - 1, 1), 3.5f, Palette.Success);

            // Short label under the icon.
            const int fs = 11;
            string label = Fit(font, e.Label, arc - 10, fs);
            var ts = font.GetStringSize(label, HorizontalAlignment.Left, -1, fs);
            var lp = new Vector2(c.X - ts.X / 2, rect.End.Y + 12);
            o.DrawStringOutline(font, lp, label, HorizontalAlignment.Left, -1, fs, 3, new Color(0, 0, 0, 0.7f));
            o.DrawString(font, lp, label, HorizontalAlignment.Left, -1, fs, e.Disabled ? Palette.TextMuted : hovered ? Palette.AccentBright : Palette.TextSecondary);
        }

        // Centre: what's hovered, else where we are.
        var ring = _stack.Count > 0 ? _stack[^1] : null;
        string title, detail;
        Color detailColor = Palette.TextMuted;
        if (_hover >= 0 && _hover < n)
        {
            var e = _shown[_hover];
            title = e.Label;
            detail = e.Detail.Length > 0 ? e.Detail : e.Open != null ? "Open" : "";
            if (e.Disabled) detailColor = Palette.Danger;
            else if (e.Active) detailColor = Palette.Success;
        }
        else
        {
            title = ring?.Title ?? "";
            detail = _stack.Count > 1 ? "Back" : "Close";
        }
        if (_stack.Count == 1 && _hover < 0 && content != null && TargetItemArt is { } item)
        {
            var area = new Rect2(_centre + new Vector2(-32, -42), new Vector2(64, 64));
            Classic.ClassicItemArt.DrawIcon(o, Classic.ClassicAssets.For(content), item,
                area, Colors.White);
            if (Classic.ClassicItemArt.CountOf(item) > 1) SlotArt.DrawBadge(o, item.Stack.ToString(), area);
        }
        else DrawCentred(o, Fonts.UiBold, title, _centre + new Vector2(0, -2), 13, Palette.Text, CentreR * 2 - 14, 2);
        DrawCentred(o, Fonts.UiMedium, detail, _centre + new Vector2(0, 26), 10, _hover == -2 ? Palette.AccentBright : detailColor, CentreR * 2 - 14, 1);

        // Breadcrumb above the ring.
        var crumbs = string.Join("  ›  ", _stack.Select(s => s.Title));
        var cf = Fonts.Display;
        var cs = cf.GetStringSize(crumbs, HorizontalAlignment.Left, -1, 15);
        var cp = new Vector2(_centre.X - cs.X / 2, _centre.Y - Outer - 26);
        o.DrawStringOutline(cf, cp, crumbs, HorizontalAlignment.Left, -1, 15, 5, new Color(0, 0, 0, 0.75f));
        o.DrawString(cf, cp, crumbs, HorizontalAlignment.Left, -1, 15, Palette.AccentBright);
        var hint = ItemContext ? "Release over a command or click to choose"
            : _assign != null ? "Click to put it on the slot · right-click to go back"
            : _shown.Any(x => x.Slot != null) ? "Click to use · drag onto the action bar · right-click to go back" : "Click to choose · right-click to go back";
        var hf = Fonts.Ui;
        var hs = hf.GetStringSize(hint, HorizontalAlignment.Left, -1, 11);
        var hp = new Vector2(_centre.X - hs.X / 2, _centre.Y + Outer + 38);
        o.DrawStyleBox(Styles.Box(Palette.Bg0 with { A = 0.72f }, 11, Palette.Line, 1, 0), new Rect2(hp + new Vector2(-12, -15), new Vector2(hs.X + 24, 22)));
        o.DrawString(hf, hp, hint, HorizontalAlignment.Left, -1, 11, Palette.TextSecondary);
    }

    static void DrawChevron(Control o, Vector2 at, float angle, Color color)
    {
        var dir = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));
        var side = new Vector2(-dir.Y, dir.X);
        o.DrawPolyline(new[] { at - dir * 2.5f + side * 4, at + dir * 2.5f, at - dir * 2.5f - side * 4 }, color, 1.6f, true);
    }

    /// <summary>Text centred on a point, wrapped to at most <paramref name="lines"/> lines of <paramref name="width"/>.</summary>
    static void DrawCentred(Control o, Font font, string text, Vector2 at, int size, Color color, float width, int lines)
    {
        if (text.Length == 0) return;
        var words = text.Split(' ');
        var rows = new List<string>();
        var cur = "";
        foreach (var w in words)
        {
            var next = cur.Length == 0 ? w : cur + " " + w;
            if (font.GetStringSize(next, HorizontalAlignment.Left, -1, size).X > width && cur.Length > 0) { rows.Add(cur); cur = w; }
            else cur = next;
        }
        if (cur.Length > 0) rows.Add(cur);
        if (rows.Count > lines) { rows = rows.Take(lines).ToList(); rows[^1] = Fit(font, rows[^1] + "…", width, size); }
        float lh = size + 3;
        float y = at.Y - (rows.Count - 1) * lh / 2;
        foreach (var row in rows)
        {
            var s = Fit(font, row, width, size);
            var ts = font.GetStringSize(s, HorizontalAlignment.Left, -1, size);
            o.DrawString(font, new Vector2(at.X - ts.X / 2, y + size * 0.35f), s, HorizontalAlignment.Left, -1, size, color);
            y += lh;
        }
    }

    static string Fit(Font font, string s, float width, int size)
    {
        if (font.GetStringSize(s, HorizontalAlignment.Left, -1, size).X <= width) return s;
        while (s.Length > 1 && font.GetStringSize(s + "…", HorizontalAlignment.Left, -1, size).X > width) s = s[..^1];
        return s.TrimEnd() + "…";
    }

    // ------------------------------------------------------------------ what's on the rings

    static string Name(ObjectView v) => v.DisplayName.Length > 0 ? NwnText.Strip(v.DisplayName) : v.Kind.ToString();

    JsonElement? Facts => (_target?.ObjectId ?? _inventoryItem?.Id) is { } id && _book.TargetFacts.TryGetValue(id, out var f) ? f : null;
    bool Fact(string name) => Facts is { } f && f.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>The target-type bits this object answers to.</summary>
    int TargetBits(ObjectView? t, bool self) => t == null ? AbilityUser.TargetGround : self ? AbilityUser.TargetSelf | AbilityUser.TargetCreature : t.Kind switch
    {
        ObjectKind.Creature => AbilityUser.TargetCreature,
        ObjectKind.Door => AbilityUser.TargetDoor,
        ObjectKind.Placeable => AbilityUser.TargetPlaceable,
        ObjectKind.Item => AbilityUser.TargetItem,
        ObjectKind.Trigger => AbilityUser.TargetTrigger,
        _ => 0,
    };

    List<RadialEntry> GroundRoot()
    {
        // Choose closes the menu before invoking Act, so retain this point in the closure.
        var point = _ground;
        return FixedRing(
            (2, Icon(new RadialEntry
            {
                Label = "Move To", Glyph = "move", Disabled = point == null,
                Act = () =>
                {
                    if (point is { } destination)
                        Session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = destination, Run = false }.Write());
                },
            }, "ir_moveto")),
            (3, Icon(new RadialEntry
            {
                Label = Session.Content?.Resources.TlkString(66848) ?? "More Actions", Glyph = "cog", Open = () => _tileActions,
            }, "ir_tileaction")),
            (1, Category("Cast Spell", "magic", () => Spells(null, false))),
            (6, Category("Special Abilities", "feat", () => Feats(null, false)
                .Concat(new[] { Category("Items", "bag", () => Items(null, false)) }).ToList())));
    }

    List<RadialEntry> Root(ObjectView t, bool self)
    {
        var list = new List<RadialEntry>();
        bool dead = (t.Flags & ObjectFlags.Dead) != 0 || Fact("dead");
        bool hostile = (t.Flags & ObjectFlags.Hostile) != 0 || Fact("hostile");
        void Add(string label, string glyph, Action act, string detail = "") =>
            list.Add(new RadialEntry { Label = label, Glyph = glyph, Act = act, Detail = detail });

        if (self)
        {
            list = FixedRing(
                (0, new RadialEntry { Label = "Character Sheet", Glyph = "character", Act = () => (GetParent() as Hud)?.OpenCharacter?.Invoke() }),
                (1, Category("Cast Spell", "magic", () => Spells(t, true))),
                (2, SlotEntry(new BarSlot { Kind = "command", Arg = "attack" }, null)),
                (3, Category("Special Attacks", "combat", () => CombatAbilities(t, true))),
                (5, Category("Issue Command", "command", Commands)),
                (6, Category("Special Abilities", "feat", () => SpecialAbilities(t, true))),
                (7, Category("Emotes", "emote", Emotes)));
            if (Fact("canUnpossessFamiliar"))
                list[4] = FamiliarEntry(false);
            else if (Fact("canCancelPolymorph"))
                list[4] = new RadialEntry { Label = "Cancel Polymorph", Glyph = "cancel", Act = () => Session.Request("cancel-polymorph") };
        }
        else
        {
            Add("Examine", "eye", () => Examine?.Invoke(t.ObjectId));
            switch (t.Kind)
            {
                case ObjectKind.Creature:
                    if (!dead && !hostile) Add("Talk", "talk", () => Session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = t.ObjectId }.Write()));
                    if (!dead) Add("Attack", "attack", () => Session.Request("attack", new { target = t.ObjectId }), hostile ? "Hostile" : "Not hostile");
                    if (!dead && Fact("injured") && _book.Skill(4) is { } heal)
                        list.Add(SkillEntry(heal, t, "heal"));
                    break;
                case ObjectKind.Door:
                case ObjectKind.Placeable:
                    var doorAction = DoorMenuAction(t);
                    Add(t.Kind == ObjectKind.Door ? ((t.Flags & ObjectFlags.Open) != 0 ? "Close" : "Open") : Fact("hasInventory") ? "Open" : "Use",
                        t.Kind == ObjectKind.Door ? "key" : Fact("hasInventory") ? "chest" : "use",
                        () => Session.Send(new ActionRequestMessage { Action = doorAction, Target = t.ObjectId }.Write()),
                        (t.Flags & ObjectFlags.Locked) != 0 || Fact("locked") ? "Locked" : "");
                    if (((t.Flags & ObjectFlags.Locked) != 0 || Fact("locked")) && _book.Skill(9) is { } openLock)
                        list.Add(SkillEntry(openLock, t, "lockpick"));
                    Add(t.Kind == ObjectKind.Door ? "Bash" : "Attack", "attack", () => Session.Request("attack", new { target = t.ObjectId }));
                    break;
                case ObjectKind.Item:
                    Add("Pick up", "pickup", () => Session.Request("pickup", new { item = t.ObjectId }));
                    break;
            }
            if (Fact("trapped") && _book.Skill(2) is { } disarm)
                list.Add(new RadialEntry
                {
                    Label = "Trap", Glyph = "trap", Detail = "Disarm, examine, flag or recover",
                    Open = () => disarm.Subs.Select(s => SlotEntry(new BarSlot { Kind = "skill", Id = 2, Sub = s.Id }, t, s.Name, "trap",
                        s.Id == 101 && !Fact("trapRecoverable") || (s.Id is 0 or 101) && !Fact("trapDisarmable"))).ToList(),
                });
            if (t.Kind is ObjectKind.Creature or ObjectKind.Door or ObjectKind.Placeable or ObjectKind.Item)
            {
                list.Add(Category("Spells", "spellbook", () => Spells(t, false)));
                if (t.Kind == ObjectKind.Creature) list.Add(Category("Feats", "feat", () => Feats(t, false)));
                list.Add(Category("Skills", "skill", () => Skills(t, false)));
                list.Add(Category("Items", "bag", () => Items(t, false)));
            }
        }
        if (!self && t.Kind == ObjectKind.Item)
        {
            list = FixedRing(
                (0, new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Examine?.Invoke(t.ObjectId) }),
                (1, Category("Cast Spell", "magic", () => Spells(t, false))),
                (4, Icon(new RadialEntry { Label = "Pick up", Glyph = "pickup", Act = () => Session.Request("pickup", new { item = t.ObjectId }) }, "ir_pickup")));
            if (Fact("equipGround"))
                list[2] = Icon(new RadialEntry { Label = "Equip", Glyph = "weapon", Act = () => Session.Request("equip-ground", new { item = t.ObjectId }) }, "ir_equip_r");
            if (Fact("equipOffhand"))
                list[6] = Icon(new RadialEntry { Label = "Equip Offhand", Glyph = "offhand", Act = () => Session.Request("equip-ground", new { item = t.ObjectId, offhand = true }) }, "ir_equip_l");
        }
        if (!self && t.Kind == ObjectKind.Creature)
        {
            var extra = list.Where(e => e.Label is "Talk" or "Trap").ToList();
            list = FixedRing(
                (0, new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Examine?.Invoke(t.ObjectId) }),
                (1, Category("Cast Spell", "magic", () => Spells(t, false))),
                (3, Category("Special Attacks", "combat", () => CombatAbilities(t, false))),
                (6, Category("Special Abilities", "feat", () => SpecialAbilities(t, false).Concat(extra).ToList())));
            if (dead && (t.Flags & ObjectFlags.Useable) != 0)
                list[4] = new RadialEntry { Label = "Open", Glyph = "chest", Act = () => Session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = t.ObjectId }.Write()) };
            if (!dead) list[2] = new RadialEntry { Label = "Attack", Glyph = "attack", Act = () => Session.Request("attack", new { target = t.ObjectId }) };
            // The player's own associate: its commands replace Attack and Special Attacks (docs/ASSOCIATE_COMMANDS.md).
            if (!dead && Fact("associate")) list = AssociateRoot(t);
            // Another player: the Party ring at NW (docs/PARTY.md; the place is inferred).
            else if (PartyEntry(t) is { } party && list[7].Hidden)
            {
                list[7] = party;
                // Barter (dialog.tlk 6835, ir_barter) on another player; its place on the ring is inferred (docs/BARTER.md).
                if (list[5].Hidden)
                    list[5] = Icon(new RadialEntry { Label = Tlk(6835, "Barter"), Glyph = "party", Act = () => Session.Request("barter-request", new { target = t.ObjectId }) }, "ir_barter");
            }
        }
        if (!self && t.Kind is ObjectKind.Door or ObjectKind.Placeable)
        {
            bool door = t.Kind == ObjectKind.Door;
            bool locked = (t.Flags & ObjectFlags.Locked) != 0 || Fact("locked");
            bool open = (t.Flags & ObjectFlags.Open) != 0;
            var doorAction = DoorMenuAction(t);
            list = FixedRing(
                (0, new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Examine?.Invoke(t.ObjectId) }),
                (1, Category("Cast Spell", "magic", () => Spells(t, false))),
                (2, Icon(new RadialEntry
                {
                    Label = door ? open ? "Close" : "Open" : "Use", Glyph = "use",
                    Act = () => Session.Send(new ActionRequestMessage { Action = doorAction, Target = t.ObjectId }.Write()),
                }, door ? open ? "ir_close" : "ir_open" : "ir_use")),
                (3, new RadialEntry
                {
                    Label = "Trap", Glyph = "trap", Disabled = !Fact("trapped") || _book.Skill(2) == null,
                    Open = () => TrapCommands(t),
                }),
                (5, Icon(new RadialEntry { Label = "Bash", Glyph = "attack", Act = () => Session.Request("attack", new { target = t.ObjectId }) }, "ir_bash")));
            if (!door || locked || Fact("lockable"))
                list[6] = Icon(new RadialEntry
                {
                    Label = locked ? "Unlock" : "Lock", Glyph = locked ? "unlock" : "lock",
                    Disabled = !locked && !Fact("lockable"),
                    Act = () => Session.Request("lock-object", new { target = t.ObjectId, locked = !locked }),
                }, locked ? "ir_unlock" : "ir_lock");
        }
        if (Interaction.ExtendMenu != null && Dm(t) is { Count: > 0 } dm)
        {
            var tools = new RadialEntry { Label = "DM tools", Glyph = "magic", Detail = $"Dungeon Master · {dm.Count} tools", Open = () => Dm(t) };
            int free = list.FindIndex(entry => entry.Hidden);
            if (free >= 0) list[free] = tools;
            else if (list.Count == 8 && list[6].Open is { } abilities)
            {
                // Polymorph can occupy the last free self wedge. Keep the eight directions intact
                // and make the DM tools reachable through the existing Special Abilities category.
                // Take the sub-ring's first free wedge so it stays eight fixed directions.
                list[6].Open = () =>
                {
                    var ring = abilities();
                    int slot = ring.FindIndex(entry => entry.Hidden);
                    if (slot >= 0) ring[slot] = tools; else ring.Add(tools);
                    return ring;
                };
            }
            else list.Add(tools); // Non-fixed object menus have no directional contract.
        }
        return list;
    }

    List<RadialEntry> TrapCommands(ObjectView target)
    {
        bool available = Fact("trapped") && _book.Skill(2) != null;
        RadialEntry Choice(int sub, string label) => SlotEntry(new BarSlot { Kind = "skill", Id = 2, Sub = sub }, target,
            label, "trap", !available || sub == 101 && !Fact("trapRecoverable")
                || sub is 0 or 101 && !Fact("trapDisarmable"));
        return FixedRing((0, Choice(102, "Examine")), (2, Choice(0, "Disable Trap")),
            (4, Choice(100, "Flag Trap")), (6, Choice(101, "Recover Trap")));
    }

    /// <summary>
    /// The radial menu of a detected trap trigger (triggers aren't objects on the client): straight to the four trap
    /// commands, Examine north, Disable Trap east, Flag Trap south, Recover Trap west, sent with the trap's id
    /// (traplook g_radial_root; the same ring as a trapped chest's Trap entry).
    /// </summary>
    public void OpenTrap(Views.WorldFx.TrapFx.Shown trap, Vector2 at)
    {
        // No centre icon (traplook g_radial_root shows only the ring's dot there).
        var root = new RadialEntry { Label = trap.Name, Glyph = "trap", Hidden = true };
        OpenMenu(root, at, () =>
        {
            bool available = _book.Skill(2) != null;
            RadialEntry Choice(int sub, string label)
            {
                var entry = SlotEntry(new BarSlot { Kind = "skill", Id = 2, Sub = sub }, null, label, "trap",
                    !available || sub == 101 && !trap.Recoverable || sub is 0 or 101 && !trap.Disarmable);
                entry.Act = () => Session.Request("use-skill", new { skill = 2, sub, target = trap.Id });
                return entry;
            }
            return FixedRing((0, Choice(102, "Examine")), (2, Choice(0, "Disable Trap")),
                (4, Choice(100, "Flag Trap")), (6, Choice(101, "Recover Trap")));
        });
    }

    internal static List<RadialEntry> FixedRing(params (int Direction, RadialEntry Entry)[] entries)
    {
        var ring = Enumerable.Range(0, 8).Select(_ => new RadialEntry { Hidden = true, Disabled = true }).ToList();
        foreach (var (direction, entry) in entries) ring[direction] = entry;
        return ring;
    }

    internal static RadialEntry Icon(RadialEntry entry, string icon)
    {
        entry.Look = (entry.Look ?? new SlotLook(entry.Label, [], entry.Glyph, -1, entry.Active, !entry.Disabled, "", "")) with { Icons = [icon] };
        return entry;
    }

    List<RadialEntry> Commands()
    {
        RadialEntry Command(string label, string icon, int voice) => Icon(new RadialEntry
        {
            Label = label, Glyph = "command", Act = () => Session.Request("voice-chat", new { id = voice }),
        }, icon);
        // The same authenticated associate command request used by the voice menu: sound-set slot 0 Attack, 22 Follow me,
        // 10 Hold, 9 Guard me, 4 Heal me (QuickChat.AssociateCommand gives ASSOCIATE_COMMAND_* -3, -5, -2, -7, -4).
        return FixedRing((2, Command("Attack Nearest", "ir_attacknearest", 0)),
            (3, Command("Follow", "ir_follow", 22)), (4, Command("Stand Your Ground", "ir_standgrnd", 10)),
            (5, Command("Guard Me", "ir_guard", 9)), (6, Command("Heal Me", "ir_healme", 4)));
    }

    string Tlk(uint strref, string fallback) => Session.Content?.Resources.TlkString(strref) is { Length: > 0 } s ? s : fallback;

    /// <summary>An order to one associate (server "associate-command"; ASSOCIATE_COMMAND_*).</summary>
    RadialEntry Order(ObjectView t, int command, uint strref, string fallback, string icon) => Icon(new RadialEntry
    {
        Label = Tlk(strref, fallback), Glyph = "command",
        Act = () => Session.Request("associate-command", new { target = t.ObjectId, command }),
    }, icon);

    /// <summary>
    /// The radial on the player's own associate, measured on the original for a summon, familiar, henchman and dominated
    /// creature, in the world and on the party portrait (associateradial, associatesubmenus, associatedominated): Heal Me N,
    /// Cast Spell NE, Attack Nearest E, Follow SE, Stand Your Ground S, Guard Me SW, Special Abilities W and the
    /// associate's own actions NW, with no Attack or Special Attacks.
    /// </summary>
    List<RadialEntry> AssociateRoot(ObjectView t) => FixedRing(
        (0, Order(t, -4, 1483, "Heal Me", "ir_healme")),
        (1, Category("Cast Spell", "magic", () => Spells(t, false))),
        (2, Order(t, -3, 5345, "Attack Nearest", "ir_attacknearest")),
        (3, Order(t, -5, 682, "Follow", "ir_follow")),
        (4, Order(t, -2, 1477, "Stand Your Ground", "ir_standgrnd")),
        (5, Order(t, -7, 1474, "Guard Me", "ir_guard")),
        (6, Category("Special Abilities", "feat", () => AssociateAbilities(t))),
        // The NW label was not readable in the captures; TLK 1478 sits with the measured command strings.
        (7, Icon(new RadialEntry { Label = Tlk(1478, "Non-Combat Commands"), Glyph = "command", Open = () => AssociateActions(t) }, "ir_assoc_action")));

    /// <summary>
    /// The associate's NW ring: Examine N, then by type Unsummon SE (summon, familiar; animal companion assumed),
    /// Possess Familiar E, Release from Domination SE, or the henchman's eight orders. Labels for Disarm Trap and Inventory
    /// are the matching TLK strings; the captures show only icons.
    /// </summary>
    List<RadialEntry> AssociateActions(ObjectView t)
    {
        var ring = FixedRing((0, new RadialEntry { Label = "Examine", Glyph = "eye", Act = () => Examine?.Invoke(t.ObjectId) }));
        int type = Facts is { } f && f.TryGetProperty("associateType", out var v) && v.TryGetInt32(out var n) ? n : 0;
        switch (type)
        {
            case 1: // henchman
                ring[1] = Order(t, -18, 1467, "Pick Lock", "ir_picklock");
                ring[2] = Order(t, -19, 3731, "Inventory", "ir_inventory");
                ring[3] = Order(t, -17, 1068, "Remove From Party", "ir_kick");
                ring[4] = Order(t, -20, 3842, "Disarm trap", "ir_hand_trap");
                ring[5] = Order(t, -21, 83327, "Toggle Spell-Casting", "ir_spell");
                ring[6] = Order(t, -22, 83410, "Toggle Stealth Mode", "ir_stealth");
                ring[7] = Order(t, -23, 83411, "Toggle Search Mode", "ir_search");
                break;
            case 2: ring[3] = Order(t, -9, 1569, "Unsummon", "ir_unsummon"); break;
            case 3:
                ring[2] = FamiliarEntry(true);
                if (!Fact("canPossessFamiliar")) { ring[2].Disabled = true; ring[2].Look = ring[2].Look! with { Usable = false }; }
                ring[3] = Order(t, -8, 1569, "Unsummon", "ir_unsummon");
                break;
            case 4: ring[3] = Order(t, -10, 1569, "Unsummon", "ir_unsummon"); break;
            case 5: ring[3] = Order(t, -12, 6393, "Release from Domination", "ir_reldom"); break;
        }
        return ring;
    }

    /// <summary>
    /// The associate's W ring is the player's own Special Abilities ring (Craft N, Taunt NE, Search E, Summon/Possess
    /// Familiar SW, Stealth W), with Taunt offered against the associate: the original offers it and its server refuses
    /// it with the PvP feedback (associatesubmenus, associateradial).
    /// </summary>
    List<RadialEntry> AssociateAbilities(ObjectView t)
    {
        var ring = SpecialAbilities(t, true);
        if (_book.Skill(18) is { } taunt) ring[1] = SlotEntry(new BarSlot { Kind = "skill", Id = taunt.Skill }, t);
        return ring;
    }

    RadialEntry FamiliarEntry(bool possess)
    {
        uint target = Facts is { } facts && facts.TryGetProperty("familiar", out var id) ? id.GetUInt32() : 0x7F000000;
        return new RadialEntry
        {
            Label = possess ? "Possess Familiar" : "Unpossess Familiar", Glyph = "eye",
            Look = new SlotLook(possess ? "Possess Familiar" : "Unpossess Familiar", [possess ? "ife_possess" : "ife_unpossess"], "eye", -1, false, true, "", ""),
            Act = () =>
            {
                if (possess) Session.Request("possess-familiar", new { target });
                else Session.Request("unpossess-familiar");
            },
        };
    }

    bool CombatFeat(int feat) => Session.Content?.Resources.TwoDA("feat").GetInt(feat, "CATEGORY", -1) == 22;

    List<RadialEntry> CombatAbilities(ObjectView t, bool self)
    {
        var list = _book.Modes.Where(m => m.Combat && m.Mode != 10).OrderBy(m => m.Mode)
            .Select(m => SlotEntry(new BarSlot { Kind = "mode", Id = m.Mode }, null)).ToList();
        list.AddRange(Feats(t, self, f => CombatFeat(f.Feat)));
        return list;
    }

    List<RadialEntry> SpecialAbilities(ObjectView t, bool self)
    {
        var feats = Feats(t, self, f => !CombatFeat(f.Feat));
        if (!self)
            return Skills(t, false).Concat(feats).Concat(new[] { Category("Items", "bag", () => Items(t, false)) }).ToList();
        var ring = FixedRing((0, Icon(new RadialEntry { Label = "Craft", Glyph = "craft", Act = () => Session.Request("craft") }, "ir_craft")));
        if (_book.Skill(18) is { } taunt) ring[1] = SlotEntry(new BarSlot { Kind = "skill", Id = taunt.Skill }, null, disabled: self);
        if (_book.Mode(0) != null) ring[2] = SlotEntry(new BarSlot { Kind = "mode", Id = 0 }, null);
        if (_book.Mode(1) != null) ring[6] = SlotEntry(new BarSlot { Kind = "mode", Id = 1 }, null);
        if (feats.FirstOrDefault(e => e.Slot?.Id == 303) is { } familiar) { ring[5] = familiar; feats.Remove(familiar); }
        if (Fact("canPossessFamiliar")) ring[5] = FamiliarEntry(true);
        // Keep every additional feat and usable skill accessible; their exact per-class order is measured separately.
        var extra = feats;
        for (int i = 0; i < 8 && extra.Count > 0; i++)
            if (ring[i].Hidden) { ring[i] = extra[0]; extra.RemoveAt(0); }
        if (extra.Count > 0) ring.AddRange(extra);
        return ring;
    }

    /// <summary>A sub-ring wedge; disabled ("None") when it would be empty.</summary>
    RadialEntry Category(string label, string glyph, Func<List<RadialEntry>> build)
    {
        var entries = build();
        int count = entries.Count(e => !e.Hidden);
        int usable = entries.Count(e => !e.Disabled && !e.Hidden);
        return new RadialEntry
        {
            Label = label, Glyph = glyph, Open = count > 0 ? build : null, Disabled = count == 0,
            Detail = entries.Count == 0 ? "None" : $"{entries.Count} · {usable} ready",
        };
    }

    RadialEntry SlotEntry(BarSlot slot, ObjectView? target, string? label = null, string? glyph = null, bool disabled = false)
    {
        var look = _user.Look(slot);
        var ground = target == null ? _ground : null; // Close clears menu state before invoking the entry.
        if (glyph != null && look.Icons.Length == 0) look = look with { Glyph = glyph };
        return new RadialEntry
        {
            Label = label ?? look.Name, Detail = look.Detail.Length > 0 ? look.Detail : look.Kind, Look = look, Glyph = look.Glyph,
            Disabled = disabled || !look.Usable, Active = look.Toggled, Slot = slot,
            Act = () => _user.Use(slot, target, ground),
        };
    }

    RadialEntry SkillEntry(SkillAbility skill, ObjectView t, string glyph) =>
        SlotEntry(new BarSlot { Kind = "skill", Id = skill.Skill }, t, skill.Name, glyph);

    /// <summary>The object to hand to an ability: the target if the ability can take it, else null (pick one).</summary>
    ObjectView? Given(int types, ObjectView? t) => t != null && _user.Fits(types, t) ? t : null;

    List<RadialEntry> Spells(ObjectView? t, bool self)
    {
        int bits = _inventoryItem != null ? AbilityUser.TargetItem : TargetBits(t, self);
        // Polymorphed, Cast Spell offers only the form's abilities, SPELL1-3 at NE, E and SE (visual scene polymorphpc).
        if (_book.Polymorphed)
        {
            var form = FixedRing();
            for (int i = 0; i < 3 && i < _book.FormAbilities.Length; i++)
            {
                int spell = _book.FormAbilities[i];
                if (spell < 0) continue;
                int types = Casting.Info.GetValueOrDefault(spell)?.TargetType ?? AbilityUser.TargetSelf;
                form[i + 1] = SlotEntry(new BarSlot { Kind = "spell", Id = spell, Class = 255, Arg = "form" }, t == null ? null : Given(types, t));
            }
            return form;
        }
        var spells = _book.Spells.Where(s => self || ((Casting.Info.GetValueOrDefault(s.Spell)?.TargetType ?? 0) & bits) != 0).ToList();
        var ring = FixedRing((6, Icon(new RadialEntry { Label = "Spellcasting Abilities", Glyph = "magic", Open = () => CastingAbilities(spells, t, self) }, "ir_spell_abs")));
        foreach (int cls in _book.ClassNames.Keys.OrderBy(c => c == 10 ? 0 : 1).ThenBy(c => c))
        {
            var book = spells.Where(s => s.Class == cls).ToList();
            var entry = Icon(new RadialEntry
            {
                Label = _book.ClassNames.GetValueOrDefault(cls, "Spells"), Glyph = "spellbook",
                Detail = $"{book.Sum(s => s.Ready)} ready", Open = () => SpellLevels(book, t, self),
            }, _book.ClassIcons.GetValueOrDefault(cls, "ir_splbook"));
            // Wizard SW is measured against EE. Other/custom classes retain a usable stable fallback.
            int slot = cls == 10 ? 5 : ring.FindIndex(e => e.Hidden);
            if (slot >= 0) ring[slot] = entry; else ring.Add(entry);
        }
        return ring;
    }

    List<RadialEntry> CastingAbilities(List<BookSpell> spells, ObjectView? t, bool self)
    {
        var ring = FixedRing((0, Icon(new RadialEntry
        {
            Label = "Counterspell", Glyph = "magic", Disabled = self || t?.Kind != ObjectKind.Creature,
            Act = () => { if (t != null) Session.Request("counterspell", new { target = t.ObjectId }); },
        }, "ir_cntrspell")));
        if (_book.Mode(10) != null) ring[1] = SlotEntry(new BarSlot { Kind = "mode", Id = 10 }, null);
        var innate = SpellList(spells.Where(s => s.Class == 255).ToList(), t, self);
        for (int i = 2; i < 8 && innate.Count > 0; i++) { ring[i] = innate[0]; innate.RemoveAt(0); }
        ring.AddRange(innate);
        return ring;
    }

    List<RadialEntry> SpellLevels(List<BookSpell> spells, ObjectView? t, bool self)
    {
        RadialEntry Level(int level) => Icon(new RadialEntry
        {
            Label = level == 0 ? "Cantrips" : $"Level {level}", Glyph = "magic",
            Detail = $"{spells.Where(s => s.Level == level).Sum(s => s.Ready)} ready",
            Open = () => SpellList(spells.Where(s => s.Level == level).ToList(), t, self),
        }, level == 0 ? "ir_cantrips" : level <= 6 ? $"ir_level{level}" : "ir_level789");
        var levels = Enumerable.Range(0, 7).Select(Level).ToList();
        levels.Add(Icon(new RadialEntry { Label = "Levels 7-9", Glyph = "magic", Open = () => Enumerable.Range(7, 3).Select(Level).ToList() }, "ir_level789"));
        return levels;
    }

    List<RadialEntry> SpellList(List<BookSpell> spells, ObjectView? t, bool self) =>
        spells.GroupBy(s => (s.Spell, s.Meta)).Select(g =>
        {
            var s = g.First();
            var slot = new BarSlot { Kind = "spell", Id = s.Spell, Class = s.Class, Meta = s.Meta };
            int types = Casting.Info.GetValueOrDefault(s.Spell)?.TargetType ?? AbilityUser.TargetSelf;
            var e = SlotEntry(slot, t == null ? null : Given(types, t));
            if (t == null && _inventoryItem is { } owned)
            {
                uint targetId = owned.Id;
                e.Act = () => Session.Request("cast-spell", new { spell = s.Spell, @class = s.Class, meta = s.Meta, target = targetId });
            }
            int ready = g.Sum(x => x.Ready), total = g.Sum(x => x.Total);
            e.Look = e.Look! with { Uses = ready };
            e.Detail = $"{ready} of {total} ready";
            e.Disabled = ready == 0;
            return e;
        }).ToList();

    List<RadialEntry> Feats(ObjectView? t, bool self, Func<FeatAbility, bool>? include = null)
    {
        int bits = TargetBits(t, self);
        bool Fits(int types) => self || (types & bits) != 0;
        var list = new List<RadialEntry>();
        foreach (var group in _book.Feats.Where(f => (include == null || include(f)) && (Fits(f.TargetType) || f.Subs.Any(s => Fits(s.TargetType)))).GroupBy(f => f.Master))
        {
            var feats = group.ToList();
            if (group.Key >= 0 && feats.Count > 1)
            {
                var first = feats[0];
                list.Add(new RadialEntry
                {
                    Label = first.MasterName.Length > 0 ? first.MasterName : first.Name, Detail = $"{feats.Count} feats",
                    Look = new SlotLook(first.MasterName, [first.MasterIcon], "feat", -1, false, true, "", ""),
                    Open = () => feats.Select(f => FeatEntry(f, t, Fits)).ToList(),
                });
            }
            else list.AddRange(feats.Select(f => FeatEntry(f, t, Fits)));
        }
        return list;
    }

    RadialEntry FeatEntry(FeatAbility f, ObjectView? t, Func<int, bool> fits)
    {
        if (f.Subs.Length > 0)
        {
            var look = _user.Look(new BarSlot { Kind = "feat", Id = f.Feat });
            return new RadialEntry
            {
                Label = f.Name, Look = look, Detail = look.Detail.Length > 0 ? look.Detail : $"{f.Subs.Length} choices", Disabled = !f.Ready,
                Slot = new BarSlot { Kind = "feat", Id = f.Feat },
                Open = () => f.Subs.Where(s => fits(s.TargetType > 0 ? s.TargetType : f.TargetType)).Select(s =>
                    SlotEntry(new BarSlot { Kind = "feat", Id = f.Feat, Sub = s.Id }, Given(s.TargetType > 0 ? s.TargetType : f.TargetType, t), s.Name)).ToList(),
            };
        }
        return SlotEntry(new BarSlot { Kind = "feat", Id = f.Feat }, Given(f.TargetType, t));
    }

    List<RadialEntry> Skills(ObjectView t, bool self)
    {
        int bits = TargetBits(t, self);
        var list = new List<RadialEntry>();
        foreach (var s in _book.Skills)
        {
            if (!self && (s.TargetType & bits) == 0) continue;
            if (s.Subs.Length > 0)
                list.Add(new RadialEntry
                {
                    Label = s.Name, Look = _user.Look(new BarSlot { Kind = "skill", Id = s.Skill }), Detail = $"Rank {s.Rank}",
                    Open = () => s.Subs.Select(sub => SlotEntry(new BarSlot { Kind = "skill", Id = s.Skill, Sub = sub.Id }, Given(s.TargetType, t), sub.Name, "trap")).ToList(),
                });
            else list.Add(SlotEntry(new BarSlot { Kind = "skill", Id = s.Skill }, Given(s.TargetType, t)));
        }
        return list;
    }

    List<RadialEntry> Items(ObjectView? t, bool self)
    {
        int bits = TargetBits(t, self);
        var list = new List<RadialEntry>();
        // Identical items (potions that don't stack) show once; using it uses the first with a use left.
        foreach (var item in _book.Items.GroupBy(i => (i.Name, string.Join("|", i.Powers.Select(p => p.Name)))).Select(g => g.FirstOrDefault(i => i.Usable && i.Powers.Any(p => p.Ready)) ?? g.First()))
        {
            var powers = item.Powers.Where(p => self || (p.TargetType & bits) != 0).ToList();
            if (powers.Count == 0) continue;
            if (powers.Count == 1)
            {
                var e = SlotEntry(new BarSlot { Kind = "item", Item = item.Item, Prop = powers[0].Index, Name = item.Name }, Given(powers[0].TargetType, t));
                e.Detail = powers[0].Name + (e.Look!.Uses >= 0 ? $" · {e.Look.Uses} left" : "");
                list.Add(e);
                continue;
            }
            list.Add(new RadialEntry
            {
                Label = item.Name, Look = new SlotLook(item.Name, item.Icon, "bag", -1, false, item.Usable, "", ""), Detail = $"{powers.Count} powers",
                Disabled = !item.Usable, Slot = new BarSlot { Kind = "item", Item = item.Item, Name = item.Name },
                Open = () => powers.Select(p =>
                {
                    var e = SlotEntry(new BarSlot { Kind = "item", Item = item.Item, Prop = p.Index, Name = item.Name }, Given(p.TargetType, t), p.Name);
                    e.Look = e.Look! with { Icons = p.Icon.Length > 0 ? [p.Icon] : item.Icon };
                    return e;
                }).ToList(),
            });
        }
        return list;
    }

    List<RadialEntry> Modes() => _book.Modes.Select(m => SlotEntry(new BarSlot { Kind = "mode", Id = m.Mode }, null)).ToList();

    List<RadialEntry> Emotes() => EmoteRing(0);

    /// <summary>
    /// An emote ring as the original's (quickbargaps): Bow N, Threaten NE, Greet E, Laugh SE, Salute S, Cheer SW, Goodbye
    /// W, More Emotes NW; then Nod, Tired, Beg, Poisoned, Bored, Look Far, Scratch Head and More Emotes; then Fall down
    /// forwards N, Sit on ground E, Meditate SE, Drink potion S, Read SW, Worship W and Fall down backwards NW.
    /// </summary>
    List<RadialEntry> EmoteRing(int ring)
    {
        var list = FixedRing();
        foreach (var e in OpenWinter.Content.Emotes.All.Where(e => e.Ring == ring))
            list[e.Direction] = SlotEntry(new BarSlot { Kind = "emote", Arg = e.Key, Name = e.Name }, null);
        if (ring < 2)
            list[7] = Icon(Category(Tlk(OpenWinter.Content.Emotes.MoreStrRef, "More Emotes"), "emote", () => EmoteRing(ring + 1)), OpenWinter.Content.Emotes.MoreIcon);
        return list;
    }

    /// <summary>The DM's entries: <see cref="Interaction.ExtendMenu"/> fills a scratch menu; each item becomes a wedge that calls <see cref="Interaction.MenuChosen"/>.</summary>
    List<RadialEntry> Dm(ObjectView t)
    {
        var scratch = new PopupMenu();
        try
        {
            Interaction.ExtendMenu!(scratch, t);
            var list = new List<RadialEntry>();
            for (int i = 0; i < scratch.ItemCount; i++)
            {
                if (scratch.IsItemSeparator(i)) continue;
                long id = scratch.GetItemId(i);
                string text = scratch.GetItemText(i);
                list.Add(new RadialEntry
                {
                    Label = text.TrimEnd('.', '…'), Glyph = DmGlyph(text), Disabled = scratch.IsItemDisabled(i),
                    Detail = text.EndsWith("...") ? "Asks for a value" : "",
                    Act = () => { if (IsInstanceValid(t)) Interaction.MenuChosen?.Invoke(id, t); },
                });
            }
            return list;
        }
        finally { scratch.Free(); }
    }

    static string DmGlyph(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("examine")) return "eye";
        if (t.Contains("heal")) return "heal";
        if (t.Contains("kill") || t.Contains("destroy")) return "death";
        if (t.Contains("go to") || t.Contains("summon") || t.Contains("jump")) return "compass";
        if (t.Contains("possess")) return "character";
        if (t.Contains("gold")) return "gold";
        if (t.Contains("experience") || t.Contains("level")) return "levelup";
        if (t.Contains("lock")) return "lock";
        if (t.Contains("rest")) return "rest";
        if (t.Contains("mute") || t.Contains("boot")) return "sound";
        if (t.Contains("give") || t.Contains("put")) return "chest";
        if (t.Contains("effect")) return "magic";
        if (t.Contains("ai")) return "cog";
        if (t.Contains("limbo")) return "hourglass";
        return "shield";
    }

    /// <summary>Frosted ring of wedges with gaps, hover and toggled highlights, a centre disc and a soft shadow (anti-aliased in the shader).</summary>
    const string RingShader = """
        shader_type canvas_item;
        uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
        uniform float size_px = 368.0;
        uniform float inner_px = 60.0;
        uniform float outer_px = 156.0;
        uniform float centre_px = 52.0;
        uniform int count = 8;
        uniform int hovered = -1;
        uniform int used = 8;
        uniform float gap_px = 3.0;
        uniform float glass = 1.0;
        uniform vec4 fill : source_color = vec4(0.09, 0.1, 0.13, 0.86);
        uniform vec4 fill_hover : source_color = vec4(0.17, 0.19, 0.25, 0.95);
        uniform vec4 fill_active : source_color = vec4(0.55, 0.42, 0.21, 0.55);
        uniform vec4 edge : source_color = vec4(1.0, 1.0, 1.0, 0.14);
        uniform vec4 edge_hover : source_color = vec4(0.83, 0.65, 0.34, 1.0);
        uniform vec4 centre_edge : source_color = vec4(0.55, 0.42, 0.21, 1.0);
        uniform float disabled_mask[24];
        uniform float active_mask[24];

        void fragment() {
            vec2 p = (UV - 0.5) * size_px;
            float r = length(p);
            float ang = atan(p.x, -p.y);
            if (ang < 0.0) ang += TAU;
            float n = float(max(count, 1));
            float seg = TAU / n;
            float idxf = floor((ang + seg * 0.5) / seg);
            int idx = int(mod(idxf, n));
            float local = ang - idxf * seg;
            float lateral = (seg * 0.5 - abs(local)) * r;
            if (count <= 1) lateral = 1000.0;

            vec3 behind = vec3(0.0);
            if (glass > 0.5) {
                behind = textureLod(screen_tex, SCREEN_UV, 3.0).rgb;
                float l = dot(behind, vec3(0.299, 0.587, 0.114));
                behind = mix(vec3(l), behind, 0.6) * 0.5;
            }

            float ring = smoothstep(inner_px - 0.75, inner_px + 0.75, r) * (1.0 - smoothstep(outer_px - 0.75, outer_px + 0.75, r));
            float wedge = ring * smoothstep(gap_px * 0.5 - 0.75, gap_px * 0.5 + 0.75, lateral);
            bool hov = idx == hovered;
            float dis = disabled_mask[idx];
            float act = active_mask[idx];
            vec4 f = hov ? fill_hover : mix(fill, fill_active, act);
            bool empty = idx >= used;
            if (empty) f.a *= 0.45;
            vec3 wc = mix(behind, f.rgb, glass > 0.5 ? f.a : (empty ? 0.6 : 1.0));
            wc *= 1.0 - 0.3 * dis;
            // Hairline round each wedge; a brass rim on the outside edge of the hovered one.
            float d_edge = min(min(r - inner_px, outer_px - r), lateral - gap_px * 0.5);
            vec4 ec = hov ? edge_hover : edge;
            if (empty) ec.a *= 0.5;
            wc = mix(wc, ec.rgb, (1.0 - smoothstep(0.5, 1.5, d_edge)) * ec.a);
            if (hov) wc = mix(wc, edge_hover.rgb, (1.0 - smoothstep(2.5, 3.5, outer_px - r)) * 0.9);
            // Lighter towards the outside for a little depth.
            wc += vec3(0.025) * clamp((r - inner_px) / (outer_px - inner_px), 0.0, 1.0);

            float centre = 1.0 - smoothstep(centre_px - 0.75, centre_px + 0.75, r);
            vec4 cf = hovered == -2 ? fill_hover : fill;
            vec3 cc = mix(behind, cf.rgb, glass > 0.5 ? 0.94 : 1.0);
            cc = mix(cc, centre_edge.rgb, 1.0 - smoothstep(0.6, 1.8, centre_px - r));
            if (hovered == -2) cc = mix(cc, edge_hover.rgb, 1.0 - smoothstep(1.5, 2.8, centre_px - r));

            // Soft shadow outside the ring; a dark band between the centre disc and the ring.
            float shadow = (1.0 - smoothstep(outer_px - 6.0, outer_px + 24.0, r)) * 0.5;
            shadow = max(shadow, (1.0 - smoothstep(inner_px - 1.0, inner_px + 1.0, r)) * 0.72);
            vec4 col = vec4(0.0, 0.0, 0.0, shadow * (1.0 - wedge));
            col = mix(col, vec4(wc, 1.0), wedge);
            col = mix(col, vec4(cc, 1.0), centre);
            COLOR = col;
        }
        """;
}
