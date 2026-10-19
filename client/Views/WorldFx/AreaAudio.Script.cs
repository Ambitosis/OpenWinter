using System.Text.Json;
using OpenWinter.Client.Views.WorldFx;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Audio;

/// <summary>
/// Script control of the area's music and ambience (MusicBackground*, MusicBattle*, AmbientSound* through the
/// area-audio notify), the server's battle music for this player (battle-music notify), and the switch between
/// the day and night tracks as the area's lighting crosses dusk and dawn.
/// </summary>
public partial class AreaAudio
{
    int _dayTrack, _nightTrack, _battleTrack, _dayAmbient, _nightAmbient;
    /// <summary>Ambient volumes on the area's 0-127 scale (AmbientSoundSet*Volume sets 0-100 of it).</summary>
    int _dayAmbientVolume = 127, _nightAmbientVolume = 127;
    bool _musicOn = true, _scriptBattle, _autoBattle, _ambientOn = true, _isNight;
    double _musicDelay;

    string AmbientResource => Row("ambientsound", _isNight ? _nightAmbient : _dayAmbient);
    int AmbientVolume => _isNight ? _nightAmbientVolume : _dayAmbientVolume;

    /// <summary>Resources playing now ("" = none): background music, battle music, ambience.</summary>
    public string CurrentMusic => _director.BattlePlaying ? "" : _director.Track;
    public string CurrentBattle => _director.BattlePlaying ? _director.Track : "";
    public string CurrentAmbient => _ambience.Playing;

    void TakeArea(LoadAreaMessage area)
    {
        _dayTrack = area.MusicDay;
        _nightTrack = area.MusicNight;
        _battleTrack = area.MusicBattle;
        _musicDelay = Math.Max(0, area.MusicDelay / 1000.0);
        _dayAmbient = area.AmbientDay;
        _nightAmbient = area.AmbientNight;
        _dayAmbientVolume = Math.Clamp(area.AmbientDayVolume, 0, 127);
        _nightAmbientVolume = Math.Clamp(area.AmbientNightVolume, 0, 127);
        _musicOn = _ambientOn = true;
        _scriptBattle = _autoBattle = false;
        _isNight = area.IsNight;
    }

    /// <summary>Kept for the area-loaded hook; the area's settings are taken in <see cref="Configure"/>.</summary>
    public void Remember(LoadAreaMessage area) { }

    /// <summary>Applies an area-audio notify (fields missing from it keep their value).</summary>
    public void ApplyScript(JsonElement d)
    {
        _dayTrack = J.I(d, "musicDay", _dayTrack);
        _nightTrack = J.I(d, "musicNight", _nightTrack);
        _battleTrack = J.I(d, "musicBattle", _battleTrack);
        if (J.Has(d, "musicDelay")) _musicDelay = Math.Max(0, J.I(d, "musicDelay") / 1000.0);
        _musicOn = J.B(d, "musicPlaying", _musicOn);
        _scriptBattle = J.B(d, "battlePlaying", _scriptBattle);
        _dayAmbient = J.I(d, "ambientDay", _dayAmbient);
        _nightAmbient = J.I(d, "ambientNight", _nightAmbient);
        _dayAmbientVolume = Math.Clamp(J.I(d, "ambientDayVolume", _dayAmbientVolume), 0, 127);
        _nightAmbientVolume = Math.Clamp(J.I(d, "ambientNightVolume", _nightAmbientVolume), 0, 127);
        _ambientOn = J.B(d, "ambientPlaying", _ambientOn);
        Refresh();
    }

    /// <summary>The server's battle music for this player: on when a hostile attacks her, off a while after the fight.</summary>
    public void SetAutoBattle(bool on)
    {
        _autoBattle = on;
        Refresh();
    }

    /// <summary>Day and night have their own music and ambience; switches when the area's lighting turns.</summary>
    public void SetNight(bool night)
    {
        if (!_configured || night == _isNight) return;
        _isNight = night;
        Refresh();
    }

    void Refresh()
    {
        if (!_configured) return;
        _director.SetTracks(Row("ambientmusic", _dayTrack), Row("ambientmusic", _nightTrack), Row("ambientmusic", _battleTrack), Stinger(_battleTrack));
        _director.SetDelay(_musicDelay);
        _director.SetNight(_isNight);
        _director.SetBackground(_musicOn);
        _director.SetBattle(_scriptBattle || _autoBattle);
        _ambience.Set(AmbientResource, AmbientVolume, _ambientOn);
        if (WorldFxPlayer.Verbose) GD.Print($"[audio] music={CurrentMusic} battle={CurrentBattle} ambient={CurrentAmbient} night={_isNight}");
    }
}
