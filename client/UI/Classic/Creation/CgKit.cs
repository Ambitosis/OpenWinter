using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// Pieces shared by the classic character creation and level-up screens (the original's cg_*.gui layouts on
/// pnl_chr_genm): selectable row buttons, scrolling row lists, the help panel and its justified text.
/// </summary>
public static class CgKit
{
    public const string Font = "fnt_galahad14";
    /// <summary>Greyed captions (prestige classes not yet open, restricted skills, granted feats): measured (125, 125, 125) on c13.</summary>
    public static readonly Color Grey = Color.Color8(125, 125, 125);
    /// <summary>Line pitch of the help panel's text (17 px on the original's screens).</summary>
    public const float HelpPitch = 17;

    /// <summary>The panel's model origin on the middle of the area (pnl_chr_genm and pnl_lvlup_sum are centred: c02, l08).</summary>
    public static void Centre(GuiPanel panel, Vector2 area) => panel.Position = (area / 2 - panel.ModelOrigin).Floor();

    static readonly Dictionary<(string, float), float> Shifts = new();

    /// <summary>
    /// The checked (selected) picture's texture-coordinate offset from the up picture. These buttons' textures
    /// stack three pictures (up, selected in blue, highlighted in gold) and their models' animations only move the
    /// caption; the selected picture is the next band below the up one (ctl_class, ctl_btn_pre_178 ...). Found by
    /// scanning the texture's middle column for the band after <paramref name="v1"/> (the up picture's bottom).
    /// </summary>
    public static float CheckedShift(ClassicAssets assets, string texture, float v0, float v1)
    {
        if (Shifts.TryGetValue((texture, v0), out var hit)) return hit;
        float shift = v1 - v0;
        if (assets.Texture(texture) is { } tex && tex.GetImage() is { } img)
        {
            if (img.IsCompressed()) img.Decompress();
            int h = img.GetHeight(), w = img.GetWidth();
            int x = w / 2;
            float Lum(Color c) => (c.R + c.G + c.B) / 3;
            // The up picture's black inside starts a few rows under its top; the selected picture's blue inside the
            // same few rows under its own top: the distance between the two is the shift.
            int upInside = -1, blueInside = -1;
            bool border = false;
            for (int y = Math.Clamp((int)(v0 * h) - 2, 0, h - 1); y < h && upInside < 0; y++)
            {
                float l = Lum(img.GetPixel(x, y));
                if (l > 0.2f) border = true;
                else if (border && l < 0.06f) upInside = y;
            }
            for (int y = Math.Clamp((int)(v1 * h) - 2, 0, h - 1); y < h && blueInside < 0; y++)
            {
                var c = img.GetPixel(x, y);
                if (c.B > c.R + 0.04f && c.B > 0.12f) blueInside = y;
            }
            if (upInside >= 0 && blueInside > upInside) shift = (blueInside - upInside) / (float)h;
        }
        return Shifts[(texture, v0)] = shift;
    }

    /// <summary>A model's first textured mesh: its texture and v range (the up picture).</summary>
    public static (string Texture, float V0, float V1)? FrameOf(GuiModel? model)
    {
        var mesh = model?.Rest.Meshes.Where(m => m.Alpha > 0.5f && m.Uvs.Length > 0 && !m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(m => (m.Points.Max(p => p.X) - m.Points.Min(p => p.X)) * (m.Points.Max(p => p.Y) - m.Points.Min(p => p.Y))).FirstOrDefault();
        if (mesh == null) return null;
        return (mesh.Texture, mesh.Uvs.Min(u => u.Y), mesh.Uvs.Max(u => u.Y));
    }

    /// <summary>Wraps text into lines and marks which lines end a paragraph (those are not justified).</summary>
    public static List<(string Text, bool Last)> WrapLines(GuiFont font, string text, float width)
    {
        var result = new List<(string, bool)>();
        foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
        {
            var lines = font.Wrap(para, width);
            for (int i = 0; i < lines.Count; i++) result.Add((lines[i], i == lines.Count - 1));
        }
        return result;
    }

    /// <summary>
    /// Draws a line, spread to the full width when <paramref name="justify"/> (every line but a paragraph's last).
    /// </summary>
    public static void DrawJustified(CanvasItem ci, GuiFont font, Vector2 at, float width, string line, Color color, bool justify)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!justify || words.Length < 2)
        {
            font.Draw(ci, at, line, color);
            return;
        }
        float total = words.Sum(font.Measure);
        float gap = (width - total) / (words.Length - 1);
        if (gap > font.Measure(" ") * 4) { font.Draw(ci, at, line, color); return; }
        float x = at.X;
        foreach (var word in words)
        {
            font.Draw(ci, new Vector2(MathF.Round(x), at.Y), word, color);
            x += font.Measure(word) + gap;
        }
    }

    /// <summary>Letters of a talk-table string or text: the original's help texts carry the gender of the character in a few words; they are shown as the table has them.</summary>
    public static string Clean(string s) => NwnText.Strip(s ?? "");
}

