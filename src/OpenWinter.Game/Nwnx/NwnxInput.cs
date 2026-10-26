using System.Numerics;
using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx;

/// <summary>
/// NWNX_ON_INPUT_KEYBOARD for a player steering with the keys (KEY W, A, S or D: the key pressed, judged from the
/// direction against the way the character faces), once each time the key changes; a skip ignores the input.
/// </summary>
public static class NwnxInput
{
    sealed class Last { public string Key = ""; }

    static readonly ConditionalWeakTable<Creature, Last> _last = new();

    /// <summary>True when a subscriber skipped the input.</summary>
    public static bool Keyboard(World world, Creature pc, Vector3 direction)
    {
        if (!Plugins.EventsPlugin.Listening(world, "NWNX_ON_INPUT_KEYBOARD")) return false;
        var forward = new Vector2(MathF.Cos(pc.Facing), MathF.Sin(pc.Facing));
        var d = Vector2.Normalize(new Vector2(direction.X, direction.Y));
        float along = Vector2.Dot(d, forward), side = forward.X * d.Y - forward.Y * d.X;
        string key = MathF.Abs(along) >= MathF.Abs(side) ? along >= 0 ? "W" : "S" : side > 0 ? "A" : "D";
        var last = _last.GetValue(pc, _ => new Last());
        if (last.Key == key) return false;
        var ev = NwnxEvent.Begin(world, "NWNX_ON_INPUT_KEYBOARD", pc)?.Add("KEY", key);
        bool skipped = ev?.Before() == true;
        ev?.After();
        if (!skipped) last.Key = key;
        return skipped;
    }

    /// <summary>The player let go of the keys.</summary>
    public static void Released(Creature pc)
    {
        if (_last.TryGetValue(pc, out var last)) last.Key = "";
    }
}
