using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// The spellbook data the classic windows share: the server's "spellbook" notify (classes, their levels with
/// spells, memorised slots and what can be prepared) and the spells' names and icons. Requests are the modern
/// spellbook's: "spellbook", "spell-memorize", casting through <see cref="SpellCasting"/>.
/// </summary>
public sealed class SpellbookModel
{
    public readonly ClassicContext Ctx;
    public JsonDocument? Doc;
    public int ClassIndex, Level;
    public readonly Dictionary<int, (string Name, string Icon)> Info = new();
    public event Action? Changed;
    string _lastJson = "";

    public SpellbookModel(ClassicContext ctx)
    {
        Ctx = ctx;
        ctx.Session.NotifyReceived += OnNotify;
    }

    /// <summary>Stops listening (the classic windows were removed).</summary>
    public void Dispose() => Ctx.Session.NotifyReceived -= OnNotify;

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "spellbook" || m.Subject != Ctx.Session.ControlledId) return;
        if (m.Json == _lastJson) return;
        _lastJson = m.Json;
        Doc?.Dispose();
        Doc = JsonDocument.Parse(m.Json);
        foreach (var i in Doc.RootElement.GetProperty("info").EnumerateArray())
            Info[I(i, "spell")] = (NwnText.Strip(S(i, "name")), S(i, "icon"));
        ClassIndex = Math.Clamp(ClassIndex, 0, Math.Max(0, Classes.Count - 1));
        if (Class is { } c && !Levels(c).Any(l => I(l, "level") == Level))
            Level = Levels(c).Select(l => I(l, "level")).DefaultIfEmpty(0).First();
        Changed?.Invoke();
    }

    public void Request() => Ctx.Session.Request("spellbook");

    public List<JsonElement> Classes => Doc?.RootElement.GetProperty("classes").EnumerateArray().ToList() ?? new();
    public JsonElement? Class => Classes.Count > 0 ? Classes[Math.Clamp(ClassIndex, 0, Classes.Count - 1)] : null;
    public static List<JsonElement> Levels(JsonElement cls) => cls.GetProperty("levels").EnumerateArray().ToList();
    public static bool Memorizes(JsonElement cls) => cls.TryGetProperty("memorizes", out var m) && m.ValueKind == JsonValueKind.True;

    public static int I(JsonElement e, string name, int fallback = 0) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
    public static string S(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    public void Memorize(int classId, int level, int slot, int spell, int meta = 0)
    {
        ClassicSounds.Play(Ctx.Assets, spell >= 0 ? "gui_spell_mem" : "gui_spell_erase");
        Ctx.Session.Request("spell-memorize", new { @class = classId, level, slot, spell, meta });
    }

    public void Cast(int spell, int classId, int meta = 0)
    {
        Ctx.Casting.Cast(spell, classId, meta);
        Ctx.Hud.GetTree().CreateTimer(2.5).Timeout += Request;
    }

    /// <summary>The level icon of the original (ir_cantrips, ir_level1..6, ir_level789).</summary>
    public static string LevelIcon(int level) => level switch { 0 => "ir_cantrips", >= 7 => "ir_level789", _ => $"ir_level{level}" };

    /// <summary>"Cantrips", "First Level" ... "Ninth Level" (talk table).</summary>
    public string LevelName(int level) => Ctx.Assets.Tlk((uint)(68674 + Math.Clamp(level, 0, 9)));
}

/// <summary>
/// The known-spells panel (pnl_spellbook), top-left: the class name in the title, the caster classes' icons in
/// the box under it, a row of spell levels (click one), and the level's spells under a "Known" heading. A prepared
/// caster clicks a spell to memorise it into the level's first free slot; a spontaneous caster clicks to cast.
/// Spells drag to the quickbar; right-click adds one to the quickbar.
/// </summary>
public partial class ClassicSpellbookWindow : ClassicWindow
{
    public SpellbookModel Model = null!;
    /// <summary>The memorise panel opened beside it (prepared casters).</summary>
    public ClassicMemorizeWindow? Memorize;
    GuiLabel _title = null!;
    Control _classes = null!, _levelRow = null!;
    SheetScroll _list = null!;
    bool _expanded = true;
    const float Row = 33;

