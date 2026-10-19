using OpenWinter.Client.UI.Kit;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>Draws interface models and holds the classic look's few constants.</summary>
public static class GuiDraw
{
    /// <summary>How much a highlighted (hovered) control brightens; the models' own hilite states carry the rest.</summary>
    public static readonly Color Hilite = new(1.25f, 1.22f, 1.12f);
    public static readonly Color DisabledText = new(0.5f, 0.5f, 0.5f);
    /// <summary>Selected rows and hovered list entries.</summary>
    public static readonly Color Selected = new(1f, 0.85f, 0.4f);

    /// <summary>Draws a pose with the model's origin at <paramref name="origin"/> (pixels in the canvas item).</summary>
    public static void Model(CanvasItem ci, ClassicAssets assets, GuiModel.Pose pose, Vector2 origin, Color modulate, Func<GuiModel.PosedMesh, Vector2>? offset = null, Func<GuiModel.PosedMesh, bool>? skip = null, Vector2 uvShift = default)
    {
        var rid = ci.GetCanvasItem();
        foreach (var m in pose.Meshes)
        {
            if (m.Alpha <= 0.001f || skip?.Invoke(m) == true) continue;
            if (assets.Texture(m.Texture) is not { } tex) continue;
            var shift = origin + (offset?.Invoke(m) ?? Vector2.Zero);
            var pts = new Vector2[m.Points.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = m.Points[i] + shift;
            var col = modulate with { A = modulate.A * m.Alpha };
            var cols = new Color[pts.Length];
            Array.Fill(cols, col);
            var uvs = Inset(m, tex);
            if (uvShift != Vector2.Zero) uvs = uvs.Select(uv => uv + uvShift).ToArray();
            RenderingServer.CanvasItemAddTriangleArray(rid, m.Indices, pts, cols, uvs, texture: tex.GetRid());
        }
    }

    /// <summary>The original's glow round a button's dark icon (pb_but_jour encouraged, scene journalpc): about (240, 180, 96).</summary>
    public static readonly Color GlowColor = new(0.94f, 0.70f, 0.38f);
    static readonly Dictionary<(string, int, int, int, int), (int W, int H, List<Vector2I> Pixels)> Glows = new();

    /// <summary>
    /// A button's "encouraged" plane: the original draws it as a warm glow hugging the icon's dark strokes, pulsing
    /// with the plane's alpha. Its texture region is the button's own, so the glow is made here from that region: the
    /// texels next to a dark one, not dark themselves.
    /// </summary>
    public static void Glow(CanvasItem ci, ClassicAssets assets, GuiModel.PosedMesh m, Vector2 origin, float alpha)
    {
        if (alpha <= 0.01f || m.Uvs.Length == 0 || assets.Texture(m.Texture) is not { } tex) return;
        int w = tex.GetWidth(), h = tex.GetHeight();
        int x0 = (int)MathF.Floor(m.Uvs.Min(p => p.X) * w), x1 = (int)MathF.Ceiling(m.Uvs.Max(p => p.X) * w);
        int y0 = (int)MathF.Floor(m.Uvs.Min(p => p.Y) * h), y1 = (int)MathF.Ceiling(m.Uvs.Max(p => p.Y) * h);
        var key = (m.Texture, x0, y0, x1, y1);
        if (!Glows.TryGetValue(key, out var glow))
        {
            int rw = Math.Max(0, x1 - x0), rh = Math.Max(0, y1 - y0);
            var pixels = new List<Vector2I>();
            var image = tex.GetImage();
            if (image != null && rw > 0 && rh > 0)
            {
                if (image.IsCompressed()) image.Decompress();
                var ink = new bool[rw, rh];
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                    {
                        var c = image.GetPixel(Math.Min(x0 + x, w - 1), Math.Min(y0 + y, h - 1));
                        ink[x, y] = c.A > 0.5f && 0.3f * c.R + 0.59f * c.G + 0.11f * c.B < 0.3f;
                    }
                // Dark texels joined to the region's edge are the button's frame, not its icon.
                var stack = new Stack<Vector2I>();
                for (int x = 0; x < rw; x++) { stack.Push(new(x, 0)); stack.Push(new(x, rh - 1)); }
                for (int y = 0; y < rh; y++) { stack.Push(new(0, y)); stack.Push(new(rw - 1, y)); }
                while (stack.Count > 0)
                {
                    var q = stack.Pop();
                    if (q.X < 0 || q.Y < 0 || q.X >= rw || q.Y >= rh || !ink[q.X, q.Y]) continue;
                    ink[q.X, q.Y] = false;
                    stack.Push(new(q.X + 1, q.Y)); stack.Push(new(q.X - 1, q.Y)); stack.Push(new(q.X, q.Y + 1)); stack.Push(new(q.X, q.Y - 1));
                }
                for (int y = 0; y < rh; y++)
                    for (int x = 0; x < rw; x++)
                    {
                        if (ink[x, y]) continue;
                        bool near = false;
                        for (int dy = -1; dy <= 1 && !near; dy++)
                            for (int dx = -1; dx <= 1 && !near; dx++)
                                near = x + dx >= 0 && y + dy >= 0 && x + dx < rw && y + dy < rh && ink[x + dx, y + dy];
                        if (near) pixels.Add(new Vector2I(x, y));
                    }
            }
            Glows[key] = glow = (rw, rh, pixels);
        }
        if (glow.Pixels.Count == 0) return;
        var r = new Rect2(m.Points[0] + origin, Vector2.Zero);
        foreach (var pt in m.Points) r = r.Expand(pt + origin);
        var cell = new Vector2(r.Size.X / glow.W, r.Size.Y / glow.H);
        var col = GlowColor with { A = alpha };
        foreach (var p in glow.Pixels) ci.DrawRect(new Rect2(r.Position + new Vector2(p.X * cell.X, p.Y * cell.Y), cell), col);
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GuiModel.PosedMesh, Vector2[]> Insets = new();

    /// <summary>
    /// The original stretches slices a few texels wide (gui_boxes_ingame's fill is a 2-texel column) over whole
    /// boxes without the neighbouring texels bleeding in; with filtering they would. Narrow spans are pulled in by
    /// half a texel on that axis so only the slice itself is sampled.
    /// </summary>
    static Vector2[] Inset(GuiModel.PosedMesh m, Texture2D tex)
    {
        if (Insets.TryGetValue(m, out var done)) return done;
        var uv = m.Uvs;
        if (uv.Length == 0) return uv;
        float u0 = uv.Min(p => p.X), u1 = uv.Max(p => p.X), v0 = uv.Min(p => p.Y), v1 = uv.Max(p => p.Y);
        float w = tex.GetWidth(), h = tex.GetHeight();
        bool narrowU = (u1 - u0) * w < 4 && u1 > u0, narrowV = (v1 - v0) * h < 4 && v1 > v0;
        if (!narrowU && !narrowV) { Insets.AddOrUpdate(m, uv); return uv; }
        var result = new Vector2[uv.Length];
        for (int i = 0; i < uv.Length; i++)
        {
            float x = uv[i].X, y = uv[i].Y;
            if (narrowU) x = Squeeze(x, u0, u1, 0.5f / w);
            if (narrowV) y = Squeeze(y, v0, v1, 0.5f / h);
            result[i] = new Vector2(x, y);
        }
        Insets.AddOrUpdate(m, result);
        return result;
    }

    static float Squeeze(float x, float lo, float hi, float half)
    {
        float a = lo + half, b = hi - half;
        if (b < a) return (lo + hi) / 2;
        return a + (x - lo) / (hi - lo) * (b - a);
    }

    /// <summary>
    /// A scroll bar's thumb as the original draws it: gui_scrollbar's ridges alternate along u, and the original lays
    /// u along the bar (a quarter turn), repeating the texture at its own length so the ridges run across the bar.
    /// </summary>
    public static void ScrollThumb(CanvasItem ci, Texture2D tex, Rect2 r)
    {
        var size = tex.GetSize();
        if (r.Size.X <= 0 || r.Size.Y <= 0 || size.X <= 0) return;
        // Turned a quarter clockwise about the thumb's top-right corner: local x runs down the bar, local y leftwards across it.
        ci.DrawSetTransform(r.Position + new Vector2(r.Size.X, 0), Mathf.Pi / 2, Vector2.One);
        for (float x = 0; x < r.Size.Y; x += size.X)
        {
            float len = Math.Min(size.X, r.Size.Y - x);
            // Only the ridged rows (3..12 of 16): the rows above and below hold the thumb's end caps.
            ci.DrawTextureRectRegion(tex, new Rect2(x, 0, len, r.Size.X), new Rect2(0, size.Y * 3 / 16f, len, size.Y * 10 / 16f));
        }
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>
    /// The installed scroll bar's thumb (ClassicScrollBar) as the original draws it, texel for texel (partyframes):
    /// the texture's first two rows upright as the top cap and its last two as the bottom cap (the
    /// rounded ends), columns 2..13 across the 12 px; between them the ridges, rows 2..13 across the bar (so its
    /// black edge columns are rows 2 and 13) and the columns along it from the cap down, repeating every 16 px.
    /// </summary>
    public static void ScrollThumbCapped(CanvasItem ci, Texture2D tex, Rect2 r)
    {
        var size = tex.GetSize();
        if (r.Size.X <= 0 || r.Size.Y <= 0 || size.X <= 0) return;
        float tu = size.X / 16f, tv = size.Y / 16f, cap = MathF.Min(2, MathF.Floor(r.Size.Y / 2));
        ci.DrawTextureRectRegion(tex, new Rect2(r.Position, new Vector2(r.Size.X, cap)), new Rect2(2 * tu, 0, 12 * tu, cap * tv));
        ci.DrawTextureRectRegion(tex, new Rect2(r.Position.X, r.End.Y - cap, r.Size.X, cap), new Rect2(2 * tu, size.Y - cap * tv, 12 * tu, cap * tv));
        float middle = r.Size.Y - 2 * cap;
        if (middle <= 0) return;
        // Turned a quarter clockwise about the middle's top-right corner: local x runs down the bar, local y leftwards.
        ci.DrawSetTransform(new Vector2(r.End.X, r.Position.Y + cap), Mathf.Pi / 2, Vector2.One);
        for (float x = 0; x < middle; x += 16)
        {
            float len = Math.Min(16, middle - x);
            ci.DrawTextureRectRegion(tex, new Rect2(x, 0, len, r.Size.X), new Rect2(0, 2 * tv, len * tu, 12 * tv));
        }
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>A rectangle between two nodes of a pose (text0/text1, title0/title1 ...), null when either is missing.</summary>
    public static Rect2? Box(GuiModel.Pose pose, string a, string b)
    {
        if (pose.Node(a) is not { } p || pose.Node(b) is not { } q) return null;
        var r = new Rect2(p, Vector2.Zero).Expand(q);
        return r.Size.X > 0 && r.Size.Y > 0 ? r : null;
    }
}

/// <summary>A control that draws one interface model, sized to it. The model's origin sits at <see cref="Origin"/>.</summary>
public partial class GuiModelView : Control
{
    public ClassicAssets Assets = null!;
    GuiModel? _model;
    string? _state;
    float _time;

    /// <summary>Keeps sampling the state's animation over time (pulsing states such as "encouraged").</summary>
    public bool Animate;

    public GuiModelView() { MouseFilter = MouseFilterEnum.Ignore; TextureRepeat = TextureRepeatEnum.Enabled; }

    public static GuiModelView Create(ClassicAssets assets, string resref)
    {
        var v = new GuiModelView { Assets = assets, Name = resref };
        v.Model = assets.Model(resref);
        return v;
    }

    public GuiModel? Model
    {
        get => _model;
        set { _model = value; Fit(); QueueRedraw(); }
    }

    public string? State
    {
        get => _state;
        set { if (_state == value) return; _state = value; QueueRedraw(); }
    }

    /// <summary>The model origin in this control's coordinates.</summary>
    public Vector2 Origin => _model == null ? Vector2.Zero : -_model.Bounds.Position;

    /// <summary>A model node in this control's coordinates.</summary>
    public Vector2? Node(string name) => _model?.Node(name) is { } p ? p + Origin : null;

    /// <summary>Places the control so the model's origin lands on <paramref name="point"/> in the parent.</summary>
    public void PlaceOrigin(Vector2 point) => Position = point - Origin;

    void Fit()
    {
        var size = _model?.Bounds.Size ?? Vector2.Zero;
        CustomMinimumSize = size;
        Size = size;
    }

    public override void _Process(double delta)
    {
        if (!Animate) return;
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_model == null) return;
        GuiDraw.Model(this, Assets, _model.PoseOf(_state, _time), Origin, Colors.White);
    }
}

/// <summary>Text in the original's bitmap fonts: alignment as the layouts give it, optional wrapping.</summary>
public partial class GuiLabel : Control
{
    public ClassicAssets Assets = null!;
    string _text = "";
    public GuiFont? Font;
    public Color Color = Colors.White;
    public float AlignH, AlignV;
    public bool Wrap;

    public GuiLabel() { MouseFilter = MouseFilterEnum.Ignore; }

    public string Text
    {
        get => _text;
        set { if (_text == value) return; _text = value ?? ""; QueueRedraw(); }
    }

    public static GuiLabel Create(ClassicAssets assets, string text = "", string font = "", Color? color = null, float alignH = 0, float alignV = 0.5f)
        => new() { Assets = assets, Font = assets.Font(font), _text = text, Color = color ?? Colors.White, AlignH = alignH, AlignV = alignV };

    public override void _Draw()
    {
        var font = Font ?? Assets.Font("");
        var text = NwnText.Strip(_text);
        var box = new Rect2(Vector2.Zero, Size);
        // A line wider than its box (many layouts keep the editor's default 0.83 x 0.17) hangs from the box's x by
        // its alignment: left from it, centred on it, right up to it (the death panel's title, the options build number).
        if (!Wrap && font.Measure(text) is var w && w > Size.X) box = new Rect2(-AlignH * w, 0, w, Size.Y);
        font.DrawBox(this, box, text, Color, AlignH, AlignV, Wrap);
    }
}

/// <summary>
/// A button drawn by an interface model whose animations are its states: up, down, hilite, disabled and, in toggle
/// mode, checkedup / checkeddown / uncheckedup / uncheckeddown (falling back to up / down). The caption is drawn
/// in the model's text0..text1 box when it has one, else over the whole model; an icon at its "icon" node.
/// </summary>
public partial class GuiButton : BaseButton
{
    public ClassicAssets Assets = null!;
    GuiModel? _model;
    string _caption = "";
    public GuiFont? Font;
    public Color CaptionColor = Colors.White;
    public float AlignH = 0.5f, AlignV = 0.5f;
    Texture2D? _icon;
    /// <summary>Icon size in pixels (0 = the texture's own size).</summary>
    public Vector2 IconSize;
    /// <summary>Overrides the model state (e.g. "encouraged").</summary>
    public string? ForcedState;
    /// <summary>
    /// Where the texture's greyed picture is, as a texture-coordinate shift from the up picture. The party bar's
    /// pb_but_* textures stack three 17-texel pictures 18 texels apart; their models' disabled animation changes
    /// nothing, and the original draws a disabled one from the third picture, two rows (0.5625) down (partyframes:
    /// the greyed PvP button matches those texels exactly).
    /// </summary>
    public Vector2 DisabledUvShift;
    float _time;

    public GuiButton()
    {
        TextureRepeat = TextureRepeatEnum.Enabled;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        ButtonDown += QueueRedraw;
        ButtonUp += QueueRedraw;
        Toggled += _ => QueueRedraw();
        FocusMode = FocusModeEnum.None;
        Pressed += () =>
        {
            var sound = ClickSound ?? (ToggleMode ? ClassicSounds.Check : ClassicSounds.Button);
            if (sound.Length > 0 && Assets != null) ClassicSounds.Play(Assets, sound);
        };
    }

    /// <summary>The sound a click plays: null for the original's default (gui_button, gui_check for check boxes), "" for none.</summary>
    public string? ClickSound;
    /// <summary>Meshes not to draw in a state (a checkbox model whose states don't hide its check mark: ctl_chk_pre).</summary>
    public Func<GuiModel.PosedMesh, string, bool>? Skip;

    public static GuiButton Create(ClassicAssets assets, string resref, string caption = "", string font = "")
    {
        var b = new GuiButton { Assets = assets, Name = resref, Font = assets.Font(font), _caption = caption };
        b.Model = assets.Model(resref);
        return b;
    }

    public GuiModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            var size = _model?.Bounds.Size ?? new Vector2(24, 24);
            CustomMinimumSize = size;
            Size = size;
            QueueRedraw();
        }
    }

    public string Caption
    {
        get => _caption;
        set { if (_caption == value) return; _caption = value ?? ""; QueueRedraw(); }
    }

    public Texture2D? Icon
    {
        get => _icon;
        set { _icon = value; QueueRedraw(); }
    }

    public Vector2 Origin => _model == null ? Vector2.Zero : -_model.Bounds.Position;

    /// <summary>Places the button so its model origin lands on <paramref name="point"/> in the parent.</summary>
    public void PlaceOrigin(Vector2 point) => Position = point - Origin;

    /// <summary>The model state for the button's current state.</summary>
    public string State()
    {
        if (ForcedState != null) return ForcedState;
        bool down = IsPressed() && !ToggleMode || ToggleMode && GetDrawMode() is DrawMode.Pressed or DrawMode.HoverPressed && Input.IsMouseButtonPressed(MouseButton.Left) && IsHovered();
        if (Disabled) return "disabled";
        if (ToggleMode)
        {
            string s = (ButtonPressed ? "checked" : "unchecked") + (down ? "down" : IsHovered() && ButtonPressed && Has("checkedhilite") ? "hilite" : "up");
            if (Has(s)) return s;
        }
        if (down) return "down";
        if (IsHovered() && Has("hilite")) return "hilite";
        return "up";
    }

    bool Has(string anim) => _model?.HasAnimation(anim) == true;

    public override void _Process(double delta)
    {
        if (ForcedState == null) return;
        _time += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_model == null) return;
        var state = State();
        var pose = _model.PoseOf(state, _time);
        bool hover = IsHovered() && !Disabled;
        if (state == "encouraged")
        {
            // The pulsing plane is drawn as the original's glow (GuiDraw.Glow), over the button at rest.
            var rest = _model.Rest;
            GuiDraw.Model(this, Assets, rest, Origin, hover ? GuiDraw.Hilite : Colors.White, uvShift: Disabled ? DisabledUvShift : default);
            foreach (var m in pose.Meshes)
                if (rest.Meshes.FirstOrDefault(r => r.Part == m.Part) is { } r && MathF.Abs(r.Alpha - m.Alpha) > 0.001f || rest.Meshes.All(r => r.Part != m.Part))
                    GuiDraw.Glow(this, Assets, m, Origin, m.Alpha);
        }
        else GuiDraw.Model(this, Assets, pose, Origin, hover ? GuiDraw.Hilite : Colors.White, skip: Skip == null ? null : m => Skip(m, state), uvShift: Disabled ? DisabledUvShift : default);
        if (_icon != null)
        {
            var size = IconSize == Vector2.Zero ? _icon.GetSize() : IconSize;
            var at = pose.Node("icon") is { } p ? p + Origin - size / 2 : (Size - size) / 2;
            DrawTextureRect(_icon, new Rect2(at, size), false, Disabled ? new Color(0.5f, 0.5f, 0.5f) : Colors.White);
        }
        if (_caption.Length > 0)
        {
            var box = GuiDraw.Box(pose, "text0", "text1") is { } b ? new Rect2(b.Position + Origin, b.Size) : new Rect2(Vector2.Zero, Size);
            var color = Disabled ? GuiDraw.DisabledText : CaptionColor;
            (Font ?? Assets.Font("")).DrawBox(this, box, NwnText.Strip(_caption), color, AlignH, AlignV);
        }
    }
}

