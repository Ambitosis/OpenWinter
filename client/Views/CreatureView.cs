using OpenWinter.Client.Anim;
using OpenWinter.Client.Rendering;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

public partial class CreatureView : ObjectView
{
    public CreatureAnimator? Animator { get; private set; }
    public Skeleton3D? Skeleton { get; private set; }
    /// <summary>S, M, L or H (visualeffects.2da root node columns), from appearance.2da SIZECATEGORY.</summary>
    public string SizeLetter { get; private set; } = "M";

    SpawnMessage? _spawn;

    /// <summary>How the creature looks now (replaced on every appearance change; the inventory's paper doll follows it).</summary>
    public Appearance? CurrentAppearance => _spawn?.Appearance;

    /// <summary>Replaces the model after an appearance change, keeping position and stance.</summary>
    public void Rebuild(Appearance appearance, ClientContent content)
    {
        if (_spawn == null) return;
        if (_equipmentDrawPending) { _pendingEquipment = (appearance, content); return; }
        var playback = Animator?.CaptureAction();
        var attachments = Skeleton?.GetChildren().OfType<BoneAttachment3D>()
            .Where(a => a.HasMeta("effect_attachment")).ToArray()
            ?? Array.Empty<BoneAttachment3D>();
        // Keep the existing effect instances and their clocks while replacing the skeleton.
        foreach (var attachment in attachments) attachment.Reparent(this);
        foreach (var child in GetChildren())
            if (child is Skeleton3D or CreatureAnimator or MeshInstance3D || child.Name == CreatureLight.NodeName) { RemoveChild(child); child.QueueFree(); }
        Skeleton = null;
        Animator = null;
        _spawn.Appearance = appearance;
        Build(_spawn, content);
        ApplyPresentationStance();
        Animator?.RestoreAction(playback);
        foreach (var attachment in attachments)
        {
            int bone = Skeleton == null ? -1 : ModelBuilder.FindBone(Skeleton, attachment.BoneName);
            if (bone < 0)
            {
                // Models without this bone retain the effect at its last relative position.
                foreach (var effect in attachment.GetChildren()) effect.Reparent(this);
                attachment.QueueFree();
                continue;
            }
            attachment.BoneName = Skeleton!.GetBoneName(bone);
            attachment.BoneIdx = bone;
            attachment.Reparent(Skeleton, false);
        }
        if (HasLook) ApplyEffectAlpha();
    }

    public static CreatureView Create(SpawnMessage spawn, ClientContent content)
    {
        var view = new CreatureView { _spawn = spawn };
        view.Init(spawn);
        view.Build(spawn, content);
        return view;
    }

