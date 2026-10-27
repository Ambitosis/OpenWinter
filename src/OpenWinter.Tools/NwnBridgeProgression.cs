using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Net;

/// <summary>
/// nwn-bridge --levelup CLASS,CLASS,...: takes a level in each class in turn (classes.2da rows, or "last" for the class
/// taken last) the way the level-up window does: opens it, asks for the class's choices completed from its package
/// ("Recommended"), and takes them. Each step's answer is printed: the classes on offer, the plan's errors and the
/// choices, and the server's verdict.
/// </summary>
sealed class NwnBridgeLevelUp(string classes)
{
    readonly Queue<string> _levels = new(classes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    double _next = 9;
    string _waiting = "";
    int _lastClass = -1;
    JsonNode? _choice;

    /// <summary>Adds the first offered spells for each spell level the choice is short of; false when none was.</summary>
    static bool FillSpells(JsonNode plan, JsonNode choice)
    {
        bool added = false;
        var spells = choice["spells"] as JsonObject ?? new JsonObject();
        choice["spells"] = spells;
        foreach (var pick in plan["spells"]?.AsArray() ?? [])
            foreach (var (level, count) in pick!["perLevel"]!.AsObject())
            {
                var list = spells[level] as JsonArray ?? new JsonArray();
                spells[level] = list;
                var have = list.Select(x => x!.GetValue<int>()).ToHashSet();
                foreach (var option in pick["choices"]?[level]?.AsArray() ?? [])
                {
                    if (list.Count >= count!.GetValue<int>()) break;
                    if (have.Add(option!.GetValue<int>())) { list.Add(option.GetValue<int>()); added = true; }
                }
            }
        return added;
    }

    public bool Done => _levels.Count == 0 && _waiting.Length == 0;

    /// <summary>Starts the next level when the last is done; true when a request went out.</summary>
    public void Tick(double seconds, Action<string, object> request)
    {
        if (_waiting.Length > 0 || _levels.Count == 0 || seconds < _next) return;
        _waiting = "levelup-data";
        request("levelup-data", new { });
    }

    /// <summary>Follows the level-up notifies and answers them.</summary>
    public void Observe(NotifyMessage n, long ms, double seconds, Action<string, object> request)
    {
        if (!n.Kind.StartsWith("levelup-", StringComparison.Ordinal) || n.Kind != _waiting) return;
        var d = JsonNode.Parse(n.Json)!;
        switch (n.Kind)
        {
            case "levelup-data":
            {
                _lastClass = d["lastClass"]?.GetValue<int>() ?? -1;
                var offered = d["classes"]!.AsArray().Where(c => c!["available"]!.GetValue<bool>()).Select(c => c!["id"]!.GetValue<int>());
                Console.WriteLine($"{ms,6} levelup-data level {d["level"]} canLevelUp {d["canLevelUp"]} last class {_lastClass} offered [{string.Join(",", offered)}]");
                var pick = _levels.Peek();
                int cls = pick == "last" ? _lastClass : int.Parse(pick);
                _waiting = "levelup-plan";
                request("levelup-data", new { choice = new { @class = cls }, auto = true });
                break;
            }
            case "levelup-plan":
            {
                var choice = d["auto"] is { } auto ? auto.DeepClone() : _choice;
                var errors = d["errors"]?.AsArray().Select(e => e!["message"]?.ToString()).ToList() ?? [];
                Console.WriteLine($"{ms,6} levelup-plan choice {choice?.ToJsonString()} errors [{string.Join("; ", errors)}]");
                if (choice == null)
                {
                    Console.WriteLine($"{ms,6} no recommended choices: stopping");
                    _levels.Clear();
                    _waiting = "";
                    return;
                }
                if (errors.Count > 0 && _choice == null && FillSpells(d, choice))
                {
                    // The package leaves spells to choose (a sorcerer's past its list): the first ones offered.
                    _choice = choice;
                    request("levelup-data", new { choice });
                    return;
                }
                _choice = null;
                _waiting = "levelup-result";
                request("levelup-apply", new { choice });
                break;
            }
            case "levelup-result":
                Console.WriteLine($"{ms,6} levelup-result {n.Json}");
                _levels.Dequeue();
                _waiting = "";
                _next = seconds + 4;
                if (!d["ok"]!.GetValue<bool>()) _levels.Clear();
                break;
        }
    }
}

/// <summary>
/// nwn-bridge URL - --new-character FIRST[,CLASS]: creates a character on the server the way the creation window does
/// (a human of CLASS, default fighter, with the first portrait offered, the package's first level and a point buy
/// that suits most classes) and enters the module with it.
/// </summary>
sealed class NwnBridgeNewCharacter(string spec)
{
    readonly string _name = spec.Split(',')[0];
    readonly int _class = spec.Split(',') is [_, var c] ? int.Parse(c) : 4;
    JsonObject? _character;

    /// <summary>Answers the creation notifies; true when the notify was one.</summary>
    public void Observe(NotifyMessage n, long ms, Action<string, object> request)
    {
        switch (n.Kind)
        {
            case "chargen-portraits":
            {
                var first = JsonNode.Parse(n.Json)!["portraits"]!.AsArray().FirstOrDefault();
                _character = new JsonObject
                {
                    ["firstName"] = _name, ["lastName"] = "Probe", ["gender"] = 0, ["race"] = 6,
                    ["portraitId"] = first?["id"]?.GetValue<int>() ?? -1, ["lawChaos"] = 50, ["goodEvil"] = 50,
                    ["abilities"] = new JsonArray(14, 14, 14, 12, 12, 10),
                    ["level"] = new JsonObject { ["class"] = _class },
                };
                Console.WriteLine($"{ms,6} new character {_character.ToJsonString()}");
                request("chargen-validate", new { character = _character, auto = true });
                break;
            }
            case "chargen-plan" when _character != null:
            {
                var d = JsonNode.Parse(n.Json)!;
                var plan = d["plan"]!;
                var errors = plan["errors"]!.AsArray().Concat(d["identity"]!.AsArray()).Select(e => e!["message"]?.ToString());
                Console.WriteLine($"{ms,6} chargen-plan auto {plan["auto"]?.ToJsonString()} errors [{string.Join("; ", errors)}]");
                _character["level"] = plan["auto"]?.DeepClone() ?? _character["level"]!.DeepClone();
                request("chargen-create", new { character = _character });
                _character = null;
                break;
            }
            case "chargen-result":
                Console.WriteLine($"{ms,6} chargen-result {n.Json}");
                break;
        }
    }

    /// <summary>The character list arrived: ask for the portraits to pick from.</summary>
    public void Begin(Action<string, object> request) => request("chargen-portraits", new { race = 6, gender = 0 });
}
