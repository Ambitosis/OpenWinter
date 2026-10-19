using OpenWinter.Net;
using Godot;
using N = System.Numerics;

namespace OpenWinter.Client.Views;

/// <summary>Client-side representation of a server object. Lives in Aurora (Z-up) space.</summary>
public partial class ObjectView : Node3D
{
    public uint ObjectId;
    public ObjectKind Kind;
    public string DisplayName = "";
    public ObjectFlags Flags;
    /// <summary>Portrait texture base name (creatures; add the size letter), "" when none.</summary>
    public string Portrait = "";
    /// <summary>The object's tag, as the server sent it.</summary>
    public string Tag = "";
    /// <summary>Where and how the server last placed the object (the drawn model moves towards it).</summary>
    public Vector3 NetPosition => TargetPosition;
    public float NetFacing => TargetFacing;

    protected Vector3 TargetPosition;
    protected float TargetFacing;
    protected float NetSpeed;
    protected MovementMode NetMode;
    protected bool NetCombat;

    public virtual void Init(SpawnMessage spawn)
    {
        ObjectId = spawn.ObjectId;
        Kind = spawn.Kind;
        DisplayName = UI.Kit.NwnText.Strip(spawn.Name);
        Flags = spawn.Flags;
        Portrait = spawn.Portrait;
        Tag = spawn.Tag;
        Name = $"{spawn.Kind}_{spawn.ObjectId:X}_{spawn.Tag}";
        Position = TargetPosition = Rendering.Convert.V(spawn.Position);
        TargetFacing = spawn.Facing;
        Rotation = new Vector3(0, 0, FacingToModel(spawn.Facing));
    }

    /// <summary>Models face +Y; facing is measured from +X.</summary>
    protected virtual float FacingToModel(float facing) => facing - MathF.PI / 2f;

    public virtual void ApplyUpdate(UpdateMessage u)
    {
        TargetPosition = Rendering.Convert.V(u.Position);
        TargetFacing = u.Facing;
        NetSpeed = u.Speed;
        NetMode = u.Mode;
        NetCombat = u.InCombat;
        Flags = u.Flags == 0 ? Flags : u.Flags;
    }

    public virtual void PlayAnimation(PlayAnimationMessage m) { }

    // ----- Programmed duration effects (progfx.2da): "Alpha" (type 5: translucency, glows, cutscene
    // invisibility) and "Skin" (type 1: stoneskin, barkskin and the like replace the model's textures) -----

    /// <summary>How a duration effect changes the model's look.</summary>
    public sealed class EffectLook
    {
        /// <summary>Opacity (1 = opaque); the lowest of all effects wins.</summary>
        public float Alpha = 1f;
        /// <summary>A glow colour the model is tinted with (null = none).</summary>
        public Color? Tint;
        /// <summary>Texture lighting and silhouette glow (progfx type 3).</summary>
        public Color? Glow;
        /// <summary>Type 6 alternates linearly to this color; Param7 is milliseconds per half-cycle.</summary>
        public Color? GlowPulseTo;
        public float GlowHalfPeriod;
        internal double GlowAge;
        public Color? CurrentGlow => Glow is { } first && GlowPulseTo is { } second && GlowHalfPeriod > 0
            ? first.Lerp(second, (float)(1 - Math.Abs(1 - GlowAge / GlowHalfPeriod % 2))) : Glow;
        /// <summary>Pulsing: seconds per cycle (0 = steady) and the opacity it pulses to.</summary>
        public float PulsePeriod, PulseAlpha;
        /// <summary>Texture replacing every texture of the model (null = none).</summary>
        public Texture2D? Skin;
        public bool SkinFlipV;
        /// <summary>Holds the creature animation pose (progfx type 13).</summary>
        public bool FreezeAnimations;
    }

    readonly Dictionary<long, EffectLook> _effectLooks = new();
    Material? _lookOverlay, _lookSkin;
    double _pulseClock;

    /// <summary>Sets (or with null clears) the opacity a duration effect gives the object's model; the lowest wins.</summary>
    public void SetEffectAlpha(long effectId, float? alpha) => SetEffectLook(effectId, alpha is { } a ? new EffectLook { Alpha = a } : null);

    /// <summary>Sets (or with null clears) the look a duration effect gives the object's model.</summary>
    public void SetEffectLook(long effectId, EffectLook? look)
    {
        if (look != null) { look.Alpha = Math.Clamp(look.Alpha, 0, 1); _effectLooks[effectId] = look; }
        else if (!_effectLooks.Remove(effectId)) return;
        RebuildLookMaterials();
        ApplyEffectAlpha();
    }

