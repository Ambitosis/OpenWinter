using OpenWinter.Client.Preferences;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// The player's options (<see cref="GameSettings"/>), loaded from user://settings.json before the main
/// menu and applied to the window, renderer, audio buses, input map and interface. Changing them goes
/// through <see cref="Commit"/> (persisted) or <see cref="Preview"/> (applied, not saved: the Options
/// window while it is open). Anything that depends on a setting reads <see cref="Current"/> and can
/// subscribe to <see cref="Changed"/>.
/// </summary>
public static class Settings
{
    const string FilePath = "user://settings.json";

    /// <summary>The settings in effect. Treat as read-only: change a <see cref="GameSettings.Clone"/> and commit it.</summary>
    public static GameSettings Current { get; private set; } = new();

    /// <summary>Raised after settings were applied (on commit, preview and revert).</summary>
    public static event Action<GameSettings>? Changed;

    /// <summary>The effective key bindings.</summary>
    public static BindingSet Bindings => new(Current.Controls);

    /// <summary>False while the game window is in the background.</summary>
    public static bool WindowFocused { get; private set; } = true;

    /// <summary>Development captures keep the window as launched and never write the settings file.</summary>
    public static bool Locked { get; private set; }

    static SceneTree? _tree;
    static Vector2I _lastWindowSize;

    /// <summary>Loads and applies everything; call once at startup, before any interface is built.</summary>
    public static void Initialize(SceneTree tree, bool locked = false)
    {
        if (_tree == tree) { Apply(Current, window: false); return; } // the scene was reloaded (back to the main menu)
        _tree = tree;
        Locked = locked;
        Current = locked ? new GameSettings() : Load();
        AudioBuses.Ensure();
        var root = tree.Root;
        root.SizeChanged += () =>
        {
            var size = root.Size;
            if (size == _lastWindowSize) return;
            _lastWindowSize = size;
            ApplyInterface(Current);
        };
        root.FocusEntered += () => { WindowFocused = true; AudioBuses.Apply(Current.Audio, focused: true); };
        root.FocusExited += () => { WindowFocused = false; AudioBuses.Apply(Current.Audio, focused: false); };
        Apply(Current, window: !locked);
    }

    static GameSettings Load()
    {
        string? json = null;
        if (Godot.FileAccess.FileExists(FilePath)) json = Godot.FileAccess.GetFileAsString(FilePath);
        return GameSettings.FromJson(json);
    }

    static void Save()
    {
        if (Locked) return;
        using var f = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
        f?.StoreString(Current.ToJson());
    }

    /// <summary>Makes <paramref name="next"/> the settings, applies and saves them.</summary>
    public static void Commit(GameSettings next)
    {
        next.Normalize();
        bool window = WindowChanged(Current.Graphics, next.Graphics);
        Current = next;
        Apply(next, window && !Locked);
        Save();
        Changed?.Invoke(next);
    }

    /// <summary>Applies settings without saving them (live preview while the Options window is open).</summary>
    public static void Preview(GameSettings next)
    {
        next.Normalize();
        bool window = WindowChanged(Current.Graphics, next.Graphics);
        Current = next;
        Apply(next, window && !Locked);
        Changed?.Invoke(next);
    }

    /// <summary>Changes and saves one value: <c>Settings.Update(s =&gt; s.Profile.PlayerName = name)</c>.</summary>
    public static void Update(Action<GameSettings> change)
    {
        var next = Current.Clone();
        change(next);
        Commit(next);
    }

    static bool WindowChanged(GraphicsSettings a, GraphicsSettings b) =>
        a.WindowMode != b.WindowMode || a.Width != b.Width || a.Height != b.Height;

    static void Apply(GameSettings s, bool window)
    {
        if (_tree == null) return;
        GraphicsApply.Apply(_tree.Root, s.Graphics, window);
        AudioBuses.Apply(s.Audio, WindowFocused);
        InputBindings.Register(new BindingSet(s.Controls));
        ApplyInterface(s);
    }

    static void ApplyInterface(GameSettings s)
    {
        if (_tree == null) return;
        var root = _tree.Root;
        // The classic interface caps the scale as the original does (by window size); the modern one doesn't.
        float scale = s.Interface.Classic ? s.Interface.ClassicScale(root.Size.X, root.Size.Y) : s.Interface.EffectiveScale(root.Size.Y);
        if (!Mathf.IsEqualApprox(root.ContentScaleFactor, scale)) root.ContentScaleFactor = scale;
        if (UI.Kit.Styles.GlassEnabled != s.Interface.Glass)
        {
            UI.Kit.Styles.GlassEnabled = s.Interface.Glass;
            SwapGlass(root, s.Interface.Glass);
        }
    }

    /// <summary>Takes the frosted-glass material off open panels (kept aside) or puts it back.</summary>
    static void SwapGlass(Node node, bool on)
    {
        if (node is CanvasItem item)
        {
            if (!on && UI.Kit.Styles.IsGlass(item.Material)) { item.SetMeta("glass_material", item.Material); item.Material = null; }
            else if (on && item.HasMeta("glass_material")) { item.Material = item.GetMeta("glass_material").As<Material>(); item.RemoveMeta("glass_material"); }
        }
        foreach (var child in node.GetChildren()) SwapGlass(child, on);
    }

    /// <summary>The interface scale in effect (for code that converts screen pixels to interface units).</summary>
    public static float UiScale => _tree?.Root.ContentScaleFactor ?? 1f;
}
