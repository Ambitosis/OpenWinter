using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>A player's open store window and the bonus mark up / mark down OpenStore gave it.</summary>
public sealed class StoreSession
{
    public required Store Store;
    public required Creature Player;
    public int BonusMarkUp, BonusMarkDown;
    public System.Numerics.Vector3 OpenPosition;
    public uint OpenArea;
}

/// <summary>Stores: pricing, buying, selling and identifying (server side of the store window).</summary>
public sealed partial class InventorySystem
{
    readonly Dictionary<uint, StoreSession> _storeSessions = new();

    public StoreSession? SessionOf(Creature pc)
    {
        if (!_storeSessions.TryGetValue(pc.Id, out var s)) return null;
        if (s.Store.IsDestroyed || pc.IsDestroyed || pc.Area?.Id != s.OpenArea
            || System.Numerics.Vector3.DistanceSquared(pc.Position, s.OpenPosition) > 4f)
        {
            CloseStore(pc);
            return null;
        }
        return s;
    }

    void CheckStoreSessions()
    {
        foreach (var session in _storeSessions.Values.ToArray()) SessionOf(session.Player);
    }

    (int Up, int Down) Bonus(Store s, Creature? pc) =>
        pc != null && SessionOf(pc) is { } session && session.Store == s ? (session.BonusMarkUp, session.BonusMarkDown) : (0, 0);

    /// <summary>OpenStore: runs the store's OnOpenStore and sends the player the stock and prices ("store-open").</summary>
    public void OpenStore(Store s, Creature pc, int bonusMarkUp, int bonusMarkDown) => OpenStore(s, pc, bonusMarkUp, bonusMarkDown, fromReply: false);

    void OpenStore(Store s, Creature pc, int bonusMarkUp, int bonusMarkDown, bool fromReply)
    {
        // From a reply's action script the store opens once the reply is done (a reply that ends the conversation
        // runs its end script first, store_reply_end), and the conversation goes on beside it (store_fromreply).
        if (!fromReply && _world.System<Conversations.ConversationService>().SessionOf(pc) is { InReply: true })
        {
            _world.After(0, () => { if (!s.IsDestroyed && !pc.IsDestroyed) OpenStore(s, pc, bonusMarkUp, bonusMarkDown, fromReply: true); });
            return;
        }
        EnsureLoaded();
        Load(s);
        var previous = SessionOf(pc);
        // Opening another store closes the first (its OnStoreClosed runs) and measures from here (storeswitch).
        if (previous != null && previous.Store != s) { CloseStore(pc); previous = null; }
        _storeSessions[pc.Id] = new StoreSession
        {
            Store = s, Player = pc,
            // Reopening an already-visible store does not move the native close boundary.
            OpenPosition = previous?.OpenPosition ?? pc.Position,
            OpenArea = previous?.OpenArea ?? pc.Area?.Id ?? GameObject.InvalidId,
            // Reopening it keeps the first opening's bonus mark-up and mark-down too (storeprices: OpenStore(s, pc, 20, -10)
            // and then OpenStore(s, pc) still bought at 170% and sold at 40%).
            BonusMarkUp = previous?.BonusMarkUp ?? Math.Clamp(bonusMarkUp, -100, 100),
            BonusMarkDown = previous?.BonusMarkDown ?? Math.Clamp(bonusMarkDown, -100, 100),
        };
        _world.RunEvent(s, "OnOpenStore", new EventContext().Set("OpenedBy", pc.Id));
        if (SessionOf(pc)?.Store != s) return; // the open script closed it again
        _world.Output?.Notify("store-open", s, StoreView(s, pc), pc);
        // A store opened over a conversation closes it, unless a reply's action opened it (convoend).
        if (!fromReply) _world.System<Conversations.ConversationService>().StoreOpened(pc);
    }

    /// <summary>The player closed the store window (called by the client handler).</summary>
    public void CloseStore(Creature pc)
    {
        if (!_storeSessions.Remove(pc.Id, out var session)) return;
        _world.RunEvent(session.Store, "OnStoreClosed", new EventContext().Set("ClosedBy", pc.Id));
        _world.Output?.Notify("store-close", session.Store, new { store = session.Store.Id }, pc);
    }

    void RecordStorePages(Store store)
    {
        foreach (var item in store.Inventory)
            if (!item.IsDestroyed && item.StorePanel is >= 0 and < 5 && item.InvY >= 0)
                store.CategoryPages[item.StorePanel] = Math.Max(store.CategoryPages[item.StorePanel],
                    (item.InvY + Footprint(item).H + StoreGrid.PageHeight - 1) / StoreGrid.PageHeight);
    }

    /// <summary>What the store window shows: stock with the player's buy prices, and the store's terms.</summary>
    public object StoreView(Store s, Creature pc)
    {
        var (up, down) = Bonus(s, pc);
        return new
        {
            store = s.Id,
            name = s.Name.Resolve(_world.Resources.Talk),
            markUp = s.MarkUp + up,
            markDown = s.MarkDown + down,
            gold = s.Gold,
            playerGold = pc.Gold,
            grid = new { width = StoreGrid.Width, pageHeight = StoreGrid.PageHeight, pages = StoreGrid.Pages },
            categoryPages = s.CategoryPages,
            maxBuyPrice = s.MaxBuyPrice,
            identifyCost = s.IdentifyPrice,
            blackMarket = s.BlackMarket,
            // What the store would pay for each of the player's unequipped items (-1: won't buy).
            offers = Items(pc).Where(i => SlotOf(pc, i) < 0).Select(i => new { id = i.Id, price = BuyPrice(s, i, pc) }).ToArray(),
            items = s.Inventory.Where(i => !i.IsDestroyed).Select(i => new
            {
                id = i.Id,
                name = i.Name.Resolve(_world.Resources.Talk),
                resref = i.ResRef,
                baseItem = i.BaseItem,
                stack = i.StackSize,
                panel = i.StorePanel >= 0 ? i.StorePanel : Rules.StorePanel(i.BaseItem),
                infinite = i.Infinite,
                identified = i.Identified,
                price = SellPrice(s, i, pc),
                icon = Icon(i, pc),
                x = i.InvX,
                y = i.InvY,
                info = ItemInfo(i, pc),
            }).ToArray(),
        };
    }

