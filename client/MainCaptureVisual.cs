using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --capture-visual dir: the OpenWinter half of the visual parity harness (tools/visual/visual.py). The scene's
/// own script drives everything (camera, time, creatures, effects) and logs "VISUAL|shot|name" whenever a
/// screenshot is due; this takes it a moment later, with every 2D layer hidden so only the world is
/// compared, and quits on "VISUAL|done". --visual-delay seconds sets the moment (default 0.35, close to
/// the time the harness takes to notice the original client's log line).
/// </summary>
public partial class Main
{
    readonly Queue<(string Name, string Line)> _visualShots = new();
    /// <summary>Scene keys (VISUAL|key|vk|tap/down/up) and key shots (VISUAL|keyshot|vk|name|seconds|close vk).</summary>
    readonly System.Collections.Concurrent.ConcurrentQueue<string[]> _visualKeys = new();
    bool _visualDone;

    /// <summary>Called with every script log line of the single-player server.</summary>
    void VisualLog(string line)
    {
        int at = line.IndexOf("VISUAL|", StringComparison.Ordinal);
        if (at < 0) return;
        RecordVisualAudioNote(line);
        var parts = line[(at + 7)..].Trim().Split('|');
        if (parts.Length >= 2 && parts[0] == "shot") _visualShots.Enqueue((parts[1], line[at..].Trim()));
        else if (parts[0] == "done") _visualDone = true;
        // VISUAL|note|...: a reading the scene logged (facings, states), kept beside the shots for comparison.
        else if (parts[0] == "note" && _visualDir != null) System.IO.File.AppendAllText(System.IO.Path.Combine(_visualDir, "notes.txt"), line[at..].Trim() + "\n");
        // Keep the existing forced highlight path for Tab (9), independent of UI visibility.
        else if (parts.Length >= 3 && parts[0] == "key" && parts[1] == "9") Gameplay.Interaction.ForceHighlightAll = parts[2] == "down";
        else if (parts.Length >= 3 && parts[0] is "key" or "keyshot") _visualKeys.Enqueue(parts);
        else if (parts.Length >= 2 && parts[0] == "hover") _visualKeys.Enqueue(parts);
        else if (parts.Length >= 2 && parts[0] is "click" or "rightclick") _visualKeys.Enqueue(parts);
        else if (parts.Length >= 3 && parts[0] == "mouse") _visualKeys.Enqueue(parts);
        else if (parts.Length >= 4 && parts[0] == "burst") _visualKeys.Enqueue(parts);
    }

    string? _visualDir;
    uint _visualCharacterId;
    int _visualInventoryDenials;
    void VisualNotify(OpenWinter.Net.NotifyMessage m)
    {
        if (m.Kind == "inventory-denied" && m.Subject == _session.ControlledId) _visualInventoryDenials++;
        if (m.Kind == "character-sheet" && m.Subject == _session.ControlledId) _visualCharacterId = m.Subject;
    }

    /// <summary>
    /// --capture-visual: plays a visual scene's commands. A fault in a command is logged and ends the run rather than
    /// leaving the client running with no more shots.
    /// </summary>
    async Task CaptureVisual(string dir)
    {
        try { await RunVisualScene(dir); }
        catch (Exception e)
        {
            GD.PrintErr($"[visual] scene stopped by {e}");
            GetTree().Quit(1);
        }
    }

