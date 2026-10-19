using System.Text.Json.Nodes;
using OpenWinter.Client.UI.Nui.Model;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>NuiLabel: single-line text with h/v alignment.</summary>
public partial class NuiLabelView : NuiElement
{
    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size).Grow(-Node.Padding);
        Ctx.Skin.DrawText(this, r, Text("value"), Int("text_halign"), Int("text_valign"), Foreground(NuiSkin.Text));
        DrawEncouraged();
    }
}

/// <summary>NuiText: read-only word-wrapped text in an implied group (border, scrollbars).</summary>
public partial class NuiTextView : NuiElement
{
    NuiScrollArea _scroll = null!;
    Control _painter = null!;
    string _text = "";

    protected override void Build()
    {
        _scroll = new NuiScrollArea { Skin = Ctx.Skin };
        AddChild(_scroll);
        _painter = new Control { MouseFilter = MouseFilterEnum.Pass };
        _painter.Draw += () =>
        {
            var skin = Ctx.Skin;
            _painter.DrawMultilineString(skin.Font, new Vector2(0, skin.Font.GetAscent(skin.FontSize)), _text, HorizontalAlignment.Left,
                Math.Max(1, _painter.Size.X), skin.FontSize, -1, Foreground(NuiSkin.Text));
        };
        _scroll.Content.AddChild(_painter);
    }

    public override void Refresh()
    {
        _text = Text("value");
        base.Refresh();
        Reflow();
    }

    public override void ApplyLayout()
    {
        base.ApplyLayout();
        Reflow();
    }

    void Reflow()
    {
        if (_scroll == null) return;
        float pad = Node.Border ? NuiStyle.GroupPadding : 0;
        _scroll.Position = new Vector2(pad, pad);
        _scroll.Size = new Vector2(Math.Max(0, Size.X - 2 * pad), Math.Max(0, Size.Y - 2 * pad));
        int mode = Node.Scrollbars;
        bool canY = mode is NuiStyle.ScrollY or NuiStyle.ScrollBoth or NuiStyle.ScrollAuto;
        var skin = Ctx.Skin;
        float width = _scroll.Size.X;
        float height = skin.Font.GetMultilineStringSize(_text, HorizontalAlignment.Left, Math.Max(1, width), skin.FontSize).Y;
        bool showY = mode is NuiStyle.ScrollY or NuiStyle.ScrollBoth || (canY && height > _scroll.Size.Y);
        if (showY)
        {
            width = Math.Max(1, width - NuiStyle.ScrollbarSize);
            height = skin.Font.GetMultilineStringSize(_text, HorizontalAlignment.Left, width, skin.FontSize).Y;
        }
        var content = new Vector2(width, canY ? Math.Max(height, _scroll.Size.Y) : _scroll.Size.Y);
        _painter.Size = content;
        _scroll.SetContent(content, mode is NuiStyle.ScrollX or NuiStyle.ScrollBoth, showY);
        _painter.QueueRedraw();
    }

    public override void _Draw()
    {
        if (Node.Border) DrawStyleBox(Ctx.Skin.Group, new Rect2(Vector2.Zero, Size));
        DrawEncouraged();
    }
}

/// <summary>NuiButton, NuiButtonImage and NuiButtonSelect (a toggle): "click" on release.</summary>
public partial class NuiButtonView : NuiElement
{
    bool _pressed;