    /// <summary>What a player pays the store for an item: its value times the store's mark up (percent).</summary>
    public int SellPrice(Store s, Item item, Creature? pc = null)
    {
        Load(s);
        long value = Rules.GoldValue(item);
        long price = value * Math.Max(0, s.MarkUp + Bonus(s, pc).Up) / 100;
        return (int)Math.Clamp(price, value > 0 ? 1 : 0, int.MaxValue);
    }

    /// <summary>
    /// What the store pays a player for an item, or -1 if it won't buy it (plot items, filled containers,
    /// stolen goods outside a black market, WillNotBuy / WillOnlyBuy base items). Unidentified items are
    /// valued without their properties. Capped by MaxBuyPrice.
    /// </summary>
    public int BuyPrice(Store s, Item item, Creature? pc = null)
    {
        Load(s);
        if (item.Plot || item.Inventory.Count > 0) return -1;
        if (s.WillNotBuy.Contains(item.BaseItem)) return -1;
        if (s.WillOnlyBuy.Count > 0 && !s.WillOnlyBuy.Contains(item.BaseItem)) return -1;
        if (item.Stolen && !s.BlackMarket) return -1;
        double value = item.Identified
            ? Rules.GoldValue(item)
            : Rules.UnitValue(item, includeProperties: false) * Math.Max(1, item.StackSize);
        int markDown = (item.Stolen ? s.BlackMarketMarkDown : s.MarkDown) + Bonus(s, pc).Down;
        long price = (long)(value * Math.Max(0, markDown) / 100);
        // A creature's own limit for this store (NWNX_Creature_SetMaxSellToStorePriceOverride, -1: none) replaces the store's.
        int maxPrice = pc?.Overrides is { } o && o.MaxSellToStorePrice.TryGetValue(s.Id, out int own) ? own : s.MaxBuyPrice;
        if (maxPrice >= 0) price = Math.Min(price, maxPrice);
        return (int)Math.Clamp(price, 0, int.MaxValue);
    }

    /// <summary>The player buys an item from the store. Infinite stock is copied rather than moved.</summary>
    public bool Buy(Creature pc, Store s, Item item, (int X, int Y)? cell = null)
    {
        EnsureLoaded();
        Load(s);
        if (item.IsDestroyed || Owner(item) != s) return false;
        int price = SellPrice(s, item, pc);
        if (pc.Gold < price) return false;
        Item bought = item;
        if (item.Infinite)
        {
            bought = Copy(item, copyVars: true);
            bought.Infinite = false;
        }
        // Into the backpack cell it was dragged to when that is free, else the first place it fits.
        if (cell is { } at && !FitsAt(pc, bought, at.X, at.Y, ignore: bought)) cell = null;
        var received = MoveTo(bought, pc, actor: pc, source: s, cell: cell);
        if (received == null && !bought.IsDestroyed)
        {
            if (bought != item) _world.Destroy(bought);
            return false;
        }
        AddGold(pc, -price);
        if (s.Gold >= 0) s.Gold = (int)Math.Min(int.MaxValue, (long)s.Gold + price);
        TradeFeedback(pc, item, price, selling: false);
        StoreChanged(s);
        return true;
    }

    /// <summary>The player sells an item from their inventory (not equipped, not cursed) to the store.</summary>
    public bool Sell(Creature pc, Store s, Item item)
    {
        EnsureLoaded();
        Load(s);
        if (item.IsDestroyed || Owner(item) != pc || SlotOf(pc, item) >= 0 || item.Cursed) return false;
        int price = BuyPrice(s, item, pc);
        if (price < 0 || (s.Gold >= 0 && price > s.Gold)) return false;
        item.Infinite = false;
        if (MoveTo(item, s, actor: pc) == null) return false;
        AddGold(pc, price);
        if (s.Gold >= 0) s.Gold -= price;
        TradeFeedback(pc, item, price, selling: true);
        StoreChanged(s);
        return true;
    }

    void TradeFeedback(Creature pc, Item item, int price, bool selling)
    {
        if (price > 0) _world.Output?.SendMessage(pc, _world.Resources.TlkString(selling ? 62489u : 62490u)
            .Replace("<CUSTOM0>", price.ToString()));
        _world.Output?.SendMessage(pc, _world.Resources.TlkString(61618));
    }

    /// <summary>The store identifies one of the player's items for its IdentifyPrice.</summary>
    public bool Identify(Creature pc, Store s, Item item)
    {
        Load(s);
        if (item.Identified || s.IdentifyPrice < 0 || Owner(item) != pc || pc.Gold < s.IdentifyPrice) return false;
        AddGold(pc, -s.IdentifyPrice);
        item.Identified = true;
        Changed(item);
        StoreChanged(s); // the store's offer for it changes once its properties are known
        return true;
    }

    /// <summary>Refreshes the store window for everyone who has it open.</summary>
    void StoreChanged(Store s)
    {
        foreach (var session in _storeSessions.Values.Where(x => x.Store == s).ToList())
        {
            // A viewer who has walked out of range is closed here, not sent the window again.
            if (SessionOf(session.Player)?.Store != s) continue;
            _world.Output?.Notify("store-open", s, StoreView(s, session.Player), session.Player);
        }
    }
}