/// <summary>A slider drawn by an interface model with min / max / thumb nodes (ctl_ig_slider, ctl_pre_sld_gamm).</summary>
public partial class GuiSlider : Godot.Range
{
    public ClassicAssets Assets = null!;
    GuiModel? _model;
    bool _drag;

    public GuiSlider()
    {
        TextureRepeat = TextureRepeatEnum.Enabled;
        ValueChanged += _ => QueueRedraw();
        MaxValue = 100;
        Step = 1;
    }

    public static GuiSlider Create(ClassicAssets assets, string resref)
    {
        var s = new GuiSlider { Assets = assets, Name = resref };
        s.Model = assets.Model(resref);
        return s;
    }

    public GuiModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            var size = _model?.Bounds.Size ?? new Vector2(100, 17);
            CustomMinimumSize = size;
            Size = size;
            QueueRedraw();
        }
    }

    public Vector2 Origin => _model == null ? Vector2.Zero : -_model.Bounds.Position;

    public void PlaceOrigin(Vector2 point) => Position = point - Origin;

    (float Min, float Max) Track()
    {
        var pose = _model?.Rest;
        float min = pose?.Node("min")?.X ?? 0, max = pose?.Node("max")?.X ?? Size.X;
        return (min + Origin.X, max + Origin.X);
    }

    float Fraction => MaxValue > MinValue ? (float)((Value - MinValue) / (MaxValue - MinValue)) : 0;

    public override void _Draw()
    {
        if (_model == null) return;
        var (min, max) = Track();
        float shift = (max - min) * Fraction;
        bool hover = IsHovered();
        GuiDraw.Model(this, Assets, _model.Rest, Origin, hover ? GuiDraw.Hilite : Colors.White,
            offset: m => m.Node.Equals("thumb", StringComparison.OrdinalIgnoreCase) ? new Vector2(shift, 0) : Vector2.Zero);
    }

    bool IsHovered() => GetGlobalRect().HasPoint(GetGlobalMousePosition());

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            _drag = mb.Pressed;
            if (mb.Pressed) SetFrom(mb.Position.X);
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && _drag)
        {
            SetFrom(mm.Position.X);
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } w)
        {
            Value += (w.ButtonIndex == MouseButton.WheelUp ? 1 : -1) * Math.Max(Step, (MaxValue - MinValue) / 20);
            AcceptEvent();
        }
    }

    void SetFrom(float x)
    {
        var (min, max) = Track();
        float f = max > min ? Math.Clamp((x - min) / (max - min), 0, 1) : 0;
        Value = MinValue + f * (MaxValue - MinValue);
    }
}

