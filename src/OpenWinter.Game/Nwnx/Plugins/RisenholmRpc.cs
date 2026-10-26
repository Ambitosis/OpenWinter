using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Risenholm's RPC listener (plugin README): with NWNX_RISENHOLM_RPC_PORT and a secret of 16 characters or more
/// in NWNX_RISENHOLM_RPC_SECRET, "POST /rpc/&lt;name&gt;" with "Authorization: Bearer &lt;secret&gt;" runs the script
/// &lt;prefix&gt;&lt;name&gt; (NWNX_RISENHOLM_RPC_SCRIPT_PREFIX, default pw_rpc_) with the module as OBJECT_SELF at the next server
/// step, and answers with what it set through SetRpcResponse as application/json: 200, or 401 (secret), 404 (no such
/// script), 500 (no answer), 503 (no module yet), 504 (not run within 5 seconds; it is then never run). The secret
/// is checked before any of the body is read; requests are capped at 100 header lines of 8 KiB and a 64 KiB body.
/// </summary>
public sealed class RisenholmRpc : IWorldSystem
{
    const int MaxHeaderLines = 100, MaxHeaderLine = 8192, MaxBody = 64 * 1024, Workers = 4, Backlog = 32;
    static readonly Regex NamePattern = new("^[a-z0-9_]+$", RegexOptions.CultureInvariant);

    readonly World _world;
    bool _checked;
    TcpListener? _listener;
    byte[] _secret = [];
    string _prefix = "pw_rpc_";
    readonly SemaphoreSlim _workers = new(Workers);
    DateTime _lastRejectLog = DateTime.MinValue;
    int _rejectsSinceLog;

    public RisenholmRpc(World world) { _world = world; }

    /// <summary>The port the listener took (0 when off).</summary>
    public int Port => (_listener?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    public void Step(float dt)
    {
        if (_checked) return;
        _checked = true;
        var reg = _world.System<NwnxRegistry>();
        int port = reg.EnvInt("NWNX_RISENHOLM_RPC_PORT");
        if (port <= 0) return;
        var secret = reg.Env("NWNX_RISENHOLM_RPC_SECRET") ?? "";
        if (secret.Length < 16)
        {
            reg.Log("[nwnx] Risenholm: RPC listener not started: NWNX_RISENHOLM_RPC_SECRET must be at least 16 characters");
            return;
        }
        _prefix = reg.Env("NWNX_RISENHOLM_RPC_SCRIPT_PREFIX") ?? "pw_rpc_";
        _secret = Encoding.UTF8.GetBytes(secret);
        var bind = reg.Env("NWNX_RISENHOLM_RPC_BIND") ?? "127.0.0.1";
        try
        {
            _listener = new TcpListener(IPAddress.Parse(bind), port);
            _listener.Start(Backlog);
        }
        catch (Exception e) when (e is SocketException or FormatException)
        {
            reg.Log($"[nwnx] Risenholm: RPC listener cannot listen on {bind}:{port}: {e.Message}");
            _listener = null;
            return;
        }
        reg.Log($"[nwnx] Risenholm: RPC listener on {bind}:{Port}");
        _ = Task.Run(AcceptLoop);
    }

    async Task AcceptLoop()
    {
        while (_listener is { } listener)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
            catch (Exception e) when (e is SocketException or ObjectDisposedException) { return; }
            if (!_workers.Wait(0))
            {
                // Every worker is busy: past the backlog a connection is closed at once.
                client.Dispose();
                continue;
            }
            _ = Task.Run(async () =>
            {
                try { await Handle(client).ConfigureAwait(false); }
                catch (Exception e) when (e is IOException or SocketException or ObjectDisposedException or OperationCanceledException) { }
                finally { client.Dispose(); _workers.Release(); }
            });
        }
    }

    public void Stop()
    {
        _listener?.Stop();
        _listener = null;
    }