/// <summary>
/// A button of the creation screens: a selectable row or choice drawn from its model, with the selected (blue)
/// picture from the texture's second band, an optional icon at the model's "icon" node and a greyed caption for
/// entries that can be looked at but not taken (prestige classes, restricted skills, granted feats).
/// </summary>
public partial class CgButton : GuiButton
{
    bool _selected, _greyed, _encouraged;
    float _checkedShift = float.NaN;

    public bool Selected { get => _selected; set { if (_selected == value) return; _selected = value; QueueRedraw(); } }
    public bool Greyed { get => _greyed; set { if (_greyed == value) return; _greyed = value; QueueRedraw(); } }
    /// <summary>The pulsing gold frame of the next step to take (the model's "encouraged" animation).</summary>
    public bool Encouraged
    {
        get => _encouraged;
        set { if (_encouraged == value) return; _encouraged = value; ForcedState = value && Model?.HasAnimation("encouraged") == true ? "encouraged" : null; QueueRedraw(); }
    }
    public object? Tag;
    /// <summary>
    /// A list row (class, skill, feat, spell, domain, familiar): the original draws it the same under a resting pointer
    /// (level-up: class, skill, spell and familiar rows unchanged; buttons such as OK and the arrows light up).
    /// </summary>
    public bool Row;
    static readonly HashSet<string> RowModels = new(StringComparer.OrdinalIgnoreCase)
        { "ctl_cg_btn_class", "ctl_cg_btn_skill", "ctl_cg_btn_feat", "ctl_cg_btn_dom", "ctl_cg_btn_famil" };

    public static CgButton Make(ClassicAssets assets, string model, string caption = "", Texture2D? icon = null, float alignH = 0)
    {
        var b = new CgButton { Assets = assets, Name = model, Font = assets.Font(CgKit.Font), AlignH = alignH, AlignV = 0.5f, Row = RowModels.Contains(model) };
        b.Model = assets.Model(model);
        b.Caption = caption;
        if (icon != null) { b.Icon = icon; b.IconSize = new Vector2(32, 32); }
        return b;
    }

    /// <summary>Turns a layout's text button into a choice button (same model, caption and place).</summary>
    public static CgButton From(GuiButton b)
    {
        var c = new CgButton
        {
            Assets = b.Assets, Name = b.Name, Font = b.Font, AlignH = b.AlignH, AlignV = b.AlignV, CaptionColor = b.CaptionColor,
            Position = b.Position,
        };
        c.Model = b.Model;
        c.Caption = b.Caption;
        var parent = b.GetParent();
        int index = b.GetIndex();
        parent?.AddChild(c);
        parent?.MoveChild(c, index);
        // The panel finds the replacement by the layout tag from now on.
        for (Node? n = parent; n != null; n = n.GetParent())
            if (n is GuiPanel panel) { panel.Register(b.Name, c); break; }
        b.Visible = false;
        b.QueueFree();
        return c;
    }

    /// <summary>
    /// The caption box of a model with a single "text" node: left-aligned captions start at it (ctl_btn_app: c42),
    /// centred ones centre on it (ctl_btn_pre_360's General: c32).
    /// </summary>
    Rect2 TextBox(Vector2 t)
    {
        if (AlignH >= 0.25f)
        {
            float half = Math.Min(t.X, Size.X - t.X);
            return new Rect2(t.X - half, 0, half * 2, Size.Y);
        }
        return new Rect2(t.X, 0, Size.X - t.X - 4, Size.Y);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (CgList.Wheel(this, e)) { AcceptEvent(); return; }
        base._GuiInput(e);
    }

