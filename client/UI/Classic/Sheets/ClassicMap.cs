using OpenWinter.Client.UI.Map;
using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// The original's small map window (M), near the top-left: a row of buttons (add map pin, zoom in, zoom
/// out, close) over the explored tiles of the area at 16 px a tile, centred on the whole area, with map pins and the
/// player's arrow. The original has no frame model for it (pnl_map_mini is missing from the game data): a grey
/// hairline frame as in the reference. Tiles and exploration come from the shared <see cref="AreaMap"/>.
/// </summary>
public partial class ClassicMiniMap : ClassicWindow
{
    MapCanvas _canvas = null!;
    // Retained for existing capture helpers; the visible diamond is Add Map Pin.
    public Action? Enlarge;
    bool _addingPin;
    uint _pinArea;
    ClassicPopup? _pinEditor;
    const int PushpinCursor = 45;
    FrameBox _frame = null!;
    GuiButton _close = null!;
    static readonly Vector2 WindowSize = new(74, 94);

    protected override GuiPanel Build()
    {
        var a = Assets;
        var p = GuiPanel.FromModel(a, "gui_empty");
        p.Size = p.CustomMinimumSize = WindowSize;
        var bg = _frame = new FrameBox { Size = WindowSize, Divider = 19 };
        p.AddChild(bg);
        var addPin = PieceButton(a, 0, new Vector2(2, 2));
        addPin.SetMeta(ClassicTooltip.Meta, a.Tlk(67591));
        addPin.Pressed += () => SetAddingPin(!_addingPin);
        p.AddChild(addPin);
        p.Register("AddPin", addPin);
        var zin = GuiButton.Create(a, "ctl_map_zoomin");
        zin.Position = new Vector2(19, 2);
        zin.Pressed += () => SetZoom(Math.Min(2, _canvas.Zoom * 2));
        p.AddChild(zin);
        p.Register("ZoomIn", zin);
        var zout = GuiButton.Create(a, "ctl_map_zoomout");
        zout.Position = new Vector2(36, 2);
        zout.Pressed += () => SetZoom(Math.Max(1, _canvas.Zoom / 2));
        p.AddChild(zout);
        p.Register("ZoomOut", zout);
        var x = _close = GuiButton.Create(a, "ctl_btn_x");
        x.Position = new Vector2(56, 1);
        p.AddChild(x);
        p.Register("XButton", x);
        _canvas = new MapCanvas { Name = "MapCanvas", Assets = a, Map = AreaMap.Of(Ctx.Session), Session = Ctx.Session, Position = new Vector2(5, 24), Size = new Vector2(64, 64) };
        _canvas.PointClicked = OnMapPoint;
        p.AddChild(_canvas);
        Anchor = LayoutPreset.TopLeft;
        Offset = new Vector2(8, 64);
        return p;
    }

    public override void _Ready()
    {
        base._Ready();
        _canvas.Map.Changed += OnMapChanged;
    }

    public override void _ExitTree()
    {
        _canvas.Map.Changed -= OnMapChanged;
        SetAddingPin(false);
        if (IsInstanceValid(_pinEditor)) _pinEditor!.Cancel();
    }

    void SetZoom(float zoom)
    {
        _canvas.Zoom = zoom;
        ResizeMap();
    }

    void ResizeMap()
    {
        if (Panel == null || _canvas.Map.Width <= 0 || _canvas.Map.Height <= 0) return;
        // Native square4 and rectangle8x3: tile raster plus 5px margins and 20px header.
        var raster = new Vector2(_canvas.Map.Width, _canvas.Map.Height) * (16 * _canvas.Zoom);
        raster = raster.Min(new Vector2(336, 384));
        var size = raster + new Vector2(10, 30);
        size.X = Math.Max(74, size.X); // Keep the observed header controls accessible.
        Panel.Size = Panel.CustomMinimumSize = _frame.Size = size;
        _canvas.Size = new Vector2(size.X - 10, raster.Y);
        _close.Position = new Vector2(size.X - 18, 1);
        _frame.QueueRedraw();
        Place();
    }

    void OnMapChanged()
    {
        ResizeMap();
        if (_pinArea == 0 || _pinArea == _canvas.Map.AreaId) return;
        SetAddingPin(false);
        if (IsInstanceValid(_pinEditor)) _pinEditor!.Cancel();
        _pinEditor = null;
        _pinArea = 0;
    }

    public override void Close()
    {
        SetAddingPin(false);
        if (IsInstanceValid(_pinEditor)) _pinEditor!.Cancel();
        _pinEditor = null;
        base.Close();
    }

    void SetAddingPin(bool active)
    {
        _addingPin = active;
        if (active)
        {
            _pinArea = _canvas.Map.AreaId;
            HudParts.Cursors.InterfaceCursor = PushpinCursor;
        }
        else if (HudParts.Cursors.InterfaceCursor == PushpinCursor)
            HudParts.Cursors.InterfaceCursor = null;
    }

