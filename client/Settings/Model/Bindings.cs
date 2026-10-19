namespace OpenWinter.Client.Preferences;

/// <summary>
/// One key (or mouse button) with modifiers. <see cref="Key"/> is the name of a Godot <c>Key</c> value for the
/// physical key ("W", "F1", "Escape", "Shift"), or "Mouse" + a mouse button index ("Mouse4").
/// </summary>
public sealed record KeyBind
{
    public string Key { get; init; } = "";
    public bool Ctrl { get; init; }
    public bool Shift { get; init; }
    public bool Alt { get; init; }

    public KeyBind() { }
    public KeyBind(string key, bool ctrl = false, bool shift = false, bool alt = false) { Key = key; Ctrl = ctrl; Shift = shift; Alt = alt; }

    public bool IsEmpty => string.IsNullOrEmpty(Key);
    public bool IsMouse => Key.StartsWith("Mouse", StringComparison.Ordinal) && Key.Length > 5 && char.IsDigit(Key[5]);
    public static bool IsModifierKey(string key) => key is "Shift" or "Ctrl" or "Alt" or "Meta";

    /// <summary>Text for key caps and the bindings list: "Ctrl+F5", "Esc", "Num Enter", "Mouse 4".</summary>
    public string Display(Func<string, string>? keyName = null)
    {
        if (IsEmpty) return "";
        var parts = new List<string>();
        if (Ctrl) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        parts.Add(keyName?.Invoke(Key) ?? Name(Key));
        return string.Join("+", parts);
    }

    /// <summary>A readable name for a Godot key name.</summary>
    public static string Name(string key) => key switch
    {
        "Escape" => "Esc",
        "Enter" => "Enter",
        "KpEnter" => "Num Enter",
        "Pageup" or "PageUp" => "Page Up",
        "Pagedown" or "PageDown" => "Page Down",
        "Backspace" => "Backspace",
        "Space" => "Space",
        "Print" => "Print Screen",
        "KpMultiply" => "Num *",
        "KpAdd" => "Num +",
        "KpSubtract" => "Num -",
        "KpDivide" => "Num /",
        "KpPeriod" => "Num .",
        "Quoteleft" => "`",
        "Minus" => "-",
        "Equal" => "=",
        "Bracketleft" => "[",
        "Bracketright" => "]",
        "Semicolon" => ";",
        "Apostrophe" => "'",
        "Comma" => ",",
        "Period" => ".",
        "Slash" => "/",
        "Backslash" => "\\",
        "Mouse1" => "Left Mouse",
        "Mouse2" => "Right Mouse",
        "Mouse3" => "Middle Mouse",
        "Mouse4" => "Wheel Up",
        "Mouse5" => "Wheel Down",
        "Mouse8" => "Mouse 4",
        "Mouse9" => "Mouse 5",
        _ when key.Length == 4 && key.StartsWith("Key", StringComparison.Ordinal) && char.IsDigit(key[3]) => key[3..],
        _ when key.StartsWith("Kp", StringComparison.Ordinal) && key.Length > 2 => "Num " + key[2..],
        _ => key,
    };
}

/// <summary>The eight wedge directions of the radial menu, as on the numpad.</summary>
public enum RadialDirection { N, NE, E, SE, S, SW, W, NW }

/// <summary>A rebindable game action and its default keys.</summary>
public sealed record ActionDef(string Id, string Label, string Category, KeyBind[] Defaults, bool Rebindable = true);

/// <summary>
/// Every input action the client registers (InputMap names are the ids), in Options order. Defaults are the
/// original game's (its keymap.2da; see docs/CONTROLS.md for how its key codes were decoded). Actions the
/// original has no row for keep keys of our own only where they take nothing from it.
/// </summary>
public static class ActionCatalog
{
    public const int SlotsPerAction = 2;

    public const string MoveForward = "move_forward", MoveBack = "move_back", MoveLeft = "move_left", MoveRight = "move_right", TurnLeft = "turn_left", TurnRight = "turn_right", Walk = "walk";
    public const string CameraLeft = "camera_left", CameraRight = "camera_right", CameraUp = "camera_up", CameraDown = "camera_down", ZoomIn = "camera_zoom_in", ZoomOut = "camera_zoom_out";
    public const string ZoomInFull = "camera_zoom_in_full", ZoomOutFull = "camera_zoom_out_full", CameraUpFull = "camera_up_full", CameraDownFull = "camera_down_full", CameraMode = "camera_mode";
    public const string Attack = "attack", ToggleStance = "toggle_stance", Gesture = "gesture", Rest = "rest", Stealth = "stealth", Detect = "detect", HighlightObjects = "highlight_objects", Pause = "pause";
    public const string RadialSelf = "radial_self", RadialBack = "radial_back";
    public const string Inventory = "inventory", Character = "character", Journal = "journal", Map = "map", Spellbook = "spellbook", PlayerList = "player_list", QuickChat = "quick_chat", SaveLoad = "save_load", Options = "options", Chat = "chat";
    public const string InventoryPrevious = "inventory_previous", InventoryNext = "inventory_next", ToggleGui = "toggle_gui", ToggleKeyhole = "toggle_keyhole";
    public const string QuickSave = "quicksave", QuickLoad = "quickload", Screenshot = "screenshot", GameMenu = "game_menu";
    public const int QuickGroups = 4;
    public static string Quickbar(int slot) => "quickbar_" + (slot + 1);
    /// <summary>The radial menu wedge in a compass direction (numpad 1-9 around 5).</summary>
    public static string Radial(RadialDirection d) => "radial_" + d.ToString().ToLowerInvariant();
    public static string QuickGroupSet(int group) => "quick_group_set_" + (group + 1);
    public static string QuickGroupPick(int group) => "quick_group_pick_" + (group + 1);

