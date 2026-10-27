using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// The client side of a connection: applies server messages to the scene.
/// Everything visible lives under <see cref="Space"/>, which rotates Aurora's
/// Z-up coordinates into Godot's Y-up.
/// </summary>
public partial class ClientSession : Node3D
{
    public IConnection Connection = null!;
    public GamePaths Paths = null!;
    public ClientContent? Content { get; private set; }
    public Node3D Space { get; private set; } = null!;
    public AreaView? Area { get; private set; }
    public uint ControlledId { get; private set; }
    public string ModuleName { get; private set; } = "";
    public readonly Dictionary<uint, ObjectView> Objects = new();
    /// <summary>Control changed; discard data and pending interactions belonging to the previous creature.</summary>
    public event Action<uint>? ControlledChanged;
    public event Action<ChatMessage>? ChatReceived;
    /// <summary>Text to show over an object (speech and floating text): speaker id and text with colour tokens.</summary>
    public event Action<uint, string>? SpeechReceived;
    public event Action<DialogMessage>? DialogReceived;
    public event Action<NotifyMessage>? NotifyReceived;
    /// <summary>An animation the server played on an object (the multiplayer trace records them).</summary>
    public event Action<PlayAnimationMessage>? AnimationReceived;
    public event Action? AreaLoaded;
    /// <summary>The area has drawn, its loading screen is gone and the server has been told (after <see cref="AreaLoaded"/>).</summary>
    public event Action? AreaPresented;
    /// <summary>Verified resources changed, including a replacement module Welcome.</summary>
    public event Action? ContentChanged;
    /// <summary>The previous module was cleared; close object-bound UI without sending old requests.</summary>
    public event Action? ModuleCleared;
    bool _clearingModule;
    /// <summary>
    /// A LoadArea arrived. The area is built a couple of frames later (so a loading screen can show first);
    /// messages after it wait until it is built.
    /// </summary>
    public event Action<LoadAreaMessage>? AreaLoading;
    /// <summary>Frames to wait between <see cref="AreaLoading"/> and building the area.</summary>
    public int AreaBuildDelayFrames = 2;
    LoadAreaMessage? _pendingArea;
    int _pendingFrames;
    /// <summary>The area waiting to be built (null when none).</summary>
    public LoadAreaMessage? PendingArea => _pendingArea;
    public event Action<CharacterListMessage>? CharacterListReceived;
    /// <summary>Content download progress: bytes received, bytes in total.</summary>
    public event Action<long, long>? DownloadProgress;
    ContentSync? _download;
    long _downloadTotal;
    WelcomeMessage? _welcome;
    readonly Queue<byte[]> _held = new();
    // Retain sources only until old UI/scene nodes finish their deferred removal.
    readonly List<ResMan> _retiredResources = new();

    Audio.AreaAudio? _audio;
    WorldEnvironment? _env;
    DirectionalLight3D? _sun;

    /// <summary>The area music and ambient players (scripts change them through area-audio notifies).</summary>
    public Audio.AreaAudio? AreaAudio => _audio;

    public ObjectView? Controlled => Objects.TryGetValue(ControlledId, out var v) ? v : null;

    public override void _Ready()
    {
        Space = new Node3D { Name = "AuroraSpace", RotationDegrees = new Vector3(-90, 0, 0) };
        AddChild(Space);
        AddChild(new Audio.ConversationVoice { Name = "ConversationVoice", Session = this });
        AddChild(new Audio.CreatureVoice { Name = "CreatureVoice", Session = this });
    }

    public override void _ExitTree()
    {
        _download?.Dispose();
        Content?.Resources.Dispose();
        foreach (var resources in _retiredResources) resources.Dispose();
        _retiredResources.Clear();
        if (Connection is IDisposable disposable) disposable.Dispose();
    }

    public override void _Process(double delta)
    {
        var pendingBefore = _pendingArea;
        int budget = 4000;
        while (budget-- > 0 && Connection.TryReceive(out var data))
        {
            // While content downloads, everything except file chunks waits until the haks are in place;
            // while an area waits to be built, everything after it waits too.
            var type = data.Length > 0 ? (MessageType)data[0] : (MessageType)0;
            if (type != MessageType.Welcome && ((_download != null && type != MessageType.ContentChunk) || _pendingArea != null))
            { _held.Enqueue(data); continue; }
            Handle(new MessageReader(data));
        }
        if (pendingBefore != null && ReferenceEquals(pendingBefore, _pendingArea) && --_pendingFrames <= 0)
        {
            _pendingArea = null;
            LoadArea(pendingBefore);
            using (AreaLoadTiming.Phase("held")) DrainHeld();
        }
        if (Area != null && Controlled is { } controlled)
        {
            Area.UpdateFade(delta);
            var camera = GetViewport()?.GetCamera3D();
            foreach (var view in Objects.Values)
            {
                view.SetUnexplored(!Area.IsExplored(view)); // the original hides the player on unexplored tiles too
                if (camera != null && view is StaticObjectView { Kind: ObjectKind.Door } door)
                    door.UpdateDoorCamera(camera.GlobalPosition, controlled.GlobalPosition);
            }
        }
    }