    void OnMapPoint(Vector2 at)
    {
        var map = _canvas.Map;
        if (_addingPin)
        {
            if (_pinArea != map.AreaId || at.X < 0 || at.Y < 0 || at.X >= map.Extent.X || at.Y >= map.Extent.Y) return;
            uint area = map.AreaId;
            SetAddingPin(false);
            _pinEditor = ClassicMapPinEditor.Show(Ctx.Hud, Assets, "", note =>
            {
                if (IsOpen && map.AreaId == area) map.AddPin(area, at, note);
            });
        }
        else if (_canvas.PinAt(_canvas.WorldToScreen(at)) is { Personal: true } pin)
        {
            uint area = _pinArea = map.AreaId;
            _pinEditor = ClassicMapPinEditor.Show(Ctx.Hud, Assets, pin.Note,
                note => { if (IsOpen && map.AreaId == area) map.EditPin(area, pin.Id, note); },
                () => { if (IsOpen && map.AreaId == area) map.RemovePin(area, pin.Id); });
        }
    }

    public override void Refresh() { _canvas.Map.Refresh(); ResizeMap(); }

    /// <summary>A 16-pixel button drawn from a cell of gui_map_pieces (0 the diamond, 1 zoom in, 2 zoom out).</summary>
    public static TextureButton PieceButton(ClassicAssets a, int cell, Vector2 at)
    {
        var tex = a.Texture("gui_map_pieces");
        Texture2D? region = tex == null ? null : new AtlasTexture { Atlas = tex, Region = new Rect2(cell * tex.GetWidth() / 4f, 0, tex.GetWidth() / 4f, tex.GetHeight() / 4f) };
        return new TextureButton { TextureNormal = region, Position = at, Size = new Vector2(16, 16), IgnoreTextureSize = true, StretchMode = TextureButton.StretchModeEnum.Scale, FocusMode = FocusModeEnum.None };
    }
}

/// <summary>
/// The large area map (map_area): the area at a scale that fits its viewport, with the map's name, pins (hover
/// for their note) and the player; ◆ returns to the small map.
/// </summary>
public partial class ClassicAreaMap : ClassicWindow
{
    MapCanvas _canvas = null!;
    GuiLabel? _name, _pin;
    public Action? Shrink;

    protected override GuiPanel Build()
    {
        var a = Assets;
        var p = GuiPanel.FromLayout(a, "map_area") ?? GuiPanel.FromModel(a, "pnl_map_area");
        if (p.Get<GuiButton>("MinMaximize") is { } mm) mm.Pressed += () => Shrink?.Invoke();
        _name = p.Get<GuiLabel>("MapNameLabel");
        _pin = p.Get<GuiLabel>("PinTextLabel");
        if (p.Get<GuiLabel>("TGuiLabel") is { } caption) caption.Text = a.Tlk(6379);
        var v0 = p.Node("Viewport0") ?? new Vector2(20, 421);
        var v1 = p.Node("Viewport1") ?? new Vector2(340, 135);
        _canvas = new MapCanvas { Assets = a, Map = AreaMap.Of(Ctx.Session), Session = Ctx.Session, FitToView = true, Position = new Vector2(v0.X, v1.Y), Size = new Vector2(v1.X - v0.X, v0.Y - v1.Y) };
        _canvas.PinHovered = text => { if (_pin != null) _pin.Text = text; };
        p.AddChild(_canvas);
        return p;
    }

    public override void Refresh()
    {
        _canvas.Map.Refresh();
        if (_name != null) _name.Text = _canvas.Map.Name;
    }
}

/// <summary>Grey hairline frame over a dark glass, with an optional divider line under the button row.</summary>
public partial class FrameBox : Control
{
    public float Divider = -1;
    static readonly Color Line = new(0.62f, 0.62f, 0.6f, 0.9f);

    public FrameBox() { MouseFilter = MouseFilterEnum.Stop; }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.35f));
        DrawRect(new Rect2(0.5f, 0.5f, Size.X - 1, Size.Y - 1), Line, false, 1);
        if (Divider > 0) DrawLine(new Vector2(1, Divider + 0.5f), new Vector2(Size.X - 1, Divider + 0.5f), Line, 1);
    }
}

/// <summary>
/// The explored tiles of the area (tiles not yet seen stay dark), map pins and the player's arrow (gui_map_arrow
/// turned to the facing). Centred on the area at 16 px a tile times <see cref="Zoom"/>, or fitted to the view.
/// </summary>
public partial class MapCanvas : Control, IClassicHover
{
    public ClassicAssets Assets = null!;
    public AreaMap Map = null!;
    public ClientSession Session = null!;
    public bool FitToView;
    public Action<string>? PinHovered;
    public Action<Vector2>? PointClicked;
    float _zoom = 1;
    public float Zoom { get => _zoom; set { _zoom = value; QueueRedraw(); } }
    const float TilePixels = 16;

