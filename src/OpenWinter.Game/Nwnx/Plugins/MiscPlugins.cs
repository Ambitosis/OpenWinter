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

    [NwnxFunc(P, "ArrayAt")] static void At(NwnxCall n)
    {
        var (type, list) = Head(n); int i = n.PopInt();
        n.Push(i >= 0 && i < list.Count ? list[i] : Default(type));
    }
    [NwnxFunc(P, "ArrayClear")] static void Clear(NwnxCall n) => Head(n).List.Clear();
    [NwnxFunc(P, "ArrayContains")] static void Contains(NwnxCall n)
    {
        var (type, list) = Head(n);
        n.Push(IndexOf(list, PopElement(n, type)) >= 0);
    }
    [NwnxFunc(P, "ArrayCopy")] static void Copy(NwnxCall n)
    {
        int type = n.PopInt(); uint obj = n.PopObjectId(); var tag = n.PopString(); var other = n.PopString();
        var dst = Array(n, type, obj, other);
        dst.Clear();
        dst.AddRange(Array(n, type, obj, tag));
    }
    [NwnxFunc(P, "ArrayErase")] static void Erase(NwnxCall n)
    {
        var (_, list) = Head(n); int i = n.PopInt();
        if (i >= 0 && i < list.Count) list.RemoveAt(i);
    }
    [NwnxFunc(P, "ArrayFind")] static void Find(NwnxCall n)
    {
        var (type, list) = Head(n);
        n.Push(IndexOf(list, PopElement(n, type)));
    }
    [NwnxFunc(P, "ArrayInsert")] static void Insert(NwnxCall n)
    {
        var (type, list) = Head(n); int i = n.PopInt(); var v = PopElement(n, type);
        if (i >= 0 && i <= list.Count) list.Insert(i, v);
    }
    [NwnxFunc(P, "ArrayPushBack")] static void PushBack(NwnxCall n)
    {
        var (type, list) = Head(n);
        list.Add(PopElement(n, type));
    }
    [NwnxFunc(P, "ArrayResize")] static void Resize(NwnxCall n)
    {
        var (type, list) = Head(n); int size = Math.Max(0, n.PopInt());
        if (list.Count > size) list.RemoveRange(size, list.Count - size);
        while (list.Count < size) list.Add(Default(type));
    }
    [NwnxFunc(P, "ArrayShuffle")] static void Shuffle(NwnxCall n)
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
    [NwnxFunc(P, "ArraySet")] static void Set(NwnxCall n)
    {
        var (type, list) = Head(n); int i = n.PopInt(); var v = PopElement(n, type);
        if (i >= 0 && i < list.Count) list[i] = v;
    }
}

/// <summary>
/// NWNX_Events for script-defined events (PushEventData + SignalEvent) and for
/// engine code that wants to raise NWNX-style events through <see cref="Signal"/>.
/// </summary>
public static class EventsPlugin
{
    const string P = "Events";
    const string KeyEvent = "nwnx:event", KeyDataPrefix = "nwnx:data:";

    sealed class EventsState
    {
        public readonly Dictionary<string, List<string>> Subscribers = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> PendingData = new(StringComparer.Ordinal);
        public readonly Stack<(bool Skipped, string Result)> Running = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, EventsState> _state = new();
    static EventsState State(NwnxRegistry r) => _state.GetValue(r, _ => new EventsState());

    /// <summary>
    /// Runs every script subscribed to <paramref name="evt"/> with OBJECT_SELF =
    /// <paramref name="target"/> and the given event data. Returns whether a
    /// subscriber called SkipEvent and the last SetEventResult value.
    /// </summary>
    public static (bool Skipped, string Result) Signal(World world, string evt, GameObject target, IReadOnlyDictionary<string, string>? data = null)
    {
        var reg = world.System<NwnxRegistry>();
        var st = State(reg);
        if (!st.Subscribers.TryGetValue(evt, out var scripts) || scripts.Count == 0 || world.Scripts == null) return (false, "");
        var ctx = new EventContext().Set(KeyEvent, evt);
        if (data != null) foreach (var kv in data) ctx.Set(KeyDataPrefix + kv.Key, kv.Value);
        st.Running.Push((false, ""));
        try
        {
            foreach (var script in scripts.ToList()) world.Scripts.Run(script, target.Id, ctx.Clone());
            return st.Running.Peek();
        }
        finally { st.Running.Pop(); }
    }

    public static int SubscriberCount(World world, string evt) =>
        State(world.System<NwnxRegistry>()).Subscribers.TryGetValue(evt, out var l) ? l.Count : 0;

