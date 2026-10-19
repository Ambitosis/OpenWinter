using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.CharGen;

/// <summary>
/// A live 3D preview of a creature in its own small world (SubViewport): the same CreatureView the
/// game uses, rebuilt whenever the appearance changes and framed to the model's size, so a halfling,
/// a bat and a bear all fill the stage. Drag to turn it; the wheel (or <see cref="Zoom"/>) moves the
/// camera from the whole body to the face. The background is transparent: hosts draw the backdrop
/// (see <see cref="PreviewStage"/>).
/// </summary>
public partial class CreaturePreview : SubViewportContainer
{
    public ClientContent? Content;
    SubViewport _viewport = null!;
    Node3D _space = null!, _pivot = null!;
    MeshInstance3D _shadow = null!;
    CreatureView? _view;
    Camera3D _camera = null!;
    Appearance? _pending;
    bool _drag;
    float _yaw, _yawShown, _zoomShown;
    Aabb _bounds = new(new Vector3(-0.4f, 0, -0.3f), new Vector3(0.8f, 1.8f, 0.6f));
    /// <summary>Camera target between the whole body (0) and the face (1).</summary>
    public float Zoom;
    /// <summary>Whether the player can turn and zoom the preview.</summary>
    public bool Interactive = true;
    /// <summary>The appearance on show (null before the first <see cref="Show"/>).</summary>
    public Appearance? Shown { get; private set; }