    public void Send(byte[] message, bool reliable = true)
    {
        if (!_clearingModule) Connection.Send(message, reliable);
    }

    /// <summary>Sends a UI request (equip, store-buy, nui-event, ...) with JSON arguments.</summary>
    public void Request(string kind, object? data = null) =>
        Send(new ClientRequestMessage { Kind = kind, Json = data == null ? "" : System.Text.Json.JsonSerializer.Serialize(data) }.Write());

    void Handle(MessageReader r)
    {
        if ((_presentationLoading || _awaitingFirstArea) && r.Type is MessageType.Chat or MessageType.Dialog or MessageType.PlayAnimation)
        { _presentationMessages.Enqueue(r); return; }
        switch (r.Type)
        {
            case MessageType.Welcome:
            {
                var w = WelcomeMessage.Read(r);
                _download?.Dispose();
                _download = null;
                _held.Clear();
                _pendingArea = null;
                _pendingFrames = 0;
                if (_welcome != null) ClearModule();
                _awaitingFirstArea = true;
                ModuleName = w.ModuleName;
                _welcome = w;
                var sync = new ContentSync(
                    [System.IO.Path.Combine(Paths.UserRoot, "hak"), System.IO.Path.Combine(Paths.GameRoot, "data", "hak")],
                    [System.IO.Path.Combine(Paths.UserRoot, "tlk"), System.IO.Path.Combine(Paths.GameRoot, "data", "tlk")],
                    ProjectSettings.GlobalizePath("user://content"));
                sync.Plan(w.Files);
                if (sync.Done) { InstallContent(w, sync); break; }
                // Fetch what's missing, one file at a time in the order offered.
                _download = sync;
                _downloadTotal = sync.BytesNeeded;
                GD.Print($"[client] downloading {sync.Missing.Count} file(s), {sync.BytesNeeded / 1048576.0:0.0} MB");
                foreach (var f in sync.Missing) Send(new ContentRequestMessage { Sha1 = f.Sha1 }.Write());
                break;
            }
            case MessageType.ContentChunk when _download != null:
            {
                var d = _download;
                d.Accept(ContentChunkMessage.Read(r));
                DownloadProgress?.Invoke(d.BytesReceived, _downloadTotal);
                if (d.Error != null) { GD.PrintErr("[client] " + d.Error); d.Dispose(); _download = null; break; }
                if (!d.Done) break;
                _download = null;
                InstallContent(_welcome!, d);
                GD.Print("[client] downloads complete");
                DrainHeld();
                break;
            }
            case MessageType.CharacterList:
                CharacterListReceived?.Invoke(CharacterListMessage.Read(r));
                break;
            case MessageType.LoadArea:
            {
                var area = LoadAreaMessage.Read(r);
                AreaLoadTiming.Begin(area.ResRef);
                BeginAreaPresentation(area);
                AreaLoading?.Invoke(area);
                if (AreaBuildDelayFrames <= 0) { LoadArea(area); break; }
                // Show the loading screen for a frame or two before the (synchronous) build.
                _pendingArea = area;
                _pendingFrames = AreaBuildDelayFrames;
                break;
            }
            case MessageType.Spawn:
                Spawn(SpawnMessage.Read(r));
                break;
            case MessageType.Update:
            {
                var u = UpdateMessage.Read(r);
                if (Objects.TryGetValue(u.ObjectId, out var v)) v.ApplyUpdate(u);
                break;
            }
            case MessageType.Despawn:
            {
                var d = DespawnMessage.Read(r);
                if (Objects.Remove(d.ObjectId, out var v))
                {
                    if (v is CreatureView cv && d.DecayingCorpse && !_presentationLoading)
                    {
                        v.Flags = (v.Flags | ObjectFlags.Dead) & ~ObjectFlags.Selectable;
                        cv.Animator?.SetDead(true);
                        _decayingViews.Add(v);
                        var fade = v.CreateTween();
                        double remaining = cv.Animator?.DeathFallRemaining ?? 0;
                        if (remaining > 0) fade.TweenInterval(remaining);
                        fade.TweenMethod(Callable.From<float>(v.SetDecayAlpha), 1f, 0f, 1.5);
                        fade.TweenCallback(Callable.From(() => { _decayingViews.Remove(v); v.QueueFree(); }));
                    }
                    else v.QueueFree();
                }
                break;
            }
            case MessageType.ControlObject:
            {
                uint next = ControlObjectMessage.Read(r).ObjectId;
                if (next != ControlledId)
                {
                    ControlledId = next;
                    ControlledChanged?.Invoke(next);
                }
                break;
            }
            case MessageType.PlayAnimation:
            {
                var m = PlayAnimationMessage.Read(r);
                if (Objects.TryGetValue(m.ObjectId, out var v)) v.PlayAnimation(m);
                AnimationReceived?.Invoke(m);
                break;
            }
            case MessageType.Chat:
            {
                var chat = ChatMessage.Read(r);
                ChatReceived?.Invoke(chat);
                if (chat.Channel is not (ChatChannel.Server or ChatChannel.Dm or ChatChannel.Tell or ChatChannel.Dialog) && Objects.ContainsKey(chat.Speaker)) SpeechReceived?.Invoke(chat.Speaker, chat.Text);
                else if (chat.Channel == ChatChannel.Server && chat.SpeakerName.Length == 0 && Objects.ContainsKey(chat.Speaker)) SpeechReceived?.Invoke(chat.Speaker, chat.Text);
                break;
            }
            case MessageType.Dialog:
                DialogReceived?.Invoke(DialogMessage.Read(r));
                break;
            case MessageType.Appearance:
            {
                var a = AppearanceMessage.Read(r);
                if (Objects.GetValueOrDefault(a.ObjectId) is CreatureView cv && Content != null) cv.Rebuild(a.Appearance, Content);
                break;
            }
            case MessageType.Time:
                Area?.SetTime(TimeMessage.Read(r));
                break;
            case MessageType.Notify:
                var notice = NotifyMessage.Read(r);
                if (notice.Kind == "area-snapshot-ready") { SnapshotReady(notice); break; }
                if (_presentationLoading && notice.Kind is not ("map-exploration" or "creature-visibility")
                    || _awaitingFirstArea && (Views.WorldFx.WorldFxPlayer.Kinds.Contains(notice.Kind)
                        || notice.Kind.StartsWith("spell-", StringComparison.Ordinal)
                        || notice.Kind is "item-acquired" or "item-placed" or "vfx" or "voice-chat" or "attack" or "attack-impact"))
                { _presentationMessages.Enqueue(new MessageReader(notice.Write())); break; }
                ApplyExploration(notice);
                if (notice.Kind == "pause")
                {
                    using var pause = System.Text.Json.JsonDocument.Parse(notice.Json);
                    _gamePaused = (pause.RootElement.GetProperty("state").GetInt32() & 2) != 0;
                    UpdateSpaceProcess();
                }
                if (notice.Kind == "creature-visibility") ApplyCreatureVisibility(notice);
                if (notice.Kind == "object-portrait" && Objects.GetValueOrDefault(notice.Subject) is { } portrayed)
                {
                    using var portrait = System.Text.Json.JsonDocument.Parse(notice.Json);
                    portrayed.Portrait = portrait.RootElement.GetProperty("portrait").GetString() ?? "";
                }
                // Engine feedback floated over a creature without a chat line (a coup de grace's "*Coup De Grace!*").
                if (notice.Kind == "float-text" && Objects.ContainsKey(notice.Subject))
                {
                    using var floated = System.Text.Json.JsonDocument.Parse(notice.Json);
                    SpeechReceived?.Invoke(notice.Subject, floated.RootElement.GetProperty("text").GetString() ?? "");
                }
                if (notice.Kind == "creature-bump" && Objects.GetValueOrDefault(notice.Subject) is CreatureView bumped)
                    bumped.Bump(notice.Json, id => Objects.GetValueOrDefault(id));
                if (notice.Kind is "item-acquired" or "item-placed" && Content != null)
                {
                    using var data = System.Text.Json.JsonDocument.Parse(notice.Json);
                    UI.Classic.ClassicSounds.Play(UI.Classic.ClassicAssets.For(Content), data.RootElement.GetProperty("sound").GetString() ?? "");
                }
                if (notice.Kind == "nwnx-player") ApplyResourceOverride(notice);
                if (notice.Kind == "tlk-override" && Content != null)
                {
                    // SetTlkOverride: every string the client reads from the talk table follows it from now on.
                    using var tlk = System.Text.Json.JsonDocument.Parse(notice.Json);
                    Content.Resources.Talk.SetOverride(unchecked((uint)tlk.RootElement.GetProperty("strref").GetInt32()), tlk.RootElement.GetProperty("text").GetString() ?? "");
                }
                NotifyReceived?.Invoke(notice);
                break;
        }
    }