    [NwnxFunc(P, "SubscribeEvent")] static void Subscribe(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString();
        if (evt.StartsWith("NWNX_ON_", StringComparison.Ordinal) && !evt.StartsWith("NWNX_ON_DM_", StringComparison.Ordinal)
            && !evt.StartsWith("NWNX_ON_PARTY_", StringComparison.Ordinal))
            n.ReportUnsupported("automatic engine events outside NWNX_ON_DM_* and NWNX_ON_PARTY_* are not emitted; explicit SignalEvent still works", evt);
        var subs = State(n.Registry).Subscribers;
        if (!subs.TryGetValue(evt, out var list)) subs[evt] = list = new List<string>();
        if (!list.Contains(script, StringComparer.OrdinalIgnoreCase)) list.Add(script);
    }
    [NwnxFunc(P, "UnsubscribeEvent")] static void Unsubscribe(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString();
        if (State(n.Registry).Subscribers.TryGetValue(evt, out var list)) list.RemoveAll(s => s.Equals(script, StringComparison.OrdinalIgnoreCase));
    }
    [NwnxFunc(P, "PushEventData")] static void PushData(NwnxCall n)
    {
        var tag = n.PopString(); var data = n.PopString();
        State(n.Registry).PendingData[tag] = data;
    }
    [NwnxFunc(P, "SignalEvent")] static void SignalEvent(NwnxCall n)
    {
        var evt = n.PopString(); var target = n.PopObject();
        var st = State(n.Registry);
        var data = new Dictionary<string, string>(st.PendingData);
        st.PendingData.Clear();
        bool any = target != null && SubscriberCount(n.World, evt) > 0;
        if (target != null) Signal(n.World, evt, target, data);
        n.Push(any);
    }
    [NwnxFunc(P, "GetEventData")] static void GetData(NwnxCall n)
    {
        var tag = n.PopString();
        n.Push(n.Host?.CurrentEvent.Str(KeyDataPrefix + tag) ?? "");
    }
    [NwnxFunc(P, "GetCurrentEvent")] static void CurrentEvent(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Str(KeyEvent) ?? "");
    [NwnxFunc(P, "SkipEvent")] static void Skip(NwnxCall n)
    {
        var st = State(n.Registry);
        if (st.Running.Count > 0) { var top = st.Running.Pop(); st.Running.Push((true, top.Result)); }
    }
    [NwnxFunc(P, "SetEventResult")] static void SetResult(NwnxCall n)
    {
        var result = n.PopString();
        var st = State(n.Registry);
        if (st.Running.Count > 0) { var top = st.Running.Pop(); st.Running.Push((top.Skipped, result)); }
    }
    [NwnxFunc(P, "GetNumSubscribers")] static void NumSubscribers(NwnxCall n) => n.Push(SubscriberCount(n.World, n.PopString()));
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

    [NwnxFunc(P, "Search")] static void Search(NwnxCall n)
    {
        var str = n.PopString(); var re = Make(n.PopString());
        n.Push(re != null && re.IsMatch(str));
    }
    [NwnxFunc(P, "Replace")] static void Replace(NwnxCall n)
    {
        var str = n.PopString(); var re = Make(n.PopString()); var replacement = n.PopString(); bool firstOnly = n.PopInt() != 0;
        n.Push(re == null ? str : firstOnly ? re.Replace(str, replacement, 1) : re.Replace(str, replacement));
    }
    [NwnxFunc(P, "Match")] static void Match(NwnxCall n)
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

/// <summary>NWNX_Chat: sending messages and the chat-script context.</summary>
static class ChatPlugin
{
    const string P = "Chat";

