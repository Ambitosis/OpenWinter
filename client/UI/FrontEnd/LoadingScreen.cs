using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// The full-screen loader shown while a module starts, content downloads and areas build: the area's
/// loading art (loadscreens.2da by the area's LoadScreenID, else one for its tileset) with a slow push-in,
/// the area and module names, a hint from loadhints.2da, a progress line (real for downloads, a moving
/// sheen while an area builds) and a spinner. Fades out once the area is ready.
/// </summary>
public partial class LoadingScreen : CanvasLayer
{
    Control _root = null!;
    TextureRect _art = null!;
    Control _frame = null!;
    TextureRect[] _shots = null!;
    List<Texture2D> _pictures = new();
    int _picture;
    double _pictureTimer;
    Atmosphere _atmosphere = null!;
    Label _kicker = null!, _title = null!, _status = null!, _hint = null!, _hintHead = null!;
    Control _hintBox = null!;
    ProgressLine _progress = null!;
    Tween? _push, _fadeTween;
    double _shownAt;
    bool _finishing;

    /// <summary>Offered when loading takes very long (a server that does not answer): leave to the main menu.</summary>
    public Action? Abort;
    Button _abort = null!;
    /// <summary>Seconds before the way back is offered.</summary>
    public double AbortAfter = 25;

    /// <summary>True while the loader covers the screen.</summary>
    public bool Showing => _root.Visible;

    // What the loader shows, for the classic interface's loading screen drawn over it.
    public string Kicker => _kicker.Text;
    public string Title => _title.Text;
    public string Status => _status.Text;
    public string Hint => _hintBox.Visible ? _hint.Text : "";
    /// <summary>Progress 0 - 1; negative while unknown.</summary>
    public float Progress => _finishing ? 1 : _progress.Value;
    /// <summary>The loading art's texture (whole, as the game stores it) and the content it comes from; empty for none.</summary>
    public string ArtResRef { get; private set; } = "";
    public ClientContent? ArtContent { get; private set; }

