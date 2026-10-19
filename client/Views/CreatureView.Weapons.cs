using Godot;
using OpenWinter.Game.Combat;
using OpenWinter.Net;

namespace OpenWinter.Client.Views;

/// <summary>
/// How a humanoid holds what is in its hands: the combat ready stance and the arm clips laid over idling, walking
/// and running (WeaponStances, measured on the original in parity/visual scenes weaponstance1..3).
/// </summary>
public partial class CreatureView
{
    /// <summary>The stance of a built model: what its hands hold, by the creature's size category.</summary>
    static WeaponStances.Stance StanceOf(Appearance a, ClientContent content, Content.ModelData model)
    {
        var items = content.Resources.TwoDA("baseitems");
        int size = content.Resources.TwoDA("appearance").GetInt(a.Type, "SIZECATEGORY", 3);
        int Base(int slot) => a.Equipment.FirstOrDefault(e => e.Slot == slot)?.BaseItem ?? -1;
        int right = Base(PartBasedBuilder.SlotRight);
        return WeaponStances.For(items, size, right, Base(PartBasedBuilder.SlotLeft),
            row => row == right && PartBasedBuilder.HoldsAnimatedItem(model, PartBasedBuilder.SlotRight));
    }

    /// <summary>
    /// Lays the stance's arm clips over the base clips in place: the overlay's bones take the overlay's keys, the
    /// rest of the body keeps the base clip's (walk_swordr holds a weapon's arm still over the walk, plpause1 holds
    /// a polearm upright over pause1). Both clips of each pair have the same length.
    /// </summary>
    static void ApplyOverlays(AnimationLibrary library, WeaponStances.Stance stance)
    {
        foreach (var (baseClip, overlays) in new[] { ("pause1", stance.Idle), ("walk", stance.Walk), ("run", stance.Run) })
            foreach (var overlay in overlays) Overlay(library, baseClip, overlay);
    }

    static void Overlay(AnimationLibrary library, string baseClip, string overlay)
    {
        if (!library.HasAnimation(baseClip) || !library.HasAnimation(overlay)) return;
        var top = library.GetAnimation(overlay);
        var composed = (Godot.Animation)library.GetAnimation(baseClip).Duplicate(true);
        static bool Transform(Godot.Animation a, int t) => a.TrackGetType(t) is Godot.Animation.TrackType.Position3D
            or Godot.Animation.TrackType.Rotation3D or Godot.Animation.TrackType.Scale3D;
        var owned = new HashSet<(string, Godot.Animation.TrackType)>();
        for (int t = 0; t < top.GetTrackCount(); t++)
            if (Transform(top, t)) owned.Add((top.TrackGetPath(t).ToString(), top.TrackGetType(t)));
        for (int t = composed.GetTrackCount() - 1; t >= 0; t--)
            if (Transform(composed, t) && owned.Contains((composed.TrackGetPath(t).ToString(), composed.TrackGetType(t)))) composed.RemoveTrack(t);
        for (int t = 0; t < top.GetTrackCount(); t++)
            if (Transform(top, t)) top.CopyTrack(t, composed);
        library.RemoveAnimation(baseClip);
        library.AddAnimation(baseClip, composed);
    }
}
