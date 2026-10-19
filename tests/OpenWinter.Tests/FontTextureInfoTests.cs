using System.Text;
using OpenWinter.Aurora.Textures;
using Xunit;

namespace OpenWinter.Tests;

public class FontTextureInfoTests
{
    [Fact]
    public void FontStyleReadsMixedCaseAndWhitespace()
    {
        var info = TextureDecoder.ParseTxi(Encoding.ASCII.GetBytes("TTFContrast\t1.3\r\nttfOutlineOpacity 0.35\nttfOutlineBrightness 0.1\n"));
        Assert.Equal(1.3f, info.TtfContrast);
        Assert.Equal(0.35f, info.TtfOutlineOpacity);
        Assert.Equal(0.1f, info.TtfOutlineBrightness);
    }

    [Theory]
    [InlineData("NaN", "Infinity", "bad", 1, 0.35f, 0)]
    [InlineData("2", "4", "-1", 2, 1, 0)]
    public void MalformedFontStylesStayFiniteAndBounded(string contrast, string opacity, string brightness, float c, float o, float b)
    {
        var info = TextureDecoder.ParseTxi(Encoding.ASCII.GetBytes($"ttfContrast {contrast}\nttfOutlineOpacity {opacity}\nttfOutlineBrightness {brightness}"));
        Assert.Equal(c, info.TtfContrast);
        Assert.Equal(o, info.TtfOutlineOpacity);
        Assert.Equal(b, info.TtfOutlineBrightness);
    }
}