    bool Selected => Node.Type == "button_select" && Bool("value", false);

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        bool interactive = Interactive;
        DrawStyleBox(Ctx.Skin.Button(Hover && interactive, (_pressed && Hover) || Selected, !interactive), r);
        if (Node.Type == "button_image")
        {
            if (Ctx.Skin.Texture(Text("label")) is { } tex)
                NuiImageView.DrawImage(this, tex, r.Grow(-3), 0, 0, 0, Ctx.Binds.Rect(Node["image_region"], Row), Colors.White);
        }
        else
        {
            var color = Foreground(interactive ? NuiSkin.Text : NuiSkin.TextDisabled);
            Ctx.Skin.DrawText(this, r.Grow(-4), Text("label"), 0, 0, color);
        }
        DrawEncouraged();
    }

    protected override void OnMouse(InputEventMouseButton mb, int button)
    {
        if (button != 0) return;
        if (mb.Pressed) { _pressed = true; QueueRedraw(); return; }
        bool click = _pressed && new Rect2(Vector2.Zero, Size).HasPoint(mb.Position);
        _pressed = false;
        QueueRedraw();
        if (!click) return;
        if (Node.Type == "button_select") WriteValue("value", JsonValue.Create(!Selected));
        SendEvent("click");
    }

    /// <summary>Scripted click (capture runs, tests).</summary>
    public void Click()
    {
        if (!Interactive) return;
        if (Node.Type == "button_select") WriteValue("value", JsonValue.Create(!Selected));
        SendEvent("click");
    }
}

/// <summary>NuiCheck: check box with a label to its right; toggles its bool bind.</summary>
public partial class NuiCheckView : NuiElement
{
    public override void _Draw()
    {
        var skin = Ctx.Skin;
        bool interactive = Interactive;
        float box = Math.Min(Size.Y, 22);
        var br = new Rect2(0, (Size.Y - box) / 2, box * 23f / 22f, box);
        var bg = skin.Texture(!interactive ? "nui_checkbox_d" : Hover ? "nui_checkbox_h" : "nui_checkbox_n");
        if (bg != null) DrawTextureRect(bg, br, false);
        else
        {
            DrawRect(br, NuiSkin.FieldBg);
            DrawRect(br, NuiSkin.Bronze, false, 1);
        }
        if (Bool("value", false))
        {
            var mark = br.Grow(-br.Size.Y * 0.18f);
            if (skin.Texture("nui_checkbox_x") is { } x) DrawTextureRect(x, mark, false);
            else DrawRect(mark, NuiSkin.Accent);
        }
        var tr = new Rect2(br.End.X + 6, 0, Math.Max(0, Size.X - br.End.X - 6), Size.Y);
        skin.DrawText(this, tr, Text("label"), 1, 0, Foreground(interactive ? NuiSkin.Text : NuiSkin.TextDisabled));
        DrawEncouraged();
    }

    protected override void OnMouse(InputEventMouseButton mb, int button)
    {
        if (button == 0 && !mb.Pressed && new Rect2(Vector2.Zero, Size).HasPoint(mb.Position))
            WriteValue("value", JsonValue.Create(!Bool("value", false)));
    }
}

/// <summary>NuiImage: a texture with aspect mode, alignment and optional region.</summary>
public partial class NuiImageView : NuiElement
{
    public override void _Draw()
    {
        if (Ctx.Skin.Texture(Text("value")) is { } tex)
            DrawImage(this, tex, new Rect2(Vector2.Zero, Size), Int("image_aspect"), Int("image_halign"), Int("image_valign"),
                Ctx.Binds.Rect(Node["image_region"], Row), Foreground(Colors.White));
        DrawEncouraged();
    }