/// <summary>
/// The original's scroll bar: a thin track with arrow ends drawn from gui_scrollbar between a list model's
/// scrollmin / scrollmax nodes. It drives a <see cref="GuiList"/>'s offset.
/// </summary>
public partial class GuiScrollBar : Control
{
    public ClassicAssets Assets = null!;
    public float Page = 1, Total = 1, Offset;
    public Action<float>? Scrolled;
    bool _drag;
    float _grab;
    public const float Width = 14;

    public GuiScrollBar() { MouseFilter = MouseFilterEnum.Stop; }

    float Track => Math.Max(1, Size.Y - 2 * Width);
    float ThumbLength => Total <= Page ? Track : Math.Max(Width, Track * Page / Total);
    float ThumbTop => Width + (Total <= Page ? 0 : (Track - ThumbLength) * Offset / (Total - Page));

    public override void _Draw()
    {
        var tex = Assets.Texture("gui_scrollbar");
        var w = Size.X;
        var dark = new Color(0.08f, 0.07f, 0.05f, 0.9f);
        DrawRect(new Rect2(0, 0, w, Size.Y), dark);
        var edge = new Color(0.55f, 0.45f, 0.28f);
        DrawRect(new Rect2(0, 0, w, Size.Y), edge, false, 1);
        if (Total <= Page) return;
        // Arrows and thumb.
        var rectUp = new Rect2(0, 0, w, Width);
        var rectDown = new Rect2(0, Size.Y - Width, w, Width);
        var thumb = new Rect2(1, ThumbTop, w - 2, ThumbLength);
        if (tex != null) GuiDraw.ScrollThumb(this, tex, thumb);
        else DrawRect(thumb, edge);
        DrawArrow(rectUp, true, edge);
        DrawArrow(rectDown, false, edge);
    }