    float Shift()
    {
        if (!float.IsNaN(_checkedShift)) return _checkedShift;
        _checkedShift = CgKit.FrameOf(Model) is { } f ? CgKit.CheckedShift(Assets, f.Texture, f.V0, f.V1) : 0;
        return _checkedShift;
    }

    public override void _Draw()
    {
        if (Model == null) return;
        var state = State();
        if ((_selected || Row && state == "hilite") && state is not "disabled" && ForcedState == null) state = "up";
        var pose = Model.PoseOf(state, 0);
        bool hover = IsHovered() && !Disabled && !_selected && !Row;
        var shift = _selected ? new Vector2(0, Shift()) : Disabled ? DisabledUvShift : default;
        // The shift applies to the up picture only (the encouraged gold frame is its own mesh).
        GuiDraw.Model(this, Assets, pose, Origin, hover ? GuiDraw.Hilite : Colors.White, uvShift: shift);
        if (Icon != null)
        {
            var size = IconSize == Vector2.Zero ? Icon.GetSize() : IconSize;
            var at = pose.Node("icon") is { } p ? p + Origin - size / 2 : new Vector2(4, (Size.Y - size.Y) / 2);
            DrawTextureRect(Icon, new Rect2(at.Floor(), size), false, Disabled ? new Color(0.5f, 0.5f, 0.5f) : Colors.White);
        }
        if (Caption.Length > 0)
        {
            var box = GuiDraw.Box(pose, "text0", "text1") is { } b ? new Rect2(b.Position + Origin, b.Size)
                : pose.Node("text") is { } t ? TextBox(t + Origin)
                : new Rect2(Vector2.Zero, Size);
            var color = Disabled || _greyed ? CgKit.Grey : CaptionColor;
            var font = Font ?? Assets.Font(CgKit.Font);
            var text = NwnText.Strip(Caption);
            if (text.Contains('\n') || font.Measure(text) > box.Size.X + 2)
            {
                // Two-line captions ("Chaotic\nNeutral" on the alignment screen, c16) centre their lines in the box.
                var lines = font.Wrap(text, box.Size.X + 2);
                float pitch = font.LineHeight;
                float y = box.Position.Y + (box.Size.Y - lines.Count * pitch) / 2;
                foreach (var line in lines)
                {
                    float x = box.Position.X + (box.Size.X - font.Measure(line)) * AlignH;
                    font.Draw(this, new Vector2(x, y).Round(), line, color);
                    y += pitch;
                }
            }
            else font.DrawBox(this, box, text, color, AlignH, AlignV);
        }
    }
}

/// <summary>
/// Wrapped, justified text in a box with the original's scroll bar (arrows and ridged thumb) at its right: the help
/// panel's body, the feat / spell information and the level-up summary.
/// </summary>
public partial class CgText : Control
{
    public ClassicAssets Assets = null!;
    public GuiFont Font = null!;
    public float Pitch = CgKit.HelpPitch;
    /// <summary>Lines spread to the box's width (the original's help panels don't: c02, c05).</summary>
    public bool Justify;
    public Color Color = Colors.White;
    ClassicScrollBar? _bar;
    List<(string Text, bool Last)> _lines = new();
    string _text = "";
    float _offset;

    public CgText() { ClipContents = true; MouseFilter = MouseFilterEnum.Pass; }

    public static CgText Create(ClassicAssets assets, Rect2 box, Rect2? bar, Control parent)
    {
        var t = new CgText { Assets = assets, Font = assets.Font(CgKit.Font), Position = box.Position, Size = box.Size };
        parent.AddChild(t);
        if (bar is { } r)
        {
            t._bar = new ClassicScrollBar { Assets = assets, Position = r.Position, Size = r.Size, Scrolled = o => { t._offset = o; t.QueueRedraw(); } };
            parent.AddChild(t._bar);
        }
        return t;
    }

    public string Text
    {
        get => _text;
        set { _text = value ?? ""; Relayout(); _offset = 0; Sync(); }
    }

    public void Relayout()
    {
        _lines = CgKit.WrapLines(Font, CgKit.Clean(_text), Size.X);
        QueueRedraw();
    }

    void Sync()
    {
        if (_bar == null) return;
        _bar.Total = _lines.Count * Pitch;
        _bar.Page = Size.Y;
        _bar.Step = Pitch;
        _bar.Offset = _offset;
        _bar.QueueRedraw();
    }

