using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Preferences;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Audio;

/// <summary>Dialogue voice playback, shared by both interfaces and controlled by the Voices volume.</summary>
public partial class ConversationVoice : AudioStreamPlayer3D
{
    public ClientSession Session = null!;
    public string Sound { get; private set; } = "";
    uint _speaker;

    public override void _Ready()
    {
        Bus = AudioBuses.Voice3D;
        UnitSize = 6;
        MaxDistance = 45;
        Session.DialogReceived += OnLine;
        Session.AreaLoading += OnArea;
    }

    public override void _ExitTree()
    {
        Session.DialogReceived -= OnLine;
        Session.AreaLoading -= OnArea;
    }

    void OnArea(LoadAreaMessage _) => Stop();

    public override void _Process(double delta)
    {
        if (!Playing) return;
        if (Session.Objects.GetValueOrDefault(_speaker) is not { } speaker) { Stop(); return; }
        GlobalPosition = speaker.GlobalPosition + Vector3.Up * 1.5f;
    }

    void OnLine(DialogMessage line)
    {
        Stop();
        _speaker = line.Speaker;
        Sound = line.Ended ? "" : line.Sound;
        Stream = line.Ended ? null : Session.Content?.Audio.Get(line.Sound);
        if (Stream != null && Session.Objects.GetValueOrDefault(_speaker) is { } speaker)
        {
            GlobalPosition = speaker.GlobalPosition + Vector3.Up * 1.5f;
            Play();
        }
    }
}
