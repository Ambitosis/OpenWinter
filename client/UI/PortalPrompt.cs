using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>What a script's ActivatePortal asked for (the server's "portal" notify).</summary>
public sealed record PortalRequest(string Address, string Password, string Waypoint, bool Seamless)
{
    /// <summary>The address without its port, as the prompt shows it.</summary>
    public string Host => Split().Host;
    public int Port => Split().Port;

    (string Host, int Port) Split()
    {
        int colon = Address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(Address[(colon + 1)..], out var port)) return (Address[..colon], port);
        return (Address, 5121);
    }
}

/// <summary>One look of the portal prompt: the classic one (ingame_portal) or the modern one (<see cref="PortalDialog"/>).</summary>
public interface IPortalView
{
    /// <summary>The server's address and the password the script gave (the player may change it).</summary>
    void Show(PortalRequest request);
    /// <summary>The server box's lines ("Checking For Server", then what the server said) and whether Join can be pressed.</summary>
    void SetStatus(IReadOnlyList<string> lines, bool canJoin);
    void Close();
    /// <summary>The password typed in the box.</summary>
    string Password { get; }
    event Action? Cancelled;
    event Action? JoinPressed;
}

/// <summary>
/// ActivatePortal on the client, as measured with nwmain on nwserver (scene mp_portal; docs/CLASSIC_UI.md "Portal
/// prompt"). Single player shows nothing. On a server the player gets the "Server Info" prompt while the address is
/// checked ("Checking For Server" and its host; Join greyed); a server that answers is listed (name, module, players
/// and levels, password, local characters) and Join lights up. The password box holds the script's password in plain
/// text. "You are portalling, you can't do anything right now." starts the portalling state; "You are no longer
/// portalling, you can do things again." ends it, on Cancel or when the check gives up (about 10 s; the prompt then
/// stays as it was). Another portal while the prompt is open replaces its server and password; one with an empty
/// address leaves it as it is. A seamless portal joins at once. Join leaves this game for that server, which places
/// the player at the portal's waypoint.
/// </summary>
public partial class PortalPrompt : Node
{
    public ClientSession Session = null!;
    public bool SinglePlayer;
    /// <summary>Where the modern prompt goes (the front-end window layer).</summary>
    public Control ModernHost = null!;
    /// <summary>A chat feedback line.</summary>
    public Action<string>? Feedback;
    /// <summary>Leave this game and join the portal's server.</summary>
    public Action<PortalRequest>? Join;
    /// <summary>Set while the classic interface is attached: builds the classic prompt instead of the modern one.</summary>
    public static Func<IPortalView?>? ClassicView;

    IPortalView? _view;
    PortalRequest? _request;
    ServerCheck? _check;
    bool _canJoin, _portalling;

    public IPortalView? View => _view;
    public bool Open => _view != null;