    void DrawArrow(Rect2 r, bool up, Color c)
    {
        var mid = r.GetCenter();
        float s = r.Size.X * 0.3f;
        var pts = up
            ? new[] { mid + new Vector2(-s, s * 0.6f), mid + new Vector2(s, s * 0.6f), mid + new Vector2(0, -s * 0.6f) }
            : new[] { mid + new Vector2(-s, -s * 0.6f), mid + new Vector2(s, -s * 0.6f), mid + new Vector2(0, s * 0.6f) };
        DrawColoredPolygon(pts, c);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (Total <= Page) return;
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                if (mb.Position.Y < Width) Set(Offset - Page / 4);
                else if (mb.Position.Y > Size.Y - Width) Set(Offset + Page / 4);
                else if (mb.Position.Y >= ThumbTop && mb.Position.Y <= ThumbTop + ThumbLength) { _drag = true; _grab = mb.Position.Y - ThumbTop; }
                else Set(Offset + (mb.Position.Y < ThumbTop ? -Page : Page));
            }
            else _drag = false;
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && _drag)
        {
            float f = (mm.Position.Y - _grab - Width) / Math.Max(1, Track - ThumbLength);
            Set(f * (Total - Page));
            AcceptEvent();
        }
    }

    public void Set(float offset)
    {
        Offset = Math.Clamp(offset, 0, Math.Max(0, Total - Page));
        Scrolled?.Invoke(Offset);
        QueueRedraw();
    }
}

