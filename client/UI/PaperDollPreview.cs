using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The inventory's live 3D portrait of the controlled character: the game's own CreatureView in a
/// private world (transparent, over a lit backdrop), rebuilt whenever the character's appearance
/// changes (equipping armour, weapons, helmets). Drag to turn it.
/// </summary>
public partial class PaperDollPreview : Control
{
    public ClientSession Session = null!;
    SubViewportContainer _container = null!;
    SubViewport _viewport = null!;
    Node3D _pivot = null!;
    CreatureView? _view;
    Appearance? _shown;
    bool _drag;
    float _yaw = 0.35f;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Drag;

        // Backdrop: a warm pool of light behind the figure, fading to the card colour.
        var glow = new GradientTexture2D
        {
            Gradient = new Gradient { Colors = [new Color(0.34f, 0.27f, 0.18f, 0.55f), new Color(0.12f, 0.12f, 0.15f, 0.0f)], Offsets = [0f, 1f] },
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.42f), FillTo = new Vector2(0.5f, 1.05f), Width = 256, Height = 256,
        };
        var backdrop = new TextureRect { Texture = glow, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);
        // A floor ellipse the figure stands on.
        AddChild(new FloorShadow { MouseFilter = MouseFilterEnum.Ignore, AnchorLeft = 0.18f, AnchorRight = 0.82f, AnchorTop = 0.86f, AnchorBottom = 0.95f });

        _container = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        _container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_container);
        _viewport = new SubViewport
        {
            OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X,
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible,
        };
        _container.AddChild(_viewport);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.6f, 0.58f, 0.56f), AmbientLightEnergy = 0.8f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        _viewport.AddChild(new WorldEnvironment { Environment = env });
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 1.35f, LightColor = new Color(1f, 0.93f, 0.82f), RotationDegrees = new Vector3(-30, 35, 0) });
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 0.5f, LightColor = new Color(0.7f, 0.8f, 1f), RotationDegrees = new Vector3(-10, -140, 0) });
        // Rim light from behind so the silhouette separates from the dark backdrop.
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 0.9f, LightColor = new Color(1f, 0.85f, 0.6f), RotationDegrees = new Vector3(-20, 180, 0) });
        var space = new Node3D { RotationDegrees = new Vector3(-90, 0, 0) }; // Aurora is Z-up
        _viewport.AddChild(space);
        _pivot = new Node3D { Rotation = new Vector3(0, 0, _yaw) };
        space.AddChild(_pivot);
        var camera = new Camera3D { Fov = 30, Current = true };
        _viewport.AddChild(camera);
        camera.Position = new Vector3(0, 1.05f, 4.0f);
        camera.LookAt(new Vector3(0, 0.93f, 0));
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || Session.Content is not { } content) return;
        if (Session.Controlled is not CreatureView me || me.CurrentAppearance is not { } look) return;
        if (ReferenceEquals(look, _shown)) return;
        _shown = look;
        if (_view != null) { _view.Rebuild(look, content); return; }
        var spawn = new SpawnMessage { ObjectId = 0x7FFFFFF1, Kind = ObjectKind.Creature, Tag = "paperdoll", Name = "", Appearance = look, Facing = -MathF.PI / 2 };
        _view = CreatureView.Create(spawn, content);
        _pivot.AddChild(_view);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb) _drag = mb.Pressed;
        else if (e is InputEventMouseMotion mm && _drag)
        {
            _yaw += mm.Relative.X * 0.012f;
            _pivot.Rotation = new Vector3(0, 0, _yaw);
        }
    }

    partial class FloorShadow : Control
    {
        public override void _Draw()
        {
            var c = Size / 2;
            for (int i = 6; i >= 1; i--)
            {
                float t = i / 6f;
                DrawSetTransform(c, 0, new Vector2(1, 0.28f));
                DrawCircle(Vector2.Zero, Size.X / 2 * t, new Color(0, 0, 0, 0.07f));
            }
            DrawSetTransform(c, 0, new Vector2(1, 0.28f));
            DrawArc(Vector2.Zero, Size.X / 2, 0, Mathf.Tau, 64, Palette.AccentDim with { A = 0.35f }, 1.5f, true);
        }
    }
}