    /// <summary>Opacity of the model from effects (1 = opaque).</summary>
    public float EffectAlpha
    {
        get
        {
            if (_effectLooks.Count == 0) return 1f;
            float alpha = 1f;
            foreach (var l in _effectLooks.Values)
            {
                float a = l.Alpha;
                // Pulsing effects swing between their opacity and the pulse opacity.
                if (l.PulsePeriod > 0) a = Mathf.Lerp(l.Alpha, l.PulseAlpha, 0.5f - 0.5f * MathF.Cos((float)(_pulseClock / l.PulsePeriod) * MathF.Tau));
                alpha = Math.Min(alpha, a);
            }
            return alpha;
        }
    }

    bool _stealth, _stealthHidden, _disappeared, _unexplored;
    float _cameraAlpha = 1;
    float _decayAlpha = 1;
    bool? _perceived;
    float _perceptionAlpha = 1;
    /// <summary>Presentation opacity combines independent sources without one reset erasing another.</summary>
    public float PresentationAlpha => Math.Min(Math.Min(EffectAlpha, _decayAlpha), Math.Min(_cameraAlpha, Math.Min(_stealth ? StealthAlpha(_stealthClock) : 1, _perceptionAlpha)));
    double _stealthClock;

    /// <summary>
    /// The player's own character in stealth pulses on a 4 s cycle from entering it (nwmain bursts, scene stealthself):
    /// opaque for its first half second, fading to about 0.45 by 0.9 s, faint until 2.5 s, opaque again by 3 s.
    /// </summary>
    public static float StealthAlpha(double seconds)
    {
        const float Low = 0.45f;
        float p = (float)(seconds % 4.0);
        if (p < 0.5f || p >= 3.0f) return 1;
        if (p < 0.9f) return Mathf.Lerp(1, Low, (p - 0.5f) / 0.4f);
        if (p < 2.5f) return Low;
        return Mathf.Lerp(Low, 1, (p - 2.5f) / 0.5f);
    }
    /// <summary>Discovery is independent of effect, stealth and perception updates.</summary>
    // unexploredkinds, unexploreditems: a placeable, door or item on the ground the player has been shown stays
    // drawn when its tile is unexplored again, but a static placeable follows its tile like the tile's own scenery.
    // Creatures (the player's own included) follow the current explored state, except one only heard, which the
    // original still draws as its faint shape.
    bool _discovered;
    public void SetUnexplored(bool unexplored)
    {
        if (Kind is ObjectKind.Placeable or ObjectKind.Door or ObjectKind.Item && (Flags & ObjectFlags.Static) == 0)
        {
            if (!unexplored) _discovered = true;
            else if (_discovered) unexplored = false;
        }
        else if (Kind == ObjectKind.Creature && _perceived == true && _perceptionAlpha < 1) unexplored = false;
        if (_unexplored == unexplored) return;
        _unexplored = unexplored; ApplyEffectAlpha();
    }
    public void SetDecayAlpha(float alpha) { _decayAlpha = alpha; ApplyEffectAlpha(); }
    protected void SetCameraAlpha(float alpha)
    {
        if (_cameraAlpha == alpha) return;
        _cameraAlpha = alpha; ApplyEffectAlpha();
    }
    /// <summary>Appear/disappear is independent of alpha, stealth, discovery and perception.</summary>
    public void SetDisappeared(bool disappeared) { _disappeared = disappeared; ApplyEffectAlpha(); }
    public void SetStealth(bool enabled)
    {
        if (enabled && !_stealth) _stealthClock = 0;
        _stealth = enabled; ApplyEffectAlpha();
    }
    public void SetStealthVisibility(bool hidden) { _stealthHidden = hidden; ApplyEffectAlpha(); }
    public void SetPerception(bool? seen, bool heard = false)
    {
        _perceived = seen == null ? null : seen.Value || heard;
        _perceptionAlpha = seen == false && heard ? 0.5f : 1;
        ApplyEffectAlpha();
    }
    protected bool HasLook => _effectLooks.Count > 0 || _disappeared || _stealth || _stealthHidden || _perceived != null || _unexplored || _cameraAlpha != 1 || _decayAlpha != 1;