    async Task RunVisualScene(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.Delete(System.IO.Path.Combine(dir, "shots.txt"));
        System.IO.File.Delete(System.IO.Path.Combine(dir, "notes.txt"));
        System.IO.File.Delete(System.IO.Path.Combine(dir, "state.jsonl"));
        System.IO.File.Delete(System.IO.Path.Combine(dir, "frames.txt"));
        System.IO.File.Delete(System.IO.Path.Combine(dir, "chat.txt"));
        _visualDir = dir;
        // --visual-chat: everything the chat window shows, as the original's "[CHAT WINDOW TEXT]" log lines.
        void ChatLine(string text) => System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "chat.txt"), $"[CHAT WINDOW TEXT] {text}\n");
        void OnVisualChat(OpenWinter.Net.ChatMessage m) => ChatLine(UI.Kit.NwnText.Strip((m.SpeakerName.Length > 0 ? m.SpeakerName + ": " : "") + UI.Classic.ClassicChatStyle.Tag(m.Channel) + m.Text));
        void OnVisualFeedback(string text, string color) => ChatLine(UI.Kit.NwnText.Strip(text));
        bool chatLog = _args.ContainsKey("visual-chat") && _chatLog != null;
        if (chatLog) { _session.ChatReceived += OnVisualChat; _chatLog!.FeedbackAdded += OnVisualFeedback; }
        if (_args.ContainsKey("visual-familiar-proof")) _session.NotifyReceived += VisualNotify;
        if (float.TryParse(_args.GetValueOrDefault("visual-audio-bias"), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var audioBias))
            Settings.Update(s => s.Audio.SpatialBias = audioBias);
        // --visual-volumes "music=1,effects=0,voices=0,ambient=0": the volume sliders a recording needs (master at full).
        if (_args.TryGetValue("visual-volumes", out var volumes))
            Settings.Update(s =>
            {
                s.Audio.Master = 1f;
                foreach (var pair in volumes.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)))
                    if (pair.Length == 2 && float.TryParse(pair[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
                        s.Audio.Set(pair[0].Trim() switch { "music" => "Music", "effects" => "Effects", "voices" => "Voice", "ambient" => "Ambient", "interface" => "Interface", _ => pair[0].Trim() }, v);
            });
        // --visual-graphics "Glow=false;Ssao=false": graphics options for an experiment (captures never save settings).
        if (_args.TryGetValue("visual-graphics", out var overrides))
            Settings.Update(s =>
            {
                foreach (var pair in overrides.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)))
                {
                    var prop = typeof(Preferences.GraphicsSettings).GetProperty(pair[0].Trim());
                    if (prop == null || pair.Length < 2) { GD.PrintErr($"[visual] unknown graphics option {pair[0]}"); continue; }
                    var type = prop.PropertyType;
                    prop.SetValue(s.Graphics, type.IsEnum ? Enum.Parse(type, pair[1].Trim(), true)
                        : System.Convert.ChangeType(pair[1].Trim(), type, System.Globalization.CultureInfo.InvariantCulture));
                }
            });
        // --keyhole "min-diameter=0.5,max-diameter=0.5,min-z-offset=0": the original's keyholing settings, as the
        // harness's --keyhole gives them to the original.
        if (_args.TryGetValue("keyhole", out var keyhole))
        {
            var kv = keyhole.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
            float Get(string key, float fallback) =>
                kv.TryGetValue(key, out var v) && float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : fallback;
            if (kv.TryGetValue("enabled", out var en) && en == "false") Settings.Update(s => s.Graphics.Keyholing = false);
            Rendering.Keyhole.Configure(Get("min-diameter", Rendering.Keyhole.MinDiameter), Get("max-diameter", Rendering.Keyhole.MaxDiameter),
                Get("min-z-offset", Rendering.Keyhole.MinZOffset));
        }
        double delay = double.TryParse(_args.GetValueOrDefault("visual-delay"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0.35;
        double limit = double.TryParse(_args.GetValueOrDefault("seconds"), out var l) ? l : 120, clock = 0;
        // --visual-gui: the interface stays (comparing the interfaces, e.g. --ui classic) and the scene's key taps are pressed.
        bool gui = _args.ContainsKey("visual-gui");
        var cameraTrace = _args.ContainsKey("visual-camera-trace") ? TraceVisualCamera(dir) : Task.CompletedTask;
        var movementTrace = _args.ContainsKey("visual-movement-trace") ? TraceVisualMovement(dir) : Task.CompletedTask;
        void Footstep(Views.CreatureView view, string sound) => System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "footsteps.txt"), $"{Time.GetTicksMsec()}|{view.ObjectId}|{sound}\n");
        if (_args.ContainsKey("visual-footsteps")) Views.CreatureView.FootstepPlayed += Footstep;
        AudioEffectRecord? audioRecord = null;
        bool voices = _args.ContainsKey("visual-voices");
        int audioBus = AudioServer.GetBusIndex(voices ? Preferences.AudioBuses.Voice3D : Preferences.AudioBuses.Effects3D), audioEffect = -1;
        if ((voices || _args.ContainsKey("visual-footsteps")) && audioBus >= 0)
        {
            audioRecord = new AudioEffectRecord();
            audioEffect = AudioServer.GetBusEffectCount(audioBus);
            AudioServer.AddBusEffect(audioBus, audioRecord);
            audioRecord.SetRecordingActive(true);
        }
        if (_args.ContainsKey("visual-audio")) StartVisualAudioEvidence();
        if (_args.ContainsKey("visual-input-log")) AddChild(new Diagnostics.InputLog { Name = "InputLog", Path = System.IO.Path.Combine(dir, "input.txt") });
        bool visualMouseDown = false;
        var visualMouseButton = MouseButton.Left;
        while (clock < limit)
        {
            if (!gui) HideOverlays();
            if (_visualKeys.TryDequeue(out var key))
            {
                if (key[0] == "burst")
                {
                    double duration = Math.Clamp(double.Parse(key[2], System.Globalization.CultureInfo.InvariantCulture), 0.05, 5);
                    double fps = Math.Clamp(double.Parse(key[3], System.Globalization.CultureInfo.InvariantCulture), 1, 30);
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    for (int frame = 0; frame < (int)(duration * fps); frame++)
                    {
                        double remaining = frame / fps - watch.Elapsed.TotalSeconds;
                        if (remaining > 0) await Wait(remaining);
                        string name = $"{key[1]}_{frame:000}";
                        double elapsed = watch.Elapsed.TotalSeconds;
                        await Shot(dir, name);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "frames.txt"),
                            FormattableString.Invariant($"{name}|{elapsed:0.0000}\n"));
                    }
                    continue;
                }
                if (key[0] == "hover")
                {
                    var previous = _interaction.PointerOverride ?? GetViewport().GetMousePosition();
                    var xy = key[1].Split(',');
                    _interaction.PointerOverride = xy.Length == 2
                        ? new Vector2(float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture)) * GetViewport().GetVisibleRect().Size
                        : null;
                    if (_interaction.PointerOverride is { } pointer)
                        Input.ParseInputEvent(new InputEventMouseMotion { Position = pointer, GlobalPosition = pointer,
                            Relative = pointer - previous, ButtonMask = Input.GetMouseButtonMask() });
                    continue;
                }
                if (key[0] == "mouse" && key.Length > 3 && key[3] == "right")
                {
                    // VISUAL|mouse|x,y|down|right: the right button held and released separately (radial menus).
                    var rxy = key[1].Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    var rat = new Vector2(rxy[0], rxy[1]) * GetViewport().GetVisibleRect().Size;
                    _interaction.PointerOverride = rat;
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = rat, GlobalPosition = rat });
                    Input.ParseInputEvent(VisualPointerModifiers(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Position = rat, GlobalPosition = rat, Pressed = key[2] == "down" }));
                    continue;
                }
                if (key[0] == "mouse")
                {
                    var xy = key[1].Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    var at = new Vector2(xy[0], xy[1]) * GetViewport().GetVisibleRect().Size;
                    // The fourth field is a button name (left/right, as the original's harness reads it) or a
                    // native ground offset.
                    string extra = key.Length > 3 ? key[3] : "";
                    bool named = extra is "left" or "right";
                    var button = extra == "right" ? MouseButton.Right : MouseButton.Left;
                    // Optional native ground offset: compare the same initial distance despite camera framing.
                    if (!named && extra.Length > 0 && _session.Controlled is { } pc)
                    {
                        var ground = key[3].Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        at = key[2] == "down" ? _camera.UnprojectPosition(_session.Space.ToGlobal(pc.Position + new Vector3(ground[0], ground[1], 0)))
                            : _interaction.PointerOverride ?? at;
                    }
                    var previous = _interaction.PointerOverride ?? at;
                    _interaction.PointerOverride = at;
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at,
                        Relative = at - previous, ButtonMask = Input.GetMouseButtonMask() });
                    bool down = key[2] == "down";
                    if (down != visualMouseDown || button != visualMouseButton)
                    {
                        // A release of the other button first, so a press always pairs with its own release.
                        if (visualMouseDown && button != visualMouseButton)
                            Input.ParseInputEvent(VisualPointerModifiers(new InputEventMouseButton { ButtonIndex = visualMouseButton, Position = at, GlobalPosition = at, Pressed = false }));
                        if (down || button == visualMouseButton)
                            Input.ParseInputEvent(VisualPointerModifiers(new InputEventMouseButton { ButtonIndex = button, Position = at, GlobalPosition = at, Pressed = down }));
                    }
                    visualMouseDown = down;
                    visualMouseButton = button;
                    continue;
                }
                if (key[0] is "click" or "rightclick")
                {
                    var xy = key[1].Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    var at = new Vector2(xy[0], xy[1]) * GetViewport().GetVisibleRect().Size;
                    // Optional tag: aim at the same object despite the engines' different camera framing.
                    if (key.Length > 2 && key[2].Length > 0 && _session.Objects.Values.FirstOrDefault(v => v.Name.ToString().EndsWith("_" + key[2], StringComparison.OrdinalIgnoreCase)) is { } target)
                        at = _camera.UnprojectPosition(target.GlobalPosition + Vector3.Up * 0.5f);
                    if (key.Length > 3 && key[3].Length > 0 && _session.Controlled is { } clickPc)
                    {
                        var offset = key[3].Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        at = _camera.UnprojectPosition(_session.Space.ToGlobal(clickPc.Position + new Vector3(offset[0], offset[1], 0)));
                    }
                    _interaction.PointerOverride = at;
                    var button = key[0] == "rightclick" ? MouseButton.Right : MouseButton.Left;
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
                    Input.ParseInputEvent(VisualPointerModifiers(new InputEventMouseButton { ButtonIndex = button, Position = at, GlobalPosition = at, Pressed = true }));
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Input.ParseInputEvent(VisualPointerModifiers(new InputEventMouseButton { ButtonIndex = button, Position = at, GlobalPosition = at, Pressed = false }));
                    System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "notes.txt"), $"VISUAL|note|click|{at.X},{at.Y}\n");
                    continue;
                }
                if (key[0] == "key" && key[2] != "tap")
                    VisualKey(int.Parse(key[1]), key[2] == "down");
                else await Tap(int.Parse(key[1]));
                if (key[0] == "keyshot")
                {
                    // Shot on our own clock: what the key opened may pause the game and the scene's timers with it.
                    await Wait(key.Length > 3 && double.TryParse(key[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 1.5);
                    await Shot(dir, key[2]);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "shots.txt"), "VISUAL|shot|" + key[2] + "\n");
                    if (key.Length > 4 && int.TryParse(key[4], out var close)) await Tap(close);
                }
                continue;
            }
            if (_visualShots.TryDequeue(out var shot))
            {
                if (_args.ContainsKey("visual-notes-only")) continue;
                await Wait(delay);
                if (!gui) HideOverlays();
                await Shot(dir, shot.Name);
                if (_args.ContainsKey("visual-state")) VisualState(dir, shot.Name);
                if (_args.ContainsKey("visual-visibility-check")) CheckVisualVisibility(dir, shot.Name);
                // Mask/depth evidence for halo coverage and occlusion; dotted names stay out of the comparison sheets.
                if (_args.ContainsKey("visual-hilite-probe"))
                    foreach (var viewport in GetTree().Root.FindChildren("Hilite*", "SubViewport", true, false).OfType<SubViewport>())
                        viewport.GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, shot.Name + "." + viewport.Name + ".png"));
                if (_args.ContainsKey("visual-probe")) Probe(dir, shot.Name);
                if (_args.ContainsKey("visual-fadeprobe")) await FadeProbe(dir, shot.Name);
                // What the script reported at the shot (the player's position, facing and name), for the harness.
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "shots.txt"), shot.Line + "\n");
                continue;
            }
            if (_visualDone) break;
            await Wait(0.05);
            clock += 0.05;
        }
        if (clock >= limit) GD.Print("[visual] time limit reached before VISUAL|done");
        _visualDone = true;
        Views.CreatureView.FootstepPlayed -= Footstep;
        if (audioRecord != null)
        {
            audioRecord.SetRecordingActive(false);
            audioRecord.GetRecording()?.SaveToWav(System.IO.Path.Combine(dir, voices ? "voices.wav" : "footsteps.wav"));
            AudioServer.RemoveBusEffect(audioBus, audioEffect);
        }
        SaveVisualAudioEvidence(dir);
        await cameraTrace;
        await movementTrace;
        _session.NotifyReceived -= VisualNotify;
        if (chatLog) { _session.ChatReceived -= OnVisualChat; _chatLog!.FeedbackAdded -= OnVisualFeedback; }
        GetTree().Quit();
    }

    /// <summary>
    /// --visual-probe: what each 4x4-pixel cell of the shot sees, by a ray from the camera against the camera-collision
    /// and tile-fade geometry: &lt;name&gt;.probe.txt with the camera, its focus and one line per cell (column, row,
    /// view depth, world height above the focus target's feet; -1 for nothing hit). For measuring the original's
    /// keyhole against the geometry it cuts.
    /// </summary>
    void Probe(string dir, string name)
    {
        var cam = GetViewport().GetCamera3D();
        var space = cam?.GetWorld3D()?.DirectSpaceState;
        if (cam == null || space == null) return;
        var size = GetViewport().GetVisibleRect().Size;
        var feet = (cam as Gameplay.OrbitCamera)?.Target?.GlobalPosition ?? Vector3.Zero;
        var sb = new System.Text.StringBuilder();
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        sb.AppendLine(string.Create(inv, $"camera {cam.GlobalPosition.X} {cam.GlobalPosition.Y} {cam.GlobalPosition.Z} feet {feet.X} {feet.Y} {feet.Z} fov {cam.Fov} size {size.X} {size.Y} depth {-(cam.GlobalTransform.AffineInverse() * feet).Z}"));
        var forward = -cam.GlobalBasis.Z;
        for (int y = 2; y < size.Y; y += 4)
            for (int x = 2; x < size.X; x += 4)
            {
                var from = cam.ProjectRayOrigin(new Vector2(x, y));
                var to = from + cam.ProjectRayNormal(new Vector2(x, y)) * 200f;
                var q = PhysicsRayQueryParameters3D.Create(from, to, Views.AreaView.CameraLayer | Views.AreaView.FadeLayer);
                q.HitBackFaces = true;
                var hit = space.IntersectRay(q);
                if (hit.Count == 0) { sb.AppendLine(string.Create(inv, $"{x} {y} -1 -1")); continue; }
                var p = hit["position"].AsVector3();
                sb.AppendLine(string.Create(inv, $"{x} {y} {(p - cam.GlobalPosition).Dot(forward):F3} {p.Y - feet.Y:F3}"));
            }
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".probe.txt"), sb.ToString());
    }

    /// <summary>
    /// --visual-fadeprobe: for measuring the original's tile fading against the geometry. Besides the shot,
    /// &lt;name&gt;.nofade.png (every tile whole) and &lt;name&gt;.allfade.png (every tile faded), &lt;name&gt;.fadeid.png
    /// (one pixel per 4x4 cell: red = 1 + the tile whose fading part is the first thing the cell sees, 0 for none;
    /// green, blue = that tile's x, y) and &lt;name&gt;.fade.txt (camera, the player's feet in area coordinates and how
    /// faded OpenWinter had each tile in the shot).
    /// </summary>
    async Task FadeProbe(string dir, string name)
    {
        var cam = GetViewport().GetCamera3D();
        var area = _session.Area;
        var space = cam?.GetWorld3D()?.DirectSpaceState;
        if (cam == null || space == null || area == null) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var feet = (cam as Gameplay.OrbitCamera)?.Target?.GlobalPosition ?? Vector3.Zero;
        var sb = new System.Text.StringBuilder();
        // The area view's own space is the area's (Z up).
        var c = area.ToLocal(cam.GlobalPosition);
        var f = area.ToLocal(feet);
        sb.AppendLine(string.Create(inv, $"camera {c.X:F3} {c.Y:F3} {c.Z:F3} feet {f.X:F3} {f.Y:F3} {f.Z:F3} width {area.Info.Width} height {area.Info.Height}"));
        var amounts = area.TileFadeAmounts;
        for (int t = 0; t < amounts.Count; t++)
            if (amounts[t] > 0) sb.AppendLine(string.Create(inv, $"faded {t % area.Info.Width} {t / area.Info.Width} {amounts[t]:F2}"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, name + ".fade.txt"), sb.ToString());
        var size = GetViewport().GetVisibleRect().Size;
        int w = (int)size.X / 4, h = (int)size.Y / 4;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgb8);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var px = new Vector2(x * 4 + 2, y * 4 + 2);
                var from = cam.ProjectRayOrigin(px);
                var to = from + cam.ProjectRayNormal(px) * 200f;
                var qf = PhysicsRayQueryParameters3D.Create(from, to, Views.AreaView.FadeLayer);
                qf.HitBackFaces = true;
                var hf = space.IntersectRay(qf);
                if (hf.Count == 0) continue;
                var qc = PhysicsRayQueryParameters3D.Create(from, to, Views.AreaView.CameraLayer);
                qc.HitBackFaces = true;
                var hc = space.IntersectRay(qc);
                float df = from.DistanceTo(hf["position"].AsVector3());
                if (hc.Count > 0 && from.DistanceTo(hc["position"].AsVector3()) < df - 0.05f) continue;
                int tile = area.FadeTileOf(hf["collider"].AsGodotObject());
                if (tile < 0) continue;
                img.SetPixel(x, y, Color.Color8((byte)(tile + 1), (byte)(tile % area.Info.Width), (byte)(tile / area.Info.Width)));
            }
        img.SavePng(System.IO.Path.Combine(dir, name + ".fadeid.png"));
        foreach (var (amount, suffix) in new[] { (0f, ".nofade"), (1f, ".allfade") })
        {
            Views.AreaView.FadeOverride = amount;
            for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Shot(dir, name + suffix);
        }
        Views.AreaView.FadeOverride = null;
    }

    /// <summary>Presses and releases a key given as a Windows virtual-key code, including camera arrows.</summary>
    async Task Tap(int vk)
    {
        VisualKey(vk, true);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        VisualKey(vk, false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    static T VisualPointerModifiers<T>(T e) where T : InputEventWithModifiers
    {
        e.ShiftPressed = Input.IsPhysicalKeyPressed(Key.Shift);
        e.CtrlPressed = Input.IsPhysicalKeyPressed(Key.Ctrl);
        e.AltPressed = Input.IsPhysicalKeyPressed(Key.Alt);
        e.MetaPressed = Input.IsPhysicalKeyPressed(Key.Meta);
        return e;
    }

    static void VisualKey(int vk, bool pressed)
    {
        Key key = vk switch
        {
            96 => Key.Kp0, 97 => Key.Kp1, 98 => Key.Kp2, 99 => Key.Kp3, 100 => Key.Kp4,
            101 => Key.Kp5, 102 => Key.Kp6, 103 => Key.Kp7, 104 => Key.Kp8, 105 => Key.Kp9,
            >= 112 and <= 135 => (Key)((int)Key.F1 + vk - 112), // Windows F1..F24.
            27 => Key.Escape, 13 => Key.Enter, 9 => Key.Tab, 32 => Key.Space, 16 => Key.Shift,
            37 => Key.Left, 38 => Key.Up, 39 => Key.Right, 40 => Key.Down,
            33 => Key.Pageup, 34 => Key.Pagedown, 45 => Key.Insert, 46 => Key.Delete,
            _ => (Key)vk,
        };
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
    }

    void VisualState(string dir, string shot)
    {
        var voice = _session.GetNodeOrNull<Audio.ConversationVoice>("ConversationVoice");
        var radial = GetTree().Root.FindChildren("*", "Control", true, false).OfType<UI.RadialMenu>().FirstOrDefault();
        var state = new
        {
            radial = radial?.IsOpen == true ? new { path = radial.Path, labels = radial.Labels, hovered = radial.Hovered, centre = radial.Centre.ToString(), pointer = radial.Pointer.ToString() } : null,
            shot, cameraFollowsPlayer = _camera.Target == _session.Controlled,
            controlledId = _session.ControlledId,
            cameraTargetId = (_camera.Target as Views.ObjectView)?.ObjectId,
            hudSelfId = _hudState?.Self?.Id,
            targeting = _interaction.Targeting != null,
            characterSheetId = _visualCharacterId, inventoryDenials = _visualInventoryDenials,
            visibleWindows = GetTree().Root.FindChildren("*", "Control", true, false).OfType<Control>()
                .Where(c => c.IsVisibleInTree() && c is UI.InventoryPanel or UI.CharacterPanel
                    or UI.Classic.ClassicInventoryWindow or UI.Classic.Sheets.ClassicCharacterSheet)
                .Select(c => c.GetType().Name).ToArray(),
            hudPartyIds = _hudState?.Party.Select(p => p.Id).ToArray(),
            quickbar = Enumerable.Range(0, UI.ActionBar.Pages).Select(page =>
                Enumerable.Range(0, UI.ActionBar.Slots).Select(slot => ActionBar.Get(page, slot)).ToArray()).ToArray(),
            pitch = Mathf.RadToDeg(_camera.Pitch), pointer = _interaction.PointerOverride?.ToString(),
            groundWalkable = _interaction.GroundWalkable,
            groundPosition = _interaction.GroundPosition?.ToString(),
            worldSound = GetNodeOrNull<Views.WorldFx.WorldFxPlayer>("WorldFx")?.LastSound,
            audioBias = Settings.Current.Audio.SpatialBias,
            cursor = GetTree().Root.FindChildren("*", "", true, false).OfType<UI.HudParts.Cursors>().FirstOrDefault()?.CurrentCursorId,
            marker = GetTree().Root.FindChildren("*", "", true, false).OfType<Gameplay.CommandReticle>().FirstOrDefault()?.IsShowing,
            voice = new { sound = voice?.Sound, playing = voice?.Playing, seconds = voice?.GetPlaybackPosition(), length = voice?.Stream?.GetLength(), position = voice?.GlobalPosition.ToString(),
                leftDb = AudioServer.GetBusPeakVolumeLeftDb(AudioServer.GetBusIndex(Preferences.AudioBuses.Voice3D), 0),
                rightDb = AudioServer.GetBusPeakVolumeRightDb(AudioServer.GetBusIndex(Preferences.AudioBuses.Voice3D), 0) },
            creatures = _session.Objects.Values.OfType<Views.CreatureView>().Where(v => v.GlobalPosition.DistanceTo(_session.Controlled?.GlobalPosition ?? Vector3.Zero) < 8).Select(v =>
            {
                int head = v.Skeleton?.FindBone("head_g") ?? -1;
                return new { id = v.ObjectId, name = v.DisplayName, position = v.Position.ToString(), facing = v.Rotation.Z,
                    action = v.Animator?.CaptureAction(),
                    head = head >= 0 ? v.Skeleton!.GetBonePoseRotation(head).ToString() : "" };
            }).ToArray(),
        };
        System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "state.jsonl"), System.Text.Json.JsonSerializer.Serialize(state) + "\n");
        if (_args.ContainsKey("visual-familiar-proof") && shot == "a_ready")
        {
            // Deliberately leave old-owner panels open before the actual possession input.
            _input.ToggleInventory?.Invoke(); _input.ToggleCharacter?.Invoke();
            _interaction.Targeting = (_, _) => { };
            GD.Print("[familiar-proof] owner panels and targeting opened");
            VisualState(dir, "owner_windows_open");
        }
    }

    // --visual-camera-trace: sample the rendered orbit every frame, including input starts/stops.
    // The screenshots alone miss short-lived lag because the two engines capture at different times.
    async Task TraceVisualCamera(string dir)
    {
        using var trace = new System.IO.StreamWriter(System.IO.Path.Combine(dir, "camera.csv"));
        trace.WriteLine("seconds,yaw,pitch,actual_yaw,actual_pitch,distance");
        ulong start = Time.GetTicksMsec();
        while (!_visualDone)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (_camera.Target == null) continue;
            var offset = _camera.GlobalPosition - _camera.Target.GlobalPosition - Vector3.Up * _camera.HeightOffset;
            trace.WriteLine(FormattableString.Invariant($"{(Time.GetTicksMsec() - start) / 1000.0:F3},{_camera.Yaw:F6},{_camera.Pitch:F6},{Mathf.Atan2(offset.X, offset.Z):F6},{-Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()):F6},{offset.Length():F6}"));
        }
    }

    /// <summary>Only the 3D world is compared: every canvas layer (HUD, chat, fades, loading screen) is hidden.</summary>
    void HideOverlays()
    {
        foreach (var layer in GetTree().Root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>())
            layer.Visible = false;
    }
}