    protected override GuiPanel Build()
    {
        var a = Assets;
        var p = GuiPanel.FromModel(a, "pnl_spellbook");
        var title = p.Node("Title") ?? new Vector2(10, 10);
        _title = SheetKit.Text(p, a, new Rect2(title.X, 1, 320, 18));
        var x = GuiButton.Create(a, "ctl_btn_x");
        x.PlaceOrigin(p.Node("xbutton") ?? new Vector2(342, 18));
        p.AddChild(x);
        p.Register("XButton", x);
        _classes = new Control { Position = new Vector2(6, 25), Size = new Vector2(330, 34), MouseFilter = MouseFilterEnum.Pass };
        p.AddChild(_classes);
        _levelRow = new Control { Position = new Vector2(6, 68), Size = new Vector2(331, 34), MouseFilter = MouseFilterEnum.Pass };
        p.AddChild(_levelRow);
        // Arrows at the level row's right (the framed arrows of ctl_cg_btn_skup / skdn): previous / next caster class.
        var up = GuiButton.Create(a, "ctl_cg_btn_skup");
        up.Position = new Vector2(340, 69);
        var down = GuiButton.Create(a, "ctl_cg_btn_skdn");
        down.Position = new Vector2(340, 86);
        up.Pressed += () => PickClass(Model.ClassIndex - 1);
        down.Pressed += () => PickClass(Model.ClassIndex + 1);
        p.AddChild(up);
        p.AddChild(down);
        _list = SheetScroll.Create(a, new Rect2(6, 110, 331, 358), new Rect2(343, 107, SheetScrollBar.Width, 361), p);
        _list.Bar.Step = Row;
        return p;
    }

    public override void _Ready()
    {
        base._Ready();
        Model.Changed += () => { if (Visible) Fill(); };
    }

    public override void Refresh()
    {
        Model.Request();
        Fill();
    }

    public override void Open()
    {
        base.Open();
        if (Model.Class is { } c && SpellbookModel.Memorizes(c)) Memorize?.Open();
    }

    public override void Close()
    {
        base.Close();
        Memorize?.Close();
    }

    void PickClass(int index)
    {
        int n = Model.Classes.Count;
        if (n == 0) return;
        Model.ClassIndex = (index % n + n) % n;
        if (Model.Class is { } c) Model.Level = SpellbookModel.Levels(c).Select(l => SpellbookModel.I(l, "level")).DefaultIfEmpty(0).First();
        Fill();
        if (Memorize != null)
        {
            if (Model.Class is { } cls && SpellbookModel.Memorizes(cls)) { if (!Memorize.Visible) Memorize.Open(); else Memorize.Fill(); }
            else Memorize.Close();
        }
    }

    /// <summary>Shows a spell level's known spells (as clicking its cell does).</summary>
    public void PickLevel(int level)
    {
        Model.Level = level;
        _expanded = true;
        Fill();
        Memorize?.Fill();
    }

    /// <summary>Clicks the n-th spell row of the list (captures: memorise or cast it).</summary>
    public void ClickSpell(int index) => _list.Content.GetChildren().OfType<TreeRow>().Where(r => r.Arrow == 0).ElementAtOrDefault(index)?.Clicked?.Invoke();

