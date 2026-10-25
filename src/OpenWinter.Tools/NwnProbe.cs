using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;

/// <summary>
/// nwn-probe: logs in to an NWN:EE server over WSNetLayer, enters the module with a server-vault character and records
/// every frame, then reports the message types seen and any frame that didn't decode.
///   nwn-probe ws://host:5121 [--player NAME] [--character RESREF] [--seconds N] [--password PW] [--key-file INI]
///             [--say TEXT] [--walk DX,DY] [--run]   (three seconds in: talk, then walk or run to start + offset)
/// Without --key-file a random key is generated: only a server that can't reach the master server accepts it.
/// Recordings go to out/nwn/probe-*.jsonl.gz (direction, milliseconds, frame hex).
/// </summary>
static class NwnProbe
{
    public static int Run(GamePaths paths, string[] args)
    {
        // Base game data only: the server's haks are not known here.
        var rm = ResMan.ForClient(paths, [], "");
        if (args.Length < 2)
        {
            Console.WriteLine("nwn-probe <ws://host:port> [--player NAME] [--character RESREF] [--seconds N] [--password PW] [--key-file INI]");
            return 1;
        }
        string Opt(string name, string fallback) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        var key = Array.IndexOf(args, "--key-file") >= 0 ? NwnCdKey.FromIni(Opt("--key-file", "")) : NwnCdKey.Generate();
        var login = new NwnLogin(Opt("--player", "probe"), key, Opt("--password", ""));
        var wanted = Opt("--character", "");
        var seconds = double.Parse(Opt("--seconds", "30"));
        var say = Opt("--say", "");
        var walk = Opt("--walk", "");
        var run = Array.IndexOf(args, "--run") >= 0;

        var context = new NwnSessionContext(t => rm.TwoDA(t).RowCount, (t, row, col) => rm.TwoDA(t).Get(row, col));
        using var transport = NwnWebSocketTransport.Connect(new Uri(args[1]));
        var session = new NwnSession(transport, login, context);

        var outDir = Path.Combine(Directory.GetCurrentDirectory(), "out", "nwn");
        Directory.CreateDirectory(outDir);
        var recording = Path.Combine(outDir, $"probe-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl.gz");
        var clock = Stopwatch.StartNew();
        using var file = new StreamWriter(new GZipStream(File.Create(recording), CompressionLevel.SmallestSize));
        session.FrameObserver = (sent, frame) => file.WriteLine(JsonSerializer.Serialize(new
        {
            dir = sent ? "c2s" : "s2c",
            ms = clock.ElapsedMilliseconds,
            hex = Convert.ToHexString(frame),
        }));

        var counts = new SortedDictionary<string, int>();
        var received = new List<NwnMessage>();
        var loggedIn = false;
        (double, AreaMessages.AreaClientArea)? arrived = null;
        while (clock.Elapsed.TotalSeconds < seconds && session.State != NwnSessionState.Failed)
        {
            received.Clear();
            session.Pump(received);
            if (session.State == NwnSessionState.Ready && !loggedIn)
            {
                loggedIn = true;
                Console.WriteLine($"logged in to \"{session.SessionInfo?.SessionName}\" ({session.Settings?.ModuleName}), server {session.ServerVersion}");
                session.Send(new ServerStatusMessages.ServerStatusRequest());
                session.Send(new CharListMessages.CharListRequest());
            }
            foreach (var m in received)
            {
                counts[m.GetType().Name] = counts.GetValueOrDefault(m.GetType().Name) + 1;
                React(session, m, wanted);
                if (m is AreaMessages.AreaClientArea a) arrived = (clock.Elapsed.TotalSeconds, a);
            }
            if (arrived is var (at, area) && clock.Elapsed.TotalSeconds > at + 3)
            {
                arrived = null;
                if (say.Length > 0) session.Send(new ChatMessages.ChatTalkC2S { Message = say });
                if (walk.Split(',') is [var dx, var dy])
                {
                    var (x, y, z) = area.Location;
                    session.Send(new InputMessages.InputWalkToWayPoint
                    {
                        Area = area.ObjectId, Position = (x + float.Parse(dx), y + float.Parse(dy), z), EventNumber = 1,
                        Run = run, ClickedOn = GameObjUpdateMessages.InvalidObjectId,
                    });
                }
            }
            Thread.Sleep(5);
        }

        Console.WriteLine(session.State == NwnSessionState.Failed ? $"failed: {session.Failure}" : $"ran {clock.Elapsed.TotalSeconds:0}s");
        foreach (var (name, n) in counts) Console.WriteLine($"  {n,6}  {name}");
        Console.WriteLine($"{session.DecodeErrors.Count} frames didn't decode");
        foreach (var e in session.DecodeErrors.Take(20))
            Console.WriteLine($"  {e.Error}  [{Convert.ToHexString(e.Frame.AsSpan(0, Math.Min(24, e.Frame.Length)))}…]");
        Console.WriteLine($"recording: {recording}");
        return session.DecodeErrors.Count == 0 && session.State != NwnSessionState.Failed ? 0 : 2;
    }

