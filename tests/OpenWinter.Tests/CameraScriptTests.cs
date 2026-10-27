using OpenWinter.Client.Gameplay;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>How the client reads the camera script functions' notifies.</summary>
public class CameraScriptTests
{
    const uint Player = 0x7FFFFFFF, Invalid = 0x7F000000, Actor = 0xA;

    [Fact]
    public void ADirectionOfMinusOneKeepsTheCamerasAndOtherNegativesTurnTheOtherWay()
    {
        Assert.Null(CameraScript.Direction(-1f));
        // Visual scene camglide3: SetCameraFacing(-90.0) faces as 270 does.
        Assert.Equal(270f, CameraScript.Direction(-90f));
        Assert.Equal(90f, CameraScript.Direction(450f));
        Assert.Equal(0f, CameraScript.Direction(0f));
    }

    [Fact]
    public void DistanceLimitsFallBackToTheClientsAndNeverCross()
    {
        Assert.Equal((2.5f, 30f), CameraScript.DistanceLimits(-1, -1, 2.5f, 30f));
        Assert.Equal((4f, 15f), CameraScript.DistanceLimits(4, 15, 2.5f, 30f));
        Assert.Equal((10f, 20f), CameraScript.DistanceLimits(20, 10, 2.5f, 30f));
    }

    [Fact]
    public void AnAttachedCameraWaitsForItsObjectAndFindsItAgain()
    {
        var attach = new CameraAttachment();
        var shown = new HashSet<uint>();
        // AttachCamera to an object the same script just created: not shown yet, so nothing happens until it is.
        attach.Request(Actor, Player, Invalid, clearView: true);
        Assert.Null(attach.Due(shown.Contains, Player));
        shown.Add(Actor);
        Assert.Equal((Actor, true), attach.Due(shown.Contains, Player));
        // Already there: nothing to do.
        Assert.Null(attach.Due(shown.Contains, Actor));
        // Out of sight and back: the camera goes back to it, without looking for a clear view again.
        shown.Remove(Actor);
        Assert.Null(attach.Due(shown.Contains, Player));
        shown.Add(Actor);
        Assert.Equal((Actor, false), attach.Due(shown.Contains, Player));
        // Handed back to the player (by the player's own id or none).
        attach.Request(Player, Player, Invalid, clearView: false);
        Assert.Equal(0u, attach.Target);
        Assert.Null(attach.Due(shown.Contains, Player));
        attach.Request(Actor, Player, Invalid, clearView: false);
        attach.Request(Invalid, Player, Invalid, clearView: false);
        Assert.Equal(0u, attach.Target);
    }
}
