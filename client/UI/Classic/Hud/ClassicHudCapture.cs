using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// --capture-classic-hud dir (with --ui classic): the classic HUD at rest, with chat on every channel, the
/// second quickbar bank (Shift held), the chat mode list, typing on the talk line, the radial menu on the player
/// and on another creature, and the camera turned (compass). Mouse moves are posted to Godot only (the real
/// mouse never moves).
/// </summary>
public static class ClassicHudCapture
{
    public static async Task Run(string dir, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        await wait(4.0);
        if (ClassicGame.Current is not { } ctx || ClassicHud.Current is not { } hud)
        {
            GD.PrintErr("[classic-hud] the classic interface is not on (run with --ui classic)");
            quit();
            return;
        }
        var tree = hud.GetTree();
        async Task Frame() => await hud.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        async Task Move(Vector2 at)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
            await Frame();
            await Frame();
        }
        async Task Click(Vector2 at, MouseButton button = MouseButton.Left)
        {
            await Move(at);
            var mask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = at, GlobalPosition = at, ButtonMask = mask });
            await Frame();
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = at, GlobalPosition = at });
            await Frame();
            await Frame();
        }
        async Task Key(Key key, bool pressed)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
            await Frame();
        }
        var session = ctx.Session;
        await Move(new Vector2(640, 300));
        await shot(dir, "h1_hud");

        // Chat on each channel and some feedback.
        void Say(ChatChannel ch, string text) => session.Send(new ChatSendMessage { Channel = ch, Text = text }.Write());
        Say(ChatChannel.Talk, "Hello there, this is the talk channel.");
        Say(ChatChannel.Shout, "This is a shout.");
        Say(ChatChannel.Whisper, "And a whisper.");
        Say(ChatChannel.Party, "Party chat.");
        ctx.Feedback("Feedback: a journal entry was updated.");
        ctx.Journal.SetAttention(true); // the journal button pulses after an update
        await wait(1.0);
        await shot(dir, "h2_chat");

        // Hovering a quickbar slot, then the second bank (Shift held).
        var slot = hud.Quickbar.Slots[0];
        await Move(slot.GetGlobalRect().GetCenter());
        await wait(1.2);
        await shot(dir, "h3_slot_hover");
        // The player's portrait: name and area at once (two frames after the pointer arrives).
        await Move(hud.PartyBar.Portrait.GetGlobalRect().GetCenter());
        GD.Print("[classic-hud] portrait hover: " + string.Join(" | ", hud.HoverShown ?? ["(none)"]));
        await shot(dir, "h3b_portrait_hover");
        await Move(new Vector2(640, 300));
        await Key(Godot.Key.Shift, true);
        await wait(0.2);
        await shot(dir, "h4_bank2");
        await Key(Godot.Key.Shift, false);

        // The chat mode list, then typing on the talk line.
        await Click(new Vector2(20, hud.Talk.Top + 10));
        await wait(0.3);
        await shot(dir, "h5_chat_modes");
        await Click(new Vector2(20, hud.Talk.Top + 10));
        await Key(Godot.Key.Enter, true);
        await Key(Godot.Key.Enter, false);
        foreach (var ch in "typing a line")
        {
            Input.ParseInputEvent(new InputEventKey { Unicode = ch, Keycode = (Key)char.ToUpperInvariant(ch), Pressed = true });
            await Frame();
        }
        await shot(dir, "h6_typing");
        await Key(Godot.Key.Escape, true);
        await Key(Godot.Key.Escape, false);

        // The radial menu on the player.
        if (session.Controlled is { } me)
        {
            var at = ctx.Camera.UnprojectPosition(me.GlobalPosition + Vector3.Up * 1.0f);
            await Click(at, MouseButton.Right);
            await wait(0.5);
            await shot(dir, "h7_radial_self");
            if (ctx.Hud.GetChildren().OfType<RadialMenu>().FirstOrDefault() is { } radial)
            {
                radial.CaptureHover(1);
                await wait(0.2);
                await shot(dir, "h7b_radial_hover");
                // Into the first sub-ring (spells, feats ...).
                radial.Choose("Cast Spell");
                await wait(0.4);
                await shot(dir, "h7c_radial_sub");
            }
            await Key(Godot.Key.Escape, true);
            await Key(Godot.Key.Escape, false);
            // On another creature.
            var other = session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v != me)
                .OrderBy(v => v.GlobalPosition.DistanceTo(me.GlobalPosition)).FirstOrDefault();
            if (other != null)
            {
                await Click(ctx.Camera.UnprojectPosition(other.GlobalPosition + Vector3.Up * 1.0f), MouseButton.Right);
                await wait(0.5);
                await shot(dir, "h7d_radial_other");
                await Key(Godot.Key.Escape, true);
                await Key(Godot.Key.Escape, false);
            }
        }

        // Turn the camera a quarter: the compass rose turns with it.
        ctx.Camera.Yaw += MathF.PI / 2;
        await wait(0.6);
        await shot(dir, "h8_turned");

        // Something to do: the action queue shows top left.
        session.Send(new ChatSendMessage { Text = "/attack" }.Write());
        await wait(0.8);
        await shot(dir, "h10_queue");
        session.Request("clear-actions", new { index = -1 });
        await wait(0.5);

        // A level waiting: the character button pulses.
        session.Send(new ChatSendMessage { Text = "/xp 50000" }.Write());
        await wait(1.0);
        await shot(dir, "h9_levelup");
        quit();
    }
}
