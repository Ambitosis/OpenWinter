using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using OpenWinter.Aurora.Models;
using Xunit;

namespace OpenWinter.Tests;

public class ModelScaleTests
{
    [Fact]
    public void TextScaleKeysAndConstantScaleAreRetained()
    {
        var model = MdlAsciiReader.Read(Encoding.ASCII.GetBytes("""
            newmodel test
            beginmodelgeom test
            node dummy root
              parent NULL
              position 10 0 0
            endnode
            node dummy child
              parent root
              position 3 0 0
            endnode
            endmodelgeom test
            newanim grow test
              length 2
              node dummy root
                parent NULL
                scalekey 3
                  0 0
                  1 0.5
                  2 2
              endnode
              node dummy child
                parent root
                scale 3
              endnode
            doneanim grow test
            donemodel test
            """));
        Assert.Equal(new Vector3(13, 0, 0), Vector3.Transform(Vector3.Zero, model.RestGlobals()[1]));
        Assert.Equal(new float[] { 0, 1, 2 }, model.Animations["grow"].Tracks["root"].ScaleTimes);
        Assert.Equal(new float[] { 0, .5f, 2 }, model.Animations["grow"].Tracks["root"].Scales);
        Assert.Equal(new float[] { 3 }, model.Animations["grow"].Tracks["child"].Scales);
    }

    [Fact]
    public void BinaryScaleOnlyNodeCreatesAnimationTrack()
    {
        // Wholly authored minimal MDL: model/root, one animation/root, scalar controller 36.
        byte[] data = new byte[1600];
        void U(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(12 + offset), value);
        void F(int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(12 + offset), value);
        void S(int offset, string value) => Encoding.ASCII.GetBytes(value).CopyTo(data, 12 + offset);
        void Controller(int node, int keys, int values, float[] times, float[] scales)
        {
            U(node + 0x54, keys); U(node + 0x58, 1);
            U(node + 0x60, values); U(node + 0x64, times.Length * 2);
            U(keys, 36);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + keys + 4), (short)times.Length);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12 + keys + 8), (short)times.Length);
            data[12 + keys + 10] = 1;
            for (int i = 0; i < times.Length; i++) { F(values + i * 4, times[i]); F(values + (times.Length + i) * 4, scales[i]); }
        }
        S(8, "test"); U(0x48, 256); F(0xA4, 1); S(256 + 0x20, "root");
        Controller(256, 1100, 1150, [0], [2]);
        U(0x78, 400); U(0x7C, 1); U(400, 512);
        S(512 + 8, "grow"); F(512 + 0x70, 2); U(512 + 0x48, 768); S(768 + 0x20, "root");
        Controller(768, 1200, 1250, [0, 2], [.25f, 1.5f]);
        var model = MdlBinaryReader.Read(data);
        var track = model.Animations["grow"].Tracks["root"];
        Assert.Empty(track.Positions);
        Assert.Equal(new float[] { 0, 2 }, track.ScaleTimes);
        Assert.Equal(new float[] { .25f, 1.5f }, track.Scales);
    }
}
