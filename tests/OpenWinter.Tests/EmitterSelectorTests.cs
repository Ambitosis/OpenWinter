using System.Text;
using OpenWinter.Aurora.Models;
using Xunit;

namespace OpenWinter.Tests;

public class EmitterSelectorTests
{
    [Theory]
    [InlineData("p2p_sel 0", false)]
    [InlineData("p2p_sel 1", true)]
    [InlineData("p2p_sel 2", false)]
    [InlineData("p2p_sel 1\np2p_type Gravity", true)]
    [InlineData("p2p_sel 0\np2p_type Bezier", false)]
    public void ExplicitSelectorsMatchNativeControls(string fields, bool bezier)
    {
        Assert.Equal(bezier, Read(fields));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("p2p_type Gravity", false)]
    [InlineData("p2p_type Bezier", true)]
    [InlineData("p2p_sel 3", true)]
    [InlineData("p2p_sel 3\np2p_type Gravity", false)]
    [InlineData("p2p_sel malformed\np2p_type Gravity", false)]
    public void UnmeasuredMissingAndUnknownSelectorsKeepExistingFallback(string fields, bool bezier)
    {
        Assert.Equal(bezier, Read(fields));
    }

    static bool Read(string fields)
    {
        var model = MdlAsciiReader.Read(Encoding.UTF8.GetBytes($"""
            newmodel control
            beginmodelgeom control
            node emitter stream
              parent NULL
              update Fountain
              render Normal
              p2p 1
              {fields}
            endnode
            endmodelgeom control
            donemodel control
            """));
        var emitter = Assert.Single(model.Parts).Emitter!;
        Assert.True(emitter.P2P);
        return emitter.P2PBezier;
    }
}