/// <summary>
/// A list box of the original: its frame model, rows inside the model's text0..text1 box (or the whole model)
/// and a scroll bar between scrollmin and scrollmax. Rows are controls added with <see cref="AddRow"/>; plain
/// text rows with <see cref="AddText"/>.
/// </summary>
public partial class GuiList : Control
{
    public ClassicAssets Assets = null!;
    public GuiModelView? Frame;
    readonly Control _clip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
    readonly VBoxContainer _rows = new() { MouseFilter = MouseFilterEnum.Pass };
    public GuiScrollBar Bar = null!;
    ClassicScrollBar? _nativeBar;
    float _offset;

    public static GuiList Create(ClassicAssets assets, string resref)
    {
        var list = new GuiList { Assets = assets, Name = resref, MouseFilter = MouseFilterEnum.Pass };
        var model = assets.Model(resref);
        list.Frame = new GuiModelView { Assets = assets };
        list.Frame.Model = model;
        list.AddChild(list.Frame);
        list.Size = list.CustomMinimumSize = list.Frame.Size;
        var content = list.Frame.Node("text0") is { } a && list.Frame.Node("text1") is { } b
            ? new Rect2(a, Vector2.Zero).Expand(b)
            : new Rect2(new Vector2(6, 6), list.Size - new Vector2(12 + GuiScrollBar.Width, 12));
        list._clip.Position = content.Position;
        list._clip.Size = content.Size;
        list.AddChild(list._clip);
        list._rows.Size = new Vector2(content.Size.X, 0);
        list._rows.AddThemeConstantOverride("separation", 0);
        list._clip.AddChild(list._rows);
        list.Bar = new GuiScrollBar { Assets = assets };
        float x0 = list.Frame.Node("scrollmin")?.X ?? content.End.X + 2;
        float top = Math.Min(list.Frame.Node("scrollmin")?.Y ?? content.Position.Y, list.Frame.Node("scrollmax")?.Y ?? content.End.Y);
        float bottom = Math.Max(list.Frame.Node("scrollmin")?.Y ?? content.Position.Y, list.Frame.Node("scrollmax")?.Y ?? content.End.Y);
        list.Bar.Position = new Vector2(x0 - GuiScrollBar.Width / 2, top);
        list.Bar.Size = new Vector2(GuiScrollBar.Width, Math.Max(GuiScrollBar.Width * 2, bottom - top));
        list.Bar.Scrolled = o => { list._offset = o; list.Layout(); };
        list.AddChild(list.Bar);
        list._rows.Resized += list.Layout;
        return list;
    }

