using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views.WorldFx.Logic;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>
/// Per-object visual overrides: SetObjectVisualTransform (scale, rotation, translation and
/// animation speed, lerped like the server), ReplaceObjectTexture, ReplaceObjectAnimation,
/// SetTextureOverride (this player sees one texture as another everywhere), material and
/// global shader uniforms, and the mouse-over highlight colour (SetObjectHiliteColor).
/// Overrides survive model rebuilds (appearance changes, respawns): they are re-applied
/// whenever an object's model nodes change.
/// </summary>
public partial class ObjectFx : Node
{
    public ClientSession Session = null!;
    /// <summary>Default mouse-over highlight (objects without SetObjectHiliteColor).</summary>
    public static readonly Color DefaultHilite = new(0.36f, 0.34f, 1f);
    /// <summary>Highlight of placeables, doors and items (measured: cyan).</summary>
    public static readonly Color ObjectHilite = new(0f, 1f, 0.97f);
    /// <summary>Highlight of creatures hostile to the player, and of friendly ones and party members.</summary>
    public static readonly Color HostileHilite = new(1f, 0.35f, 0.15f), FriendlyHilite = new(0.35f, 1f, 0.4f);

    sealed class State
    {
        public readonly VisualTransformSet Transform = new();
        /// <summary>Untransformed local transforms of the object's direct children, by instance id.</summary>
        public readonly Dictionary<ulong, Transform3D> Bases = new();
        public readonly Dictionary<string, string> Textures = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Animations = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Clips displaced by ReplaceObjectAnimation, to put back (null: the model had none).</summary>
        public readonly Dictionary<string, Godot.Animation?> Displaced = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<(string Material, string Param), (float[] Value, long? Exact)> Uniforms = new(Game.WorldObjects.MaterialUniformKeyComparer.Instance);
        public int Hilite = -1;
        public long Stamp;
        public bool ManualAnimation;
        public ulong Animator;
        public float AnimationSpeed = 1;
    }

    readonly Dictionary<uint, State> _states = new();
    /// <summary>SetTextureOverride for this player: old resref to new.</summary>
    readonly Dictionary<string, string> _global = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<uint, long> _globalStamps = new();
    /// <summary>Set when the texture overrides changed: every object is checked once, even with none left.</summary>
    bool _globalSweep;
    readonly Dictionary<(ulong Material, string Texture), Material> _swapped = new();
    double _clock;
    Interaction? _interaction;
    /// <summary>Objects wearing the highlight overlay, with the colour it was drawn in.</summary>
    readonly Dictionary<ObjectView, int> _hilited = new();

    State For(uint id) => _states.TryGetValue(id, out var s) ? s : _states[id] = new State();

    /// <summary>Current visual transform values of an object's base scope (capture checks).</summary>
    public VisualPose PoseOf(uint id) => _states.TryGetValue(id, out var s) ? s.Transform.Pose(VisualTransformSet.ScopeBase, _clock) : VisualPose.Identity;

    /// <summary>Length of an object's animation clip by name, or -1 (capture checks of ReplaceObjectAnimation).</summary>
    public float ClipLength(uint id, string clip) =>
        Session.Objects.GetValueOrDefault(id) is { } v && MixerOf(v) is { } m && m.HasAnimation(clip) ? (float)m.GetAnimation(clip).Length : -1;

    public void AreaChanged()
    {
        // Objects of the old area are gone; the server re-sends overrides of the new one.
        _states.Clear();
        _uniformCopies.Clear();
        _globalStamps.Clear();
        _hilited.Clear();
        // SetTextureOverride is per player, not per area: it applies to the new area too.
        RefreshArea();
    }

    /// <summary>Re-applies texture overrides to the area's tiles (after loading or rebuilding tiles).</summary>
    public void RefreshArea()
    {
        if (_global.Count > 0 && Session.Area is { } area)
        {
            SwapTextures(area, Lookup(null));
            ApplyModuleShaders(area, null, Lookup(null));
        }
    }

