using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Store: a store's prices, black market and restrictions (parity suite nwnx_store).</summary>
static class StorePlugin
{
    const string P = "Store";

    static void Get(NwnxCall n, Func<Store, int> value) => n.Push(n.PopObject() is Store s ? value(s) : -1);

    static void Set(NwnxCall n, Action<Store, int> change)
    {
        var s = n.PopObject<Store>();
        int value = n.PopInt();
        if (s == null) return;
        change(s, value);
        n.World.MarkChanged(s);
    }

    /// <summary>TRUE when the store will not buy that base item (WillNotBuy, or outside a WillOnlyBuy list).</summary>
    [NwnxFunc(P, "GetIsRestrictedBuyItem")]
    static void IsRestricted(NwnxCall n)
    {
        var s = n.PopObject<Store>();
        int baseItem = n.PopInt();
        if (s == null) { n.Push(-1); return; }
        n.Push(s.WillNotBuy.Contains(baseItem) || s.WillOnlyBuy.Count > 0 && !s.WillOnlyBuy.Contains(baseItem));
    }

    [NwnxFunc(P, "GetBlackMarketMarkDown")] static void GetBlackMarketMarkDown(NwnxCall n) => Get(n, s => s.BlackMarketMarkDown);
    [NwnxFunc(P, "SetBlackMarketMarkDown")] static void SetBlackMarketMarkDown(NwnxCall n) => Set(n, (s, v) => s.BlackMarketMarkDown = v);
    [NwnxFunc(P, "GetMarkDown")] static void GetMarkDown(NwnxCall n) => Get(n, s => s.MarkDown);
    [NwnxFunc(P, "SetMarkDown")] static void SetMarkDown(NwnxCall n) => Set(n, (s, v) => s.MarkDown = v);
    [NwnxFunc(P, "GetMarkUp")] static void GetMarkUp(NwnxCall n) => Get(n, s => s.MarkUp);
    [NwnxFunc(P, "SetMarkUp")] static void SetMarkUp(NwnxCall n) => Set(n, (s, v) => s.MarkUp = v);

    // The original keeps the value it was given (5 reads back 5; nwnx_store).
    const string BlackMarketKey = "store:blackMarket";

    [NwnxFunc(P, "GetBlackMarket")]
    static void GetBlackMarket(NwnxCall n) =>
        Get(n, s => s.BlackMarket && n.Registry.HasState(s.Id) && n.State(s).TryGetValue(BlackMarketKey, out var v) && v.Value is int raw && raw != 0 ? raw : s.BlackMarket ? 1 : 0);

    [NwnxFunc(P, "SetBlackMarket")]
    static void SetBlackMarket(NwnxCall n) => Set(n, (s, v) =>
    {
        s.BlackMarket = v != 0;
        n.State(s)[BlackMarketKey] = NwnxValue.Int(v);
    });

    /// <summary>Players with the store open.</summary>
    [NwnxFunc(P, "GetCurrentCustomersCount")]
    static void Customers(NwnxCall n) => Get(n, s => n.World.Inventory.CustomerCount(s));
}
