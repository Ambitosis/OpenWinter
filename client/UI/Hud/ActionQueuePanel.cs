using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>The same authoritative queue as classic, arranged below the modern player frame.</summary>
public partial class ActionQueuePanel : HBoxContainer
{
    public ClientSession Session = null!;
    public HudState State = null!;
    string _shown = "";

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 4);
        State.Changed += Refresh;
        TreeExiting += () => State.Changed -= Refresh;
        Refresh();
    }

    void Refresh()
    {
        string key = string.Join(',', State.Queue.Select(a => $"{a.Id}:{a.Action}:{a.Icon}"));
        if (key == _shown) return;
        _shown = key;
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        foreach (var action in State.Queue)
        {
            string icon = action.Icon.Length > 0 ? action.Icon : action.Action switch
            {
                0 or 43 => "ir_moveto",
                1 => "ir_pickup",
                2 => "ir_drop",
                3 => "ir_attack",
                5 => "ir_open",
                6 => "ir_close",
                7 => "ir_dialog",
                17 => "ir_rest",
                _ => "ir_use",
            };
            var button = new Button
            {
                CustomMinimumSize = new Vector2(42, 42), ExpandIcon = true,
                Icon = Session.Content?.Textures.Get(icon)?.Texture, FocusMode = FocusModeEnum.None,
                TooltipText = (action.Name.Length > 0 ? action.Name + "\n" : "") + "Cancel this action"
            };
            button.AddThemeConstantOverride("icon_max_width", 32);
            button.AddThemeStyleboxOverride("normal", Styles.Hud(4, 5));
            button.Pressed += () => Session.Request("clear-actions", new { id = action.Id });
            AddChild(button);
        }
    }
}
