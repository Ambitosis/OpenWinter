using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Inventory;

/// <summary>
/// An inventory grid's geometry: columns, rows per page and pages (0 = as many as needed). An item
/// covers InvSlotWidth x InvSlotHeight cells from its top-left cell and never straddles two pages.
/// </summary>
public readonly record struct GridShape(int Width, int PageHeight, int Pages)
{
    public bool Limited => Pages > 0;
    public int Rows => Pages > 0 ? Pages * PageHeight : int.MaxValue;

    /// <summary>Whether a w x h footprint at (x, y) lies inside the grid and on one page.</summary>
    public bool Contains(int x, int y, int w, int h) =>
        x >= 0 && y >= 0 && x + w <= Width && (long)y + h <= Rows
        && (h > PageHeight || y / PageHeight == (y + h - 1) / PageHeight);
}

/// <summary>
/// The inventory grid: every item in a creature, placeable, store or container inventory has a cell
/// (<see cref="Item.InvX"/>, <see cref="Item.InvY"/>, saved as Repos_PosX / Repos_Posy). New items go
/// to the first free place reading row by row, page by page; a creature's backpack is full when an
/// item (after filling matching stacks) fits nowhere.
///
/// <para>Shapes come from what the original saves: across the 288 characters in the local vaults no
/// item sits beyond column 9 or row 35 and none crosses a 6-row boundary (10 x 6 pages, 6 pages);
/// placed placeables keep their items in the first 6 columns; stores lay each category out 10 wide;
/// bags were seen to use at least 7 columns and get the backpack's width.</para>
/// </summary>
public sealed partial class InventorySystem
{
    public static readonly GridShape BackpackGrid = new(10, 6, 6);
    // Measured on the original server (parity suite items_inventory): placeables fill 7 x 5 pages and
    // open another when one is full; a bag holds one 7 x 5 page.
    public static readonly GridShape PlaceableGrid = new(7, 5, 0);
    public static readonly GridShape StoreGrid = new(10, 10, 0);
    public static readonly GridShape BagGrid = new(7, 5, 1);

    public GridShape Shape(GameObject holder) => holder switch
    {
        Creature => BackpackGrid,
        Placeable => PlaceableGrid,
        Store => StoreGrid,
        _ => BagGrid,
    };

    /// <summary>Cells an item covers (baseitems InvSlotWidth x InvSlotHeight).</summary>
    public (int W, int H) Footprint(Item item) => Rules.Footprint(item.BaseItem);

    /// <summary>The store category an item is listed under (stores keep one grid per category).</summary>
    int PanelIn(GameObject holder, Item item) =>
        holder is Store ? (item.StorePanel >= 0 && InventoryList(holder)?.Contains(item) == true ? item.StorePanel : Rules.StorePanel(item.BaseItem)) : 0;

    /// <summary>The items sharing <paramref name="item"/>'s grid in <paramref name="holder"/> (placed ones only).</summary>
    IEnumerable<Item> GridMates(GameObject holder, Item item, int panel, Item? ignore)
    {
        if (InventoryList(holder) is not { } list) yield break;
        foreach (var other in list)
        {
            if (other == item || other == ignore || other.IsDestroyed || other.InvX < 0 || other.InvY < 0) continue;
            if (holder is Store && other.StorePanel != panel) continue;
            yield return other;
        }
    }

