using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>How to reach an NWN:EE server and log in to it.</summary>
public sealed record NwnBridgeOptions(Uri Server, NwnCdKey Key, string Password = "")
{
    /// <summary>Records every frame (direction, milliseconds, hex) to this gzipped JSON-lines file.</summary>
    public string? RecordTo { get; init; }

    /// <summary>Opens the transport; tests replace it with an in-memory one.</summary>
    public Func<Uri, INwnTransport> Connect { get; init; } = NwnWebSocketTransport.Connect;
}

/// <summary>
/// A connection to an NWN:EE server (over WSNetLayer) that the client uses like a connection to an OpenWinter
/// server: OpenWinter messages from the client become NWN messages, and NWN messages from the server come back as
/// OpenWinter messages. Driven from the client's frame loop through <see cref="TryReceive"/>.
/// </summary>
public sealed class NwnBridgeConnection : IConnection, IConnectionStatus
{
    static int _nextId;
    readonly NwnBridgeOptions _options;
    readonly INwnBridgeData _data;
    readonly ConcurrentQueue<byte[]> _toClient = new();
    readonly NwnWorldMirror _world;
    readonly NwnAbilities _abilities;
    readonly NwnSessionContext _context;
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly List<NwnMessage> _received = [];
    INwnTransport? _transport;
    NwnSession? _session;
    bool _welcomed, _closed;
    CharListMessages.CharListListResponse? _heldCharacters;
    string _wantedCharacter = "";
    byte _eventNumber;
    double _lastTick;
    DriveInputMessage? _drive;
    bool _driving;
    System.Numerics.Vector3 _mouseTarget;

    public NwnBridgeConnection(NwnBridgeOptions options, INwnBridgeData data)
    {
        _options = options;
        _data = data;
        _context = new NwnSessionContext(data.TwoDARowCount, data.TwoDA);
        _world = new NwnWorldMirror(data, m => _toClient.Enqueue(m));
        _abilities = new NwnAbilities(data, m => _toClient.Enqueue(m), ToServer);
    }

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsConnected => !_closed && _session?.State != NwnSessionState.Failed;
    /// <summary>Why the connection failed or was refused; null while it works.</summary>
    public string? FailureReason => _session?.Failure;
    public bool Joined => _session?.State == NwnSessionState.Ready;
    /// <summary>The session, once the client has said hello (diagnostics).</summary>
    public NwnSession? Session => _session;

    public void Send(byte[] message, bool reliable = true)
    {
        if (_closed || message.Length == 0) return;
        var r = new MessageReader(message);
        switch (r.Type)
        {
            case MessageType.Hello:
                Begin(HelloMessage.Read(r));
                break;
            case MessageType.CharacterChoice:
                var choice = CharacterChoiceMessage.Read(r);
                if (choice.File.Length > 0) ToServer(new LoginMessages.LoginServerSubDirCharacter { Resref = choice.File });
                else Tell("Characters from the local vault can't be used on this server yet.");
                break;
            case MessageType.ChatSend:
                Chat(ChatSendMessage.Read(r));
                break;
            case MessageType.ActionRequest:
                Action(ActionRequestMessage.Read(r));
                break;
            case MessageType.ClientRequest:
                Request(ClientRequestMessage.Read(r));
                break;
            case MessageType.DriveInput:
                _drive = DriveInputMessage.Read(r);
                break;
        }
    }

    public bool TryReceive(out byte[] message)
    {
        // The client drains messages in a loop each frame: pump once, when it has taken everything.
        if (_toClient.IsEmpty) Pump();
        return _toClient.TryDequeue(out message!);
    }

    public void Disconnect()
    {
        _closed = true;
        (_session?.FrameObserver?.Target as IDisposable)?.Dispose();
        _session?.Close("disconnected");
        _transport?.Dispose();
    }

    // -- client to server ---------------------------------------------------

    void Begin(HelloMessage hello)
    {
        if (_session != null) return;
        var name = string.IsNullOrWhiteSpace(hello.PlayerName) ? "Player" : hello.PlayerName;
        _wantedCharacter = hello.CharacterResRef;
        _transport = _options.Connect(_options.Server);
        _session = new NwnSession(_transport, new NwnLogin(name, _options.Key, _options.Password), _context);
        if (_options.RecordTo is { } path) _session.FrameObserver = NwnRecording.Writer(path, _clock);
    }

    void ToServer(NwnMessage m)
    {
        if (_session?.State == NwnSessionState.Ready) _session.Send(m);
    }

    void Chat(ChatSendMessage chat)
    {
        NwnMessage? m = chat.Channel switch
        {
            ChatChannel.Talk => new ChatMessages.ChatTalkC2S { Message = chat.Text },
            ChatChannel.Shout => new ChatMessages.ChatShoutC2S { Message = chat.Text },
            ChatChannel.Whisper => new ChatMessages.ChatWhisperC2S { Message = chat.Text },
            ChatChannel.Party => new ChatMessages.ChatPartyC2S { Message = chat.Text },
            _ => null,
        };
        if (m != null) ToServer(m);
    }

