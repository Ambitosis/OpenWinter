namespace OpenWinter.Client.UI.Classic;

public static partial class ClassicInventory
{
    /// <summary>The classic item windows of the current game (null in modern mode or before the game's content opened).</summary>
    public static ClassicItems? Current { get; private set; }

    /// <summary>
    /// Builds the classic inventory, store, item examine, stack split and confirmation windows on the HUD, hides
    /// the modern inventory, store, loot and item examine windows whenever they would show, and takes over the
    /// inventory key (I), the page keys ([ and ]) and the HUD's inventory button.
    /// </summary>
    static partial void AttachParts(ClassicContext ctx)
    {
        var items = new ClassicItems { Ctx = ctx };
        items.Inventory = new ClassicInventoryWindow { Ctx = ctx, Items = items, Name = "ClassicInventory" };
        items.Store = new ClassicStoreWindow { ArrangeWithPanels = true, Ctx = ctx, Items = items, Name = "ClassicStore" };
        items.Barter = new ClassicBarterWindow { ArrangeWithPanels = true, Ctx = ctx, Items = items, Name = "ClassicBarter" };
        items.Examine = new ClassicExamineItem { Ctx = ctx, Items = items, Name = "ClassicExamineItem" };
        items.Split = new ClassicStackSplit { Ctx = ctx, Items = items, Name = "ClassicStackSplit" };
        items.Confirm = new ClassicConfirm { Ctx = ctx };
        foreach (var w in new ClassicWindow[] { items.Store, items.Barter, items.Inventory, items.Examine, items.Split })
            ctx.Hud.AddChild(w);
        Current = items;

        // The modern windows keep listening to the server (the inventory keeps the player's items for us) but never show.
        HideAlways(ctx.Inventory);
        HideAlways(ctx.Store);
        HideAlways(ctx.Loot);
        ctx.Inventory.Close();
        ctx.Store.Visible = false;
        ctx.Loot.Visible = false;

        ctx.Input.ToggleInventory = items.Inventory.Toggle;
        ctx.Input.CycleInventory = items.Inventory.Cycle;
        ctx.Hud.OpenInventory = items.Inventory.Toggle;
        items.Inventory.TreeExiting += () => { if (Current == items) Current = null; };
    }

    static partial void DetachParts(ClassicContext ctx)
    {
        if (Current is { } items)
            foreach (var w in new Godot.Node?[] { items.Inventory, items.Store, items.Barter, items.Examine, items.Split })
                if (w != null && Godot.GodotObject.IsInstanceValid(w)) w.QueueFree();
        Current = null;
    }

    static readonly HashSet<Godot.Control> Hooked = new();

    static void HideAlways(Godot.Control modern)
    {
        // Once per modern window: attaching again after a switch to modern and back must not stack handlers.
        if (!Hooked.Add(modern)) return;
        modern.VisibilityChanged += () =>
        {
            if (modern.Visible && ClassicGame.Current != null) modern.Visible = false;
        };
    }
}