    /// <summary>Numpad keys of the radial directions, clockwise from the top (5 in the middle is back).</summary>
    public static readonly (RadialDirection Dir, string Key, string Name)[] RadialKeys =
    [
        (RadialDirection.N, "Kp8", "top"), (RadialDirection.NE, "Kp9", "top right"), (RadialDirection.E, "Kp6", "right"),
        (RadialDirection.SE, "Kp3", "bottom right"), (RadialDirection.S, "Kp2", "bottom"), (RadialDirection.SW, "Kp1", "bottom left"),
        (RadialDirection.W, "Kp4", "left"), (RadialDirection.NW, "Kp7", "top left"),
    ];

    static KeyBind K(string key, bool ctrl = false) => new(key, ctrl);

    public static readonly IReadOnlyList<ActionDef> All = Build();

    static List<ActionDef> Build()
    {
        var list = new List<ActionDef>
        {
            // The original's driving keys: W / S, A / D turn, Q / E sidestep.
            new(MoveForward, "Move forward", "Movement", [K("W")]),
            new(MoveBack, "Move backward", "Movement", [K("S")]),
            new(TurnLeft, "Turn left", "Movement", [K("A")]),
            new(TurnRight, "Turn right", "Movement", [K("D")]),
            new(MoveLeft, "Strafe left", "Movement", [K("Q")]),
            new(MoveRight, "Strafe right", "Movement", [K("E")]),
            new(Walk, "Walk (hold)", "Movement", [K("Shift")]),

            new(CameraLeft, "Rotate camera left", "Camera", [K("Left")]),
            new(CameraRight, "Rotate camera right", "Camera", [K("Right")]),
            new(ZoomIn, "Zoom in", "Camera", [K("Up"), K("KpAdd")]),
            new(ZoomOut, "Zoom out", "Camera", [K("Down"), K("KpSubtract")]),
            new(CameraUp, "Camera up", "Camera", [K("Pageup")]),
            new(CameraDown, "Camera down", "Camera", [K("Pagedown")]),
            new(ZoomInFull, "Zoom in full", "Camera", [K("Home")]),
            new(ZoomOutFull, "Zoom out full", "Camera", [K("End")]),
            new(CameraUpFull, "Camera up full", "Camera", [K("Insert")]),
            new(CameraDownFull, "Camera down full", "Camera", [K("Delete")]),
            new(CameraMode, "Toggle camera mode", "Camera", [K("KpMultiply")]),

            new(Attack, "Attack", "Actions", [K("F")]),
            new(ToggleStance, "Combat stance", "Actions", [K("X")]),
            new(Gesture, "Gesture", "Actions", []),
            new(Rest, "Rest", "Actions", [K("R")]),
            new(Stealth, "Stealth mode", "Actions", []),
            new(Detect, "Detect mode", "Actions", [K("T")]),
            new(HighlightObjects, "Tooltips (hold, double-tap to keep)", "Actions", [K("Tab")]),
            new(Pause, "Pause (single player)", "Actions", [K("Space")]),

            new(RadialSelf, "Radial menu on self", "Radial menu", [K("Kp0")]),
        };
        foreach (var (dir, key, name) in RadialKeys) list.Add(new(Radial(dir), "Radial menu " + name, "Radial menu", [K(key)]));
        list.Add(new(RadialBack, "Radial menu back", "Radial menu", [K("Kp5")]));
        list.AddRange(
        [
            new(Inventory, "Inventory", "Windows", [K("I")]),
            new(Character, "Character sheet", "Windows", [K("C")]),
            new(Journal, "Journal", "Windows", [K("J")]),
            new(Map, "Map", "Windows", [K("M")]),
            new(Spellbook, "Spellbook", "Windows", [K("B")]),
            new(PlayerList, "Player list (PvP)", "Windows", [K("P")]),
            new(QuickChat, "Quick chat", "Windows", [K("V")]),
            new(Options, "Options", "Windows", [K("O")]),
            new(SaveLoad, "Save / Load", "Windows", []),
            new(Chat, "Chat", "Windows", [K("Enter"), K("KpEnter")]),
            new(InventoryPrevious, "Previous inventory page", "Windows", [K("Bracketleft")]),
            new(InventoryNext, "Next inventory page", "Windows", [K("Bracketright")]),
            new(ToggleGui, "Hide / show interface", "Windows", [K("H")]),
            // The original lists Tab (the tooltip key) here too, but Tab only toggles keyholing along with tooltips
            // when its "keyholing with tooltip" setting is on, so this has no key of its own.
            new(ToggleKeyhole, "Toggle keyhole dissolve", "Windows", []),
        ]);
        // No default keys in the original either.
        for (int i = 0; i < QuickGroups; i++) list.Add(new(QuickGroupSet(i), $"Set quick group {i + 1}", "Quick groups", []));
        for (int i = 0; i < QuickGroups; i++) list.Add(new(QuickGroupPick(i), $"Select quick group {i + 1}", "Quick groups", []));
        for (int i = 0; i < 12; i++) list.Add(new(Quickbar(i), $"Quickbar slot {i + 1}", "Quickbar", [K("F" + (i + 1))]));
        list.Add(new(QuickSave, "Quick save", "System", [K("G")]));
        list.Add(new(QuickLoad, "Quick load", "System", []));
        list.Add(new(Screenshot, "Screenshot", "System", [K("Print")]));
        list.Add(new(GameMenu, "Game menu / close window", "System", [K("Escape")], Rebindable: false));
        return list;
    }

