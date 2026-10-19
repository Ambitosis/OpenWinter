using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Audio;

/// <summary>
/// An area's music (background day/night track, battle track and its stinger) and ambient loop, played the way
/// the original does (docs/AUDIO.md): <see cref="MusicDirector"/> and <see cref="AmbientDirector"/> decide what
/// plays and how it fades; this node turns that into players on the Music and Ambient buses.
/// </summary>
public partial class AreaAudio : Node
{
    public required AudioLoader Loader;
    public required OpenWinter.Aurora.Resources.ResMan Resources;

    /// <summary>
    /// Player level of music and stingers. The original plays music at 0.70 (-3.1 dB) of the file's level with the
    /// slider at full (musicbattle); OpenWinter's chain already comes out about 3 dB under the file at 0 dB here
    /// (musiclevel, audiofollow recordings), so 0 dB matches the original's level.
    /// </summary>
    public const float MusicLevelDb = 0f;

    AudioStreamPlayer _music = null!, _stinger = null!, _ambient = null!;
    readonly MusicDirector _director = new();
    readonly AmbientDirector _ambience = new();
    bool _finished, _configured;

    public override void _Ready()
    {
        _music = new AudioStreamPlayer { Name = "Music", Bus = OpenWinter.Client.Preferences.AudioBuses.Music, VolumeDb = MusicLevelDb };
        _stinger = new AudioStreamPlayer { Name = "Stinger", Bus = OpenWinter.Client.Preferences.AudioBuses.Music, VolumeDb = MusicLevelDb };
        _ambient = new AudioStreamPlayer { Name = "Ambient", Bus = OpenWinter.Client.Preferences.AudioBuses.Ambient };
        AddChild(_music);
        AddChild(_stinger);
        AddChild(_ambient);
        _music.Finished += () => _finished = true;
    }

    public void Stop()
    {
        _music.Stop();
        _stinger.Stop();
        _ambient.Stop();
        _music.Stream = null;
        _ambient.Stream = null;
        _configured = false;
        _autoBattle = false;
    }

    /// <summary>The area's own settings, as its description gives them, once its presentation is complete.</summary>
    public void Configure(LoadAreaMessage area)
    {
        _music.Stop();
        _stinger.Stop();
        _ambient.Stop();
        TakeArea(area);
        _director.Enter(Row("ambientmusic", _dayTrack), Row("ambientmusic", _nightTrack), Row("ambientmusic", _battleTrack),
            Stinger(_battleTrack), _musicDelay, _isNight);
        _ambience.Enter(_ambientOn ? AmbientResource : "", AmbientVolume);
        _configured = true;
        Apply();
    }

    string Row(string table, int row) => row > 0 ? Resources.TwoDA(table).Get(row, "Resource") : "";
    string Stinger(int row) => row > 0 ? Resources.TwoDA("ambientmusic").Get(row, "Stinger1") : "";

    public override void _Process(double delta)
    {
        if (!_configured) return;
        _director.Update(delta, _finished);
        _finished = false;
        _ambience.Update(delta);
        Apply();
    }

    void Apply()
    {
        if (_director.TakeRestart())
        {
            _music.Stream = _director.Track.Length > 0 ? Loader.Get(_director.Track) : null;
            if (_music.Stream != null) _music.Play();
        }
        if (_director.Track.Length == 0 && _music.Playing) _music.Stop();
        _music.VolumeDb = MusicLevelDb + Mathf.LinearToDb(Math.Max(0.0001f, _director.Gain));
        if (_director.TakeStinger() is { Length: > 0 } stinger && Loader.Get(stinger) is { } stingerStream)
        {
            _stinger.Stream = stingerStream;
            _stinger.Play();
        }

        if (_ambience.TakeRestart())
        {
            _ambient.Stream = _ambience.Playing.Length > 0 ? Loader.GetLooping(_ambience.Playing) : null;
            if (_ambient.Stream != null) _ambient.Play();
        }
        if (_ambience.Playing.Length == 0 && _ambient.Playing) _ambient.Stop();
        _ambient.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, _ambience.Gain));
    }
}
