using System.Collections.Concurrent;

namespace OpenWinter.Net;

/// <summary>One end of a client/server link. Received messages are queued and drained by the owner's update loop.</summary>
public interface IConnection
{
    int Id { get; }
    bool IsConnected { get; }
    void Send(byte[] message, bool reliable = true);
    bool TryReceive(out byte[] message);
    void Disconnect();
}

/// <summary>An in-process link (single-player): two queues, no sockets. Messages still go through serialization.</summary>
public sealed class LoopbackConnection : IConnection
{
    readonly ConcurrentQueue<byte[]> _inbox = new();
    LoopbackConnection? _peer;
    static int _nextId;

    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public bool IsConnected { get; private set; } = true;

    public static (LoopbackConnection Client, LoopbackConnection Server) CreatePair()
    {
        var a = new LoopbackConnection();
        var b = new LoopbackConnection();
        a._peer = b;
        b._peer = a;
        return (a, b);
    }

    public void Send(byte[] message, bool reliable = true)
    {
        if (IsConnected && _peer is { IsConnected: true }) _peer._inbox.Enqueue(message);
    }

    public bool TryReceive(out byte[] message) => _inbox.TryDequeue(out message!);

    public void Disconnect()
    {
        IsConnected = false;
        if (_peer != null) _peer.IsConnected = false;
    }
}