    public void Fill()
    {
        var a = Assets;
        foreach (var c in _classes.GetChildren()) { _classes.RemoveChild(c); c.QueueFree(); }
        foreach (var c in _levelRow.GetChildren()) { _levelRow.RemoveChild(c); c.QueueFree(); }
        _list.Clear();
        var classes = Model.Classes;
        if (Model.Class is not { } cls)
        {
            _title.Text = a.Tlk(2295);
            return;
        }
        _title.Text = $"{NwnText.Strip(SpellbookModel.S(cls, "name"))} {a.Tlk(2295)}";
        for (int i = 0; i < classes.Count; i++)
        {
            int index = i;
            var icon = new SpellCell { Assets = a, Icon = a.Texture(SpellbookModel.S(classes[i], "icon")), Position = new Vector2(i * 34, 0), Size = new Vector2(34, 34), Framed = false, Dim = i != Model.ClassIndex };
            icon.Clicked = () => PickClass(index);
            _classes.AddChild(icon);
        }
        int classId = SpellbookModel.I(cls, "class");
        bool memorizes = SpellbookModel.Memorizes(cls);
        var levels = SpellbookModel.Levels(cls);
        foreach (var l in levels)
        {
            int level = SpellbookModel.I(l, "level");
            if (level > 9) continue;
            var cell = new SpellCell { Assets = a, Icon = a.Texture(SpellbookModel.LevelIcon(level)), Position = new Vector2(MathF.Round(level * 33.1f), 0), Size = new Vector2(33, 34), Framed = false, Dim = level != Model.Level };
            cell.Clicked = () => PickLevel(level);
            _levelRow.AddChild(cell);
        }
        var current = levels.FirstOrDefault(l => SpellbookModel.I(l, "level") == Model.Level);
        // The "Known" heading with the level's icon; its arrow folds the list.
        var head = new TreeRow { Assets = a, Arrow = _expanded ? 1 : -1, Icon = a.Texture(SpellbookModel.LevelIcon(Model.Level)), Text = a.Tlk(66744), Position = Vector2.Zero, Size = new Vector2(331, Row + 1) };
        head.Clicked = () => { _expanded = !_expanded; Fill(); };
        _list.Content.AddChild(head);
        float y = Row;
        if (_expanded && current.ValueKind == JsonValueKind.Object)
        {
            // Prepared casters list what they can prepare; spontaneous casters what they know.
            var spells = memorizes
                ? current.GetProperty("available").EnumerateArray().Select(s => s.GetInt32()).ToList()
                : current.GetProperty("spells").EnumerateArray().Select(s => SpellbookModel.I(s, "spell")).ToList();
            int left = SpellbookModel.I(current, "left");
            var slots = current.GetProperty("slots").EnumerateArray().ToList();
            foreach (var spell in spells.Distinct().OrderBy(s => Model.Info.GetValueOrDefault(s).Name))
            {
                var info = Model.Info.GetValueOrDefault(spell);
                int sp = spell, level = Model.Level;
                var row = new TreeRow
                {
                    Assets = a, Arrow = 0, Icon = a.Texture(info.Icon), Text = info.Name ?? $"Spell {spell}", Position = new Vector2(0, y), Size = new Vector2(331, Row + 1),
                    Drag = new Godot.Collections.Dictionary { ["kind"] = "spell", ["spell"] = spell, ["class"] = classId },
                    Dim = !memorizes && left <= 0,
                };
                row.Clicked = () =>
                {
                    if (memorizes)
                    {
                        int free = slots.Where(s => SpellbookModel.I(s, "spell", -1) < 0).Select(s => SpellbookModel.I(s, "index")).DefaultIfEmpty(-1).First();
                        if (free >= 0) Model.Memorize(classId, level, free, sp);
                    }
                    else Model.Cast(sp, classId);
                };
                row.RightClicked = () => Ctx.Spellbook.AddToQuickbar?.Invoke(sp, classId);
                _list.Content.AddChild(row);
                y += Row;
            }
        }
        _list.SetContentHeight(y + 1);
    }
}

/// <summary>
/// The memorise panel (pnl_spellmemoriz) left of the party bar: a heading per spell level (arrow, level icon,
/// name) with the level's slots under it as icon boxes. Click a prepared slot to empty it; drag one to the
/// quickbar; right-click casts it. Slots not yet ready (prepared since the last rest) are dimmed.
/// </summary>
public partial class ClassicMemorizeWindow : ClassicWindow
{
    public SpellbookModel Model = null!;
    public ClassicSpellbookWindow? Book;
    SheetScroll _list = null!;
    readonly Dictionary<int, bool> _expanded = new();
    const float Row = 33;

    protected override GuiPanel Build()
    {
        var a = Assets;
        var p = GuiPanel.FromModel(a, "pnl_spellmemoriz");
        var title = p.Node("Title") ?? new Vector2(10, 10);
        SheetKit.Text(p, a, new Rect2(title.X, 1, 320, 18), a.Tlk(53290));
        var x = GuiButton.Create(a, "ctl_btn_x");
        x.PlaceOrigin(p.Node("xbutton") ?? new Vector2(342, 18));
        p.AddChild(x);
        p.Register("XButton", x);
        var tree = p.Node("treeview") ?? new Vector2(3, 23);
        _list = SheetScroll.Create(a, new Rect2(tree.X, tree.Y + 7, 331, 471 - tree.Y - 10), new Rect2(343, 21, SheetScrollBar.Width, 448), p);
        _list.Bar.Step = Row;
        return p;
    }

    public override void _Ready()
    {
        base._Ready();
        Model.Changed += () => { if (Visible) Fill(); };
    }

