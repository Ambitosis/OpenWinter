using System.Numerics;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

public class PartPlacementTests
{
    static ModelData Robe() => new()
    {
        Name = "robe",
        Parts =
        {
            new ModelPart { Name = "robe", Parent = -1 },
            new ModelPart { Name = "rootdummy", Parent = 0, Position = new Vector3(0, 0, 1.2f) },
            new ModelPart { Name = "pelvis_g", Parent = 1, Position = new Vector3(0, 0.01f, 0) },
            new ModelPart { Name = "belt", Parent = 2, Position = new Vector3(0, -0.03f, -0.07f) },
            new ModelPart { Name = "belt1", Parent = 3, Position = new Vector3(0.007f, 0.13f, 0.02f), Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.5f) },
        },
    };

    [Fact]
    public void ANodeTheSkeletonLacksRidesItsNearestAncestorBone()
    {
        var robe = Robe();
        var globals = robe.RestGlobals();
        int[] map = [0, 1, 7, -1, -1];
        var (bone, offset) = PartPlacement.RigidPlace(robe, map, globals, 4);
        Assert.Equal(7, bone);
        // Back in the robe's space, the offset from the pelvis is the node's own rest transform.
        var back = offset * globals[2];
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                Assert.Equal(globals[4][r, c], back[r, c], 4);
    }

    [Fact]
    public void ANodeTheSkeletonHasIsItsOwnBone()
    {
        var robe = Robe();
        var (bone, offset) = PartPlacement.RigidPlace(robe, [0, 1, 7, -1, -1], robe.RestGlobals(), 2);
        Assert.Equal(7, bone);
        Assert.Equal(Matrix4x4.Identity, offset);
    }

    [Fact]
    public void WithoutAnyMatchedAncestorTheNodeKeepsItsRestPlaceUnderTheRoot()
    {
        var robe = Robe();
        var globals = robe.RestGlobals();
        var (bone, offset) = PartPlacement.RigidPlace(robe, [-1, -1, -1, -1, -1], globals, 3);
        Assert.Equal(0, bone);
        Assert.Equal(globals[3], offset);
    }

    [Fact]
    public void LinkedRibbonsAreAsWideAsBeams()
    {
        Assert.Equal(1.1f, FxMath.LinkedWidth(0.5f), 5);
        Assert.Equal(0f, FxMath.LinkedWidth(-1f));
        Assert.Equal(FxMath.BeamWidth(0.15f, 0.15f), FxMath.LinkedWidth(0.15f), 5);
    }
}
