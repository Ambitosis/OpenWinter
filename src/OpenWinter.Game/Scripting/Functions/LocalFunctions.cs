using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Get/Set/DeleteLocal* and the EE local-variable enumeration functions.</summary>
static class LocalFunctions
{
    static LocalVariables? Vars(Call c, int i) => c.Obj(i)?.Locals;

    // Legacy NWNX (ABIv2) passes arguments through locals on OBJECT_INVALID named NWNXEE!ABIv2!X!Y!PUSH/POP.
    static bool NwnxPop(Call c, Nwnx.NwnxType type, out object? value)
    {
        value = null;
        return c.Args.Length > 1 && c.Args[0] is uint o && o == GameObject.InvalidId
            && c.World.System<Nwnx.NwnxRegistry>().TryLegacyPop(o, c.S(1), type, out value);
    }
    static bool NwnxPush(Call c, Nwnx.NwnxValue value) =>
        c.Args.Length > 1 && c.Args[0] is uint o && o == GameObject.InvalidId
        && c.World.System<Nwnx.NwnxRegistry>().TryLegacyPush(o, c.S(1), value);

    [NwFunc("GetLocalInt")] static object? GetInt(Call c) => NwnxPop(c, Nwnx.NwnxType.Int, out var n) ? n : Vars(c, 0)?.Ints.GetValueOrDefault(c.S(1)) ?? 0;
    [NwFunc("GetLocalFloat")] static object? GetFloat(Call c) => NwnxPop(c, Nwnx.NwnxType.Float, out var n) ? n : Vars(c, 0)?.Floats.GetValueOrDefault(c.S(1)) ?? 0f;
    [NwFunc("GetLocalString")] static object? GetString(Call c) => NwnxPop(c, Nwnx.NwnxType.String, out var n) ? n : Vars(c, 0)?.Strings.GetValueOrDefault(c.S(1)) ?? "";
    /// <summary>The stored id even once that object is gone (it is then simply not valid).</summary>
    [NwFunc("GetLocalObject")]
    static object? GetObject(Call c) =>
        NwnxPop(c, Nwnx.NwnxType.Object, out var n) ? n : Vars(c, 0)?.Objects.GetValueOrDefault(c.S(1), GameObject.InvalidId) ?? GameObject.InvalidId;
    [NwFunc("GetLocalLocation")] static object? GetLocation(Call c) => NwnxPop(c, Nwnx.NwnxType.Location, out var n) ? n : Vars(c, 0)?.Locations.GetValueOrDefault(c.S(1), Location.Invalid) ?? Location.Invalid;
    [NwFunc("GetLocalJson")]
    static object? GetJson(Call c) =>
        NwnxPop(c, Nwnx.NwnxType.Json, out var n) ? n ?? JsonValue.Null : Vars(c, 0)?.Jsons.GetValue(c.S(1)) ?? JsonValue.Null;

    [NwFunc("SetLocalInt")] static object? SetInt(Call c) { if (NwnxPush(c, Nwnx.NwnxValue.Int(c.I(2)))) return null; if (Vars(c, 0) is { } v) v.Ints[c.S(1)] = c.I(2); return null; }
    [NwFunc("SetLocalFloat")] static object? SetFloat(Call c) { if (NwnxPush(c, Nwnx.NwnxValue.Float(c.F(2)))) return null; if (Vars(c, 0) is { } v) { v.Floats[c.S(1)] = c.F(2) == 0 ? 0f : c.F(2); } return null; } // -0 reads back as 0
    [NwFunc("SetLocalString")] static object? SetString(Call c) { if (NwnxPush(c, Nwnx.NwnxValue.String(c.S(2)))) return null; if (Vars(c, 0) is { } v) v.Strings[c.S(1)] = c.S(2); return null; }
    [NwFunc("SetLocalObject")] static object? SetObject(Call c) { if (NwnxPush(c, Nwnx.NwnxValue.Object(c.O(2)))) return null; if (Vars(c, 0) is { } v) v.Objects[c.S(1)] = c.O(2); return null; }
    [NwFunc("SetLocalLocation")] static object? SetLocation(Call c) { if (NwnxPush(c, new Nwnx.NwnxValue(Nwnx.NwnxType.Location, c.L(2)))) return null; if (Vars(c, 0) is { } v) v.Locations[c.S(1)] = c.L(2); return null; }
    [NwFunc("SetLocalJson")] static object? SetJson(Call c) { if (NwnxPush(c, new Nwnx.NwnxValue(Nwnx.NwnxType.Json, c.J(2)))) return null; if (Vars(c, 0) is { } v) v.Jsons.SetValue(c.S(1), c.J(2)); return null; } // shared, not copied

    [NwFunc("DeleteLocalInt")] static object? DelInt(Call c) { Vars(c, 0)?.Ints.Remove(c.S(1)); return null; }
    [NwFunc("DeleteLocalFloat")] static object? DelFloat(Call c) { Vars(c, 0)?.Floats.Remove(c.S(1)); return null; }
    [NwFunc("DeleteLocalString")] static object? DelString(Call c) { Vars(c, 0)?.Strings.Remove(c.S(1)); return null; }
    [NwFunc("DeleteLocalObject")] static object? DelObject(Call c) { Vars(c, 0)?.Objects.Remove(c.S(1)); return null; }
    [NwFunc("DeleteLocalLocation")] static object? DelLocation(Call c) { Vars(c, 0)?.Locations.Remove(c.S(1)); return null; }
    [NwFunc("DeleteLocalJson")] static object? DelJson(Call c) { Vars(c, 0)?.Jsons.Remove(c.S(1)); return null; }

    // EE: enumerate locals (GetLocalVariableCount / GetLocalVariableByPosition use the "struct" helpers in nw_inc_*).
    [NwFunc("GetLocalVariableCount")]
    static object? Count(Call c)
    {
        var v = Vars(c, 0);
        return v == null ? 0 : v.Ints.Count + v.Floats.Count + v.Strings.Count + v.Objects.Count + v.Locations.Count + v.Jsons.Count;
    }
}
