using System.Numerics;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>
/// Where an area's sun or moon light comes from. The original keeps one fixed direction, up in the north-east
/// (about 49 degrees up, bearing 42), at every hour of the day and night and in every kind of area (exterior,
/// natural, interior, underground, with or without a day/night cycle): scenes sundir and sunshadow. It is the
/// direction GetAreaLightDirection reports before a script sets one; SetAreaLightDirection replaces it.
/// </summary>
public static class AreaLightDirection
{
    /// <summary>Towards the light in area space (x east, y north, z up), as GetAreaLightDirection reports it.</summary>
    public static readonly Vector3 Default = new(0.433224291f, 0.487377316f, 0.758142531f);

    /// <summary>
    /// Towards the light (area space) for the light now shown: the moon's at night, the sun's otherwise; a
    /// script-set direction where there is one, else <see cref="Default"/>. The hour doesn't move it.
    /// </summary>
    public static Vector3 Current(bool sun, Vector3? scriptSun, Vector3? scriptMoon) =>
        (sun ? scriptSun : scriptMoon) is { } set && set.LengthSquared() > 1e-12f ? Vector3.Normalize(set) : Default;

    /// <summary>An area-space (Z up) direction in the renderer's space (Y up): (x, z, -y).</summary>
    public static Vector3 ToRenderer(Vector3 area) => new(area.X, area.Z, -area.Y);

    /// <summary>A renderer-space (Y up) direction in area space (Z up): (x, -z, y).</summary>
    public static Vector3 FromRenderer(Vector3 renderer) => new(renderer.X, -renderer.Z, renderer.Y);
}
