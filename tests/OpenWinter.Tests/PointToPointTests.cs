using System.Numerics;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

public class PointToPointTests
{
    static EmitterData Stream() => new() { Update = "Fountain", Render = "Linked", P2P = true, P2PBezier = true };

    [Fact]
    public void BoundSupportedStreamReplacesFallbackButUnbuiltOrUnmeasuredPathsDoNot()
    {
        var model = new ModelData();
        var emitter = Stream();
        model.Parts.Add(new() { Parent = -1, Emitter = emitter });
        model.Parts.Add(new() { Parent = 0, Reference = new() { Model = "fx_ref", Reattachable = true } });
        Assert.True(Assert.Single(BeamModelLayout.Decorations(model)).Targeted);
        Assert.True(BeamModelLayout.NeedsLegacyConnector(model));
        Assert.False(BeamModelLayout.NeedsLegacyConnector(model, boundPointToPoint: true));
        emitter.P2PBezier = false;
        Assert.False(BeamModelLayout.CanTarget(model, 0));
        Assert.True(BeamModelLayout.NeedsLegacyConnector(model, boundPointToPoint: true));
    }

    [Fact]
    public void UnboundNestedOrNonReattachableReferencesDoNotEnterTargetRoute()
    {
        var model = new ModelData();
        model.Parts.Add(new() { Parent = -1, Emitter = Stream() });
        model.Parts.Add(new() { Parent = 0 });
        model.Parts.Add(new() { Parent = 1, Reference = new() { Model = "fx_ref", Reattachable = true } });
        Assert.False(BeamModelLayout.CanTarget(model, 0));
        model.Parts[2].Parent = 0;
        model.Parts[2].Reference!.Reattachable = false;
        Assert.False(BeamModelLayout.CanTarget(model, 0));
    }

    [Fact]
    public void NonzeroHandlesAndLocalOrNonFountainModesRemainOutsideMeasuredRoute()
    {
        var e = Stream();
        Assert.True(PointToPoint.Supports(e));
        e.P2PBezier2 = 1;
        Assert.False(PointToPoint.Supports(e));
        e.P2PBezier2 = 0; e.P2PBezier3 = 1;
        Assert.False(PointToPoint.Supports(e));
        e.P2PBezier3 = 0; e.Inherit = true;
        Assert.False(PointToPoint.Supports(e));
        e.Inherit = false; e.Update = "Explosion";
        Assert.False(PointToPoint.Supports(e));
    }

    [Fact]
    public void LifetimeScalesTraversalWhileBirthAndLiveTargetStayIndependent()
    {
        var birth = new Vector3(2, 3, 4);
        var target = new Vector3(10, 3, 4);
        Assert.Equal(birth, PointToPoint.Position(birth, target, 0, 1));
        Assert.Equal(target, PointToPoint.Position(birth, target, 1, 1));
        Assert.Equal(target, PointToPoint.Position(birth, target, 2, 2));
        Assert.Equal(new Vector3(6, 3, 4), PointToPoint.Position(birth, target, 1, 2));
        Assert.Equal(PointToPoint.Position(birth, target, .25f, 1), PointToPoint.Position(birth, target, .5f, 2));
        Assert.Equal(new Vector3(2, 7, 4), PointToPoint.Position(birth, new Vector3(2, 11, 4), 1, 2));
    }
}
