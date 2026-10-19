using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The quickbar slot's radial (--capture-quickbar-radial dir, Aluvian; add --ui classic for the original's look):
/// right-clicks F12 (her Light), reads the filled ring, empties the slot with Empty Quickslot, reads the empty ring
/// and each sub-ring, then assigns an emote and Assign Examine through it, with checks (docs/QUICKBAR_RADIAL.md).
/// </summary>
public static class QuickbarRadialCapture
{
    public static async Task Run(string dir, ActionBar bar, RadialMenu radial, Func<string, string, Task> shot, Func<double, Task> wait, Action<int> quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        bool failed = false;
        void Check(bool ok, string label) { GD.Print($"[quickbar-radial] {label}={ok}"); failed |= !ok; }
        var tree = radial.GetTree();
        async Task Frame() { await radial.ToSignal(tree, SceneTree.SignalName.ProcessFrame); }
        MouseButtonMask held = 0;
        async Task Move(Vector2 at)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = held });
            await Frame(); await Frame();
        }
        async Task Button(Vector2 at, MouseButton button, bool pressed)
        {
            var mask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right;
            held = pressed ? held | mask : held & ~mask;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = at, GlobalPosition = at, ButtonMask = held });
            await Frame(); await Frame();
        }
        async Task Click(Vector2 at, MouseButton button = MouseButton.Left)
        {
            await Move(at);
            await Button(at, button, true);
            await Button(at, button, false);
        }
        async Task Choose(string label)
        {
            var point = radial.WedgePoint(label);
            Check(point != null, $"offers:{label}");
            if (point is { } p) { await Move(p); await Click(p); }
        }
        try
        {
            await wait(4);
            var classic = Classic.ClassicHud.Current;
            Vector2 Slot(int index) => classic != null ? classic.Quickbar.Slots[index].GetGlobalRect().GetCenter() : bar.SlotRect(index).GetCenter();
            const int f12 = 11;
            bar.SetBasePage(0);
            // Her abilities and spellbook first (the rings are built from them).
            bar.Book.Refresh();
            bar.Book.Session.Request("spellbook");
            for (int i = 0; i < 100 && (!bar.Book.Loaded || bar.Book.ClassNames.Count == 0); i++) await wait(0.1);
            GD.Print($"[quickbar-radial] book: {bar.Book.Spells.Count} spells in {string.Join(", ", bar.Book.ClassNames.Values)}");
            // F12 starts with her Light, whatever an earlier run left there.
            var original = bar.Get(0, f12);
            bar.Set(0, f12, new BarSlot { Kind = "spell", Id = 100, Class = 10, Meta = 255 });
            await wait(0.5);
            await shot(dir, "a_bar");

            // The filled slot's ring: opens on the press, stays open after letting go where it opened.
            await Move(Slot(f12));
            await Button(Slot(f12), MouseButton.Right, true);
            Check(radial.IsOpen && radial.Assigning, "opens_on_press");
            Check(radial.Pointer == radial.Centre && radial.Centre != Slot(f12), "pointer_to_centre_on_open");
            await Button(Slot(f12), MouseButton.Right, false);
            Check(radial.IsOpen, "stays_open_after_release");
            var labels = radial.Labels;
            Check(labels.Count == 8 && labels[3].StartsWith("Use : ", StringComparison.Ordinal) && labels[4] == "Empty Quickslot", "filled_ring:" + string.Join("/", labels));
            await wait(0.3);
            await shot(dir, "b_full");
            if (radial.WedgePoint(labels[3]) is { } use) { await Move(use); await wait(0.2); await shot(dir, "b_full_use"); }
            await Choose("Empty Quickslot");
            await wait(0.3);
            Check(!radial.IsOpen && bar.Get(0, f12) == null, "emptied");
            await Move(new Vector2(640, 300));
            await shot(dir, "c_emptied");

            // The empty slot's ring and each of its sub-rings.
            await Click(Slot(f12), MouseButton.Right);
            labels = radial.Labels;
            string[] expected = ["Custom Text Macro", "Assign Spell", "Attack", "Assign Special Attacks", "Assign Other Actions", "Assign Command", "Assign Special Abilities", "Emote"];
            Check(labels.SequenceEqual(expected), "empty_ring:" + string.Join("/", labels));
            await wait(0.3);
            await shot(dir, "d_empty");
            foreach (var (label, name) in new[] { ("Assign Spell", "ne"), ("Assign Special Attacks", "se"), ("Assign Other Actions", "s"), ("Assign Command", "sw"), ("Assign Special Abilities", "w"), ("Emote", "nw") })
            {
                if (radial.WedgePoint(label) is not { } p) { Check(false, $"offers:{label}"); continue; }
                await Move(p);
                await wait(0.3);
                await shot(dir, $"d_empty_{name}");
                await Click(p);
                Check(radial.Path.Count == 2, $"opens:{label}:{string.Join("/", radial.Labels.Where(l => l.Length > 0))}");
                await wait(0.3);
                await shot(dir, $"e_{name}");
                await Click(radial.Centre); // back
            }
            // Other Actions: Assign Examine N, Assign Talk To E, Assign Barter NW.
            await Choose("Assign Other Actions");
            labels = radial.Labels;
            Check(labels[0] == "Assign Examine" && labels[2] == "Assign Talk To" && labels[7] == "Assign Barter", "other_actions:" + string.Join("/", labels));
            await Choose("Assign Examine");
            await wait(0.3);
            Check(!radial.IsOpen && bar.Get(0, f12) is { Kind: "command", Arg: "examine" }, "assigned_examine");
            // An emote through Emote.
            await Click(Slot(f12), MouseButton.Right);
            Check(radial.Labels[4] == "Empty Quickslot", "now_filled");
            await Choose("Empty Quickslot");
            await Click(Slot(f12), MouseButton.Right);
            await Choose("Emote");
            // The original's emote rings (quickbargaps): three, each More Emotes at north-west.
            Check(radial.Labels.SequenceEqual(["Bow", "Threaten", "Greet", "Laugh", "Salute", "Cheer", "Goodbye", "More Emotes"]), "emote_ring:" + string.Join("/", radial.Labels));
            await Choose("More Emotes");
            Check(radial.Labels.SequenceEqual(["Nod", "Tired", "Beg", "Poisoned", "Bored", "Look Far", "Scratch Head", "More Emotes"]), "more_emotes:" + string.Join("/", radial.Labels));
            await wait(0.3);
            await shot(dir, "e_more_emotes");
            await Choose("More Emotes");
            Check(radial.Labels.SequenceEqual(["Fall down forwards", "", "Sit on ground", "Meditate", "Drink potion", "Read", "Worship", "Fall down backwards"]), "more_emotes2:" + string.Join("/", radial.Labels));
            await wait(0.3);
            await shot(dir, "e_more_emotes2");
            await Choose("Sit on ground");
            await wait(0.3);
            Check(bar.Get(0, f12) is { Kind: "emote", Arg: "sit" }, "assigned_emote:sit");
            await Move(new Vector2(640, 300));
            await shot(dir, "f_assigned");

            // Examine on a spell slot opens its description (the original's: Light's).
            bar.Set(0, f12, new BarSlot { Kind = "spell", Id = 100, Class = 10, Meta = 255 });
            await Click(Slot(f12), MouseButton.Right);
            Check(radial.Labels[0] == "Examine", "spell_examine_offered");
            await Choose("Examine");
            await wait(1.5);
            var description = tree.Root.FindChildren("*", "Control", true, false).OfType<Control>()
                .FirstOrDefault(c => c.IsVisibleInTree() && (c is Classic.Sheets.ClassicDescription || c is UiTheme.Window { TitleLabel.Text: "Light" }));
            Check(description != null, "description_opened");
            await shot(dir, "g_examine_spell");
            if (description is Classic.Sheets.ClassicDescription cd) cd.Close();
            else if (description is UiTheme.Window w) w.Close();

            // A text macro: Custom Text Macro asks for the label, then the line; the slot shows the label, using it
            // shouts the line, and its own ring offers Edit Label, Execute Command, Empty Quickslot and Edit Command.
            string heard = "";
            void OnChat(OpenWinter.Net.ChatMessage m) { if (m.Text.Contains("well met")) heard = $"{m.Channel}:{m.Text}"; }
            bar.Session.ChatReceived += OnChat;
            await Click(Slot(f12), MouseButton.Right);
            await Choose("Empty Quickslot");
            await Click(Slot(f12), MouseButton.Right);
            await Choose("Custom Text Macro");
            await wait(0.3);
            async Task Answer(string text, string name)
            {
                if (Classic.ClassicPopup.Top is { } popup && popup.Panel.GetChildren().OfType<LineEdit>().FirstOrDefault() is { } edit)
                {
                    edit.Text = text;
                    await shot(dir, name);
                    Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true });
                    await Frame(); await Frame();
                    Input.ParseInputEvent(new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false });
                    await Frame(); await Frame();
                }
            }
            if (classic != null)
            {
                await Answer("Hail", "h_macro_label");
                await Answer("Hail and well met", "h_macro_text");
            }
            else if (tree.Root.FindChildren("*", "Control", true, false).OfType<UiTheme.Window>().FirstOrDefault(w => w.IsVisibleInTree() && w.TitleLabel.Text == "Text macro") is { } editor)
            {
                var edits = editor.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().ToList();
                edits[0].Text = "Hail"; edits[1].Text = "Hail and well met";
                await shot(dir, "h_macro_editor");
                editor.FindChildren("*", "Button", true, false).OfType<Button>().First(b => b.Text == "Save").EmitSignal(BaseButton.SignalName.Pressed);
            }
            await wait(0.3);
            Check(bar.Get(0, f12) is { Kind: "macro", Name: "Hail", Text: "Hail and well met" }, "assigned_macro");
            await Move(Slot(f12));
            await wait(1.5);
            await shot(dir, "h_macro_slot");
            await Click(Slot(f12));
            for (int i = 0; i < 30 && heard.Length == 0; i++) await wait(0.1);
            Check(heard == "Shout:Hail and well met", $"macro_shouts:{heard}");
            await Click(Slot(f12), MouseButton.Right);
            labels = radial.Labels;
            Check(labels.Count == 8 && labels[2] == "Edit Label" && labels[3] == "Execute Command" && labels[4] == "Empty Quickslot" && labels[6] == "Edit Command",
                "macro_ring:" + string.Join("/", labels));
            await wait(0.3);
            await shot(dir, "i_macro_ring");
            await Click(radial.Centre, MouseButton.Right);
            radial.Close();
            bar.Session.ChatReceived -= OnChat;
            bar.Set(0, f12, original);
            GD.Print(failed ? "[quickbar-radial] FAIL" : "[quickbar-radial] PASS");
            quit(failed ? 1 : 0);
        }
        catch (Exception ex)
        {
            GD.PushError($"[quickbar-radial] {ex}");
            quit(1);
        }
    }
}
