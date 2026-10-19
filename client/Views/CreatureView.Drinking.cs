using OpenWinter.Client.Rendering;
using OpenWinter.Client.Anim;
using Godot;
using OpenWinter.Net;

namespace OpenWinter.Client.Views;

public partial class CreatureView
{
    ClientContent? _presentationContent;
    BoneAttachment3D? _drinkBottle;
    bool _drinkHidesWeapon;
    long _drinkSerial;
    bool _potionReady, _spellReady;
    string _potionReadyClip = "";

    public void SetPotionReady(bool active)
    {
        _potionReady = active;
        ApplyPresentationStance();
    }

    public void SetSpellReady(bool active)
    {
        _spellReady = active;
        ApplyPresentationStance();
    }

    void ApplyPresentationStance()
    {
        if (Animator == null) return;
        // The measured humanoid ready pose also underlies the drinking gesture.
        // Direct public PlayAnimation(DRINK) does not enter this item-use state.
        Animator.Stance = (_potionReady || _spellReady) && _potionReadyClip.Length > 0 && Animator.HasClip(_potionReadyClip) ? CreatureAnimator.ItemReady
            : NetCombat ? CreatureAnimator.Combat : CreatureAnimator.Peace;
    }

    static string PotionReadyClip(Appearance appearance, ClientContent content)
    {
        var items = content.Resources.TwoDA("baseitems");
        var right = appearance.Equipment.FirstOrDefault(e => e.Slot == 0x10);
        if (right == null) return "nwreadyr";
        int size = content.Resources.TwoDA("appearance").GetInt(appearance.Type, "SIZECATEGORY", 3);
        bool offWeapon = appearance.Equipment.Any(e => e.Slot == 0x20 && items.GetInt(e.BaseItem, "WeaponType", 0) > 0);
        // Native potionstanceready identifies the one-handed clip with a dagger.
        // Other wield families retain their existing stance until measured.
        return !offWeapon && items.GetInt(right.BaseItem, "WeaponType", 0) > 0
            && items.GetInt(right.BaseItem, "WeaponWield", 0) == 0
            && items.GetInt(right.BaseItem, "WeaponSize", 0) <= size ? "1hreadyr" : "";
    }

    // Native potionbare/potionarmed: the drink animation itself temporarily holds
    // the stock blue bottle, including direct PlayAnimation without item use.
    void ShowDrinkBottle()
    {
        if (_presentationContent is not { } content || Skeleton == null || Animator == null) return;
        string clip = Animator.Resolve("drink");
        if (!Animator.HasClip(clip) || ModelBuilder.FindBone(Skeleton, "rhand") < 0) return;
        string model = content.Resources.TwoDA("baseitems").Get(49, "DefaultModel");
        if (content.Models.Get(model) is not { } data) return;
        double lifetime = Animator.GetAnimation(clip).Length;
        long serial = ++_drinkSerial;
        RemoveDrinkBottle();
        _drinkBottle = null;
        bool hideWeapon = CurrentAppearance?.Equipment.Any(e => e.Slot == 0x10) == true;
        if (hideWeapon != _drinkHidesWeapon)
        {
            _drinkHidesWeapon = hideWeapon;
            if (CurrentAppearance is { } appearance) Rebuild(appearance, content);
        }
        int bone = ModelBuilder.FindBone(Skeleton!, "rhand");
        if (bone < 0) return;
        var attachment = new BoneAttachment3D { Name = "DrinkBottle", BoneName = Skeleton!.GetBoneName(bone), BoneIdx = bone };
        // Existing rebuild handling preserves and rebinds bone attachments.
        attachment.SetMeta("effect_attachment", true);
        Skeleton.AddChild(attachment);
        _drinkBottle = attachment;
        var animations = content.Models.AllAnimations(data).ToList();
        var skeleton = ModelBuilder.BuildSkinned(data, content.Materials, animations: animations.Select(a => a.Item2).ToList());
        attachment.AddChild(skeleton);
        var library = new AnimationLibrary();
        foreach (var (name, animation, _) in animations)
            library.AddAnimation(name, ModelBuilder.BuildAnimation(animation, skeleton, "Skeleton3D", 1));
        // Native held placement matches the default mesh pose. The item model
        // drink track shifts the mesh by 20.3 cm and fails the observed hand alignment.
        if (library.HasAnimation("default"))
        {
            var player = new AnimationPlayer();
            attachment.AddChild(player);
            player.AddAnimationLibrary("", library);
            player.Play("default");
            player.Advance(0);
        }
        if (HasLook) ApplyEffectAlpha();
        GetTree().CreateTimer(Math.Max(.01, lifetime)).Timeout += () =>
        {
            if (!IsInstanceValid(this) || serial != _drinkSerial) return;
            RemoveDrinkBottle();
            _drinkBottle = null;
            if (_drinkHidesWeapon && IsInsideTree() && CurrentAppearance is { } appearance)
            {
                _drinkHidesWeapon = false;
                Rebuild(appearance, content);
            }
        };
    }

    void RemoveDrinkBottle()
    {
        if (!IsInstanceValid(_drinkBottle)) return;
        _drinkBottle!.GetParent()?.RemoveChild(_drinkBottle);
        _drinkBottle.QueueFree();
    }
}
