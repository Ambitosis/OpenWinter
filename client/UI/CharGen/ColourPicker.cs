using System.Runtime.CompilerServices;
using OpenWinter.Client.Rendering;
using OpenWinter.Client.Views;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.CharGen;

/// <summary>
/// One colour channel's palette texture (pal_skin01, pal_hair01, pal_tattoo01, ...), read from the
/// game data: each row is a colour index and runs from shadow (left) to highlight (right), the way
/// the tinter shades a PLT texture with it. The rows are authored in blocks of four related shades
/// (a family); <see cref="Families"/> recovers that layout and orders a family's shades light to dark
/// when they share a hue.
/// </summary>
public sealed class PaletteSheet
{
    public const int FamilySize = 4;
    /// <summary>The first rows (0-63) are the channel's own palette; later rows are the shared extended colours.</summary>
    public const int MainRows = 64;
    public readonly string Name;
    public readonly Image? Image;
    public readonly Texture2D? Texture;
    public int Rows => Image?.GetHeight() ?? 0;
    public int Width => Image?.GetWidth() ?? 1;

    PaletteSheet(string name, Image? image)
    {
        Name = name;
        Image = image;
        if (image != null) Texture = ImageTexture.CreateFromImage(image);
    }

    static readonly ConditionalWeakTable<ClientContent, Dictionary<string, PaletteSheet>> Cache = new();

    public static PaletteSheet Get(ClientContent? content, string name)
    {
        if (content == null) return new PaletteSheet(name, null);
        var sheets = Cache.GetValue(content, _ => new Dictionary<string, PaletteSheet>(StringComparer.OrdinalIgnoreCase));
        if (sheets.TryGetValue(name, out var hit)) return hit;
        Image? img = null;
        if (content.Textures.Source.Get(name) is { } data && TextureFactory.ToImage(data) is { } image)
        {
            if (image.IsCompressed()) image.Decompress();
            image.ClearMipmaps();
            if (data.FlipV) image.FlipY();
            image.Convert(Image.Format.Rgba8);
            img = image;
        }
        return sheets[name] = new PaletteSheet(name, img);
    }

    /// <summary>Shade column for a light level 0 (shadow) to 1 (highlight); the ends of a row are near black and near white.</summary>
    public float Column(float light) => Mathf.Lerp(Width * 0.16f, Width * 0.86f, Math.Clamp(light, 0, 1));

    /// <summary>A colour index at a light level (0.62 is how it reads on a lit model).</summary>
    public Color Color(int row, float light = 0.62f)
    {
        if (Image == null) return Colors.Gray;
        return Image.GetPixel(Math.Clamp((int)Column(light), 0, Width - 1), Math.Clamp(row, 0, Rows - 1));
    }

    List<int[]>? _families;

    /// <summary>
    /// The colour indices in families of related shades, in palette order. Each family is a block of
    /// four rows. Inside a block, shades of one hue sit together, light to dark (some palettes pair a
    /// light and a dark shade of two hues in one block; tattoo palettes do).
    /// </summary>
    public IReadOnlyList<int[]> Families
    {
        get
        {
            if (_families != null) return _families;
            _families = new List<int[]>();
            for (int start = 0; start < Rows; start += FamilySize)
            {
                var rows = Enumerable.Range(start, Math.Min(FamilySize, Rows - start)).ToList();
                // Cluster by hue (greys together), clusters in palette order, each light to dark.
                var clusters = new List<List<int>>();
                foreach (int r in rows)
                {
                    var c = Color(r);
                    var home = clusters.FirstOrDefault(k => SameHue(Color(k[0]), c));
                    if (home != null) home.Add(r); else clusters.Add(new List<int> { r });
                }
                _families.Add(clusters.SelectMany(k => k.OrderByDescending(r => Luma(Color(r)))).ToArray());
            }
            return _families;
        }
    }

    static bool Grey(Color c) => c.S < 0.18f || c.V < 0.12f;

    static bool SameHue(Color a, Color b)
    {
        if (Grey(a) || Grey(b)) return Grey(a) && Grey(b);
        float d = Math.Abs(a.H - b.H);
        return Math.Min(d, 1 - d) < 0.045f; // about 16 degrees
    }

    static float Luma(Color c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;
}

/// <summary>
/// A round colour swatch shaded with its palette row (highlight at the top left, shadow at the
/// bottom right), so it reads like the colour on a model. A ring marks the selected one.
/// </summary>
public partial class ColourSwatch : Control
{
    public PaletteSheet Sheet = null!;
    public int Index;
    bool _selected, _hover;
    public Action<int>? Picked;

