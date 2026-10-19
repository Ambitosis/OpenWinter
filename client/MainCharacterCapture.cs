using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client;

/// <summary>--capture-character dir: the character sheet's tabs and tooltips, then the spellbook (with a detail pane and tooltip).</summary>
public partial class Main
{
    async Task CaptureCharacter(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(1.5);
        Character.Toggle();
        await Wait(1.2);
        await Shot(dir, "30_sheet_skills");
        await HoverShot(dir, "31_tip_ability", Character, n => n is TipPanel && n.GetParent() is GridContainer, 0);
        await HoverShot(dir, "32_tip_ac", Character, n => n is TipPanel && n.GetParent() is HBoxContainer && n.GetIndex() == 1 && n.GetParent().GetChildCount() == 2, 0);
        await HoverShot(dir, "33_tip_skill", Character, n => n is TipPanel && n.GetParent() is VBoxContainer v && v.GetParent() is ScrollContainer, 2);
        Character.ShowTab(1);
        await Wait(0.6);
        await Shot(dir, "34_sheet_feats");
        await HoverShot(dir, "35_tip_feat", Character, n => n is TipPanel && n.GetParent() is GridContainer g && g.GetParent()?.GetParent()?.GetParent() is ScrollContainer, 1);
        Character.ShowTab(2);
        await Wait(0.6);
        await Shot(dir, "36_sheet_details");
        Character.ShowTab(0);
        Character.Close();
        Spellbook.Toggle();
        await Wait(1.2);
        await Shot(dir, "40_spellbook");
        await HoverShot(dir, "41_spell_hover", Spellbook, n => n.Name.ToString().Contains("SpellTile"), 1);
        if (_args.ContainsKey("prepare"))
        {
            Spellbook.CaptureOpenPicker();
            await Wait(0.8);
            await Shot(dir, "43_spell_prepare");
            await HoverShot(dir, "44_spell_prepare_hover", Spellbook, n => n.Name.ToString().Contains("SpellTile"), 0);
            // Prepare the first two spells offered, then show the result.
            await HoverShot(dir, "45_spell_prepared", Spellbook, n => n.Name.ToString().Contains("SpellTile"), 0, click: true);
        }
        GetTree().Quit();
    }

    /// <summary>Moves the mouse over the n-th control matching a predicate under a root (optionally clicks it), waits for the tooltip and captures.</summary>
    async Task HoverShot(string dir, string name, Node root, Func<Control, bool> match, int index, bool click = false)
    {
        var found = new List<Control>();
        void Walk(Node n)
        {
            foreach (var c in n.GetChildren())
            {
                if (c is Control ctl && ctl.IsVisibleInTree() && match(ctl)) found.Add(ctl);
                Walk(c);
            }
        }
        Walk(root);
        if (found.Count <= index) { GD.Print($"[capture] {name}: nothing to hover"); return; }
        var target = found[index];
        var p = target.GetGlobalRect().GetCenter();
        Input.WarpMouse(p);
        // Two motions (the first can be swallowed while focus settles), then rest for the tooltip.
        for (int i = 0; i < 2; i++)
        {
            var q = p + new Vector2(i * 2 - 2, 0);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = q, GlobalPosition = q, Relative = new Vector2(2, 0) });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        if (click)
        {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p + Vector2.One, GlobalPosition = p + Vector2.One });
        }
        await Wait(1.3);
        await Shot(dir, name);
    }
}
