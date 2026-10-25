using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenWinter.Client.Preferences;

// Godot-free: the settings model, its defaults, validation and JSON form. Applying it to the engine is
// done by OpenWinter.Client.Settings (client/Settings); tests compile this folder directly.

public enum WindowMode { Windowed, Borderless, Fullscreen }
public enum Upscaler { Bilinear, Fsr1, Fsr2 }
public enum Msaa { Off, X2, X4, X8 }
public enum PostAntiAlias { Off, Fxaa, Smaa, Taa }
public enum ShadowQuality { Off, Low, Medium, High, Ultra }
public enum TextureFiltering { Bilinear, Aniso2, Aniso4, Aniso8, Aniso16 }

/// <summary>Everything the player can change in Options, persisted as user://settings.json.</summary>
public sealed class GameSettings
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public GraphicsSettings Graphics { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public ControlSettings Controls { get; set; } = new();
    public InterfaceSettings Interface { get; set; } = new();
    public GameplaySettings Gameplay { get; set; } = new();
    public ProfileSettings Profile { get; set; } = new();

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Reads settings; unreadable or missing input gives the defaults. Values are clamped to their valid ranges.</summary>
    public static GameSettings FromJson(string? json)
    {
        GameSettings s;
        try { s = string.IsNullOrWhiteSpace(json) ? new GameSettings() : JsonSerializer.Deserialize<GameSettings>(json, Json) ?? new GameSettings(); }
        catch (JsonException) { s = new GameSettings(); }
        catch (NotSupportedException) { s = new GameSettings(); }
        s.Normalize();
        return s;
    }

    public GameSettings Clone() => FromJson(ToJson());

    /// <summary>Fills missing sections and clamps every value into range.</summary>
    public void Normalize()
    {
        Version = CurrentVersion;
        Graphics ??= new();
        Audio ??= new();
        Controls ??= new();
        Interface ??= new();
        Gameplay ??= new();
        Profile ??= new();
        Graphics.Normalize();
        Audio.Normalize();
        Controls.Normalize();
        Interface.Normalize();
        Gameplay.Normalize();
        Profile.Normalize();
    }

    internal static float Clamp(float v, float min, float max) => float.IsNaN(v) ? min : Math.Clamp(v, min, max);
}

public sealed class GraphicsSettings
{
    public WindowMode WindowMode { get; set; } = WindowMode.Windowed;
    /// <summary>Window size in windowed mode (0 = the default 1600 x 900, shrunk to fit the screen).</summary>
    public int Width { get; set; }
    public int Height { get; set; }
    public bool VSync { get; set; } = true;
    /// <summary>Frames per second cap (0 = unlimited).</summary>
    public int FrameCap { get; set; }
    /// <summary>3D resolution relative to the window (0.5 - 2.0).</summary>
    public float RenderScale { get; set; } = 1f;
    public Upscaler Upscaler { get; set; } = Upscaler.Fsr1;
    public float Sharpness { get; set; } = 0.2f;
    public Msaa Msaa { get; set; } = Msaa.X2;
    public PostAntiAlias PostAntiAlias { get; set; } = PostAntiAlias.Off;
    public ShadowQuality Shadows { get; set; } = ShadowQuality.High;
    public TextureFiltering TextureFiltering { get; set; } = TextureFiltering.Aniso8;
    public bool Ssao { get; set; } = true;
    public bool Glow { get; set; } = true;
    /// <summary>Cut a hole through walls and roofs between the camera and the player (the original's keyholing, on by default).</summary>
    public bool Keyholing { get; set; } = true;
    /// <summary>Exposure multiplier (0.5 - 1.5).</summary>
    public float Brightness { get; set; } = 1f;
    /// <summary>Display gamma (1.6 - 2.8; 2.2 = neutral).</summary>
    public float Gamma { get; set; } = 2.2f;

    public static readonly int[] FrameCaps = { 0, 30, 60, 75, 90, 120, 144, 165, 240 };

