namespace OpenWinter.Client.Gameplay;

/// <summary>How the client reads a script's camera calls (the "camera" notifies).</summary>
public static class CameraScript
{
    /// <summary>
    /// SetCameraFacing's direction in degrees from east, 0 to 360; null for -1, which keeps the camera's own. Other
    /// negative directions turn the other way round (visual scene camglide3: -90 faces as 270 does).
    /// </summary>
    public static float? Direction(float degrees)
    {
        if (!float.IsFinite(degrees) || Math.Abs(degrees + 1) < 1e-3f) return null;
        float d = degrees % 360f;
        return d < 0 ? d + 360f : d;
    }

    /// <summary>SetCameraLimits' distances (-1 for the client's own), never with the least above the most.</summary>
    public static (float Min, float Max) DistanceLimits(float min, float max, float defaultMin, float defaultMax)
    {
        float lo = min >= 0 ? min : defaultMin, hi = max >= 0 ? max : defaultMax;
        return lo <= hi ? (lo, hi) : (hi, lo);
    }
}

/// <summary>
/// AttachCamera: the object the camera should follow, kept until the script hands the camera back or the area
/// changes. The object may not be shown yet (created by the same script, or out of sight): the camera goes to it
/// once it is, and again whenever it comes back into view.
/// </summary>
public sealed class CameraAttachment
{
    /// <summary>The object asked for; 0 for the player's own character.</summary>
    public uint Target { get; private set; }
    /// <summary>The script asked for a clear view of it (applied when the camera first reaches it).</summary>
    public bool ClearView { get; private set; }
    bool _reached;

    /// <summary>A script's AttachCamera: <paramref name="target"/> the object, the player's own character or none.</summary>
    public void Request(uint target, uint player, uint invalid, bool clearView)
    {
        Target = target == player || target == invalid ? 0 : target;
        ClearView = clearView;
        _reached = false;
    }

    public void Reset() => (Target, ClearView, _reached) = (0, false, false);

    /// <summary>
    /// What the camera should do now: follow the object (and whether to look for a clear view, the first time) when it
    /// is shown and the camera isn't on it already; null when nothing changes.
    /// </summary>
    public (uint Target, bool ClearView)? Due(Func<uint, bool> shown, uint following)
    {
        if (Target == 0 || following == Target || !shown(Target)) return null;
        bool clear = ClearView && !_reached;
        _reached = true;
        return (Target, clear);
    }
}