    /// <summary>The minimum a client must answer to get into the module: a character, then loaded module and area.</summary>
    static void React(NwnSession session, NwnMessage m, string wanted)
    {
        switch (m)
        {
            case CharListMessages.CharListListResponse list:
                var pick = list.Characters.FirstOrDefault(c => wanted.Length == 0 || c.Resref.Equals(wanted, StringComparison.OrdinalIgnoreCase));
                if (pick == null)
                {
                    Console.WriteLine($"no character {(wanted.Length > 0 ? wanted : "")} in the server vault ({list.Characters.Count} listed)");
                    return;
                }
                Console.WriteLine($"choosing {pick.Resref}");
                session.Send(new LoginMessages.LoginServerSubDirCharacter { Resref = pick.Resref });
                break;
            case ModuleMessages.ModuleInfo info:
                Console.WriteLine($"module {info.ResourceName}, {info.Areas.Count} areas");
                session.Send(new ModuleMessages.ModuleLoaded());
                break;
            case AreaMessages.AreaClientArea area:
                Console.WriteLine($"area {area.Resref} {area.Width}x{area.Height} ({area.Tileset})");
                session.Send(new AreaMessages.AreaAreaLoaded());
                break;
        }
    }
}

/// <summary>
/// nwn-dump: prints a recording's messages as JSON lines (time, direction, message), optionally only types whose
/// name contains a filter. Packets before login are shown by head.
///   nwn-dump out/nwn/probe-*.jsonl.gz [filter]
/// </summary>
static class NwnRecordingDump
{
    public static int Run(GamePaths paths, string[] args)
    {
        var rm = ResMan.ForClient(paths, [], "");
        var context = new NwnSessionContext(t => rm.TwoDA(t).RowCount, (t, row, col) => rm.TwoDA(t).Get(row, col));
        var filter = args.Length > 2 ? args[2] : "";
        using var reader = new StreamReader(new GZipStream(File.OpenRead(args[1]), CompressionMode.Decompress));
        while (reader.ReadLine() is { } line)
        {
            var row = JsonDocument.Parse(line).RootElement;
            var frame = Convert.FromHexString(row.GetProperty("hex").GetString()!);
            var dir = row.GetProperty("dir").GetString()!;
            var ms = row.GetProperty("ms").GetInt64();
            string name;
            System.Text.Json.Nodes.JsonNode? body;
            if (frame.Length >= 3 && frame[0] is (byte)'P' or (byte)'p')
            {
                var direction = frame[0] == (byte)'P' ? NwnDirection.ServerToClient : NwnDirection.ClientToServer;
                var m = NwnMessageRegistry.Create(frame[1], frame[2], direction);
                if (m == null)
                {
                    name = $"unknown {frame[1]:X2}.{frame[2]:X2}";
                    body = Convert.ToHexString(frame);
                }
                else
                {
                    name = m.GetType().Name;
                    try
                    {
                        m.Read(new NwnWsReader(frame.AsMemory(3), context));
                        body = NwnDump.ToJson(m);
                    }
                    catch (Exception e)
                    {
                        body = $"undecodable: {e.Message}";
                    }
                }
            }
            else
            {
                name = System.Text.Encoding.ASCII.GetString(frame, 0, Math.Min(4, frame.Length));
                body = NwnPacket.Parse(frame)?.ToString();
            }
            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            Console.WriteLine($"{ms,7} {dir} {name} {body?.ToJsonString()}");
        }
        return 0;
    }
}

