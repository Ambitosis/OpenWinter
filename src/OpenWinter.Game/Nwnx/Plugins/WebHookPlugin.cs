using System.Globalization;
using System.Net.Http.Headers;
using System.Text;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_WebHook: a Slack-format message posted to https://host/path. Text is sent as {"text", "username", "mrkdwn"};
/// a message that is already JSON (NWNX_WebHook_BuildMessageForWebHook) is sent as it is. The answer comes as
/// NWNX_ON_WEBHOOK_SUCCESS or NWNX_ON_WEBHOOK_FAILED on the module, with STATUS, MESSAGE, HOST, PATH and the
/// rate-limit data (plugin README; RETRY_AFTER in milliseconds). Nothing is sent unless the host allows plugins outside
/// the game (a dedicated server) and NWNX_WEBHOOK_ENABLE is on; otherwise the message is only logged.
/// </summary>
public static class WebHookPlugin
{
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
        if (!n.Registry.OutsideEnabled("NWNX_WEBHOOK_ENABLE"))
        {
            n.ReportUnsupported("webhooks are off: they need a dedicated server with NWNX_WEBHOOK_ENABLE set");
            n.Registry.Log($"[nwnx] webhook {host}{path} not sent: {message}");
            return;
        }
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
            if (NwnxHttp.Succeeded(res.Status)) EventsPlugin.Signal(world, "NWNX_ON_WEBHOOK_SUCCESS", world.Module, data);
            else
            {
                // nwnx_events.nss names the failure NWNX_ON_WEBHOOK_FAILURE, the WebHook README NWNX_ON_WEBHOOK_FAILED: both run.
                EventsPlugin.Signal(world, "NWNX_ON_WEBHOOK_FAILURE", world.Module, data);
                EventsPlugin.Signal(world, "NWNX_ON_WEBHOOK_FAILED", world.Module, data);
            }
        }, NwnxHttp.ClientFor(n.Registry));
    }
}
