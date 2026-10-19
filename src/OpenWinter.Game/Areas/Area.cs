using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game.Areas;

public sealed partial class Area : GameObject
{
    /// <summary>The area's triggers, cached for movement checks (cleared when one is added or removed).</summary>
    public Objects.Trigger[]? TriggerCache;
    /// <summary>The area's doors, cached for sight and room checks (cleared when one is added or removed).</summary>
    public Objects.Door[]? DoorCache;
    /// <summary>The area's doors. A new array whenever one has been added or removed since the last call.</summary>
    public Objects.Door[] Doors => DoorCache ??= Objects.OfType<Objects.Door>().ToArray();

    public override ObjectType Type => ObjectType.Area;

    public const float TileSize = 10f;

    public GffStruct Are = new();
    /// <summary>The GIT's AreaProperties (music, ambient sound).</summary>
    public GffStruct Properties = new();
    public int MusicDay, MusicNight, MusicBattle, AmbientDay, AmbientNight, AmbientDayVolume = 64, AmbientNightVolume = 64, MusicDelay;
    public string Tileset = "";
    public int Width, Height;
    public TileInfo[] Tiles = Array.Empty<TileInfo>();
    public float TileHeightStep = 5f;
    public readonly Walkmesh Walkmesh = new();
    public readonly List<GameObject> Objects = new();

    public bool IsInterior => (Are.GetInt("Flags") & 1) != 0;
    public bool IsUnderground => (Are.GetInt("Flags") & 2) != 0;
    public bool IsNatural => (Are.GetInt("Flags") & 4) != 0;

    public LoadAreaMessage Describe(TalkTable? tlk) => new()
    {
        AreaId = Id,
        ResRef = ResRef,
        Name = Name.Resolve(tlk),
        Tileset = Tileset,
        Width = Width,
        Height = Height,
        Tiles = Tiles,
        IsNight = Are.GetInt("IsNight") != 0,
        IsInterior = IsInterior,
        IsUnderground = IsUnderground,
        IsNatural = IsNatural,
        SunAmbient = (uint)Are.GetInt("SunAmbientColor"),
        SunDiffuse = (uint)Are.GetInt("SunDiffuseColor"),
        SunFogColor = (uint)Are.GetInt("SunFogColor"),
        MoonAmbient = (uint)Are.GetInt("MoonAmbientColor"),
        MoonDiffuse = (uint)Are.GetInt("MoonDiffuseColor"),
        MoonFogColor = (uint)Are.GetInt("MoonFogColor"),
        SunFogAmount = (byte)Are.GetInt("SunFogAmount"),
        MoonFogAmount = (byte)Are.GetInt("MoonFogAmount"),
        SunShadows = Are.GetInt("SunShadows") != 0,
        MoonShadows = Are.GetInt("MoonShadows") != 0,
        FogClipDistance = Are.GetFloat("FogClipDist", 45f),
        SkyBox = Are.GetInt("SkyBox").ToString(),
        MusicDay = MusicDay, MusicNight = MusicNight, MusicBattle = MusicBattle,
        AmbientDay = AmbientDay, AmbientNight = AmbientNight, AmbientDayVolume = AmbientDayVolume, AmbientNightVolume = AmbientNightVolume,
        MusicDelay = MusicDelay,
        DayNightCycle = Are.GetInt("DayNightCycle") != 0,
        LoadScreen = (int)Are.GetInt("LoadScreenID"),
        ShadowOpacity = (byte)Math.Clamp(Are.GetInt("ShadowOpacity", 60), 0, 100),
    };

    public IEnumerable<T> ObjectsOf<T>() where T : GameObject => Objects.OfType<T>();
}
