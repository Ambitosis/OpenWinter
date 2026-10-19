using OpenWinter.Client.UI;

namespace OpenWinter.Client;

/// <summary>ActivatePortal: the prompt (client/UI/PortalPrompt.cs) and joining the portal's server.</summary>
public partial class Main
{
    /// <summary>The server a portal's Join leaves for, kept across the scene reload (InitFrontEnd takes it).</summary>
    static Dictionary<string, string>? _portalJoin;
    PortalPrompt? _portal;

    /// <summary>The portal prompt of this game (captures).</summary>
    public PortalPrompt? Portal => _portal;

    void AttachPortal(ChatLog chat)
    {
        _portal = new PortalPrompt
        {
            Name = "PortalPrompt", Session = _session, SinglePlayer = _server != null, ModernHost = _windowHost,
            // Yellow, as the original prints its portalling lines (mp_portal).
            Feedback = line => chat.Feedback(line, "#ffff00"), Join = JoinPortal,
        };
        AddChild(_portal);
        if (_args.TryGetValue("capture-portal", out var dir)) _ = CapturePortal(dir);
    }

    /// <summary>--capture-portal dir: the prompt while it checks a server that never answers, then with an answer, then closed.</summary>
    async Task CapturePortal(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && (_session.Controlled == null || _loading.Showing); i++) await Wait(0.1);
        await Wait(3);
        _portal!.SinglePlayer = false; // single player shows no prompt; the capture shows the multiplayer one
        _portal.Activate(new PortalRequest("127.0.0.1:5199", "secret", "WP_PORTAL", false));
        await Wait(1.5);
        await Shot(dir, "a_portal");
        _portal.Activate(new PortalRequest("127.0.0.2:5200", "", "", false));
        await Wait(1);
        await Shot(dir, "b_again");
        _portal.ShowAnswer(new PortalPrompt.ServerCheck.Result(true, "openwinter-mp", "vis_mp_portal", 1, 3));
        await Wait(0.5);
        await Shot(dir, "c_answered");
        _portal.Press("cancel");
        await Wait(1);
        await Shot(dir, "d_cancelled");
        GetTree().Quit();
    }

    Godot.Control _windowHost
    {
        get
        {
            var host = _windowLayer.GetNodeOrNull<Godot.Control>("PortalHost");
            if (host != null) return host;
            host = new Godot.Control { Name = "PortalHost", MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
            host.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.FullRect);
            _windowLayer.AddChild(host);
            return host;
        }
    }

    /// <summary>Leaves this game and joins the portal's server as the same player (its waypoint goes with the login).</summary>
    public void JoinPortal(PortalRequest request)
    {
        _portalJoin = new() { ["connect"] = request.Address, ["name"] = _args.GetValueOrDefault("name", "Player") };
        if (request.Waypoint.Length > 0) _portalJoin["portal-waypoint"] = request.Waypoint;
        ExitToMainMenu();
    }

    /// <summary>After the reload: the portal's server becomes this run's --connect.</summary>
    void TakePortalJoin()
    {
        if (_portalJoin is not { } join) return;
        _portalJoin = null;
        foreach (var (k, v) in join) _args[k] = v;
    }
}
