using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// What scripts do to the player's own screen: fades, the death panel, cutscene mode, camera
/// moves, PostString text, effect icons, the party list, XP feedback, pause, boot, and targeting
/// mode. One node listening to the server's player notifies.
/// </summary>
public partial class PlayerFx : CanvasLayer
{
    public ClientSession Session = null!;
    public OrbitCamera Camera = null!;
    public CanvasLayer Hud = null!;
    /// <summary>Told about cutscene mode (scripts own the camera then).</summary>
    public DialogPanel? Dialog;
    public ChatLog Chat = null!;
    public PlayerInput Input_ = null!;
    public Interaction Interaction = null!;
    /// <summary>Where effect icons go (the HUD's player frame); a row at the top right otherwise.</summary>
    public HBoxContainer? EffectsHost;
    /// <summary>Shows the death panel instead of this one when set (the classic interface's).</summary>
    public Action<bool, bool, string>? DeathPanel;

    /// <summary>Shared rest presentation clock; server notifications own start/end and pause freezes interpolation.</summary>
    public bool RestActive { get; private set; }
    public float RestDuration { get; private set; }
    public float RestElapsed { get; private set; }
    public float RestProgress => RestDuration > 0 ? Math.Clamp(RestElapsed / RestDuration, 0, 1) : 0;
    public bool RestPaused => _paused;
    bool _paused, _playerPaused, _classicRest;
    public void UseClassicRest(bool enabled)
    {
        _classicRest = enabled;
        if (RestActive && !_paused && _banner != null) _banner.Text = enabled ? "" : "Resting\u2026";
    }

    ColorRect _fade = null!;
    float _fadeAlpha, _fadeTarget, _fadeStep;
    Control _death = null!;
    bool _sawDead;
    Label _deathText = null!;
    Button _respawn = null!, _wait = null!;
    Control _strings = null!;
    readonly Dictionary<int, (Label Label, double Until)> _posted = new();
    HBoxContainer _effects = null!;
    readonly Dictionary<int, Control> _effectIcons = new();
    readonly HashSet<int> _flashingIcons = new();
    Label _banner = null!;
    (float Yaw, float Pitch, float Distance)? _stored;
    bool _cutscene;
    int _lastXp = -1;

    public override void _Ready()
    {
        Layer = 5;
        _strings = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_strings);

