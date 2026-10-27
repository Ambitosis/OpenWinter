using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Net;

/// <summary>
/// nwn-bridge --do "STEP;STEP;...": steps run four seconds apart once the player is in the area.
///   use:NAME            use the nearest object whose name contains NAME
///   req:KIND [JSON]     send a client request; "@NAME" in the JSON is the id of an item whose name contains NAME
///                       (from the last inventory, container or store contents; "@c:", "@s:" or "@i:" picks one),
///                       "@o:NAME" the nearest other object whose name contains NAME, "@o:me" the player's creature
///   say:TEXT            say a line in the talk channel
///   gap:SECONDS         the time between the steps after this one (four seconds at first)
///   wait                do nothing for a step
/// Inventory, container and store contents are printed one item a line.
/// </summary>
sealed class NwnBridgeSteps(string script)
{
    readonly Queue<string> _steps = new(script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    readonly Dictionary<string, List<(uint Id, string Name)>> _items = new() { ["i"] = [], ["c"] = [], ["s"] = [] };
    double _next = 6, _gap = 4;

    public bool Done => _steps.Count == 0;

    /// <summary>Prints a notice's contents when it lists items; true when it did.</summary>
    public bool Observe(NotifyMessage n, long ms)
    {
        if (n.Kind == "barter-state")
        {
            var b = JsonDocument.Parse(n.Json).RootElement;
            string Side(string k) => b.GetProperty(k) is var s
                ? $"{k} {s.GetProperty("name")} gold {s.GetProperty("gold")} offered {s.GetProperty("offered")} accepted {s.GetProperty("accepted")} items [{string.Join(", ", s.GetProperty("items").EnumerateArray().Select(i => $"{i.GetProperty("name")} x{i.GetProperty("stack")}"))}]"
                : "";
            Console.WriteLine($"{ms,6} Notify barter-state {Side("mine")} | {Side("theirs")}");
            return true;
        }
        string? scope = n.Kind switch { "inventory-open" => "i", "container-open" => "c", "store-open" => "s", _ => null };
        if (scope == null) return false;
        var d = JsonDocument.Parse(n.Json).RootElement;
        var list = _items[scope];
        list.Clear();
        var head = string.Join(" ", d.EnumerateObject().Where(p => p.Value.ValueKind is JsonValueKind.Number or JsonValueKind.String or JsonValueKind.True or JsonValueKind.False)
            .Select(p => $"{p.Name}={p.Value}"));
        var lines = new List<string>();
        foreach (var group in new[] { "equipped", "items" })
        {
            if (!d.TryGetProperty(group, out var items)) continue;
            foreach (var e in items.EnumerateArray())
            {
                uint id = e.GetProperty("id").GetUInt32();
                string name = e.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
                list.Add((id, name));
                string S(string k) => e.TryGetProperty(k, out var v) ? v.ToString() : "-";
                var info = e.TryGetProperty("info", out var inf) ? inf : default;
                string I(string k) => info.ValueKind == JsonValueKind.Object && info.TryGetProperty(k, out var v) ? v.ToString() : "-";
                lines.Add($"         {group[0]} {id:X} \"{name}\" base {S("baseItem")} stack {S("stack")} slot {S("slot")} at {S("x")},{S("y")} in {S("container")} id'd {S("identified")} price {S("price")} icon {S("icon")}"
                    + $" | {I("type")} {I("category")} {I("width")}x{I("height")} wt {I("weight")} value {I("value")} charges {I("charges")} props {I("properties")}");
            }
        }
        // Item lines only when they changed since the last of this kind.
        var text = string.Join('\n', lines);
        Console.WriteLine($"{ms,6} Notify {n.Kind} {head} items {lines.Count}{(text == _last.GetValueOrDefault(n.Kind) ? " (unchanged)" : "")}");
        if (text != _last.GetValueOrDefault(n.Kind) && text.Length > 0) Console.WriteLine(text);
        _last[n.Kind] = text;
        return true;
    }

    readonly Dictionary<string, string> _last = [];

    /// <summary>Runs the next step when it is due.</summary>
    public void Step(IConnection bridge, double seconds, Func<string, uint> nearest)
    {
        if (_steps.Count == 0 || seconds < _next) return;
        var step = _steps.Dequeue();
        if (step.StartsWith("gap:")) _gap = double.Parse(step[4..], System.Globalization.CultureInfo.InvariantCulture);
        _next = seconds + _gap;
        Console.WriteLine($"{(long)(seconds * 1000),6} step {step}");
        if (step.StartsWith("use:"))
        {
            uint target = nearest(step[4..]);
            if (target != 0) bridge.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = target }.Write());
            else Console.WriteLine("         nothing by that name");
        }
        else if (step.StartsWith("say:"))
            bridge.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = step[4..] }.Write());
        else if (step.StartsWith("req:"))
        {
            var body = step[4..];
            int space = body.IndexOf(' ');
            string kind = space < 0 ? body : body[..space], json = space < 0 ? "{}" : body[(space + 1)..];
            var node = JsonNode.Parse(json)!;
            Resolve(node, nearest);
            bridge.Send(new ClientRequestMessage { Kind = kind, Json = node.ToJsonString() }.Write());
            Console.WriteLine($"         sent {kind} {node.ToJsonString()}");
        }
    }

    void Resolve(JsonNode node, Func<string, uint> nearest)
    {
        if (node is not JsonObject o) return;
        foreach (var (key, value) in o.ToList())
        {
            if (value is JsonValue v && v.TryGetValue<string>(out var s) && s.StartsWith('@'))
            {
                var name = s[1..];
                if (name.StartsWith("o:"))
                {
                    o[key] = nearest(name[2..]);
                    continue;
                }
                string[] scopes = name.Length > 2 && name[1] == ':' ? [name[..1]] : ["i", "c", "s"];
                if (name.Length > 2 && name[1] == ':') name = name[2..];
                var hit = scopes.SelectMany(sc => _items[sc]).FirstOrDefault(i => i.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                o[key] = hit.Id;
            }
            else if (value != null) Resolve(value, nearest);
        }
    }
}