    public override void _Draw()
    {
        float y = -_offset;
        foreach (var (line, last) in _lines)
        {
            if (y > -Pitch && y < Size.Y) CgKit.DrawJustified(this, Font, new Vector2(0, MathF.Round(y)), Size.X, line, Color, Justify && !last);
            y += Pitch;
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w)
        {
            float max = Math.Max(0, _lines.Count * Pitch - Size.Y);
            _offset = Math.Clamp(_offset + (w.ButtonIndex == MouseButton.WheelUp ? -3 : 3) * Pitch, 0, max);
            Sync();
            QueueRedraw();
            AcceptEvent();
        }
    }
}

/// <summary>
/// The help panel (ctl_cg_help): a title between title0 and title1 above the banner, and the body text between
/// text0 and text1 with the scroll bar's track between scrollmin and scrollmax.
/// </summary>
public sealed class CgHelp
{
    public GuiLabel Title = null!;
    public CgText Body = null!;

    /// <summary>Fills a layout's help list (its frame model gives the boxes); the list's own rows and bar are hidden.</summary>
    public static CgHelp? Attach(GuiPanel panel, string tag)
    {
        if (panel.Get<GuiList>(tag) is not { Frame: { } frame } list) return null;
        var a = panel.Assets;
        list.Bar.Visible = false;
        foreach (var c in list.GetChildren().OfType<Control>()) if (c != frame) c.Visible = false;
        var at = list.Position;
        Rect2 Box(string n0, string n1, Rect2 fallback) => frame.Node(n0) is { } p && frame.Node(n1) is { } q ? new Rect2(p, Vector2.Zero).Expand(q) : fallback;
        var h = new CgHelp();
        var title = Box("title0", "title1", new Rect2(8, 4, 340, 18));
        h.Title = GuiLabel.Create(a, "", CgKit.Font, Colors.White, 0, 0.5f);
        h.Title.Position = at + title.Position + new Vector2(0, -1);
        h.Title.Size = title.Size + new Vector2(0, 2);
        panel.AddChild(h.Title);
        var text = Box("text0", "text1", new Rect2(8, 92, 320, 310));
        var bar = BarRect(a, frame, text);
        h.Body = CgText.Create(a, new Rect2(at + text.Position, text.Size), new Rect2(at + bar.Position, bar.Size), panel);
        return h;
    }

    /// <summary>
    /// The scroll bar of a list model, between its scrollmin and scrollmax nodes.
    /// </summary>
    public static Rect2 BarRect(ClassicAssets a, GuiModelView frame, Rect2 text)
    {
        var arrow = a.Model("pb_scrl_up")?.Bounds.Size ?? new Vector2(16, 16);
        float x = frame.Node("scrollmin")?.X ?? text.End.X + 4;
        float y0 = Math.Min(frame.Node("scrollmin")?.Y ?? text.Position.Y, frame.Node("scrollmax")?.Y ?? text.End.Y);
        float y1 = Math.Max(frame.Node("scrollmin")?.Y ?? text.Position.Y, frame.Node("scrollmax")?.Y ?? text.End.Y);
        // Measured on the help, race, class and feat lists (c02, c05, c27): the up arrow ends at scrollmin, the down
        // arrow ends 10 px below scrollmax, the arrows' left edge a pixel right of the nodes.
        return new Rect2(x + 1, y0 - arrow.Y, arrow.X, y1 - y0 + arrow.Y + 10);
    }

    public void Show(string title, string body)
    {
        Title.Text = CgKit.Clean(title);
        Body.Text = body;
    }
}

/// <summary>
/// A list box of the creation screens: rows built from a row model (ctl_cg_btn_class, _skill, _feat ...) stacked from
/// the list model's "listitem" node one row model "next" apart (44 px), clipped to the list's text box, scrolled a
/// row at a time by the original's scroll bar and three rows a wheel notch (measured on the class list, c11 / c12).
/// </summary>
public partial class CgList : Control
{
    public ClassicAssets Assets = null!;
    public GuiModelView Frame = null!;
    readonly Control _clip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
    readonly List<Control> _rows = new();
    ClassicScrollBar _bar = null!;
    Vector2 _first;
    public float Pitch = 44;
    int _top;
    /// <summary>Rows that fit (a partly shown last row counts: the original draws it cut).</summary>
    public int VisibleRows => Math.Max(1, (int)MathF.Ceiling((_clip.Size.Y - (_first.Y - _clip.Position.Y)) / Pitch));

