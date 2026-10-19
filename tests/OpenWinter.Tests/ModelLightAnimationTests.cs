using System.Numerics;
using System.Text;
using OpenWinter.Aurora.Models;
using Xunit;

namespace OpenWinter.Tests;

public class ModelLightAnimationTests
{
    [Fact]
    public void TextLightKeysAndConstantValuesAreRetained()
    {
        var model = MdlAsciiReader.Read(Encoding.ASCII.GetBytes("""
            newmodel test
            beginmodelgeom test
            node light lamp
              parent NULL
              radius 2
            endnode
            endmodelgeom test
            newanim pulse test
              length 2
              node light lamp
                parent NULL
                radiuskey 3
                  0 2
                  1 8
                  2 2
                colorkey 2
                  0 1 0 0
                  2 0 0 1
                multiplierkey 2
                  0 1
                  2 0
              endnode
            doneanim pulse test
            newanim fixed test
              length 1
              node light lamp
                parent NULL
                radius 4
                color 0 1 0
                multiplier 0.5
              endnode
            doneanim fixed test
            donemodel test
            """));
        var pulse = model.Animations["pulse"].Tracks["lamp"];
        Assert.Equal(new float[] { 0, 1, 2 }, pulse.LightRadiusTimes);
        Assert.Equal(new float[] { 2, 8, 2 }, pulse.LightRadii);
        Assert.Equal(new[] { Vector3.UnitX, Vector3.UnitZ }, pulse.LightColors);
        Assert.Equal(new float[] { 0, 2 }, pulse.LightColorTimes);
        Assert.Equal(new float[] { 1, 0 }, pulse.LightMultipliers);
        var fixedLight = model.Animations["fixed"].Tracks["lamp"];
        Assert.Equal(new float[] { 4 }, fixedLight.LightRadii);
        Assert.Equal(new[] { Vector3.UnitY }, fixedLight.LightColors);
        Assert.Equal(new float[] { .5f }, fixedLight.LightMultipliers);
        Assert.Equal(new float[] { 0 }, fixedLight.LightTimes);
    }
}
