using System.Text.Json;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// Stunned creatures: the original keeps a few blue stars circling the head for as long as the condition lasts,
/// with no visual effect applied by the script (parity/visual scene conditions, rows a and b and the player's stun).
/// That is VFX_DUR_MIND_AFFECTING_DISABLED's model (visualeffects.2da row 208, vdr_mindhit03), played here as a
/// lasting visual while the server's Stunned flag is set.
/// </summary>
public partial class EffectPlayer
{
    const int VfxDurMindAffectingDisabled = 208;
    readonly Dictionary<uint, long> _stunVisuals = new();
    long _nextConditionVisual = long.MinValue + 1000;

    /// <summary>EffectLook key for the player's own translucency.</summary>
    const long SelfTranslucentLook = long.MinValue + 103;
    uint _translucentSelf;
    CanvasLayer? _blindLayer;

    /// <summary>
    /// What the player sees of her own conditions (parity/visual scene conditions, p13, p19-p22): invisible, in
    /// sanctuary or ethereal she is half transparent (other creatures with them are not); blind or in darkness the
    /// world goes nearly black (about a ninth of its brightness).
    /// </summary>
    void UpdateSelfConditions()
    {
        var me = Session.Controlled;
        uint translucent = me != null && (me.Flags & ObjectFlags.Translucent) != 0 ? me.ObjectId : 0;
        if (translucent != _translucentSelf)
        {
            if (_translucentSelf != 0 && Session.Objects.TryGetValue(_translucentSelf, out var old)) old.SetEffectLook(SelfTranslucentLook, null);
            if (translucent != 0) me!.SetEffectLook(SelfTranslucentLook, new ObjectView.EffectLook { Alpha = 0.5f });
            _translucentSelf = translucent;
        }
        bool blind = me != null && (me.Flags & (ObjectFlags.Blinded | ObjectFlags.Dead)) == ObjectFlags.Blinded;
        if (blind && _blindLayer == null)
        {
            // Under every interface layer, over the 3D view.
            _blindLayer = new CanvasLayer { Name = "BlindView", Layer = -50 };
            var shade = new ColorRect { Color = new Color(0, 0, 0, 0.88f), MouseFilter = Control.MouseFilterEnum.Ignore };
            shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _blindLayer.AddChild(shade);
            AddChild(_blindLayer);
        }
        if (_blindLayer != null) _blindLayer.Visible = blind;
    }

    public override void _Process(double delta)
    {
        if (Session.Content == null) return;
        UpdateSelfConditions();
        foreach (var (id, effectId) in _stunVisuals.ToList())
            if (!Session.Objects.TryGetValue(id, out var o) || (o.Flags & (ObjectFlags.Stunned | ObjectFlags.Dead)) != ObjectFlags.Stunned)
            {
                _stunVisuals.Remove(id);
                if (_byEffect.Remove(effectId, out var nodes)) foreach (var n in nodes) End(n);
            }
        foreach (var (id, view) in Session.Objects)
        {
            if (view is not CreatureView || (view.Flags & (ObjectFlags.Stunned | ObjectFlags.Dead)) != ObjectFlags.Stunned || _stunVisuals.ContainsKey(id)) continue;
            long effectId = ++_nextConditionVisual;
            _stunVisuals[id] = effectId;
            using var doc = JsonDocument.Parse($"{{\"vfx\":{VfxDurMindAffectingDisabled},\"creator\":0,\"miss\":false,\"scale\":1,\"translate\":[0,0,0],\"rotate\":[0,0,0],\"duration\":-1,\"id\":{effectId}}}");
            Vfx(id, doc.RootElement);
        }
    }
}