    [NwnxFunc(P, "SendMessage")] static void SendMessage(NwnxCall n)
    {
        int channel = n.PopInt(); var message = n.PopString(); var sender = n.PopObject(); var target = n.PopObject<Creature>();
        if (sender == null) { n.Push(0); return; }
        var output = n.World.Output;
        // NWNX_CHAT_CHANNEL_*: 1 talk, 2 shout, 3 whisper, 4 tell, 5 server, 6 party, 14 DM.
        switch (channel)
        {
            case 4 or 20 when target != null:
            case 5 or 21 when target != null:
                output?.SendMessage(target, message);
                break;
            case 5 or 21:
                foreach (var pc in n.World.ConnectedPCs.ToList()) output?.SendMessage(pc, message);
                break;
            default:
                output?.Speak(sender, message, channel is 2 or 18 ? 2 : channel is 3 or 19 ? 1 : 0);
                break;
        }
        n.Push(1);
    }
    [NwnxFunc(P, "RegisterChatScript")] static void Register(NwnxCall n) => n.State(n.World.Module)["chatScript"] = NwnxValue.String(n.PopString());
    [NwnxFunc(P, "SkipMessage")] static void Skip(NwnxCall n) => n.Host?.CurrentEvent.Set("nwnx:chat:skip", 1);
    [NwnxFunc(P, "GetChannel")] static void Channel(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Int("nwnx:chat:channel") ?? 0);
    [NwnxFunc(P, "GetMessage")] static void Message(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Str("nwnx:chat:message") ?? "");
    [NwnxFunc(P, "GetSender")] static void Sender(NwnxCall n) => n.PushObject(n.Host?.CurrentEvent.Obj("nwnx:chat:sender") ?? GameObject.InvalidId);
    [NwnxFunc(P, "GetTarget")] static void Target(NwnxCall n) => n.PushObject(n.Host?.CurrentEvent.Obj("nwnx:chat:target") ?? GameObject.InvalidId);
}

/// <summary>NWNX_Feedback: hidden feedback / combat log messages (global, or per player).</summary>
static class FeedbackPlugin
{
    const string P = "Feedback";

    [NwnxFunc(P, "SetMessageHidden", UnsupportedReason = "hidden flag is stored but feedback messages do not consult it")] static void SetHidden(NwnxCall n)
    {
        uint pc = n.PopObjectId(); int type = n.PopInt(); int message = n.PopInt(); bool hidden = n.PopInt() != 0;
        // TODO(world): message senders should consult NwnxRegistry state "feedback:{type}:{message}".
        n.State(pc)[$"feedback:{type}:{message}"] = NwnxValue.Int(hidden ? 1 : 0);
    }
    [NwnxFunc(P, "GetMessageHidden")] static void GetHidden(NwnxCall n)
    {
        uint pc = n.PopObjectId(); int type = n.PopInt(); int message = n.PopInt();
        var key = $"feedback:{type}:{message}";
        int v = n.State(pc).TryGetValue(key, out var x) && x.Value is int i ? i
            : n.State(GameObject.InvalidId).TryGetValue(key, out var g) && g.Value is int gi ? gi : 0;
        n.Push(v);
    }
    [NwnxFunc(P, "SetFeedbackMode")] static void SetMode(NwnxCall n)
    {
        int type = n.PopInt(); bool whitelist = n.PopInt() != 0;
        n.State(GameObject.InvalidId)[$"feedbackmode:{type}"] = NwnxValue.Int(whitelist ? 1 : 0);
    }
}

/// <summary>NWNX_Rename: player name overrides, sent to observers' clients.</summary>
static class RenamePlugin
{
    const string P = "Rename";

    [NwnxFunc(P, "SetPCNameOverride")] static void SetOverride(NwnxCall n)
    {
        var target = n.PopObject<Creature>(); var name = n.PopString(); var prefix = n.PopString(); var suffix = n.PopString();
        int playerNameState = n.PopInt(); uint observer = n.PopObjectId();
        if (target == null) return;
        var full = prefix + name + suffix;
        n.State(target)["rename:" + observer] = NwnxValue.String(full);
        var data = new { op = "name-override", target = target.Id, name = full, playerNameState };
        if (n.World.Get(observer) is Creature { IsPC: true } obs) n.World.Output?.Notify("nwnx-player", obs, data, obs);
        else n.World.NotifyAllPlayers("nwnx-player", data);
    }
    [NwnxFunc(P, "GetPCNameOverride")] static void GetOverride(NwnxCall n)
    {
        var target = n.PopObject<Creature>(); uint observer = n.PopObjectId();
        if (target == null) { n.Push(""); return; }
        var st = n.State(target);
        n.Push(st.TryGetValue("rename:" + observer, out var v) && v.Value is string s ? s
            : st.TryGetValue("rename:" + GameObject.InvalidId, out var g) && g.Value is string gs ? gs : "");
    }
    [NwnxFunc(P, "ClearPCNameOverride")] static void ClearOverride(NwnxCall n)
    {
        var target = n.PopObject<Creature>(); uint observer = n.PopObjectId(); bool all = n.PopInt() != 0;
        if (target == null) return;
        var st = n.State(target);
        foreach (var k in st.Keys.Where(k => k.StartsWith("rename:", StringComparison.Ordinal) && (all || k == "rename:" + observer)).ToList()) st.Remove(k);
        n.World.NotifyAllPlayers("nwnx-player", new { op = "name-override", target = target.Id, name = "", playerNameState = 0 });
    }
}

/// <summary>NWNX_Visibility: per-player (or global) visibility overrides.</summary>
static class VisibilityPlugin
{
    const string P = "Visibility";

