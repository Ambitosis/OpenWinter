using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.UI.Map;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The HUD minimap: a round window onto the explored map around the player (the same map and fog
/// as the map window, <see cref="AreaMap"/>), turning with the camera so "up" is where you look,
/// in a brass ring with a north marker, zoom buttons and the area's name. Click to open the map.
/// Self-sized (about 200×200); the HUD places it.
/// </summary>
public partial class Minimap : Control
{
    public ClientSession Session = null!;
    /// <summary>Opens the full map window (clicking the minimap).</summary>
    public Action? OpenMap;
    /// <summary>Turn the map with the camera (otherwise north stays up and a cone shows the view).</summary>
    public bool RotateWithCamera = true;

    static readonly float[] ZoomLevels = [1.4f, 2.0f, 2.8f, 4.0f, 5.6f];
    int _zoomLevel = 2;
    float _angle;
    MapView _view = null!;
    Control _mask = null!, _ring = null!;
    Label _name = null!;
    PanelContainer _plate = null!;
    const float Diameter = 176;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(200, 204);
        MouseFilter = MouseFilterEnum.Ignore;
        var map = AreaMap.Of(Session);

        // The round window: a mask that draws a circle and clips the map to it.
        _mask = new Control { ClipChildren = ClipChildrenMode.Only, MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Diameter, Diameter) };
        _mask.Draw += () => _mask.DrawCircle(_mask.Size / 2, Diameter / 2 - 1, Colors.White, true, -1, true);
        AddChild(_mask);
        _view = new MapView
        {
            Session = Session, Map = map, Compact = true, AnchorRight = 1, AnchorBottom = 1, Zoom = ZoomLevels[_zoomLevel],
            TooltipText = "Open the map (M)", MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _view.GuiInput += OnInput;
        _mask.AddChild(_view);

        // Bezel, north marker and inner shadow over the map.
        _ring = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = new Vector2(Diameter + 16, Diameter + 16) };
        _ring.Draw += DrawRing;
        AddChild(_ring);

        var zoomIn = MapGlyphs.Button(MapGlyphs.Kind.Plus, "Zoom in", () => SetZoom(_zoomLevel + 1), 24, round: true);
        var zoomOut = MapGlyphs.Button(MapGlyphs.Kind.Minus, "Zoom out", () => SetZoom(_zoomLevel - 1), 24, round: true);
        AddChild(zoomIn);
        AddChild(zoomOut);

        _plate = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        _plate.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0.05f, 0.055f, 0.07f, 0.94f), Palette.RadiusSmall, Palette.AccentDim, 1, 10, 3, shadow: 6));
        _name = Ui.Text("", Ui.TextStyle.Caption, Palette.AccentBright);
        _name.AddThemeFontOverride("font", Fonts.Display);
        _name.AddThemeFontSizeOverride("font_size", 11);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.ClipText = true;
        // Long area names end in an ellipsis instead of losing both ends to the centred clip.
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _plate.AddChild(_name);
        AddChild(_plate);

        Resized += Layout;
        map.Changed += OnMapChanged;
        TreeExiting += () => map.Changed -= OnMapChanged;
        OnMapChanged();
        Layout();

        void Layout()
        {
            float w = Math.Max(Size.X, CustomMinimumSize.X);
            var centre = new Vector2(w / 2, Diameter / 2 + 8);
            _mask.Position = centre - _mask.Size / 2;
            _ring.Position = centre - _ring.Size / 2;
            // Zoom buttons sit on the ring at about four and eight o'clock.
            float r = Diameter / 2 + 2;
            zoomIn.Position = centre + new Vector2(MathF.Cos(0.62f), MathF.Sin(0.62f)) * r - new Vector2(12, 12);
            zoomOut.Position = centre + new Vector2(-MathF.Cos(0.62f), MathF.Sin(0.62f)) * r - new Vector2(12, 12);
            PlacePlate();
        }
    }

    void OnMapChanged()
    {
        var name = AreaMap.Of(Session).Name;
        _name.Text = name;
        _plate.Visible = name.Length > 0;
        _plate.TooltipText = name;
        CallDeferred(MethodName.PlacePlate);
    }

    void PlacePlate()
    {
        float w = Math.Max(Size.X, CustomMinimumSize.X);
        var size = _plate.GetCombinedMinimumSize();
        float text = Fonts.Display.GetStringSize(_name.Text, HorizontalAlignment.Left, -1, 11).X + 24;
        float width = Math.Min(Math.Max(text, 96), w - 4);
        _plate.Size = new Vector2(width, size.Y);
        _plate.Position = new Vector2((w - width) / 2, Diameter + 8 - size.Y / 2 - 4);
    }

    void SetZoom(int level)
    {
        _zoomLevel = Math.Clamp(level, 0, ZoomLevels.Length - 1);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _view == null) return;
        float t = Math.Clamp((float)delta * 10f, 0, 1);
        _view.Zoom = Mathf.Lerp(_view.Zoom, ZoomLevels[_zoomLevel], t);
        _view.Center = _view.PlayerPosition;
        float wanted = 0;
        if (RotateWithCamera && _view.CameraDirection() is { } look)
            wanted = -MathF.PI / 2 - MathF.Atan2(-look.Y, look.X);
        _angle = Mathf.LerpAngle(_angle, wanted, Math.Clamp((float)delta * 12f, 0, 1));
        _view.Angle = _angle;
        _ring.QueueRedraw();
    }

    void OnInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.Left) { OpenMap?.Invoke(); AcceptEvent(); }
            else if (mb.ButtonIndex == MouseButton.WheelUp) { SetZoom(_zoomLevel + 1); AcceptEvent(); }
            else if (mb.ButtonIndex == MouseButton.WheelDown) { SetZoom(_zoomLevel - 1); AcceptEvent(); }
        }
    }

    void DrawRing()
    {
        var c = _ring.Size / 2;
        float r = Diameter / 2;
        // Inner shadow: the map sinks into the bezel.
        for (int i = 0; i < 10; i++)
            _ring.DrawArc(c, r - 1 - i * 1.2f, 0, MathF.Tau, 96, new Color(0, 0, 0, 0.32f * (1 - i / 10f)), 1.6f, true);
        // Bezel: dark band between two brass lines, with a highlight on top.
        _ring.DrawArc(c, r + 3.5f, 0, MathF.Tau, 128, new Color("0c0d12"), 7, true);
        _ring.DrawArc(c, r, 0, MathF.Tau, 128, Palette.AccentDim, 1.4f, true);
        _ring.DrawArc(c, r + 7, 0, MathF.Tau, 128, Palette.Accent, 1.6f, true);
        _ring.DrawArc(c, r + 7, -MathF.PI * 0.85f, -MathF.PI * 0.15f, 64, Palette.AccentBright with { A = 0.55f }, 1.2f, true);
        // Ticks every 30°, turning with the map; north as a brass marker with an N.
        for (int i = 0; i < 12; i++)
        {
            if (i % 3 == 0) continue;
            var d = Vector2.Up.Rotated(_angle + i * MathF.Tau / 12);
            _ring.DrawLine(c + d * (r + 1.5f), c + d * (r + 5), Palette.AccentDim, 1, true);
        }
        foreach (int i in new[] { 1, 2, 3 })
        {
            var d = Vector2.Up.Rotated(_angle + i * MathF.PI / 2);
            var side = new Vector2(-d.Y, d.X);
            _ring.DrawColoredPolygon([c + d * (r + 6), c + d * (r + 1) + side * 3, c + d * (r + 1) - side * 3], Palette.AccentDim.Lightened(0.15f));
        }
        var n = Vector2.Up.Rotated(_angle);
        var at = c + n * (r + 3.5f);
        _ring.DrawCircle(at, 9.5f, new Color("0c0d12"));
        _ring.DrawArc(at, 9.5f, 0, MathF.Tau, 32, Palette.Accent, 1.4f, true);
        var font = Fonts.DisplayBold;
        var ts = font.GetStringSize("N", HorizontalAlignment.Left, -1, 11);
        _ring.DrawString(font, at + new Vector2(-ts.X / 2 + 0.5f, font.GetAscent(11) / 2 - 1.5f), "N", HorizontalAlignment.Left, -1, 11, Palette.AccentBright);
    }
}