    public bool Selected { get => _selected; set { _selected = value; QueueRedraw(); } }

    public static ColourSwatch Create(PaletteSheet sheet, int index, float size, string tooltip)
    {
        var s = new ColourSwatch
        {
            Sheet = sheet, Index = index, CustomMinimumSize = new Vector2(size, size), TooltipText = tooltip,
            MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand, FocusMode = FocusModeEnum.None,
        };
        s.MouseEntered += () => { s._hover = true; s.QueueRedraw(); };
        s.MouseExited += () => { s._hover = false; s.QueueRedraw(); };
        return s;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Picked?.Invoke(Index);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var c = Size * 0.5f;
        float r = Math.Min(Size.X, Size.Y) * 0.5f - (_selected ? 4f : 2f);
        if (_hover && !_selected) r += 1f;
        // The outline of a disc (a convex polygon; Godot fans it), each point taking the palette shade
        // for how lit it is: up and left are brighter.
        const int n = 32;
        var pts = new Vector2[n];
        var uvs = new Vector2[n];
        var cols = new Color[n];
        float v = (Index + 0.5f) / Math.Max(1, Sheet.Rows);
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Tau * i / n;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            pts[i] = c + d * r;
            float light = 0.6f - 0.3f * d.Y - 0.1f * d.X;
            uvs[i] = new Vector2(Sheet.Column(light) / Sheet.Width, v);
        }
        Array.Fill(cols, Colors.White);
        if (Sheet.Texture != null) DrawPolygon(pts, cols, uvs, Sheet.Texture);
        else DrawCircle(c, r, Colors.Gray);
        // A hairline so dark colours stand off the dark panel.
        DrawArc(c, r, 0, Mathf.Tau, 40, new Color(1, 1, 1, _hover ? 0.5f : 0.16f), 1f, true);
        if (_selected)
        {
            DrawArc(c, r + 1.5f, 0, Mathf.Tau, 48, Colors.White, 2f, true);
            DrawArc(c, r + 3.5f, 0, Mathf.Tau, 48, Palette.Accent, 1.5f, true);
        }
    }
}

/// <summary>
/// The appearance step's colour chooser: a tab per channel (skin, hair, tattoo 1, tattoo 2) showing
/// its current colour, and below it every colour index the channel's palette offers, as families of
/// shades laid out in the palette's own order (main palette, then the extended colours).
/// </summary>
public partial class ColourPicker : VBoxContainer
{
    ClientContent? _content;
    string[] _names = Array.Empty<string>(), _palettes = Array.Empty<string>();
    int[] _values = Array.Empty<int>();
    int _channel;
    Action<int, int>? _changed;
    HBoxContainer _tabs = null!;
    VBoxContainer _grid = null!;
    Label _readout = null!;
    readonly List<ColourSwatch> _swatches = new();
    readonly List<(PanelContainer Panel, int[] Rows)> _familyPanels = new();
    public const float SwatchSize = 28;

    /// <summary>The swatch of the current channel's colour.</summary>
    public Control? SelectedSwatch => _swatches.FirstOrDefault(s => s.Selected);

    /// <summary>The channel being edited.</summary>
    public int Channel => _channel;

    public static ColourPicker Create(ClientContent? content, string[] names, string[] palettes, int[] values, Action<int, int> changed, int channel = 0)
    {
        var p = new ColourPicker { _content = content, _names = names, _palettes = palettes, _values = values, _changed = changed, _channel = channel };
        p.AddThemeConstantOverride("separation", 10);
        p._tabs = Ui.HBox(6);
        p.AddChild(p._tabs);
        p._readout = Ui.Text("", Ui.TextStyle.Caption, Palette.TextSecondary);
        p.AddChild(p._readout);
        p._grid = Ui.VBox(8);
        p.AddChild(p._grid);
        p.BuildTabs();
        p.BuildGrid();
        return p;
    }

    /// <summary>Called when the player switches channel (the window zooms to the face for hair).</summary>
    public Action<int>? ChannelChanged;

    public void SelectChannel(int channel)
    {
        _channel = Math.Clamp(channel, 0, _names.Length - 1);
        BuildTabs();
        BuildGrid();
        ChannelChanged?.Invoke(_channel);
    }

    /// <summary>Picks a colour for the current channel (as a click would).</summary>
    public void Pick(int index)
    {
        _values[_channel] = index;
        foreach (var s in _swatches) s.Selected = s.Index == index;
        foreach (var (panel, rows) in _familyPanels) panel.AddThemeStyleboxOverride("panel", FamilyStyle(rows.Contains(index)));
        BuildTabs();
        UpdateReadout();
        _changed?.Invoke(_channel, index);
    }

