using System.Text.Json;
using OpenWinter.Client.Audio;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// A placed sound object, played as the original does (docs/AUDIO.md; rules in <see cref="SoundObjectRules"/>):
/// looping, repeating with a gap of Interval plus or minus IntervalVrtn after each sound, or once through its list;
/// in sequence or at random; positional (fading linearly to MaxDistance from the listener, at a random offset within
/// RandomRangeX/Y when RandomPosition is set) or area-wide; only at the Times it is set for. PitchVariation has no
/// audible effect in the original and none here.
/// </summary>
public partial class SoundView : ObjectView
{
    /// <summary>The sounds it plays, loaded on a worker thread when it is made (<see cref="AudioLoader.Preload"/>).</summary>
    readonly List<string> _sounds = new();
    AudioLoader _loader = null!;
    AudioStreamPlayer3D? _positional;
    AudioStreamPlayer? _flat;
    bool _looping, _continuous, _random, _randomPosition, _active, _audible;
    float _rangeX, _rangeY, _elevation, _maxDistance;
    double _interval, _intervalVar;
    int _volume = 127, _volumeVar, _times = 3, _playVolume = 127;
    uint _hours;
    double _wait;
    int _next;
    bool _onceDone;

    public static SoundView? Create(SpawnMessage spawn, AudioLoader loader)
    {
        if (spawn.Extra.Length == 0) return null;
        using var doc = JsonDocument.Parse(spawn.Extra);
        var j = doc.RootElement;
        var view = new SoundView { _loader = loader };
        view.Init(spawn);
        foreach (var s in j.GetProperty("sounds").EnumerateArray())
            if (s.GetString() is { Length: > 0 } name) view._sounds.Add(name);
        if (view._sounds.Count == 0) return null;
        view._loader = loader;
        loader.Preload(view._sounds);
        view._active = j.GetProperty("active").GetBoolean();
        view._looping = j.GetProperty("looping").GetBoolean();
        view._continuous = j.GetProperty("continuous").GetBoolean();
        view._random = j.GetProperty("random").GetBoolean();
        view._randomPosition = j.GetProperty("randomPosition").GetBoolean();
        view._rangeX = j.GetProperty("rangeX").GetSingle();
        view._rangeY = j.GetProperty("rangeY").GetSingle();
        view._elevation = j.GetProperty("elevation").GetSingle();
        view._interval = j.GetProperty("interval").GetInt32() / 1000.0;
        view._intervalVar = j.GetProperty("intervalVariation").GetInt32() / 1000.0;
        view._volume = Math.Clamp(j.GetProperty("volume").GetInt32(), 0, 127);
        view._volumeVar = j.TryGetProperty("volumeVariation", out var vv) ? vv.GetInt32() : 0;
        view._times = j.TryGetProperty("times", out var times) ? times.GetInt32() : 3;
        view._hours = j.TryGetProperty("hours", out var hours) ? hours.GetUInt32() : 0;
        view._maxDistance = Math.Max(0.1f, j.GetProperty("maxDistance").GetSingle());
        if (j.GetProperty("positional").GetBoolean())
        {
            // Distance is applied by hand (the original's linear fade from the listener); Godot only pans.
            view._positional = new AudioStreamPlayer3D
            {
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
                MaxDistance = 0, AttenuationFilterCutoffHz = 20500, // Godot's camera-based cut-off and filter off
                Position = new Vector3(0, 0, view._elevation),
                Bus = OpenWinter.Client.Preferences.AudioBuses.Ambient3D,
            };
            view.AddChild(view._positional);
            view._positional.Finished += view.OnFinished;
        }
        else
        {
            view._flat = new AudioStreamPlayer { Bus = OpenWinter.Client.Preferences.AudioBuses.Ambient };
            view.AddChild(view._flat);
            view._flat.Finished += view.OnFinished;
        }
        view._audible = view.TimesAllow();
        view._wait = 0;
        return view;
    }

    bool TimesAllow() => SoundObjectRules.Audible(_times, _hours, AudioWorld.Night, AudioWorld.Hour);

    void OnFinished()
    {
        if (!_active) return;
        // Looping sounds go straight on; the others wait their gap. A sound that is neither looping nor continuous
        // plays its list once and stops (SoundObjectPlay starts it again from the first sound).
        if (!_looping && !_continuous && !_random && _next >= _sounds.Count) { _onceDone = true; _wait = 1e9; return; }
        if (!_looping && !_continuous && _random) { _onceDone = true; _wait = 1e9; return; }
        _wait = _looping ? 0 : SoundObjectRules.Gap(_interval, _intervalVar, GD.Randf());
    }

    bool Playing => (_positional?.Playing ?? false) || (_flat?.Playing ?? false);

    public override void _Process(double delta)
    {
        base._Process(delta);
        bool audible = TimesAllow();
        if (audible != _audible)
        {
            // The hour or day/night changing starts or silences the sound at once (audiofollow).
            _audible = audible;
            if (!audible) StopPlayers();
            else { _wait = 0; _onceDone = false; }
        }
        if (_positional != null && Playing) UpdateDistance();
        if (!_active || !_audible || _onceDone || Playing) return;
        _wait -= delta;
        if (_wait > 0) return;
        PlayNext();
    }

    void PlayNext()
    {
        int index = _random ? GD.RandRange(0, _sounds.Count - 1) : _next % _sounds.Count;
        // Still loading: try again next frame. A sound that can't be loaded is dropped.
        if (!_loader.TryGetLoaded(_sounds[index], out var loaded)) return;
        if (loaded == null)
        {
            _sounds.RemoveAt(index);
            if (_sounds.Count == 0) _active = false;
            return;
        }
        _next++;
        var stream = _looping ? _loader.GetLooping(_sounds[index]) ?? loaded : loaded;
        _playVolume = SoundObjectRules.Volume(_volume, _volumeVar, GD.Randf());
        if (_positional != null)
        {
            _positional.Position = _randomPosition
                ? new Vector3((float)GD.RandRange(-_rangeX, _rangeX), (float)GD.RandRange(-_rangeY, _rangeY), _elevation)
                : new Vector3(0, 0, _elevation);
            _positional.Stream = stream;
            UpdateDistance();
            _positional.Play();
        }
        else if (_flat != null)
        {
            _flat.Stream = stream;
            _flat.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, SoundObjectRules.FlatLevel * SoundObjectRules.Gain(_playVolume)));
            _flat.Play();
        }
        _wait = 1e9;
    }

    void UpdateDistance()
    {
        if (_positional == null) return;
        float distance = AudioWorld.Listener is { } listener ? _positional.GlobalPosition.DistanceTo(listener) : 0f;
        float gain = SoundObjectRules.PannedCompensation * SoundObjectRules.PositionalLevel * SoundObjectRules.Gain(_playVolume)
            * SoundObjectRules.DistanceGain(distance, _maxDistance);
        _positional.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, gain));
    }
}