    internal void Normalize()
    {
        if (Width < 0 || Height < 0 || (Width > 0 && Width < 640) || (Height > 0 && Height < 360)) Width = Height = 0;
        Width = Math.Min(Width, 16384);
        Height = Math.Min(Height, 16384);
        FrameCap = Math.Clamp(FrameCap, 0, 1000);
        RenderScale = GameSettings.Clamp(RenderScale, 0.5f, 2f);
        Sharpness = GameSettings.Clamp(Sharpness, 0f, 2f);
        Brightness = GameSettings.Clamp(Brightness, 0.5f, 1.5f);
        Gamma = GameSettings.Clamp(Gamma, 1.6f, 2.8f);
        if (!Enum.IsDefined(WindowMode)) WindowMode = WindowMode.Windowed;
        if (!Enum.IsDefined(Upscaler)) Upscaler = Upscaler.Fsr1;
        if (!Enum.IsDefined(Msaa)) Msaa = Msaa.X2;
        if (!Enum.IsDefined(PostAntiAlias)) PostAntiAlias = PostAntiAlias.Off;
        if (!Enum.IsDefined(Shadows)) Shadows = ShadowQuality.High;
        if (!Enum.IsDefined(TextureFiltering)) TextureFiltering = TextureFiltering.Aniso8;
    }

    /// <summary>A named starting point for the quality options.</summary>
    public void ApplyPreset(int level)
    {
        // 0 low, 1 medium, 2 high, 3 ultra
        Shadows = level switch { 0 => ShadowQuality.Low, 1 => ShadowQuality.Medium, 2 => ShadowQuality.High, _ => ShadowQuality.Ultra };
        Msaa = level switch { 0 => Msaa.Off, 1 => Msaa.X2, 2 => Msaa.X2, _ => Msaa.X4 };
        PostAntiAlias = level == 0 ? PostAntiAlias.Fxaa : PostAntiAlias.Off;
        Ssao = level >= 2;
        Glow = level >= 1;
        TextureFiltering = level switch { 0 => TextureFiltering.Aniso2, 1 => TextureFiltering.Aniso4, 2 => TextureFiltering.Aniso8, _ => TextureFiltering.Aniso16 };
        RenderScale = level == 0 ? 0.77f : 1f;
    }
}

public sealed class AudioSettings
{
    public float Master { get; set; } = 0.9f;
    public float Music { get; set; } = 0.7f;
    public float Ambient { get; set; } = 0.8f;
    public float Effects { get; set; } = 0.9f;
    public float Voice { get; set; } = 1f;
    public float Interface { get; set; } = 0.7f;
    /// <summary>Balance of 2D effects and positional sounds; music is independent.</summary>
    public float SpatialBias { get; set; } = 1f;
    public bool MuteWhenUnfocused { get; set; }

    /// <summary>The volume buses: settings name and display name.</summary>
    public static readonly (string Bus, string Label)[] Buses =
    {
        ("Master", "Master"), ("Music", "Music"), ("Ambient", "Ambience"), ("Effects", "Sound effects"), ("Voice", "Voices"), ("Interface", "Interface"),
    };

    public float Get(string bus) => bus switch
    {
        "Master" => Master,
        "Music" => Music,
        "Ambient" => Ambient,
        "Effects" => Effects,
        "Voice" => Voice,
        "Interface" => Interface,
        _ => 1f,
    };

    public void Set(string bus, float v)
    {
        v = GameSettings.Clamp(v, 0, 1);
        switch (bus)
        {
            case "Master": Master = v; break;
            case "Music": Music = v; break;
            case "Ambient": Ambient = v; break;
            case "Effects": Effects = v; break;
            case "Voice": Voice = v; break;
            case "Interface": Interface = v; break;
        }
    }

    internal void Normalize()
    {
        foreach (var (bus, _) in Buses) Set(bus, Get(bus));
        SpatialBias = GameSettings.Clamp(SpatialBias, 0.1f, 1.9f);
    }

    public float SpatialGain(bool positional) => Math.Min(1f, positional ? SpatialBias : 2f - SpatialBias);
}

public sealed class ControlSettings
{
    /// <summary>Bindings the player changed: action id → its keys (an empty list unbinds it). Actions not listed use their defaults.</summary>
    public Dictionary<string, List<KeyBind>> Bindings { get; set; } = new();

