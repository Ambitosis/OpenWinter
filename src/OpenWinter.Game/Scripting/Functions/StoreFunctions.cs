using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Stores: opening the store window and the store's gold and prices.</summary>
static class StoreFunctions
{
    const int NotAStore = -2;

    static Store? StoreArg(Call c, int i)
    {
        var s = c.Obj<Store>(i);
        if (s != null) { c.World.Inventory.EnsureLoaded(); c.World.Inventory.Load(s); }
        return s;
    }

    [NwFunc("OpenStore")] static object? Open(Call c)
    {
        if (StoreArg(c, 0) is { } s && c.Obj<Creature>(1) is { } pc) c.World.Inventory.OpenStore(s, pc, c.I(2), c.I(3));
        return null;
    }

    [NwFunc("GetStoreGold")] static object? Gold(Call c) => StoreArg(c, 0)?.Gold ?? NotAStore;
    [NwFunc("SetStoreGold")] static object? SetGold(Call c) { if (StoreArg(c, 0) is { } s) s.Gold = Math.Max(-1, c.I(1)); return null; }
    [NwFunc("GetStoreMaxBuyPrice")] static object? MaxBuy(Call c) => StoreArg(c, 0)?.MaxBuyPrice ?? NotAStore;
    [NwFunc("SetStoreMaxBuyPrice")] static object? SetMaxBuy(Call c) { if (StoreArg(c, 0) is { } s) s.MaxBuyPrice = Math.Max(-1, c.I(1)); return null; }
    [NwFunc("GetStoreIdentifyCost")] static object? IdentifyCost(Call c) => StoreArg(c, 0)?.IdentifyPrice ?? NotAStore;
    [NwFunc("SetStoreIdentifyCost")] static object? SetIdentifyCost(Call c) { if (StoreArg(c, 0) is { } s) s.IdentifyPrice = Math.Max(-1, c.I(1)); return null; }
}
