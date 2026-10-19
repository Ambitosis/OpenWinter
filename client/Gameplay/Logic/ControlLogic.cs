using OpenWinter.Client.Preferences;

namespace OpenWinter.Client.Gameplay;

/// <summary>
/// The original's three camera modes, numbered as the scripting constants (CAMERA_MODE_*). The toggle key steps
/// through them in the order the original's tutorial introduces them: top down, chase, driving.
/// </summary>
public enum CameraMode
{
    /// <summary>The camera follows behind the player as they move (CAMERA_MODE_CHASE_CAMERA).</summary>
    Chase = 0,
    /// <summary>The default: the camera stays where the player turns it (CAMERA_MODE_TOP_DOWN).</summary>
    TopDown = 1,
    /// <summary>Stays right behind the player at a fixed distance; turning keys turn both (CAMERA_MODE_STIFF_CHASE_CAMERA).</summary>
    Driving = 2,
}

public static class CameraModes
{
    public static CameraMode Next(CameraMode m) => m switch
    {
        CameraMode.TopDown => CameraMode.Chase,
        CameraMode.Chase => CameraMode.Driving,
        _ => CameraMode.TopDown,
    };

    public static CameraMode FromInt(int v) => v is >= 0 and <= 2 ? (CameraMode)v : CameraMode.TopDown;

    public static string Name(CameraMode m) => m switch
    {
        CameraMode.Chase => "Chase camera",
        CameraMode.Driving => "Driving camera",
        _ => "Top-down camera",
    };

    /// <summary>The driving camera keeps this distance (the original does not let it zoom).</summary>
    public const float DrivingDistance = 6f;

    /// <summary>
    /// Turns <paramref name="current"/> toward <paramref name="target"/> (radians) the short way round, by at most
    /// <paramref name="maxStep"/>; the result stays within one turn of <paramref name="current"/> so it never spins.
    /// </summary>
    public static float TurnToward(float current, float target, float maxStep)
    {
        float diff = Wrap(target - current);
        return Math.Abs(diff) <= maxStep ? current + diff : current + MathF.CopySign(maxStep, diff);
    }

    /// <summary>An angle in (-π, π].</summary>
    public static float Wrap(float a)
    {
        a %= MathF.Tau;
        if (a <= -MathF.PI) a += MathF.Tau;
        else if (a > MathF.PI) a -= MathF.Tau;
        return a;
    }
}

/// <summary>Numpad navigation of the radial menu, whose wedges run clockwise from the top.</summary>
public static class RadialNav
{
    /// <summary>Compass bearing of a direction in degrees, clockwise from the top.</summary>
    public static float Bearing(RadialDirection d) => (int)d * 45f;

    /// <summary>
    /// The wedge nearest a numpad direction on a ring of <paramref name="wedges"/> drawn wedges of which the first
    /// <paramref name="used"/> hold entries, or -1 when that wedge is empty. A direction halfway between two
    /// wedges takes the even one, which keeps left and right mirror images (on six wedges, 4 and 6 pick the two
    /// lower side wedges).
    /// </summary>
    public static int Wedge(int wedges, int used, RadialDirection d)
    {
        if (wedges <= 0 || used <= 0) return -1;
        float seg = 360f / wedges;
        int i = (int)MathF.Round(Bearing(d) / seg) % wedges;
        return i < used ? i : -1;
    }
}

/// <summary>
/// A hold key that a quick double tap latches on until the next press (the original's tooltip key: hold Tab to
/// show names, double-tap within its threshold, 200 ms by default, to keep them up).
/// </summary>
public sealed class TapLatch
{
    public double Threshold = 0.2;
    double _lastPress = double.NegativeInfinity;
    public bool Latched { get; private set; }

    /// <summary>The key went down at <paramref name="now"/> (seconds).</summary>
    public void Press(double now)
    {
        if (Latched) { Latched = false; _lastPress = double.NegativeInfinity; return; }
        if (now - _lastPress <= Threshold) { Latched = true; _lastPress = double.NegativeInfinity; return; }
        _lastPress = now;
    }

    public void Reset() { Latched = false; _lastPress = double.NegativeInfinity; }

    /// <summary>Whether the effect shows: latched, or the key is held.</summary>
    public bool Active(bool held) => Latched || held;
}

/// <summary>
/// Quick groups: remembered selections the player can call back with a key. Our client selects one object at a
/// time, so a group holds the objects selected when it was set (one today); picking it back skips any that are gone.
/// </summary>
public sealed class QuickGroups
{
    readonly List<uint>[] _groups;

    public QuickGroups(int count = ActionCatalog.QuickGroups)
    {
        _groups = new List<uint>[count];
        for (int i = 0; i < count; i++) _groups[i] = new();
    }

    public int Count => _groups.Length;

    public void Set(int group, IEnumerable<uint> ids)
    {
        if (group < 0 || group >= _groups.Length) return;
        _groups[group] = ids.Where(id => id != 0).Distinct().ToList();
    }

    public IReadOnlyList<uint> Get(int group) => group >= 0 && group < _groups.Length ? _groups[group] : Array.Empty<uint>();

    /// <summary>The group's objects that still exist (the group forgets the rest).</summary>
    public IReadOnlyList<uint> Pick(int group, Func<uint, bool> exists)
    {
        if (group < 0 || group >= _groups.Length) return Array.Empty<uint>();
        _groups[group].RemoveAll(id => !exists(id));
        return _groups[group];
    }

    public void Clear() { foreach (var g in _groups) g.Clear(); }
}
