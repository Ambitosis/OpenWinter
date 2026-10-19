using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Preferences;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ControlsTests
{
    /// <summary>The original game's default keys (keymap.2da, decoded as docs/CONTROLS.md explains).</summary>
    [Theory]
    [InlineData(ActionCatalog.CameraLeft, "Left")]
    [InlineData(ActionCatalog.CameraRight, "Right")]
    [InlineData(ActionCatalog.ZoomIn, "Up")]
    [InlineData(ActionCatalog.ZoomOut, "Down")]
    [InlineData(ActionCatalog.CameraUp, "Pageup")]
    [InlineData(ActionCatalog.CameraDown, "Pagedown")]
    [InlineData(ActionCatalog.ZoomInFull, "Home")]
    [InlineData(ActionCatalog.ZoomOutFull, "End")]
    [InlineData(ActionCatalog.CameraUpFull, "Insert")]
    [InlineData(ActionCatalog.CameraDownFull, "Delete")]
    [InlineData(ActionCatalog.CameraMode, "KpMultiply")]
    [InlineData(ActionCatalog.RadialSelf, "Kp0")]
    [InlineData(ActionCatalog.RadialBack, "Kp5")]
    [InlineData("radial_sw", "Kp1")]
    [InlineData("radial_s", "Kp2")]
    [InlineData("radial_se", "Kp3")]
    [InlineData("radial_w", "Kp4")]
    [InlineData("radial_e", "Kp6")]
    [InlineData("radial_nw", "Kp7")]
    [InlineData("radial_n", "Kp8")]
    [InlineData("radial_ne", "Kp9")]
    [InlineData(ActionCatalog.ToggleGui, "H")]
    [InlineData(ActionCatalog.Inventory, "I")]
    [InlineData(ActionCatalog.QuickChat, "V")]
    [InlineData(ActionCatalog.Journal, "J")]
    [InlineData(ActionCatalog.Character, "C")]
    [InlineData(ActionCatalog.Spellbook, "B")]
    [InlineData(ActionCatalog.Map, "M")]
    [InlineData(ActionCatalog.PlayerList, "P")]
    [InlineData(ActionCatalog.Options, "O")]
    [InlineData(ActionCatalog.QuickSave, "G")]
    [InlineData(ActionCatalog.MoveLeft, "Q")]
    [InlineData(ActionCatalog.MoveRight, "E")]
    [InlineData(ActionCatalog.MoveForward, "W")]
    [InlineData(ActionCatalog.MoveBack, "S")]
    [InlineData(ActionCatalog.TurnLeft, "A")]
    [InlineData(ActionCatalog.TurnRight, "D")]
    [InlineData(ActionCatalog.Screenshot, "Print")]
    [InlineData(ActionCatalog.Pause, "Space")]
    [InlineData(ActionCatalog.HighlightObjects, "Tab")]
    [InlineData(ActionCatalog.InventoryPrevious, "Bracketleft")]
    [InlineData(ActionCatalog.InventoryNext, "Bracketright")]
    [InlineData(ActionCatalog.Rest, "R")]
    public void DefaultsAreTheOriginalKeys(string action, string key)
    {
        var def = ActionCatalog.Find(action);
        Assert.NotNull(def);
        Assert.Equal(new KeyBind(key), def!.Defaults[0]);
    }

    [Fact]
    public void ActionsWithoutAnOriginalKeyStartUnbound()
    {
        var set = new BindingSet(new ControlSettings());
        for (int i = 0; i < ActionCatalog.QuickGroups; i++)
        {
            Assert.Empty(set.Get(ActionCatalog.QuickGroupSet(i)));
            Assert.Empty(set.Get(ActionCatalog.QuickGroupPick(i)));
        }
        Assert.Empty(set.Get(ActionCatalog.ToggleKeyhole));
        // Our own keys gave way to the original's: G is quick save, H hides the interface, O is Options.
        Assert.Empty(set.Get(ActionCatalog.Gesture));
        Assert.Empty(set.Get(ActionCatalog.Stealth));
        Assert.Empty(set.Get(ActionCatalog.SaveLoad));
        // Ctrl + F-keys are the third quickbar.
        Assert.DoesNotContain(ActionCatalog.All.SelectMany(a => a.Defaults), b => b.Ctrl && b.Key.StartsWith('F') && b.Key.Length <= 3);
    }

    [Fact]
    public void EveryActionAppearsOnceInOptions()
    {
        var ids = ActionCatalog.All.Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Contains("Radial menu", ActionCatalog.Categories);
        Assert.Contains("Quick groups", ActionCatalog.Categories);
        Assert.Equal(8, ActionCatalog.RadialKeys.Length);
        Assert.All(ActionCatalog.All, a => Assert.False(string.IsNullOrWhiteSpace(a.Label)));
    }

    [Theory]
    [InlineData(8, 8, RadialDirection.N, 0)]
    [InlineData(8, 8, RadialDirection.E, 2)]
    [InlineData(8, 8, RadialDirection.SW, 5)]
    [InlineData(8, 8, RadialDirection.NW, 7)]
    [InlineData(6, 6, RadialDirection.E, 2)]       // 90° on a 60° ring: halfway, ties go to the even wedge (120°)
    [InlineData(6, 6, RadialDirection.W, 4)]       // ... so left and right mirror each other (240°)
    [InlineData(6, 6, RadialDirection.S, 3)]
    [InlineData(6, 6, RadialDirection.NW, 5)]
    [InlineData(7, 7, RadialDirection.NW, 6)]      // 315° on a 7-wedge ring: the wedge at 309°
    [InlineData(10, 10, RadialDirection.SE, 4)]    // 135° on a 36° ring: 3.75 → 4
    [InlineData(6, 3, RadialDirection.S, -1)]      // that wedge is drawn but empty
    [InlineData(6, 0, RadialDirection.N, -1)]
    public void NumpadPicksTheNearestWedge(int wedges, int used, RadialDirection d, int expected) =>
        Assert.Equal(expected, RadialNav.Wedge(wedges, used, d));

    [Fact]
    public void CameraModeCyclesLikeTheTutorial()
    {
        Assert.Equal(CameraMode.Chase, CameraModes.Next(CameraMode.TopDown));
        Assert.Equal(CameraMode.Driving, CameraModes.Next(CameraMode.Chase));
        Assert.Equal(CameraMode.TopDown, CameraModes.Next(CameraMode.Driving));
        Assert.Equal(CameraMode.TopDown, CameraModes.FromInt(new GameSettings().Gameplay.CameraMode));
        Assert.Equal(CameraMode.TopDown, CameraModes.FromInt(7));
        Assert.Equal(1, GameSettings.FromJson("""{ "gameplay": { "cameraMode": 9 } }""").Gameplay.CameraMode);
        Assert.Equal(2, GameSettings.FromJson("""{ "gameplay": { "cameraMode": 2 } }""").Gameplay.CameraMode);
    }

    [Fact]
    public void FollowTurnsTheShortWay()
    {
        float pi = MathF.PI;
        // From just below +π to just above -π is a small step across the seam, not a full turn back.
        float r = CameraModes.TurnToward(pi - 0.1f, -pi + 0.1f, 1f);
        Assert.Equal(pi + 0.1f, r, 3);
        Assert.Equal(0.5f, CameraModes.TurnToward(0, 2f, 0.5f), 3);
        Assert.Equal(-0.5f, CameraModes.TurnToward(0, -2f, 0.5f), 3);
        Assert.Equal(1f, CameraModes.TurnToward(0.9f, 1f, 0.5f), 3);
    }

    [Fact]
    public void DoubleTapLatchesTheTooltipKey()
    {
        var t = new TapLatch();
        t.Press(1.0);
        Assert.False(t.Latched);
        Assert.True(t.Active(held: true));
        Assert.False(t.Active(held: false));
        t.Press(1.15);                       // second tap within 200 ms
        Assert.True(t.Latched);
        Assert.True(t.Active(held: false));
        t.Press(3.0);                        // the next press lets go
        Assert.False(t.Latched);
        t.Press(3.5);                        // too slow to latch
        Assert.False(t.Latched);
    }

    [Fact]
    public void QuickGroupsRememberAndForgetObjects()
    {
        var g = new QuickGroups();
        Assert.Equal(4, g.Count);
        g.Set(0, [5u, 0u, 5u, 9u]);
        Assert.Equal([5u, 9u], g.Get(0));
        Assert.Equal([9u], g.Pick(0, id => id != 5));
        Assert.Equal([9u], g.Get(0));
        Assert.Empty(g.Pick(3, _ => true));
        Assert.Empty(g.Pick(7, _ => true));
    }
}
