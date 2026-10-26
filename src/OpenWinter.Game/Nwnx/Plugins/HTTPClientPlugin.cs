using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>Outbound HTTP for NWNX_HTTPClient and NWNX_WebHook: requests run in the background and report on the game thread.</summary>
public static class NwnxHttp
{
    static readonly HttpClient Shared = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) });

    /// <summary>The most of an answer's body that is read; the rest is left unread.</summary>
    public const int MaxResponseBytes = 1024 * 1024;

    static readonly ConditionalWeakTable<NwnxRegistry, HttpMessageInvoker> Clients = new();

    /// <summary>Sends a world's requests through <paramref name="client"/> instead of the network (tests); null restores it.</summary>
    public static void SetClient(NwnxRegistry reg, HttpMessageInvoker? client)
    {
        if (client == null) Clients.Remove(reg);
        else Clients.AddOrUpdate(reg, client);
    }

    public static HttpMessageInvoker? ClientFor(NwnxRegistry reg) => Clients.TryGetValue(reg, out var c) ? c : null;

    static async Task<byte[]> ReadCapped(HttpContent content, CancellationToken ct)
    {
        using var stream = await content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[MaxResponseBytes];
        int total = 0, read;
        while (total < buffer.Length && (read = await stream.ReadAsync(buffer.AsMemory(total), ct).ConfigureAwait(false)) > 0) total += read;
        return buffer[..total];
    }

    /// <summary>The outcome of one request: the status (-1 when no answer came), the body and the rate-limit headers.</summary>
    public sealed record Result(int Status, string Body, Dictionary<string, string> Headers);

    /// <summary>
    /// "https://host:port/path", or plain HTTP for port 80. A host given with its own "http://" or "https://" keeps
    /// that scheme.
    /// </summary>
    public static Uri Url(string host, int port, string path)
    {
        string scheme = port == 80 ? "http" : "https";
        foreach (var s in new[] { "http://", "https://" })
            if (host.StartsWith(s, StringComparison.OrdinalIgnoreCase)) { scheme = s[..^3]; host = host[s.Length..]; }
        host = host.TrimEnd('/');
        if (path.Length > 0 && path[0] != '/') path = "/" + path;
        return new Uri($"{scheme}://{host}{(port > 0 ? ":" + port.ToString(CultureInfo.InvariantCulture) : "")}{path}");
    }

    /// <summary>Sends a request in the background; <paramref name="done"/> runs on the game thread with the result.</summary>
    public static void Send(NwnxRegistry reg, HttpRequestMessage request, int timeoutMs, Action<Result> done, HttpMessageInvoker? client = null)
    {
        _ = Task.Run(async () =>
        {
            Result result;
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs > 0 ? timeoutMs : Timeout.Infinite);
                using var response = await (client != null ? client.SendAsync(request, cts.Token) : Shared.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)).ConfigureAwait(false);
                var body = await ReadCapped(response.Content, cts.Token).ConfigureAwait(false);
                result = new Result((int)response.StatusCode, OpenWinter.Aurora.Text.Windows1252.GetString(body), RateLimits(response));
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or OperationCanceledException or InvalidOperationException)
            {
                result = new Result(-1, e is OperationCanceledException ? "request timed out" : e.Message, new());
            }
            finally { request.Dispose(); }
            reg.Post(() => done(result));
        });
    }

    static Dictionary<string, string> RateLimits(HttpResponseMessage response)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        string? Header(string name) => response.Headers.TryGetValues(name, out var v) ? v.FirstOrDefault() : null;
        d["RATELIMIT_LIMIT"] = Header("X-RateLimit-Limit") ?? "";
        d["RATELIMIT_REMAINING"] = Header("X-RateLimit-Remaining") ?? "";
        d["RATELIMIT_RESET"] = Header("X-RateLimit-Reset") ?? "";
        d["RETRY_AFTER"] = Header("Retry-After") ?? "";
        return d;
    }

    public static bool Succeeded(int status) => status is >= 200 and < 300;
}

/// <summary>
/// NWNX_HTTPClient: SendRequest sends at once in the background and returns the request's id; the answer comes as
/// NWNX_ON_HTTPCLIENT_SUCCESS (a 2xx status) or NWNX_ON_HTTPCLIENT_FAILED on the module, with event data REQUEST_ID,
/// STATUS, RESPONSE and the rate-limit headers (plugin README). NWNX_HTTPCLIENT_REQUEST_TIMEOUT is in milliseconds.
/// Nothing is sent unless the host allows plugins outside the game (a dedicated server) and NWNX_HTTPCLIENT_ENABLE is
/// on; otherwise every request fails at once with status -1. Answers are read up to 1 MiB.
/// </summary>
public static class HTTPClientPlugin
{
    const string P = "HTTPClient";

    /// <summary>A request as the script described it.</summary>
    public sealed record Request(string Tag, int Method, string Host, string Path, int ContentType, string Data, int AuthType,
        string AuthUserOrToken, string AuthPassword, int Port, string Headers, string Custom1, string Custom2, string Custom3);

    sealed class State
    {
        public int NextId;
        public readonly Dictionary<int, Request> Requests = new();
    }

    static readonly ConditionalWeakTable<NwnxRegistry, State> States = new();

    static readonly HttpMethod[] Methods = [HttpMethod.Get, HttpMethod.Post, HttpMethod.Delete, HttpMethod.Patch, HttpMethod.Put, HttpMethod.Options, HttpMethod.Head];
    static readonly string[] ContentTypes = ["text/html", "text/plain", "application/json", "application/x-www-form-urlencoded", "application/xml"];