/// <summary>
/// nwn-bridge: drives the client's NWN bridge without the client (hello, then a named server-vault character) and
/// prints the OpenWinter messages it produces, as the client would receive them.
///   nwn-bridge ws://host:port CHARACTER [--seconds N] [--walk DX,DY] [--say TEXT] [--use NAME|#ID,...] [--record FILE]
/// (four seconds in: say, walk; then use the nearest object whose name contains each NAME, five seconds apart)
/// </summary>
static class NwnBridgeProbe
{
    public static int Run(GamePaths paths, string[] args)
    {
        string Opt(string name, string fallback) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        var bridge = new OpenWinter.Net.Nwn.Bridge.NwnBridgeConnection(
            new OpenWinter.Net.Nwn.Bridge.NwnBridgeOptions(new Uri(args[1]), NwnCdKey.Generate()) { RecordTo = Opt("--record", "") is { Length: > 0 } rec ? rec : null },
            new OpenWinter.Net.Nwn.Bridge.NwnResManData(paths));
        bridge.Send(new OpenWinter.Net.HelloMessage { PlayerName = "probe", CharacterResRef = args[2], AreaReadyHandshake = true }.Write());
        var clock = Stopwatch.StartNew();
        var seconds = double.Parse(Opt("--seconds", "10"));
        var walk = Opt("--walk", "");
        var say = Opt("--say", "");
        var uses = new Queue<string>(Opt("--use", "").Split(',', StringSplitOptions.RemoveEmptyEntries));
        var nextUse = 6.0;
        var objects = new Dictionary<uint, (string Name, OpenWinter.Net.ObjectKind Kind, System.Numerics.Vector3 At)>();
        var counts = new SortedDictionary<string, int>();
        uint controlled = 0;
        System.Numerics.Vector3 at = default;
        bool walked = false;
        while (clock.Elapsed.TotalSeconds < seconds && bridge.IsConnected)
        {
            while (bridge.TryReceive(out var bytes))
            {
                var r = new OpenWinter.Net.MessageReader(bytes);
                var type = r.Type.ToString();
                counts[type] = counts.GetValueOrDefault(type) + 1;
                switch (r.Type)
                {
                    case OpenWinter.Net.MessageType.Welcome:
                        var w = OpenWinter.Net.WelcomeMessage.Read(r);
                        Console.WriteLine($"{clock.ElapsedMilliseconds,6} Welcome {w.ModuleName} haks [{string.Join(", ", w.Haks)}]");
                        break;
                    case OpenWinter.Net.MessageType.LoadArea:
                        var a = OpenWinter.Net.LoadAreaMessage.Read(r);
                        Console.WriteLine($"{clock.ElapsedMilliseconds,6} LoadArea {a.ResRef} \"{a.Name}\" {a.Width}x{a.Height} {a.Tileset} token {a.LoadToken}");
                        break;
                    case OpenWinter.Net.MessageType.Notify:
                        var n = OpenWinter.Net.NotifyMessage.Read(r);
                        Console.WriteLine($"{clock.ElapsedMilliseconds,6} Notify {n.Kind} {n.Json}");
                        if (n.Kind == "area-snapshot-ready")
                            bridge.Send(new OpenWinter.Net.ClientRequestMessage { Kind = "area-ready", Json = n.Json }.Write());
                        break;
                    case OpenWinter.Net.MessageType.Spawn:
                        var s = OpenWinter.Net.SpawnMessage.Read(r);
                        objects[s.ObjectId] = (s.Name, s.Kind, s.Position);
                        Console.WriteLine($"{clock.ElapsedMilliseconds,6} Spawn {s.ObjectId:X} {s.Kind} \"{s.Name}\" at {s.Position} facing {s.Facing:0.00} type {s.Appearance.Type} parts [{string.Join(",", s.Appearance.BodyParts)}] equip {s.Appearance.Equipment.Count} {s.Flags}");
                        break;
                    case OpenWinter.Net.MessageType.Update:
                        var u = OpenWinter.Net.UpdateMessage.Read(r);
                        if (u.ObjectId == controlled) at = u.Position;
                        if (objects.TryGetValue(u.ObjectId, out var o) && o.Kind != OpenWinter.Net.ObjectKind.Creature)
                            Console.WriteLine($"{clock.ElapsedMilliseconds,6} Update {u.ObjectId:X} {o.Kind} \"{o.Name}\" {u.Flags}");
                        else if (u.Mode != OpenWinter.Net.MovementMode.Stand || counts[type] < 5)
                            Console.WriteLine($"{clock.ElapsedMilliseconds,6} Update {u.ObjectId:X} {u.Mode} at {u.Position} facing {u.Facing:0.00} speed {u.Speed}");
                        break;
                    case OpenWinter.Net.MessageType.Despawn:
                        objects.Remove(OpenWinter.Net.DespawnMessage.Read(r).ObjectId);
                        break;
                    case OpenWinter.Net.MessageType.ControlObject:
                        controlled = OpenWinter.Net.ControlObjectMessage.Read(r).ObjectId;
                        Console.WriteLine($"{clock.ElapsedMilliseconds,6} ControlObject {controlled:X}");
                        break;
                    case OpenWinter.Net.MessageType.Chat:
                        var c = OpenWinter.Net.ChatMessage.Read(r);
                        Console.WriteLine($"{clock.ElapsedMilliseconds,6} Chat {c.Channel} {c.Speaker:X} \"{c.SpeakerName}\": {c.Text}");
                        break;
                    case OpenWinter.Net.MessageType.CharacterList:
                        Console.WriteLine($"{clock.ElapsedMilliseconds,6} CharacterList (the named character wasn't there)");
                        break;
                }
            }
            if (!walked && controlled != 0 && clock.Elapsed.TotalSeconds > 4)
            {
                walked = true;
                if (say.Length > 0)
                {
                    bridge.Send(new OpenWinter.Net.ChatSendMessage { Channel = OpenWinter.Net.ChatChannel.Talk, Text = say }.Write());
                    Console.WriteLine($"{clock.ElapsedMilliseconds,6} said \"{say}\"");
                }
            }
            if (walked && uses.Count > 0 && clock.Elapsed.TotalSeconds > nextUse)
            {
                nextUse = clock.Elapsed.TotalSeconds + 5;
                var name = uses.Dequeue();
                var here = at;
                var target = name.StartsWith('#') ? Convert.ToUInt32(name[1..], 16) : objects.Where(kv => kv.Value.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(kv => System.Numerics.Vector3.Distance(kv.Value.At, here)).Select(kv => kv.Key).FirstOrDefault();
                Console.WriteLine($"{clock.ElapsedMilliseconds,6} using \"{name}\": {target:X}");
                if (target != 0) bridge.Send(new OpenWinter.Net.ActionRequestMessage { Action = OpenWinter.Net.ActionKind.Use, Target = target }.Write());
            }
            if (walked && walk.Split(',') is [var dx, var dy])
            {
                walk = "";
                bridge.Send(new OpenWinter.Net.ActionRequestMessage { Action = OpenWinter.Net.ActionKind.MoveTo, Position = at + new System.Numerics.Vector3(float.Parse(dx), float.Parse(dy), 0), Run = false }.Write());
                Console.WriteLine($"{clock.ElapsedMilliseconds,6} walking from {at}");
            }
            Thread.Sleep(16);
        }
        Console.WriteLine(bridge.FailureReason is { } why ? $"failed: {why}" : "done");
        foreach (var (name, n) in counts) Console.WriteLine($"  {n,6}  {name}");
        Console.WriteLine($"{bridge.Session?.DecodeErrors.Count ?? 0} frames didn't decode");
        bridge.Disconnect();
        return 0;
    }
}
