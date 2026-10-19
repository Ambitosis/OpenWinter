using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views.WorldFx;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>SoundObjectPlay / SoundObjectStop / SoundObjectSetVolume / SoundObjectSetPosition (the sound-object notify).</summary>
public partial class SoundView
{
    public bool Active => _active;

    public void ApplyScript(JsonElement d)
    {
        switch (J.S(d, "action"))
        {
            case "play":
                // Starts at once, from the first sound of its list (soundobj: "once", "once_again").
                _active = true;
                _wait = 0;
                _next = 0;
                _onceDone = false;
                StopPlayers();
                break;
            case "stop":
                _active = false;
                StopPlayers();
                break;
            case "volume":
            {
                // Takes effect on the sound already playing (soundobj: flat_96 ... flat_0), linearly in 0-127.
                _volume = _playVolume = Math.Clamp(J.I(d, "volume", 127), 0, 127);
                if (_positional != null) UpdateDistance();
                if (_flat != null) _flat.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, SoundObjectRules.FlatLevel * SoundObjectRules.Gain(_volume)));
                break;
            }
            case "position":
                if (J.V(d, "position") is { } p) TargetPosition = Position = p;
                break;
        }
    }

    void StopPlayers()
    {
        _positional?.Stop();
        _flat?.Stop();
        if (!_active) _wait = 1e9;
    }
}
