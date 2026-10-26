using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Client notices that only matter to NWNX_Events subscribers: windows closing, the inventory panel changing,
/// device properties, exporting the character, force-moving to an object and timing bars. Each raises its event's
/// _BEFORE and _AFTER halves as nwnx_events.nss documents them.
/// </summary>
static class NwnxClientRequests
{
    static void Raise(Request r, string name, Action<NwnxEvent>? data = null, Action? work = null)
    {
        var ev = NwnxEvent.Begin(r.World, name, r.Player);
        if (ev != null) data?.Invoke(ev);
        if (ev?.Before() != true) work?.Invoke();
        ev?.After();
    }

    /// <summary>{target}: the player closed an inventory window (their own unless a target is named).</summary>
    [ClientRequest("inventory-close")]
    static void InventoryClose(Request r) =>
        Raise(r, "NWNX_ON_INVENTORY_CLOSE", ev => ev.Add("TARGET_INVENTORY", r.World.Get(r.Id("target")) ?? r.Player));

    /// <summary>{current, selected}: the player switched inventory panels.</summary>
    [ClientRequest("inventory-panel")]
    static void InventoryPanel(Request r) =>
        Raise(r, "NWNX_ON_INVENTORY_SELECT_PANEL", ev => ev.Add("CURRENT_PANEL", r.Int("current")).Add("SELECTED_PANEL", r.Int("selected")));

    /// <summary>{item, open}: a bag's own inventory window opened or closed (OBJECT_SELF the bag).</summary>
    [ClientRequest("item-inventory")]
    static void ItemInventory(Request r)
    {
        if (r.Obj<Item>("item") is not { } bag || !r.Owns(bag)) return;
        var ev = NwnxEvent.Begin(r.World, r.Bool("open") ? "NWNX_ON_ITEM_INVENTORY_OPEN" : "NWNX_ON_ITEM_INVENTORY_CLOSE", bag)?.Add("OWNER", r.Player);
        ev?.Before();
        ev?.After();
    }

    [ClientRequest("journal-close")]
    static void JournalClose(Request r) => Raise(r, "NWNX_ON_JOURNAL_CLOSE");

    [ClientRequest("character-close")]
    static void CharacterClose(Request r) => Raise(r, "NWNX_ON_CHARACTER_SHEET_CLOSE", ev => ev.Add("TARGET", r.Player));

    /// <summary>{property, old, new}: the client's window size or GUI scale changed (PLAYER_DEVICE_PROPERTY_GUI_*).</summary>
    [ClientRequest("device-property")]
    static void DeviceProperty(Request r) =>
        Raise(r, "NWNX_ON_CLIENT_SET_DEVICE_PROPERTY", ev => ev.Add("PROPERTY", r.Str("property")).Add("OLD_VALUE", r.Int("old")).Add("NEW_VALUE", r.Int("new")));

    /// <summary>The options menu's Save Character (the client keeps the copy it saves).</summary>
    [ClientRequest("export-character")]
    static void ExportCharacter(Request r) => Raise(r, "NWNX_ON_CLIENT_EXPORT_CHARACTER");

    /// <summary>{target}: walk to an object whatever lies in the way.</summary>
    [ClientRequest("force-move")]
    static void ForceMove(Request r)
    {
        if (r.World.Get(r.Id("target")) is not { } t || t.Area != r.Player.Area || !ActionQueues.Accepts(r.Player)) return;
        Raise(r, "NWNX_ON_INPUT_FORCE_MOVE_TO_OBJECT", ev => ev.Add("TARGET", t), () =>
        {
            ActionQueues.Clear(r.World, r.Player);
            r.Player.Actions.Add(new MoveAction { TargetObject = t.Id, Range = 1f, Run = true });
        });
    }

    /// <summary>The timing bar the client shows ended: {cancelled}.</summary>
    [ClientRequest("timing-bar-end")]
    static void TimingBarEnd(Request r) => NwnxTimingBar.Ended(r.World, r.Player, r.Bool("cancelled"));
}
