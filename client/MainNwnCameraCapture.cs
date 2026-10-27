using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-camera dir: on an NWN:EE server running the camanim
    /// scene (parity/visual/scenes/camanim), takes the scene's shots, timed from the step each one follows (the player
    /// announces every step in chat), and prints the camera's direction, pitch, distance and height with each.
    /// </summary>
    async Task CaptureNwnCamera(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        // The step's chat line (its start), the shot names and their times after it.
        (string Step, string Name, double[] At)[] plan =
        [
            ("C all ", "c_all", [1.5]), ("C dir ", "c_dir", [1.5]), ("C dist ", "c_dist", [1.5]), ("C pitch ", "c_pitch", [1.5]),
            ("C medium ", "t_medium", [0.5, 1.5, 2.5, 3.5, 5.0]), ("C fast ", "t_fast", [0.5, 1.5, 2.5, 3.5]),
            ("H 3.00", "h_3", [1.5]), ("H 0.00", "h_0", [1.5]), ("H -1.00", "h_m1", [1.5]),
            ("C away ", "s_away", [1.5]), ("S restore", "s_restored", [1.5]), ("T Actor", "a_attached", [1.5]),
            ("A pause1 custom3lp", "r_idle", [2.0]),
            ("P custom3", "p_custom3", [0.4, 2.0, 4.5, 7.5]), ("P custom6", "p_custom6", [0.4, 2.0, 4.5, 7.5]),
            ("P custom4", "p_custom4", [0.4, 2.0, 4.5, 7.5]), ("P custom1", "p_custom1", [0.4, 2.0, 4.5, 7.5]),
        ];
        bool done = false;
        async Task Take(string name, double after)
        {
            await Wait(after);
            float yaw = Mathf.RadToDeg(_camera.Yaw) + 90, pitch = 90 + Mathf.RadToDeg(_camera.Pitch);
            GD.Print($"[nwn-camera] {name}: direction {(yaw % 360 + 360) % 360:0.0} pitch {pitch:0.0} distance {_camera.Distance:0.00} "
                + $"height {_camera.HeightOffset:0.00} target {(_camera.Target as Views.ObjectView)?.ObjectId}");
            await Shot(dir, name);
            if (name == "p_custom1_3") done = true;
        }
        void OnChat(ChatMessage m)
        {
            foreach (var (step, name, at) in plan)
                if (m.Text.StartsWith(step, StringComparison.Ordinal))
                {
                    GD.Print($"[nwn-camera] step \"{m.Text}\"");
                    for (int i = 0; i < at.Length; i++) _ = Take(at.Length > 1 ? $"{name}_{i}" : name, at[i]);
                    break;
                }
        }
        _session.ChatReceived += OnChat;
        for (int i = 0; i < 2400 && !done; i++) await Wait(0.1);
        _session.ChatReceived -= OnChat;
        await Wait(1);
        GetTree().Quit(done ? 0 : 1);
    }

    /// <summary>
    /// --capture-camera-timeline dir [--every SECONDS] [--seconds N]: from the moment the player's character is in the
    /// world, a shot every --every seconds (default 1.5) for --seconds (default 40), each with the camera's direction,
    /// pitch, distance, height and target printed: a module's opening cutscene as the camera frames it.
    /// </summary>
    async Task CaptureCameraTimeline(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        double every = double.TryParse(_args.GetValueOrDefault("every"), System.Globalization.NumberStyles.Float, inv, out var e) ? e : 1.5;
        double seconds = double.TryParse(_args.GetValueOrDefault("seconds"), System.Globalization.NumberStyles.Float, inv, out var s) ? s : 40;
        _session.NotifyReceived += n =>
        {
            if (n.Kind is "camera" or "camera-store" or "cutscene" or "object-animation") GD.Print($"[camera-timeline] {n.Kind} {n.Subject} {n.Json}");
        };
        _session.AnimationReceived += m => GD.Print($"[camera-timeline] animation {m.ObjectId} {m.Animation} {m.Duration:0.#}");
        for (int i = 0; i < 1200 && _session.Controlled == null; i++) await Wait(0.1);
        for (int n = 0; n * every <= seconds; n++)
        {
            float yaw = Mathf.RadToDeg(_camera.Yaw) + 90, pitch = 90 + Mathf.RadToDeg(_camera.Pitch);
            GD.Print($"[camera-timeline] t{n * every:000.0}: direction {(yaw % 360 + 360) % 360:0.0} pitch {pitch:0.0} distance {_camera.Distance:0.00} "
                + $"height {_camera.HeightOffset:0.00} target {(_camera.Target as Views.ObjectView)?.ObjectId} area {_session.Area?.Name}");
            await Shot(dir, $"t{n * every:000.0}".Replace('.', '_'));
            await Wait(every);
        }
        GetTree().Quit();
    }
}
