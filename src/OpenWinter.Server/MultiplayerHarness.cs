using System.Numerics;
using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

/// <summary>
/// Multiplayer sync harness, server side (<c>--harness &lt;dir&gt;</c>; tools/multiplayer, docs/MULTIPLAYER_HARNESS.md).
/// Records what the server holds, in the same form the clients' <c>--mp-trace</c> records what they show, and relays
/// the scene script's markers (the visual harness's <c>VISUAL|...</c> log lines) to the clients:
/// <list type="bullet">
/// <item><c>VISUAL|shot|name</c>: a checkpoint. The server writes its state to <c>checkpoints.jsonl</c> at the end of
/// the tick and tells every client (notify <c>mp-checkpoint</c>), which writes what it shows.</item>
/// <item><c>VISUAL|note|...</c>: a reading the scene logged, kept in <c>notes.txt</c>.</item>
/// <item><c>VISUAL|do|player|verb|args...</c>: client input for one player (notify <c>mp-do</c>, see the client's
/// MultiplayerTrace): keys, clicks, chat, requests, so the input path is exercised too.</item>
/// <item><c>VISUAL|done</c>: tells the clients to finish (notify <c>mp-done</c>); the server stops shortly after.</item>
/// </list>
/// It also samples every creature and door the players can see every 0.2 s (<c>samples.jsonl</c>).
/// Nothing here changes the simulation.
/// </summary>
sealed class MultiplayerHarness : IDisposable
{
    readonly World _world;
    readonly GameServer _server;
    readonly StreamWriter _samples, _checkpoints, _notes;
    readonly List<string[]> _pending = new();

    /// <summary>"@tag" in a request's JSON becomes the id of the object with that tag, items in inventories too (the client
    /// only knows the objects it was sent).</summary>
    string Resolve(string arg) => System.Text.RegularExpressions.Regex.Replace(arg, "\"@([^\"]+)\"",
        m => _world.ByTag(m.Groups[1].Value).FirstOrDefault() is { } o ? o.Id.ToString() : m.Value);
    double _nextSample;
    double _doneAt = -1;
    public bool Finished { get; private set; }

    static readonly JsonSerializerOptions Json = new() { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };

    public MultiplayerHarness(World world, GameServer server, string dir)
    {
        _world = world;
        _server = server;
        Directory.CreateDirectory(dir);
        _samples = new StreamWriter(Path.Combine(dir, "samples.jsonl")) { AutoFlush = true };
        _checkpoints = new StreamWriter(Path.Combine(dir, "checkpoints.jsonl")) { AutoFlush = true };
        _notes = new StreamWriter(Path.Combine(dir, "notes.txt")) { AutoFlush = true };
    }

    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>A script log line (WriteTimestampedLogEntry): markers are handled after the tick.</summary>
    public void OnLog(string line)
    {
        int at = line.IndexOf("VISUAL|", StringComparison.Ordinal);
        if (at < 0) return;
        var text = line[at..].Trim();
        var parts = text[7..].Split('|');
        if (parts[0] == "note") { _notes.WriteLine($"{Now}|{text}"); return; }
        _pending.Add(parts);
    }

    /// <summary>Runs after each server tick: markers, then a sample when one is due.</summary>
    public void AfterTick()
    {
        foreach (var parts in _pending.ToList())
        {
            switch (parts[0])
            {
                case "shot" when parts.Length > 1:
                    Checkpoint(parts[1]);
                    break;
                case "do" when parts.Length > 2:
                    if (Session(parts[1]) is { Creature: { } pc })
                        _server.Notify("mp-do", pc, new { verb = parts[2], args = parts.Skip(3).Select(Resolve).ToArray() }, pc);
                    else Console.WriteLine($"[harness] no player named {parts[1]} for {string.Join('|', parts)}");
                    break;
                case "done":
                    _server.Notify("mp-done", null, new { });
                    _doneAt = _world.Time + 3;
                    break;
            }
        }
        _pending.Clear();
        if (_world.Time >= _nextSample)
        {
            _nextSample = _world.Time + 0.2;
            if (_server.Sessions.Any(s => s.Area != null)) _samples.WriteLine(JsonSerializer.Serialize(Sample(), Json));
        }
        if (_doneAt >= 0 && _world.Time >= _doneAt) Finished = true;
    }

    PlayerSession? Session(string name) => _server.Sessions.FirstOrDefault(s =>
        s.PlayerName.Equals(name, StringComparison.OrdinalIgnoreCase) || s.Creature?.Name.FirstString.Equals(name, StringComparison.OrdinalIgnoreCase) == true);

