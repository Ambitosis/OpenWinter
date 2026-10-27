using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Rendering;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Content;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// Plays server engine events on the client: visual effects (visualeffects.2da),
/// persistent areas of effect (vfx_persistent.2da), attack swings with hit and
/// miss reactions and damage numbers, and appear/disappear.
/// </summary>
public partial class EffectPlayer : Node
{
    public ClientSession Session = null!;
    readonly Dictionary<long, List<Node3D>> _byEffect = new();
    readonly Dictionary<uint, List<Node3D>> _aoes = new();
    /// <summary>Where each area of effect was placed, so a move shifts it.</summary>
    readonly Dictionary<uint, Vector3> _aoeAt = new();
    readonly Dictionary<long, Action> _cessations = new();
    readonly Dictionary<uint, long> _appearanceChanges = new();
    long _appearanceSerial;
    /// <summary>Duration effects that change an object's opacity: effect id to object.</summary>
    readonly Dictionary<long, uint> _alphaEffects = new();

    /// <summary>Effects drawn now, by the server's effect id (the multiplayer trace compares them with what it was told).</summary>
    public IEnumerable<long> ActiveEffectIds => _byEffect.Keys;

    public override void _Ready() => Session.AreaLoading += OnLoading;
    void OnLoading(LoadAreaMessage _) => Clear();

    public override void _ExitTree() => Session.AreaLoading -= OnLoading;

    void Clear()
    {
        ClearAudio();
        foreach (var n in _byEffect.Values.Concat(_aoes.Values).SelectMany(nodes => nodes))
            if (IsInstanceValid(n)) n.QueueFree();
        _byEffect.Clear();
        _stunVisuals.Clear();
        _aoes.Clear();
        _aoeAt.Clear();
        _alphaEffects.Clear();
        _cessations.Clear();
        _appearanceChanges.Clear();
    }

    public void Handle(NotifyMessage m)
    {
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        switch (m.Kind)
        {
            case "vfx": Vfx(m.Subject, d); break;
            case "effect-removed":
                if (d.TryGetProperty("id", out var id) && _byEffect.Remove(id.GetInt64(), out var nodes))
                    foreach (var n in nodes) End(n);
                if (id.ValueKind == JsonValueKind.Number && _cessations.Remove(id.GetInt64(), out var finish)) finish();
                if (id.ValueKind == JsonValueKind.Number && _alphaEffects.Remove(id.GetInt64(), out var faded) && Session.Objects.TryGetValue(faded, out var view))
                    view.SetEffectAlpha(id.GetInt64(), null);
                break;
            case "attack": Attack(d); break;
            case "potion-ready":
                if (Session.Objects.GetValueOrDefault(m.Subject) is CreatureView drinker)
                    drinker.SetPotionReady(d.GetProperty("active").GetBoolean());
                break;
            case "spell-ready":
                if (Session.Objects.GetValueOrDefault(m.Subject) is CreatureView caster)
                    caster.SetSpellReady(d.GetProperty("active").GetBoolean());
                break;
            case "object-use":
                if (Session.Objects.TryGetValue(m.Subject, out var user))
                {
                    string useClip = d.GetProperty("animation").GetString() ?? "";
                    float useSeconds = d.TryGetProperty("duration", out var duration) ? duration.GetSingle() : 0;
                    // The low reach of item put-downs and pick-ups continues through consecutive ones (GroundReachNotice).
                    if (useClip == "getlow" && user is CreatureView { Animator: { } reacher })
                    {
                        if (d.TryGetProperty("cancelled", out var cancelled) && cancelled.ValueKind == JsonValueKind.True) reacher.EndReach();
                        else reacher.PlayReach(useSeconds, d.TryGetProperty("chain", out var chain) && chain.ValueKind == JsonValueKind.True);
                    }
                    else
                        user.PlayAnimation(new PlayAnimationMessage { ObjectId = m.Subject, Animation = useClip, Duration = useSeconds });
                }
                break;
            case "equipment-draw":
                if (Session.Objects.GetValueOrDefault(m.Subject) is CreatureView equipper)
                    equipper.BeginEquipmentDraw(d.GetProperty("animation").GetString() ?? "drwright");
                break;
            case "rest":
                if (d.GetProperty("state").GetString() == "started"
                    && Session.Objects.GetValueOrDefault(m.Subject) is CreatureView rester)
                    rester.Animator?.HoldSittingUntilStopped();
                break;
            case "aoe-created": AoeCreated(d); break;
            case "aoe-removed":
                _aoeAt.Remove(d.GetProperty("id").GetUInt32());
                if (_aoes.Remove(d.GetProperty("id").GetUInt32(), out var aoe))
                    foreach (var n in aoe) End(n);
                break;
            case "aoe-moved":
                // An area of effect that follows its creature: its models move with it.
                if (_aoes.TryGetValue(d.GetProperty("id").GetUInt32(), out var moving) && _aoeAt.TryGetValue(d.GetProperty("id").GetUInt32(), out var from)
                    && d.TryGetProperty("pos", out var to))
                {
                    var now = new Vector3(to[0].GetSingle(), to[1].GetSingle(), to[2].GetSingle());
                    foreach (var n in moving) if (IsInstanceValid(n)) n.Position += now - from;
                    _aoeAt[d.GetProperty("id").GetUInt32()] = now;
                }
                break;
            case "disappear":
                Appearance(m.Subject, d, appearing: false);
                break;
            case "appear":
                Appearance(m.Subject, d, appearing: true);
                break;
        }
    }