    /// <summary>
    /// Draws an image into a box. Aspect: 0 fit, 1 fill (cover), 2 fit but never
    /// upscale, 3 exact pixels, 4 exact scaled by UI scale, 5 stretch. halign 0
    /// centre / 1 left / 2 right; valign 0 middle / 1 top / 2 bottom. A region is a
    /// pixel rectangle of the source image.
    /// </summary>
    public static void DrawImage(CanvasItem ci, Texture2D tex, Rect2 box, int aspect, int halign, int valign, NuiRect? region, Color modulate)
    {
        var src = region is { W: > 0, H: > 0 } rg ? new Rect2(rg.X, rg.Y, rg.W, rg.H) : new Rect2(Vector2.Zero, tex.GetSize());
        Vector2 size = src.Size;
        if (size.X <= 0 || size.Y <= 0 || box.Size.X <= 0 || box.Size.Y <= 0) return;
        float sx = box.Size.X / size.X, sy = box.Size.Y / size.Y;
        Vector2 draw = aspect switch
        {
            1 => size * Math.Max(sx, sy),
            2 => size * Math.Min(1, Math.Min(sx, sy)),
            3 or 4 => size,
            5 => box.Size,
            _ => size * Math.Min(sx, sy),
        };
        float x = halign switch { 1 => box.Position.X, 2 => box.End.X - draw.X, _ => box.Position.X + (box.Size.X - draw.X) / 2 };
        float y = valign switch { 1 => box.Position.Y, 2 => box.End.Y - draw.Y, _ => box.Position.Y + (box.Size.Y - draw.Y) / 2 };
        var dest = new Rect2(x, y, draw.X, draw.Y);
        // Crop to the box (fill and exact modes may overflow it).
        var clipped = dest.Intersection(box);
        if (clipped.Size.X <= 0 || clipped.Size.Y <= 0) return;
        var scale = new Vector2(src.Size.X / dest.Size.X, src.Size.Y / dest.Size.Y);
        var srcClipped = new Rect2(src.Position + (clipped.Position - dest.Position) * scale, clipped.Size * scale);
        ci.DrawTextureRectRegion(tex, clipped, srcClipped, modulate);
    }
}

/// <summary>NuiCombo: a drop-down of NuiComboEntry [label, value]; the bind holds the chosen entry's value.</summary>
public partial class NuiComboView : NuiElement
{
    PopupMenu _menu = null!;
    List<(string Label, int Value)> _entries = new();

    protected override void Build()
    {
        _menu = new PopupMenu();
        _menu.AddThemeFontOverride("font", Ctx.Skin.Font);
        _menu.AddThemeFontSizeOverride("font_size", Ctx.Skin.FontSize);
        _menu.AddThemeStyleboxOverride("panel", Ctx.Skin.Box("nui_windowh", 3, new Color(0, 0, 0, 0.95f)));
        _menu.AddThemeColorOverride("font_color", NuiSkin.Text);
        _menu.AddThemeColorOverride("font_hover_color", NuiSkin.Title);
        var hover = new StyleBoxFlat { BgColor = NuiSkin.Highlight };
        _menu.AddThemeStyleboxOverride("hover", hover);
        _menu.IdPressed += id =>
        {
            if (id >= 0 && id < _entries.Count) WriteValue("value", JsonValue.Create(_entries[(int)id].Value));
        };
        AddChild(_menu);
    }

    public override void Refresh()
    {
        _entries = Entries(Ctx.Binds.Resolve(Node["elements"], Row), Ctx.Binds);
        base.Refresh();
    }

    public static List<(string, int)> Entries(JsonNode? elements, NuiBinds binds)
    {
        var list = new List<(string, int)>();
        if (elements is not JsonArray a) return list;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] is JsonArray pair && pair.Count > 0)
                list.Add((binds.Text(pair[0]), pair.Count > 1 && NuiBinds.Number(pair[1]) is double d ? (int)d : i));
            else if (a[i] != null)
                list.Add((binds.Text(a[i]), i));
        }
        return list;
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        bool interactive = Interactive;
        DrawStyleBox(Ctx.Skin.Button(Hover && interactive, false, !interactive), r);
        int value = Int("value", -1);
        string label = _entries.FirstOrDefault(e => e.Value == value).Label ?? "";
        float arrow = Math.Min(14, Size.Y - 6);
        Ctx.Skin.DrawText(this, new Rect2(6, 0, Math.Max(0, Size.X - arrow - 14), Size.Y), label, 1, 0, Foreground(interactive ? NuiSkin.Text : NuiSkin.TextDisabled));
        var ar = new Rect2(Size.X - arrow - 6, (Size.Y - arrow) / 2, arrow, arrow);
        if (Ctx.Skin.Texture("nui_cnt_down") is { } tex) DrawTextureRect(tex, ar, false);
        else DrawPolygon([ar.Position, ar.Position + new Vector2(arrow, 0), ar.Position + new Vector2(arrow / 2, arrow)], [NuiSkin.Accent]);
        DrawEncouraged();
    }

    protected override void OnMouse(InputEventMouseButton mb, int button)
    {
        if (button != 0 || !mb.Pressed) return;
        _menu.Clear();
        int value = Int("value", -1);
        for (int i = 0; i < _entries.Count; i++)
        {
            _menu.AddRadioCheckItem(_entries[i].Label, i);
            _menu.SetItemChecked(i, _entries[i].Value == value);
        }
        _menu.Position = (Vector2I)(GetScreenPosition() + new Vector2(0, Size.Y));
        _menu.MinSize = new Vector2I((int)Size.X, 0);
        _menu.Popup();
    }
}