    void Build(SpawnMessage spawn, ClientContent content)
    {
        _presentationContent = content;
        var appearance = content.Resources.TwoDA("appearance");
        int row = spawn.Appearance.Type;
        SizeLetter = appearance.GetInt(row, "SIZECATEGORY", 3) switch { <= 2 => "S", 3 => "M", 4 => "L", _ => "H" };
        string modelType = appearance.Get(row, "MODELTYPE").ToUpperInvariant();
        var built = modelType.StartsWith("P")
            ? PartBasedBuilder.Build(spawn.Appearance, content, _drinkHidesWeapon ? 0x10 : 0)
            : content.Models.Get(appearance.Get(row, "RACE")) is { } plain
                ? PartBasedBuilder.WithHeldItems(content.Tinter.TintModel(plain, PartBasedBuilder.Layers(spawn.Appearance.Colors, null)), spawn.Appearance, content, _drinkHidesWeapon ? 0x10 : 0)
                : null;
        if (built == null)
        {
            AddChild(Placeholder());
            return;
        }
        var model = built;
        var animSource = PartBasedBuilder.AnimationSource(model, content);
        var animations = content.Models.AllAnimations(animSource).ToList();
        Skeleton = ModelBuilder.BuildSkinned(model, content.Materials, null, animations.Select(a => a.Item2).ToList(), warm: true);
        AddChild(Skeleton);
        // Creature lights (a torch's) cast only creatures' shadows.
        foreach (var mi in Skeleton.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
            if (mi.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off) mi.Layers |= CreatureLight.CasterLayer;
        CreatureLight.Apply(this, CreatureLight.For(spawn.Appearance, content));
        _footstepContent = content;
        if (spawn.Appearance.Scale is > 0 and not 1f) Scale = Vector3.One * spawn.Appearance.Scale;
        // Authored creature models can be static. Keep their geometry, lights and
        // effect attachment skeleton without constructing an empty animation graph.
        if (animations.Count == 0) return;
        Anim.HeadConjureFollow.Attach(Skeleton);
        var library = new AnimationLibrary();
        var transitions = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        _equipmentDrawEvents.Clear();
        _hitEvents.Clear();
        foreach (var (name, anim, inherited) in animations)
        {
            float scale = inherited || animSource != model ? model.AnimationScale : 1f;
            var animation = ModelBuilder.BuildAnimation(PartBasedBuilder.WithItemTracks(model, name, anim, scale), Skeleton, "Skeleton3D", scale);
            AddFootstepEvents(animation, anim);
            ReadEquipmentDrawEvent(name, anim);
            ReadHitEvent(name, anim);
            library.AddAnimation(name, animation);
            transitions[name] = anim.Transition;
        }
        float walkDist = appearance.GetFloat(row, "WALKDIST", 1.6f);
        float runDist = appearance.GetFloat(row, "RUNDIST", 3.0f);
        // Non-humanoid models use the creature animation set (cpause1, cwalk, crun); for humanoids cpause1 is the combat idle.
        bool creatureSet = !library.HasAnimation("pause1") && library.HasAnimation("cpause1");
        // Humanoids hold their weapons as the original does: the ready stance and the arm clips over idling, walking
        // and running follow what is in their hands (WeaponStances).
        var stance = StanceOf(spawn.Appearance, content, model);
        if (!creatureSet) ApplyOverlays(library, stance);
        string idle = creatureSet ? "cpause1" : "pause1", walk = creatureSet ? "cwalk" : "walk", run = creatureSet ? "crun" : "run";
        float walkLen = library.HasAnimation(walk) ? (float)library.GetAnimation(walk).Length : 1f;
        float runLen = library.HasAnimation(run) ? (float)library.GetAnimation(run).Length : 1f;
        Animator = new CreatureAnimator { Name = "Animator" };
        AddChild(Animator);
        Animator.Setup(library, Skeleton, new AnimationProfile
        {
            Idle = idle, Walk = walk, Run = run,
            CombatIdle = creatureSet ? (library.HasAnimation("creadyr") ? "creadyr" : "")
                : library.HasAnimation(stance.Ready) ? stance.Ready : library.HasAnimation("cpause1") ? "cpause1" : "nwreadyr",
            CombatWalk = creatureSet ? "" : "cwalkf",
            ItemReady = _potionReadyClip = PotionReadyClip(spawn.Appearance, content),
            WalkSpeed = walkDist / Math.Max(0.05f, walkLen),
            RunSpeed = runDist / Math.Max(0.05f, runLen),
            Transitions = transitions,
            UpperBodyRoot = Skeleton.FindBone("torso_g") >= 0 ? "torso_g" : "torso",
        });
        HoldPose(spawn.Appearance, content, animations);
        Animator.SetDead(Lying, immediate: true);
        _conditionsShown = false;
        ApplyConditions(immediate: true);
    }

    /// <summary>
    /// A torch in the left hand is held up (the torchl pose on the left arm) whatever the creature is doing on its feet.
    /// The torch is the only hand-held base item baseitems.2da marks as no weapon (WeaponWield 1).
    /// In a conversation the original lets the arm down (the talk animations then move the torch with the arm) and
    /// raises it again when the conversation ends (parity/visual scene talktorch).
    /// </summary>
    void HoldPose(Appearance a, ClientContent content, List<(string Name, OpenWinter.Content.ModelAnimation Animation, bool Inherited)> animations)
    {
        _heldPose = null;
        if (Animator == null) return;
        var items = content.Resources.TwoDA("baseitems");
        bool torch = a.Equipment.Any(e => e.Slot == SlotLeft && items.GetInt(e.BaseItem, "WeaponWield", 0) == 1);
        var pose = torch ? animations.FirstOrDefault(x => string.Equals(x.Name, TorchPose, StringComparison.OrdinalIgnoreCase)).Animation : null;
        if (pose != null) _heldPose = (TorchPose, pose.Root, pose.Transition);
        ApplyHeldPose();
    }

    (string Clip, string Root, float Fade)? _heldPose;

    void ApplyHeldPose()
    {
        if (Animator == null) return;
        if (_heldPose is { } p && (Flags & (ObjectFlags.Talking | ObjectFlags.Dead | ObjectFlags.Dying)) == 0) Animator.SetArmPose(p.Clip, p.Root, p.Fade);
        else Animator.SetArmPose("", "", _heldPose?.Fade ?? 0.25f);
    }

    /// <summary>Dead, or a dying player: the original shows both in the death pose, and a healed player gets up again.</summary>
    bool Lying => (Flags & (ObjectFlags.Dead | ObjectFlags.Dying)) != 0;

    const string TorchPose = "torchl";
    const int SlotLeft = 0x20;

    static Node3D Placeholder()
    {
        var mesh = new CapsuleMesh { Radius = 0.35f, Height = 1.8f };
        var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.3f, 0.9f) } };
        mi.Position = new Vector3(0, 0, 0.9f);
        mi.RotationDegrees = new Vector3(90, 0, 0);
        return mi;
    }

    public override void ApplyUpdate(UpdateMessage u)
    {
        ReconcileBump(u);
        base.ApplyUpdate(u);
        _surface = u.Surface;
        _footstepType = u.FootstepType;
        ApplyPresentationStance();
        Animator?.SetDead(Lying);
        ApplyConditions(immediate: false);
        ApplyHeldPose();
    }

    public override void PlayAnimation(PlayAnimationMessage m)
    {
        if (Animator == null) return;
        if (m.Animation.Equals("drink", StringComparison.OrdinalIgnoreCase)) ShowDrinkBottle();
        if (m.Gesture) Animator.PlayGesture(m.Animation, m.Duration);
        else Animator.PlayAction(m.Animation, m.Duration);
    }

    public override void _Process(double delta)
    {
        float facingGap = Mathf.AngleDifference(Rotation.Z, FacingToModel(TargetFacing));
        base._Process(delta);
        UpdateEquipmentDraw();
        if (Animator != null) Animator.GroundSpeed = Lying || DownForCondition ? 0 : NetSpeed;
        Animator?.SetDrive(NetMode == MovementMode.Stand && Math.Abs(facingGap) > 0.01f && (Flags & (ObjectFlags.Dead | ObjectFlags.Dying | ObjectFlags.Talking)) == 0 && !DownForCondition
            ? facingGap > 0 ? MovementMode.TurnLeft : MovementMode.TurnRight : NetMode,
            finishTurn: (Flags & (ObjectFlags.Dead | ObjectFlags.Dying | ObjectFlags.Talking)) == 0);
    }
}
