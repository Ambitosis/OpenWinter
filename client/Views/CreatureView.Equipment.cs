using OpenWinter.Content;
using OpenWinter.Net;

namespace OpenWinter.Client.Views;

public partial class CreatureView
{
    bool _equipmentDrawPending;
    ulong _equipmentDrawVersion;
    readonly Dictionary<string, double> _equipmentDrawEvents = new(StringComparer.OrdinalIgnoreCase);
    double _equipmentDrawAt;
    (Appearance Appearance, ClientContent Content)? _pendingEquipment;

    /// <summary>The server changes equipment immediately; the authored draw event changes its visible model.</summary>
    public void BeginEquipmentDraw(string clip)
    {
        OnEquipmentDraw();
        if (Animator == null || !Animator.HasClip(clip) || (Flags & ObjectFlags.Dead) != 0) return;
        Animator.PlayAction(clip);
        _equipmentDrawVersion = Animator.ActionVersion;
        _equipmentDrawAt = _equipmentDrawEvents.GetValueOrDefault(clip, double.PositiveInfinity);
        _equipmentDrawPending = true;
    }

    void ReadEquipmentDrawEvent(string clip, ModelAnimation source)
    {
        var times = source.Events.Where(e => e.Name.Equals("draw_weapon", StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Time).Distinct().Order().ToArray();
        if (times.Length == 0) return;
        _equipmentDrawEvents[clip] = times[0];
    }

    public void OnEquipmentDraw()
    {
        if (!_equipmentDrawPending) return;
        _equipmentDrawPending = false;
        var pending = _pendingEquipment;
        _pendingEquipment = null;
        if (pending is { } p) Rebuild(p.Appearance, p.Content);
    }

    void UpdateEquipmentDraw()
    {
        // Interrupted actions and models without the authored event must still reach the authoritative appearance.
        if (!_equipmentDrawPending) return;
        var action = Animator?.CaptureAction();
        if (Animator == null || Animator.ActionVersion != _equipmentDrawVersion || action == null
            || action.Position >= _equipmentDrawAt) OnEquipmentDraw();
    }
}