/// <summary>Shared painting for option and toggle groups split into equal cells.</summary>
public abstract partial class NuiChoiceView : NuiElement
{
    protected List<string> Labels = new();
    protected bool Vertical => Int("direction") == 1;

    public override void Refresh()
    {
        Labels = Ctx.Binds.Resolve(Node["elements"], Row) is JsonArray a ? a.Select(x => Ctx.Binds.Text(x)).ToList() : new List<string>();
        base.Refresh();
    }

    protected Rect2 Cell(int i)
    {
        int n = Math.Max(1, Labels.Count);
        return Vertical
            ? new Rect2(0, Size.Y * i / n, Size.X, Size.Y / n)
            : new Rect2(Size.X * i / n, 0, Size.X / n, Size.Y);
    }

    protected int HoverIndex()
    {
        if (!Hover) return -1;
        var p = GetLocalMousePosition();
        for (int i = 0; i < Labels.Count; i++) if (Cell(i).HasPoint(p)) return i;
        return -1;
    }

    public override void _GuiInput(InputEvent e)
    {
        base._GuiInput(e);
        if (e is InputEventMouseMotion) QueueRedraw();
    }

    protected override void OnMouse(InputEventMouseButton mb, int button)
    {
        if (button != 0 || mb.Pressed) return;
        for (int i = 0; i < Labels.Count; i++)
            if (Cell(i).HasPoint(mb.Position)) { WriteValue("value", JsonValue.Create(i)); return; }
    }
}

/// <summary>NuiOptions: radio buttons; the bind holds the selected index (-1 = none).</summary>
public partial class NuiOptionsView : NuiChoiceView
{
    public override void _Draw()
    {
        var skin = Ctx.Skin;
        bool interactive = Interactive;
        int selected = Int("value", -1), hover = HoverIndex();
        for (int i = 0; i < Labels.Count; i++)
        {
            var cell = Cell(i);
            float s = Math.Min(Math.Min(cell.Size.Y, 22), cell.Size.X);
            var rr = new Rect2(cell.Position.X, cell.Position.Y + (cell.Size.Y - s) / 2, s, s);
            var tex = skin.Texture(!interactive ? "nui_radial_d" : hover == i ? "nui_radial_h" : "nui_radial_n");
            if (tex != null) DrawTextureRect(tex, rr, false);
            else DrawCircle(rr.GetCenter(), s / 2, NuiSkin.FieldBg);
            if (i == selected)
            {
                var dot = rr.Grow(-s * 0.22f);
                if (skin.Texture("nui_radial_x") is { } x) DrawTextureRect(x, dot, false);
                else DrawCircle(dot.GetCenter(), dot.Size.X / 2, NuiSkin.Accent);
            }
            var tr = new Rect2(rr.End.X + 5, cell.Position.Y, Math.Max(0, cell.End.X - rr.End.X - 5), cell.Size.Y);
            skin.DrawText(this, tr, Labels[i], 1, 0, Foreground(interactive ? NuiSkin.Text : NuiSkin.TextDisabled));
        }
        DrawEncouraged();
    }
}

