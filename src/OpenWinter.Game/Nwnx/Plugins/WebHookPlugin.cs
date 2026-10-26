using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_WebHook: a Slack-format message posted to https://host/path. Text is sent as {"text", "username", "mrkdwn"};
/// a message that is already JSON (NWNX_WebHook_BuildMessageForWebHook) is sent as it is. The answer comes as
/// NWNX_ON_WEBHOOK_SUCCESS or NWNX_ON_WEBHOOK_FAILED on the module, with STATUS, MESSAGE, HOST, PATH and the
/// rate-limit data (plugin README; RETRY_AFTER in milliseconds).
/// </summary>
public static class WebHookPlugin
{
    /// <summary>Sends through this instead of the network (tests).</summary>
    public static HttpMessageInvoker? Client;

    /// <summary>The JSON body posted for a message.</summary>
    public static string Body(string message, string username, bool mrkdwn)
    {
        if (message.TrimStart().StartsWith('{')) return message;
        var o = new System.Text.Json.Nodes.JsonObject { ["text"] = message };
        if (username.Length > 0) o["username"] = username;
        if (!mrkdwn) o["mrkdwn"] = false;
        return o.ToJsonString();
    }

    [NwnxFunc("WebHook", "SendWebHookHTTPS")]
    static void Send(NwnxCall n)
    {
        var host = n.PopString(); var path = n.PopString(); var message = n.PopString(); var username = n.PopString(); bool mrkdwn = n.PopInt() != 0;
        var world = n.World;
        var body = Body(message, username, mrkdwn);
        HttpRequestMessage request;
        try
        {
            request = new HttpRequestMessage(HttpMethod.Post, NwnxHttp.Url(host, 443, path))
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)),
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }
        catch (UriFormatException e)
        {
            n.Registry.Log($"[nwnx] WebHook: bad address {host}{path}: {e.Message}");
            return;
        }
        NwnxHttp.Send(n.Registry, request, 10000, res =>
        {
            var data = new Dictionary<string, string>(res.Headers)
            {
                ["STATUS"] = res.Status.ToString(CultureInfo.InvariantCulture),
                ["MESSAGE"] = body,
                ["HOST"] = host,
                ["PATH"] = path,
            };
            if (double.TryParse(data.GetValueOrDefault("RETRY_AFTER"), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                data["RETRY_AFTER"] = ((long)(seconds * 1000)).ToString(CultureInfo.InvariantCulture);
            if (!NwnxHttp.Succeeded(res.Status)) world.System<NwnxRegistry>().Log($"[nwnx] WebHook: {host}{path} answered {res.Status}: {res.Body}");
            EventsPlugin.Signal(world, NwnxHttp.Succeeded(res.Status) ? "NWNX_ON_WEBHOOK_SUCCESS" : "NWNX_ON_WEBHOOK_FAILED", world.Module, data);
        }, Client);
    }
}