    public override void _Ready()
    {
        Stretch = true;
        MouseFilter = MouseFilterEnum.Stop;
        _viewport = new SubViewport { OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible };
        AddChild(_viewport);
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = new Color(0.58f, 0.58f, 0.62f), AmbientLightEnergy = 0.7f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        _viewport.AddChild(new WorldEnvironment { Environment = env });
        // Warm key from the front left, cool fill from the right, a rim from behind for the silhouette.
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 1.05f, LightColor = new Color(1f, 0.96f, 0.9f), RotationDegrees = new Vector3(-32, 28, 0) });
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 0.4f, LightColor = new Color(0.75f, 0.82f, 1f), RotationDegrees = new Vector3(-12, -60, 0) });
        _viewport.AddChild(new DirectionalLight3D { LightEnergy = 0.6f, LightColor = new Color(1f, 0.88f, 0.7f), RotationDegrees = new Vector3(-20, 180, 0) });
        // Aurora is Z-up; the same rotation the game's space uses.
        _space = new Node3D { RotationDegrees = new Vector3(-90, 0, 0) };
        _viewport.AddChild(_space);
        _pivot = new Node3D();
        _space.AddChild(_pivot);
        _camera = new Camera3D { Fov = 30, Current = true, Near = 0.02f };
        _viewport.AddChild(_camera);
        _shadow = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(1.6f, 1.6f) },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = ShadowTexture(), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0, 0, 0, 0.75f),
            },
            Position = new Vector3(0, 0.002f, 0),
        };
        _viewport.AddChild(_shadow);
        // A Show() before this node entered the tree (the window builds the stage, then shows).
        if (_pending is { } a) { _pending = null; Show(a); }
        PlaceCamera(true);
    }

    static Texture2D ShadowTexture()
    {
        var g = new Gradient();
        g.SetColor(0, new Color(1, 1, 1, 1));
        g.SetColor(1, new Color(1, 1, 1, 0));
        g.AddPoint(0.55f, new Color(1, 1, 1, 0.35f));
        return new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 64, Height = 64 };
    }

    /// <summary>Shows a creature with this appearance (rebuilt in place when one is shown already).</summary>
    public void Show(Appearance appearance)
    {
        Shown = appearance;
        if (Content == null) return;
        if (_viewport == null || !IsInsideTree()) { _pending = appearance; return; }
        if (_view != null && _view.CurrentAppearance?.Type == appearance.Type)
            _view.Rebuild(appearance, Content);
        else
        {
            // A different creature (another familiar, say): a fresh view and a fresh turn.
            if (_view != null) { _pivot.RemoveChild(_view); _view.QueueFree(); }
            var spawn = new SpawnMessage { ObjectId = 0x7FFFFFF0, Kind = ObjectKind.Creature, Tag = "preview", Name = "", Appearance = appearance, Facing = -MathF.PI / 2 };
            _view = CreatureView.Create(spawn, Content);
            _pivot.AddChild(_view);
            // Characters face the camera; animals turn three-quarters, which shows their shape.
            bool partBased = Content.Resources.TwoDA("appearance").Get(appearance.Type, "MODELTYPE").StartsWith("P", StringComparison.OrdinalIgnoreCase);
            _yaw = _yawShown = partBased ? 0 : 0.6f;
            _pivot.Rotation = new Vector3(0, 0, _yaw);
        }
        Measure();
        PlaceCamera(true);
        // Measure again once the idle animation has posed the skeleton.
        _measureFrames = 3;
    }

    /// <summary>
    /// The creature's extent in the stage's (Y-up) space, for framing. Taken from where its bones
    /// are once the idle animation poses it (the bind pose can be taller: supermodel animations
    /// rescale the skeleton), falling back to the mesh bounds for models without a skeleton.
    /// Effect dummies (headconjure floats above the head, handconjure in front) don't count.
    /// </summary>
    void Measure()
    {
        if (_view == null) return;
        Aabb? box = null;
        _head = null;
        if (_view.Skeleton is { } sk && sk.GetBoneCount() > 3)
        {
            Vector3? head = null, headBase = null;
            for (int i = 1; i < sk.GetBoneCount(); i++)
            {
                string name = sk.GetBoneName(i).ToLowerInvariant();
                if (name.Contains("conjure") || name.Contains("impact") || name.Contains("dummy") && name != "rootdummy") continue;
                var p = sk.GlobalTransform * sk.GetBoneGlobalPose(i).Origin;
                box = box is { } x ? x.Expand(p) : new Aabb(p, Vector3.Zero);
                if (name == "head") head = p;
                else if (name == "head_g") headBase = p;
            }
            if (head is { } h && headBase is { } hb && h.Y > hb.Y) _head = (hb, h);
            // Bones sit inside the body: pad by a little all round, and the top of the head is the "head" bone.
            if (box is { } bones && bones.Size.Y > 0.1f)
            {
                float pad = Math.Max(0.05f, bones.Size.Y * 0.06f);
                bones = bones.Grow(pad);
                // Wings and tails reach past their last bone: take the width from the meshes.
                if (MeshBounds() is { } mesh)
                {
                    float x0 = Math.Min(bones.Position.X, mesh.Position.X), x1 = Math.Max(bones.End.X, mesh.End.X);
                    bones = new Aabb(new Vector3(x0, bones.Position.Y, bones.Position.Z), new Vector3(x1 - x0, bones.Size.Y, bones.Size.Z));
                }
                box = bones;
            }
            else box = null;
        }
        box ??= MeshBounds();
        if (box is not { } found) return;
        var size = found.Size;
        if (size.Y < 0.05f || size.Y > 12f) return; // stray geometry: keep the last framing
        // Standing creatures keep their feet on the floor.
        if (found.Position.Y < size.Y * 0.25f) found = new Aabb(new Vector3(found.Position.X, 0, found.Position.Z), new Vector3(size.X, found.End.Y, size.Z));
        _bounds = found;
        float radius = Math.Max(size.X, size.Z) * 0.5f;
        _shadow.Scale = Vector3.One * Math.Clamp(radius * 1.4f, 0.35f, 4f);
        _shadow.Position = new Vector3(found.GetCenter().X, 0.002f, found.GetCenter().Z);
    }

    Aabb? MeshBounds()
    {
        Aabb? box = null;
        foreach (var node in _view!.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mi || mi.Mesh == null || !mi.Visible) continue;
            var b = mi.GlobalTransform * mi.GetAabb();
            if (b.Size.LengthSquared() < 1e-6f) continue;
            box = box is { } x ? x.Merge(b) : b;
        }
        return box;
    }

    /// <summary>The base and top of a humanoid's head (head_g and head bones), for the face close-up.</summary>
    (Vector3 Base, Vector3 Top)? _head;
    int _measureFrames;

    /// <summary>Where the camera looks for a zoom: body (0) frames everything, face (1) the head (or the top of the model).</summary>
    (Vector3 Target, float Distance) Framing(float zoom)
    {
        var b = _bounds;
        float top = b.End.Y, bottom = b.Position.Y;
        float height = Math.Max(0.2f, top - bottom);
        float width = Math.Max(b.Size.X, b.Size.Z * 0.6f);
        float tan = MathF.Tan(Mathf.DegToRad(_camera.Fov * 0.5f));
        float aspect = Size.Y > 1 ? Math.Max(0.3f, Size.X / Size.Y) : 0.6f;
        // Whole body, with a little air above the head and below the feet.
        var bodyTarget = new Vector3(b.GetCenter().X, bottom + height * 0.5f, b.GetCenter().Z);
        float bodyDist = Math.Max(height * 1.1f * 0.5f / tan, width * 1.15f * 0.5f / (tan * aspect)) + b.Size.Z * 0.35f;
        // Face: the head and a little of the shoulders.
        Vector3 faceTarget;
        float faceH;
        if (_head is { } head)
        {
            float headSize = head.Top.Y - head.Base.Y;
            faceH = headSize * 2.6f;
            faceTarget = head.Base.Lerp(head.Top, 0.4f);
        }
        else
        {
            faceH = Math.Max(0.12f, height * 0.3f);
            faceTarget = new Vector3(b.GetCenter().X, top - faceH * 0.5f, b.GetCenter().Z);
        }
        float faceDist = Math.Max(faceH * 0.5f / tan, faceH * 0.8f * 0.5f / (tan * aspect));
        return (bodyTarget.Lerp(faceTarget, zoom), Mathf.Lerp(bodyDist, faceDist, zoom));
    }

    void PlaceCamera(bool snap)
    {
        if (_camera == null) return;
        if (snap) _zoomShown = Zoom;
        var (target, dist) = Framing(_zoomShown);
        // Slightly above, looking a little down, like a mirror at eye height.
        _camera.Position = target + new Vector3(0, dist * 0.08f, dist);
        _camera.LookAt(target);
    }

    public override void _Process(double delta)
    {
        if (_camera == null) return;
        if (_measureFrames > 0 && --_measureFrames <= 1)
        {
            Measure();
            PlaceCamera(false);
        }
        float k = 1f - MathF.Exp(-(float)delta * 10f);
        bool moving = Math.Abs(_zoomShown - Zoom) > 0.001f || Math.Abs(_yawShown - _yaw) > 0.0005f;
        if (!moving) return;
        _zoomShown = Mathf.Lerp(_zoomShown, Zoom, k);
        _yawShown = Mathf.Lerp(_yawShown, _yaw, k);
        _pivot.Rotation = new Vector3(0, 0, _yawShown);
        PlaceCamera(false);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) PlaceCamera(true);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (!Interactive) return;
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                _drag = mb.Pressed;
                if (mb.DoubleClick) { _yaw = 0; Zoom = 0; }
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                Zoom = Math.Clamp(Zoom + 0.15f, 0, 1);
                AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                Zoom = Math.Clamp(Zoom - 0.15f, 0, 1);
                AcceptEvent();
                break;
            case InputEventMouseMotion mm when _drag:
                _yaw += mm.Relative.X * 0.012f;
                AcceptEvent();
                break;
        }
    }
}