    void UpdateReadout()
    {
        var sheet = PaletteSheet.Get(_content, _palettes[_channel]);
        int fam = sheet.Families.ToList().FindIndex(f => f.Contains(_values[_channel]));
        _readout.Text = $"{_names[_channel]}: colour {_values[_channel]} of {Math.Max(0, sheet.Rows - 1)}" + (fam >= 0 ? $"  ·  family {fam + 1}" : "");
    }

    void BuildTabs()
    {
        foreach (var c in _tabs.GetChildren()) { _tabs.RemoveChild(c); c.QueueFree(); }
        for (int i = 0; i < _names.Length; i++)
        {
            int ch = i;
            bool on = i == _channel;
            var tab = new Button { ToggleMode = true, ButtonPressed = on, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 42), SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = $"Choose the {_names[i].ToLowerInvariant()} colour" };
            var normal = Styles.Box(new Color(0, 0, 0, 0.25f), Palette.RadiusSmall, Palette.Line, 1, 8, 4);
            var sel = Styles.Box(Palette.Bg3, Palette.RadiusSmall, Palette.Accent, 1, 8, 4);
            sel.ShadowColor = Palette.AccentGlow;
            sel.ShadowSize = 8;
            tab.AddThemeStyleboxOverride("normal", on ? sel : normal);
            tab.AddThemeStyleboxOverride("hover", on ? sel : Styles.Box(Palette.Bg3, Palette.RadiusSmall, Palette.AccentDim, 1, 8, 4));
            tab.AddThemeStyleboxOverride("pressed", sel);
            tab.AddThemeStyleboxOverride("hover_pressed", sel);
            var row = Ui.HBox(8);
            row.MouseFilter = MouseFilterEnum.Ignore;
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 8; row.OffsetRight = -6;
            var sw = ColourSwatch.Create(PaletteSheet.Get(_content, _palettes[i]), _values[i], 24, "");
            sw.MouseFilter = MouseFilterEnum.Ignore;
            sw.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(sw);
            var names = Ui.VBox(-2);
            names.MouseFilter = MouseFilterEnum.Ignore;
            names.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            names.AddChild(Ui.Text(_names[i], Ui.TextStyle.Small, on ? Palette.AccentBright : Palette.Text));
            names.AddChild(Ui.Text($"#{_values[i]}", Ui.TextStyle.Caption, Palette.TextMuted, 11));
            foreach (var l in names.GetChildren().OfType<Control>()) l.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(names);
            tab.AddChild(row);
            tab.Pressed += () => SelectChannel(ch);
            _tabs.AddChild(tab);
        }
    }

    static StyleBoxFlat FamilyStyle(bool current) =>
        Styles.Box(current ? Palette.Accent with { A = 0.1f } : new Color(0, 0, 0, 0.28f), 22, current ? Palette.AccentDim : Palette.Line, 1, 5, 4);

    void BuildGrid()
    {
        foreach (var c in _grid.GetChildren()) { _grid.RemoveChild(c); c.QueueFree(); }
        _swatches.Clear();
        _familyPanels.Clear();
        var sheet = PaletteSheet.Get(_content, _palettes[_channel]);
        if (sheet.Rows == 0) { _grid.AddChild(Ui.Text("This palette isn't installed.", Ui.TextStyle.Secondary)); return; }
        var families = sheet.Families;
        void Section(string title, IEnumerable<int[]> list)
        {
            var items = list.ToList();
            if (items.Count == 0) return;
            _grid.AddChild(Ui.Section(title));
            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 8);
            flow.AddThemeConstantOverride("v_separation", 8);
            _grid.AddChild(flow);
            foreach (var fam in items)
            {
                var panel = new PanelContainer();
                panel.AddThemeStyleboxOverride("panel", FamilyStyle(fam.Contains(_values[_channel])));
                var strip = Ui.HBox(2);
                panel.AddChild(strip);
                foreach (int idx in fam)
                {
                    var sw = ColourSwatch.Create(sheet, idx, SwatchSize, $"{_names[_channel]} colour {idx}");
                    sw.Selected = idx == _values[_channel];
                    sw.Picked = Pick;
                    strip.AddChild(sw);
                    _swatches.Add(sw);
                }
                _familyPanels.Add((panel, fam));
                flow.AddChild(panel);
            }
        }
        Section("Palette", families.Where(f => f[0] < PaletteSheet.MainRows));
        Section("Extended colours", families.Where(f => f[0] >= PaletteSheet.MainRows));
        UpdateReadout();
    }
}
