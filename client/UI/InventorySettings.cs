using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// How item windows show inventories. Classic (default) is the original's grid, items at their saved
/// cells; Modern is a uniform, sortable and filterable view of the same items (positions and capacity
/// stay the server's, so the two never disagree). Kept in user://interface.cfg; the Options screen can
/// bind to <see cref="ModernView"/>, and the inventory header has a switch.
/// </summary>
public static class InventorySettings
{
    const string File = "user://interface.cfg", Section = "inventory";
    static bool? _modern;

    public static event Action? Changed;

    public static bool ModernView
    {
        get => _modern ??= Load();
        set
        {
            if (_modern == value) return;
            _modern = value;
            try
            {
                var cfg = new ConfigFile();
                cfg.Load(File);
                cfg.SetValue(Section, "modern_view", value);
                cfg.Save(File);
            }
            catch (Exception e) { GD.PrintErr($"[ui] saving {File}: {e.Message}"); }
            Changed?.Invoke();
        }
    }

    static bool Load()
    {
        var cfg = new ConfigFile();
        return cfg.Load(File) == Error.Ok && cfg.GetValue(Section, "modern_view", false).AsBool();
    }
}
