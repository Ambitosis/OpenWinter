using System.Text.Json.Nodes;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;

namespace OpenWinter.Game.Requests;

/// <summary>Client requests for inventory, stores and script-driven UI (NUI, GUI events, targeting).</summary>
static class BuiltinRequests
{
    // ----- inventory -----

    /// <summary>The player opened their own inventory panel.</summary>
    [ClientRequest("inventory")]
    static void Inventory(Request r)
    {
        if (r.Player.Player?.FamiliarMaster != null)
        {
            r.World.Output?.Notify("inventory-denied", r.Player, new { strref = 68708 }, r.Player);
            r.World.Output?.SendMessage(r.Player, r.World.Resources.TlkString(68708));
            return;
        }
        r.World.Output?.Notify("inventory-open", r.Player, r.World.Inventory.InventoryView(r.Player), r.Player);
    }

    [ClientRequest("equip")]
    static void Equip(Request r)
    {
        if (r.Obj<Item>("item") is not { } item || !r.Owns(item)) return;
        int slot = r.Int("slot", -1);
        if (slot < 0) slot = FirstSlot(r.World, item);
        if (slot < 0) return;
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, new EquipItemAction { ItemId = item.Id, Slot = slot, OffhandCommand = r.Bool("offhand") });
    }

    [ClientRequest("unequip")]
    static void Unequip(Request r)
    {
        if (r.Obj<Item>("item") is not { } item || !r.Owns(item)) return;
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, new UnequipItemAction { ItemId = item.Id });
    }

    [ClientRequest("drop")]
    static void Drop(Request r)
    {
        if (r.Obj<Item>("item") is not { } item || !r.Owns(item) || !item.Droppable || !ActionQueues.Accepts(r.Player)
            || !ItemDropDestination.Read(r, out var destination)) return;
        var drop = new PutDownItemAction { ItemId = item.Id, Destination = destination };
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, drop);
        if (destination is { } spot && r.World.Output is GameServer server) server.TrackOrder(r.Player, drop, spot.Position);
    }

    [ClientRequest("pickup")]
    static void PickUp(Request r)
    {
        if (r.Obj<Item>("item") is not { Area: not null } item || item.Area != r.Player.Area
            || item.Possessor != GameObject.InvalidId || !item.UseableOnGround || !ActionQueues.Accepts(r.Player)) return;
        int x = r.Int("x", -1), y = r.Int("y", -1);
        bool cellRequested = r.Element("x") != null || r.Element("y") != null;
        var footprint = r.World.Inventory.Footprint(item);
        if (cellRequested && !r.World.Inventory.Shape(r.Player).Contains(x, y, footprint.W, footprint.H)) return;
        ActionQueues.EnqueuePlayerTask(r.World, r.Player, new PickUpItemAction { ItemId = item.Id, Cell = x >= 0 ? (x, y) : null });
    }

    static int FirstSlot(World world, Item item)
    {
        int mask = world.Inventory.Rules.EquipableSlots(item.BaseItem);
        for (int i = 0; i < 18; i++) if ((mask & (1 << i)) != 0) return i;
        return -1;
    }

    // ----- stores -----

    static Store? OpenStore(Request r) => r.World.Inventory.SessionOf(r.Player)?.Store;

    [ClientRequest("store-buy")]
    static void StoreBuy(Request r)
    {
        if (OpenStore(r) is not { } s || r.Obj<Item>("item") is not { } item) return;
        (int, int)? cell = r.Int("x", -1) is >= 0 and var x && r.Int("y", -1) is >= 0 and var y ? (x, y) : null;
        r.World.Inventory.Buy(r.Player, s, item, cell);
    }

    [ClientRequest("store-sell")]
    static void StoreSell(Request r)
    {
        if (OpenStore(r) is { } s && r.Obj<Item>("item") is { } item && r.Owns(item)) r.World.Inventory.Sell(r.Player, s, item);
    }

    [ClientRequest("store-identify")]
    static void StoreIdentify(Request r)
    {
        if (OpenStore(r) is { } s && r.Obj<Item>("item") is { } item && r.Owns(item)) r.World.Inventory.Identify(r.Player, s, item);
    }

    [ClientRequest("store-close")]
    static void StoreClose(Request r) => r.World.Inventory.CloseStore(r.Player);

    // ----- modes -----

    /// <summary>
    /// An action mode on/off: {mode: ACTION_MODE_* (detect 0, stealth 1, parry, power attack, flurry, rapid shot,
    /// expertise, defensive casting, dirty fighting, ...), on}. Only modes the player qualifies for (see <see cref="AbilityRequests.SetMode"/>).
    /// </summary>
    [ClientRequest("action-mode")]
    static void ActionMode(Request r)
    {
        int mode = r.Int("mode", -1);
        if (mode >= 0) AbilityRequests.SetMode(r.World, r.Player, mode, r.Bool("on"));
    }

    // ----- death -----

    /// <summary>
    /// The Respawn button on the death panel: runs the module's OnPlayerRespawn script, for a living player too when a
    /// script showed it the panel (dying scene: ev_respawn on the living), but only while a panel with the button is up.
    /// </summary>
    [ClientRequest("respawn")]
    static void Respawn(Request r)
    {
        if (r.Player.Player is not { DeathPanelRespawn: true } session) return;
        session.DeathPanelRespawn = false;
        r.World.RunEvent(r.World.Module, "Mod_OnSpawnBtnDn", new Scripting.EventContext().Set("Respawner", r.Player.Id));
    }

    // ----- script-driven UI -----

    /// <summary>NUI window event: {token, type, element, index, payload}.</summary>
    [ClientRequest("nui-event")]
    static void NuiEvent(Request r)
    {
        var payload = r.Element("payload") is { } p ? Scripting.Functions.JsonFunctions.Parse(p.GetRawText()) : null;
        r.World.System<NuiService>().HandleEvent(r.Player, r.Int("token"), r.Str("type"), r.Str("element"), r.Int("index", -1), payload);
    }

    /// <summary>A NUI bind the player changed (text field, slider, check box): {token, bind, value}.</summary>
    [ClientRequest("nui-bind")]
    static void NuiBind(Request r) =>
        r.World.System<NuiService>().ClientSetBind(r.Player, r.Int("token"), r.Str("bind"), r.Element("value") is { } v ? JsonNode.Parse(v.GetRawText()) : null);

    /// <summary>A built-in GUI event (GUIEVENT_*): {type, integer, object, vector}.</summary>
    [ClientRequest("gui-event")]
    static void GuiEvent(Request r) =>
        PlayerEvents.GuiEvent(r.World, r.Player, r.Int("type"), r.Int("integer"), r.Id("object"), r.Vec("vector"));

    /// <summary>The player picked a target after EnterTargetingMode: {object, position}.</summary>
    [ClientRequest("target")]
    static void Target(Request r) => PlayerEvents.TargetSelected(r.World, r.Player, r.Id("object"), r.Vec("position"));
}
