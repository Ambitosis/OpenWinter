using System.Buffers.Binary;
using System.Text;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using Xunit;

namespace OpenWinter.Tests;

public class ModelReferenceTests
{
    [Fact]
    public void TextReferenceRetainsIdentityAttachmentAndHierarchy()
    {
        var model = MdlAsciiReader.Read(Encoding.ASCII.GetBytes("""
            newmodel test
            beginmodelgeom test
            node emitter bolt
              parent NULL
              update Lightning
            endnode
            node reference endpoint
              parent bolt
              position 0 0 2
              refmodel custom_endpoint
              reattachable 1
            endnode
            node reference empty
              parent bolt
              refmodel NULL
              reattachable 0
            endnode
            endmodelgeom test
            donemodel test
            """));
        var endpoint = model.Parts[1];
        Assert.Equal(0, endpoint.Parent);
        Assert.Equal(2f, endpoint.Position.Z);
        Assert.Equal("custom_endpoint", endpoint.Reference!.Model);
        Assert.True(endpoint.Reference.Reattachable);
        Assert.Equal("", model.Parts[2].Reference!.Model);
        Assert.False(model.Parts[2].Reference!.Reattachable);
        Assert.Null(model.Parts[0].Reference);
    }

    [Theory]
    [InlineData("fx_ref_cold", 1)]
    [InlineData("custom_reference_model_longer_than_thirty_two_characters", 0)]
    [InlineData("NULL", 1)]
    public void BinaryReferenceUsesTheDocumentedSixtyFourByteName(string name, int reattachable)
    {
        var bytes = ReferenceModel(name, reattachable);
        var model = MdlBinaryReader.Read(bytes);
        var reference = model.Parts[1];
        Assert.Equal(0, reference.Parent);
        Assert.Equal(name == "NULL" ? "" : name, reference.Reference!.Model);
        Assert.Equal(reattachable != 0, reference.Reference.Reattachable);
        Assert.Null(model.Parts[0].Reference);
    }

    [Fact]
    public void TruncatedReferencePayloadDoesNotReadPastTheModel()
    {
        var model = MdlBinaryReader.Read(ReferenceModel("fx_ref", 1)[..(12 + 512 + 0x70)]);
        Assert.Equal(2, model.Parts.Count);
        Assert.Null(model.Parts[1].Reference);
    }

    [DataFact]
    public void CleanStockColdBeamReferencesTheObservedEndpointModel()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var models = new ModelCache(resources);
        var beam = models.Get("vim_raycold")!;
        var endpoints = beam.Parts.Where(p => p.Reference?.Model == "fx_ref_cold").ToArray();
        Assert.Equal(2, endpoints.Length);
        Assert.All(endpoints, endpoint =>
        {
            Assert.True(endpoint.Reference!.Reattachable);
            Assert.True(beam.Parts[endpoint.Parent].Emitter!.Is("Lightning"));
        });
        Assert.Contains(models.Get("fx_ref_cold")!.Parts, p => p.Emitter != null);
        Assert.DoesNotContain(models.Get("fx_ref")!.Parts, p => p.Emitter != null || p.Mesh != null);
    }

    static byte[] ReferenceModel(string name, int reattachable)
    {
        // Wholly authored binary fixture: root, child pointer and Reference Node payload.
        byte[] data = new byte[1024];
        void U(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12 + offset), value);
        void S(int offset, string value) => Encoding.ASCII.GetBytes(value).CopyTo(data, 12 + offset);
        S(8, "test"); U(0x48, 256);
        S(256 + 0x20, "root"); U(256 + 0x6C, 1);
        U(256 + 0x48, 400); U(256 + 0x4C, 1); U(400, 512);
        S(512 + 0x20, "endpoint"); U(512 + 0x6C, 0x11);
        S(512 + 0x70, name); U(512 + 0xB0, reattachable);
        return data;
    }
}
