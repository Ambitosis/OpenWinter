using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>
/// --capture-fx dir: runs the world presentation test scripts (tests/scripts/fx_*.nss) through the
/// /script dev command and screenshots each result: weather, fog, SetTile, visual transforms,
/// beams, highlight, traps, grass and wind, sky and light direction, night and tile lights, then
/// logs audio and camera-lock state.
/// </summary>
public static class FxCapture
{
    /// <summary>Lets the dev server find the test scripts (lowest priority, after the module and game).</summary>
    public static void AddTestScripts(ResMan rm) =>
        rm.Add(new DirectorySource(System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "tests", "scripts"))));

    public static async Task Run(Node host, ClientSession session, OrbitCamera camera, string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        WorldFxPlayer.Verbose = true;
        var fx = host.GetChildren().OfType<WorldFxPlayer>().First();
        var me = session.Controlled!;
        async Task Wait(double s) => await host.ToSignal(host.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
        async Task Shot(string name)
        {
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            host.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png"));
            GD.Print($"captured {name}");
        }
        void Script(string name) => session.Send(new ChatSendMessage { Text = "/script " + name }.Write());
        void View(Node3D target, float distance, float pitchDeg, float height = 1.2f, float? yaw = null)
        {
            camera.Target = target;
            camera.Distance = distance;
            camera.Pitch = Mathf.DegToRad(pitchDeg);
            camera.HeightOffset = height;
            if (yaw != null) camera.Yaw = yaw.Value;
            camera.Snap();
        }
        ObjectView? Nearest() => session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v != me && IsAlive(v))
            .OrderBy(v => v.Position.DistanceTo(me.Position)).FirstOrDefault();

        Script("fx_spawn");
        View(me, 9, -22);
        float baseYaw = camera.Yaw;
        await Wait(2.5);
        await Shot("fx_00_base");
        GD.Print($"[capture-fx] grass tufts: {string.Join(", ", fx.Grass.Counts.Select(kv => $"{kv.Key}={kv.Value}"))}");

        Script("fx_rain");
        await Wait(7.0);
        await Shot("fx_01_rain");
        Script("fx_snow");
        await Wait(7.0);
        await Shot("fx_02_snow");
        Script("fx_clear");
        await Wait(6.5);
        Script("fx_fog");
        await Wait(1.6);
        await Shot("fx_03_fog");
        Script("fx_clear");
        await Wait(1.0);

        // SetTile: the tile north of the player turns into a building.
        View(me, 38, -62, 0, yaw: 0);
        await Wait(1.0);
        await Shot("fx_04_tile_before");
        Script("fx_tile");
        await Wait(1.5);
        await Shot("fx_04_tile_after");

        // Visual transforms on the test cow (or the nearest creature).
        var npc = session.Objects.Values.FirstOrDefault(v => v.Name.ToString().EndsWith("_FX_COW", StringComparison.OrdinalIgnoreCase)) ?? Nearest();
        if (npc != null)
        {
            View(npc, 8, -12, 1.4f);
            await Wait(0.6);
            await Shot("fx_05_vt_before");
            Script("fx_vt");
            await Wait(1.0);
            GD.Print($"[capture-fx] vt mid {fx.Objects.PoseOf(npc.ObjectId)}");
            await Shot("fx_05_vt_mid");
            await Wait(1.6);
            GD.Print($"[capture-fx] vt end {fx.Objects.PoseOf(npc.ObjectId)}");
            await Shot("fx_05_vt_end");
            Script("fx_vtclear");
            await Wait(0.5);

            // A lightning beam from the player's hand to it.
            camera.Target = me;
            camera.Distance = 9;
            camera.Pitch = Mathf.DegToRad(-18);
            camera.Yaw = Mathf.Atan2(me.GlobalPosition.X - npc.GlobalPosition.X, me.GlobalPosition.Z - npc.GlobalPosition.Z) + 1.2f;
            camera.Snap();
            Script("fx_beam");
            await Wait(0.9);
            GD.Print($"[capture-fx] beams {fx.Beams.Count}");
            await Shot("fx_06_beam");

            // Mouse-over highlight with a script colour.
            Script("fx_hilite");
            await Wait(0.4);
            fx.Objects.ForcedHilite = npc;
            View(npc, 6, -12, 1.2f);
            await Wait(0.4);
            await Shot("fx_07_hilite");
            fx.Objects.ForcedHilite = null;

            // Texture swaps: the cow wears the ox skin; everyone sees the grass ground texture as cobbles.
            Script("fx_texture");
            View(me, 9, -22, yaw: baseYaw);
            await Wait(0.8);
            await Shot("fx_13_texture");
            Script("fx_textureoff");
            await Wait(0.5);

            // ReplaceObjectAnimation: the idle clip now plays the death pose.
            GD.Print($"[capture-fx] cpause1 {fx.Objects.ClipLength(npc.ObjectId, "cpause1"):0.00}s before");
            Script("fx_anim");
            await Wait(0.5);
            GD.Print($"[capture-fx] cpause1 {fx.Objects.ClipLength(npc.ObjectId, "cpause1"):0.00}s after (cdead {fx.Objects.ClipLength(npc.ObjectId, "cdead"):0.00}s)");
        }

        // A detected trap outline next to the player.
        Script("fx_trap");
        View(me, 12, -50, 0.5f);
        await Wait(1.2);
        GD.Print($"[capture-fx] traps shown {fx.Traps.Count}");
        await Shot("fx_08_trap");
        // Any trigger polygon: a right triangle north-east of the player, right angle at its south-west
        // corner, seen from above with north up.
        var at = me.Position;
        fx.Handle(new NotifyMessage
        {
            Kind = "trap-detected", Subject = 0x7FFFFFF0,
            Json = System.FormattableString.Invariant($"{{\"detected\":true,\"geometry\":[[{at.X + 2},{at.Y + 1},{at.Z}],[{at.X + 7},{at.Y + 1},{at.Z}],[{at.X + 2},{at.Y + 6},{at.Z}]]}}"),
        });
        View(me, 16, -80, 0, yaw: 0);
        await Wait(0.8);
        await Shot("fx_08_trap_polygon");
        fx.Handle(new NotifyMessage { Kind = "trap-disarmed", Subject = 0x7FFFFFF0, Json = "{}" });

        // Grass override and strong wind.
        Script("fx_grass");
        View(me, 6, -14, 1.0f);
        await Wait(1.5);
        await Shot("fx_09_grass");

        // Stormy sky and a low western sun.
        Script("fx_sky");
        View(me, 9, -4, 2.5f);
        await Wait(1.2);
        await Shot("fx_10_sky");

        // Night falls; then the player's tile lights turn red.
        Script("fx_night");
        View(me, 14, -35, 1.0f);
        await Wait(2.6);
        await Shot("fx_11_night");
        Script("fx_light");
        await Wait(0.8);
        await Shot("fx_12_tilelight");

        // Audio and camera locks can't be screenshotted: log their state.
        Script("fx_audio");
        await Wait(1.0);
        var audio = session.AreaAudio;
        GD.Print($"[capture-fx] audio music={audio?.CurrentMusic} ambient={audio?.CurrentAmbient} sound={fx.LastSound}");
        Script("fx_lock");
        await Wait(0.5);
        GD.Print($"[capture-fx] camera locks pitch={camera.LockPitch} distance={camera.LockDistance} yaw={camera.LockYaw}");
        await Wait(0.3);
        host.GetTree().Quit();
    }

    static bool IsAlive(ObjectView v) => (v.Flags & ObjectFlags.Dead) == 0;
}
