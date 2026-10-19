using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Areas
{
    public sealed partial class Area
    {
        /// <summary>Sound, weather, lighting and other scriptable settings; created on first use by <see cref="WorldObjects.AreaSettings.Of"/>.</summary>
        public WorldObjects.AreaEnvironment? Environment;
        /// <summary>The module area this one was instanced from (CreateArea/CopyArea), or "" for module areas.</summary>
        public string SourceResRef = "";
    }
}

namespace OpenWinter.Game.WorldObjects
{
    using OpenWinter.Game.Areas;

    /// <summary>One visual transform channel on an object (SetObjectVisualTransform).</summary>
    public sealed class VisualTransform
    {
        public float From, To;
        public int LerpType;
        public float LerpDuration;
        public double StartTime;
        public bool PauseWithGame;
        public int Behavior, Repeats;

        /// <summary>Value at a time, following the lerp curve (OBJECT_VISUAL_TRANSFORM_LERP_*).</summary>
        public float ValueAt(double time)
        {
            if (LerpType == 0 || LerpDuration <= 0) return To;
            double elapsed = time - StartTime;
            if (elapsed < 0) return From;
            int cycle = (int)(elapsed / LerpDuration);
            if (Repeats >= 0 && cycle > Repeats) return Behavior == 1 && Repeats % 2 == 1 ? From : To;
            float x = (float)(elapsed / LerpDuration - cycle);
            if (cycle > Repeats && Repeats >= 0) x = 1;
            bool reverse = Behavior == 1 && cycle % 2 == 1;
            if (reverse) x = 1 - x;
            float f = LerpType switch
            {
                1 => x,
                2 => x * x * (3 - 2 * x),
                3 => 0.5f - MathF.Sin(MathF.Asin(1f - 2f * x) / 3f),
                4 => 1 - MathF.Cos(x * MathF.PI * 0.5f),
                5 => MathF.Sin(x * MathF.PI * 0.5f),
                6 => x * x,
                7 => x * x * x * (x * (6f * x - 15f) + 10f),
                _ => 1,
            };
            return From + (To - From) * f;
        }

        public static float Default(int transform) => transform is 10 or 40 ? 1f : 0f;
    }

    /// <summary>Per-area settings scripts can read and change.</summary>
    public sealed partial class AreaEnvironment
    {
        public int MusicDay, MusicNight, MusicBattle, MusicDelay;
        public int AmbientDay, AmbientNight, AmbientDayVolume, AmbientNightVolume;
        public bool MusicPlaying = true, BattlePlaying, AmbientPlaying = true;
        public int ChanceRain, ChanceSnow, ChanceLightning, WindPower;
        /// <summary>SetWeather override (WEATHER_*), or -1 to follow the area settings.</summary>
        public int WeatherOverride = -1;
        /// <summary>Current weather when following the area settings.</summary>
        public int RolledWeather;
        public double NextWeatherRoll;
        public int SunFog, MoonFog;
        public int SkyBox;
        public bool NoRest;
        /// <summary>Light directions; until a script sets one (...Set) clients use their own default.</summary>
        public Vector3 SunDirection = new(-1f, -1f, -1f), MoonDirection = new(-1f, -1f, -1f);
        public bool SunDirectionSet, MoonDirectionSet;
        public Vector3 WindDirection = new(1, 1, 0);
        public float WindMagnitude, WindYaw, WindPitch;
        public bool TileBorderDisabled, DefaultGrassDisabled;
        public Dictionary<int, object> GrassOverrides = new();
        /// <summary>Explored tiles per player creature.</summary>
        public Dictionary<uint, bool[]> Explored = new();
        public bool LightingDirty;

        public static AreaEnvironment Load(ResMan rm, Area area)
        {
            var are = area.Are;
            var git = rm.ReadGff(area.SourceResRef.Length > 0 ? area.SourceResRef : area.ResRef, ResType.Git);
            var props = git?.GetStruct("AreaProperties") ?? new GffStruct();
            var env = new AreaEnvironment
            {
                MusicDay = (int)props.GetInt("MusicDay"),
                MusicNight = (int)props.GetInt("MusicNight"),
                MusicBattle = (int)props.GetInt("MusicBattle"),
                MusicDelay = (int)props.GetInt("MusicDelay"),
                AmbientDay = (int)props.GetInt("AmbientSndDay"),
                AmbientNight = (int)props.GetInt("AmbientSndNight"),
                AmbientDayVolume = (int)props.GetInt("AmbientSndDayVol", 127),
                AmbientNightVolume = (int)props.GetInt("AmbientSndNitVol", 127),
                ChanceRain = (int)are.GetInt("ChanceRain"),
                ChanceSnow = (int)are.GetInt("ChanceSnow"),
                ChanceLightning = (int)are.GetInt("ChanceLightning"),
                WindPower = (int)are.GetInt("WindPower"),
                SunFog = (int)are.GetInt("SunFogAmount"),
                MoonFog = (int)are.GetInt("MoonFogAmount"),
                SkyBox = (int)are.GetInt("SkyBox"),
                NoRest = are.GetInt("NoRest") != 0,
            };
            // Toolset wind presets (see SetAreaWind).
            (env.WindMagnitude, env.WindYaw, env.WindPitch) = env.WindPower switch { 1 => (1f, 100f, 3f), 2 => (2f, 150f, 5f), _ => (0f, 0f, 0f) };
            return env;
        }

        public AreaEnvironment Clone()
        {
            var c = (AreaEnvironment)MemberwiseClone();
            // Exploration is per player and not copied; grass overrides are.
            c.Explored = new Dictionary<uint, bool[]>();
            c.PersonalMapPins = new Dictionary<uint, List<PersonalMapPin>>();
            c.GrassOverrides = new Dictionary<int, object>(GrassOverrides);
            return c;
        }
    }

    public static class AreaSettings
    {
        /// <summary>The environment state of an area, loaded from its ARE/GIT on first use.</summary>
        public static AreaEnvironment Of(World world, Area area) => area.Environment ??= AreaEnvironment.Load(world.Resources, area);

        /// <summary>The area an "oArea" argument means: an area itself, the area an object is in, or the caller's area when invalid.</summary>
        public static Area? Resolve(GameObject? target, GameObject? self) => target switch
        {
            Area a => a,
            { Area: { } a } => a,
            null => self as Area ?? self?.Area,
            _ => null,
        };
    }
}
