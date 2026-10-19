namespace OpenWinter.Client.Gameplay;

/// <summary>
/// The area's music channel as the original runs it, measured with the `musicbattle`, `musicbattle2` and
/// `audiofollow` scenes (docs/AUDIO.md). One track plays at a time; this decides which, when it starts (always
/// from its beginning), how it fades and when the battle stinger plays. Godot-free: <see cref="Update"/> is called
/// every frame and the player applies <see cref="Track"/>, <see cref="Gain"/>, <see cref="TakeRestart"/> and
/// <see cref="TakeStinger"/>.
/// </summary>
public sealed class MusicDirector
{
    /// <summary>A track leaving (changed, stopped, or giving way to battle music) fades out linearly over this long.</summary>
    public const double FadeSeconds = 0.85;
    /// <summary>At dusk and dawn the day or night track fades out over this long before the other one starts (audiofollow).</summary>
    public const double DayNightFadeSeconds = 10.0;

    // What should play.
    string _day = "", _night = "", _battle = "", _battleStinger = "";
    bool _night_, _backgroundOn = true, _battleOn;
    double _delay;

    // What plays.
    string _track = "";
    bool _trackIsBattle;
    double _fade = -1;      // seconds left of a fade-out, or -1
    double _fadeLength = FadeSeconds;
    bool _nightChanged;
    double _wait;           // silence before the background may start
    bool _restart;
    string _stinger = "";

    /// <summary>The resource on the channel now ("" = silence).</summary>
    public string Track => _track;
    /// <summary>Fade multiplier, 0..1.</summary>
    public float Gain => _fade < 0 ? 1f : (float)Math.Clamp(_fade / _fadeLength, 0, 1);
    public bool BattlePlaying => _track.Length > 0 && _trackIsBattle;

    /// <summary>True once when <see cref="Track"/> must (re)start from its beginning.</summary>
    public bool TakeRestart() { bool r = _restart; _restart = false; return r; }
    /// <summary>A one-shot to play now (the battle track's Stinger1 when battle music ends), or "".</summary>
    public string TakeStinger() { var s = _stinger; _stinger = ""; return s; }

    /// <summary>A new area: its tracks start at once, from the beginning, with no fade.</summary>
    public void Enter(string day, string night, string battle, string battleStinger, double delaySeconds, bool isNight)
    {
        _day = day; _night = night; _battle = battle; _battleStinger = battleStinger; _delay = Math.Max(0, delaySeconds);
        _night_ = isNight; _backgroundOn = true; _battleOn = false;
        _fade = -1; _wait = 0; _stinger = "";
        Start(Desired(), BattleDesired());
    }

    public void SetTracks(string day, string night, string battle, string battleStinger)
    {
        _day = day; _night = night; _battle = battle; _battleStinger = battleStinger;
    }

    public void SetDelay(double seconds) => _delay = Math.Max(0, seconds);
    public void SetNight(bool night)
    {
        if (night != _night_) _nightChanged = true;
        _night_ = night;
    }
    public void SetBackground(bool on)
    {
        // MusicBackgroundPlay starts the track at once, from its beginning, even inside the delay after a track.
        if (on && !_backgroundOn) _wait = 0;
        _backgroundOn = on;
    }

    public void SetBattle(bool on)
    {
        if (_battleOn == on) return;
        _battleOn = on;
        if (!on && _trackIsBattle && _track.Length > 0)
        {
            // Battle music ends at once with its stinger; the background comes back MusicDelay plus a fade later.
            _stinger = _battleStinger;
            _track = "";
            _fade = -1;
            _wait = _delay + FadeSeconds;
        }
    }

    bool BattleDesired() => _battleOn && _battle.Length > 0;
    string Desired() => BattleDesired() ? _battle : _backgroundOn ? (_night_ ? _night : _day) : "";

    void Start(string track, bool battle)
    {
        _track = track;
        _trackIsBattle = battle;
        _fade = -1;
        _restart = track.Length > 0;
    }

    /// <param name="finished">The playing track reached its end this frame.</param>
    public void Update(double dt, bool finished)
    {
        if (_fade >= 0)
        {
            _fade -= dt;
            if (_fade > 0) return;
            // Faded out: whatever is wanted now starts straight away.
            _fade = -1;
            _track = "";
            _wait = 0;
        }
        if (_track.Length > 0 && finished)
        {
            // Battle music loops; the background waits MusicDelay in silence, then plays again from the start.
            if (_trackIsBattle) { _restart = true; return; }
            _track = "";
            _wait = _delay;
        }
        string want = Desired();
        bool battle = BattleDesired();
        if (_track.Length > 0)
        {
            if (!string.Equals(want, _track, StringComparison.OrdinalIgnoreCase) || battle != _trackIsBattle)
            {
                _fadeLength = _nightChanged && !battle && _backgroundOn ? DayNightFadeSeconds : FadeSeconds;
                _fade = _fadeLength;
            }
            _nightChanged = false;
            return;
        }
        _nightChanged = false;
        if (want.Length == 0) return;
        if (!battle && _wait > 0) { _wait -= dt; if (_wait > 0) return; }
        _wait = 0;
        Start(want, battle);
    }
}

/// <summary>
/// The area's ambient sound loop (AmbientSnd*): measured with `soundobj` / `audiofollow`. Stopping or changing it
/// fades the current loop out linearly over six seconds; only then does whatever is wanted start, at full volume.
/// Volumes are on the area's 0-127 scale (AmbientSoundSet*Volume accepts 0-100 of it).
/// </summary>
public sealed class AmbientDirector
{
    public const double FadeSeconds = 6.0;
    string _want = "", _playing = "";
    int _wantVolume = 127;
    bool _on = true;
    double _fade = -1;
    bool _restart;

    public string Playing => _playing;
    public float Gain => (_fade < 0 ? 1f : (float)Math.Clamp(_fade / FadeSeconds, 0, 1)) * Math.Clamp(_wantVolume, 0, 127) / 127f;
    public bool TakeRestart() { bool r = _restart; _restart = false; return r; }

    public void Enter(string resource, int volume)
    {
        _want = resource; _wantVolume = volume; _on = true; _fade = -1;
        _playing = resource; _restart = resource.Length > 0;
    }

    public void Set(string resource, int volume, bool on)
    {
        _want = resource; _wantVolume = volume; _on = on;
    }

    public void Update(double dt)
    {
        string want = _on ? _want : "";
        if (_fade >= 0)
        {
            _fade -= dt;
            if (_fade > 0) return;
            _fade = -1;
            _playing = "";
        }
        if (_playing.Length > 0)
        {
            if (!string.Equals(want, _playing, StringComparison.OrdinalIgnoreCase)) _fade = FadeSeconds;
            return;
        }
        if (want.Length == 0) return;
        _playing = want;
        _restart = true;
    }
}
