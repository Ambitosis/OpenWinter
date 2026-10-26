using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using JsonValue = OpenWinter.Game.Scripting.JsonValue;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Data: the old native array store (arrays per object, element type and tag).</summary>
static class DataPlugin
{
    const string P = "Data";
    const int TypeFloat = 0, TypeInt = 1, TypeObject = 2, TypeString = 3;

    static List<NwnxValue> Array(NwnxCall n, int type, uint obj, string tag)
    {
        var state = n.State(obj);
        var key = $"array:{type}:{tag}";
        if (!state.TryGetValue(key, out var v) || v.Value is not List<NwnxValue> list)
        {
            list = new List<NwnxValue>();
            state[key] = new NwnxValue(NwnxType.Object, list);
        }
        return list;
    }

    static (int Type, List<NwnxValue> List) Head(NwnxCall n)
    {
        int type = n.PopInt();
        uint obj = n.PopObjectId();
        var tag = n.PopString();
        return (type, Array(n, type, obj, tag));
    }

    static NwnxValue PopElement(NwnxCall n, int type) => type switch
    {
        TypeFloat => NwnxValue.Float(n.PopFloat()),
        TypeInt => NwnxValue.Int(n.PopInt()),
        TypeObject => NwnxValue.Object(n.PopObjectId()),
        _ => NwnxValue.String(n.PopString()),
    };

    static NwnxValue Default(int type) => type switch
    {
        TypeFloat => NwnxValue.Float(0),
        TypeInt => NwnxValue.Int(0),
        TypeObject => NwnxValue.Object(GameObject.InvalidId),
        _ => NwnxValue.String(""),
    };

    static int IndexOf(List<NwnxValue> list, NwnxValue v) => list.FindIndex(x => Equals(x.Value, v.Value));

    [NwnxFunc(P, "ArrayAt")]
    static void At(NwnxCall n)
    {
        var (type, list) = Head(n); int i = n.PopInt();
        n.Push(i >= 0 && i < list.Count ? list[i] : Default(type));
    }
    [NwnxFunc(P, "ArrayClear")] static void Clear(NwnxCall n) => Head(n).List.Clear();
    [NwnxFunc(P, "ArrayContains")]
    static void Contains(NwnxCall n)
    {
        var (type, list) = Head(n);
        n.Push(IndexOf(list, PopElement(n, type)) >= 0);
    }
    [NwnxFunc(P, "ArrayCopy")]
    static void Copy(NwnxCall n)
    {
        int type = n.PopInt(); uint obj = n.PopObjectId(); var tag = n.PopString(); var other = n.PopString();
        var dst = Array(n, type, obj, other);
        dst.Clear();
        dst.AddRange(Array(n, type, obj, tag));
    }
    [NwnxFunc(P, "ArrayErase")]
    static void Erase(NwnxCall n)
    {
        var (_, list) = Head(n); int i = n.PopInt();
        if (i >= 0 && i < list.Count) list.RemoveAt(i);
    }
    [NwnxFunc(P, "ArrayFind")]
    static void Find(NwnxCall n)
    {
        var (type, list) = Head(n);
        n.Push(IndexOf(list, PopElement(n, type)));
    }
    [NwnxFunc(P, "ArrayInsert")]
    static void Insert(NwnxCall n)
    {
        var (type, list) = Head(n); int i = n.PopInt(); var v = PopElement(n, type);
        if (i >= 0 && i <= list.Count) list.Insert(i, v);
    }
    [NwnxFunc(P, "ArrayPushBack")]
    static void PushBack(NwnxCall n)
    {
        var (type, list) = Head(n);
        list.Add(PopElement(n, type));
    }
    [NwnxFunc(P, "ArrayResize")]
    static void Resize(NwnxCall n)
    {
        var (type, list) = Head(n); int size = Math.Max(0, n.PopInt());
        if (list.Count > size) list.RemoveRange(size, list.Count - size);
        while (list.Count < size) list.Add(Default(type));
    }
    [NwnxFunc(P, "ArrayShuffle")]
    static void Shuffle(NwnxCall n)
    {
        var (_, list) = Head(n);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Shared.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
    [NwnxFunc(P, "ArraySize")] static void Size(NwnxCall n) => n.Push(Head(n).List.Count);
    static int Compare(NwnxValue a, NwnxValue b) => (a.Value, b.Value) switch
    {
        (int x, int y) => x.CompareTo(y),
        (float x, float y) => x.CompareTo(y),
        (uint x, uint y) => x.CompareTo(y),
        _ => string.CompareOrdinal(a.ToString(), b.ToString()),
    };
    [NwnxFunc(P, "ArraySortAscending")] static void SortAsc(NwnxCall n) => Head(n).List.Sort(Compare);
    [NwnxFunc(P, "ArraySortDescending")] static void SortDesc(NwnxCall n) => Head(n).List.Sort((a, b) => Compare(b, a));
    [NwnxFunc(P, "ArraySet")]
    static void Set(NwnxCall n)
    {
        var (type, list) = Head(n); int i = n.PopInt(); var v = PopElement(n, type);
        if (i >= 0 && i < list.Count) list[i] = v;
    }
}

/// <summary>NWNX_Regex.</summary>
static class RegexPlugin
{
    const string P = "Regex";

    static Regex? Make(string pattern)
    {
        try { return new Regex(pattern, RegexOptions.ECMAScript); }
        catch (ArgumentException)
        {
            try { return new Regex(pattern); }
            catch (ArgumentException) { return null; }
        }
    }

    [NwnxFunc(P, "Search")]
    static void Search(NwnxCall n)
    {
        var str = n.PopString(); var re = Make(n.PopString());
        n.Push(re != null && re.IsMatch(str));
    }
    [NwnxFunc(P, "Replace")]
    static void Replace(NwnxCall n)
    {
        var str = n.PopString(); var re = Make(n.PopString()); var replacement = n.PopString(); bool firstOnly = n.PopInt() != 0;
        n.Push(re == null ? str : firstOnly ? re.Replace(str, replacement, 1) : re.Replace(str, replacement));
    }
    [NwnxFunc(P, "Match")]
    static void Match(NwnxCall n)
    {
        var str = n.PopString(); var re = Make(n.PopString());
        var result = new JsonArray();
        if (re != null)
            foreach (System.Text.RegularExpressions.Match m in re.Matches(str))
            {
                var groups = new JsonArray();
                foreach (Group g in m.Groups) groups.Add(g.Value);
                result.Add(groups);
            }
        n.Push(new JsonValue(result));
    }
}