    [NwnxFunc(P, "SendRequest")]
    static void SendRequest(NwnxCall n)
    {
        var r = new Request(n.PopString(), n.PopInt(), n.PopString(), n.PopString(), n.PopInt(), n.PopString(), n.PopInt(),
            n.PopString(), n.PopString(), n.PopInt(), n.PopString(), n.PopString(), n.PopString(), n.PopString());
        var st = States.GetValue(n.Registry, _ => new State());
        int id = ++st.NextId;
        st.Requests[id] = r;
        n.Push(id);
        if (!n.Registry.OutsideEnabled("NWNX_HTTPCLIENT_ENABLE"))
        {
            n.ReportUnsupported("outbound HTTP is off: it needs a dedicated server with NWNX_HTTPCLIENT_ENABLE set");
            n.Registry.Post(() => Finish(n.World, id, new NwnxHttp.Result(-1, "outbound HTTP is not enabled", new())));
            return;
        }
        if (r.Method < 0 || r.Method >= Methods.Length || r.Host.Length == 0)
        {
            n.Registry.Post(() => Finish(n.World, id, new NwnxHttp.Result(-1, "invalid request", new())));
            return;
        }
        HttpRequestMessage message;
        try { message = Build(r); }
        catch (Exception e) when (e is UriFormatException or FormatException or ArgumentException)
        {
            n.Registry.Post(() => Finish(n.World, id, new NwnxHttp.Result(-1, e.Message, new())));
            return;
        }
        var world = n.World;
        int timeout = n.Registry.EnvInt("NWNX_HTTPCLIENT_REQUEST_TIMEOUT", 2000);
        var test = NwnxHttp.ClientFor(n.Registry);
        var client = test ?? (r.AuthType == 2 ? DigestClient(message.RequestUri!, r) : null);
        NwnxHttp.Send(n.Registry, message, timeout, res =>
        {
            if (client != null && client != test) client.Dispose();
            Finish(world, id, res);
        }, client);
    }

    /// <summary>The HTTP request for a script's description of one.</summary>
    public static HttpRequestMessage Build(Request r)
    {
        var message = new HttpRequestMessage(Methods[r.Method], NwnxHttp.Url(r.Host, r.Port, r.Path));
        string? contentType = null;
        foreach (var pair in r.Headers.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            int colon = pair.IndexOf(':');
            if (colon <= 0) continue;
            var name = pair[..colon].Trim();
            var value = pair[(colon + 1)..].Trim();
            if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) contentType = value;
            else message.Headers.TryAddWithoutValidation(name, value);
        }
        switch (r.AuthType)
        {
            case 1:
                message.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(OpenWinter.Aurora.Text.Windows1252.GetBytes(r.AuthUserOrToken + ":" + r.AuthPassword)));
                break;
            case 3:
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", r.AuthUserOrToken);
                break;
        }
        if (r.Method is 1 or 3 or 4 || r.Data.Length > 0 && r.Method != 0 && r.Method != 6)
        {
            var type = r.ContentType >= 0 && r.ContentType < ContentTypes.Length ? ContentTypes[r.ContentType] : ContentTypes[1];
            message.Content = new ByteArrayContent(OpenWinter.Aurora.Text.Windows1252.GetBytes(r.Data));
            message.Content.Headers.TryAddWithoutValidation("Content-Type", contentType ?? type);
        }
        return message;
    }

    static HttpMessageInvoker DigestClient(Uri uri, Request r)
    {
        var credentials = new CredentialCache { { new Uri(uri.GetLeftPart(UriPartial.Authority)), "Digest", new NetworkCredential(r.AuthUserOrToken, r.AuthPassword) } };
        return new HttpClient(new SocketsHttpHandler { Credentials = credentials, PreAuthenticate = false });
    }

    static void Finish(World world, int id, NwnxHttp.Result res)
    {
        var data = new Dictionary<string, string>(res.Headers)
        {
            ["REQUEST_ID"] = id.ToString(CultureInfo.InvariantCulture),
            ["STATUS"] = res.Status.ToString(CultureInfo.InvariantCulture),
            ["RESPONSE"] = res.Body,
        };
        foreach (var k in new[] { "RATELIMIT_LIMIT", "RATELIMIT_REMAINING", "RATELIMIT_RESET", "RETRY_AFTER" }) data.TryAdd(k, "");
        if (res.Status < 0) world.System<NwnxRegistry>().Log($"[nwnx] HTTPClient: request {id} failed: {res.Body}");
        EventsPlugin.Signal(world, NwnxHttp.Succeeded(res.Status) ? "NWNX_ON_HTTPCLIENT_SUCCESS" : "NWNX_ON_HTTPCLIENT_FAILED", world.Module, data);
    }

    [NwnxFunc(P, "GetRequest")]
    static void GetRequest(NwnxCall n)
    {
        int id = n.PopInt();
        var r = States.TryGetValue(n.Registry, out var st) && st.Requests.TryGetValue(id, out var found) ? found
            : new Request("", 0, "", "", 0, "", 0, "", "", 0, "", "", "", "");
        // Pushed last to first: the script pops the tag first.
        n.Push(r.Custom3); n.Push(r.Custom2); n.Push(r.Custom1); n.Push(r.Headers); n.Push(r.Port);
        n.Push(r.AuthPassword); n.Push(r.AuthUserOrToken); n.Push(r.AuthType); n.Push(r.Data); n.Push(r.ContentType);
        n.Push(r.Path); n.Push(r.Host); n.Push(r.Method); n.Push(r.Tag);
    }
}