    public MapCanvas() { ClipContents = true; MouseFilter = MouseFilterEnum.Pass; }

    public override void _Ready() => Map.Changed += QueueRedraw;
    public override void _ExitTree() => Map.Changed -= QueueRedraw;

    public override void _Process(double delta)
    {
        if (IsVisibleInTree()) QueueRedraw();
    }

    float Scale => FitToView && Map.Width > 0 ? MathF.Floor(Math.Min(Size.X / Map.Width, Size.Y / Map.Height)) : TilePixels * _zoom;

    Vector2 Centre
    {
        get
        {
            var center = Map.Extent / 2;
            if (FitToView || Session.Controlled is not { } me) return center;
            var half = Size * (AreaMap.TileSize / Math.Max(.001f, Scale)) / 2;
            // Native capped maps follow the player, bounded by the physical area.
            // Unclipped axes retain the whole-area center; small maps never crop.
            if (half.X < center.X) center.X = Math.Clamp(me.Position.X, half.X, Map.Extent.X - half.X);
            if (half.Y < center.Y) center.Y = Math.Clamp(me.Position.Y, half.Y, Map.Extent.Y - half.Y);
            return center;
        }
    }

    public Vector2 WorldToScreen(Vector2 world)
    {
        var c = Centre;
        float s = Scale / AreaMap.TileSize;
        return (Size / 2 + new Vector2(world.X - c.X, -(world.Y - c.Y)) * s).Round();
    }

    public Vector2 ScreenToWorld(Vector2 screen)
    {
        var delta = (screen - Size / 2) * (AreaMap.TileSize / Math.Max(0.001f, Scale));
        return Centre + new Vector2(delta.X, -delta.Y);
    }

    public MapPin? PinAt(Vector2 screen)
    {
        foreach (var pin in Map.Pins)
            if (Map.IsExplored(pin.Position) && WorldToScreen(pin.Position).DistanceTo(screen) < 8) return pin;
        return null;
    }

    /// <summary>The pointer as the last motion over the map put it (pushed capture events included).</summary>
    Vector2? _pointer;

    public string[]? HoverLines() => _pointer is { } at && PinAt(at) is { } pin ? [pin.Note] : null;
    /// <summary>The original shows a pin's note as soon as the pointer is on it.</summary>
    public bool HoverInstant => true;

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit) _pointer = null;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion) _pointer = motion.Position;
        if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse) return;
        PointClicked?.Invoke(ScreenToWorld(mouse.Position));
        AcceptEvent();
    }

    public override void _Draw()
    {
        if (Map.Tiles is not { } tiles || Map.Width <= 0) return;
        float s = Scale;
        int ppt = Map.PixelsPerTile;
        for (int ty = 0; ty < Map.Height; ty++)
            for (int tx = 0; tx < Map.Width; tx++)
            {
                if (!Map.IsExplored(tx, ty)) continue;
                var at = WorldToScreen(new Vector2(tx, ty + 1) * AreaMap.TileSize);
                var r = new Rect2(at, new Vector2(s, s));
                if (!r.Intersects(new Rect2(Vector2.Zero, Size))) continue;
                DrawTextureRectRegion(tiles, r, new Rect2(tx * ppt, (Map.Height - 1 - ty) * ppt, ppt, ppt));
            }
        // Pins.
        var pinModel = Assets.Model("ctl_map_pin");
        string hovered = "";
        var mouse = GetLocalMousePosition();
        foreach (var pin in Map.Pins)
        {
            if (!Map.IsExplored(pin.Position)) continue;
            var at = WorldToScreen(pin.Position);
            if (pinModel != null) GuiDraw.Model(this, Assets, pinModel.Rest, at, Colors.White);
            else DrawCircle(at, 3, GuiDraw.Selected);
            if (at.DistanceTo(mouse) < 8) hovered = pin.Note;
        }
        PinHovered?.Invoke(hovered);
        // The player.
        if (Session.Controlled is { } me && Assets.Texture("gui_map_arrow") is { } arrow)
        {
            var at = WorldToScreen(new Vector2(me.Position.X, me.Position.Y));
            float facing = me.Rotation.Z + MathF.PI / 2; // as the modern map: ObjectView rotation is facing - 90°
            // The arrow points up (north) in its texture; turn it to the facing (screen y is down).
            float angle = MathF.PI / 2 - facing;
            DrawSetTransform(at, angle, Vector2.One);
            DrawTextureRect(arrow, new Rect2(-7, -7, 14, 14), false);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }
}