    public override void _Ready() => Session.NotifyReceived += OnNotify;
    public override void _ExitTree() { Session.NotifyReceived -= OnNotify; _check?.Dispose(); }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "portal") return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        string S(string k) => d.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
        Activate(new PortalRequest(S("address"), S("password"), S("waypoint"), d.TryGetProperty("seamless", out var s) && s.GetBoolean()));
    }

    /// <summary>A portal for this player (also the captures' way in).</summary>
    public void Activate(PortalRequest request)
    {
        // Single player shows no prompt and prints nothing (scene portalprompt on nwmain).
        if (SinglePlayer) return;
        if (request.Seamless && request.Address.Trim().Length > 0) { Join?.Invoke(request); return; }
        SetPortalling(true);
        // An empty address leaves an open prompt as it was and opens none (mp_portal "b", "f"), though the player is
        // portalling from then on.
        if (request.Address.Trim().Length == 0) return;
        _request = request;
        if (_view == null)
        {
            _view = ClassicView?.Invoke() ?? PortalDialog.Create(ModernHost);
            _view.Cancelled += Close;
            _view.JoinPressed += () => { if (_canJoin) JoinNow(); };
        }
        _view.Show(request);
        _canJoin = false;
        _view.SetStatus(new[] { Text(8131, "Checking For Server"), request.Host }, false);
        _check?.Dispose();
        _check = new ServerCheck(request.Host, request.Port);
    }

    void SetPortalling(bool on)
    {
        if (_portalling == on) return;
        _portalling = on;
        Feedback?.Invoke(on ? Text(53292, "You are portalling, you can't do anything right now.")
            : Text(53293, "You are no longer portalling, you can do things again."));
    }

    public override void _Process(double delta)
    {
        if (_check == null || _view == null || _request == null) return;
        var result = _check.Poll();
        if (result == null) return;
        _check.Dispose();
        _check = null;
        if (result.Answered) { _canJoin = true; _view.SetStatus(InfoLines(result), true); }
        // The original gives up after about 10 s: the prompt stays, still checking, with Join greyed.
        else SetPortalling(false);
    }

    /// <summary>Shows an answer as if the server had sent it (captures).</summary>
    public void ShowAnswer(ServerCheck.Result answer)
    {
        if (_view == null) return;
        _check?.Dispose();
        _check = null;
        _canJoin = true;
        _view.SetStatus(InfoLines(answer), true);
    }

    /// <summary>
    /// What the prompt lists of a server that answered, as the original words it (mp_portal e_01): "Server Name: x",
    /// "Module Name: y", "Players: (1/3) Levels: 1 - 40", "Password Required: False", "Local Characters Allowed: False".
    /// </summary>
    List<string> InfoLines(ServerCheck.Result r) => new()
    {
        $"{Text(8134, "Server Name")}: {r.Name}",
        $"{Text(8135, "Module Name")}: {r.Module}",
        $"{Text(8136, "Players")}: ({r.Players}/{r.MaxPlayers}) {Text(8137, "Levels")}: {r.MinLevel} - {r.MaxLevel}",
        $"{Text(8138, "Password Required")}: {Bool(r.Password)}",
        $"{Text(57998, "Local Characters Allowed")}: {Bool(r.LocalCharacters)}",
    };

    string Bool(bool b) => b ? Text(8141, "True") : Text(8142, "False");

    /// <summary>A button of the open prompt, as if clicked ("cancel" or "join"; Join only once the server answered).</summary>
    public void Press(string button)
    {
        if (button == "cancel") Close();
        else if (button == "join" && _canJoin) JoinNow();
    }

    void JoinNow()
    {
        if (_request == null || _view == null) return;
        var request = _request with { Password = _view.Password };
        // The original goes straight to the loading screen: no "no longer portalling" line.
        _portalling = false;
        Close();
        Join?.Invoke(request);
    }

    void Close()
    {
        if (_view == null) return;
        var view = _view;
        _view = null;
        _request = null;
        _check?.Dispose();
        _check = null;
        view.Close();
        SetPortalling(false);
    }

    string Text(uint strref, string fallback) => Session.Content?.Resources.TlkString(strref) is { Length: > 0 } s ? s : fallback;

    /// <summary>
    /// Asks a server what it is before joining: a connection that only sends the "server-info" request (no Hello, so
    /// no player joins) and reads the answer. OpenWinter servers answer it; a server that never completes the
    /// handshake fails after 10 s (NetClientConnection), about when the original gives up too.
    /// </summary>
    public sealed class ServerCheck : IDisposable
    {
        public sealed record Result(bool Answered, string Name = "", string Module = "", int Players = 0, int MaxPlayers = 0,
            int MinLevel = 1, int MaxLevel = 40, bool Password = false, bool LocalCharacters = false);

        readonly NetClientConnection? _connection;

        public ServerCheck(string host, int port)
        {
            try
            {
                _connection = new NetClientConnection(host, port);
                _connection.Send(new ClientRequestMessage { Kind = "server-info" }.Write());
            }
            catch (Exception e) when (e is InvalidOperationException or System.Net.Sockets.SocketException or ArgumentException)
            {
                GD.PrintErr("[portal] " + e.Message);
            }
        }

        /// <summary>The answer once there is one, a failed one when the server never answered; null while waiting.</summary>
        public Result? Poll()
        {
            if (_connection == null) return new Result(false);
            while (_connection.TryReceive(out var raw))
            {
                var r = new MessageReader(raw);
                if (r.Type != MessageType.Notify) continue;
                var n = NotifyMessage.Read(r);
                if (n.Kind != "server-info") continue;
                using var doc = JsonDocument.Parse(n.Json);
                var d = doc.RootElement;
                string S(string k) => d.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
                int I(string k, int fallback = 0) => d.TryGetProperty(k, out var v) && v.TryGetInt32(out var i) ? i : fallback;
                bool B(string k) => d.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;
                return new Result(true, S("name"), S("module"), I("players"), I("maxPlayers"), I("minLevel", 1), I("maxLevel", 40),
                    B("password"), B("localCharacters"));
            }
            return _connection.FailureReason != null ? new Result(false) : null;
        }

        public void Dispose() => _connection?.Dispose();
    }
}