/// <summary>
/// A framed 3D preview: a soft radial backdrop, the <see cref="CreaturePreview"/>, an optional
/// caption across the bottom and optional Body / Face buttons. Used for the character in the
/// creation wizard's side column and for familiars and companions.
/// </summary>
public partial class PreviewStage : Control
{
    public CreaturePreview Preview = null!;
    VBoxContainer _caption = null!;
    HBoxContainer _zoomBar = null!;
    Label _title = null!, _subtitle = null!;
    Control _captionBand = null!;

    public static PreviewStage Create(ClientContent? content, bool zoomButtons = false)
    {
        var s = new PreviewStage();
        s.Build(content, zoomButtons);
        return s;
    }

    void Build(ClientContent? content, bool zoomButtons)
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        ClipContents = true;
        var g = new Gradient();
        g.SetColor(0, new Color(0.2f, 0.17f, 0.13f));
        g.SetColor(1, new Color(0.035f, 0.04f, 0.055f));
        g.AddPoint(0.45f, new Color(0.09f, 0.085f, 0.085f));
        AddChild(new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.42f), FillTo = new Vector2(1.15f, 1.0f), Width = 128, Height = 128 },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore,
        });
        Preview = new CreaturePreview { Content = content, AnchorRight = 1, AnchorBottom = 1, TooltipText = "Drag to turn · wheel to zoom · double-click to reset" };
        AddChild(Preview);

        // Caption: a dark fade across the bottom with a title and a line under it.
        var fade = new Gradient();
        fade.SetColor(0, new Color(0, 0, 0, 0));
        fade.SetColor(1, new Color(0, 0, 0, 0.78f));
        _captionBand = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = fade, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 4, Height = 64 },
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale,
            AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1, OffsetTop = -74, MouseFilter = MouseFilterEnum.Ignore, Visible = false,
        };
        AddChild(_captionBand);
        _caption = new VBoxContainer { AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1, OffsetTop = -58, OffsetLeft = 14, OffsetRight = -14, OffsetBottom = -10, MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        _caption.AddThemeConstantOverride("separation", 0);
        _title = Ui.Text("", Ui.TextStyle.Title);
        _title.ClipText = true;
        _title.MouseFilter = MouseFilterEnum.Ignore;
        _subtitle = Ui.Text("", Ui.TextStyle.Small, Palette.TextSecondary);
        _subtitle.ClipText = true;
        _subtitle.MouseFilter = MouseFilterEnum.Ignore;
        _caption.AddChild(_title);
        _caption.AddChild(_subtitle);
        _caption.Visible = false;
        AddChild(_caption);

        _zoomBar = new HBoxContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -128, OffsetRight = -10, OffsetTop = 10, OffsetBottom = 38, Visible = zoomButtons, Alignment = BoxContainer.AlignmentMode.End };
        _zoomBar.AddThemeConstantOverride("separation", 4);
        AddChild(_zoomBar);
        _zoomBar.AddChild(ZoomButton("Body", 0, "Show the whole body"));
        _zoomBar.AddChild(ZoomButton("Face", 1, "Close-up of the head"));
    }

    Button ZoomButton(string text, float zoom, string tip)
    {
        var b = new Button { Text = text, FocusMode = FocusModeEnum.None, TooltipText = tip, CustomMinimumSize = new Vector2(54, 26), MouseDefaultCursorShape = CursorShape.PointingHand };
        b.AddThemeStyleboxOverride("normal", Styles.Box(new Color(0, 0, 0, 0.45f), 13, Palette.LineStrong, 1, 8, 2));
        b.AddThemeStyleboxOverride("hover", Styles.Box(new Color(0, 0, 0, 0.6f), 13, Palette.Accent, 1, 8, 2));
        b.AddThemeStyleboxOverride("pressed", Styles.Box(Palette.AccentDim, 13, Palette.Accent, 1, 8, 2));
        b.AddThemeFontSizeOverride("font_size", Palette.FontCaption);
        b.Pressed += () => Preview.Zoom = zoom;
        return b;
    }

    public bool ZoomButtons { set => _zoomBar.Visible = value; }

    /// <summary>The caption over the bottom of the stage (empty hides it).</summary>
    public void SetCaption(string title, string subtitle)
    {
        _title.Text = title;
        _subtitle.Text = subtitle;
        _subtitle.Visible = subtitle.Length > 0;
        _caption.Visible = _captionBand.Visible = title.Length > 0 || subtitle.Length > 0;
    }
}