    public void Handle(string kind, uint subject, JsonElement d)
    {
        switch (kind)
        {
            case "visual-transform":
                For(subject).Transform.Set(J.I(d, "scope"), J.I(d, "transform"), J.F(d, "from"), J.F(d, "value"), J.I(d, "lerp"),
                    J.F(d, "duration"), J.I(d, "behavior"), J.I(d, "repeats"), _clock, J.F(d, "elapsed"));
                break;
            case "visual-transform-clear":
                if (_states.TryGetValue(subject, out var vs)) vs.Transform.Clear(J.I(d, "scope", -1));
                break;
            case "object-texture":
            {
                var s = For(subject);
                string old = J.S(d, "old"), replacement = J.S(d, "new");
                if (replacement.Length == 0) s.Textures.Remove(old);
                else s.Textures[old] = replacement;
                s.Stamp = 0;
                break;
            }
            case "object-animation":
            {
                var s = For(subject);
                string old = J.S(d, "old"), replacement = J.S(d, "new");
                if (replacement.Length == 0) s.Animations.Remove(old);
                else s.Animations[old] = replacement;
                s.Stamp = 0;
                break;
            }
            case "texture-override":
            {
                string old = J.S(d, "old"), replacement = J.S(d, "new");
                if (replacement.Length == 0) _global.Remove(old);
                else _global[old] = replacement;
                _globalStamps.Clear();
                _globalSweep = true;
                foreach (var s in _states.Values) s.Stamp = 0;
                if (Session.Area is { } area)
                {
                    SwapTextures(area, Lookup(null));
                    ApplyModuleShaders(area, null, Lookup(null));
                }
                break;
            }
            case "material-uniform":
                For(subject).Uniforms[(J.S(d, "material"), J.S(d, "param"))] = (J.Floats(d, "value"), J.Has(d, "int") ? J.L(d, "int") : null);
                For(subject).Stamp = 0;
                break;
            case "material-uniform-reset":
                if (_states.TryGetValue(subject, out var us))
                {
                    string material = J.S(d, "material"), param = J.S(d, "param");
                    foreach (var key in us.Uniforms.Keys.Where(k => (material.Length == 0 || k.Material.Equals(material, StringComparison.OrdinalIgnoreCase)) && (param.Length == 0 || k.Param == param)).ToList())
                        us.Uniforms.Remove(key);
                    us.Stamp = 0;
                }
                break;
            case "shader-uniform":
                Rendering.Shaders.ShaderGlobals.SetScriptUniform(J.S(d, "type", "vec4"), J.I(d, "shader", -1), J.Floats(d, "value"), J.Has(d, "int") ? J.L(d, "int") : null);
                break;
            case "hilite-color":
                For(subject).Hilite = J.I(d, "color", -1);
                foreach (var v in _hilited.Keys.Where(v => !IsInstanceValid(v) || v.ObjectId == subject).ToList()) _hilited.Remove(v);
                break;
        }
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        foreach (var (id, s) in _states)
        {
            if (Session.Objects.GetValueOrDefault(id) is not { } view || !IsInstanceValid(view)) continue;
            long stamp = Stamp(view);
            if (stamp != s.Stamp)
            {
                s.Stamp = stamp;
                SwapTextures(view, Lookup(s));
                ApplyModuleShaders(view, s, Lookup(s));
                ApplyAnimations(view, s);
            }
            if (!s.Transform.IsEmpty || s.Bases.Count > 0 || s.ManualAnimation || s.AnimationSpeed != 1) ApplyTransform(view, s, delta);
        }
        if (_global.Count > 0 || _globalSweep)
        {
            _globalSweep = false;
            foreach (var (id, view) in Session.Objects)
            {
                if (_states.ContainsKey(id)) continue;
                long stamp = Stamp(view);
                if (_globalStamps.GetValueOrDefault(id) == stamp) continue;
                _globalStamps[id] = stamp;
                SwapTextures(view, Lookup(null));
                ApplyModuleShaders(view, null, Lookup(null));
            }
        }
        UpdateHilite();
        Rendering.Shaders.ShaderGlobals.Update(Session, GetViewport()?.GetCamera3D());
    }

    /// <summary>Changes whenever the object's model nodes are replaced.</summary>
    static long Stamp(Node view)
    {
        long h = 17;
        foreach (var c in view.GetChildren()) h = h * 31 + (long)c.GetInstanceId();
        return h == 0 ? 1 : h;
    }

    // ---- visual transforms ----

    void ApplyTransform(ObjectView view, State s, double delta)
    {
        var pose = s.Transform.Pose(VisualTransformSet.ScopeBase, _clock);
        var xf = Rendering.Convert.T(pose.Matrix());
        bool identity = pose.IsIdentity;
        var seen = new HashSet<ulong>();
        foreach (var child in view.GetChildren())
        {
            if (child is not Node3D n) continue;
            ulong key = n.GetInstanceId();
            seen.Add(key);
            if (!s.Bases.TryGetValue(key, out var b))
            {
                if (identity) continue;
                s.Bases[key] = b = n.Transform;
            }
            n.Transform = identity ? b : xf * b;
        }
        foreach (var key in s.Bases.Keys.Where(k => !seen.Contains(k)).ToList()) s.Bases.Remove(key);
        if (identity) s.Bases.Clear();
        ApplyAnimationSpeed(view, s, pose.AnimationSpeed, delta);
    }