    internal void Normalize()
    {
        Bindings ??= new();
        foreach (var key in Bindings.Keys.ToList())
        {
            if (ActionCatalog.Find(key) == null) { Bindings.Remove(key); continue; }
            Bindings[key] = (Bindings[key] ?? new()).Where(b => b != null && !b.IsEmpty).Take(ActionCatalog.SlotsPerAction).ToList();
        }
    }
}

public sealed class InterfaceSettings
{
    /// <summary>Interface scale (0 = automatic from the window height).</summary>
    public float UiScale { get; set; }
    public bool Glass { get; set; } = true;
    public bool ShowFps { get; set; }
    public bool CombatText { get; set; } = true;
    /// <summary>The original game's cursors (gui_mp_*, replaceable by haks) instead of the modern brass set.</summary>
    public bool OriginalCursors { get; set; } = true;
    public int ChatFontSize { get; set; } = 15;
    /// <summary>
    /// The classic quickbar's key labels (F1 ...) also on slots that hold something. The original draws them only on
    /// empty slots (quickbarradial, encumberspeed); on by default (a deliberate difference), off for the original's look.
    /// </summary>
    public bool QuickbarKeyLabels { get; set; } = true;
    /// <summary>NWN's large-font option: the serif high-resolution face instead of the regular sans-serif face.</summary>
    public bool HighResolutionFont { get; set; }
    /// <summary>Spoken and floating text over creatures, in points; a fixed size on screen, as in the original.</summary>
    public int OverheadTextSize { get; set; } = 16;
    /// <summary>Nameplates: 0 off, 1 text only, 2 text, portrait and bubble (NWN's Text bubble mode).</summary>
    public int TextBubbleMode { get; set; } = 1;
    /// <summary>The main menu's backdrop: "" the default scene, "module" the configured module, "none", or a named scene id.</summary>
    public string MenuScene { get; set; } = "";
    /// <summary>"classic" (the default): the original game's interface, drawn from its own art; or "modern". Takes effect on the next menu or game.</summary>
    public string Style { get; set; } = "classic";
    public bool Classic => Style == "classic";
    /// <summary>The classic chat window's menu (right-click its bar): split into two windows side by side (server messages, combat and dice rolls in the right one).</summary>
    public bool ChatSplit { get; set; }
    /// <summary>Lines shown by the classic chat window (the left one when split) and by the right one when split; 0 leaves just the bar (the original saves both heights).</summary>
    public int ChatLines { get; set; } = 4;
    public int ChatSecondaryLines { get; set; } = 4;
    /// <summary>Kinds of line the classic chat window leaves out ("Talk", "Shouts", "Server", "Party", "Dice", "Dialog", "Combat", "Dm").</summary>
    public List<string> ChatExcluded { get; set; } = new();

    internal void Normalize()
    {
        ChatLines = Math.Clamp(ChatLines, 0, 60);
        ChatSecondaryLines = Math.Clamp(ChatSecondaryLines, 0, 60);
        ChatExcluded ??= new();
        UiScale = UiScale <= 0 || float.IsNaN(UiScale) ? 0 : Math.Clamp(UiScale, 0.75f, 2f);
        ChatFontSize = Math.Clamp(ChatFontSize, 11, 24);
        OverheadTextSize = OverheadTextSize <= 0 ? 16 : Math.Clamp(OverheadTextSize, 11, 30);
        TextBubbleMode = Math.Clamp(TextBubbleMode, 0, 2);
        MenuScene ??= "";
        Style = Style == "modern" ? "modern" : "classic";
    }

    /// <summary>The automatic interface scale: 1 up to 1080 lines, then in quarter steps (1440 → 1.25, 2160 → 2).</summary>
    public static float AutoScale(int windowHeight) => Math.Clamp(MathF.Floor(windowHeight / 1080f * 4f + 0.25f) / 4f, 1f, 2f);

    public float EffectiveScale(int windowHeight) => UiScale > 0 ? UiScale : AutoScale(windowHeight);

    /// <summary>
    /// The classic interface's scale in a window: the setting, but no more than the original allows there. Measured
    /// with the partyframes scene (ui.scale 1.1 to 2 at twelve window sizes, docs/CLASSIC_UI.md): the original keeps
    /// the virtual screen at least about <see cref="ClassicMinimumWidth"/> x <see cref="ClassicMinimumHeight"/>, the
    /// largest scale that does rounded to a tenth, and never below 1 (1600x900 at 1.5 draws at 1.2, 1440x960 at 2 at
    /// 1.3, 1024x900 at 1.5 at 1.1, 1280x720 at 1.5 at 1).
    /// </summary>
    public float ClassicScale(int windowWidth, int windowHeight) => Math.Min(EffectiveScale(windowHeight), ClassicScaleCap(windowWidth, windowHeight));