    public override void Refresh() => Fill();

    public void Fill()
    {
        var a = Assets;
        _list.Clear();
        if (Model.Class is not { } cls || !SpellbookModel.Memorizes(cls)) return;
        int classId = SpellbookModel.I(cls, "class");
        float y = 0;
        foreach (var l in SpellbookModel.Levels(cls))
        {
            int level = SpellbookModel.I(l, "level");
            var slots = l.GetProperty("slots").EnumerateArray().ToList();
            if (slots.Count == 0) continue;
            // The level chosen in the spellbook starts unfolded, the others folded.
            bool open = _expanded.TryGetValue(level, out var e) ? e : level == Model.Level;
            var head = new TreeRow { Assets = a, Arrow = open ? 1 : -1, Icon = a.Texture(SpellbookModel.LevelIcon(level)), Text = Model.LevelName(level), Position = new Vector2(0, y), Size = new Vector2(331, Row + 1) };
            head.Clicked = () => { _expanded[level] = !open; Model.Level = level; Fill(); Book?.Fill(); };
            _list.Content.AddChild(head);
            y += Row;
            if (open)
            {
                // Slots in rows of nine boxes, starting under the heading's icon.
                for (int i = 0; i < slots.Count; i++)
                {
                    var s = slots[i];
                    int spell = SpellbookModel.I(s, "spell", -1), index = SpellbookModel.I(s, "index"), meta = SpellbookModel.I(s, "meta");
                    bool ready = s.TryGetProperty("ready", out var r) && r.ValueKind == JsonValueKind.True;
                    var info = spell >= 0 ? Model.Info.GetValueOrDefault(spell) : default;
                    var cell = new SpellCell
                    {
                        Assets = a, Icon = spell >= 0 ? a.Texture(info.Icon) : null, Position = new Vector2(33 + i % 9 * 33, y + i / 9 * 33), Size = new Vector2(34, 34),
                        Dim = spell >= 0 && !ready, Tip = spell >= 0 ? info.Name ?? "" : "",
                        SharesRight = i % 9 < 8 && i + 1 < slots.Count, SharesBottom = i + 9 < slots.Count,
                        Drag = spell >= 0 ? new Godot.Collections.Dictionary { ["kind"] = "spell", ["spell"] = spell, ["class"] = classId, ["meta"] = meta } : null,
                    };
                    if (spell >= 0)
                    {
                        cell.Clicked = () => Model.Memorize(classId, level, index, -1);
                        cell.RightClicked = () => { if (ready) Model.Cast(spell, classId, meta); };
                    }
                    _list.Content.AddChild(cell);
                }
                y += ((slots.Count + 8) / 9) * 33 + 6;
            }
            else y += 6;
        }
        _list.SetContentHeight(y);
    }
}

/// <summary>
/// A row of the spell trees: an optional fold arrow in the first box (1 open, -1 closed, 0 none), an icon box and
/// the text box. Spell rows drag their spell to the quickbar.
/// </summary>
public partial class TreeRow : Control
{
    public ClassicAssets Assets = null!;
    public int Arrow;
    public Texture2D? Icon;
    public string Text = "";
    public bool Dim;
    /// <summary>No icon box: the text box follows the arrow box (the journal's quest rows).</summary>
    public bool HideIconBox;
    public Action? Clicked, RightClicked;
    public Godot.Collections.Dictionary? Drag;

    public TreeRow() { MouseFilter = MouseFilterEnum.Pass; }

    public override void _Ready()
    {
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
    }

    public override void _Draw()
    {
        float h = Size.Y;
        bool hover = GetGlobalRect().HasPoint(GetGlobalMousePosition());
        if (Arrow != 0)
        {
            SheetKit.RowBox(this, new Rect2(0, 0, 34, h));
            DrawArrow(this, Assets, new Vector2(17, h / 2), Arrow > 0);
        }
        float tx = 33;
        if (!HideIconBox)
        {
            SheetKit.RowBox(this, new Rect2(33, 0, 34, h), Colors.Black);
            if (Icon != null) DrawTextureRect(Icon, new Rect2(34, 1, h - 2, h - 2), false, Dim ? new Color(0.5f, 0.5f, 0.5f) : Colors.White);
            tx = 66;
        }
        SheetKit.RowBox(this, new Rect2(tx, 0, Size.X - tx, h), hover && (Clicked != null) ? new Color(1, 1, 1, 0.05f) : null);
        Assets.Font(SheetKit.Label).DrawBox(this, new Rect2(tx + 8, 0, Size.X - tx - 22, h), Text, Dim ? SheetKit.Caption : hover && Arrow == 0 ? GuiDraw.Selected : SheetKit.White, 0, 0.5f);
    }