    void RebuildLookMaterials()
    {
        _lookOverlay = null;
        _lookSkin = null;
        // The most recent tint and skin win.
        if (_effectLooks.Values.LastOrDefault(l => l.Tint != null)?.Tint is { } tint)
            // A glow: the model shows through in the effect's colour, added on top (the original's glow effects).
            _lookOverlay = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(tint.R, tint.G, tint.B, 0.6f),
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
            };
        if (_effectLooks.Values.LastOrDefault(l => l.Skin != null) is { Skin: { } skin } withSkin)
        {
            var m = new StandardMaterial3D { AlbedoTexture = skin, DiffuseMode = BaseMaterial3D.DiffuseModeEnum.Lambert, SpecularMode = BaseMaterial3D.SpecularModeEnum.Disabled };
            if (withSkin.SkinFlipV) { m.Uv1Scale = new Vector3(1, -1, 1); m.Uv1Offset = new Vector3(0, 1, 0); }
            _lookSkin = m;
        }
    }

    /// <summary>Applies the effect looks to the model (call again after rebuilding it). Attached effect models (fx_*) keep theirs.</summary>
    protected void ApplyEffectAlpha()
    {
        float alpha = PresentationAlpha;
        Visible = !_unexplored && !_disappeared && _perceived != false && !_stealthHidden;
        if (this is CreatureView creature)
            creature.Animator?.SetEffectFrozen(_effectLooks.Values.Any(l => l.FreezeAnimations));
        void Walk(Node n)
        {
            foreach (var child in n.GetChildren())
            {
                if (child.Name.ToString().StartsWith("fx_")) continue;
                if (child is GeometryInstance3D g)
                {
                    g.Transparency = 1 - alpha;
                    // Glow/depth twins keep their mask shaders; a skin or tint belongs
                    // to the visible mesh. They still follow its effect opacity.
                    if (g is MeshInstance3D twin && (twin.Layers == Rendering.Hilite.MaskLayer || twin.Layers == Rendering.Hilite.DepthLayer))
                        continue;
                    // Fully invisible models cast no shadow either.
                    if (alpha <= 0.01f && !g.HasMeta("openwinter_shadow")) { g.SetMeta("openwinter_shadow", (int)g.CastShadow); g.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; }
                    else if (alpha > 0.01f && g.HasMeta("openwinter_shadow")) { g.CastShadow = (GeometryInstance3D.ShadowCastingSetting)(int)g.GetMeta("openwinter_shadow"); g.RemoveMeta("openwinter_shadow"); }
                    // Only what an effect set is replaced or cleared (highlights and placeholders own theirs).
                    if (_lookOverlay != null) { g.MaterialOverlay = _lookOverlay; g.SetMeta("openwinter_look_overlay", true); }
                    else if (g.HasMeta("openwinter_look_overlay")) { g.MaterialOverlay = null; g.RemoveMeta("openwinter_look_overlay"); }
                    if (_lookSkin != null) { g.MaterialOverride = _lookSkin; g.SetMeta("openwinter_look_skin", true); }
                    else if (g.HasMeta("openwinter_look_skin")) { g.MaterialOverride = null; g.RemoveMeta("openwinter_look_skin"); }
                }
                Walk(child);
            }
        }
        Walk(this);
        ApplyEffectGlow();
    }

    void ApplyEffectGlow()
    {
        var look = _effectLooks.Values.LastOrDefault(l => l.Glow != null);
        Rendering.Hilite.SetEffectGlow(this, look?.CurrentGlow, look is { GlowPulseTo: not null, GlowHalfPeriod: > 0 });
    }

    public override void _Process(double delta)
    {
        foreach (var look in _effectLooks.Values)
            if (look.GlowPulseTo != null && look.GlowHalfPeriod > 0) look.GlowAge += delta;
        bool pulsing = HasLook && _effectLooks.Values.Any(l => l.PulsePeriod > 0);
        if (pulsing) _pulseClock += delta;
        if (_stealth) _stealthClock += delta;
        if (pulsing || _stealth) ApplyEffectAlpha();
        float t = Math.Clamp((float)delta * 15f, 0, 1);
        UpdatePosition((float)delta);
        float current = Rotation.Z;
        float wanted = FacingToModel(TargetFacing);
        Rotation = new Vector3(0, 0, this is CreatureView && (Flags & ObjectFlags.Talking) == 0
            // moveturn: native 180-degree visible turns take about half a second.
            // This catch-up rate is separate from the authoritative held A/D steering rate.
            ? current + Math.Clamp(Mathf.AngleDifference(current, wanted), -MathF.Tau * (float)delta, MathF.Tau * (float)delta)
            : Mathf.LerpAngle(current, wanted, t));
        // Keep glow twins in sync with animated mesh transforms and rebuilt models.
        if (_effectLooks.Values.Any(l => l.Glow != null)) ApplyEffectGlow();
    }

    protected virtual void UpdatePosition(float delta) => Position = Position.Lerp(TargetPosition, Math.Clamp(delta * 15f, 0, 1));
}