    /// <summary>Lists whose scroll nodes mark the thumb track use the installed end caps outside it.</summary>
    public void UseClassicScrollBar()
    {
        if (_nativeBar != null) return;
        var arrow = Assets.Model("pb_scrl_up")?.Bounds.Size ?? new Vector2(14, 14);
        Bar.Visible = false;
        _nativeBar = new ClassicScrollBar { Assets = Assets,
            Position = Bar.Position + new Vector2(GuiScrollBar.Width / 2, -arrow.Y),
            Size = new Vector2(arrow.X, Bar.Size.Y + arrow.Y * 2), Scrolled = ScrollTo };
        AddChild(_nativeBar);
        Layout();
    }

    public Vector2 ContentSize => _clip.Size;
    /// <summary>Moves the text box's left edge (a window that draws its own icon beside the text uses less margin).</summary>
    public void MoveContentLeft(float dx)
    {
        _clip.Position += new Vector2(dx, 0);
        _clip.Size -= new Vector2(dx, 0);
        Clear();
    }
    public Vector2 Origin => Frame?.Origin ?? Vector2.Zero;
    public void PlaceOrigin(Vector2 point) => Position = point - Origin;

    public IEnumerable<Control> Rows => _rows.GetChildren().OfType<Control>();

    public void Clear()
    {
        foreach (var c in _rows.GetChildren()) { _rows.RemoveChild(c); c.QueueFree(); }
        _offset = 0;
        _rows.Size = new Vector2(_clip.Size.X, 0);
        Layout();
    }

