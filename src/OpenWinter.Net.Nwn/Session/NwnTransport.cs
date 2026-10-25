using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace OpenWinter.Net.Nwn.Session;

/// <summary>Carries whole frames to and from a server. Frames are queued; the owner drains them on its own thread.</summary>
public interface INwnTransport : IDisposable
{
    bool IsOpen { get; }
    /// <summary>Why the connection ended or failed; null while it is open or still connecting.</summary>
    string? CloseReason { get; }
    void Send(byte[] frame);
    bool TryReceive(out byte[] frame);
    void Close(string reason);
}

/// <summary>
/// A WSNetLayer connection: one binary websocket frame per packet or message. Uses permessage-deflate when the server
/// offers it, which stands in for the zlib compression of the UDP protocol.
/// </summary>
public sealed class NwnWebSocketTransport : INwnTransport
{
    readonly ClientWebSocket _ws = new();
    readonly ConcurrentQueue<byte[]> _inbox = new();
    readonly BlockingCollection<byte[]> _outbox = new();
    readonly CancellationTokenSource _cts = new();
    /// <summary>Largest frame accepted from the server; a bigger one ends the connection instead of growing without bound.</summary>
    public const int MaxFrameBytes = 32 * 1024 * 1024;
    volatile string? _closeReason;
    volatile bool _open;

    public bool IsOpen => _open;
    public string? CloseReason => _closeReason;

    NwnWebSocketTransport()
    {
        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(10);
        _ws.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
    }

    /// <summary>
    /// Connects to ws://host:port or wss://host:port (a TLS proxy in front of the plugin). Returns at once; the
    /// connection opens in the background and <see cref="CloseReason"/> reports a failure.
    /// </summary>
    public static NwnWebSocketTransport Connect(Uri server)
    {
        var t = new NwnWebSocketTransport();
        _ = t.Run(WithProtocolFlags(server));
        return t;
    }

    /// <summary>The URI with WSNetLayer's protocol flags, as its reference client sends them.</summary>
    public static Uri WithProtocolFlags(Uri server)
    {
        var b = new UriBuilder(server);
        if (!b.Query.Contains("proto_flags=")) b.Query = (b.Query.TrimStart('?') is { Length: > 0 } q ? q + "&" : "") + "proto_flags=0x100";
        return b.Uri;
    }

    async Task Run(Uri uri)
    {
        try
        {
            await _ws.ConnectAsync(uri, _cts.Token);
            _open = true;
            var sender = Task.Run(SendLoop);
            await ReceiveLoop();
            await sender;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            _closeReason ??= e is WebSocketException ws ? $"connection failed: {ws.Message}" : e.Message;
        }
        finally
        {
            _open = false;
            _closeReason ??= "connection closed";
            _outbox.CompleteAdding();
        }
    }

    async Task ReceiveLoop()
    {
        var buffer = new byte[64 * 1024];
        using var frame = new MemoryStream();
        while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            var result = await _ws.ReceiveAsync(buffer, _cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                _closeReason ??= string.IsNullOrEmpty(result.CloseStatusDescription)
                    ? $"server closed the connection ({result.CloseStatus})"
                    : result.CloseStatusDescription;
                return;
            }
            if (frame.Length + result.Count > MaxFrameBytes)
            {
                _closeReason ??= "the server sent an oversized frame";
                return;
            }
            frame.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;
            // Text frames are reserved for out-of-band use; only binary frames carry the protocol.
            if (result.MessageType == WebSocketMessageType.Binary) _inbox.Enqueue(frame.ToArray());
            frame.SetLength(0);
        }
    }

    async Task SendLoop()
    {
        foreach (var frame in _outbox.GetConsumingEnumerable(_cts.Token))
        {
            if (_ws.State != WebSocketState.Open) return;
            await _ws.SendAsync(frame, WebSocketMessageType.Binary, true, _cts.Token);
        }
    }

    public void Send(byte[] frame)
    {
        if (!_outbox.IsAddingCompleted) _outbox.TryAdd(frame);
    }

    public bool TryReceive(out byte[] frame) => _inbox.TryDequeue(out frame!);

    public void Close(string reason)
    {
        _closeReason ??= reason;
        try
        {
            if (_ws.State == WebSocketState.Open)
                _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None).Wait(1000);
        }
        catch (Exception)
        {
            // Closing a dead socket is not an error.
        }
        _cts.Cancel();
    }

    public void Dispose()
    {
        Close("disconnected");
        _ws.Dispose();
        _cts.Dispose();
    }
}
