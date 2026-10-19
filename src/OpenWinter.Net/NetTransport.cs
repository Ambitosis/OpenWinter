using System.Collections.Concurrent;
using LiteNetLib;

namespace OpenWinter.Net;

/// <summary>A remote peer over UDP (LiteNetLib). Messages are queued as they arrive during Poll.</summary>
public sealed class NetConnection : IConnection
{
    readonly NetPeer _peer;
    internal readonly ConcurrentQueue<byte[]> Inbox = new();
    internal bool Closed;

    public NetConnection(NetPeer peer) { _peer = peer; }

    public int Id => _peer.Id;
    public bool IsConnected => !Closed && _peer.ConnectionState == ConnectionState.Connected;

    /// <summary>Everything goes reliable-ordered for now; unordered snapshots come with delta compression.</summary>
    public void Send(byte[] message, bool reliable = true) => _peer.Send(message, DeliveryMethod.ReliableOrdered);

    public bool TryReceive(out byte[] message) => Inbox.TryDequeue(out message!);
    public void Disconnect() { Closed = true; _peer.Disconnect(); }
}

/// <summary>Listens for clients. Call <see cref="Poll"/> once per server tick.</summary>
public sealed class NetServerHost : IDisposable
{
    public const string ConnectionKey = NetworkVersion.HandshakeKey;
    readonly EventBasedNetListener _listener = new();
    readonly NetManager _manager;
    readonly Dictionary<int, NetConnection> _connections = new();
    public event Action<NetConnection>? Connected;

    public NetServerHost(int port, NetworkVersion? version = null)
    {
        version ??= NetworkVersion.Current;
        if (!version.Valid) throw new InvalidOperationException("Missing or invalid OpenWinter network build identity. Rebuild the engine.");
        _manager = new NetManager(_listener) { AutoRecycle = true, DisconnectTimeout = 15000 };
        _listener.ConnectionRequestEvent += r =>
        {
            if (NetworkVersion.TryRead(r.Data, out var client) && client.Mismatch(version) == null) r.Accept();
            else r.Reject(version.Write());
        };
        _listener.PeerConnectedEvent += peer =>
        {
            // The client verifies our identity before it releases its queued Hello or other gameplay input.
            peer.Send(version.Write(), DeliveryMethod.ReliableOrdered);
            var c = new NetConnection(peer);
            _connections[peer.Id] = c;
            Connected?.Invoke(c);
        };
        _listener.PeerDisconnectedEvent += (peer, _) =>
        {
            if (_connections.Remove(peer.Id, out var c)) c.Closed = true;
        };
        _listener.NetworkReceiveEvent += (peer, reader, _, _) =>
        {
            if (_connections.TryGetValue(peer.Id, out var c)) c.Inbox.Enqueue(reader.GetRemainingBytes());
        };
        if (!_manager.Start(port)) throw new InvalidOperationException($"Cannot listen on UDP port {port}");
    }

    public int Port => _manager.LocalPort;
    public void Poll() => _manager.PollEvents();
    public void Dispose() => _manager.Stop();
}

/// <summary>Client side of a UDP connection. Poll every frame.</summary>
public sealed class NetClientConnection : IConnection, IDisposable
{
    readonly EventBasedNetListener _listener = new();
    readonly NetManager _manager;
    readonly ConcurrentQueue<byte[]> _inbox = new();
    NetPeer? _peer;
    readonly Queue<byte[]> _pending = new();
    readonly long _started = System.Diagnostics.Stopwatch.GetTimestamp();
    bool _ready, _closed;
    public string? FailureReason { get; private set; }

    public NetClientConnection(string host, int port, NetworkVersion? version = null)
    {
        version ??= NetworkVersion.Current;
        if (!version.Valid) throw new InvalidOperationException("Missing or invalid OpenWinter network build identity. Rebuild the engine.");
        _manager = new NetManager(_listener) { AutoRecycle = true, DisconnectTimeout = 15000 };
        _listener.NetworkReceiveEvent += (peer, reader, _, _) =>
        {
            if (_closed) return;
            if (_ready) { _inbox.Enqueue(reader.GetRemainingBytes()); return; }
            if (!NetworkVersion.TryRead(reader, out var server))
            {
                Fail("The server did not provide a supported version handshake. Update the server and client to the same OpenWinter build.");
                peer.Disconnect();
                return;
            }
            if (version.Mismatch(server) is { } mismatch) { Fail(mismatch); peer.Disconnect(); return; }
            _ready = true;
            while (_pending.Count > 0) peer.Send(_pending.Dequeue(), DeliveryMethod.ReliableOrdered);
        };
        _listener.PeerDisconnectedEvent += (_, info) =>
        {
            if (_closed) return;
            string reason = _ready
                ? info.Reason == DisconnectReason.RemoteConnectionClose
                    ? "The server closed the connection."
                    : "The connection to the server was lost or timed out."
                : info.Reason == DisconnectReason.ConnectionRejected
                ? NetworkVersion.TryRead(info.AdditionalData, out var server)
                    ? version.Mismatch(server) ?? "The server refused the connection."
                    : "The server refused the version handshake. It may be an older OpenWinter build. Update the server and client to the same build."
                : "The connection to the server was lost or timed out. Check the address and network, then reconnect.";
            Fail(reason);
        };
        if (!_manager.Start()) throw new InvalidOperationException("Cannot start the UDP client.");
        _peer = _manager.Connect(host, port, version.Write());
    }

    public int Id => 0;
    // Pending connections stay alive while PollEvents performs the handshake, but failures never do.
    public bool IsConnected => !_closed;
    /// <summary>The handshake succeeded, so a later failure is a disconnect from a game in progress.</summary>
    public bool Joined => _ready;

    void Fail(string reason)
    {
        FailureReason = reason;
        _closed = true;
        _pending.Clear();
        // Keep what a joined server sent before it hung up (e.g. a boot message); handshake failures have nothing to keep.
        if (!_ready) _inbox.Clear();
    }

    public void Send(byte[] message, bool reliable = true)
    {
        if (_closed) return;
        if (_ready && _peer is { ConnectionState: ConnectionState.Connected } p) p.Send(message, DeliveryMethod.ReliableOrdered);
        else _pending.Enqueue(message);
    }

    public bool TryReceive(out byte[] message)
    {
        _manager.PollEvents();
        if (!_ready && !_closed && System.Diagnostics.Stopwatch.GetElapsedTime(_started).TotalSeconds > 10)
        {
            Fail("The server did not complete the version handshake. Check the address and use the same OpenWinter build as the server.");
            _manager.DisconnectAll();
        }
        return _inbox.TryDequeue(out message!);
    }

    public void Disconnect()
    {
        _closed = true;
        _pending.Clear();
        _inbox.Clear();
        _manager.DisconnectAll();
    }

    public void Dispose() { Disconnect(); _manager.Stop(); }
}
