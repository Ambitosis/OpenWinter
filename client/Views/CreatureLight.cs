using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// The light a creature gives off while it has an item with the Light property equipped (a torch: yellow, 20 m).
/// The original draws it as the matching VFX_DUR_LIGHT_{colour}_{metres} duration effect: visualeffects.2da →
/// progfx.2da (type 4, a light) → the fx_light_clr model, whose animation named in Param1 keys the light's colour
/// and radius (Yello_20m: colour 1.75, 1.5, 1.0, radius 13.3; the "5 m" to "20 m" steps differ in colour only).
/// The light hangs over the creature, not at the torch: fitting the lit floor and the shadow another creature casts
/// put it 3.7 m up and about 0.4 m in front (parity/visual, scene torchlight; tools/visual/torchprofile.py).
/// Only creatures cast shadows in it (fx_light_clr's light has the shadow flag), and those darken the floor to about
/// a fifth rather than blacking it out.
/// </summary>
public static class CreatureLight
{
    public const string NodeName = "CreatureLight";

    /// <summary>Render layer of creature meshes, the only shadow casters of creature lights.</summary>
    public const uint CasterLayer = 1u << 18;

    /// <summary>
    /// Height above the creature's feet, range (times the model's radius), Godot falloff exponent and strength, fitted to
    /// the original's floor from 2 to 14 m within 7% (scene torchlight). Godot's falloff ends at the range, so it
    /// reaches well past the model's 13.3 m to keep the original's long dim tail.
    /// </summary>
    public static float Height = 3.7f, RangeScale = 2.37f, Attenuation = 2.15f, Energy = 5.9f;

    /// <summary>How far in front of the creature the light sits.</summary>
    public static float Forward = 0.4f;

    /// <summary>Shadow strength: the original's shadows in a torch's light leave about a fifth of the light.</summary>
    public static float ShadowOpacity = 0.8f;

    public readonly record struct Spec(Color Color, float Radius, bool Shadow);

    /// <summary>The light of the brightest light-giving equipped item, if any.</summary>
    public static Spec? For(Appearance a, ClientContent content)
    {
        Spec? best = null;
        foreach (var item in a.Equipment)
        {
            if (item.LightColor == ItemVisual.NoLight) continue;
            if (FromProperty(item.LightColor, item.LightLevel, content) is not { } spec) continue;
            if (best is not { } b || Brightness(spec.Color) > Brightness(b.Color)) best = spec;
        }
        return best;
    }

    static float Brightness(Color c) => c.R + c.G + c.B;

    /// <summary>
    /// A Light property's colour (iprp_color.2da row) and brightness (iprp_lightcost.2da row, "Type:_Bright_(20m)")
    /// to the VFX_DUR_LIGHT_* row that draws it.
    /// </summary>
    public static Spec? FromProperty(int color, int level, ClientContent content)
    {
        var res = content.Resources;
        string colour = res.TwoDA("iprp_color").Get(color, "Label").ToUpperInvariant();
        string cost = res.TwoDA("iprp_lightcost").Get(level, "Label");
        int open = cost.IndexOf('('), m = cost.IndexOf("m)", StringComparison.Ordinal);
        if (colour.Length == 0 || open < 0 || m <= open) return null;
        string label = $"VFX_DUR_LIGHT_{colour}_{cost[(open + 1)..m]}";
        var vfx = res.TwoDA("visualeffects");
        for (int row = 0; row < vfx.RowCount; row++)
            if (string.Equals(vfx.Get(row, "Label"), label, StringComparison.OrdinalIgnoreCase))
                return FromProgFx(vfx.GetInt(row, "ProgFX_Duration", -1), content);
        return null;
    }

    /// <summary>A progfx.2da light (type 4): Param7's model played at Param1's animation, read at its end.</summary>
    public static Spec? FromProgFx(int prog, ClientContent content)
    {
        if (prog < 0) return null;
        var progfx = content.Resources.TwoDA("progfx");
        if (progfx.GetInt(prog, "Type", -1) != 4) return null;
        if (content.Models.Get(progfx.Get(prog, "Param7")) is not { } model) return null;
        if (!model.Animations.TryGetValue(progfx.Get(prog, "Param1"), out var anim)) return null;
        foreach (var (node, track) in anim.Tracks)
        {
            if (track.LightColors.Length == 0) continue;
            int part = model.FindPart(node);
            var light = part >= 0 ? model.Parts[part].Light : null;
            var c = track.LightColors[^1];
            float radius = track.LightRadii.Length > 0 ? track.LightRadii[^1] : light?.Radius ?? 10f;
            return new Spec(new Color(c.X, c.Y, c.Z), radius, light?.Shadow ?? false);
        }
        return null;
    }

    /// <summary>Puts the light on a creature view (replacing any it had), or takes it away.</summary>
    public static void Apply(Node3D view, Spec? spec)
    {
        if (view.GetNodeOrNull(NodeName) is { } old) { view.RemoveChild(old); old.QueueFree(); }
        if (spec is not { } s) return;
        view.AddChild(new OmniLight3D
        {
            Name = NodeName,
            LightColor = s.Color,
            LightEnergy = Energy,
            OmniRange = s.Radius * RangeScale,
            OmniAttenuation = Attenuation,
            ShadowEnabled = s.Shadow && Preferences.GraphicsApply.ShadowsOn,
            ShadowCasterMask = CasterLayer,
            ShadowOpacity = ShadowOpacity,
            // The view lives in area (Z-up) space; models face +Y.
            Position = new Vector3(0, Forward, Height),
        });
    }
}