    public T AddRow<T>(T row) where T : Control
    {
        row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _rows.AddChild(row);
        Callable.From(Layout).CallDeferred();
        return row;
    }

    /// <summary>A text row; clickable (with hover and selection colours) when <paramref name="pressed"/> is given.</summary>
    public GuiListRow AddText(string text, Action? pressed = null, string font = "", Color? color = null, bool wrap = false)
    {
        var row = new GuiListRow { Assets = Assets, Font = Assets.Font(font), Text = text, Color = color ?? Colors.White, Wrap = wrap, Clickable = pressed != null };
        if (pressed != null) row.Pressed += pressed;
        row.Width = _clip.Size.X;
        return AddRow(row);
    }

    void Layout()
    {
        float total = _rows.GetCombinedMinimumSize().Y;
        _rows.Size = new Vector2(_clip.Size.X, total);
        Bar.Total = total;
        Bar.Page = _clip.Size.Y;
        _offset = Math.Clamp(_offset, 0, Math.Max(0, total - _clip.Size.Y));
        Bar.Offset = _offset;
        _rows.Position = new Vector2(0, -_offset);
        Bar.QueueRedraw();
        if (_nativeBar != null)
        {
            _nativeBar.Total = total; _nativeBar.Page = Bar.Page; _nativeBar.Offset = _offset;
            _nativeBar.QueueRedraw();
        }
    }

