using OpenWinter.Content;

namespace OpenWinter.Client.Views;

public partial class CreatureView
{
    readonly Dictionary<string, float> _hitEvents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The time of a clip's first "hit" event (the blow landing), read from the model.</summary>
    void ReadHitEvent(string clip, ModelAnimation source)
    {
        foreach (var e in source.Events)
            if (e.Name.Equals("hit", StringComparison.OrdinalIgnoreCase)
                && (!_hitEvents.TryGetValue(clip, out var t) || e.Time < t))
                _hitEvents[clip] = e.Time;
    }

    /// <summary>
    /// When the swing <paramref name="animation"/> lands: the "hit" event of the clip this model plays for it
    /// (the humanoid swings and the creature set's ca1slashl ... ccloseh alike have it 0.5 s in; a wolf's cclosel
    /// 0.467 s), or <paramref name="fallback"/> when the model has no such clip or event.
    /// </summary>
    public float HitTime(string animation, float fallback)
    {
        if (Animator == null || animation.Length == 0) return fallback;
        return _hitEvents.TryGetValue(Animator.Resolve(animation), out var t) ? t : fallback;
    }
}