/// <summary>NuiToggles: a bar of buttons, one selected (the bind holds its index).</summary>
public partial class NuiTogglesView : NuiChoiceView
{
    public override void _Draw()
    {
        bool interactive = Interactive;
        int selected = Int("value", -1), hover = HoverIndex();
        for (int i = 0; i < Labels.Count; i++)
        {
            var cell = Cell(i);
            DrawStyleBox(Ctx.Skin.Button(hover == i && interactive, i == selected, !interactive), cell);
            Ctx.Skin.DrawText(this, cell.Grow(-4), Labels[i], 0, 0, Foreground(interactive ? NuiSkin.Text : NuiSkin.TextDisabled));
        }
        DrawEncouraged();
    }
}

/// <summary>NuiSlider (int) and NuiSliderFloat: value, min, max, step.</summary>
public partial class NuiSliderView : NuiElement
{
    bool _drag;
    bool IsFloat => Node.Type == "sliderf";

    (double Min, double Max, double Step) Range()
    {
        double min = Float("min", 0), max = Float("max", IsFloat ? 1 : 100), step = Float("step", IsFloat ? 0.01 : 1);
        if (max < min) (min, max) = (max, min);
        if (step <= 0) step = IsFloat ? 0.01 : 1;
        return (min, max, step);
    }

    const float Cursor = 12;

    public override void _Draw()
    {
        var (min, max, _) = Range();
        double v = Math.Clamp(Float("value", min), min, max);
        float t = max > min ? (float)((v - min) / (max - min)) : 0;
        float trackH = 6, left = Cursor / 2, width = Math.Max(0, Size.X - Cursor);
        var track = new Rect2(left, (Size.Y - trackH) / 2, width, trackH);
        DrawRect(track, NuiSkin.Track);
        DrawRect(new Rect2(track.Position, new Vector2(width * t, trackH)), Foreground(NuiSkin.Bronze));
        DrawRect(track, new Color(0, 0, 0, 0.8f), false, 1);
        float ch = Math.Min(Size.Y - 2, 18), cw = ch * 0.75f;
        var cr = new Rect2(left + width * t - cw / 2, (Size.Y - ch) / 2, cw, ch);
        if (Ctx.Skin.Texture("nui_slider") is { } tex) DrawTextureRect(tex, cr, false, Interactive ? Colors.White : new Color(0.6f, 0.6f, 0.6f));
        else DrawRect(cr, NuiSkin.Accent);
        DrawEncouraged();
    }

    protected override void OnMouse(InputEventMouseButton mb, int button)
    {
        if (button != 0) return;
        _drag = mb.Pressed;
        if (_drag) SetFrom(mb.Position.X);
    }

    public override void _GuiInput(InputEvent e)
    {
        base._GuiInput(e);
        if (e is InputEventMouseMotion mm && _drag && Interactive) SetFrom(mm.Position.X);
    }

    protected override bool OnWheel(float dx, float dy)
    {
        if (!Interactive) return false;
        var (min, max, step) = Range();
        Set(Math.Clamp(Float("value", min) + (dy != 0 ? dy : dx) * step, min, max));
        return true;
    }

    void SetFrom(float x)
    {
        var (min, max, step) = Range();
        float width = Math.Max(1, Size.X - Cursor);
        double t = Math.Clamp((x - Cursor / 2) / width, 0, 1);
        double v = min + Math.Round(t * (max - min) / step) * step;
        Set(Math.Clamp(v, min, max));
    }

    void Set(double v)
    {
        if (IsFloat)
        {
            v = Math.Round(v, 6);
            if (Math.Abs(Float("value", double.NaN) - v) < 1e-9) return;
            WriteValue("value", JsonValue.Create(v));
        }
        else
        {
            int iv = (int)Math.Round(v);
            if (Int("value", int.MinValue) == iv) return;
            WriteValue("value", JsonValue.Create(iv));
        }
    }
}