    static async Task<string?> ReadLine(Stream s, CancellationToken ct)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (true)
        {
            if (await s.ReadAsync(one, ct).ConfigureAwait(false) == 0) return null;
            if (one[0] == '\n') break;
            if (one[0] != '\r') bytes.Add(one[0]);
            if (bytes.Count > MaxHeaderLine) return null;
        }
        return Encoding.Latin1.GetString(bytes.ToArray());
    }

    async Task Handle(TcpClient client)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var stream = client.GetStream();
        var ip = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
        var requestLine = await ReadLine(stream, cts.Token).ConfigureAwait(false);
        if (requestLine == null) return;
        var parts = requestLine.Split(' ');
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; ; i++)
        {
            if (i >= MaxHeaderLines) { await Reply(stream, 431, "").ConfigureAwait(false); return; }
            var line = await ReadLine(stream, cts.Token).ConfigureAwait(false);
            if (line == null) return;
            if (line.Length == 0) break;
            int colon = line.IndexOf(':');
            if (colon > 0) headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        // The secret first, before any of the body is read.
        var auth = headers.GetValueOrDefault("Authorization") ?? "";
        var given = auth.StartsWith("Bearer ", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes(auth[7..]) : [];
        if (!CryptographicOperations.FixedTimeEquals(given, _secret))
        {
            Rejected(ip);
            await Reply(stream, 401, "").ConfigureAwait(false);
            return;
        }
        if (parts.Length < 2 || parts[0] != "POST" || !parts[1].StartsWith("/rpc/", StringComparison.Ordinal))
        {
            await Reply(stream, 404, "").ConfigureAwait(false);
            return;
        }
        var name = parts[1][5..];
        var script = _prefix + name;
        if (!NamePattern.IsMatch(name) || script.Length > 16 || !_world.Resources.Contains(script, Aurora.Resources.ResType.Ncs))
        {
            await Reply(stream, 404, "").ConfigureAwait(false);
            return;
        }
        var body = await ReadBody(stream, headers, cts.Token).ConfigureAwait(false);
        if (body == null) { await Reply(stream, 413, "").ConfigureAwait(false); return; }
        if (_world.Module == null || _world.Scripts == null) { await Reply(stream, 503, "").ConfigureAwait(false); return; }

        var done = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int abandoned = 0;
        _world.System<NwnxRegistry>().Post(() =>
        {
            // A request nobody waits for any more is not run.
            if (Interlocked.CompareExchange(ref abandoned, 2, 0) != 0) return;
            done.TrySetResult(RisenholmPlugin.RunRpc(_world, script, Encoding.Latin1.GetString(body)));
        });
        var finished = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
        if (finished != done.Task && Interlocked.CompareExchange(ref abandoned, 1, 0) == 0)
        {
            await Reply(stream, 504, "").ConfigureAwait(false);
            return;
        }
        var answer = await done.Task.ConfigureAwait(false);
        if (answer == null) await Reply(stream, 500, "").ConfigureAwait(false);
        else await Reply(stream, 200, answer).ConfigureAwait(false);
    }

    static async Task<byte[]?> ReadBody(Stream s, Dictionary<string, string> headers, CancellationToken ct)
    {
        var body = new MemoryStream();
        if (headers.TryGetValue("Transfer-Encoding", out var te) && te.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            while (true)
            {
                var size = await ReadLine(s, ct).ConfigureAwait(false);
                if (size == null || !int.TryParse(size.Split(';')[0].Trim(), System.Globalization.NumberStyles.HexNumber, null, out int n) || n < 0) return null;
                if (n == 0) { await ReadLine(s, ct).ConfigureAwait(false); return body.ToArray(); }
                if (body.Length + n > MaxBody) return null;
                if (!await Copy(s, body, n, ct).ConfigureAwait(false)) return null;
                await ReadLine(s, ct).ConfigureAwait(false);
            }
        }
        int length = headers.TryGetValue("Content-Length", out var cl) && int.TryParse(cl, out int l) ? l : 0;
        if (length < 0 || length > MaxBody) return null;
        return await Copy(s, body, length, ct).ConfigureAwait(false) ? body.ToArray() : null;
    }

    static async Task<bool> Copy(Stream s, MemoryStream into, int count, CancellationToken ct)
    {
        var buffer = new byte[Math.Min(count, 8192) + 1];
        while (count > 0)
        {
            int read = await s.ReadAsync(buffer.AsMemory(0, Math.Min(count, buffer.Length)), ct).ConfigureAwait(false);
            if (read == 0) return false;
            into.Write(buffer, 0, read);
            count -= read;
        }
        return true;
    }

    static async Task Reply(Stream s, int status, string body)
    {
        var text = status switch { 200 => "OK", 401 => "Unauthorized", 404 => "Not Found", 413 => "Payload Too Large", 431 => "Request Header Fields Too Large", 500 => "Internal Server Error", 503 => "Service Unavailable", _ => "Gateway Timeout" };
        var payload = Encoding.Latin1.GetBytes(body);
        var head = $"HTTP/1.1 {status} {text}\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n";
        await s.WriteAsync(Encoding.ASCII.GetBytes(head)).ConfigureAwait(false);
        await s.WriteAsync(payload).ConfigureAwait(false);
    }

    /// <summary>A rejected request is logged with its sender at most once every ten seconds, with a count of the rest.</summary>
    void Rejected(string ip)
    {
        lock (_workers)
        {
            _rejectsSinceLog++;
            if (DateTime.UtcNow - _lastRejectLog < TimeSpan.FromSeconds(10)) return;
            _world.System<NwnxRegistry>().Log($"[nwnx] Risenholm: RPC request from {ip} refused (wrong or missing secret; {_rejectsSinceLog} since the last report)");
            _rejectsSinceLog = 0;
            _lastRejectLog = DateTime.UtcNow;
        }
    }
}
