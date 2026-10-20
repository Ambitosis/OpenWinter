using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>EE NUI: server-driven windows, binds and events (see <see cref="NuiService"/>).</summary>
static class NuiFunctions
{
    static NuiService Nui(Call c) => c.World.System<NuiService>();
    static Creature? Pc(Call c, int i) => c.Obj<Creature>(i) is { IsPC: true } pc ? pc : null;

    [NwFunc("NuiCreate")]
    static object? Create(Call c) =>
        Pc(c, 0) is { } pc ? Nui(c).Create(pc, c.J(1).Node, "", c.S(2), c.S(3)) : 0;
    [NwFunc("NuiCreateFromResRef")]
    static object? CreateFromResRef(Call c) =>
        Pc(c, 0) is { } pc && c.S(1).Length > 0 ? Nui(c).Create(pc, null, c.S(1), c.S(2), c.S(3)) : 0;
    [NwFunc("NuiDestroy")]
    static object? Destroy(Call c)
    {
        if (Pc(c, 0) is { } pc) Nui(c).Destroy(pc, c.I(1));
        return null;
    }
    [NwFunc("NuiFindWindow")] static object? FindWindow(Call c) => Pc(c, 0) is { } pc ? Nui(c).Find(pc, c.S(1))?.Token ?? 0 : 0;
    [NwFunc("NuiGetWindowId")] static object? WindowId(Call c) => Nui(c).Get(Pc(c, 0), c.I(1))?.Id ?? "";
    [NwFunc("NuiGetNthWindow")]
    static object? NthWindow(Call c)
    {
        if (Pc(c, 0) is not { } pc) return 0;
        var list = Nui(c).Windows(pc);
        int n = c.I(1);
        return n >= 0 && n < list.Count ? list[n].Token : 0;
    }

    [NwFunc("NuiGetBind")]
    static object? GetBind(Call c) =>
        Pc(c, 0) is { } pc ? new JsonValue(Nui(c).GetBind(pc, c.I(1), c.S(2))) : JsonValue.Null;
    [NwFunc("NuiSetBind")]
    static object? SetBind(Call c)
    {
        if (Pc(c, 0) is { } pc) Nui(c).SetBind(pc, c.I(1), c.S(2), c.J(3).Node);
        return null;
    }
    [NwFunc("NuiSetBindWatch")]
    static object? SetBindWatch(Call c) =>
        Pc(c, 0) is { } pc && Nui(c).SetWatch(pc, c.I(1), c.S(2), c.I(3) != 0) ? 1 : 0;
    [NwFunc("NuiGetNthBind")]
    static object? NthBind(Call c) =>
        Pc(c, 0) is { } pc ? Nui(c).NthBind(pc, c.I(1), c.I(2) != 0, c.I(3)) : "";
    [NwFunc("NuiSetGroupLayout")]
    static object? SetGroupLayout(Call c)
    {
        if (Pc(c, 0) is { } pc) Nui(c).SetGroupLayout(pc, c.I(1), c.S(2), c.J(3).Node);
        return null;
    }
    [NwFunc("NuiGetUserData")]
    static object? GetUserData(Call c) =>
        Nui(c).Get(Pc(c, 0), c.I(1)) is { } w ? new JsonValue(w.UserData?.DeepClone()) : JsonValue.Null;
    [NwFunc("NuiSetUserData")]
    static object? SetUserData(Call c)
    {
        if (Nui(c).Get(Pc(c, 0), c.I(1)) is { } w) w.UserData = c.J(2).Node?.DeepClone();
        return null;
    }

    [NwFunc("NuiGetEventPlayer")] static object? EventPlayer(Call c) => c.Event.Obj(NuiService.KeyPlayer);
    [NwFunc("NuiGetEventWindow")] static object? EventWindow(Call c) => c.Event.Int(NuiService.KeyToken);
    [NwFunc("NuiGetEventType")] static object? EventType(Call c) => c.Event.Str(NuiService.KeyType);
    [NwFunc("NuiGetEventElement")] static object? EventElement(Call c) => c.Event.Str(NuiService.KeyElement);
    [NwFunc("NuiGetEventArrayIndex")]
    static object? EventArrayIndex(Call c) =>
        c.Event.Values.TryGetValue(NuiService.KeyArrayIndex, out var v) && v is int i ? i : -1;
    [NwFunc("NuiGetEventPayload")]
    static object? EventPayload(Call c) =>
        c.Event.Values.TryGetValue(NuiService.KeyPayload, out var v) && v is JsonValue j ? new JsonValue(j.Node?.DeepClone()) : JsonValue.Null;
}
