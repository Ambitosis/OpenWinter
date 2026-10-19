using Godot;

namespace OpenWinter.Client.Preferences;

/// <summary>
/// The mixer: buses created at runtime under Master, one per volume slider in Options → Audio.
/// Every player the client creates names one of these as its <c>Bus</c>.
/// </summary>
public static class AudioBuses
{
    public const string Master = "Master", Music = "Music", Ambient = "Ambient", Effects = "Effects", Voice = "Voice", Interface = "Interface";
    public const string Ambient3D = "Ambient3D", Effects3D = "Effects3D", Voice3D = "Voice3D";

    static readonly string[] Children = { Music, Ambient, Effects, Voice, Interface, Ambient3D, Effects3D, Voice3D };

    /// <summary>Creates the buses (once).</summary>
    public static void Ensure()
    {
        foreach (var name in Children)
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            AudioServer.AddBus();
            int index = AudioServer.BusCount - 1;
            AudioServer.SetBusName(index, name);
            AudioServer.SetBusSend(index, Master);
        }
    }

    public static void Apply(AudioSettings a, bool focused)
    {
        foreach (var (bus, _) in AudioSettings.Buses)
            ApplyBus(bus, a.Get(bus), bus is Master or Music ? 1f : a.SpatialGain(false));
        ApplyBus(Ambient3D, a.Ambient, a.SpatialGain(true));
        ApplyBus(Effects3D, a.Effects, a.SpatialGain(true));
        ApplyBus(Voice3D, a.Voice, a.SpatialGain(true));

        void ApplyBus(string bus, float v, float gain)
        {
            int index = AudioServer.GetBusIndex(bus);
            if (index < 0) return;
            bool mute = v <= 0.001f || (bus == Master && !focused && a.MuteWhenUnfocused);
            AudioServer.SetBusMute(index, mute);
            // A perceptual curve: the slider's middle sounds like half as loud.
            AudioServer.SetBusVolumeDb(index, mute ? -80 : Mathf.LinearToDb(v * v * gain));
        }
    }
}