    public static ActionDef? Find(string id) => All.FirstOrDefault(a => a.Id == id);
    public static IEnumerable<string> Categories => All.Select(a => a.Category).Distinct();
}

/// <summary>The effective bindings: the defaults with the player's changes from <see cref="ControlSettings"/>.</summary>
public sealed class BindingSet
{
    readonly ControlSettings _controls;
    public BindingSet(ControlSettings controls) => _controls = controls;

    public IReadOnlyList<KeyBind> Get(string action) =>
        _controls.Bindings.TryGetValue(action, out var custom) ? custom : ActionCatalog.Find(action)?.Defaults ?? (IReadOnlyList<KeyBind>)Array.Empty<KeyBind>();

    public KeyBind? Get(string action, int slot) => Get(action) is var list && slot < list.Count ? list[slot] : null;

    public bool IsCustom(string action) => _controls.Bindings.ContainsKey(action);

    /// <summary>The action (other than <paramref name="except"/>) a key is bound to, if any.</summary>
    public string? Owner(KeyBind bind, string? except = null) =>
        ActionCatalog.All.FirstOrDefault(a => a.Id != except && Get(a.Id).Contains(bind))?.Id;

    /// <summary>
    /// Binds a key to an action's slot (null clears it). The key is taken away from any other action
    /// that had it; those are returned so the interface can say what moved. Keys of fixed actions
    /// (Esc) cannot be taken: the call then changes nothing and returns null.
    /// </summary>
    public List<string>? Assign(string action, int slot, KeyBind? bind)
    {
        var def = ActionCatalog.Find(action);
        if (def == null || !def.Rebindable || slot < 0 || slot >= ActionCatalog.SlotsPerAction) return null;
        var displaced = new List<string>();
        if (bind != null && !bind.IsEmpty)
        {
            var owner = Owner(bind, action);
            if (owner != null && ActionCatalog.Find(owner) is { Rebindable: false }) return null;
            foreach (var other in ActionCatalog.All)
            {
                if (other.Id == action || !Get(other.Id).Contains(bind)) continue;
                Set(other.Id, Get(other.Id).Where(b => b != bind).ToList());
                displaced.Add(other.Id);
            }
        }
        var list = Get(action).ToList();
        while (list.Count <= slot) list.Add(new KeyBind());
        if (bind != null) list.Remove(bind); // the same key in the other slot moves here
        if (slot < list.Count) list[slot] = bind ?? new KeyBind();
        else list.Add(bind ?? new KeyBind());
        Set(action, list.Where(b => !b.IsEmpty).ToList());
        return displaced;
    }

    void Set(string action, List<KeyBind> binds)
    {
        var defaults = ActionCatalog.Find(action)?.Defaults ?? [];
        if (binds.SequenceEqual(defaults)) _controls.Bindings.Remove(action);
        else _controls.Bindings[action] = binds;
    }

    public void Reset(string action) => _controls.Bindings.Remove(action);
    public void ResetAll() => _controls.Bindings.Clear();

    /// <summary>Keys bound to more than one action (each pair once).</summary>
    public List<(string A, string B, KeyBind Bind)> Conflicts()
    {
        var result = new List<(string, string, KeyBind)>();
        var all = ActionCatalog.All;
        for (int i = 0; i < all.Count; i++)
            foreach (var bind in Get(all[i].Id))
                for (int j = i + 1; j < all.Count; j++)
                    if (Get(all[j].Id).Contains(bind)) result.Add((all[i].Id, all[j].Id, bind));
        return result;
    }
}
