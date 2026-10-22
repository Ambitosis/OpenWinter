using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;

namespace OpenWinter.Net.Nwn.Wire;

/// <summary>Decoded messages as JSON, with the protocol's field names, for recordings and diagnostics.</summary>
public static class NwnDump
{
    public static JsonNode? ToJson(object? v) => v switch
    {
        null => null,
        NwnTagged t => ToJson(t.Value),
        GameObjUpdateMessages.ObjListGuiEntry g => Typed($"Gui{g.Element}{(g.Op == '\0' ? "" : g.Op.ToString())}", g.Value),
        NwnLocString l => l.IsStrRef ? new JsonObject { ["strref"] = l.StrRef, ["gender"] = l.Gender } : new JsonObject { ["text"] = l.Text },
        Enum e => JsonValue.Create(e.ToString()),
        bool b => b,
        string s => s,
        byte[] bytes => Convert.ToHexString(bytes),
        float f => f,
        double d => d,
        byte or sbyte or short or ushort or int or uint or long or ulong => JsonValue.Create(Convert.ToDecimal(v)),
        ITuple tuple => new JsonArray(Enumerable.Range(0, tuple.Length).Select(i => ToJson(tuple[i])).ToArray()),
        IList list => new JsonArray(list.Cast<object?>().Select(ToJson).ToArray()),
        INwnStruct s => Typed(s.GetType().Name, s),
        _ => JsonValue.Create(v.ToString()),
    };

    static JsonObject Typed(string type, object value)
    {
        var o = new JsonObject { ["$"] = type };
        foreach (var p in value.GetType().GetProperties())
            if (p.GetCustomAttribute<NwnFieldAttribute>() is { } f) o[f.Name] = ToJson(p.GetValue(value));
        return o;
    }
}