    /// <summary>
    /// NWNX_Player_SetResManOverride: the server asks this client to read one resource in place of another (an empty
    /// name clears it). Resources loaded from then on follow it.
    /// </summary>
    void ApplyResourceOverride(NotifyMessage message)
    {
        if (Content == null) return;
        using var doc = System.Text.Json.JsonDocument.Parse(message.Json);
        var d = doc.RootElement;
        if (!d.TryGetProperty("op", out var op) || op.GetString() != "resman-override") return;
        Content.Resources.SetOverride((ushort)d.GetProperty("type").GetInt32(), d.GetProperty("from").GetString() ?? "", d.GetProperty("to").GetString() ?? "");
    }

    void ApplyCreatureVisibility(NotifyMessage message)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(message.Json);
        var data = doc.RootElement;
        if (data.TryGetProperty("reset", out _))
            foreach (var view in Objects.Values) view.SetPerception(null);
        foreach (var item in data.EnumerateObject())
        {
            if (!uint.TryParse(item.Name, out uint id) || !Objects.TryGetValue(id, out var view)) continue;
            if (item.Value.ValueKind == System.Text.Json.JsonValueKind.Null) view.SetPerception(null);
            else view.SetPerception(item.Value.GetProperty("seen").GetBoolean(), item.Value.GetProperty("heard").GetBoolean());
        }
    }

    /// <summary>Handles held messages in order, stopping again if one of them is a LoadArea.</summary>
    void DrainHeld()
    {
        while (_pendingArea == null && _download == null && _held.TryDequeue(out var held)) Handle(new MessageReader(held));
    }

    void InstallContent(WelcomeMessage welcome, ContentSync sync)
    {
        Content = new ClientContent(ResMan.ForClient(Paths, welcome.Haks, welcome.CustomTlk, sync.Resolved,
            welcome.Files.Where(f => f.Kind == ContentKind.ModuleResources).Select(f => f.Name)));
        OpenWinter.Client.UI.HudParts.Portraits.Clear();
        ContentChanged?.Invoke();
    }

    void ClearModule()
    {
        ResetPresentation();
        foreach (var view in Objects.Values) view.QueueFree();
        Objects.Clear();
        Area?.QueueFree(); Area = null;
        _env?.QueueFree(); _env = null;
        _sun?.QueueFree(); _sun = null;
        _audio?.Free(); _audio = null;
        ControlledId = 0;
        var oldResources = Content?.Resources;
        Content = null;
        _clearingModule = true;
        try { ModuleCleared?.Invoke(); }
        finally { _clearingModule = false; }
        if (oldResources != null) Retire(oldResources);
    }

    async void Retire(ResMan resources)
    {
        _retiredResources.Add(resources);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_retiredResources.Remove(resources)) resources.Dispose();
    }

    void LoadArea(LoadAreaMessage msg)
    {
        foreach (var v in Objects.Values) v.QueueFree();
        Objects.Clear();
        Area?.QueueFree();
        _env?.QueueFree();
        _sun?.QueueFree();
        if (Content == null) return;
        if (_audio == null)
        {
            _audio = new Audio.AreaAudio { Loader = Content.Audio, Resources = Content.Resources };
            AddChild(_audio);
        }
        AreaLoadTiming.Mark("build");
        var built = AreaLoadTiming.Phase("area");
        Area = AreaView.Build(msg, Content, out var env, out var sun);
        built.Dispose();
        Space.AddChild(Area);
        _env = new WorldEnvironment { Environment = env };
        AddChild(_env);
        _sun = sun;
        AddChild(_sun);
        Area.Bind(env, sun, Content);
        if (msg.LoadToken == 0) SnapshotReady(null);
    }

    void Spawn(SpawnMessage s)
    {
        if (Content == null || Objects.ContainsKey(s.ObjectId)) return;
        long started = AreaLoadTiming.Running ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        ObjectView? view = s.Kind switch
        {
            ObjectKind.Creature => CreatureView.Create(s, Content),
            ObjectKind.Placeable or ObjectKind.Door or ObjectKind.Item => StaticObjectView.Create(s, Content),
            ObjectKind.Sound => SoundView.Create(s, Content.Audio),
            _ => null,
        };
        if (view == null) return;
        Objects[s.ObjectId] = view;
        Space.AddChild(view);
        if (started != 0) AreaLoadTiming.Add(s.Kind.ToString().ToLowerInvariant(), System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
}