    void Checkpoint(string name)
    {
        _checkpoints.WriteLine(JsonSerializer.Serialize(new { t = Now, world = _world.Time, name, state = State() }, Json));
        _server.Notify("mp-checkpoint", null, new { name });
        Console.WriteLine($"[harness] checkpoint {name}");
    }

    static float[] V(Vector3 v) => [MathF.Round(v.X, 3), MathF.Round(v.Y, 3), MathF.Round(v.Z, 3)];
    static float Deg(float radians) => MathF.Round((radians * 180f / MathF.PI % 360f + 360f) % 360f, 1);

    IEnumerable<Area> WatchedAreas() => _server.Sessions.Select(s => s.Area).OfType<Area>().Distinct();

    object Sample() => new
    {
        t = Now,
        world = _world.Time,
        objects = WatchedAreas().SelectMany(a => a.Objects).Where(o => o is Creature or Door && !o.IsDestroyed).Select(o => o switch
        {
            Creature c => (object)new { id = c.Id, area = c.Area?.ResRef, pos = V(c.Position), facing = Deg(c.Facing), mode = c.Mode.ToString(), speed = MathF.Round(c.Speed, 2), hp = c.CurrentHitPoints, dead = c.IsDead, combat = c.InCombat },
            Door d => new { id = d.Id, area = d.Area?.ResRef, open = d.IsOpen },
            _ => new { id = o.Id },
        }).ToList(),
    };

    /// <summary>Everything a checkpoint compares: each watched area's visible objects and each player's view of the game.</summary>
    object State()
    {
        var party = OpenWinter.Game.Society.PartyService.Of(_world);
        var effects = _world.System<OpenWinter.Game.Rules.EffectSystem>();
        return new
        {
            players = _server.Sessions.Where(s => s.Creature != null).Select(s => new
            {
                player = s.PlayerName,
                id = s.Creature!.Id,
                name = s.Creature.Name.Resolve(_world.Resources.Talk),
                area = s.Area?.ResRef,
                xp = s.Creature.Experience,
                gold = s.Creature.Gold,
                party = party.Members(party.Ensure(s.PlayerCreature!)).Select(m => m.Id).ToList(),
                inventory = s.Creature.Inventory.Select(i => i.Tag).OrderBy(t => t, StringComparer.Ordinal).ToList(),
                known = s.Known.OrderBy(id => id).ToList(),
            }).ToList(),
            objects = WatchedAreas().SelectMany(a => a.Objects).Where(o => !o.IsDestroyed && o is Creature or Door or Item or Placeable { Useable: true }).Select(o => Describe(o, effects)).ToList(),
        };
    }

    object Describe(GameObject o, OpenWinter.Game.Rules.EffectSystem effects)
    {
        var vfx = o.Effects.Count == 0 ? new List<int>() : effects.LastingVisuals(o).Where(v => v.Kind == "vfx")
            .Select(v => JsonSerializer.SerializeToElement(v.Data).GetProperty("vfx").GetInt32()).OrderBy(x => x).ToList();
        return o switch
        {
            Creature c => new
            {
                id = c.Id, kind = "creature", tag = c.Tag, name = c.Name.Resolve(_world.Resources.Talk), area = c.Area?.ResRef, pos = V(c.Position), facing = Deg(c.Facing),
                mode = c.Mode.ToString(), speed = MathF.Round(c.Speed, 2), combat = c.InCombat, dead = c.IsDead,
                hp = c.CurrentHitPoints, maxHp = c.MaxHitPoints, damageLevel = OpenWinter.Game.Players.CreatureLabelFeed.DamageLevel(_world, c),
                action = c.Actions.Count > 0 ? c.Actions[0].GetType().Name : "", effects = c.Effects.Select(e => e.Type).OrderBy(x => x).ToList(), vfx,
                faction = c.Faction, pc = c.IsPC,
            },
            Door d => (object)new { id = d.Id, kind = "door", tag = d.Tag, name = d.Name.Resolve(_world.Resources.Talk), area = d.Area?.ResRef, pos = V(d.Position), open = d.IsOpen, locked = d.Locked, vfx },
            Item i => new { id = i.Id, kind = "item", tag = i.Tag, name = i.Name.Resolve(_world.Resources.Talk), area = i.Area?.ResRef, pos = V(i.Position), facing = Deg(i.Facing), baseItem = i.BaseItem, vfx },
            Placeable p => new { id = p.Id, kind = "placeable", tag = p.Tag, name = p.Name.Resolve(_world.Resources.Talk), area = p.Area?.ResRef, pos = V(p.Position), open = p.IsOpen, vfx },
            _ => new { id = o.Id },
        };
    }

    public void Dispose()
    {
        _samples.Dispose();
        _checkpoints.Dispose();
        _notes.Dispose();
    }
}
