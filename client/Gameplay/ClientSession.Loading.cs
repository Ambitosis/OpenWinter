using System.Text.Json;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Gameplay;

public partial class ClientSession
{
    public bool WaitForLoadingScreen;
    bool _presentationLoading, _awaitingFirstArea;
    LoadAreaMessage? _presentationArea;
    readonly Queue<MessageReader> _presentationMessages = new();
    readonly HashSet<Views.ObjectView> _decayingViews = new();

    void ClearDecayingViews()
    {
        foreach (var view in _decayingViews) if (IsInstanceValid(view)) view.QueueFree();
        _decayingViews.Clear();
    }

    void ResetPresentation()
    {
        _presentationLoading = false;
        _awaitingFirstArea = true;
        _presentationArea = null;
        _presentationMessages.Clear();
        ClearDecayingViews();
    }
    public bool PresentationLoading => _presentationLoading;

    bool _gamePaused, _localPause;
    /// <summary>The player pause is on (the server's "pause" notify, state bit 2).</summary>
    public bool GamePaused => _gamePaused;
    /// <summary>Single player held by a menu or by the window being in the background (the world doesn't step).</summary>
    public bool LocalPause
    {
        get => _localPause;
        set { if (_localPause == value) return; _localPause = value; UpdateSpaceProcess(); }
    }

    /// <summary>
    /// The world's presentation (animations, footsteps, placed and effect sounds) stops while an area loads and while
    /// the game is paused, as the original freezes everything; the camera and the interface keep working.
    /// </summary>
    void UpdateSpaceProcess() =>
        Space.ProcessMode = _presentationLoading || _gamePaused || _localPause ? ProcessModeEnum.Disabled : ProcessModeEnum.Inherit;

    void BeginAreaPresentation(LoadAreaMessage area)
    {
        ClearDecayingViews();
        _presentationLoading = true;
        _presentationArea = area;
        if (!_awaitingFirstArea) _presentationMessages.Clear();
        _awaitingFirstArea = false;
        _audio?.Stop();
        // Models can upload/render beneath the loading screen, but their clocks and
        // placed sounds must not start ticking while the player is still loading.
        UpdateSpaceProcess();
    }

    void SnapshotReady(NotifyMessage? message)
    {
        if (_presentationArea is not { } area || Area?.Info.AreaId != area.AreaId) return;
        if (message != null)
        {
            using var data = JsonDocument.Parse(message.Json);
            if (message.Subject != area.AreaId || data.RootElement.GetProperty("token").GetUInt32() != area.LoadToken) return;
        }
        AreaLoadTiming.Mark("snapshot");
        AreaLoaded?.Invoke();
        if (!WaitForLoadingScreen) _ = ReadyAfterFrame(area.LoadToken);
    }

    async Task ReadyAfterFrame(uint token)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (IsInsideTree()) CompleteAreaPresentation(token);
    }

    /// <summary>Hands the area over once it has drawn a frame (under the loading screen), then runs <paramref name="then"/>.</summary>
    public async Task CompleteAfterFrame(uint token, Action? then = null)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (!IsInsideTree() || token != PresentationToken || !_presentationLoading) return;
        CompleteAreaPresentation(token);
        then?.Invoke();
    }

    public uint PresentationToken => _presentationArea?.LoadToken ?? 0;

    /// <summary>Called only once the destination has rendered and its loading screen is gone.</summary>
    public void CompleteAreaPresentation(uint token)
    {
        if (!_presentationLoading || _presentationArea is not { } area || token != area.LoadToken) return;
        _presentationLoading = false;
        UpdateSpaceProcess();
        _audio?.Configure(area);
        while (!_presentationLoading && _presentationMessages.TryDequeue(out var message)) Handle(message);
        if (token != 0) Request("area-ready", new { token });
        AreaLoadTiming.End();
    }
}
