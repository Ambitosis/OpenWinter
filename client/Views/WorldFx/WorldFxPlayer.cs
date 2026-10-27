using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>
/// Shows what scripts change in the world: area audio, sounds, weather, fog, sky, light,
/// tiles, grass, object visual overrides, beams and traps. Subscribe <see cref="Handle"/>
/// to <see cref="ClientSession.NotifyReceived"/>; kinds it doesn't know are ignored.
/// Payloads are documented in docs/notify/world.md, effects.md and creature.md.
/// </summary>
public partial class WorldFxPlayer : Node
{
    public ClientSession Session = null!;
    public OrbitCamera? Camera;
    /// <summary>Logs every handled notify (capture runs).</summary>
    public static bool Verbose;

    public WeatherFx Weather { get; private set; } = null!;
    public GrassFx Grass { get; private set; } = null!;
    public ObjectFx Objects { get; private set; } = null!;
    public BeamFx Beams { get; private set; } = null!;
    public TrapFx Traps { get; private set; } = null!;

    /// <summary>Notify kinds this player handles (others are left to other handlers).</summary>
    public static readonly HashSet<string> Kinds = new()
    {
        "area-audio", "battle-music", "play-sound", "sound-object", "weather", "area-fog", "fog-color", "area-light-color", "area-skybox",
        "area-light-direction", "area-wind", "area-recompute-lighting", "area-tiles", "area-reload", "area-grass", "day-night",
        "tile-light", "camera-lock", "visual-transform", "visual-transform-clear", "object-texture", "object-animation",
        "texture-override", "material-uniform", "material-uniform-reset", "shader-uniform", "hilite-color", "beam",
        "effect-removed", "trap-triggered", "trap-detected", "trap-disarmed",
    };

    sealed class ColorFade
    {
        public required string Key;
        public required Action<uint> Apply;
        public uint From, To;
        public float Duration, Elapsed;
    }

    readonly List<ColorFade> _colorFades = new();

    public override void _Ready()
    {
        Weather = new WeatherFx { Name = "Weather", Session = Session };
        Grass = new GrassFx { Name = "Grass", Session = Session };
        Objects = new ObjectFx { Name = "ObjectFx", Session = Session };
        Beams = new BeamFx { Name = "Beams", Session = Session };
        Traps = new TrapFx { Name = "Traps", Session = Session };
        foreach (var n in new Node[] { Weather, Grass, Objects, Beams, Traps }) AddChild(n);
        Rendering.Shaders.ShaderGlobals.Weather = () => (Weather.Current, Weather.Density);
        Session.AreaLoaded += OnAreaLoaded;
        Session.AreaLoading += OnLoading;
    }

    public override void _ExitTree()
    {
        Session.AreaLoaded -= OnAreaLoaded;
        Session.AreaLoading -= OnLoading;
    }

    void OnLoading(LoadAreaMessage _)
    {
        _colorFades.Clear();
        Weather.Reset();
        Beams.Clear();
        Traps.Clear();
        Objects.AreaChanged();
    }

    void OnAreaLoaded()
    {
        if (Session.Area is { } area)
        {
            Session.AreaAudio?.Remember(area.Info);
            Grass.Build(area);
        }
    }

