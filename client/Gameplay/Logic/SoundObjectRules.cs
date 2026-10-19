using System.Numerics;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// How placed sound objects (UTS), ambient sound and footsteps play in the original, measured with the
/// `soundobj`, `audiofollow` and `footfall` scenes (docs/AUDIO.md). Godot-free so the tests can check it.
/// </summary>
public static class SoundObjectRules
{
    /// <summary>Linear level of a non-positional sound object at volume 127 (full-scale sample, effects at full).</summary>
    public const float FlatLevel = 0.59f;
    /// <summary>Linear level of a positional sound object at volume 127 with the listener on top of it.</summary>
    public const float PositionalLevel = 0.304f;
    /// <summary>Footsteps: level at the listener and the distance where they fade out.</summary>
    public const float FootstepLevel = 0.071f, FootstepMaxDistance = 20f;
    /// <summary>
    /// Godot's panning puts a positional mono sound about 3 dB under the original's at the same gain (audiofollow:
    /// the same curve 2.7-3.4 dB lower), so positional players are raised by this much.
    /// </summary>
    public const float PannedCompensation = 1.41f;
    /// <summary>The listener sits this far along the line from the player to the camera.</summary>
    public const float ListenerCameraFraction = 0.19f;

    /// <summary>Times: 0 only in the hours set in Hours (bit n = hour n), 1 by day, 2 by night, 3 always.</summary>
    public static bool Audible(int times, uint hours, bool night, int hour) => times switch
    {
        1 => !night,
        2 => night,
        0 => hour is >= 0 and < 32 && (hours & (1u << hour)) != 0,
        _ => true,
    };

    /// <summary>
    /// Seconds from the end of one sound to the start of the next for a repeating sound: Interval plus or minus
    /// IntervalVrtn, uniformly (unit is a 0..1 roll). The first sound plays as soon as the object starts.
    /// </summary>
    public static double Gap(double interval, double variation, double unit) =>
        Math.Max(0, interval + (2 * unit - 1) * Math.Max(0, variation));

    /// <summary>Volume (0-127) for one play: Volume plus or minus VolumeVrtn, never above 127.</summary>
    public static int Volume(int volume, int variation, double unit) =>
        Math.Clamp(volume + (int)Math.Round((2 * unit - 1) * Math.Max(0, variation)), 0, 127);

    /// <summary>Linear gain of a 0-127 volume (SoundObjectSetVolume, the blueprint's Volume, the area's ambient volume).</summary>
    public static float Gain(int volume) => Math.Clamp(volume, 0, 127) / 127f;

    /// <summary>
    /// Distance gain of a positional sound: falling linearly from the listener to MaxDistance and silent beyond.
    /// MinDistance makes no measurable difference in the original.
    /// </summary>
    public static float DistanceGain(float distance, float max) =>
        max <= 0 ? 0f : Math.Clamp((max - distance) / max, 0f, 1f);

    /// <summary>Where the original hears from: a fifth of the way from the player to the camera.</summary>
    public static Vector3 Listener(Vector3 player, Vector3 camera) => player + (camera - player) * ListenerCameraFraction;
}