    public const float ClassicMinimumWidth = 937.5f, ClassicMinimumHeight = 750f;

    /// <summary>The largest scale the original's interface takes in a window of this size (see <see cref="ClassicScale"/>).</summary>
    public static float ClassicScaleCap(int windowWidth, int windowHeight) =>
        Math.Max(1f, MathF.Round(Math.Min(windowWidth / ClassicMinimumWidth, windowHeight / ClassicMinimumHeight) * 10f, MidpointRounding.AwayFromZero) / 10f);
}

public sealed class GameplaySettings
{
    /// <summary>Mouse-look speed multiplier (0.25 - 3).</summary>
    public float CameraSensitivity { get; set; } = 1f;
    public bool InvertY { get; set; }
    /// <summary>Speed of the keyboard camera turn (0.25 - 3).</summary>
    public float CameraKeySpeed { get; set; } = 1f;
    /// <summary>Walk unless the walk key is held (which then runs).</summary>
    public bool AlwaysWalk { get; set; }
    /// <summary>Camera mode, numbered as CAMERA_MODE_*: 0 chase, 1 top down (the original's default), 2 driving.</summary>
    public int CameraMode { get; set; } = 1;
    /// <summary>Pull the camera in front of walls, only while keyholing is off. Off by default, as in the original.</summary>
    public bool CameraCollision { get; set; }
    /// <summary>Single player: the game pauses while the game menu or options are open.</summary>
    public bool PauseInMenus { get; set; } = true;
    /// <summary>Single player: the game pauses when the window loses focus (on by default, as the original's
    /// game.auto-pause.on-focus-loss).</summary>
    public bool PauseOnFocusLoss { get; set; } = true;
    /// <summary>Loading screens show gameplay hints.</summary>
    public bool LoadingHints { get; set; } = true;
    /// <summary>Single player's game difficulty (GAME_DIFFICULTY_*): 1 Easy (the original's default), 2 Normal,
    /// 3 D&amp;D Hardcore Rules, 4 Very Difficult. A server uses its own setting.</summary>
    public int Difficulty { get; set; } = 1;
    /// <summary>Party invitations open a window (the original's ui.party.invite-popup.enabled; "Never show this again." turns it off).</summary>
    public bool PartyInvitePopup { get; set; } = true;

    internal void Normalize()
    {
        CameraSensitivity = GameSettings.Clamp(CameraSensitivity, 0.25f, 3f);
        CameraKeySpeed = GameSettings.Clamp(CameraKeySpeed, 0.25f, 3f);
        if (CameraMode is < 0 or > 2) CameraMode = 1;
        if (Difficulty is < 1 or > 4) Difficulty = 1;
    }
}

/// <summary>Remembered between sessions: player name and the servers joined lately.</summary>
public sealed class ProfileSettings
{
    public string PlayerName { get; set; } = "";
    public List<RecentServer> RecentServers { get; set; } = new();
    public const int MaxRecent = 8;

    public void Remember(string address, DateTime when)
    {
        address = address.Trim();
        if (address.Length == 0) return;
        RecentServers.RemoveAll(r => string.Equals(r.Address, address, StringComparison.OrdinalIgnoreCase));
        RecentServers.Insert(0, new RecentServer { Address = address, LastJoined = when });
        if (RecentServers.Count > MaxRecent) RecentServers.RemoveRange(MaxRecent, RecentServers.Count - MaxRecent);
    }

    internal void Normalize()
    {
        PlayerName ??= "";
        if (PlayerName.Length > 32) PlayerName = PlayerName[..32];
        RecentServers = (RecentServers ?? new()).Where(r => r != null && !string.IsNullOrWhiteSpace(r.Address)).Take(MaxRecent).ToList();
    }
}

public sealed class RecentServer
{
    public string Address { get; set; } = "";
    public DateTime LastJoined { get; set; }
}