        _effects = EffectsHost ?? new HBoxContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -520, OffsetRight = -16, OffsetTop = 48, Alignment = BoxContainer.AlignmentMode.End };
        _effects.AddThemeConstantOverride("separation", 4);
        if (EffectsHost == null) AddChild(_effects);

        _banner = Ui.Overlay("", 30, Palette.AccentBright, Fonts.DisplayBold);
        _banner.AnchorLeft = _banner.AnchorRight = 0.5f;
        _banner.OffsetLeft = -300; _banner.OffsetRight = 300; _banner.OffsetTop = 200;
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_banner);

        BuildDeath();

        // Drawn last so a fade covers everything, windows included.
        _fade = new ColorRect { Color = new Color(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(_fade);

        Session.NotifyReceived += OnNotify;
        Session.ModuleCleared += ClearModule;
        AddChild(new CutsceneAbortInput
        {
            Session = Session,
            // Esc in cutscene mode cancels the cutscene, a conversation shown or
            // not; the conversation stays open (visual scene cutsceneconvo).
            CanRequest = () => _cutscene,
        });
    }

    public override void _ExitTree()
    {
        Session.NotifyReceived -= OnNotify;
        Session.ModuleCleared -= ClearModule;
    }

    void ClearModule()
    {
        // Welcome can discard held old-module notices, including cutscene-off and
        // pause/rest completion. Reset local presentation at the replacement boundary.
        // Preserve a manually hidden HUD when cutscene mode did not hide it.
        if (_cutscene) SetCutscene(false);
        _paused = false;
        RestActive = false;
        RestDuration = RestElapsed = 0;
        _banner.Text = "";
    }

    void OnNotify(NotifyMessage m)
    {
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        bool mine = m.Subject == Session.ControlledId;
        switch (m.Kind)
        {
            case "fade": Fade(d); break;
            case "gui-death":
                ShowDeath(d.GetProperty("respawn").GetBoolean(), d.GetProperty("waitForHelp").GetBoolean(), d.TryGetProperty("help", out var help) ? help.GetString() ?? "" : "");
                break;
            case "gui-death-close":
                _death.Visible = _sawDead = false;
                break;
            case "gui-panel" when d.GetProperty("panel").GetInt32() == 0: // GUI_PANEL_PLAYER_DEATH
                ShowDeath(true, true, "");
                break;
            case "cutscene": SetCutscene(d.GetProperty("enabled").GetBoolean()); break;
            case "camera": CameraOp(d); break;
            case "camera-store": _stored = (Camera.Yaw, Camera.Pitch, Camera.Distance); break;
            case "post-string": PostString(d); break;
            case "hud" when d.GetProperty("self").TryGetProperty("effects", out var effects):
                SyncEffects(effects);
                break;
            case "effect-icon-flash" when mine:
                int iconId = d.GetProperty("icon").GetInt32();
                if (d.GetProperty("flashing").GetBoolean()) _flashingIcons.Add(iconId);
                else _flashingIcons.Remove(iconId);
                break;
            case "party": Party(d); break;
            case "xp-changed" when mine:
            {
                int xp = d.GetProperty("xp").GetInt32();
                if (_lastXp >= 0 && xp > _lastXp) HudParts.Toasts.Instance?.Show($"+{xp - _lastXp:N0} experience", "", "levelup", Palette.Xp, 2.5);
                _lastXp = xp;
                // The classic interface keeps to the original's chat lines (the server's "Experience Points Gained").
                if (!_classicRest)
                    Chat.Feedback($"Experience: {xp:N0}" + (d.TryGetProperty("canLevelUp", out var up) && up.GetBoolean() ? "  ·  you can level up!" : ""), "#a0d0ff");
                // A level just became available: the original plays gui_level_up (levelup scene); "*Level Up!*" floats
                // over the head from SpeechText.
                if (d.TryGetProperty("levelUpReady", out var ready) && ready.GetBoolean() && Session.Content is { } content)
                    Classic.ClassicSounds.Play(Classic.ClassicAssets.For(content), "gui_level_up");
            }
            break;
            case "pause":
            {
                bool was = _playerPaused;
                _paused = d.GetProperty("state").GetInt32() != 0;
                _playerPaused = (d.GetProperty("state").GetInt32() & 2) != 0;
                // The player pause: the original says so in red in the chat window, and shows no banner (pausegame).
                if (_playerPaused != was) Chat.Feedback(_playerPaused ? "PAUSED" : "UNPAUSED", "#ff0101");
                bool classic = Settings.Current.Interface.Classic;
                _banner.Text = _paused && !classic ? "Paused" : RestActive && !_classicRest ? "Resting\u2026" : "";
                break;
            }
            case "boot":
                _banner.Text = "Disconnected";
                Chat.Feedback("You were removed from the game: " + (d.TryGetProperty("reason", out var reason) ? reason.GetString() : ""), "#ff8080");
                break;
            case "achievement":
                Chat.Feedback("Achievement unlocked: " + d.GetProperty("id").GetString(), "#ffd060");
                break;
            case "targeting-mode": Targeting(d); break;
            case "rest" when mine:
                RestActive = d.GetProperty("state").GetString() == "started";
                RestElapsed = 0;
                RestDuration = RestActive ? d.GetProperty("duration").GetSingle() : 0;
                switch (d.GetProperty("state").GetString())
                {
                    // The server sends the original's "Resting." / "Done resting." / "Cancelled Rest." lines;
                    // the classic interface keeps to those.
                    case "started": if (!_classicRest) Chat.Feedback($"Resting... ({d.GetProperty("duration").GetSingle():0} s)", "#b0c8ff"); _banner.Text = _paused ? "Paused" : _classicRest ? "" : "Resting\u2026"; break;
                    case "finished": if (!_classicRest) Chat.Feedback("You feel refreshed.", "#b0c8ff"); _banner.Text = _paused ? "Paused" : ""; HudParts.Toasts.Instance?.Show("Rested", "You feel refreshed.", "rest", Palette.Info); break;
                    case "cancelled": if (!_classicRest) Chat.Feedback("Your rest was interrupted.", "#ffb080"); _banner.Text = _paused ? "Paused" : ""; HudParts.Toasts.Instance?.Show("Rest interrupted", "", "rest", Palette.Warning); break;
                }
                break;
            case "action-mode":
            {
                uint who = d.GetProperty("creature").GetUInt32();
                int mode = d.GetProperty("mode").GetInt32();
                bool on = d.GetProperty("on").GetBoolean();
                if (who == Session.ControlledId)
                {
                    if (mode == 1) Input_.Stealth = on;
                    else if (mode == 0) Input_.Detect = on;
                }
                // The player sees her own character translucent in stealth; others in stealth that she sees are drawn
                // normally (visual scene stealthview: a spotted stealthy NPC is opaque, a heard-only one faint).
                if (mode == 1 && Session.Objects.TryGetValue(who, out var view))
                    view.SetStealth(on && who == Session.ControlledId);
                break;
            }
            case "stealth-visibility":
            {
                // Creatures in stealth that this player neither sees nor hears disappear.
                if (Session.Objects.TryGetValue(d.GetProperty("target").GetUInt32(), out var view))
                    view.SetStealthVisibility(d.GetProperty("stealth").GetBoolean() && !d.GetProperty("seen").GetBoolean() && !d.GetProperty("heard").GetBoolean());
                break;
            }
        }
    }

    // ----- fades -----

    void Fade(JsonElement d)
    {
        string op = d.GetProperty("op").GetString() ?? "";
        // FADE_SPEED_* is the alpha change per frame at the original's frame rate (~30 fps).
        float speed = d.TryGetProperty("speed", out var s) ? s.GetSingle() : 0.01f;
        switch (op)
        {
            case "to-black": _fadeTarget = 1; _fadeStep = speed * 30; break;
            case "from-black": _fadeAlpha = Math.Max(_fadeAlpha, 1); _fadeTarget = 0; _fadeStep = speed * 30; break;
            case "black": _fadeAlpha = _fadeTarget = 1; break;
            case "stop": _fadeAlpha = _fadeTarget = 0; break;
        }
    }

    public override void _Process(double delta)
    {
        if (RestActive && !_paused) RestElapsed = Math.Min(RestDuration, RestElapsed + (float)delta);
        if (_fadeAlpha != _fadeTarget)
        {
            float step = (float)delta * Math.Max(0.05f, _fadeStep);
            _fadeAlpha = _fadeAlpha < _fadeTarget ? Math.Min(_fadeTarget, _fadeAlpha + step) : Math.Max(_fadeTarget, _fadeAlpha - step);
        }
        _fade.Color = new Color(0, 0, 0, _fadeAlpha);
        double now = Time.GetTicksMsec() / 1000.0;
        foreach (var (id, (label, until)) in _posted.ToList())
            if (now > until) { label.QueueFree(); _posted.Remove(id); }
        foreach (var (id, icon) in _effectIcons)
            icon.Modulate = new Color(1, 1, 1, _flashingIcons.Contains(id) ? 0.5f + 0.5f * MathF.Sin((float)now * MathF.Tau) : 1);
        // The death panel goes away once the player is alive again; a script may show it to the living too, who keep it.
        if (_death.Visible && Session.Controlled is { } me)
        {
            bool dead = (me.Flags & ObjectFlags.Dead) != 0;
            if (dead) _sawDead = true;
            else if (_sawDead) _death.Visible = _sawDead = false;
        }
    }

    // ----- death -----

    void ShowDeath(bool respawn, bool wait, string help)
    {
        if (DeathPanel != null) { DeathPanel(respawn, wait, help); return; }
        _respawn.Visible = respawn;
        _wait.Visible = wait;
        _deathText.Text = help.Length > 0 ? help : "Your journey is not over. Return to the living, or wait for an ally to raise you.";
        if (_death.Visible) return;
        _sawDead = false;
        _death.Visible = true;
        _death.Modulate = _death.Modulate with { A = 0 };
        _death.CreateTween().TweenProperty(_death, "modulate:a", 1f, 1.2).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Full screen: the world drained of colour behind a vignette, the verdict, and the choices.</summary>
    void BuildDeath()
    {
        _death = new Control { AnchorRight = 1, AnchorBottom = 1, Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(_death);
        var veil = new ColorRect { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        veil.Material = new ShaderMaterial
        {
            Shader = new Shader
            {
                Code = """
                shader_type canvas_item;
                uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
                void fragment() {
                    vec3 c = textureLod(screen_tex, SCREEN_UV, 1.0).rgb;
                    float l = dot(c, vec3(0.3, 0.59, 0.11));
                    vec3 grey = vec3(l) * vec3(1.0, 0.86, 0.84) * 0.6;
                    float v = smoothstep(1.15, 0.25, length(SCREEN_UV - 0.5) * 1.6);
                    COLOR = vec4(grey * v, 1.0);
                }
                """,
            },
        };
        _death.AddChild(veil);
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore };
        _death.AddChild(center);
        var col = Ui.VBox(14);
        col.Alignment = BoxContainer.AlignmentMode.Center;
        center.AddChild(col);
        var skull = Icons.Rect("death", 52, Palette.Danger.Lightened(0.1f));
        skull.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        col.AddChild(skull);
        var title = Ui.Overlay("You have fallen", 54, Palette.Danger.Lightened(0.25f), Fonts.DisplayBold);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(title);
        col.AddChild(new Divider { Color = Palette.Danger, CustomMinimumSize = new Vector2(460, 1), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter });
        _deathText = Ui.Text("", Ui.TextStyle.Reading, Palette.Text);
        _deathText.HorizontalAlignment = HorizontalAlignment.Center;
        _deathText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _deathText.CustomMinimumSize = new Vector2(520, 0);
        col.AddChild(_deathText);
        var buttons = Ui.HBox(16);
        buttons.Alignment = BoxContainer.AlignmentMode.Center;
        col.AddChild(Ui.Pad(buttons, 0, 10, 0, 0));
        _respawn = Ui.Primary("Respawn", () => { Session.Request("respawn"); _death.Visible = false; });
        _respawn.CustomMinimumSize = new Vector2(170, 44);
        buttons.AddChild(_respawn);
        _wait = Ui.Button("Wait for help", () => _death.Visible = false);
        _wait.CustomMinimumSize = new Vector2(170, 44);
        buttons.AddChild(_wait);
    }

    // ----- cutscenes and camera -----

    void SetCutscene(bool enabled)
    {
        _cutscene = enabled;
        Hud.Visible = !enabled;
        if (Dialog != null) Dialog.Cutscene = enabled;
        _effects.Visible = !enabled;
        Input_.SetProcessUnhandledInput(!enabled);
        Input_.SetProcess(!enabled);
        Interaction.SetProcessUnhandledInput(!enabled);
        Interaction.Suspended = enabled;
    }

    void CameraOp(JsonElement d)
    {
        switch (d.GetProperty("op").GetString())
        {
            case "facing":
            {
                // NWN: direction the camera looks (0 = east, counter-clockwise), pitch 0 = straight down; -1 keeps one.
                float dir = d.GetProperty("direction").GetSingle(), dist = d.GetProperty("distance").GetSingle(), pitch = d.GetProperty("pitch").GetSingle();
                float? yaw = dir >= 0 ? Mathf.DegToRad(dir - 90) : null;
                float? tilt = pitch >= 0 ? -Mathf.DegToRad(Math.Clamp(90 - pitch, 5, 90)) : null;
                float? arm = dist >= 0 ? Math.Clamp(dist, Camera.MinDistance, Camera.MaxDistance) : null;
                int transition = d.TryGetProperty("transition", out var t) ? t.GetInt32() : 0;
                if (transition > 0) { Camera.GlideTo(yaw, tilt, arm, transition); break; }
                if (yaw is { } y) Camera.Yaw = y;
                if (tilt is { } p) Camera.Pitch = p;
                if (arm is { } a) Camera.Distance = a;
                Camera.Snap();
                break;
            }
            case "mode":
                // SetCameraMode: CAMERA_MODE_* (0 chase, 1 top down, 2 stiff chase / driving).
                Camera.Mode = Gameplay.CameraModes.FromInt(d.GetProperty("mode").GetInt32());
                break;
            case "restore":
                if (_stored is { } s) { Camera.Yaw = s.Yaw; Camera.Pitch = s.Pitch; Camera.Distance = s.Distance; Camera.Snap(); }
                break;
            case "height":
            {
                float h = d.GetProperty("height").GetSingle();
                Camera.HeightOffset = h > 0 ? h : OrbitCamera.DefaultHeight;
                break;
            }
            case "limits":
            {
                float minD = d.GetProperty("minDistance").GetSingle(), maxD = d.GetProperty("maxDistance").GetSingle();
                Camera.MinDistance = minD >= 0 ? minD : 2.5f;
                Camera.MaxDistance = maxD >= 0 ? maxD : 30f;
                Camera.Distance = Math.Clamp(Camera.Distance, Camera.MinDistance, Camera.MaxDistance);
                Camera.SetPitchLimits(d.TryGetProperty("minPitch", out var minP) ? minP.GetSingle() : -1, d.TryGetProperty("maxPitch", out var maxP) ? maxP.GetSingle() : -1);
                break;
            }
            case "flags":
                Camera.Flags = d.GetProperty("flags").GetInt32();
                break;
            case "attach":
            {
                // AttachCamera: the camera follows another creature or placeable the player sees; the player's
                // own character (or none) hands it back.
                uint target = d.GetProperty("target").GetUInt32();
                bool other = target != Session.ControlledId && Session.Objects.ContainsKey(target);
                Camera.Target = other ? Session.Objects[target] : Session.Controlled;
                if (other && d.TryGetProperty("findClearView", out var clear) && clear.GetBoolean()) Camera.LookAlong(Camera.Target!);
                Camera.Snap();
                break;
            }
        }
    }

    // ----- PostString -----

    void PostString(JsonElement d)
    {
        int id = d.GetProperty("id").GetInt32();
        if (_posted.Remove(id, out var old)) old.Label.QueueFree();
        string text = NwnText.Strip(d.GetProperty("text").GetString() ?? "");
        if (text.Length == 0) return;
        // Positions are in character cells from the anchor (SCREEN_ANCHOR_* 0..8: top-left, top, top-right, ...).
        const float cellW = 9, cellH = 18;
        int anchor = d.GetProperty("anchor").GetInt32();
        var label = UiTheme.Label(text, 16, RgbaColor(d.GetProperty("rgba").GetUInt32()));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 5);
        float ax = (anchor % 3) * 0.5f, ay = (anchor / 3) * 0.5f;
        label.AnchorLeft = label.AnchorRight = ax;
        label.AnchorTop = label.AnchorBottom = ay;
        label.OffsetLeft = d.GetProperty("x").GetInt32() * cellW;
        label.OffsetTop = d.GetProperty("y").GetInt32() * cellH;
        _strings.AddChild(label);
        float life = d.GetProperty("life").GetSingle();
        _posted[id == 0 ? -_posted.Count - 1 : id] = (label, life > 0 ? Time.GetTicksMsec() / 1000.0 + life : double.MaxValue);
    }

    static Color RgbaColor(uint rgba) => new((rgba >> 24 & 255) / 255f, (rgba >> 16 & 255) / 255f, (rgba >> 8 & 255) / 255f, Math.Max(1, rgba & 255) / 255f);

    // ----- effect icons -----

    /// <summary>Move the same authoritative icons into the active interface's host.</summary>
    public void SetEffectsHost(HBoxContainer host)
    {
        if (_effects == host) return;
        foreach (var icon in _effectIcons.Values) icon.Reparent(host);
        _effects = host;
        ResizeEffects();
    }

    void ResizeEffects()
    {
        bool classic = _effects.HasMeta("classic_effects");
        foreach (var icon in _effectIcons.Values)
        {
            icon.CustomMinimumSize = new Vector2(classic ? 40 : 26, classic ? 40 : 26);
            icon.QueueRedraw();
        }
    }

    void SyncEffects(JsonElement effects)
    {
        var wanted = effects.EnumerateArray().Select(e => e.GetInt32()).ToHashSet();
        foreach (var id in _effectIcons.Keys.Where(id => !wanted.Contains(id)).ToArray())
        {
            var old = _effectIcons[id];
            old.GetParent()?.RemoveChild(old);
            old.QueueFree();
            _effectIcons.Remove(id);
            _flashingIcons.Remove(id);
        }
        if (Session.Content is not { } content) return;
        var table = content.Resources.TwoDA("effecticons");
        foreach (int id in effects.EnumerateArray().Select(e => e.GetInt32()))
        {
            if (_effectIcons.ContainsKey(id)) continue;
            string resref = table.Get(id, "Icon").ToLowerInvariant();
            if (resref.Length == 0) continue;
            var control = new EffectTile { Assets = Classic.ClassicAssets.For(content), Resref = resref, CustomMinimumSize = new Vector2(26, 26) };
            int strref = table.GetInt(id, "StrRef", -1);
            control.TooltipText = strref >= 0 ? content.Resources.TlkString(unchecked((uint)strref)) : table.Get(id, "Label");
            control.SetMeta(Classic.ClassicTooltip.Meta, control.TooltipText);
            control.MouseFilter = Control.MouseFilterEnum.Pass;
            _effects.AddChild(control);
            _effectIcons[id] = control;
        }
        ResizeEffects();
    }

    partial class EffectTile : Control
    {
        public Classic.ClassicAssets Assets = null!;
        public string Resref = "";
        // The classic party bar shows the name in its own hover box (the "classic_hover" meta), not Godot's.
        public override string _GetTooltip(Vector2 atPosition) => GetParent()?.HasMeta("classic_effects") == true ? "" : TooltipText;
        public override void _Draw()
        {
            bool classic = GetParent().HasMeta("classic_effects");
            if (classic && Assets.Model("ctl_ig_plreffect") is { } frame)
                Classic.GuiDraw.Model(this, Assets, frame.Rest, -frame.Bounds.Position, Colors.White,
                    skip: m => m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase));
            if (Assets.Texture(Resref) is { } texture)
            {
                var size = new Vector2(classic ? 32 : 26, classic ? 32 : 26);
                DrawTextureRect(texture, new Rect2((Size - size) / 2, size), false);
            }
        }
    }

    // ----- party -----

    // The party list is drawn by the HUD (HudParts.PartyFrames) from the "hud" and "party" notifies.
    static void Party(JsonElement d) { }

    // ----- targeting mode -----

    void Targeting(JsonElement d)
    {
        int types = d.GetProperty("types").GetInt32();
        if (types == 0) { Interaction.Targeting = null; return; }
        // The cursors the script asked for (MOUSECURSOR_MAGIC / NOMAGIC by default) and which object types count.
        HudParts.Cursors.TargetTypes = types;
        HudParts.Cursors.TargetCursor = d.TryGetProperty("cursor", out var good) ? good.GetInt32() : HudParts.Cursors.Original.Magic;
        HudParts.Cursors.BadTargetCursor = d.TryGetProperty("badCursor", out var bad) ? bad.GetInt32() : HudParts.Cursors.Original.NoMagic;
        Interaction.Targeting = (target, ground) =>
        {
            Interaction.Targeting = null;
            var at = ground ?? (target != null ? Rendering.Convert.N(target.Position) : System.Numerics.Vector3.Zero);
            Session.Request("target", new { @object = target?.ObjectId ?? 0x7F000000u, position = new[] { at.X, at.Y, at.Z } });
        };
    }
}