    static void ApplyAnimationSpeed(ObjectView view, State s, float speed, double delta)
    {
        speed = Math.Max(0, speed);
        if (view is CreatureView { Animator: { } tree })
        {
            // A rebuilt model (appearance change) comes with a fresh animator in its normal mode.
            if (tree.GetInstanceId() != s.Animator)
            {
                s.Animator = tree.GetInstanceId();
                s.ManualAnimation = false;
            }
            // AnimationTree has no speed scale: drive it by hand while the speed is changed.
            bool manual = Math.Abs(speed - 1) > 1e-3f;
            if (manual != s.ManualAnimation)
            {
                tree.CallbackModeProcess = manual ? AnimationMixer.AnimationCallbackModeProcess.Manual : AnimationMixer.AnimationCallbackModeProcess.Idle;
                s.ManualAnimation = manual;
            }
            if (manual) tree.Advance(delta * speed);
        }
        else if (view.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") is { } player) player.SpeedScale = speed;
        s.AnimationSpeed = speed;
    }

    // ---- textures ----

    /// <summary>Texture name (as used by a material) to its replacement for an object, or null.</summary>
    Func<string, string?> Lookup(State? s) => name =>
    {
        if (name.Length == 0) return null;
        string resref = name, colors = "";
        bool palette = name.StartsWith("plt:", StringComparison.OrdinalIgnoreCase);
        if (palette)
        {
            var parts = name.Split(':');
            if (parts.Length < 3) return null;
            resref = parts[1];
            colors = parts[2];
        }
        string? replacement = s != null && s.Textures.TryGetValue(resref, out var r) ? r : _global.TryGetValue(resref, out var g) ? g : null;
        if (replacement == null) return null;
        // A palette texture replaced by another palette texture keeps the object's colours.
        var content = Session.Content!;
        if (palette && content.Tinter.IsPalette(replacement))
            return content.Tinter.Tint(replacement, System.Convert.FromHexString(colors)) ?? replacement;
        if (content.Tinter.IsPalette(replacement))
            return content.Tinter.Tint(replacement, new byte[Rendering.PltTinter.Layers]) ?? replacement;
        return replacement;
    };

    /// <summary>Gives every surface whose texture is replaced a copy of its material with the new texture; restores the others.</summary>
    void SwapTextures(Node root, Func<string, string?> map)
    {
        var content = Session.Content;
        if (content == null) return;
        int swaps = 0;
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mi.Mesh == null) continue;
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
            {
                // Module-shader surfaces: ApplyModuleShaders.
                if (mi.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D original) continue;
                var name = original.AlbedoTexture?.ResourceName ?? "";
                var replacement = map(name);
                bool keyholed = Rendering.Keyhole.IsKeyholed(mi);
                if (replacement == null)
                {
                    if (mi.GetSurfaceOverrideMaterial(i) is { } o && o.HasMeta("openwinter_swap"))
                        mi.SetSurfaceOverrideMaterial(i, keyholed ? Rendering.Keyhole.Convert(original) : null);
                    continue;
                }
                var key = (original.GetInstanceId(), replacement);
                if (!_swapped.TryGetValue(key, out var swapped))
                {
                    var tex = content.Textures.Get(replacement);
                    var copy = (StandardMaterial3D)original.Duplicate();
                    copy.SetMeta("openwinter_swap", replacement);
                    if (tex != null)
                    {
                        copy.AlbedoTexture = tex.Texture;
                        bool flip = tex.FlipV;
                        copy.Uv1Scale = flip ? new Vector3(1, -1, 1) : Vector3.One;
                        copy.Uv1Offset = flip ? new Vector3(0, 1, 0) : Vector3.Zero;
                    }
                    _swapped[key] = swapped = copy;
                }
                var applied = keyholed && swapped is StandardMaterial3D std ? Rendering.Keyhole.Convert(std) ?? swapped : swapped;
                if (applied != swapped) applied.SetMeta("openwinter_swap", replacement);
                mi.SetSurfaceOverrideMaterial(i, applied);
                swaps++;
            }
        }
        if (WorldFxPlayer.Verbose && swaps > 0) GD.Print($"[objectfx] {root.Name}: {swaps} surfaces retextured");
    }

    // ---- animations ----

    static AnimationMixer? MixerOf(ObjectView view) =>
        view is CreatureView cv ? cv.Animator : view.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

    static void ApplyAnimations(ObjectView view, State s)
    {
        if (s.Animations.Count == 0 && s.Displaced.Count == 0) return;
        if (MixerOf(view) is not { } mixer || !mixer.HasAnimationLibrary("")) return;
        var lib = mixer.GetAnimationLibrary("");
        // A rebuilt model has fresh clips: forget what was displaced from the old one.
        if (s.Displaced.Count > 0 && !lib.HasMeta("openwinter_replaced")) s.Displaced.Clear();
        foreach (var old in s.Displaced.Keys.Where(k => !s.Animations.ContainsKey(k)).ToList())
        {
            if (lib.HasAnimation(old)) lib.RemoveAnimation(old);
            if (s.Displaced[old] is { } original) lib.AddAnimation(old, original);
            s.Displaced.Remove(old);
        }
        foreach (var (old, replacement) in s.Animations)
        {
            var source = s.Displaced.TryGetValue(replacement, out var displaced) && displaced != null ? displaced
                : lib.HasAnimation(replacement) ? lib.GetAnimation(replacement) : null;
            if (source == null) continue;
            if (!s.Displaced.ContainsKey(old)) s.Displaced[old] = lib.HasAnimation(old) ? lib.GetAnimation(old) : null;
            var clip = (Godot.Animation)source.Duplicate();
            if (s.Displaced[old] is { } before) clip.LoopMode = before.LoopMode;
            if (lib.HasAnimation(old)) lib.RemoveAnimation(old);
            lib.AddAnimation(old, clip);
        }
        lib.SetMeta("openwinter_replaced", true);
    }

    // ---- module shader surfaces ----

    /// <summary>The parameters an object's copy of a material overrides.</summary>
    const string UniformParams = "ow_uniform_params";
    /// <summary>Each object's copy of a material for SetMaterialShaderUniform*, by the material it was made from.</summary>
    readonly Dictionary<(ulong Material, uint Object), ShaderMaterial> _uniformCopies = new();

    /// <summary>
    /// Surfaces drawn with a module shader: retextured as the original does (ModuleShaders.Retextured, scene
    /// shadertex), then given the object's SetMaterialShaderUniform* values on its own copy (other objects with the
    /// same material are unchanged). Both are worked out from the surface's own material (Logic.SurfaceOverride), so
    /// a texture swap keeps the uniforms and a uniform reset keeps the swap.
    /// </summary>
    void ApplyModuleShaders(Node root, State? s, Func<string, string?> map)
    {
        if (Session.Content?.Materials.Shaders is not { } shaders) return;
        uint owner = root is ObjectView ov ? ov.ObjectId : 0;
        foreach (var mi in root.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mi.Mesh is not { } mesh) continue;
            for (int i = 0; i < mesh.GetSurfaceCount(); i++)
            {
                if (mesh.SurfaceGetMaterial(i) is not ShaderMaterial shared || !shared.HasMeta(Rendering.Shaders.ModuleShaders.BitmapMeta)) continue;
                var replacement = map(shared.GetMeta(Rendering.Shaders.ModuleShaders.BitmapMeta).AsString());
                var shown = Logic.SurfaceOverride.Resolve(shared, replacement, (m, r) => Retextured(shaders, m, r), m => Uniforms(m, s, owner));
                if (mi.GetSurfaceOverrideMaterial(i) != shown) mi.SetSurfaceOverrideMaterial(i, shown);
            }
        }
    }

    ShaderMaterial? Retextured(Rendering.Shaders.ModuleShaders shaders, ShaderMaterial material, string replacement)
    {
        var key = (material.GetInstanceId(), replacement);
        if (_swapped.TryGetValue(key, out var done)) return done as ShaderMaterial;
        var made = shaders.Retextured(material, replacement);
        if (made != null) _swapped[key] = made;
        return made;
    }

    /// <summary>The object's copy of a material with its SetMaterialShaderUniform* values, or null when it has none for it.</summary>
    ShaderMaterial? Uniforms(ShaderMaterial material, State? s, uint owner)
    {
        if (s == null || s.Uniforms.Count == 0 || material.Shader is not { } shader) return null;
        string name = material.GetMeta("ow_mtr").AsString();
        var types = Parameters(shader);
        // Scripts name parameters as the shader source does; a few are renamed in the translation.
        var mine = s.Uniforms.Select(u => (Name: Aurora.Shaders.ShaderTranslator.UniformName(u.Key.Param), u.Key.Material, u.Value))
            .Where(u => u.Material.Equals(name, StringComparison.OrdinalIgnoreCase) && types.ContainsKey(u.Name)).ToList();
        if (mine.Count == 0) return null;
        var key = (material.GetInstanceId(), owner);
        if (!_uniformCopies.TryGetValue(key, out var copy)) _uniformCopies[key] = copy = (ShaderMaterial)material.Duplicate();
        // Parameters no longer overridden go back to the material's own values.
        var before = copy.HasMeta(UniformParams) ? copy.GetMeta(UniformParams).AsStringArray() : [];
        foreach (var gone in before.Except(mine.Select(u => u.Name))) copy.SetShaderParameter(gone, material.GetShaderParameter(gone));
        copy.SetMeta(UniformParams, mine.Select(u => u.Name).ToArray());
        foreach (var (param, _, (value, exact)) in mine)
        {
            float V(int k) => k < value.Length ? value[k] : 0f;
            Variant v = types[param] switch
            {
                Variant.Type.Int => (int)(exact ?? (long)V(0)),
                Variant.Type.Bool => V(0) != 0,
                Variant.Type.Vector2 => new Vector2(V(0), V(1)),
                Variant.Type.Vector3 => new Vector3(V(0), V(1), V(2)),
                Variant.Type.Vector4 => new Vector4(V(0), V(1), V(2), V(3)),
                Variant.Type.Color => new Color(V(0), V(1), V(2), value.Length > 3 ? V(3) : 1f),
                _ => V(0),
            };
            copy.SetShaderParameter(param, v);
        }
        return copy;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Shader, Dictionary<string, Variant.Type>> ParameterTypes = new();

    /// <summary>A shader's parameters and their types.</summary>
    static Dictionary<string, Variant.Type> Parameters(Shader shader)
    {
        if (ParameterTypes.TryGetValue(shader, out var known)) return known;
        var types = new Dictionary<string, Variant.Type>(StringComparer.Ordinal);
        foreach (var u in shader.GetShaderUniformList())
        {
            var d = u.AsGodotDictionary();
            types[d["name"].AsString()] = (Variant.Type)d["type"].AsInt32();
        }
        ParameterTypes.AddOrUpdate(shader, types);
        return types;
    }

    // ---- mouse-over highlight ----

    /// <summary>Forces the highlight on an object (scripted captures); null follows the mouse again.</summary>
    public ObjectView? ForcedHilite;

    void UpdateHilite()
    {
        // The mouse picking node is a sibling of the WorldFxPlayer this node belongs to.
        _interaction ??= GetParent()?.GetParent()?.GetChildren().OfType<Interaction>().FirstOrDefault();
        var want = new HashSet<ObjectView>();
        // Transitions light their own outline instead (door planes, trigger fills).
        if ((ForcedHilite ?? (_interaction?.HoveredDoorTransition == true || _interaction?.Hovered is TriggerView ? null : _interaction?.Hovered)) is { } hovered)
            want.Add(hovered);
        // Holding the highlight key (Tab) lights up everything usable.
        if (_interaction != null) want.UnionWith(_interaction.HighlightedAll);
        want.RemoveWhere(v => !IsInstanceValid(v));
        if (want.Count == 0 && _hilited.Count == 0) return;
        foreach (var v in _hilited.Keys.Where(v => !want.Contains(v)).ToList())
        {
            if (IsInstanceValid(v)) Rendering.Hilite.Remove(v);
            _hilited.Remove(v);
        }
        foreach (var v in want)
        {
            // A script's SetObjectHiliteColor wins. Otherwise, as measured on the original: red for hostile
            // creatures, green for friendly ones (party members included), blue-violet for other creatures and
            // cyan for placeables, doors and items. The controlled character highlights white.
            int rgb = _states.TryGetValue(v.ObjectId, out var s) ? s.Hilite : -1;
            if (rgb < 0) rgb = v.ObjectId == Session.ControlledId ? 0xFFFFFF : _interaction?.Reaction(v) switch
            {
                Interaction.Hostile => -3,
                Interaction.Friendly => -2,
                _ => v.Kind == OpenWinter.Net.ObjectKind.Creature ? -1 : -4,
            };
            if (_hilited.TryGetValue(v, out var was) && was != rgb) Rendering.Hilite.Remove(v);
            _hilited[v] = rgb;
            // Repeated every frame: models rebuild (equipment, appearance) with fresh materials.
            Rendering.Hilite.Apply(v, rgb switch { >= 0 => J.Rgb(rgb), -3 => HostileHilite, -2 => FriendlyHilite, -4 => ObjectHilite, _ => DefaultHilite },
                flatLight: v.ObjectId == Session.ControlledId && rgb == 0xFFFFFF);
        }
    }
}
