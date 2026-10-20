using System.Numerics;
using OpenWinter.Client.Views.WorldFx.Logic;
using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

public class BeamModelLayoutTests
{
    [Fact]
    public void DecorationRetainsAccumulatedParentRotationAndTranslation()
    {
        var model = new ModelData();
        model.Parts.Add(new ModelPart { Name = "root", Parent = -1, Position = new(5, 0, 0) });
        model.Parts.Add(new ModelPart
        {
            Name = "offset", Parent = 0, Position = new(1, 2, 3),
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2)
        });
        var marker = new EmitterData { Update = "Fountain" };
        model.Parts.Add(new ModelPart { Name = "marker", Parent = 1, Position = new(2, 0, 0), Emitter = marker });
        model.Parts.Add(new ModelPart { Name = "bolt", Parent = 0, Emitter = new() { Update = "Lightning" } });

        var decoration = Assert.Single(BeamModelLayout.Decorations(model));
        Assert.Same(marker, decoration.Data);
        Near(new Vector3(6, 4, 3), Vector3.Transform(Vector3.Zero, decoration.Transform));
        Near(Vector3.UnitY, Vector3.TransformNormal(Vector3.UnitX, decoration.Transform));
        Assert.Equal(new Vector3(2, 0, 0), model.Parts[2].Position);
    }

    [Fact]
    public void DecorationOnlyAndEmptyModelsDoNotInventConnectors()
    {
        var model = new ModelData();
        Assert.False(BeamModelLayout.NeedsLegacyConnector(model));
        model.Parts.Add(new ModelPart { Parent = -1, Emitter = new() { Update = "Fountain" } });
        Assert.False(BeamModelLayout.NeedsLegacyConnector(model));
        model.Parts.Add(new ModelPart { Parent = -1, Emitter = new() { Update = "Single" } });
        Assert.False(BeamModelLayout.NeedsLegacyConnector(model));
    }

    [Fact]
    public void UncorrectedPointToPointModelsRetainOnlyTheirExistingFallback()
    {
        var model = new ModelData();
        model.Parts.Add(new ModelPart { Parent = -1, Emitter = new() { Update = "Fountain", P2P = true } });
        Assert.True(BeamModelLayout.NeedsLegacyConnector(model));
        model.Parts.Add(new ModelPart { Parent = -1, Emitter = new() { Update = "lightning" } });
        Assert.False(BeamModelLayout.NeedsLegacyConnector(model));
    }

    static void Near(Vector3 expected, Vector3 actual) => Assert.True(Vector3.Distance(expected, actual) < 0.00001f,
        $"Expected {expected}, got {actual}");

    [Fact]
    public void OnlyNamedReattachableLightningChildrenEnterTheMeasuredEndpointPath()
    {
        var model = new ModelData();
        model.Parts.Add(new ModelPart { Emitter = new() { Update = "Lightning" } });
        model.Parts.Add(new ModelPart { Emitter = new() { Update = "Fountain", P2P = true } });
        var expected = new ModelReference { Model = "fx_ref_cold", Reattachable = true };
        model.Parts.Add(new ModelPart { Parent = 0, Reference = expected });
        model.Parts.Add(new ModelPart { Parent = 0, Reference = new() { Model = "fixed", Reattachable = false } });
        model.Parts.Add(new ModelPart { Parent = 0, Reference = new() { Model = "", Reattachable = true } });
        model.Parts.Add(new ModelPart { Parent = 1, Reference = new() { Model = "p2p", Reattachable = true } });
        model.Parts.Add(new ModelPart { Parent = 50, Reference = new() { Model = "invalid", Reattachable = true } });
        model.Parts.Add(new ModelPart { Parent = -1, Reference = new() { Model = "root", Reattachable = true } });
        Assert.Same(expected, Assert.Single(BeamModelLayout.EndpointReferences(model)));
    }
}