    /// <summary>The small gold arrow of the original's trees (gui_actionqueue's arrow, turned up or down).</summary>
    public static void DrawArrow(CanvasItem ci, ClassicAssets assets, Vector2 centre, bool up)
    {
        var tex = assets.Texture("gui_actionqueue");
        if (tex == null) return;
        var size = tex.GetSize();
        var src = new Rect2(0.656f * size.X, 0.172f * size.Y, 0.141f * size.X, 0.203f * size.Y);
        ci.DrawSetTransform(centre, up ? Mathf.Pi / 2 : -Mathf.Pi / 2, Vector2.One);
        ci.DrawTextureRectRegion(tex, new Rect2(-src.Size / 2, src.Size), src);
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } && _down)
        {
            _down = false;
            if (!GetViewport().GuiIsDragging()) Clicked?.Invoke();
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { _down = true; AcceptEvent(); }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) { RightClicked?.Invoke(); AcceptEvent(); }
    }

    bool _down;

    public override Variant _GetDragData(Vector2 atPosition)
    {
        _down = false;
        if (Drag == null) return default;
        SetDragPreview(SpellCell.Preview(Icon));
        return Drag;
    }
}

/// <summary>An icon box (class, spell level or memorised slot): click, right-click, drag a spell.</summary>
public partial class SpellCell : Control, IClassicHover
{
    public ClassicAssets Assets = null!;
    public Texture2D? Icon;
    public bool Dim, Framed = true;
    /// <summary>Another cell follows to the right / below, overlapping this one's last column / row.</summary>
    public bool SharesRight, SharesBottom;
    public string Tip = "";
    public Action? Clicked, RightClicked;
    public Godot.Collections.Dictionary? Drag;
    bool _down;

    /// <summary>An empty slot's edge: white at the alpha (48) of gui_boxes_ingame's clear box.</summary>
    static readonly Color EmptyEdge = new(1, 1, 1, 48 / 255f);

    public SpellCell() { MouseFilter = MouseFilterEnum.Pass; }

    public string[]? HoverLines() => Tip.Length > 0 ? Tip.Split('\n') : null;

    public override void _Ready()
    {
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
    }

    public override void _Draw()
    {
        // A memorized slot keeps the black box behind its icon. An empty one is the original's ctl_spelbx_next box
        // (gui_boxes_ingame: a clear interior in white edges at alpha 48), so the panel shows through it, as the
        // memorizeslots capture shows.
        // Cells overlap their neighbours by a pixel: an edge another cell draws is left to it, so no edge doubles.
        if (Framed && Icon == null) SheetKit.Frame(this, new Rect2(Vector2.Zero, Size), EmptyEdge, right: !SharesRight, bottom: !SharesBottom);
        else if (Framed) SheetKit.RowBox(this, new Rect2(Vector2.Zero, Size), Colors.Black);
        if (Icon != null)
        {
            var s = Math.Min(Size.X, Size.Y) - 2;
            DrawTextureRect(Icon, new Rect2((Size - new Vector2(s, s)) / 2, new Vector2(s, s)), false, Dim ? new Color(0.5f, 0.5f, 0.5f) : Colors.White);
        }
        if (Clicked != null && GetGlobalRect().HasPoint(GetGlobalMousePosition()))
            DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), GuiDraw.Selected with { A = 0.6f }, false, 1);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Left } && _down)
        {
            _down = false;
            if (!GetViewport().GuiIsDragging()) Clicked?.Invoke();
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { _down = true; AcceptEvent(); }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) { RightClicked?.Invoke(); AcceptEvent(); }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        _down = false;
        if (Drag == null) return default;
        SetDragPreview(Preview(Icon));
        return Drag;
    }

    public static Control Preview(Texture2D? icon)
    {
        var holder = new Control();
        holder.AddChild(new TextureRect { Texture = icon, Size = new Vector2(32, 32), Position = new Vector2(-16, -16), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, Modulate = new Color(1, 1, 1, 0.85f) });
        return holder;
    }
}
