using Godot;

namespace OpenWinter.Client.Audio;

/// <summary>
/// What placed sounds need to know about the world each frame (set by WorldFxPlayer): where the original's
/// listener is (a fifth of the way from the player to the camera, docs/AUDIO.md), whether it is night and the hour.
/// </summary>
public static class AudioWorld
{
    public static Vector3? Listener;
    public static bool Night;
    public static int Hour = 12;
}
