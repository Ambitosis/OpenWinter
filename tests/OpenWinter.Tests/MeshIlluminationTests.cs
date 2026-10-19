using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using OpenWinter.Aurora.Models;
using Xunit;

namespace OpenWinter.Tests;

public class MeshIlluminationTests
{
    [Fact]
    public void TextMeshKeepsSelfIlluminationSeparateFromDiffuseAndClone()
    {
        const string source = """
            newmodel test
            beginmodelgeom test
            node trimesh triangle
              parent NULL
              diffuse 0.2 0.3 0.4
              selfillumcolor 0.125 0.25 0.5
              verts 3
                0 0 0
                1 0 0
                0 1 0
              faces 1
                0 1 2 0 0 1 2 0
            endnode
            endmodelgeom test
            donemodel test
            """;
        var mesh = Assert.Single(MdlAsciiReader.Read(Encoding.UTF8.GetBytes(source)).Parts).Mesh!;
        Assert.Equal(new Vector3(.125f, .25f, .5f), mesh.SelfIllumination);
        Assert.Equal(new Vector3(.2f, .3f, .4f), mesh.Diffuse);
        Assert.Equal(mesh.SelfIllumination, mesh.Clone().SelfIllumination);
        var absent = MdlAsciiReader.Read(Encoding.UTF8.GetBytes(source.Replace("selfillumcolor 0.125 0.25 0.5", "")));
        Assert.Equal(Vector3.Zero, Assert.Single(absent.Parts).Mesh!.SelfIllumination);
    }

    [Fact]
    public void BinaryMeshReadsSelfIlluminationController100()
    {
        // A wholly authored minimal binary model: one empty mesh with a single
        // three-component controller. Offsets are from the existing MDL reader.
        var bytes = new byte[4096];
        void UInt(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12 + offset), value);
        void Float(int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12 + offset), value);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 3072);
        UInt(0x48, 0x200);
        Float(0xA4, 1);
        UInt(0x200 + 0x6C, 0x20);
        UInt(0x200 + 0x54, 0x500);
        UInt(0x200 + 0x58, 1);
        UInt(0x200 + 0x60, 0x540);
        UInt(0x200 + 0x64, 4);
        UInt(0x500, 100);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + 0x504), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12 + 0x508), 1);
        bytes[12 + 0x50A] = 3;
        Float(0x544, .125f);
        Float(0x548, .25f);
        Float(0x54C, .5f);
        var mesh = Assert.Single(MdlBinaryReader.Read(bytes).Parts).Mesh!;
        Assert.Equal(new Vector3(.125f, .25f, .5f), mesh.SelfIllumination);
        UInt(0x200 + 0x58, 0);
        Assert.Equal(Vector3.Zero, Assert.Single(MdlBinaryReader.Read(bytes).Parts).Mesh!.SelfIllumination);
    }
}
