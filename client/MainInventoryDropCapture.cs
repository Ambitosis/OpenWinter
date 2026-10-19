using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // --capture-items <dir> --inventory-drop-only: same real input path in either UI,
    // with synthetic viewport events only; cancellation and the completed server action are checked.
    async Task CaptureInventoryDrop(string dir)
    {
        await Wait(2);
        if (_args.ContainsKey("visual-audio")) StartVisualAudioEvidence();
        _input.ToggleInventory?.Invoke();
        await Wait(1);
        var classic = ClassicInventory.Current;
        bool oldView = InventorySettings.ModernView;
        if (classic == null) { InventorySettings.ModernView = false; await Wait(0.2); }
        var grid = classic == null ? Descendants<ItemGrid>(Inventory).First(g => g.DragSource == "pack") : null;
        var items = classic?.Data.Carried ?? Inventory.Carried;
        var item = items.First(i => i.Info?.Plot != true);
        int Page() => classic?.Inventory.Grid.Page ?? grid!.Page;
        Vector2 From()
        {
            if (classic != null)
            {
                var g = classic.Inventory.Grid;
                return g.GetGlobalRect().Position + new Vector2((item.X + 0.5f) * g.Cell.X,
                    (g.Rows - item.Y % g.Rows - item.H + 0.5f) * g.Cell.Y);
            }
            var tile = Descendants<ItemSlot>(grid!).First(s => s.Item?.Id == item.Id);
            return tile.GetGlobalRect().Position + Vector2.One * grid!.Cell / 2;
        }
        bool Owned() => (classic?.Data.Carried ?? Inventory.Carried).Any(i => i.Id == item.Id);
        var world = classic != null ? new Vector2(700, 450) : new Vector2(150, 250);
        var page = classic != null
            ? Descendants<ClassicInventoryWindow.InvTab>(classic.Inventory).First(t => t.Page == 1).GetGlobalRect().GetCenter()
            : Descendants<GridPageTabs>(Inventory).First().GetGlobalRect().GetCenter();
        var rows = new List<object>();
        bool passed = false;
        async Task Record(string name)
        {
            RecordVisualAudioNote($"VISUAL|note|{name}");
            rows.Add(new { name, item = item.Id, owned = Owned(), page = Page(), dragging = GetViewport().GuiIsDragging(),
                worldView = _session.Objects.TryGetValue(item.Id, out var view) ? view.Position.ToString() : null });
            await Shot(dir, name);
        }
        try
        {
            await Record("a_before");
            var from = From();
            Button(from, MouseButton.Left, true);
            await MovePushed(from, world, true);
            await Wait(0.2);
            VisualKey(27, true); VisualKey(27, false);
            await Wait(0.1); // ParseInputEvent dispatches on the next frame; do not release before Escape.
            Button(world, MouseButton.Left, false);
            await Wait(1.5);
            await Record("b_cancelled");
            if (!Owned() || Page() != 0) throw new InvalidOperationException("Cancelled drag changed inventory ownership/page");
            from = From();
            Button(from, MouseButton.Left, true);
            await MovePushed(from, page, true, 6);
            await Wait(0.1);
            await MovePushed(page, world, true, 6);
            await Record("c_crossed_tab");
            Button(world, MouseButton.Left, false);
            await Wait(0.2);
            await Record("d_queued");
            for (int wait = 0; wait < 100 && Owned(); wait++) await Wait(0.15);
            await Record("e_finished");
            if (Owned() || Page() != 0) throw new InvalidOperationException("Completed ground drop failed or changed the page");
            passed = true;
            GD.Print("[inventory-drop] cancellation, page retention and completed ground drop passed");
        }
        finally
        {
            if (_args.ContainsKey("visual-audio")) SaveVisualAudioEvidence(dir);
            InventorySettings.ModernView = oldView;
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "inventory-drop.json"),
                System.Text.Json.JsonSerializer.Serialize(rows, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            GetTree().Quit(passed ? 0 : 1);
        }
    }
}