/// <summary>NuiProgress: a bar filled to value (0..1); NuiStyleForegroundColor colours the bar.</summary>
public partial class NuiProgressView : NuiElement
{
    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Ctx.Skin.Field, r);
        float t = (float)Math.Clamp(Float("value", 0), 0, 1);
        var inner = r.Grow(-3);
        var fill = new Rect2(inner.Position, new Vector2(inner.Size.X * t, inner.Size.Y));
        if (fill.Size.X > 0)
        {
            if (Ctx.Binds.Color(Node["foreground_color"], Row) is { } c)
            {
                var col = NuiSkin.ToColor(c);
                DrawRect(fill, col.Darkened(0.35f));
                DrawRect(new Rect2(fill.Position + new Vector2(0, fill.Size.Y * 0.3f), new Vector2(fill.Size.X, fill.Size.Y * 0.4f)), col);
            }
            else if (Ctx.Skin.Texture("nui_progress") is { } tex) DrawTextureRect(tex, fill, false);
            else DrawRect(fill, new Color(0.45f, 0.38f, 0.9f));
        }
        DrawEncouraged();
    }
}

/// <summary>NuiTextEdit: single or multi-line editable text; every edit updates the bind.</summary>
public partial class NuiTextEditView : NuiElement
{
    LineEdit? _line;
    TextEdit? _multi;
    bool _updating;

    protected override bool TakesMouse => true;

    protected override void Build()
    {
        var skin = Ctx.Skin;
        int max = Int("max", 0);
        var normal = skin.Field;
        var focus = new StyleBoxFlat { BgColor = NuiSkin.FieldBg, BorderColor = NuiSkin.Accent, ContentMarginLeft = 5, ContentMarginRight = 5, ContentMarginTop = 2, ContentMarginBottom = 2 };
        focus.SetBorderWidthAll(1);
        Control c;
        if (Bool("multiline", false))
        {
            _multi = new TextEdit
            {
                WrapMode = Bool("wordwrap", true) ? TextEdit.LineWrappingMode.Boundary : TextEdit.LineWrappingMode.None,
            };
            _multi.TextChanged += () =>
            {
                if (_updating) return;
                if (max > 0 && _multi.Text.Length > max)
                {
                    _updating = true;
                    _multi.Text = _multi.Text[..max];
                    _updating = false;
                }
                WriteValue("value", JsonValue.Create(_multi.Text));
            };
            c = _multi;
        }
        else
        {
            _line = new LineEdit { MaxLength = max > 0 ? max : 0 };
            _line.TextChanged += t => { if (!_updating) WriteValue("value", JsonValue.Create(t)); };
            c = _line;
        }
        c.AddThemeFontOverride("font", skin.Font);
        c.AddThemeFontSizeOverride("font_size", skin.FontSize);
        c.AddThemeColorOverride("font_color", NuiSkin.Text);
        c.AddThemeColorOverride("font_placeholder_color", NuiSkin.Placeholder);
        c.AddThemeColorOverride("caret_color", NuiSkin.Title);
        c.AddThemeColorOverride("selection_color", NuiSkin.Highlight);
        c.AddThemeStyleboxOverride("normal", normal);
        c.AddThemeStyleboxOverride("focus", focus);
        c.AddThemeStyleboxOverride("read_only", normal);
        c.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) Ctx.Window.Activate(); };
        AddChild(c);
    }

    public override void Refresh()
    {
        base.Refresh();
        string text = Text("value"), placeholder = Text("label");
        bool editable = Interactive;
        _updating = true;
        if (_line != null)
        {
            if (_line.Text != text) { int caret = _line.CaretColumn; _line.Text = text; _line.CaretColumn = Math.Min(caret, text.Length); }
            _line.PlaceholderText = placeholder;
            _line.Editable = editable;
            _line.AddThemeColorOverride("font_color", Foreground(NuiSkin.Text));
        }
        if (_multi != null)
        {
            if (_multi.Text != text) _multi.Text = text;
            _multi.PlaceholderText = placeholder;
            _multi.Editable = editable;
            _multi.AddThemeColorOverride("font_color", Foreground(NuiSkin.Text));
        }
        _updating = false;
    }

    public override void ApplyLayout()
    {
        base.ApplyLayout();
        Control? c = (Control?)_line ?? _multi;
        if (c == null) return;
        c.Position = Vector2.Zero;
        c.Size = Size;
    }
}

