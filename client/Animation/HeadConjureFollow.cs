using Godot;

namespace OpenWinter.Client.Anim;

/// <summary>
/// Keeps a creature's "headconjure" node (where head visual effects attach) above its head as the head moves. In
/// the models it hangs off the root at a fixed height, so it would stay in the air over a creature lying down; the
/// original's sleep and stun visuals go down with the sleeper (parity/visual scene conditionfreeze, b_sleep_*:
/// VFX_IMP_SLEEP's Zs at the lying bandit). The node keeps its resting offset from the head, in world orientation.
/// </summary>
public partial class HeadConjureFollow : SkeletonModifier3D
{
    int _conjure = -1, _head = -1;
    Vector3 _offset;

    public static void Attach(Skeleton3D skeleton)
    {
        int conjure = Rendering.ModelBuilder.FindBone(skeleton, "headconjure");
        int head = Rendering.ModelBuilder.FindBone(skeleton, "head");
        if (head < 0) head = Rendering.ModelBuilder.FindBone(skeleton, "head_g");
        if (conjure < 0 || head < 0) return;
        var follow = new HeadConjureFollow { Name = "HeadConjureFollow", _conjure = conjure, _head = head };
        follow._offset = skeleton.GetBoneGlobalRest(conjure).Origin - skeleton.GetBoneGlobalRest(head).Origin;
        skeleton.AddChild(follow);
    }

    public override void _ProcessModificationWithDelta(double delta)
    {
        var sk = GetSkeleton();
        if (sk == null || _conjure >= sk.GetBoneCount() || _head >= sk.GetBoneCount()) return;
        var target = sk.GetBoneGlobalPose(_head).Origin + _offset;
        int parent = sk.GetBoneParent(_conjure);
        var local = parent >= 0 ? sk.GetBoneGlobalPose(parent).AffineInverse() * target : target;
        sk.SetBonePosePosition(_conjure, local);
    }
}