    void Action(ActionRequestMessage a)
    {
        switch (a.Action)
        {
            case ActionKind.MoveTo:
                ToServer(new InputMessages.InputWalkToWayPoint
                {
                    Area = _world.AreaId,
                    Position = (a.Position.X, a.Position.Y, a.Position.Z),
                    EventNumber = ++_eventNumber,
                    Run = a.Run,
                    ClickedOn = GameObjUpdateMessages.InvalidObjectId,
                });
                break;
            case ActionKind.PickUp or ActionKind.Use when _world.KindOf(a.Target) == ObjectKind.Item:
                // Into the backpack wherever it fits: no container, no grid position.
                ToServer(new InventoryMessages.InventoryPickupC2S { Item = a.Target, TargetRepository = GameObjUpdateMessages.InvalidObjectId, X = 255, Y = 255 });
                break;
            case ActionKind.Use or ActionKind.Talk or ActionKind.OpenDoor or ActionKind.CloseDoor:
                // Doors change state; everything else is used (the server walks the player there first).
                ToServer((NwnMessage?)_world.DoorRequest(a.Target, a.Action == ActionKind.Use) ?? new InputMessages.InputUseObject { Target = a.Target, LeftClick = true });
                break;
            case ActionKind.Attack:
                ToServer(new InputMessages.InputAttack { Target = a.Target });
                break;
        }
    }

