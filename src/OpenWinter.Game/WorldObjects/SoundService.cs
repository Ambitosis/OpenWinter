using System.Buffers.Binary;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.WorldObjects;

/// <summary>One-shot sounds, sound objects, area music/ambience and sound lengths.</summary>
public static class SoundService
{
    /// <summary>Plays a sound at an object (PlaySound / PlaySoundByStrRef).</summary>
    public static void Play(World world, GameObject source, string resref, int strRef = -1)
    {
        if (strRef >= 0 && resref.Length == 0) resref = world.Resources.Talk.Sound(unchecked((uint)strRef));
        if (resref.Length == 0) return;
        world.Output?.Notify("play-sound", source, new { resref, position = Vec(source.Position), strref = strRef });
    }

    public static float[] Vec(System.Numerics.Vector3 v) => new[] { v.X, v.Y, v.Z };

    /// <summary>Tells clients in the area about its music/ambient state.</summary>
    public static void NotifyAudio(World world, Area area)
    {
        world.Output?.Notify("area-audio", area, AudioData(AreaSettings.Of(world, area)));
    }

    /// <summary>The "area-audio" notify payload.</summary>
    public static object AudioData(AreaEnvironment env) => new
    {
        musicDay = env.MusicDay, musicNight = env.MusicNight, musicBattle = env.MusicBattle, musicDelay = env.MusicDelay,
        musicPlaying = env.MusicPlaying, battlePlaying = env.BattlePlaying,
        ambientDay = env.AmbientDay, ambientNight = env.AmbientNight,
        ambientDayVolume = env.AmbientDayVolume, ambientNightVolume = env.AmbientNightVolume, ambientPlaying = env.AmbientPlaying,
    };

    public static void NotifySoundObject(World world, Sound s, string action) =>
        world.Output?.Notify("sound-object", s, new { action, active = s.Active, volume = s.Volume, position = Vec(s.Position), sounds = s.Sounds });

    /// <summary>Duration in seconds of a WAV resource (0 if missing or not PCM-measurable).</summary>
    public static float WavDuration(ResMan rm, string resref)
    {
        if (resref.Length == 0 || rm.Read(resref, ResType.Wav) is not { } data) return 0f;
        // Voice files may carry a 470-byte fake header in front of the real RIFF.
        int start = data.Length > 478 && data.AsSpan(470, 4).SequenceEqual("RIFF"u8) ? 470 : 0;
        if (data.Length < start + 12 || !data.AsSpan(start, 4).SequenceEqual("RIFF"u8) || !data.AsSpan(start + 8, 4).SequenceEqual("WAVE"u8)) return 0f;
        int pos = start + 12;
        int byteRate = 0;
        while (pos + 8 <= data.Length)
        {
            var id = data.AsSpan(pos, 4);
            int size = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(pos + 4));
            if (id.SequenceEqual("fmt "u8) && pos + 16 <= data.Length) byteRate = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(pos + 16));
            else if (id.SequenceEqual("data"u8))
            {
                if (byteRate <= 0) return 0f;
                int length = Math.Min(size, data.Length - pos - 8);
                return (float)length / byteRate;
            }
            if (size < 0) break;
            pos += 8 + size + (size & 1);
        }
        return 0f;
    }
}

/// <summary>Rolls area weather from ChanceRain/ChanceSnow when scripts haven't fixed it (SetWeather).</summary>
public sealed class WeatherSystem : IWorldSystem
{
    /// <summary>How often outdoor areas re-roll their weather, in seconds.</summary>
    public const double RollInterval = 300;
    readonly World _world;
    double _next;
    public int Order => 30;

    public WeatherSystem(World world) { _world = world; }

    public void Step(float dt)
    {
        if (_world.Time < _next) return;
        _next = _world.Time + 5;
        foreach (var area in _world.Areas)
        {
            // Only areas someone has looked at or that players are in need rolling.
            if (area.Environment == null && !area.Objects.Any(o => o is Creature { IsPC: true })) continue;
            var env = AreaSettings.Of(_world, area);
            if (_world.Time < env.NextWeatherRoll) continue;
            int before = Current(area, env);
            Roll(env);
            env.NextWeatherRoll = _world.Time + RollInterval;
            int after = Current(area, env);
            if (after != before) _world.Output?.Notify("weather", area, Data(area, env));
        }
    }

    public static void Roll(AreaEnvironment env)
    {
        int r = Random.Shared.Next(100);
        env.RolledWeather = r < env.ChanceSnow ? 2 : r < env.ChanceSnow + env.ChanceRain ? 1 : 0;
    }

    /// <summary>The weather notify payload: WEATHER_* and the area's ChanceLightning (percent) for storms.</summary>
    public static object Data(Area area, AreaEnvironment env) => new { weather = Current(area, env), lightning = env.ChanceLightning };

    /// <summary>GetWeather: WEATHER_CLEAR indoors, the SetWeather override, or the rolled weather.</summary>
    public static int Current(Area area, AreaEnvironment env)
    {
        if (area.IsInterior) return 0;
        return env.WeatherOverride >= 0 ? env.WeatherOverride : env.RolledWeather;
    }
}