    public override void _Ready()
    {
        Layer = 64;
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false, Theme = GameTheme.Theme };
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        _root.AddChild(new ColorRect { Color = new Color("06070a"), AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore });
        // The art twice: blurred and dark filling the screen, and sharp in a frame.
        _art = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = "shader_type canvas_item; void fragment() { vec3 c = textureLod(TEXTURE, UV, 3.5).rgb; float l = dot(c, vec3(0.3, 0.59, 0.11)); COLOR = vec4(mix(vec3(l), c, 0.7) * 0.42, 1.0); }",
                },
            },
        };
        _root.AddChild(_art);
        _atmosphere = new Atmosphere { Smoke = 1, LeftShade = 0.45f, Vignette = 0.9f, Embers = 0.7f };
        _root.AddChild(_atmosphere);
        var frameBox = new AspectRatioContainer
        {
            Ratio = 1.92f, StretchMode = AspectRatioContainer.StretchModeEnum.Fit, AlignmentVertical = AspectRatioContainer.AlignmentMode.Begin,
            AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.1f, AnchorBottom = 0.6f, OffsetLeft = -420, OffsetRight = 420,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _root.AddChild(frameBox);
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        var frameStyle = Styles.Box(Colors.Black, 4, Palette.Accent with { A = 0.55f }, 1, 0, shadow: 40, shadowAlpha: 0.7f);
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        frameBox.AddChild(frame);
        _frame = frame;
        _shots = new TextureRect[2];
        for (int i = 0; i < 2; i++)
        {
            _shots[i] = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
            frame.AddChild(_shots[i]);
        }
        // Inner vignette over the picture.
        frame.AddChild(new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment() { vec2 d = UV - 0.5; COLOR = vec4(0.0, 0.0, 0.0, smoothstep(0.3, 0.75, length(d * vec2(1.0, 1.6))) * 0.6); }" } },
        });
        // The brass hairline over the picture's edge.
        var rim = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        var rimStyle = Styles.Box(Colors.Transparent, 4, Palette.Accent with { A = 0.6f }, 1, 0);
        rim.AddThemeStyleboxOverride("panel", rimStyle);
        frame.AddChild(rim);
        // A deep shade along the bottom for the text.
        _root.AddChild(new ColorRect
        {
            AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = new Shader { Code = "shader_type canvas_item; void fragment() { COLOR = vec4(0.02, 0.022, 0.03, smoothstep(0.5, 1.0, UV.y) * 0.85); }" } },
        });

        // Bottom-left: module kicker, area title, status and progress.
        var block = Ui.VBox(Palette.Space2);
        block.AnchorTop = block.AnchorBottom = 1;
        block.AnchorRight = 1;
        block.OffsetLeft = 88; block.OffsetRight = -88; block.OffsetTop = -230; block.OffsetBottom = -56;
        block.Alignment = BoxContainer.AlignmentMode.End;
        block.MouseFilter = Control.MouseFilterEnum.Ignore;
        _root.AddChild(block);

        var row = Ui.HBox(Palette.Space6);
        row.MouseFilter = Control.MouseFilterEnum.Ignore;
        block.AddChild(row);
        var left = Ui.VBox(2);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        left.Alignment = BoxContainer.AlignmentMode.End;
        left.MouseFilter = Control.MouseFilterEnum.Ignore;
        row.AddChild(left);
        _kicker = Ui.Text("", Ui.TextStyle.Caption, Palette.Accent, 13);
        _kicker.AddThemeFontOverride("font", new FontVariation { BaseFont = Fonts.UiBold, SpacingGlyph = 3 });
        left.AddChild(_kicker);
        _title = Ui.Text("", Ui.TextStyle.Display, new Color("f4dca6"), 50);
        _title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.75f));
        _title.AddThemeConstantOverride("shadow_outline_size", 10);
        _title.AddThemeConstantOverride("shadow_offset_y", 2);
        _title.ClipText = true;
        left.AddChild(_title);

        // Hint card on the right.
        _hintBox = new PanelContainer { CustomMinimumSize = new Vector2(460, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkEnd, MouseFilter = Control.MouseFilterEnum.Ignore };
        var hintStyle = Styles.Box(new Color(0.04f, 0.045f, 0.06f, 0.72f), Palette.Radius, Palette.LineStrong, 1, 18, 14);
        hintStyle.BorderColor = Palette.Accent with { A = 0.5f };
        hintStyle.BorderWidthLeft = 3;
        ((PanelContainer)_hintBox).AddThemeStyleboxOverride("panel", hintStyle);
        var hintCol = Ui.VBox(4);
        hintCol.MouseFilter = Control.MouseFilterEnum.Ignore;
        _hintHead = Ui.Text("HINT", Ui.TextStyle.Caption, Palette.Accent);
        _hintHead.AddThemeFontOverride("font", new FontVariation { BaseFont = Fonts.UiBold, SpacingGlyph = 2 });
        hintCol.AddChild(_hintHead);
        _hint = Ui.Text("", Ui.TextStyle.Reading, Palette.Text, 18);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _hint.CustomMinimumSize = new Vector2(420, 0);
        hintCol.AddChild(_hint);
        _hintBox.AddChild(hintCol);
        row.AddChild(_hintBox);

        block.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        _progress = new ProgressLine { CustomMinimumSize = new Vector2(0, 3) };
        block.AddChild(_progress);
        var statusRow = Ui.HBox(Palette.Space2);
        statusRow.MouseFilter = Control.MouseFilterEnum.Ignore;
        statusRow.AddChild(new Spinner { CustomMinimumSize = new Vector2(16, 16), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        _status = Ui.Text("", Ui.TextStyle.Caption, Palette.TextSecondary);
        _status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        statusRow.AddChild(_status);
        _abort = Ui.Ghost("Back to the main menu", () => Abort?.Invoke());
        _abort.Icon = Icons.Get("exit", 28);
        _abort.Visible = false;
        statusRow.AddChild(_abort);
        block.AddChild(statusRow);
    }

    /// <summary>
    /// Shows the loader (or updates it when already up): <paramref name="title"/> large, <paramref name="kicker"/>
    /// above it, art from <paramref name="content"/> (null: the smoke backdrop), a hint when Options allow.
    /// </summary>
    public void Present(string kicker, string title, ClientContent? content, int loadScreen = 0, string tileset = "", bool campaign = false, string status = "Loading…")
    {
        _finishing = false;
        _fadeTween?.Kill();
        // Back to opaque if it was fading off the last area (a player can now turn round in the doorway at once).
        if (_root.Visible) _root.Modulate = Colors.White;
        _kicker.Text = NwnText.Strip(kicker).ToUpperInvariant();
        _title.Text = NwnText.Strip(title);
        _status.Text = status;
        _progress.Value = -1;
        var artResRef = content == null ? "" : FrontEndContent.LoadScreen(content.Resources, loadScreen, tileset, (title + tileset).GetHashCode() & 0x7fffffff);
        var pictures = content == null ? new List<Texture2D>() : FrontEndContent.LoadScreenShots(content, artResRef);
        if (content != null && artResRef.Length > 0) (ArtContent, ArtResRef) = (content, artResRef);
        else if (!_root.Visible) (ArtContent, ArtResRef) = (null, "");
        var art = pictures.FirstOrDefault();
        var hintSource = content?.Resources ?? FrontEndContent.Base?.Resources;
        var hint = Settings.Current.Gameplay.LoadingHints && hintSource != null ? FrontEndContent.Hint(hintSource, campaign) : "";
        if (hint.Length > 0 || !_root.Visible || art != null)
        {
            _hint.Text = hint;
            _hintBox.Visible = hint.Length > 0;
        }
        if (art != null || !_root.Visible)
        {
            _crossfade?.Kill();
            _art.Texture = art;
            _pictures = pictures;
            _picture = 0;
            _pictureTimer = 0;
            _frame.Visible = art != null;
            for (int i = 0; i < 2; i++)
            {
                _shots[i].Texture = i < pictures.Count ? pictures[i] : null;
                _shots[i].Modulate = i == 0 ? Colors.White : Colors.Transparent;
            }
            _atmosphere.Smoke = art == null ? 1 : 0;
            _atmosphere.Refresh();
            // A slow push in on the picture.
            _push?.Kill();
            _push = _frame.CreateTween().SetParallel();
            foreach (var shot in _shots)
            {
                shot.Scale = Vector2.One;
                _push.TweenProperty(shot, "scale", new Vector2(1.08f, 1.08f), 20).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
            }
        }
        if (!_root.Visible)
        {
            _root.Visible = true;
            _root.Modulate = Colors.White;
            _shownAt = Time.GetTicksMsec() / 1000.0;
            _abort.Visible = false;
        }
        PlayerInput.Blocked = true;
    }

    Tween? _crossfade;

    public override void _Process(double delta)
    {
        if (_root.Visible && !_abort.Visible && !_finishing && Abort != null && Time.GetTicksMsec() / 1000.0 - _shownAt > AbortAfter)
        {
            _abort.Visible = true;
            if (_status.Text.EndsWith('…')) _status.Text = _status.Text.TrimEnd('…') + " (this is taking long)";
        }
        foreach (var shot in _shots) shot.PivotOffset = shot.Size / 2;
        // Two pictures: cross-fade between them every few seconds.
        if (!_root.Visible || _pictures.Count < 2) return;
        _pictureTimer += delta;
        if (_pictureTimer < 5.5) return;
        _pictureTimer = 0;
        int next = 1 - _picture;
        _crossfade = _frame.CreateTween().SetParallel();
        _crossfade.TweenProperty(_shots[next], "modulate", Colors.White, 1.2);
        _crossfade.TweenProperty(_shots[_picture], "modulate", Colors.Transparent, 1.2);
        _art.Texture = _pictures[next];
        _picture = next;
    }

    /// <summary>Status text and progress (0 - 1; negative: indeterminate).</summary>
    public void SetStatus(string text, float progress = -1)
    {
        _status.Text = text;
        _progress.Value = progress;
    }

    /// <summary>Fades the loader out (after it has been up for at least a moment, so it never flashes).</summary>
    public void Finish(double minimum = 0.9, Action? completed = null)
    {
        if (!_root.Visible || _finishing) return;
        _finishing = true;
        _progress.Value = 1;
        double wait = Math.Max(0.15, minimum - (Time.GetTicksMsec() / 1000.0 - _shownAt));
        _fadeTween?.Kill();
        _fadeTween = _root.CreateTween();
        _fadeTween.TweenInterval(wait);
        _fadeTween.TweenProperty(_root, "modulate:a", 0f, 0.45).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        _fadeTween.TweenCallback(Callable.From(() =>
        {
            if (!_finishing) return;
            _root.Visible = false;
            _finishing = false;
            _push?.Kill();
            PlayerInput.Blocked = false;
            completed?.Invoke();
        }));
    }

    /// <summary>Seconds an area's loader stays up at least (from when it appeared) before it fades off the area.</summary>
    public const double AreaMinimum = 0.3;

    /// <summary>
    /// The hint an area's loading screen shows. The original copies it into the chat window on every area load, the
    /// first included: "Loading Screen: &lt;hint&gt;" (dialog.tlk 7166 and a loadhints.2da HINT; loadtips scene).
    /// </summary>
    public event Action<string>? AreaHint;

    /// <summary>Follows a session: downloads, area loads, the character list (which ends the first load).</summary>
    public void Attach(ClientSession session, Func<bool> campaign)
    {
        session.WaitForLoadingScreen = true;
        session.DownloadProgress += (got, total) =>
        {
            if (!Showing) Present(session.ModuleName, session.ModuleName, null, status: "Downloading content");
            float p = total > 0 ? (float)got / total : 1;
            SetStatus($"Downloading module content  ·  {got / 1048576.0:0.0} of {total / 1048576.0:0.0} MB", p);
        };
        session.AreaLoading += area =>
        {
            Present(session.ModuleName, area.Name, session.Content, area.LoadScreen, area.Tileset, campaign(), "Building the area…");
            if (Hint.Length > 0) AreaHint?.Invoke(Hint);
        };
        session.AreaLoaded += () =>
        {
            SetStatus("Entering…", 1);
            // The player has the area as soon as it has drawn under the loader (the original hands a player the next
            // area about 0.3 s after it leaves the last one; docs/INTER_AREA_MOVEMENT.md). The loader then fades off
            // over the live area: keys held through the load (W) drive the player at once, as in the original.
            uint token = session.PresentationToken;
            _ = session.CompleteAfterFrame(token, () => PlayerInput.Blocked = false);
            Finish(AreaMinimum);
        };
        session.CharacterListReceived += _ => Finish(0.6);
    }

    /// <summary>A thin bar: fills to <see cref="Value"/>, or shows a moving sheen when it is negative.</summary>
    partial class ProgressLine : Control
    {
        public float Value = -1;
        float _shown;
        public override void _Process(double delta)
        {
            if (Value >= 0) _shown = Mathf.Lerp(_shown, Value, Math.Clamp((float)delta * 8, 0, 1));
            QueueRedraw();
        }
        public override void _Draw()
        {
            DrawRect(new Rect2(Vector2.Zero, Size), new Color(1, 1, 1, 0.1f));
            if (Value >= 0)
            {
                DrawRect(new Rect2(0, 0, Size.X * _shown, Size.Y), Palette.Accent);
                DrawRect(new Rect2(Size.X * _shown - 30, 0, 30, Size.Y), Palette.AccentBright with { A = 0.6f });
                return;
            }
            float t = (float)(Time.GetTicksMsec() / 1000.0 % 1.8) / 1.8f;
            float w = Size.X * 0.22f;
            float x = -w + (Size.X + w) * t;
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float a = MathF.Sin((i + 0.5f) / steps * MathF.PI);
                DrawRect(new Rect2(x + w * i / steps, 0, w / steps + 0.5f, Size.Y), Palette.Accent with { A = a * 0.9f });
            }
        }
    }

    /// <summary>A small rotating arc.</summary>
    partial class Spinner : Control
    {
        public override void _Process(double delta) => QueueRedraw();
        public override void _Draw()
        {
            float t = (float)(Time.GetTicksMsec() / 1000.0);
            var c = Size / 2;
            float r = Math.Min(Size.X, Size.Y) / 2 - 1.5f;
            DrawArc(c, r, 0, MathF.Tau, 32, new Color(1, 1, 1, 0.12f), 2, true);
            DrawArc(c, r, t * 5, t * 5 + 1.9f, 24, Palette.Accent, 2, true);
        }
    }
}