    [NwnxFunc(P, "SetVisibilityOverride", UnsupportedReason = "override is stored but authoritative visibility does not consume it")] static void SetOverride(NwnxCall n)
    {
        uint player = n.PopObjectId(); var target = n.PopObject(); int value = n.PopInt();
        if (target == null) return;
        // TODO(world): object visibility for clients should honour these overrides.
        n.State(target)["visibility:" + player] = NwnxValue.Int(value);
        var data = new { op = "visibility", target = target.Id, value };
        if (n.World.Get(player) is Creature { IsPC: true } pc) n.World.Output?.Notify("nwnx-player", pc, data, pc);
        else n.World.NotifyAllPlayers("nwnx-player", data);
    }
    [NwnxFunc(P, "GetVisibilityOverride")] static void GetOverride(NwnxCall n)
    {
        uint player = n.PopObjectId(); var target = n.PopObject();
        n.Push(target != null && n.State(target).TryGetValue("visibility:" + player, out var v) && v.Value is int i ? i : -1);
    }
}

/// <summary>NWNX_Item: the setters that map onto item fields.</summary>
static class ItemPlugin
{
    const string P = "Item";

    [NwnxFunc(P, "SetWeight", UnsupportedReason = "override is stored but inventory weight does not consume it")] static void SetWeight(NwnxCall n)
    {
        var item = n.PopObject<Item>(); int weight = n.PopInt();
        // TODO(items): the weight rules should read this override (tenths of pounds).
        if (item != null) n.State(item)["weight"] = NwnxValue.Int(weight);
    }
    [NwnxFunc(P, "SetBaseGoldPieceValue")] static void SetBaseValue(NwnxCall n)
    {
        var item = n.PopObject<Item>(); int g = n.PopInt();
        if (item != null) item.Cost = g;
    }
    [NwnxFunc(P, "GetBaseGoldPieceValue")] static void GetBaseValue(NwnxCall n) => n.Push(n.PopObject<Item>()?.Cost ?? 0);
    [NwnxFunc(P, "SetAddGoldPieceValue")] static void SetAddValue(NwnxCall n)
    {
        var item = n.PopObject<Item>(); int g = n.PopInt();
        if (item != null) item.AddCost = g;
    }
    [NwnxFunc(P, "GetAddGoldPieceValue")] static void GetAddValue(NwnxCall n) => n.Push(n.PopObject<Item>()?.AddCost ?? 0);
    [NwnxFunc(P, "SetBaseItemType")] static void SetBaseItem(NwnxCall n)
    {
        var item = n.PopObject<Item>(); int type = n.PopInt();
        if (item == null) return;
        item.BaseItem = type;
        n.World.MarkChanged(item);
    }
}

/// <summary>NWNX_Area setters the module uses; values are kept for the area systems and sent to clients where visible.</summary>
static class AreaPlugin
{
    const string P = "Area";

    static void Store(NwnxCall n, string key, Func<NwnxCall, NwnxValue> value, bool notify = false)
    {
        var area = n.PopObject<Areas.Area>();
        var v = value(n);
        if (area == null) return;
        n.State(area)[key] = v;
        if (notify) n.World.Output?.Notify("area-setting", area, new { setting = key, value = v.Value });
    }