/// <summary>NuiColorPicker: saturation/value square, hue bar and alpha bar for a colour bind.</summary>
public partial class NuiColorPickerView : NuiElement
{
    int _drag = -1; // 0 = square, 1 = hue, 2 = alpha
    float _hue;

    const float Bar = 16, Gap = 4;

    Rect2 Square => new(0, 0, Math.Max(0, Size.X - 2 * (Bar + Gap)), Size.Y);
    Rect2 HueBar => new(Size.X - 2 * Bar - Gap, 0, Bar, Size.Y);
    Rect2 AlphaBar => new(Size.X - Bar, 0, Bar, Size.Y);

    Color Current()
    {
        var c = Ctx.Binds.Color(Node["value"], Row) ?? new NuiColor(255, 255, 255, 255);
        return NuiSkin.ToColor(c);
    }

    public override void _Draw()
    {
        var c = Current();
        float h = c.S > 0.001f && c.V > 0.001f ? c.H : _hue;
        var sq = Square;
        var hueColor = Color.FromHsv(h, 1, 1);
        DrawPolygon([sq.Position, sq.Position + new Vector2(sq.Size.X, 0), sq.End, sq.Position + new Vector2(0, sq.Size.Y)],
            [Colors.White, hueColor, hueColor, Colors.White]);
        DrawPolygon([sq.Position, sq.Position + new Vector2(sq.Size.X, 0), sq.End, sq.Position + new Vector2(0, sq.Size.Y)],
            [new Color(0, 0, 0, 0), new Color(0, 0, 0, 0), Colors.Black, Colors.Black]);
        var hb = HueBar;
        for (int i = 0; i < 6; i++)
        {
            float y0 = hb.Position.Y + hb.Size.Y * i / 6, y1 = hb.Position.Y + hb.Size.Y * (i + 1) / 6;
            Color a = Color.FromHsv(i / 6f, 1, 1), b = Color.FromHsv((i + 1) / 6f, 1, 1);
            DrawPolygon([new Vector2(hb.Position.X, y0), new Vector2(hb.End.X, y0), new Vector2(hb.End.X, y1), new Vector2(hb.Position.X, y1)], [a, a, b, b]);
        }
        var ab = AlphaBar;
        var opaque = new Color(c, 1);
        DrawRect(ab, new Color(0.3f, 0.3f, 0.3f));
        DrawPolygon([ab.Position, ab.Position + new Vector2(ab.Size.X, 0), ab.End, ab.Position + new Vector2(0, ab.Size.Y)],
            [opaque, opaque, new Color(opaque, 0), new Color(opaque, 0)]);
        // Markers.
        var sv = sq.Position + new Vector2(c.S * sq.Size.X, (1 - c.V) * sq.Size.Y);
        DrawArc(sv, 5, 0, MathF.Tau, 16, Colors.Black, 2);
        DrawArc(sv, 4, 0, MathF.Tau, 16, Colors.White, 1);
        float hy = hb.Position.Y + h * hb.Size.Y, ay = ab.Position.Y + (1 - c.A) * ab.Size.Y;
        DrawRect(new Rect2(hb.Position.X - 1, hy - 2, hb.Size.X + 2, 4), Colors.White, false, 1);
        DrawRect(new Rect2(ab.Position.X - 1, ay - 2, ab.Size.X + 2, 4), Colors.White, false, 1);
        foreach (var r in new[] { sq, hb, ab }) DrawRect(r, NuiSkin.Bronze, false, 1);
        DrawEncouraged();
    }

    protected override void OnMouse(InputEventMouseButton mb, int button)
    {
        if (button != 0) return;
        if (!mb.Pressed) { _drag = -1; return; }
        _drag = Square.HasPoint(mb.Position) ? 0 : HueBar.HasPoint(mb.Position) ? 1 : AlphaBar.HasPoint(mb.Position) ? 2 : -1;
        Pick(mb.Position);
    }

    public override void _GuiInput(InputEvent e)
    {
        base._GuiInput(e);
        if (e is InputEventMouseMotion mm && _drag >= 0 && Interactive) Pick(mm.Position);
    }

