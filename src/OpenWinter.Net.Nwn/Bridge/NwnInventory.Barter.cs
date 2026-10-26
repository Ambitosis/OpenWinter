using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// Barter between two players. Asking starts it at once for both (the server asks the other player nothing); each
/// player's offer arrives as the object list's 'B' entries (their own) and 'A' entries (the other's), and locking,
/// accepting and closing as the barter messages, which name the side by whether it is the one who asked. The client
/// sees the session as "barter-state" and "barter-close".
/// </summary>
public sealed partial class NwnInventory
{
    const int BaseGold = 76;

    sealed class BarterSide
    {
        public uint Player;
        public readonly Dictionary<uint, Held> Items = [];
        public bool Locked, Accepted;
    }

    BarterSide? _mine, _theirs;
    bool _asked;

    void BarterRequest(string kind, Func<string, uint> id, Func<string, int, int> number)
    {
        var send = host.ToServer;
        switch (kind)
        {
            case "barter-request":
                send(new BarterMessages.BarterRqstStartBarter { Target = id("target"), Item = InvalidObjectId });
                break;
            case "barter-offer":
                send(new BarterMessages.BarterRqstAddItem { Item = id("item"), X = 255, Y = 255 });
                break;
            case "barter-withdraw":
                send(new BarterMessages.BarterRqstRemoveItem { Item = id("item"), X = 255, Y = 255 });
                break;
            case "barter-gold":
            {
                // The offer holds the gold the player names: the piles offered come back out and that much goes in.
                int wanted = Math.Max(0, number("gold", 0));
                if (_mine == null || wanted == Gold(_mine)) break;
                foreach (var pile in _mine.Items.Values.Where(h => h.Look.BaseItemId == BaseGold).ToList())
                    send(new BarterMessages.BarterRqstRemoveItem { Item = pile.Id, X = 255, Y = 255 });
                if (wanted > 0) send(new GoldMessages.GoldTransferGold { ObjectId = Pc, Amount = wanted, Barter = true, XPos = 255, YPos = 255 });
                break;
            }
            case "barter-answer":
                // The server starts a barter for both players at once and asks the other nothing: no answer to give.
                break;
            case "barter-lock":
                send(new BarterMessages.BarterRqstLockList { Locked = true });
                break;
            case "barter-accept":
                send(new BarterMessages.BarterRqstAcceptTrade { Accept = true });
                break;
            case "barter-cancel":
                send(new BarterMessages.BarterRqstCloseBarter { Close = true });
                break;
        }
    }

    /// <summary>An offer's entries: 'B' the player's own, 'A' the other player's.</summary>
    bool Barter(ObjListGuiEntry gui)
    {
        var side = gui.Element switch { 'B' => _mine, 'A' => _theirs, _ => null };
        if (gui.Element is not ('B' or 'A')) return false;
        if (side == null) return true;
        switch (gui.Value)
        {
            case GuiRepositoryItemAdd add:
                side.Items[add.ItemId] = new Held(add.ItemId, add.Item) { X = add.X, Y = add.Y, Holder = side.Player };
                break;
            case GuiRepositoryItemDelete del:
                side.Items.Remove(del.ItemId);
                break;
            case GuiRepositoryItemUpdate u when side.Items.TryGetValue(u.ItemId, out var held):
                Restack(held, u.StackSize, u.Cost);
                break;
        }
        // A changed offer takes back both acceptances.
        _mine!.Accepted = _theirs!.Accepted = false;
        BarterState();
        return true;
    }

    void Barter(NwnMessage message)
    {
        switch (message)
        {
            case BarterMessages.BarterAcptStartBarter start:
                _asked = start.InitiatorCreature == Pc;
                _mine = new BarterSide { Player = Pc };
                _theirs = new BarterSide { Player = _asked ? start.RecipientCreature : start.InitiatorCreature };
                BarterState();
                break;
            case BarterMessages.BarterAcptLockList locked when _mine != null:
                (locked.Initiator == _asked ? _mine : _theirs!).Locked = locked.Locked;
                BarterState();
                break;
            case BarterMessages.BarterAcptAcceptTrade accepted when _mine != null:
                (accepted.Initiator == _asked ? _mine : _theirs!).Accepted = accepted.Accept;
                BarterState();
                break;
            case BarterMessages.BarterAcptCloseBarter close:
                EndBarter(cancelled: !close.Accepted);
                Want();
                break;
        }
    }

    void EndBarter(bool cancelled)
    {
        if (_mine != null) Notify("barter-close", new JsonObject { ["cancelled"] = cancelled }, Pc);
        _mine = _theirs = null;
        _bagsWanted = true;
    }

    static int Gold(BarterSide? side) =>
        side == null ? 0 : (int)Math.Min(side.Items.Values.Where(h => h.Look.BaseItemId == BaseGold).Sum(h => (long)h.Look.StackSize), int.MaxValue);

    void BarterState()
    {
        if (_mine == null || _theirs == null) return;
        JsonObject Side(BarterSide side)
        {
            var items = new JsonArray();
            foreach (var h in side.Items.Values.Where(h => h.Look.BaseItemId != BaseGold).OrderBy(h => h.Y).ThenBy(h => h.X)) items.Add(Entry(h));
            return new JsonObject
            {
                ["id"] = side.Player, ["name"] = host.NameOf(side.Player), ["portrait"] = "", ["gold"] = Gold(side),
                ["offered"] = side.Locked, ["accepted"] = side.Accepted, ["items"] = items,
            };
        }
        Notify("barter-state", new JsonObject { ["mine"] = Side(_mine), ["theirs"] = Side(_theirs) }, Pc);
    }
}
