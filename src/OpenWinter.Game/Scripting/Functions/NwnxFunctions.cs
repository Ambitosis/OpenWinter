using OpenWinter.Game.Nwnx;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>EE's native NWNX interface: a typed argument stack and NWNXCall, served by <see cref="NwnxRegistry"/>.</summary>
static class NwnxFunctions
{
    static NwnxRegistry Nwnx(Call c) => c.World.System<NwnxRegistry>();

    static object? Push(Call c, NwnxType type, object? value)
    {
        Nwnx(c).Stack.Push(new NwnxValue(type, value));
        return null;
    }

    static object Pop(Call c, NwnxType type, NssType nss) => Nwnx(c).PopAs(type) ?? c.Host.DefaultEngineValue(nss);

    [NwFunc("NWNXGetIsAvailable")] static object? Available(Call c) => Nwnx(c).Available ? 1 : 0;
    [NwFunc("NWNXCall")]
    static object? NwnxCall(Call c)
    {
        Nwnx(c).Call(c.S(0), c.S(1), c.Host, c.Thread);
        return null;
    }

    [NwFunc("NWNXPushInt")] static object? PushInt(Call c) => Push(c, NwnxType.Int, c.I(0));
    [NwFunc("NWNXPushFloat")] static object? PushFloat(Call c) => Push(c, NwnxType.Float, c.F(0));
    [NwFunc("NWNXPushObject")] static object? PushObject(Call c) => Push(c, NwnxType.Object, c.O(0));
    [NwFunc("NWNXPushString")] static object? PushString(Call c) => Push(c, NwnxType.String, c.S(0));
    [NwFunc("NWNXPushVector")] static object? PushVector(Call c) => Push(c, NwnxType.Vector, c.V(0));
    [NwFunc("NWNXPushLocation")] static object? PushLocation(Call c) => Push(c, NwnxType.Location, c.L(0));
    [NwFunc("NWNXPushEffect")] static object? PushEffect(Call c) => Push(c, NwnxType.Effect, c.E(0));
    [NwFunc("NWNXPushJson")] static object? PushJson(Call c) => Push(c, NwnxType.Json, c.J(0));
    [NwFunc("NWNXPushAction")] static object? PushAction(Call c) => Push(c, NwnxType.Action, c.A(0));
    [NwFunc("NWNXPushEvent")] static object? PushEvent(Call c) => Push(c, NwnxType.Event, c.Ev(0));
    [NwFunc("NWNXPushTalent")] static object? PushTalent(Call c) => Push(c, NwnxType.Talent, c.T(0));
    [NwFunc("NWNXPushSqlquery")] static object? PushSql(Call c) => Push(c, NwnxType.SqlQuery, c.Q(0));
    [NwFunc("NWNXPushCassowary")] static object? PushCassowary(Call c) => Push(c, NwnxType.Cassowary, c.C(0));

    [NwFunc("NWNXPopInt")] static object? PopInt(Call c) => Nwnx(c).PopAs(NwnxType.Int);
    [NwFunc("NWNXPopFloat")] static object? PopFloat(Call c) => Nwnx(c).PopAs(NwnxType.Float);
    [NwFunc("NWNXPopObject")] static object? PopObject(Call c) => Nwnx(c).PopAs(NwnxType.Object);
    [NwFunc("NWNXPopString")] static object? PopString(Call c) => Nwnx(c).PopAs(NwnxType.String);
    [NwFunc("NWNXPopVector")] static object? PopVector(Call c) => Nwnx(c).PopAs(NwnxType.Vector);
    [NwFunc("NWNXPopLocation")] static object? PopLocation(Call c) => Nwnx(c).PopAs(NwnxType.Location);
    [NwFunc("NWNXPopEffect")] static object? PopEffect(Call c) => Pop(c, NwnxType.Effect, NssType.Effect);
    [NwFunc("NWNXPopJson")] static object? PopJson(Call c) => Pop(c, NwnxType.Json, NssType.Json);
    [NwFunc("NWNXPopEvent")] static object? PopEvent(Call c) => Pop(c, NwnxType.Event, NssType.Event);
    [NwFunc("NWNXPopTalent")] static object? PopTalent(Call c) => Pop(c, NwnxType.Talent, NssType.Talent);
    [NwFunc("NWNXPopSqlquery")] static object? PopSql(Call c) => Pop(c, NwnxType.SqlQuery, NssType.SqlQuery);
    [NwFunc("NWNXPopCassowary")] static object? PopCassowary(Call c) => Pop(c, NwnxType.Cassowary, NssType.Cassowary);
}