    void Pick(Vector2 p)
    {
        if (_drag < 0) return;
        var c = Current();
        float h = c.S > 0.001f && c.V > 0.001f ? c.H : _hue, s = c.S, v = c.V, a = c.A;
        var sq = Square;
        switch (_drag)
        {
            case 0:
                s = Math.Clamp((p.X - sq.Position.X) / Math.Max(1, sq.Size.X), 0, 1);
                v = 1 - Math.Clamp((p.Y - sq.Position.Y) / Math.Max(1, sq.Size.Y), 0, 1);
                break;
            case 1: h = Math.Clamp(p.Y / Math.Max(1, Size.Y), 0, 0.9999f); break;
            case 2: a = 1 - Math.Clamp(p.Y / Math.Max(1, Size.Y), 0, 1); break;
        }
        _hue = h;
        var n = Color.FromHsv(h, s, v, a);
        WriteValue("value", new NuiColor((int)Math.Round(n.R * 255), (int)Math.Round(n.G * 255), (int)Math.Round(n.B * 255), (int)Math.Round(n.A * 255)).ToJson());
    }
}

/// <summary>NuiChart: line and column slots over auto-scaled data.</summary>
public partial class NuiChartView : NuiElement
{
    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Ctx.Skin.Field, r);
        var slots = new List<(int Type, string Legend, Color Color, float[] Data)>();
        if (Ctx.Binds.Resolve(Node["value"], Row) is JsonArray arr)
            foreach (var s in arr.OfType<JsonObject>())
            {
                var col = Ctx.Binds.Color(s["color"], Row) is { } c ? NuiSkin.ToColor(c) : NuiSkin.Accent;
                slots.Add(((int)(NuiBinds.Number(s["type"]) ?? 0), Ctx.Binds.Text(s["legend"], Row), col, NuiBinds.Floats(Ctx.Binds.Resolve(s["data"], Row))));
            }
        var all = slots.SelectMany(s => s.Data).ToList();
        if (all.Count == 0) return;
        float min = Math.Min(0, all.Min()), max = all.Max();
        if (max - min < 1e-6f) max = min + 1;
        var plot = r.Grow(-6);
        float Y(float v) => plot.End.Y - (v - min) / (max - min) * plot.Size.Y;
        int columnSlots = slots.Count(s => s.Type == 1), ci = 0;
        foreach (var s in slots)
        {
            if (s.Data.Length == 0) continue;
            if (s.Type == 1)
            {
                float slotW = plot.Size.X / s.Data.Length;
                float barW = Math.Max(1, slotW / Math.Max(1, columnSlots) - 2);
                for (int i = 0; i < s.Data.Length; i++)
                {
                    float x = plot.Position.X + i * slotW + ci * (barW + 2) + 1;
                    float y0 = Y(Math.Max(0, min)), y1 = Y(s.Data[i]);
                    DrawRect(new Rect2(x, Math.Min(y0, y1), barW, Math.Abs(y0 - y1)), s.Color);
                }
                ci++;
            }
            else
            {
                var pts = new Vector2[s.Data.Length];
                for (int i = 0; i < pts.Length; i++)
                    pts[i] = new Vector2(plot.Position.X + (pts.Length == 1 ? plot.Size.X / 2 : plot.Size.X * i / (pts.Length - 1)), Y(s.Data[i]));
                if (pts.Length > 1) DrawPolyline(pts, s.Color, 2, true);
                foreach (var p in pts) DrawCircle(p, 2.5f, s.Color);
            }
        }
        // Legends, top left.
        float ly = plot.Position.Y;
        foreach (var s in slots.Where(s => s.Legend.Length > 0))
        {
            DrawRect(new Rect2(plot.Position.X + 2, ly + 5, 8, 8), s.Color);
            Ctx.Skin.DrawText(this, new Rect2(plot.Position.X + 14, ly, plot.Size.X - 14, 18), s.Legend, 1, 0, NuiSkin.Text, Ctx.Skin.FontSize - 2);
            ly += 16;
        }
        DrawEncouraged();
    }
}