    HashSet<(int, int)> Occupied(GameObject holder, Item item, int panel, Item? ignore)
    {
        var cells = new HashSet<(int, int)>();
        foreach (var other in GridMates(holder, item, panel, ignore))
        {
            var (w, h) = Footprint(other);
            for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) cells.Add((other.InvX + i, other.InvY + j));
        }
        return cells;
    }

    static bool Free(HashSet<(int, int)> occupied, int x, int y, int w, int h)
    {
        for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) if (occupied.Contains((x + i, y + j))) return false;
        return true;
    }

    /// <summary>Whether <paramref name="item"/> could sit with its top-left cell at (x, y) in <paramref name="holder"/>'s grid.</summary>
    public bool FitsAt(GameObject holder, Item item, int x, int y, Item? ignore = null)
    {
        var (w, h) = Footprint(item);
        return Shape(holder).Contains(x, y, w, h) && Free(Occupied(holder, item, PanelIn(holder, item), ignore), x, y, w, h);
    }

    /// <summary>The first free place for an item, row by row and page by page; null when the grid is full.</summary>
    public (int X, int Y)? FirstFit(GameObject holder, Item item, Item? ignore = null)
    {
        var shape = Shape(holder);
        var (w, h) = Footprint(item);
        var occupied = Occupied(holder, item, PanelIn(holder, item), ignore);
        return InventoryGridPlacement.FirstFit(shape, w, h, occupied);
    }

    /// <summary>
    /// Gives every item in a holder's grid a valid cell: keeps stored positions that are inside the
    /// grid and don't overlap an earlier item, places the rest first-fit. Items that fit nowhere stay
    /// unplaced (they are never lost; they take the first room that opens up).
    /// </summary>
    public void ArrangeLoaded(GameObject holder)
    {
        if (InventoryList(holder) is not { } list || list.Count == 0) return;
        var shape = Shape(holder);
        var kept = new List<Item>();
        foreach (var item in list)
        {
            if (item.IsDestroyed || item.InvX < 0 || item.InvY < 0) continue;
            var (w, h) = Footprint(item);
            int panel = PanelIn(holder, item);
            bool ok = shape.Contains(item.InvX, item.InvY, w, h);
            if (ok)
            {
                // Overlap only with items already kept.
                foreach (var other in kept)
                {
                    if (holder is Store && other.StorePanel != panel) continue;
                    var (ow, oh) = Footprint(other);
                    if (item.InvX < other.InvX + ow && other.InvX < item.InvX + w && item.InvY < other.InvY + oh && other.InvY < item.InvY + h) { ok = false; break; }
                }
            }
            if (ok) kept.Add(item);
            else item.InvX = item.InvY = -1;
        }
        PlaceUnplaced(holder);
    }

    /// <summary>First-fit cells for items in a holder that have none (added without a position).</summary>
    void PlaceUnplaced(GameObject holder)
    {
        if (InventoryList(holder) is not { } list) return;
        foreach (var item in list)
        {
            if (item.IsDestroyed || item.InvX >= 0 && item.InvY >= 0) continue;
            if (FirstFit(holder, item) is { } cell) (item.InvX, item.InvY) = cell;
        }
    }

    /// <summary>
    /// How much of <paramref name="item"/>'s stack the matching stacks in <paramref name="target"/>
    /// can absorb (what merging would take before the rest needs a cell of its own).
    /// </summary>
    int StackRoom(GameObject target, Item item)
    {
        if (InventoryList(target) is not { } list) return 0;
        int max = Rules.MaxStack(item.BaseItem), room = 0;
        foreach (var other in list)
            if (other != item && !other.IsDestroyed && other.StackSize < max && CanStack(other, item)) room += max - other.StackSize;
        return room;
    }

    /// <summary>
    /// GetBaseItemFitsInInventory: a free place for the base item's footprint. Placeables (unlimited pages)
    /// only look at their last page; containers aren't refused for bags here; stores never have room.
    /// </summary>
    public bool BaseItemFits(GameObject holder, int baseItem)
    {
        if (holder is Store) return false;
        var shape = Shape(holder);
        var (w, h) = Rules.Footprint(baseItem);
        var cells = new HashSet<(int, int)>();
        if (InventoryList(holder) is { } list)
            foreach (var other in list)
            {
                if (other.IsDestroyed || other.InvX < 0 || other.InvY < 0) continue;
                var (ow, oh) = Footprint(other);
                for (int i = 0; i < ow; i++) for (int j = 0; j < oh; j++) cells.Add((other.InvX + i, other.InvY + j));
            }
        int first = 0, rows = shape.Rows;
        if (!shape.Limited)
        {
            first = cells.Count == 0 ? 0 : cells.Max(c => c.Item2) / shape.PageHeight * shape.PageHeight;
            rows = first + shape.PageHeight;
        }
        for (int y = first; y + h <= rows; y++)
            for (int x = 0; x + w <= shape.Width; x++)
                if (shape.Contains(x, y, w, h) && Free(cells, x, y, w, h)) return true;
        return false;
    }

    /// <summary>Whether a creature's backpack has room for the item (matching stacks first, then a free place).</summary>
    public bool HasRoomFor(GameObject target, Item item) =>
        !Shape(target).Limited || target is not Creature || StackRoom(target, item) >= item.StackSize || FirstFit(target, item, ignore: item) != null;

    // ------------------------------------------------------------------ moving inside a grid

    /// <summary>
    /// Moves an item to a cell of a grid (its own holder's, or another one: a bag, an open container).
    /// Dropped onto a matching stack it merges; onto one item of the same footprint it swaps places with
    /// it (within the same grid); other occupied drops use the first free footprint.
    /// The requested footprint must still be inside the grid. Returns false when nothing moved.
    /// </summary>
    public bool MoveToCell(Item item, GameObject target, int x, int y, GameObject? actor = null)
    {
        EnsureLoaded();
        Load(target);
        if (item.IsDestroyed || !CanHold(target, item)) return false;
        var holder = Holder(item);
        var shape = Shape(target);
        var (w, h) = Footprint(item);
        if (!shape.Contains(x, y, w, h)) return false;
        int panel = PanelIn(target, item);
        var under = GridMates(target, item, panel, null)
            .Where(o => { var (ow, oh) = Footprint(o); return x < o.InvX + ow && o.InvX < x + w && y < o.InvY + oh && o.InvY < y + h; })
            .ToList();

        if (under.Count == 0)
        {
            if (holder == target && InventoryList(target)!.Contains(item)) { (item.InvX, item.InvY) = (x, y); _world.MarkChanged(target); Changed(target); return true; }
            return MoveTo(item, target, actor, merge: false, cell: (x, y)) != null;
        }
        var other = under.Count == 1 ? under[0] : null;
        // Onto a matching stack: top it up.
        if (other != null && CanStack(other, item))
        {
            int max = Rules.MaxStack(item.BaseItem);
            int moved = Math.Min(max - other.StackSize, item.StackSize);
            if (moved <= 0) return false;
            bool sameOwner = Owner(item) == (target is Item bag ? Owner(bag) ?? target : target);
            if (!sameOwner)
            {
                // From someone else's inventory: only whole stacks, through MoveTo for the acquire events.
                if (moved != item.StackSize) return false;
                return MoveTo(item, target, actor, merge: true) != null;
            }
            // NWNX_ON_ITEM_MERGE for a player putting one stack onto another; a skip keeps them apart.
            var ev = actor is Creature { IsPC: true } ? Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_ITEM_MERGE", actor)?.Add("ITEM_TO_MERGE_INTO", other).Add("ITEM_TO_MERGE", item) : null;
            if (ev?.Before() == true) { ev.After(); return false; }
            other.StackSize += moved;
            item.StackSize -= moved;
            if (item.StackSize == 0) { item.StackSize = moved; Detach(item); _world.Destroy(item); }
            _world.MarkChanged(target); Changed(target);
            ev?.After();
            return true;
        }
        // Onto one item of the same size in the same grid: swap them.
        if (other != null && holder == target && InventoryList(target)!.Contains(item) && Footprint(other) == (w, h) && other.InvX == x && other.InvY == y)
        {
            (other.InvX, other.InvY, item.InvX, item.InvY) = (item.InvX, item.InvY, x, y);
            _world.MarkChanged(target); Changed(target);
            return true;
        }
        if (FirstFit(target, item) is not { } fallback) return false;
        if (holder == target && InventoryList(target)!.Contains(item))
        {
            (item.InvX, item.InvY) = fallback;
            _world.MarkChanged(target); Changed(target);
            return true;
        }
        return MoveTo(item, target, actor, merge: false, cell: fallback) != null;
    }

    /// <summary>Repacks a grid: largest items first (then by base item and name), each at the first free place.</summary>
    public void AutoArrange(GameObject holder)
    {
        if (InventoryList(holder) is not { } list) return;
        var items = list.Where(i => !i.IsDestroyed).ToList();
        foreach (var i in items) i.InvX = i.InvY = -1;
        var tlk = _world.Resources.Talk;
        foreach (var item in items
            .OrderByDescending(i => { var (w, h) = Footprint(i); return w * h; })
            .ThenByDescending(i => Footprint(i).H)
            .ThenBy(i => i.BaseItem)
            .ThenBy(i => i.Name.Resolve(tlk), StringComparer.OrdinalIgnoreCase))
            if (FirstFit(holder, item) is { } cell) (item.InvX, item.InvY) = cell;
        _world.MarkChanged(holder);
        Changed(holder);
    }

    void InventoryFull(GameObject target)
    {
        if (target is Creature { IsPC: true } pc) _world.Output?.SendMessage(pc, "Your inventory is full.");
    }
}
