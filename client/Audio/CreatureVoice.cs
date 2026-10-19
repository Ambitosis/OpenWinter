using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Preferences;
using OpenWinter.Net;
using Godot;
namespace OpenWinter.Client.Audio;

/// <summary>Positional soundsets for all visible creatures, independent of interface style.</summary>
public partial class CreatureVoice : Node
{
    public ClientSession Session = null!;
    readonly Dictionary<uint, AudioStreamPlayer3D> _playing = new();
    public override void _Ready() { Session.NotifyReceived += OnNotify; Session.AreaLoading += OnArea; }
    public override void _ExitTree() { Session.NotifyReceived -= OnNotify; Session.AreaLoading -= OnArea; }
    void OnArea(LoadAreaMessage _) { foreach (var p in _playing.Values) if (IsInstanceValid(p)) p.QueueFree(); _playing.Clear(); }
    void OnNotify(NotifyMessage note)
    {
        if (note.Kind != "voice-chat" || Session.Objects.GetValueOrDefault(note.Subject) is not { } speaker) return;
        using var doc = JsonDocument.Parse(note.Json);
        if (!doc.RootElement.TryGetProperty("sound", out var value)) return;
        string sound = value.GetString() ?? "";
        if (Session.Content?.Audio.Get(sound) is not { } stream) return;
        if (_playing.Remove(note.Subject, out var previous) && IsInstanceValid(previous)) previous.QueueFree();
        // ObjectView local space is Aurora Z-up; Session.Space rotates it into global Y-up.
        var player = new AudioStreamPlayer3D { Stream = stream, Bus = AudioBuses.Voice3D, UnitSize = 6, MaxDistance = 45, Position = new Vector3(0, 0, 1.5f) };
        speaker.AddChild(player);
        _playing[note.Subject] = player;
        player.Finished += () => player.QueueFree();
        player.TreeExited += () => { if (_playing.GetValueOrDefault(note.Subject) == player) _playing.Remove(note.Subject); };
        if (Views.WorldFx.WorldFxPlayer.Verbose) GD.Print($"[voice] {note.Subject} {sound} length={stream.GetLength():0.000}");
        player.Play();
    }
}