    void Appearance(uint subject, JsonElement d, bool appearing)
    {
        if (Session.Objects.GetValueOrDefault(subject) is not { } view) return;
        long serial = ++_appearanceSerial;
        _appearanceChanges.Remove(subject);
        bool animate = d.TryGetProperty("animation", out var animation) && animation.GetInt32() != 0;
        string name = appearing ? "appear" : "disappear";
        var animator = (view as CreatureView)?.Animator;
        string? clip = animate && animator != null ? new[] { name, "c" + name }.FirstOrDefault(animator.HasClip) : null;
        if (appearing || clip == null) view.SetDisappeared(!appearing);
        if (clip == null) return;
        view.PlayAnimation(new PlayAnimationMessage { ObjectId = subject, Animation = clip });
        if (!appearing)
        {
            _appearanceChanges[subject] = serial;
            GetTree().CreateTimer(animator!.GetAnimation(clip).Length).Timeout += () =>
            {
                if (_appearanceChanges.GetValueOrDefault(subject) != serial) return;
                _appearanceChanges.Remove(subject);
                if (IsInstanceValid(view)) view.SetDisappeared(true);
            };
        }
    }

    void Vfx(uint subject, JsonElement d)
    {
        var content = Session.Content;
        if (content == null) return;
        int row = d.GetProperty("vfx").GetInt32();
        var table = content.Resources.TwoDA("visualeffects");
        bool duration = table.Get(row, "Type_FD") == "D" || d.TryGetProperty("duration", out var dur) && dur.GetSingle() != 0;
        long effectId = d.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0;
        var created = new List<Node3D>();

        ObjectView? target = Session.Objects.GetValueOrDefault(subject);
        Vector3? point = d.TryGetProperty("pos", out var pos)
            ? new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle()) : null;
        // Programmed duration effects (progfx.2da) change the target's look. "Alpha" (type 5): opacity (Param1),
        // a glow colour (Param2-4, -1 = none) and pulsing (Param5 ms per cycle, to the opacity in Param6):
        // VFX_DUR_CUTSCENE_INVISIBILITY, ghosts, the VFX_DUR_GLOW_* colours. "Skin" (type 1): the texture in
        // Param1 replaces the model's (stoneskin).
        int prog = table.GetInt(row, "ProgFX_Duration", -1);
        if (WorldFx.WorldFxPlayer.Verbose) GD.Print($"[vfx] {table.Get(row, "Label")} on {target?.DisplayName ?? "location"} (progfx {prog}, duration {duration})");
        if (duration && effectId != 0 && target != null && prog >= 0 && content.Resources.TwoDA("progfx") is var progfx)
        {
            int type = progfx.GetInt(prog, "Type", -1);
            if (type == 5)
            {
                float r = progfx.GetFloat(prog, "Param2", -1f);
                target.SetEffectLook(effectId, new ObjectView.EffectLook
                {
                    Alpha = progfx.GetFloat(prog, "Param1", 1f),
                    Tint = r >= 0 ? new Color(r, progfx.GetFloat(prog, "Param3", 0f), progfx.GetFloat(prog, "Param4", 0f)) : null,
                    PulsePeriod = progfx.GetFloat(prog, "Param5", 0f) / 1000f,
                    PulseAlpha = progfx.GetFloat(prog, "Param6", 1f),
                });
                _alphaEffects[effectId] = subject;
            }
            else if (type is 3 or 6)
            {
                target.SetEffectLook(effectId, new ObjectView.EffectLook
                {
                    Glow = new Color(progfx.GetFloat(prog, "Param1", 0f),
                        progfx.GetFloat(prog, "Param2", 0f), progfx.GetFloat(prog, "Param3", 0f)),
                    GlowPulseTo = type == 6 ? new Color(progfx.GetFloat(prog, "Param4", 0f),
                        progfx.GetFloat(prog, "Param5", 0f), progfx.GetFloat(prog, "Param6", 0f)) : null,
                    GlowHalfPeriod = type == 6 ? progfx.GetFloat(prog, "Param7", 0f) / 1000f : 0f,
                });
                _alphaEffects[effectId] = subject;
            }
            else if (type == 1 && content.Textures.Get(progfx.Get(prog, "Param1")) is { } skin)
            {
                target.SetEffectLook(effectId, new ObjectView.EffectLook { Skin = skin.Texture, SkinFlipV = skin.FlipV });
                _alphaEffects[effectId] = subject;
            }
            else if (type == 13)
            {
                target.SetEffectLook(effectId, new ObjectView.EffectLook { FreezeAnimations = true });
                _alphaEffects[effectId] = subject;
            }
            // "Light" (type 4): VFX_DUR_LIGHT_*, the light an item's Light property gives (CreatureLight). The vision
            // rows (Param5 1: darkvision, blindsight, low-light) are left out.
            else if (type == 4 && progfx.GetInt(prog, "Param5", 0) == 0 && CreatureLight.FromProgFx(prog, content) is { } light)
            {
                var holder = new Node3D { Name = "LightFx" };
                CreatureLight.Apply(holder, light);
                target.AddChild(holder);
                created.Add(holder);
            }
        }
        string size = target is CreatureView cv ? cv.SizeLetter : "M";
        foreach (var (model, node) in new[] { (table.Get(row, "Imp_HeadCon_Node"), "headconjure"), (table.Get(row, "Imp_Impact_Node"), "impact"), (VisualEffectModel.Root(table, row, "Imp", size), "") })
        {
            if (model.Length == 0) continue;
            var fx = BuildModel(model, burst: true, duration ? "duration" : "impact");
            if (fx == null) continue;
            // Effects keep the world's orientation unless visualeffects.2da says to turn with the object
            // (parity/visual, scene emitcal: an effect on a creature facing the camera is not turned to face it).
            if (target != null && table.GetInt(row, "OrientWithObject", 0) != 1) fx = new Rendering.Emitters.WorldAligned(fx);
            Attach(fx, target, node, point);
            created.Add(fx);
        }
        // Programmed spell-failure visuals carry both their body node and model in progfx.
        // They have no ordinary Imp_* model columns (the stock head/hand failure rows).
        int impactProgram = table.GetInt(row, "ProgFX_Impact", -1);
        var programs = content.Resources.TwoDA("progfx");
        if (impactProgram >= 0 && programs.GetInt(impactProgram, "Type", -1) == 12
            && BuildModel(programs.Get(impactProgram, "Param2"), burst: true, "impact") is { } failure)
        {
            Attach(failure, target, programs.Get(impactProgram, "Param1"), point);
            GetTree().CreateTimer(OneShotSeconds(failure)).Timeout += () => { if (IsInstanceValid(failure)) failure.QueueFree(); };
        }
        StartAudio(table, row, target, point, effectId, created);
        if (duration && effectId != 0)
        {
            _byEffect[effectId] = created;
            // Native reads the correctly spelled header. The shipped table typo
            // SoundCessastion is ignored, including its stock cessation-only rows.
            string cessationSound = table.Get(row, "SoundCessation");
            if (cessationSound.Length > 0)
                _cessations[effectId] = () =>
                {
                    if (target != null && !IsInstanceValid(target)) return;
                    PlayAudio(cessationSound, target, point, loop: false);
                };
        }
        else
            foreach (var n in created)
            {
                var model = n is Rendering.Emitters.WorldAligned { Content: { } inner } ? inner : n;
                GetTree().CreateTimer(model.HasMeta(LengthMeta) ? OneShotSeconds(model) : 3.0).Timeout += () => { if (IsInstanceValid(n)) n.QueueFree(); };
            }
    }

    /// <summary>Ends an effect node: models play their cessation, sounds stop.</summary>
    void End(Node3D n)
    {
        if (!IsInstanceValid(n)) return;
        if (n is Rendering.Emitters.WorldAligned { Content: { } inner } && IsInstanceValid(inner)) n = inner;
        if (n.HasMeta(LingerMeta)) EndEffect(n);
        else n.QueueFree();
    }

    public void Attach(Node3D fx, ObjectView? target, string node, Vector3? point)
    {
        if (target is CreatureView { Skeleton: { } sk } && node.Length > 0 && ModelBuilder.FindBone(sk, node) is var bone && bone >= 0)
        {
            // Godot resolves the attachment by name once it enters the tree, so give it the name too.
            var attach = new BoneAttachment3D { Name = "fx_attach", BoneName = sk.GetBoneName(bone), BoneIdx = bone };
            attach.SetMeta("effect_attachment", true);
            sk.AddChild(attach);
            attach.AddChild(fx);
            // A model rebuild temporarily removes this subtree while rebinding its bone.
            fx.TreeExited += () => Callable.From(() =>
            {
                if (IsInstanceValid(attach) && (!IsInstanceValid(fx) || !fx.IsInsideTree() || fx.GetParent() != attach))
                    attach.QueueFree();
            }).CallDeferred();
        }
        else if (target != null) target.AddChild(fx);
        else
        {
            Session.Space.AddChild(fx);
            fx.Position = point ?? Vector3.Zero;
        }
    }

    /// <summary>
    /// A VFX model with its emitters and lights. Animated models play their animations as the original does: a
    /// one-shot effect its "impact"; a lasting one "impact" and then "duration" in a loop, and "cessation" when it
    /// ends (<see cref="EndEffect"/>). The animations key the emitters' controllers (birthrate, alpha, size,
    /// colour ...) and the meshes' opacity, so effects fade in, flash and switch emitters on and off as designed;
    /// explosion emitters burst when their keyed birthrate turns on, or at once when nothing keys them.
    /// </summary>
    public Node3D? BuildModel(string resref, bool burst, string animation = "")
    {
        var content = Session.Content!;
        var data = content.Models.Get(resref);
        if (data == null) return null;
        var root = new Node3D { Name = "fx_" + resref };
        var anims = content.Models.AllAnimations(data).ToList();
        float maxLife = data.Parts.Where(p => p.Emitter != null).Select(p => Math.Clamp(p.Emitter!.LifeExpectancy, 0, 6)).DefaultIfEmpty(0).Max();
        if (anims.Count > 0)
        {
            bool deferred = EffectAnimation.WaitsForCessation(animation, anims.Select(a => a.Name));
            var sk = ModelBuilder.BuildSkinned(data, content.Materials, part => content.Materials.Get(part.Mesh!, MaterialKind.World),
                anims.Select(a => a.Animation).ToList(), burstUnkeyed: !deferred, lights: true);
            // Effect meshes laid on the ground (Hellball's cracks, scorch marks) sit a little above it, clear of the
            // tiles they would otherwise z-fight with.
            sk.Position = new Vector3(0, 0, GroundLift);
            root.AddChild(sk);
            var lib = new AnimationLibrary();
            foreach (var (name, anim, _) in anims) lib.AddAnimation(name, ModelBuilder.BuildAnimation(anim, sk, "Skeleton3D", 1f));
            var player = new AnimationPlayer { Name = "AnimationPlayer" };
            root.AddChild(player);
            player.AddAnimationLibrary("", lib);
            string Find(string name) => anims.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Name ?? "";
            string impact = Find("impact"), duration = Find("duration"), cessation = Find("cessation");
            string fallback = Find("default") is { Length: > 0 } d ? d : anims[0].Name;
            float lifetime;
            if (deferred)
            {
                // Cessation-only models (vce_positive/negative) have no start or
                // duration phase. Do not loop their ending or emit static particles.
                root.Visible = false;
                foreach (var emitter in sk.FindChildren("*", "Node3D", true, false).OfType<Rendering.Emitters.AuroraEmitter>())
                    emitter.SetEmitting(false);
                root.SetMeta(DeferredMeta, true);
                root.SetMeta(DeferredBurstMeta, !anims.Any(a => a.Animation.Events.Any(e => e.Name.Equals("detonate", StringComparison.OrdinalIgnoreCase))));
                lifetime = float.MaxValue;
            }
            else if (animation == "duration" && duration.Length > 0)
            {
                lib.GetAnimation(duration).LoopMode = Animation.LoopModeEnum.Linear;
                if (impact.Length > 0) { player.Play(impact); player.Queue(duration); }
                else player.Play(duration);
                lifetime = float.MaxValue;
            }
            else
            {
                string clip = animation == "impact" && impact.Length > 0 ? impact : animation.Length > 0 && Find(animation) is { Length: > 0 } named ? named : fallback;
                // Conjuring and travelling effects loop; an impact model applied as a lasting effect (elemental
                // protection on a drow wizard) plays its impact once and then holds, as the original does: looping it
                // re-ran its snowfall for as long as the effect lasted.
                if (!burst && animation.Length == 0 || animation == "duration" && clip != impact) lib.GetAnimation(clip).LoopMode = Animation.LoopModeEnum.Linear;
                player.Play(clip);
                lifetime = (float)lib.GetAnimation(clip).Length;
            }
            // Casting-hand models use "fade" for their authored wind-down, while
            // lasting impact effects use "cessation".
            root.SetMeta(CessationMeta, cessation.Length > 0 ? cessation : Find("fade"));
            root.SetMeta(LingerMeta, Math.Max(maxLife, 0.5f));
            root.SetMeta(LengthMeta, lifetime == float.MaxValue ? -1f : lifetime);
        }
        else
        {
            root.AddChild(ModelBuilder.BuildStatic(data, content.Materials, lights: true, emitters: true, effect: true).Root);
            root.SetMeta(LingerMeta, Math.Max(maxLife, 0.5f));
            root.SetMeta(LengthMeta, 0f);
        }
        return root;
    }

    const float GroundLift = 0.02f;
    const string DeferredMeta = "fx_deferred_cessation", DeferredBurstMeta = "fx_deferred_burst";
    const string CessationMeta = "fx_cessation", LingerMeta = "fx_linger", LengthMeta = "fx_length";

    /// <summary>How long a one-shot effect model runs: its animation, then its last particles' lives.</summary>
    public static float OneShotSeconds(Node3D fx)
    {
        float length = fx.HasMeta(LengthMeta) ? fx.GetMeta(LengthMeta).AsSingle() : 0f;
        float linger = fx.HasMeta(LingerMeta) ? fx.GetMeta(LingerMeta).AsSingle() : 1f;
        return Math.Clamp(Math.Max(length, 0) + linger, 2.5f, 12f);
    }

    /// <summary>
    /// Ends a lasting effect: its "cessation" animation plays (the glow fades, the emitters wind down), particles
    /// already in the air finish, then the model is freed.
    /// </summary>
    public void EndEffect(Node3D fx)
    {
        if (!IsInstanceValid(fx)) return;
        float wait = fx.HasMeta(LingerMeta) ? fx.GetMeta(LingerMeta).AsSingle() : 0.5f;
        string cessation = fx.HasMeta(CessationMeta) ? fx.GetMeta(CessationMeta).AsString() : "";
        if (cessation.Length > 0 && fx.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") is { } player && player.HasAnimation(cessation))
        {
            if (fx.HasMeta(DeferredMeta) && fx.GetMeta(DeferredMeta).AsBool())
            {
                fx.SetMeta(DeferredMeta, false);
                fx.Visible = true;
                foreach (var emitter in fx.FindChildren("*", "Node3D", true, false).OfType<Rendering.Emitters.AuroraEmitter>())
                {
                    emitter.SetEmitting(true);
                    // The unkeyed burst was suppressed at construction. Fire it
                    // now; keyed rates and authored detonate events own the rest.
                    if (emitter.Data.Is("Explosion") && !emitter.Options.Range.BirthRateKeyed
                        && fx.GetMeta(DeferredBurstMeta).AsBool()) emitter.Detonate();
                }
            }
            player.ClearQueue();
            player.Play(cessation);
            wait += (float)player.GetAnimation(cessation).Length;
        }
        else
            // FindChildren's type filter uses Godot's native class names, not this
            // C# script's name. Filter the native descendants with the managed type.
            foreach (var em in fx.FindChildren("*", "Node3D", true, false).OfType<Rendering.Emitters.AuroraEmitter>())
                em.SetEmitting(false);
        GetTree().CreateTimer(Math.Min(wait, 8f)).Timeout += () => { if (IsInstanceValid(fx)) fx.QueueFree(); };
    }

    /// <summary>The "hit" event of the humanoid attack clips (1hslashl and the rest: 0.5 s into 1 s).</summary>
    const float HitEvent = 0.5f;

    void Attack(JsonElement d)
    {
        uint attacker = d.GetProperty("attacker").GetUInt32(), target = d.GetProperty("target").GetUInt32();
        var anim = d.GetProperty("animation").GetString() ?? "";
        var a = Session.Objects.GetValueOrDefault(attacker) as CreatureView;
        // An empty animation is a follow-up hit of the same swing (whirlwind, cleave targets).
        if (anim.Length > 0 && a != null)
            a.PlayAnimation(new PlayAnimationMessage { ObjectId = attacker, Animation = anim });
        if (Session.Objects.GetValueOrDefault(target) is not { } t) return;
        bool hit = d.GetProperty("hit").GetBoolean();
        // The weapon's sounds (weaponsounds.2da, chosen by the server) at the swing's "hit" event: half way through
        // the 1 s attack clips, humanoid and creature (ca1slashl, ccloseh) alike, read from the clip played.
        if (d.TryGetProperty("sounds", out var sounds) && sounds.GetArrayLength() > 0)
        {
            var names = sounds.EnumerateArray().Select(s => s.GetString() ?? "").Where(s => s.Length > 0).ToList();
            double at = a?.HitTime(anim, HitEvent) ?? HitEvent;
            GetTree().CreateTimer(at).Timeout += () => { foreach (var name in names) PlayAudio(name, t, null, loop: false); };
        }
        if (t is CreatureView tc)
        {
            // Reactions play a beat later, when the blow lands.
            var reaction = hit ? "damages" : (Random.Shared.Next(2) == 0 ? "dodges" : "dodgelr");
            GetTree().CreateTimer(0.35).Timeout += () => { if (IsInstanceValid(tc)) tc.PlayAnimation(new PlayAnimationMessage { ObjectId = target, Animation = reaction }); };
        }
        // Damage numbers and hit words are drawn by the HUD (UI/Hud/CombatText).
    }

    void AoeCreated(JsonElement d)
    {
        var content = Session.Content;
        if (content == null) return;
        int row = d.GetProperty("aoe").GetInt32();
        var table = content.Resources.TwoDA("vfx_persistent");
        var p = d.GetProperty("pos");
        var at = new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle());
        float radius = d.TryGetProperty("radius", out var r) ? r.GetSingle() : 5;
        bool rectangular = d.TryGetProperty("shape", out var shape) && shape.GetString() == "R";
        float width = d.TryGetProperty("width", out var w) ? w.GetSingle() : 0;
        float length = d.TryGetProperty("length", out var l) ? l.GetSingle() : 0;
        float facing = d.TryGetProperty("facing", out var f) ? Mathf.DegToRad(f.GetSingle()) : 0;
        var nodes = new List<Node3D>();
        foreach (var col in new[] { "MODEL01", "MODEL02", "MODEL03" })
        {
            var model = table.Get(row, col);
            if (model.Length == 0) continue;
            int count = Math.Clamp(table.GetInt(row, "NUMACT0" + col[^1], 1), 1, 12);
            for (int i = 0; i < count; i++)
            {
                if (BuildModel(model, burst: false, "duration") is not { } fx) continue;
                Vector3 offset;
                if (rectangular)
                {
                    // Rectangle rows have no radius. Use the server's oriented footprint,
                    // otherwise every wall actor is stacked at its center.
                    float along = (float)GD.RandRange(-length / 2, length / 2);
                    float across = (float)GD.RandRange(-width / 2, width / 2);
                    offset = new Vector3(along * Mathf.Cos(facing) - across * Mathf.Sin(facing),
                        along * Mathf.Sin(facing) + across * Mathf.Cos(facing), 0);
                }
                else
                {
                    float a = (float)GD.RandRange(0, Mathf.Tau), dist = (float)GD.RandRange(0, radius * 0.8f);
                    offset = new Vector3(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist, 0);
                }
                Session.Space.AddChild(fx);
                fx.Position = at + offset;
                nodes.Add(fx);
            }
        }
        // The area's lasting visual effect (visualeffects.2da row): web strands, the darkness globe.
        if (int.TryParse(table.Get(row, "DurationVFX"), out int vfxRow) && vfxRow >= 0)
        {
            var vfx = content.Resources.TwoDA("visualeffects");
            foreach (var column in new[] { "Imp_Root_M_Node", "Imp_Impact_Node" })
            {
                var model = vfx.Get(vfxRow, column);
                if (model.Length == 0 || BuildModel(model, burst: false, "duration") is not { } fx) continue;
                Session.Space.AddChild(fx);
                fx.Position = at;
                nodes.Add(fx);
                break;
            }
        }
        uint id = d.GetProperty("id").GetUInt32();
        // The same area of effect announced again replaces the models shown for it.
        if (_aoes.Remove(id, out var shown)) foreach (var n in shown) End(n);
        _aoes[id] = nodes;
        _aoeAt[id] = at;
    }
}
