using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// --capture-classic-chat dir (with --ui classic): the chat window's menu and split, driven with mouse events posted to
/// Godot only (the real mouse never moves). The menu held open with the right button over its top and bottom options,
/// its submenu, then opened with a click and hovered with no button held (its label and pointer line
/// must show), Split chosen with a left click, and both windows of the split dragged taller. The chat settings it
/// changes are put back at the end.
/// </summary>
public static class ClassicChatCapture
{
    public static async Task Run(string dir, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        await wait(4.0);
        if (ClassicGame.Current is not { } ctx || ClassicHud.Current is not { } hud)
        {
            GD.PrintErr("[classic-chat] the classic interface is not on (run with --ui classic)");
            quit();
            return;
        }
        var saved = Settings.Current.Interface;
        (bool split, int lines, int secondary, List<string> excluded) before = (saved.ChatSplit, saved.ChatLines, saved.ChatSecondaryLines, saved.ChatExcluded.ToList());
        Settings.Update(s => { s.Interface.ChatSplit = false; s.Interface.ChatLines = 4; s.Interface.ChatSecondaryLines = 4; s.Interface.ChatExcluded.Clear(); });
        hud.Chat.Refresh();
        var tree = hud.GetTree();
        var radial = ctx.Hud.GetChildren().OfType<RadialMenu>().First();
        bool failed = false;
        void Check(bool ok, string label) { GD.Print($"[classic-chat] {label}={ok}"); failed |= !ok; }
        async Task Frame() { await hud.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
        MouseButtonMask held = 0;
        async Task Move(Vector2 at)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = held });
            await Frame();
            await Frame();
        }
        async Task Button(Vector2 at, MouseButton button, bool pressed)
        {
            var mask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right;
            held = pressed ? held | mask : held & ~mask;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = at, GlobalPosition = at, ButtonMask = held });
            await Frame();
            await Frame();
        }
        async Task Drag(Vector2 from, Vector2 to)
        {
            await Move(from);
            await Button(from, MouseButton.Left, true);
            for (int i = 1; i <= 6; i++) await Move(from.Lerp(to, i / 6f));
            await Button(to, MouseButton.Left, false);
        }
        Vector2 Wedge(int index) => radial.Centre + new Vector2(MathF.Sin(index * MathF.Tau / 8), -MathF.Cos(index * MathF.Tau / 8)) * 100;

        // Lines of every kind: speech, a server message, combat information and a dice roll.
        void Say(ChatChannel ch, string text) => ctx.Session.Send(new ChatSendMessage { Channel = ch, Text = text }.Write());
        Say(ChatChannel.Talk, "Talk line for the left window.");
        Say(ChatChannel.Shout, "A shout, also on the left.");
        await wait(0.5);
        ctx.Feedback("Server message for the right window.");
        ctx.Chat.Feedback("Someone damages Aluvian Darkstar: 3 (3 Magical)");
        ctx.Chat.Feedback("Aluvian Darkstar : Fortitude Save : *success* : (12 + 1 = 13 vs. DC: 12)");
        await wait(1.0);

        // 1. Held right button: press on the bar, move over the top option, then the bottom one.
        var bar = hud.Chat.BarRect(false).GetCenter();
        await Move(bar);
        await Button(bar, MouseButton.Right, true);
        Check(radial.IsOpen, "menu_opens_on_press");
        await Move(Wedge(0));
        Check(radial.Hovered == 0, "held_hover_top");
        await wait(0.25);
        await shot(dir, "c1_held_top");
        await Move(Wedge(4));
        Check(radial.Hovered == 4, "held_hover_bottom");
        await wait(0.25);
        await shot(dir, "c2_held_bottom");
        // Let go over the submenu (upper left): it opens, centred where the ring was.
        await Move(Wedge(7));
        await Button(Wedge(7), MouseButton.Right, false);
        Check(radial.IsOpen && radial.Path.Count == 2, "release_opens_submenu");
        await Move(Wedge(2));
        Check(radial.Hovered == 2, "submenu_hover");
        await wait(0.25);
        await shot(dir, "c3_submenu");
        // A left click outside closes it.
        await Button(new Vector2(1000, 200), MouseButton.Left, true);
        await Button(new Vector2(1000, 200), MouseButton.Left, false);
        Check(!radial.IsOpen, "click_outside_closes");

        // 2. Click mode: press and release on the bar, then hover with no button held (this showed no label before).
        await Move(bar);
        await Button(bar, MouseButton.Right, true);
        await Button(bar, MouseButton.Right, false);
        Check(radial.IsOpen, "click_keeps_menu_open");
        await Move(Wedge(1));
        await Move(Wedge(0));
        Check(radial.Hovered == 0, "click_hover_top");
        await wait(0.25);
        await shot(dir, "c4_click_hover_top");
        await Button(Wedge(0), MouseButton.Left, true);
        await Button(Wedge(0), MouseButton.Left, false);
        Check(!radial.IsOpen && Settings.Current.Interface.ChatSplit, "left_click_splits");
        await wait(0.3);
        await shot(dir, "c5_split");
        // The bar let go of the mouse with the menu: a drag out in the world doesn't resize the chat.
        await Move(new Vector2(1000, 300));
        await Button(new Vector2(1000, 300), MouseButton.Left, true);
        await Move(new Vector2(1000, 150));
        await Button(new Vector2(1000, 150), MouseButton.Left, false);
        Check(Settings.Current.Interface.ChatLines == 4, "world_drag_leaves_chat");

        // 3. Each window of the split has its own bar: the left one 3 lines taller, the right one 6.
        var left = hud.Chat.BarRect(false).GetCenter();
        await Drag(left, left - new Vector2(0, 3 * ClassicChatStyle.Line));
        var right = hud.Chat.BarRect(true).GetCenter();
        await Drag(right, right - new Vector2(0, 6 * ClassicChatStyle.Line));
        var prefs = Settings.Current.Interface;
        Check(prefs.ChatLines == 7 && prefs.ChatSecondaryLines == 10, $"heights_saved({prefs.ChatLines},{prefs.ChatSecondaryLines})");
        await wait(0.3);
        await shot(dir, "c6_split_resized");
        // The menu again from the right window's bar: the top option is now Combine.
        right = hud.Chat.BarRect(true).GetCenter();
        await Move(right);
        await Button(right, MouseButton.Right, true);
        await Move(Wedge(0));
        await wait(0.25);
        await shot(dir, "c7_combine");
        await Move(radial.Centre);
        await Button(radial.Centre, MouseButton.Right, false);
        await Button(radial.Centre, MouseButton.Left, true);
        await Button(radial.Centre, MouseButton.Left, false);

        Settings.Update(s =>
        {
            s.Interface.ChatSplit = before.split; s.Interface.ChatLines = before.lines; s.Interface.ChatSecondaryLines = before.secondary;
            s.Interface.ChatExcluded.Clear(); s.Interface.ChatExcluded.AddRange(before.excluded);
        });
        GD.Print(failed ? "[classic-chat] FAIL" : "[classic-chat] PASS");
        quit();
    }
}
