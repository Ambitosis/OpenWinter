using OpenWinter.Game.Inventory;
using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Looting: a player who opens a container placeable (chest, barrel, body bag) sees its contents
/// ("container-open"), takes or puts items, and closes it again - explicitly, or by walking away.
/// Moving items fires the container's OnDisturbed and the player's acquire/unacquire events through
/// the inventory system.
/// </summary>
public sealed class ContainerService : IWorldSystem
{
    /// <summary>Maximum container reach, also enforced when the container itself moves.</summary>
    public const float ReachDistance = 3.5f;

    readonly World _world;
    readonly Dictionary<uint, uint> _open = new(); // player -> container
    readonly Dictionary<uint, Vector3> _openedAt = new(); // player's position when shown

    public ContainerService(World world) { _world = world; }

    public Placeable? OpenFor(Creature pc) => _open.TryGetValue(pc.Id, out var id) ? _world.Get<Placeable>(id) : null;

    /// <summary>Shows a container's contents to a player (called once the container has opened).</summary>
    public void Show(Creature pc, Placeable container)
    {
        if (!pc.IsPC) return;
        if (OpenFor(pc) is { } previous && previous != container) Close(pc);
        _open[pc.Id] = container.Id;
        _openedAt[pc.Id] = pc.Position;
        Send(pc, container);
    }

    void Send(Creature pc, Placeable container)
    {
        var inv = _world.Inventory;
        var talk = _world.Resources.Talk;
        _world.Output?.Notify("container-open", container, new
        {
            container = container.Id,
            name = container.Name.Resolve(talk),
            grid = Grid(inv.Shape(container)),
            items = inv.Items(container).Where(i => inv.Holder(i) == container).Select(i => new
            {
                id = i.Id, name = i.Name.Resolve(talk), baseItem = i.BaseItem, stack = i.StackSize, icon = inv.Icon(i, pc), identified = i.Identified,
                x = i.InvX, y = i.InvY, info = inv.ItemInfo(i, pc),
            }).ToArray(),
        }, pc);
    }

    /// <summary>Re-sends the open container's contents (after a move into or out of it).</summary>
    public void Refresh(Creature pc)
    {
        if (OpenFor(pc) is { } container) Send(pc, container);
    }

    /// <summary>Refresh every current viewer after an authoritative container mutation.</summary>
    public void RefreshViewers(Placeable container)
    {
        foreach (var pcId in _open.Where(pair => pair.Value == container.Id).Select(pair => pair.Key).ToArray())
            if (_world.Get<Creature>(pcId) is { IsPC: true, IsDestroyed: false } pc) Send(pc, container);
    }

    public void Close(Creature pc, bool notifyClient = true)
    {
        if (!_open.Remove(pc.Id, out var id)) return;
        _openedAt.Remove(pc.Id);
        if (_world.Get<Placeable>(id) is { } container)
        {
            // NWNX_ON_PLACEABLE_CLOSE, OBJECT_SELF the placeable (not skippable).
            var ev = Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_PLACEABLE_CLOSE", container)?.Add("OBJECT", pc);
            ev?.Before();
            using var after = new Nwnx.NwnxEvent.AfterScope(ev);
            // The last looter closing it shuts the lid (OnClosed).
            if (!_open.ContainsValue(id) && DoorService.IsOpen(container)) DoorService.Close(_world, container, pc);
            if (notifyClient) _world.Output?.Notify("container-close", container, new { container = id }, pc);
            if (!_open.ContainsValue(id)) _world.System<Rules.CorpseSystem>().Closed(container);
        }
    }

    public void Step(float dt)
    {
        if (_open.Count == 0) return;
        foreach (var (pcId, containerId) in _open.ToList())
        {
            var pc = _world.Get<Creature>(pcId);
            var container = _world.Get<Placeable>(containerId);
            if (pc == null || pc.IsDestroyed) { _open.Remove(pcId); _openedAt.Remove(pcId); continue; }
            if (container == null || container.IsDestroyed || container.Area != pc.Area || pc.IsDead
                || _openedAt.TryGetValue(pcId, out var openedAt) && pc.Position != openedAt
                || Vector2.Distance(new(pc.Position.X, pc.Position.Y), new(container.Position.X, container.Position.Y)) > ReachDistance)
                Close(pc);
        }
    }

    /// <summary>A grid's shape for the client: columns, rows per page, pages (0 = unlimited).</summary>
    public static object Grid(GridShape g) => new { width = g.Width, pageHeight = g.PageHeight, pages = g.Pages };

    static (int, int)? Cell(Request r) => r.Int("x", -1) is >= 0 and var x && r.Int("y", -1) is >= 0 and var y ? (x, y) : null;

    // ----- client requests -----

    [ClientRequest("container-take")]
    static void Take(Request r)
    {
        var self = r.World.System<ContainerService>();
        if (self.OpenFor(r.Player) is not { } container || r.Obj<Item>("item") is not { } item) return;
        if (r.World.Inventory.Holder(item) != container) return;
        // Into a chosen backpack cell ({x, y}: dragged there), else wherever it fits.
        if (Cell(r) is { } cell) r.World.Inventory.MoveToCell(item, r.Player, cell.Item1, cell.Item2, actor: r.Player);
        else r.World.Inventory.MoveTo(item, r.Player, actor: r.Player, source: container);
    }

    /// <summary>Takes everything (the loot window's "Take all").</summary>
    [ClientRequest("container-take-all")]
    static void TakeAll(Request r)
    {
        var self = r.World.System<ContainerService>();
        if (self.OpenFor(r.Player) is not { } container) return;
        foreach (var item in r.World.Inventory.Items(container).ToList())
            r.World.Inventory.MoveTo(item, r.Player, actor: r.Player, source: container);
    }

    [ClientRequest("container-put")]
    static void Put(Request r)
    {
        var self = r.World.System<ContainerService>();
        if (self.OpenFor(r.Player) is not { } container || r.Obj<Item>("item") is not { } item) return;
        if (!r.Owns(item) || r.Player.Equipment.ContainsValue(item)) return;
        if (Cell(r) is { } cell) r.World.Inventory.MoveToCell(item, container, cell.Item1, cell.Item2, actor: r.Player);
        else r.World.Inventory.MoveTo(item, container, actor: r.Player, source: r.Player);
    }

    [ClientRequest("container-close")]
    static void CloseRequest(Request r) => r.World.System<ContainerService>().Close(r.Player, notifyClient: false);
}
