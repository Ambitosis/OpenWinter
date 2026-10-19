using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's action queue (pnl_actionqueue), top left while the player has something to do: the current
/// action at the panel's Current node and the waiting ones from its Queue node, each a ctl_actionqueue button
/// (nextlink apart) with the action's icon. Left-click cancels only the selected action.
/// The server reports stable identities, so advancing the queue cannot redirect a cancellation.
/// </summary>
public partial class ClassicActionQueue : Control
{
    public ClassicContext Ctx = null!;
    GuiModel? _panel;
    readonly List<GuiButton> _buttons = new();
    string _shown = "";

    static readonly Dictionary<int, string> Icons = new()
    {
        [0] = "ir_moveto", [1] = "ir_pickup", [2] = "ir_drop", [3] = "ir_attack", [5] = "ir_open", [6] = "ir_close", [7] = "ir_dialog",
        [8] = "ir_dis_trap", [9] = "ir_rec_trap", [10] = "ir_flag_trap", [11] = "ir_examine", [12] = "ir_trap", [13] = "ir_picklock",
        [14] = "ir_lock", [15] = "ir_use", [16] = "ir_animalemp", [17] = "ir_rest", [18] = "ir_taunt", [19] = "ir_use", [21] = "ir_heal",
        [22] = "ir_pickpocket", [31] = "ir_cntrspell", [35] = "ir_follow", [37] = "ir_sit", [43] = "ir_moveto",
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _panel = Ctx.Assets.Model("pnl_actionqueue");
        Ctx.State.Changed += Refresh;
        TreeExiting += () => Ctx.State.Changed -= Refresh;
    }

    /// <summary>The panel's origin at the screen's top-left corner.</summary>
    public void Place(Vector2 view) => Position = Vector2.Zero;

    void Refresh()
    {
        var queue = Ctx.State.Queue;
        var key = string.Join(",", queue.Select(q => $"{q.Id}:{q.Action}:{q.Spell}:{q.Icon}"));
        if (key == _shown) return;
        _shown = key;
        foreach (var b in _buttons) b.QueueFree();
        _buttons.Clear();
        var current = _panel?.Node("Current") ?? new Vector2(3, 3);
        var next = _panel?.Node("Queue") ?? new Vector2(46, 3);
        for (int i = 0; i < queue.Count; i++)
        {
            var entry = queue[i];
            var b = GuiButton.Create(Ctx.Assets, "ctl_actionqueue");
            b.IconSize = new Vector2(32, 32);
            b.Icon = entry.Icon.Length > 0 ? Ctx.Assets.Texture(entry.Icon) : Icon(entry.Action, entry.Spell);
            b.Pressed += () => Ctx.Session.Request("clear-actions", new { id = entry.Id });
            b.SetMeta(ClassicTooltip.Meta, entry.Name.Length > 0 ? entry.Name + " — cancel this action" : "Cancel this action");
            AddChild(b);
            float pitch = b.Model?.Node("nextlink")?.X ?? 43;
            b.PlaceOrigin(i == 0 ? current : next + new Vector2((i - 1) * pitch, 0));
            _buttons.Add(b);
        }
    }

    Texture2D? Icon(int action, int spell)
    {
        var content = Ctx.Session.Content;
        if (spell >= 0 && Ctx.Casting.Info.GetValueOrDefault(spell)?.Icon is { Length: > 0 } si && Ctx.Assets.Texture(si) is { } st) return st;
        string ir = action is 4 or 19 ? "ir_cast" : Icons.GetValueOrDefault(action, "ir_action");
        return content?.Textures.Get(ir) != null ? Ctx.Assets.Texture(ir) : null;
    }
}
