using OpenWinter.Aurora.Formats;
using Godot;

namespace OpenWinter.Client.Views;

public partial class EffectPlayer
{
    readonly HashSet<Node3D> _playingAudio = new();

    void ClearAudio()
    {
        foreach (var holder in _playingAudio)
            if (IsInstanceValid(holder) && !holder.IsQueuedForDeletion()) holder.QueueFree();
        _playingAudio.Clear();
    }

    void StartAudio(TwoDA table, int row, ObjectView? target, Vector3? point, long effectId, List<Node3D> lasting)
    {
        // Impact and duration are independent layers. Native Elemental Shield starts
        // both; the impact sample finishes even when its short effect has expired.
        PlayAudio(table.Get(row, "SoundImpact"), target, point, loop: false);
        var sustained = PlayAudio(table.Get(row, "SoundDuration"), target, point, loop: true);
        if (sustained == null) return;
        string kind = table.Get(row, "Type_FD");
        if (effectId != 0 && kind is not ("F" or "P"))
        {
            lasting.Add(sustained); // authoritative effect removal stops the loop
            return;
        }
        // Measured with long and short authored samples: MIRV cues loop at the
        // target for one second, independently of distance or requested duration.
        // Ordinary instantaneous D/F visual cues instead have a four-second tail.
        GetTree().CreateTimer(kind == "P" ? 1.0 : 4.0).Timeout += () =>
        {
            if (IsInstanceValid(sustained)) sustained.QueueFree();
        };
    }

    Node3D? PlayAudio(string resref, ObjectView? target, Vector3? point, bool loop)
    {
        if (target != null && !IsInstanceValid(target)) return null;
        var audio = Session.Content?.Audio;
        var stream = loop ? audio?.GetLooping(resref) : audio?.Get(resref);
        if (stream == null) return null;
        var holder = new Node3D { Name = "fx_audio_" + resref };
        var player = new AudioStreamPlayer3D
        {
            Stream = stream, MaxDistance = 40,
            Bus = OpenWinter.Client.Preferences.AudioBuses.Effects3D,
        };
        holder.AddChild(player);
        _playingAudio.Add(holder);
        holder.TreeExiting += () => _playingAudio.Remove(holder);
        Attach(holder, target, "", point);
        if (!loop) player.Finished += () => { if (IsInstanceValid(holder)) holder.QueueFree(); };
        if (WorldFx.WorldFxPlayer.Verbose) GD.Print($"[effect-audio] {resref} loop={loop} on={target?.ObjectId}");
        player.Play();
        return holder;
    }
}
