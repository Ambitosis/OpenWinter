using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Screenshots of the action bar, radial menu and abilities window (--capture-abilities dir): a filled bar,
/// a mode switched on, the radial on yourself and its sub-rings, on a creature and on a door or placeable,
/// the abilities window, the second bar (Shift), and targeting.
/// </summary>
public static class AbilitiesCapture
{
    /// <summary>Left-button press at a, motion to b in steps, release at b (the real drag-and-drop path).</summary>
    static async Task MouseDrag(Node n, Vector2 a, Vector2 b)
    {
        var tree = n.GetTree();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = a, GlobalPosition = a, ButtonMask = MouseButtonMask.Left });
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        for (int i = 1; i <= 10; i++)
        {
            var p = a.Lerp(b, i / 10f);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = (b - a) / 10, ButtonMask = MouseButtonMask.Left });
            await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = b, GlobalPosition = b });
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        await n.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
    }

    public static async Task Run(string dir, ClientSession session, OrbitCamera camera, ActionBar bar,
        Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        Node hud = bar.GetParent();
        while (hud is not CanvasLayer && hud.GetParent() != null) hud = hud.GetParent();
        var radial = hud.GetChildren().OfType<RadialMenu>().First();
        var panel = hud.GetChildren().OfType<AbilitiesPanel>().First();
        camera.Distance = 7;
        await wait(3.0);
        bar.Book.Refresh();
        session.Request("spellbook");
        await wait(1.0);
        GD.Print($"[capture] {bar.Book.Feats.Count} feats, {bar.Book.Modes.Count} modes, {bar.Book.Skills.Count} skills, {bar.Book.Items.Count} usable items, " +
            $"{bar.Book.Spells.Count} spells in {string.Join(", ", bar.Book.ClassNames.Values)}");
        for (int i = 0; i < ActionBar.Slots; i++) bar.Set(0, i, null);
        bar.SetBasePage(0);
        bar.AutoFill();
        await wait(0.6);
        await shot(dir, "50_actionbar");

        // A mode on (glows), and a slot just used (sweep).
        bar.User.ToggleMode(0);
        await wait(0.8);
        int mode = Enumerable.Range(0, ActionBar.Slots).FirstOrDefault(i => bar.Get(0, i)?.Kind == "mode" && bar.Get(0, i)!.Id > 1, -1);
        if (mode >= 0) bar.UseSlot(0, mode);
        await wait(0.3);
        await shot(dir, "51_actionbar_mode");
        session.Request("action-mode", new { mode = 0, on = false });

        // Radial on yourself, then its sub-rings.
        var me = session.Controlled!;
        var at = camera.UnprojectPosition(me.GlobalPosition + Vector3.Up * 1.1f);
        radial.Open(me, at);
        await wait(0.8);
        radial.CaptureHover(2);
        await wait(0.3);
        await shot(dir, "52_radial_self");
        foreach (var (label, name) in new[] { ("Cast Spell", "53b_radial_spells"), ("Special Attacks", "53_radial_combat"), ("Special Abilities", "54_radial_abilities"), ("Issue Command", "55_radial_commands"), ("Emotes", "56_radial_emotes") })
        {
            radial.Open(me, at);
            await wait(0.4);
            if (!radial.Choose(label)) continue;
            await wait(0.4);
            radial.CaptureHover(0);
            await wait(0.2);
            await shot(dir, name);
        }
        radial.Close();

        // Radial on the nearest creature and on the nearest door or placeable.
        var others = session.Objects.Values.Where(v => v.ObjectId != session.ControlledId && v.Visible && !camera.IsPositionBehind(v.GlobalPosition))
            .OrderBy(v => v.GlobalPosition.DistanceTo(me.GlobalPosition)).ToList();
        if (others.FirstOrDefault(v => v.Kind == ObjectKind.Creature) is { } npc)
        {
            camera.Target = npc;
            await wait(0.8);
            radial.Open(npc, camera.UnprojectPosition(npc.GlobalPosition + Vector3.Up * 1.1f));
            await wait(0.8);
            radial.CaptureHover(1);
            await wait(0.2);
            await shot(dir, "57_radial_creature");
            if (radial.Choose("Skills")) { await wait(0.4); await shot(dir, "58_radial_creature_skills"); }
            radial.Open(npc, camera.UnprojectPosition(npc.GlobalPosition + Vector3.Up * 1.1f));
            await wait(0.3);
            if (radial.Choose("DM tools")) { await wait(0.4); radial.CaptureHover(3); await shot(dir, "58b_radial_dm"); } // with --dm
            radial.Close();
            camera.Target = me;
        }
        if (others.FirstOrDefault(v => v.Kind is ObjectKind.Door or ObjectKind.Placeable && (v.Flags & ObjectFlags.Selectable) != 0) is { } thing)
        {
            camera.Target = thing;
            await wait(0.8);
            radial.Open(thing, camera.UnprojectPosition(thing.GlobalPosition + Vector3.Up * 0.6f));
            await wait(0.8);
            await shot(dir, "59_radial_object");
            radial.Close();
            camera.Target = me;
        }
        await wait(0.6);

        // The abilities window.
        panel.Toggle();
        await wait(1.0);
        await shot(dir, "60_abilities_window");
        panel.Close();

        // Bar 2 while Shift is held.
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = true });
        await wait(0.3);
        await shot(dir, "61_actionbar_page2");
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = false });

        // Drink a potion from the bar: its count drops once the server has used it.
        int potion = Enumerable.Range(0, ActionBar.Slots).FirstOrDefault(i => bar.Get(0, i)?.Kind == "item", -1);
        if (potion >= 0)
        {
            var before = bar.User.Look(bar.Get(0, potion)!).Uses;
            bar.UseSlot(0, potion);
            await wait(4.0);
            GD.Print($"[capture] potion uses {before} -> {bar.User.Look(bar.Get(0, potion)!).Uses}");
            await shot(dir, "63_after_potion");
        }

        // Drag F1 onto F9 with real mouse events, then drag F2 off the bar (clears it).
        var views = bar.SlotViews;
        if (views.Count == ActionBar.Slots)
        {
            string Kinds() => string.Join(",", Enumerable.Range(0, ActionBar.Slots).Select(i => bar.Get(0, i)?.Kind ?? "-"));
            GD.Print($"[capture] before drag: {Kinds()}");
            await MouseDrag(bar, views[0].GetGlobalRect().GetCenter(), views[8].GetGlobalRect().GetCenter());
            await MouseDrag(bar, views[1].GetGlobalRect().GetCenter(), views[1].GetGlobalRect().GetCenter() + new Vector2(0, -300));
            await wait(0.3);
            GD.Print($"[capture] after drag:  {Kinds()}");
            await shot(dir, "64_after_drag");
        }

        // Targeting: something that needs a target.
        bar.User.Use(new BarSlot { Kind = "command", Arg = "attack" });
        await wait(0.4);
        await shot(dir, "62_targeting");
        quit();
    }
}