    [NwnxFunc(P, "SetPVPSetting")] static void Pvp(NwnxCall n) => Store(n, "pvp", c => NwnxValue.Int(c.PopInt()));
    [NwnxFunc(P, "SetNoRestingAllowed")] static void NoRest(NwnxCall n) => Store(n, "noRest", c => NwnxValue.Int(c.PopInt()));
    [NwnxFunc(P, "SetDayNightCycle")] static void DayNight(NwnxCall n) => Store(n, "dayNightCycle", c => NwnxValue.Int(c.PopInt()), notify: true);
    [NwnxFunc(P, "SetSunMoonColors")] static void SunMoon(NwnxCall n)
    {
        var area = n.PopObject<Areas.Area>(); int type = n.PopInt(); int color = n.PopInt();
        if (area == null) return;
        n.State(area)["sunMoon:" + type] = NwnxValue.Int(color);
        n.World.Output?.Notify("area-setting", area, new { setting = "sunMoon", type, value = color });
    }
    [NwnxFunc(P, "TestDirectLine")] static void TestDirectLine(NwnxCall n)
    {
        var area = n.PopObject<Areas.Area>();
        float sx = n.PopFloat(), sy = n.PopFloat(), ex = n.PopFloat(), ey = n.PopFloat();
        n.PopFloat(); n.PopFloat(); n.PopInt();
        if (area == null) { n.Push(0); return; }
        // Walkable if ground exists at regular samples along the segment (1 = clear, like NWNX's "no collision").
        int steps = Math.Max(1, (int)(MathF.Sqrt((ex - sx) * (ex - sx) + (ey - sy) * (ey - sy)) / 0.5f));
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            if (area.Walkmesh.HighestGround(sx + (ex - sx) * t, sy + (ey - sy) * t) == null) { n.Push(-1); return; }
        }
        n.Push(1);
    }
}

/// <summary>NWNX_Dialog: node data of the running conversation (provided by the conversation system through the event context).</summary>
static class DialogPlugin
{
    const string P = "Dialog";
    [NwnxFunc(P, "GetCurrentNodeType")] static void NodeType(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Int("nwnx:dialog:nodeType") ?? 0);
    [NwnxFunc(P, "GetCurrentScriptType")] static void ScriptType(NwnxCall n) => n.Push(n.Host?.CurrentEvent.Int("nwnx:dialog:scriptType") ?? 0);
    [NwnxFunc(P, "GetCurrentNodeID")] static void NodeId(NwnxCall n) =>
        n.Push(n.Host?.CurrentEvent.Values.TryGetValue("nwnx:dialog:nodeId", out var v) == true && v is int i ? i : -1);
    [NwnxFunc(P, "GetCurrentNodeIndex")] static void NodeIndex(NwnxCall n) =>
        n.Push(n.Host?.CurrentEvent.Values.TryGetValue("nwnx:dialog:nodeIndex", out var v) == true && v is int i ? i : -1);
    [NwnxFunc(P, "GetCurrentNodeText")] static void NodeText(NwnxCall n)
    {
        n.PopInt(); n.PopInt();
        n.Push(n.Host?.CurrentEvent.Str("nwnx:dialog:text") ?? "");
    }
    [NwnxFunc(P, "End", UnsupportedReason = "ending the conversation is not implemented")] static void End(NwnxCall n)
    {
        // TODO(conversation): end the conversation of the given object.
        n.PopObject();
    }
}

/// <summary>NWNX_Damage: event script registration (fired by the combat system) and event data.</summary>
static class DamagePlugin
{
    const string P = "Damage";

    [NwnxFunc(P, "SetEventScript", UnsupportedReason = "damage/attack callbacks are stored but are not dispatched by combat")] static void SetEventScript(NwnxCall n)
    {
        var evt = n.PopString(); var script = n.PopString(); uint owner = n.PopObjectId();
        // TODO(combat): run "damage:DAMAGE"/"damage:ATTACK" scripts from the combat round, owner first then the global (InvalidId) one.
        n.State(owner)["damage:" + evt] = NwnxValue.String(script);
    }

    /// <summary>The DAMAGE or ATTACK script for an owner, falling back to the global one.</summary>
    public static string EventScript(World world, string evt, uint owner)
    {
        var reg = world.System<NwnxRegistry>();
        if (reg.ObjectState(owner).TryGetValue("damage:" + evt, out var v) && v.Value is string s && s.Length > 0) return s;
        return reg.ObjectState(GameObject.InvalidId).TryGetValue("damage:" + evt, out var g) && g.Value is string gs ? gs : "";
    }
}

/// <summary>NWNX_WebHook: outbound HTTP is not performed; messages are logged.</summary>
static class WebHookPlugin
{
    [NwnxFunc("WebHook", "SendWebHookHTTPS")] static void Send(NwnxCall n)
    {
        var host = n.PopString(); var path = n.PopString(); var message = n.PopString();
        n.PopString(); n.PopInt();
        n.Registry.Log($"[nwnx] webhook {host}{path}: {message}");
    }
}

/// <summary>NWNX_Administration: the calls that map onto server actions.</summary>
static class AdministrationPlugin
{
    [NwnxFunc("Administration", "DeletePlayerCharacter", UnsupportedReason = "boots the player but does not delete the server vault character file")] static void DeleteCharacter(NwnxCall n)
    {
        var pc = n.PopObject<Creature>(); n.PopInt(); var message = n.PopString();
        if (pc is not { IsPC: true }) return;
        // TODO(persistence): remove the character file from the server vault.
        n.World.Output?.Notify("boot", pc, new { reason = message }, pc);
        pc.Player?.Connection.Disconnect();
    }
}