    public void Handle(NotifyMessage m)
    {
        if (!Kinds.Contains(m.Kind)) return;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrEmpty(m.Json) ? "{}" : m.Json);
            if (Verbose) GD.Print($"[worldfx] {m.Kind} {m.Subject:X} {m.Json}");
            Dispatch(m.Kind, m.Subject, doc.RootElement);
        }
        catch (Exception e)
        {
            GD.PushWarning($"[worldfx] {m.Kind} failed: {e.Message}");
        }
    }

    void Dispatch(string kind, uint subject, JsonElement d)
    {
        var area = Session.Area;
        switch (kind)
        {
            case "area-audio": Session.AreaAudio?.ApplyScript(d); break;
            case "battle-music": Session.AreaAudio?.SetAutoBattle(J.B(d, "on")); break;
            case "play-sound": PlaySound(subject, d); break;
            case "sound-object": (Session.Objects.GetValueOrDefault(subject) as SoundView)?.ApplyScript(d); break;
            case "weather": Weather.Set(J.I(d, "weather"), J.I(d, "lightning", -1)); break;
            case "area-fog" when area != null:
                area.Info.SunFogAmount = (byte)Math.Clamp(J.I(d, "sun", area.Info.SunFogAmount), 0, 100);
                area.Info.MoonFogAmount = (byte)Math.Clamp(J.I(d, "moon", area.Info.MoonFogAmount), 0, 100);
                break;
            case "fog-color" when area != null:
            {
                uint bgr = FxMath.RgbToBgr(J.U(d, "color") & 0xFFFFFF);
                int type = J.I(d, "fogType");
                float fade = J.F(d, "fadeTime");
                var info = area.Info;
                if (type is 0 or 1) FadeColor("sunfog", info.SunFogColor, bgr, fade, c => info.SunFogColor = c);
                if (type is 0 or 2) FadeColor("moonfog", info.MoonFogColor, bgr, fade, c => info.MoonFogColor = c);
                break;
            }
            case "area-light-color" when area != null:
            {
                uint bgr = FxMath.RgbToBgr(J.U(d, "color") & 0xFFFFFF);
                float fade = J.F(d, "fadeTime");
                var info = area.Info;
                switch (J.I(d, "colorType", -1))
                {
                    case 0: FadeColor("moonamb", info.MoonAmbient, bgr, fade, c => info.MoonAmbient = c); break;
                    case 1: FadeColor("moondif", info.MoonDiffuse, bgr, fade, c => info.MoonDiffuse = c); break;
                    case 2: FadeColor("sunamb", info.SunAmbient, bgr, fade, c => info.SunAmbient = c); break;
                    case 3: FadeColor("sundif", info.SunDiffuse, bgr, fade, c => info.SunDiffuse = c); break;
                }
                break;
            }
            case "area-skybox" when area != null:
            {
                int row = J.I(d, "skybox");
                area.Info.SkyBox = row > 0 ? row.ToString() : "";
                if (row <= 0) area.ClearSky();
                break;
            }
            case "area-light-direction" when area != null:
                if (J.V(d, "direction") is { } dir) area.SetLightDirection(J.I(d, "type"), dir, J.F(d, "fade"));
                break;
            case "area-wind":
                Grass.SetWind(J.V(d, "direction") ?? new Vector3(1, 1, 0), J.F(d, "magnitude"), J.F(d, "yaw"), J.F(d, "pitch"));
                Rendering.Shaders.ShaderGlobals.SetWind(J.V(d, "direction") ?? new Vector3(1, 1, 0), J.F(d, "magnitude"), J.B(d, "scripted"));
                break;
            case "area-recompute-lighting":
                if (d.TryGetProperty("placeables", out var list) && list.ValueKind == JsonValueKind.Array)
                    foreach (var p in list.EnumerateArray())
                        if (Session.Objects.GetValueOrDefault(J.U(p, "id")) is StaticObjectView pv)
                            foreach (var light in pv.FindChildren("*", "OmniLight3D", true, false).OfType<OmniLight3D>())
                                light.Visible = J.B(p, "on", true);
                break;
            case "area-tiles" when area != null:
            {
                var tiles = new List<(int, int, int, int, byte)>();
                if (d.TryGetProperty("tiles", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var t in arr.EnumerateArray())
                        tiles.Add((J.I(t, "index", -1), J.I(t, "id"), J.I(t, "orientation"), J.I(t, "height"),
                            (byte)((J.B(t, "animLoop1") ? 1 : 0) | (J.B(t, "animLoop2") ? 2 : 0) | (J.B(t, "animLoop3") ? 4 : 0))));
                var rebuilt = area.ReplaceTiles(J.S(d, "tileset"), tiles);
                // SETTILE_FLAG_RELOAD_GRASS: grass follows the new ground.
                if ((J.I(d, "flags") & 1) != 0) Grass.Build(area);
                Objects.RefreshArea();
                if (Verbose) GD.Print($"[worldfx] rebuilt tiles {string.Join(",", rebuilt)}");
                break;
            }
            case "area-reload" when area != null:
                if (J.S(d, "what") == "grass") Grass.Build(area);
                break;
            case "area-grass" when area != null:
                Grass.ApplyOverride(d, area);
                break;
            case "day-night" when area != null:
                area.ForceDaylight(J.B(d, "night") ? 0f : 1f, J.F(d, "transition"));
                break;
            case "tile-light" when area != null:
                area.RelightTile(J.I(d, "x", -1), J.I(d, "y", -1), J.I(d, "mainLight1"), J.I(d, "mainLight2"), J.I(d, "sourceLight1"), J.I(d, "sourceLight2"));
                break;
            case "camera-lock" when Camera != null:
            {
                bool locked = J.B(d, "locked", true);
                switch (J.S(d, "which"))
                {
                    case "pitch": Camera.LockPitch = locked; break;
                    case "distance": Camera.LockDistance = locked; break;
                    case "direction": Camera.LockYaw = locked; break;
                }
                break;
            }
            case "beam": Beams.Add(subject, d); break;
            case "effect-removed": Beams.Remove(J.L(d, "id")); break;
            case "trap-triggered" or "trap-detected" or "trap-disarmed": Traps.Handle(kind, subject, d); break;
            default: Objects.Handle(kind, subject, d); break;
        }
    }

    void FadeColor(string key, uint from, uint to, float seconds, Action<uint> apply)
    {
        _colorFades.RemoveAll(f => f.Key == key);
        if (seconds <= 0) { apply(to); return; }
        _colorFades.Add(new ColorFade { Key = key, Apply = apply, From = from, To = to, Duration = seconds });
    }

    public override void _Process(double delta)
    {
        if (Session.PresentationLoading) return;
        for (int i = _colorFades.Count - 1; i >= 0; i--)
        {
            var f = _colorFades[i];
            f.Elapsed += (float)delta;
            float t = Math.Clamp(f.Elapsed / f.Duration, 0f, 1f);
            f.Apply(FxMath.LerpBgr(f.From, f.To, t));
            if (t >= 1) _colorFades.RemoveAt(i);
        }
        if (Session.Area is { } area)
        {
            // The game clock, not the lighting's fade: the original switches as soon as it is night (audiofollow).
            Session.AreaAudio?.SetNight(!area.IsDaytime);
            Audio.AudioWorld.Night = !area.IsDaytime;
            Audio.AudioWorld.Hour = (int)(area.ClockMinutes / 60) % 24;
        }
        var camera = GetViewport()?.GetCamera3D();
        Audio.AudioWorld.Listener = Session.Controlled is { } me && IsInstanceValid(me) && camera != null
            ? me.GlobalPosition + (camera.GlobalPosition - me.GlobalPosition) * Gameplay.SoundObjectRules.ListenerCameraFraction
            : camera?.GlobalPosition;
    }

    /// <summary>PlaySound / PlaySoundByStrRef: a one-shot 3D sound at the object (following it) or where it was.</summary>
    void PlaySound(uint subject, JsonElement d)
    {
        var resref = J.S(d, "resref");
        if (resref.Length == 0 || Session.Content?.Audio.Get(resref) is not { } stream) return;
        // Native PlaySound preserves the sample spectrum; do not add Godot's distance low-pass.
        var player = new AudioStreamPlayer3D { Stream = stream, UnitSize = 6, MaxDistance = 45, AttenuationFilterCutoffHz = 20500, Name = "Sound_" + resref, Bus = OpenWinter.Client.Preferences.AudioBuses.Effects3D };
        if (!J.B(d, "detached") && Session.Objects.GetValueOrDefault(subject) is { } view)
        {
            player.Position = new Vector3(0, 0, 1.2f);
            view.AddChild(player);
        }
        else
        {
            Session.Space.AddChild(player);
            player.Position = J.V(d, "position") ?? Vector3.Zero;
        }
        player.Finished += player.QueueFree;
        player.Play();
        LastSound = resref;
    }

    /// <summary>The last one-shot sound started (capture checks).</summary>
    public string LastSound { get; private set; } = "";
}
