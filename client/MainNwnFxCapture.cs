using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-fx dir: on an NWN:EE server running the bridgefx
    /// scene (parity/visual/scenes/bridgefx), takes shots along the scene's timeline: script animations and a
    /// knockdown, placeable and door animations, visual effects at a place and on creatures, a beam, an area of
    /// effect, visual transforms, a highlight, conjuring, casting and a projectile. The scene announces its first step
    /// ("S knockdown", six seconds in) in chat; shot times count scene seconds from it. --fx-shots "name@seconds,..."
    /// replaces the default list.
    /// </summary>
    async Task CaptureNwnFx(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        double? begun = null;
        void OnChat(ChatMessage m)
        {
            if (begun == null && m.Text == "S knockdown") begun = Time.GetTicksMsec() / 1000.0 - 6.0;
        }
        _session.ChatReceived += OnChat;
        for (int i = 0; i < 600 && begun == null; i++) await Wait(0.1);
        _session.ChatReceived -= OnChat;
        if (begun is not { } start || _session.Controlled == null)
        {
            GD.PrintErr("[nwn-fx] the scene didn't start within 60 s");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"[nwn-fx] in {_session.Area?.Info.ResRef} with {_session.Objects.Count} objects");
        _camera.Distance = 15;
        _camera.Pitch = Mathf.DegToRad(-48);
        _camera.HeightOffset = 0;
        _camera.Snap();
        const string Default = "01_knockdown@8.0,02_pause2_chest_open@9.6,03_worship_lever_on@16.6,04_sit_brazier_off@22.0,"
            + "05_talk_door_open@26.8,06_fireball_at_place@62.6,07_glow_red@67.0,08_protection@73.0,09_beam@80.0,"
            + "10_acid_fog@88.5,11_scaled@97.5,12_rotated@101.0,13_hilite@106.5,14_conjure@110.9,15_cast_missile@112.1,"
            + "16_conjure_fireball@124.8,17_fireball_flight@126.1,18_bless@139.9,19_light@153.5";
        foreach (var shot in _args.GetValueOrDefault("fx-shots", Default).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = shot.Split('@');
            double wait = start + double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) - Time.GetTicksMsec() / 1000.0;
            if (wait > 0) await Wait(wait);
            await Shot(dir, p[0]);
        }
        GetTree().Quit();
    }
}
