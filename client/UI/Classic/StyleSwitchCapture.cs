using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// --capture-style-switch dir: switches the interface style during play, back and forth, opening windows in each
/// style (keys go through the same toggles the keyboard uses), to check that nothing of the other style is left over.
/// </summary>
public static class StyleSwitchCapture
{
    public static async Task Run(string dir, PlayerInputHandle input, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        await wait(5);
        void Style(string s) => Settings.Preview(Settings.Current.Clone().Also(g => g.Interface.Style = s));
        string first = Settings.Current.Interface.Classic ? "classic" : "modern", second = first == "classic" ? "modern" : "classic";
        await shot(dir, $"a_{first}");
        input.ToggleInventory();
        await wait(1);
        await shot(dir, $"b_{first}_inventory");
        Style(second);
        await wait(1.5);
        await shot(dir, $"c_{second}_with_inventory_open");
        input.ToggleInventory();
        await wait(1);
        await shot(dir, $"d_{second}_inventory");
        input.ToggleInventory();
        input.ToggleCharacter();
        await wait(1);
        await shot(dir, $"e_{second}_character");
        input.OpenGameMenu();
        await wait(1);
        await shot(dir, $"f_{second}_menu");
        Style(first);
        await wait(1.5);
        await shot(dir, $"g_{first}_after_switch_from_menu");
        input.ToggleCharacter();
        await wait(1);
        await shot(dir, $"h_{first}_character");
        Style(second);
        await wait(1.5);
        input.ToggleInventory();
        await wait(1);
        await shot(dir, $"i_{second}_again_inventory");
        if (ClassicGame.Current is { } c)
            GD.Print($"[switch] TopRight={c.Hud.TopRight.Visible} roots={c.Hud.GetChildren().OfType<ClassicHudRoot>().Count()} rootValid={ClassicHud.Current != null} processing={ClassicHud.Current?.CanProcess()} player={c.Hud.Player.Visible}");
        quit();
    }

    /// <summary>The window keys as Main wires them (read at call time, so they follow a switch).</summary>
    public sealed class PlayerInputHandle(Gameplay.PlayerInput input)
    {
        public void ToggleInventory() => input.ToggleInventory?.Invoke();
        public void ToggleCharacter() => input.ToggleCharacter?.Invoke();
        public void OpenGameMenu() => input.OpenGameMenu?.Invoke();
    }
}
