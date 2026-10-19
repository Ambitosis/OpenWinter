using OpenWinter.Content;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

public partial class CreatureView
{
    ClientContent? _footstepContent;
    int _surface, _footstepType = -1;
    ulong _lastFootstep;
    AudioStreamPlayer3D? _footstepPlayer;

    static void AddFootstepEvents(Godot.Animation animation, ModelAnimation source)
    {
        var events = source.Events.Where(e => e.Name.Equals("snd_footstep", StringComparison.OrdinalIgnoreCase)).Select(e => e.Time).Distinct().Order().ToArray();
        if (events.Length == 0) return;
        int track = animation.AddTrack(Godot.Animation.TrackType.Method);
        animation.TrackSetPath(track, new NodePath("."));
        foreach (float time in events)
            animation.TrackInsertKey(track, time, new Godot.Collections.Dictionary { ["method"] = nameof(OnFootstep), ["args"] = new Godot.Collections.Array() });
    }

    // Called by the currently playing animation, including turns, strafes and bump steps.
    public void OnFootstep()
    {
        if (_footstepContent is not { } content || (Flags & ObjectFlags.Dead) != 0) return;
        ulong now = Time.GetTicksMsec();
        // Blended clips and duplicate model events must not play the same contact several times.
        if (now - _lastFootstep < 100) return;
        string column = Gameplay.FootstepSounds.Column(content.Resources.TwoDA("surfacemat").Get(_surface, "Sound"), _surface);
        if (column.Length == 0 || _footstepType < 0) return;
        string sound = content.Resources.TwoDA("footstepsounds").Get(_footstepType, column + GD.RandRange(0, 2));
        if (sound.Length == 0 || sound == "****" || content.Audio.Get(sound) is not { } stream) return;
        _lastFootstep = now;
        if (_footstepPlayer == null)
        {
            // Distance is applied by hand: the original's footsteps fade linearly from the listener to 20 m (footfall).
            _footstepPlayer = new AudioStreamPlayer3D
            {
                Name = "Footsteps", Bus = Preferences.AudioBuses.Effects3D, MaxPolyphony = 4,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled, MaxDistance = 0, AttenuationFilterCutoffHz = 20500,
            };
            AddChild(_footstepPlayer);
        }
        float distance = Audio.AudioWorld.Listener is { } listener ? GlobalPosition.DistanceTo(listener) : 0f;
        float gain = Gameplay.SoundObjectRules.PannedCompensation * Gameplay.SoundObjectRules.FootstepLevel * Gameplay.SoundObjectRules.DistanceGain(distance, Gameplay.SoundObjectRules.FootstepMaxDistance);
        if (gain <= 0) return;
        _footstepPlayer.VolumeDb = Mathf.LinearToDb(gain);
        _footstepPlayer.Stream = stream;
        _footstepPlayer.Play();
        FootstepPlayed?.Invoke(this, sound);
    }

    /// <summary>Capture diagnostics; never used to choose or time footsteps.</summary>
    public static event Action<CreatureView, string>? FootstepPlayed;
}