    public void ScrollTo(float offset) => Bar.Set(offset);

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w)
        {
            Bar.Set(_offset + (w.ButtonIndex == MouseButton.WheelUp ? -1 : 1) * 3 * Assets.Font("").LineHeight);
            AcceptEvent();
        }
    }
}

/// <summary>A text row in a <see cref="GuiList"/>.</summary>
public partial class GuiListRow : Control
{
    public ClassicAssets Assets = null!;
    public GuiFont Font = null!;
    public string Text = "";
    public Color Color = Colors.White;
    public bool Wrap, Clickable, Selected;
    public float Width;
    public event Action? Pressed;
    public object? Tag;

    public override void _Ready()
    {
        MouseFilter = Clickable ? MouseFilterEnum.Stop : MouseFilterEnum.Pass;
        MouseEntered += QueueRedraw;
        MouseExited += QueueRedraw;
        UpdateHeight();
    }

    void UpdateHeight()
    {
        float w = Width > 0 ? Width : Size.X;
        int lines = Wrap ? Font.Wrap(NwnText.Strip(Text), w - 4).Count : 1;
        CustomMinimumSize = new Vector2(0, lines * Font.LineHeight + 2);
    }

    public override void _Draw()
    {
        bool hover = Clickable && GetGlobalRect().HasPoint(GetGlobalMousePosition());
        var c = Selected || hover ? GuiDraw.Selected : Color;
        Font.DrawBox(this, new Rect2(2, 1, Size.X - 4, Size.Y - 2), NwnText.Strip(Text), c, 0, 0, Wrap);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (Clickable && e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Pressed?.Invoke();
            AcceptEvent();
        }
    }
}