    /// <summary>
    /// Keyboard driving: the player's creature moves here, ahead of the server, and the server is told where it is
    /// and which way it faces ten times a second.
    /// </summary>
    void Drive(float dt)
    {
        const float TurnRate = 2.2f;
        if (_drive is { Active: true, Mouse: true } held)
        {
            // Holding the mouse button: walk or run towards the point under the cursor.
            if (System.Numerics.Vector3.Distance(held.Direction, _mouseTarget) > 0.5f)
            {
                _mouseTarget = held.Direction;
                Action(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = held.Direction, Run = !held.Walk });
            }
            return;
        }
        if (_drive is not { Active: true } d)
        {
            // Released: the last position, with no movement flag, stops the creature there.
            if (_driving && _world.TryPose(_world.Controlled, out var last, out var lastFacing, out _, out _))
                ToServer(new InputMessages.InputDriveControl { X = last.X, Y = last.Y, Area = _world.AreaId, Bearing = Bearing(lastFacing), PathfindEventNumber = _eventNumber });
            if (_driving) _driving = _world.Driving = false;
            return;
        }
        // While driving, the client's position and facing lead and the server's echoes are ignored (NwnWorldMirror.Driving).
        if (!_world.TryPose(_world.Controlled, out var at, out var facing, out var walkRate, out var runRate)) return;
        if (!_driving) _driving = _world.Driving = true;
        facing = MathF.IEEERemainder(facing + Math.Clamp(d.Turn, -1, 1) * TurnRate * dt, MathF.Tau);
        var ahead = new System.Numerics.Vector2(MathF.Cos(facing), MathF.Sin(facing));
        var step = ahead * Math.Clamp(d.Direction.Y, -1, 1) + new System.Numerics.Vector2(ahead.Y, -ahead.X) * Math.Clamp(d.Direction.X, -1, 1);
        if (step.LengthSquared() > 1) step = System.Numerics.Vector2.Normalize(step);
        var walking = d.Walk || d.Direction.Y <= 0;
        step *= (walking ? walkRate : runRate) * dt;
        var to = at + new System.Numerics.Vector3(step, 0);
        _world.Predict(_world.Controlled, to, facing, step == default ? MovementMode.Stand : walking ? MovementMode.Walk : MovementMode.Run);
        var forward = Math.Clamp(d.Direction.Y, -1, 1);
        var right = Math.Clamp(d.Direction.X, -1, 1);
        ToServer(new InputMessages.InputDriveControl
        {
            X = to.X, Y = to.Y, Area = _world.AreaId, Bearing = Bearing(facing), PathfindEventNumber = _eventNumber,
            DriveFlags = step == default ? (byte)0 : DriveFlags(walking, forward, right),
        });
    }

    /// <summary>A facing as the drive message's bearing: tenths of a degree counter-clockwise from +X.</summary>
    public static ushort Bearing(float facing) => (ushort)(((int)MathF.Round(facing * 1800f / MathF.PI) % 3600 + 3600) % 3600);

    /// <summary>
    /// The drive message's flags: moving (1), running (2), strafing right (8); none when standing (turning in place)
    /// or released.
    /// </summary>
    public static byte DriveFlags(bool walking, float forward = 1, float right = 0) =>
        forward == 0 && right > 0 ? (byte)9 : (byte)(walking ? 1 : 3);

    void Request(ClientRequestMessage request)
    {
        if (NwnNui.FromClient(request.Kind, request.Json) is { } nui)
        {
            ToServer(nui);
            return;
        }
        if (_abilities.FromClient(request.Kind, request.Json)) return;
        switch (request.Kind)
        {
            case "area-ready":
                // The client built the area: the server may send the rest and let the player in.
                if (JsonDocument.Parse(request.Json).RootElement.GetProperty("token").GetUInt32() == _world.LoadToken)
                    ToServer(new AreaMessages.AreaAreaLoaded());
                break;
        }
    }

    // -- server to client ---------------------------------------------------

    void Pump()
    {
        if (_session == null || _closed) return;
        var wasReady = _session.State == NwnSessionState.Ready;
        _received.Clear();
        _session.Pump(_received);
        if (!wasReady && _session.State == NwnSessionState.Ready)
        {
            _session.Send(new ServerStatusMessages.ServerStatusRequest());
            _session.Send(new CharListMessages.CharListRequest());
        }
        foreach (var m in _received) FromServer(m);
        // Moving creatures report ten times a second; the client interpolates between.
        var now = _clock.Elapsed.TotalSeconds;
        if (now - _lastTick < 0.1) return;
        var dt = (float)Math.Min(0.25, now - _lastTick);
        _world.Tick(dt);
        Drive(dt);
        _lastTick = now;
    }

    void FromServer(NwnMessage message)
    {
        switch (message)
        {
            case ServerStatusMessages.ServerStatusModuleRunning status:
                Welcome(status);
                break;
            case CharListMessages.CharListListResponse list:
                if (_welcomed) Characters(list);
                else _heldCharacters = list;
                break;
            case ModuleMessages.ModuleInfo info:
                _world.ModuleInfo(info);
                ToServer(new ModuleMessages.ModuleLoaded());
                break;
            case ModuleMessages.ModuleTime time:
                _world.Time(time);
                break;
            case AreaMessages.AreaClientArea area:
                _world.Area(area);
                break;
            case GameObjUpdateMessages.GameObjUpdateObjList list:
                _world.ObjectList(list);
                _abilities.ObjectList(list);
                break;
            case GameObjUpdateMessages.GameObjUpdateObjControl control:
                _world.Control(control.ObjectId);
                _abilities.Player = control.ObjectId;
                break;
            case GuiQuickbarMessages.GuiQuickbarSetAllButtons quickbar:
                _abilities.Quickbar(quickbar);
                break;
            case GuiQuickbarMessages.GuiQuickbarSetButton button:
                _abilities.QuickbarButton(button);
                break;
            case ClientSideMessageFeedbackMessages.ClientSideMessageFeedback feedback:
                NwnFeedback.ToClient(feedback, _data, m => _toClient.Enqueue(m));
                break;
            case LoginMessages.LoginDeny or LoginMessages.LoginCharacterFail:
                Tell("The server refused the character.");
                break;
            default:
                if (!NwnNui.ToClient(message, m => _toClient.Enqueue(m)))
                    NwnChat.ToClient(message, _data, m => _toClient.Enqueue(m));
                break;
        }
    }

    void Welcome(ServerStatusMessages.ServerStatusModuleRunning status)
    {
        if (_welcomed) return;
        _welcomed = true;
        // The server's haks and talk table override the base game's 2DAs and text from here on.
        _data.UseContent(status.Haks, status.AltTlk);
        _toClient.Enqueue(new WelcomeMessage
        {
            ModuleName = _session?.Settings?.ModuleName ?? status.ResourceName,
            CustomTlk = status.AltTlk,
            Haks = [.. status.Haks],
        }.Write());
        if (_heldCharacters is { } held)
        {
            _heldCharacters = null;
            Characters(held);
        }
    }

    void Characters(CharListMessages.CharListListResponse list)
    {
        // A character named up front (--character) is chosen without the selection screen.
        if (_wantedCharacter.Length > 0 && list.Characters.FirstOrDefault(c => c.Resref.Equals(_wantedCharacter, StringComparison.OrdinalIgnoreCase)) is { } wanted)
        {
            ToServer(new LoginMessages.LoginServerSubDirCharacter { Resref = wanted.Resref });
            return;
        }
        _toClient.Enqueue(new CharacterListMessage
        {
            Mode = VaultMode.Server,
            Characters = list.Characters.Select(c => new CharacterEntry
            {
                File = c.Resref,
                Name = $"{Text(c.FirstName)} {Text(c.LastName)}".Trim(),
                Classes = string.Join(" / ", c.Classes.Select(k => $"{ClassName(k.Item1)} {k.Item2}")),
                Portrait = c.PortraitResref,
                Level = c.Classes.Sum(k => k.Item2),
            }).ToList(),
        }.Write());
    }

    string ClassName(int cls) =>
        uint.TryParse(_data.TwoDA("classes", cls, "Name"), out var strRef) ? _data.Text(strRef, 0) : cls.ToString();

    string Text(NwnLocString s) => s.IsStrRef ? _data.Text(s.StrRef, s.Gender) : s.Text;

    void Tell(string text) => _toClient.Enqueue(new ChatMessage { Channel = ChatChannel.Server, Text = text }.Write());
}
