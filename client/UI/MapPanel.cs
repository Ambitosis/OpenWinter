using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.UI.Map;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The area map (M): the explored part of the area under a fog of war (<see cref="AreaMap"/>),
/// with map notes, the party and the player. Wheel zooms at the pointer, dragging pans; the side
/// bar has the legend and the area's notes (click one to find it on the map).
/// </summary>
public partial class MapPanel : UiTheme.Window
{
    public ClientSession Session = null!;

    AreaMap _map = null!;
    MapView _view = null!;
    Label _explored = null!, _coords = null!, _notesTitle = null!;
    VBoxContainer _notes = null!;
    bool _dragging, _dragMoved, _notesHover, _recentre;
    float _targetZoom;
    Vector2? _zoomAnchor;
    Tween? _glide;
    const float MaxZoom = 22f;

    public static MapPanel Create(ClientSession session)
    {
        var p = Create<MapPanel>("Map", new Vector2(980, 640));
        p.Session = session;
        p.AnchorLeft = p.AnchorRight = 0.5f;
        p.AnchorTop = p.AnchorBottom = 0.5f;
        return p;
    }

    public override void _Ready()
    {
        _map = AreaMap.Of(Session);
        var row = Ui.HBox(Palette.Space4);
        row.SizeFlagsVertical = SizeFlags.ExpandFill;
        Body.AddChild(row);

        // The map, in a sunken frame with its controls floating over it.
        var frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        frame.AddThemeStyleboxOverride("panel", Styles.Box(new Color("0b0c10"), Palette.Radius, Palette.LineStrong, 1, 0, 0));
        row.AddChild(frame);
        var stack = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, ClipContents = true };
        frame.AddChild(stack);
        _view = new MapView { Session = Session, Map = _map, AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 1, OffsetTop = 1, OffsetRight = -1, OffsetBottom = -1 };
        _view.GuiInput += OnMapInput;
        _view.Resized += () => { if (Visible) ClampView(); };
        stack.AddChild(_view);
        stack.AddChild(Vignette());
        stack.AddChild(Compass());
        stack.AddChild(ZoomControls());
        var infoPanel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        infoPanel.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0.04f, 0.045f, 0.06f, 0.78f), Palette.RadiusSmall, Palette.Line, 1, 10, 5));
        _coords = Ui.Text("", Ui.TextStyle.Caption, Palette.TextSecondary);
        infoPanel.AddChild(_coords);
        infoPanel.AnchorTop = infoPanel.AnchorBottom = 1;
        infoPanel.OffsetLeft = 12; infoPanel.OffsetBottom = -12;
        infoPanel.GrowVertical = GrowDirection.Begin;
        stack.AddChild(infoPanel);

        // Side bar: the area, legend, notes and controls.
        var side = Ui.VBox(Palette.Space3);
        side.CustomMinimumSize = new Vector2(236, 0);
        row.AddChild(side);
        _explored = Ui.Text("", Ui.TextStyle.Small);
        side.AddChild(_explored);

        side.AddChild(Ui.Section("Legend"));
        var legend = Ui.VBox(Palette.Space1);
        legend.AddChild(LegendRow("You", (c, at) => MapGlyphs.PlayerArrow(c, at, Vector2.Up, 0.95f)));
        legend.AddChild(LegendRow("Party member", (c, at) => { c.DrawCircle(at, 6.5f, new Color(0, 0, 0, 0.55f)); c.DrawCircle(at, 4.5f, Palette.Friendly); }));
        legend.AddChild(LegendRow("Map note", (c, at) => MapGlyphs.Pin(c, at + new Vector2(0, 9), 0.85f)));
        legend.AddChild(LegendRow("Camera view", (c, at) => MapGlyphs.ViewCone(c, at + new Vector2(-10, 0), Vector2.Right, 24)));
        legend.AddChild(LegendRow("Unexplored", (c, at) =>
        {
            var r = new Rect2(at - new Vector2(10, 7), new Vector2(20, 14));
            c.DrawRect(r, new Color("14161c"));
            c.DrawRect(r, Palette.LineStrong, false, 1);
        }));
        side.AddChild(legend);

        var notesHeader = Ui.Section("Map notes");
        _notesTitle = (Label)notesHeader.GetChild(0);
        side.AddChild(notesHeader);
        _addPin = new Button { Text = "Add map pin", TooltipText = "Choose a position on the map for a personal note.", FocusMode = FocusModeEnum.None };
        _addPin.Pressed += () => SetAddingPin(!_addingPin);
        side.AddChild(_addPin);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _notes = Ui.VBox(2);
        _notes.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_notes);
        side.AddChild(scroll);

        var hints = Ui.VBox(6);
        hints.AddChild(Hint("Wheel", "Zoom"));
        hints.AddChild(Hint("Drag", "Pan"));
        hints.AddChild(Hint("Space", "Centre on you"));
        hints.AddChild(Hint("M", "Close"));
        side.AddChild(hints);

        _map.Changed += OnMapChanged;
        Session.AreaLoaded += OnMapAreaLoaded;
        VisibilityChanged += () => { if (!Visible) { SetAddingPin(false); CancelPinEdit(); } };
        TreeExiting += () => { _map.Changed -= OnMapChanged; Session.AreaLoaded -= OnMapAreaLoaded; CancelPinEdit(); };
    }

    public void Toggle()
    {
        if (Visible) { Close(); return; }
        var view = GetViewportRect().Size;
        var size = new Vector2(Math.Min(1120, view.X - 96), Math.Min(760, view.Y - 72));
        CustomMinimumSize = size;
        Size = size;
        OffsetLeft = -size.X / 2; OffsetRight = size.X / 2; OffsetTop = -size.Y / 2; OffsetBottom = size.Y / 2;
        Visible = true;
        _map.Refresh();
        OnMapChanged();
        _recentre = true;
    }

    void OnMapChanged()
    {
        if (_view == null) return;
        TitleLabel.Text = _map.Name.Length > 0 ? _map.Name : "Map";
        int total = Math.Max(1, _map.Width * _map.Height);
        _explored.Text = _map.Width > 0 ? $"{_map.ExploredCount * 100 / total}% explored  ·  {_map.Width} × {_map.Height} tiles" : "No map for this area.";
        RebuildNotes();
    }

    /// <summary>Shows the whole area, or the player's surroundings when the area is too big to read whole.</summary>
    void Recentre()
    {
        if (_map.Width <= 0 || _view.Size.X <= 0) return;
        float fit = _view.FitZoom(28);
        _view.Zoom = _targetZoom = Math.Max(fit, 3.2f);
        _view.Center = _view.Zoom > fit * 1.05f ? _view.PlayerPosition : _map.Extent / 2;
        _view.HighlightPin = -1;
        ClampView();
    }

    float MinZoom => Math.Min(_view.FitZoom(28), 3.2f) * 0.85f;

    void ClampView()
    {
        if (_map.Width <= 0) return;
        _view.Zoom = Math.Clamp(_view.Zoom, MinZoom, MaxZoom);
        // The centre may wander until a quarter of the view is past the area's edge.
        var half = _view.Size / 2 / _view.Zoom;
        var ext = _map.Extent;
        float x = ext.X < half.X * 2 ? ext.X / 2 : Math.Clamp(_view.Center.X, half.X * 0.5f, ext.X - half.X * 0.5f);
        float y = ext.Y < half.Y * 2 ? ext.Y / 2 : Math.Clamp(_view.Center.Y, half.Y * 0.5f, ext.Y - half.Y * 0.5f);
        _view.Center = new Vector2(x, y);
    }

    /// <summary>Glides the view to a point (notes, "centre on me").</summary>
    void GlideTo(Vector2 center, float? zoom = null)
    {
        _glide?.Kill();
        _zoomAnchor = null;
        _glide = CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        if (zoom is { } z)
        {
            _targetZoom = Math.Clamp(z, MinZoom, MaxZoom);
            _glide.TweenMethod(Callable.From<float>(v => _view.Zoom = v), _view.Zoom, _targetZoom, 0.3);
        }
        _glide.TweenMethod(Callable.From<Vector2>(c => { _view.Center = c; ClampView(); }), _view.Center, center, 0.3);
    }

    void ZoomBy(float factor, Vector2? at = null)
    {
        _glide?.Kill();
        _targetZoom = Math.Clamp((_targetZoom > 0 ? _targetZoom : _view.Zoom) * factor, MinZoom, MaxZoom);
        _zoomAnchor = at ?? _view.Size / 2;
    }

    public override void _Process(double delta)
    {
        if (!Visible || _view == null) return;
        // Frame the area once the window has its size.
        if (_recentre && _view.Size.X > 64 && _map.Width > 0) { _recentre = false; Recentre(); }
        // Smooth zoom towards the target, keeping the point under the pointer still.
        if (_zoomAnchor is { } anchor && Math.Abs(_view.Zoom - _targetZoom) > 0.001f)
        {
            var before = _view.ScreenToWorld(anchor);
            _view.Zoom = Mathf.Lerp(_view.Zoom, _targetZoom, Math.Clamp((float)delta * 14f, 0, 1));
            if (Math.Abs(_view.Zoom - _targetZoom) < 0.002f) { _view.Zoom = _targetZoom; _zoomAnchor = null; }
            var after = _view.ScreenToWorld(anchor);
            _view.Center += before - after;
            ClampView();
        }
        var mouse = _view.GetLocalMousePosition();
        bool inside = new Rect2(Vector2.Zero, _view.Size).HasPoint(mouse);
        if (inside && _map.Width > 0)
        {
            var w = _view.ScreenToWorld(mouse);
            _coords.Text = w.X >= 0 && w.Y >= 0 && w.X < _map.Extent.X && w.Y < _map.Extent.Y
                ? $"Tile {(int)(w.X / AreaMap.TileSize)}, {(int)(w.Y / AreaMap.TileSize)}   ·   {w.X:0}, {w.Y:0} m"
                : "Outside the area";
        }
        else _coords.Text = Session.Controlled is { } me ? $"You are at {me.Position.X:0}, {me.Position.Y:0} m" : "";
        if (!_notesHover) _view.HighlightPin = inside ? _view.PinAt(mouse) : -1;
    }

    void OnMapInput(InputEvent e)
    {
        if (HandlePinInput(e)) { AcceptEvent(); return; }
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true } mb:
                ZoomBy(1.2f, mb.Position); AcceptEvent(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true } mb:
                ZoomBy(1 / 1.2f, mb.Position); AcceptEvent(); break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left or MouseButton.Middle or MouseButton.Right } mb:
                if (mb.DoubleClick) GlideTo(_view.ScreenToWorld(mb.Position), _view.Zoom * 1.6f);
                else if (mb.Pressed) { _dragging = true; _dragMoved = false; }
                else
                {
                    if (_dragging && !_dragMoved && mb.ButtonIndex == MouseButton.Left && _view.PinAt(mb.Position) is var pin and >= 0)
                    {
                        if (_map.Pins[pin].Personal) EditPin(_map.Pins[pin]);
                        else GlideTo(_map.Pins[pin].Position);
                    }
                    _dragging = false;
                }
                AcceptEvent();
                break;
            case InputEventMouseMotion mm when _dragging:
                _glide?.Kill();
                _zoomAnchor = null;
                _targetZoom = _view.Zoom;
                _dragMoved |= mm.Relative.LengthSquared() > 1;
                _view.Center -= new Vector2(mm.Relative.X, -mm.Relative.Y).Rotated(-_view.Angle) / _view.Zoom;
                ClampView();
                AcceptEvent();
                break;
        }
        _view.MouseDefaultCursorShape = _dragging ? CursorShape.Drag : _view.PinAt(_view.GetLocalMousePosition()) >= 0 ? CursorShape.PointingHand : CursorShape.Arrow;
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (!Visible || GetViewport().GuiGetFocusOwner() is TextEdit or LineEdit || e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.PhysicalKeycode == Key.Space) { CentreOnPlayer(); GetViewport().SetInputAsHandled(); }
        else if (k.PhysicalKeycode is Key.Equal or Key.KpAdd) { ZoomBy(1.25f); GetViewport().SetInputAsHandled(); }
        else if (k.PhysicalKeycode is Key.Minus or Key.KpSubtract) { ZoomBy(0.8f); GetViewport().SetInputAsHandled(); }
    }

    void CentreOnPlayer() => GlideTo(_view.PlayerPosition, Math.Max(_view.Zoom, 6f));

    void RebuildNotes()
    {
        foreach (var c in _notes.GetChildren()) c.QueueFree();
        int shown = 0;
        for (int i = 0; i < _map.Pins.Count; i++)
        {
            var pin = _map.Pins[i];
            if (!_map.IsExplored(pin.Position)) continue;
            shown++;
            int index = i;
            var b = new Button
            {
                Text = pin.Note.Length > 0 ? pin.Note : "Unnamed note", ThemeTypeVariation = "MenuRow", Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.None, ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, TooltipText = pin.Note, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            b.AddThemeFontOverride("font", Fonts.UiMedium);
            b.AddThemeFontSizeOverride("font_size", Palette.FontSmall + 1);
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
            {
                var s = (StyleBoxFlat)b.GetThemeStylebox(state).Duplicate();
                s.ContentMarginLeft = 30; s.ContentMarginRight = 8; s.ContentMarginTop = s.ContentMarginBottom = 6;
                b.AddThemeStyleboxOverride(state, s);
            }
            b.Draw += () => MapGlyphs.Pin(b, new Vector2(15, b.Size.Y / 2 + 7), 0.7f, b.IsHovered());
            b.MouseEntered += () => { _notesHover = true; _view.HighlightPin = index; b.QueueRedraw(); };
            b.MouseExited += () => { _notesHover = false; _view.HighlightPin = -1; b.QueueRedraw(); };
            b.Pressed += () => GlideTo(pin.Position, Math.Max(_view.Zoom, 7f));
            if (pin.Personal)
            {
                var noteRow = Ui.HBox(Palette.Space1);
                noteRow.AddChild(b);
                var edit = new Button { Text = "Edit", TooltipText = "Edit or delete this personal map pin.", ThemeTypeVariation = "GhostButton", FocusMode = FocusModeEnum.None };
                edit.Pressed += () => EditPin(pin);
                noteRow.AddChild(edit);
                _notes.AddChild(noteRow);
            }
            else _notes.AddChild(b);
        }
        _notesTitle.Text = shown > 0 ? $"MAP NOTES  ·  {shown}" : "MAP NOTES";
        if (shown == 0)
        {
            var empty = Ui.Text(_map.Pins.Count > 0 ? "Explore further to find this area's notes." : "No notes in this area.", Ui.TextStyle.Small, Palette.TextMuted);
            empty.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _notes.AddChild(empty);
        }
    }

    Control ZoomControls()
    {
        var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        pill.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0.04f, 0.045f, 0.06f, 0.82f), Palette.Radius, Palette.LineStrong, 1, 3, 3));
        var col = Ui.VBox(2);
        pill.AddChild(col);
        col.AddChild(MapGlyphs.Button(MapGlyphs.Kind.Plus, "Zoom in (+)", () => ZoomBy(1.4f)));
        col.AddChild(MapGlyphs.Button(MapGlyphs.Kind.Minus, "Zoom out (−)", () => ZoomBy(1 / 1.4f)));
        col.AddChild(new Divider { Fade = Divider.FadeMode.None, Color = Palette.Line, CustomMinimumSize = new Vector2(0, 1) });
        col.AddChild(MapGlyphs.Button(MapGlyphs.Kind.Locate, "Centre on you (Space)", CentreOnPlayer));
        col.AddChild(MapGlyphs.Button(MapGlyphs.Kind.Fit, "Show the whole area", () => GlideTo(_map.Extent / 2, _view.FitZoom(28))));
        pill.AnchorLeft = pill.AnchorRight = 1;
        pill.AnchorTop = pill.AnchorBottom = 1;
        pill.OffsetRight = -12; pill.OffsetBottom = -12;
        pill.GrowHorizontal = GrowDirection.Begin;
        pill.GrowVertical = GrowDirection.Begin;
        return pill;
    }

    /// <summary>A small compass rose in the map's top-left corner.</summary>
    static Control Compass()
    {
        var c = new Control { MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(12, 12), Size = new Vector2(56, 60) };
        c.Draw += () => MapGlyphs.Compass(c, new Vector2(28, 32), 22, 0);
        return c;
    }

    /// <summary>Darkens the map's edges so the frame reads as a window onto it.</summary>
    static Control Vignette()
    {
        var v = new Control { MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 };
        v.Draw += () =>
        {
            var s = v.Size;
            const float w = 40;
            var dark = new Color(0, 0, 0, 0.45f);
            var clear = new Color(0, 0, 0, 0);
            v.DrawPolygon([new(0, 0), new(s.X, 0), new(s.X, w), new(0, w)], [dark, dark, clear, clear]);
            v.DrawPolygon([new(0, s.Y - w), new(s.X, s.Y - w), new(s.X, s.Y), new(0, s.Y)], [clear, clear, dark, dark]);
            v.DrawPolygon([new(0, 0), new(w, 0), new(w, s.Y), new(0, s.Y)], [dark, clear, clear, dark]);
            v.DrawPolygon([new(s.X - w, 0), new(s.X, 0), new(s.X, s.Y), new(s.X - w, s.Y)], [clear, dark, dark, clear]);
        };
        v.Resized += v.QueueRedraw;
        return v;
    }

    static Control LegendRow(string text, Action<CanvasItem, Vector2> glyph)
    {
        var row = Ui.HBox(Palette.Space3);
        var swatch = new Control { CustomMinimumSize = new Vector2(28, 26), MouseFilter = MouseFilterEnum.Ignore };
        swatch.Draw += () => glyph(swatch, swatch.Size / 2);
        row.AddChild(swatch);
        var l = Ui.Text(text, Ui.TextStyle.Small);
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(l);
        return row;
    }

    static Control Hint(string key, string action)
    {
        var row = Ui.HBox(Palette.Space2);
        var cap = KeyCap.Create(key);
        cap.CustomMinimumSize = new Vector2(52, 0);
        row.AddChild(cap);
        var l = Ui.Text(action, Ui.TextStyle.Caption, Palette.TextSecondary);
        l.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(l);
        return row;
    }
}