    /// <summary>Takes over a layout's list (its model drawn by the layout; its own rows and bar are hidden).</summary>
    public static CgList? Attach(GuiPanel panel, string tag, float rowWidth = 0)
    {
        if (panel.Get<GuiList>(tag) is not { Frame: { } frame } list) return null;
        list.Bar.Visible = false;
        foreach (var c in list.GetChildren().OfType<Control>()) if (c != frame) c.Visible = false;
        var l = new CgList { Assets = panel.Assets, Frame = frame, Position = list.Position, Size = list.Size, MouseFilter = MouseFilterEnum.Pass, Name = tag + "_rows" };
        panel.AddChild(l);
        var text = frame.Node("text0") is { } a && frame.Node("text1") is { } b ? new Rect2(a, Vector2.Zero).Expand(b) : new Rect2(4, 4, list.Size.X - 24, list.Size.Y - 8);
        // The rows start at listitem and fill down to the bottom node (or text1).
        var item = frame.Node("listitem") ?? text.Position;
        float bottom = frame.Node("bottom")?.Y ?? text.End.Y;
        l._clip.Position = new Vector2(0, MathF.Min(item.Y, text.Position.Y));
        l._clip.Size = new Vector2(list.Size.X, bottom - l._clip.Position.Y);
        l._first = new Vector2(item.X, item.Y);
        l.AddChild(l._clip);
        var bar = CgHelp.BarRect(panel.Assets, frame, text);
        l._bar = new ClassicScrollBar { Assets = panel.Assets, Position = bar.Position, Size = bar.Size, Scrolled = o => l.ScrollTo((int)MathF.Round(o / l.Pitch)) };
        l.AddChild(l._bar);
        return l;
    }

    public IReadOnlyList<Control> Rows => _rows;

    public void Clear()
    {
        foreach (var r in _rows) { _clip.RemoveChild(r); r.QueueFree(); }
        _rows.Clear();
        _top = 0;
        Layout();
    }

    /// <summary>Adds a row control (its model origin goes on the row's anchor: listitem, then one pitch down per row).</summary>
    public T Add<T>(T row) where T : Control
    {
        _rows.Add(row);
        _clip.AddChild(row);
        Layout();
        return row;
    }

    public void ScrollTo(int top)
    {
        _top = Math.Clamp(top, 0, Math.Max(0, _rows.Count - VisibleRows + (PartialLast ? 1 : 0)));
        Layout();
    }

    /// <summary>Scrolls just enough to show a row.</summary>
    public void Reveal(int index)
    {
        if (index < _top) ScrollTo(index);
        else if (index >= _top + FullRows) ScrollTo(index - FullRows + 1);
    }

    int FullRows => Math.Max(1, (int)((_clip.Size.Y - (_first.Y - _clip.Position.Y)) / Pitch));
    bool PartialLast => FullRows < VisibleRows;

    public int Top => _top;

    void Layout()
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            var r = _rows[i];
            var origin = new Vector2(_first.X, _first.Y - _clip.Position.Y + (i - _top) * Pitch);
            r.Position = r is GuiButton gb ? (origin - gb.Origin).Floor() : origin;
            r.Visible = i >= _top - 1 && i < _top + VisibleRows + 1;
        }
        _bar.Total = _rows.Count * Pitch;
        _bar.Page = FullRows * Pitch;
        _bar.Step = Pitch;
        _bar.Offset = _top * Pitch;
        _bar.Visible = true;
        _bar.QueueRedraw();
    }

    /// <summary>A wheel notch over a row scrolls the list it is in.</summary>
    public static bool Wheel(Control from, InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w) return false;
        for (Node? n = from.GetParent(); n != null; n = n.GetParent())
            if (n is CgList list) { list.ScrollTo(list._top + (w.ButtonIndex == MouseButton.WheelUp ? -3 : 3)); return true; }
        return false;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w)
        {
            ScrollTo(_top + (w.ButtonIndex == MouseButton.WheelUp ? -3 : 3));
            AcceptEvent();
        }
    }
}
