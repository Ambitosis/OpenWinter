using OpenWinter.Client.Preferences;
using Xunit;

namespace OpenWinter.Tests;

public sealed class SettingsTests
{
    [Fact]
    public void ClassicInterfaceIsTheDefaultAndASavedChoiceIsKept()
    {
        Assert.True(new GameSettings().Interface.Classic);
        var modern = new GameSettings();
        modern.Interface.Style = "modern";
        Assert.False(GameSettings.FromJson(modern.ToJson()).Interface.Classic);
        Assert.True(GameSettings.FromJson("{}").Interface.Classic);
    }

    [Fact]
    public void DefaultsRoundTripThroughJson()
    {
        var s = new GameSettings();
        s.Audio.Music = 0.25f;
        s.Audio.SpatialBias = 1.7f;
        s.Graphics.WindowMode = WindowMode.Borderless;
        s.Interface.TextBubbleMode = 2;
        s.Profile.Remember("play.example.org:5121", new DateTime(2026, 10, 8));
        var back = GameSettings.FromJson(s.ToJson());
        Assert.Equal(0.25f, back.Audio.Music);
        Assert.Equal(1.7f, back.Audio.SpatialBias);
        Assert.Equal(WindowMode.Borderless, back.Graphics.WindowMode);
        Assert.Equal(2, back.Interface.TextBubbleMode);
        Assert.Equal("play.example.org:5121", back.Profile.RecentServers.Single().Address);
        Assert.Contains("\"Borderless\"", s.ToJson());
    }

    [Fact]
    public void BrokenOrOutOfRangeInputIsRepaired()
    {
        Assert.Equal(1f, GameSettings.FromJson("{ not json").Graphics.RenderScale);
        var s = GameSettings.FromJson("""{ "graphics": { "renderScale": 9, "gamma": 0.1, "width": 12 }, "audio": { "master": -3 }, "interface": { "uiScale": 7, "chatFontSize": 2 }, "controls": { "bindings": { "nonsense": [ { "key": "A" } ] } } }""");
        Assert.Equal(2f, s.Graphics.RenderScale);
        Assert.Equal(1.6f, s.Graphics.Gamma);
        Assert.Equal(0, s.Graphics.Width);
        Assert.Equal(0f, s.Audio.Master);
        Assert.Equal(2f, s.Interface.UiScale);
        Assert.Equal(11, s.Interface.ChatFontSize);
        Assert.Empty(s.Controls.Bindings);
        Assert.Equal(1, s.Interface.TextBubbleMode); // old settings retain text-only labels
        Assert.Equal(2, GameSettings.FromJson("""{ "interface": { "textBubbleMode": 99 } }""").Interface.TextBubbleMode);
        Assert.Equal(0, GameSettings.FromJson("""{ "interface": { "textBubbleMode": 0 } }""").Interface.TextBubbleMode);
    }

    [Theory]
    [InlineData(0.1f, 1f, 0.1f)]
    [InlineData(1f, 1f, 1f)]
    [InlineData(1.9f, 0.1f, 1f)]
    public void SpatialBiasMatchesNativeSoundObjectMeasurements(float bias, float flat, float spatial)
    {
        var audio = new AudioSettings { SpatialBias = bias };
        Assert.Equal(flat, audio.SpatialGain(false), 5);
        Assert.Equal(spatial, audio.SpatialGain(true), 5);
        Assert.Equal(0.1f, GameSettings.FromJson("""{ "audio": { "spatialBias": -10 } }""").Audio.SpatialBias);
        Assert.Equal(1.9f, GameSettings.FromJson("""{ "audio": { "spatialBias": 10 } }""").Audio.SpatialBias);
    }

    [Theory]
    [InlineData(720, 1f)]
    [InlineData(900, 1f)]
    [InlineData(1080, 1f)]
    [InlineData(1440, 1.25f)]
    [InlineData(1600, 1.5f)]
    [InlineData(2160, 2f)]
    [InlineData(4320, 2f)]
    public void AutomaticUiScaleFollowsTheWindowHeight(int height, float scale) => Assert.Equal(scale, InterfaceSettings.AutoScale(height));

    /// <summary>The original's classic interface scale (partyframes scene; docs/CLASSIC_UI.md, "Interface scale").</summary>
    [Theory]
    [InlineData(1280, 720, 1.5f, 1f)]
    [InlineData(1600, 900, 1.5f, 1.2f)]
    [InlineData(1600, 900, 1.25f, 1.2f)]
    [InlineData(1600, 900, 1.1f, 1.1f)]
    [InlineData(1440, 960, 2f, 1.3f)]
    [InlineData(1024, 900, 1.5f, 1.1f)]
    [InlineData(1080, 900, 1.5f, 1.2f)]
    [InlineData(1000, 900, 1.5f, 1.1f)]
    [InlineData(968, 900, 1.5f, 1f)]
    [InlineData(960, 900, 1.5f, 1f)]
    [InlineData(1600, 790, 1.5f, 1.1f)]
    [InlineData(1600, 760, 1.5f, 1f)]
    public void ClassicInterfaceScaleIsCappedByTheWindow(int width, int height, float setting, float drawn)
    {
        var settings = new InterfaceSettings { UiScale = setting };
        Assert.Equal(drawn, settings.ClassicScale(width, height), 3);
    }

    [Fact]
    public void DefaultBindingsHaveNoConflicts()
    {
        var set = new BindingSet(new ControlSettings());
        Assert.Empty(set.Conflicts());
        Assert.Equal(new KeyBind("W"), set.Get(ActionCatalog.MoveForward, 0));
        Assert.Equal(new KeyBind("G"), set.Get(ActionCatalog.QuickSave, 0));
        Assert.Equal(12, ActionCatalog.All.Count(a => a.Category == "Quickbar"));
    }

    [Fact]
    public void RebindingTakesTheKeyFromItsOldAction()
    {
        var controls = new ControlSettings();
        var set = new BindingSet(controls);
        var displaced = set.Assign(ActionCatalog.Attack, 0, new KeyBind("I"));
        Assert.Equal([ActionCatalog.Inventory], displaced);
        Assert.Equal(new KeyBind("I"), set.Get(ActionCatalog.Attack, 0));
        Assert.Empty(set.Get(ActionCatalog.Inventory));
        Assert.Empty(set.Conflicts());
        // Changed actions persist; restoring a default removes the override.
        Assert.True(controls.Bindings.ContainsKey(ActionCatalog.Inventory));
        set.Assign(ActionCatalog.Inventory, 0, new KeyBind("I"));
        Assert.Equal(new KeyBind("I"), set.Get(ActionCatalog.Inventory, 0));
        Assert.False(controls.Bindings.ContainsKey(ActionCatalog.Inventory));
        set.ResetAll();
        Assert.Empty(controls.Bindings);
        Assert.Equal(new KeyBind("F"), set.Get(ActionCatalog.Attack, 0));
    }

    [Fact]
    public void ModifiersMakeDifferentKeysAndFixedKeysCannotBeTaken()
    {
        var set = new BindingSet(new ControlSettings());
        // F6 (quickbar slot 6) and Shift+F6 are different keys.
        Assert.Empty(set.Assign(ActionCatalog.Rest, 1, new KeyBind("F6", shift: true))!);
        Assert.Null(set.Assign(ActionCatalog.Rest, 0, new KeyBind("Escape")));
        Assert.Null(set.Assign(ActionCatalog.GameMenu, 0, new KeyBind("P")));
        Assert.Equal(new KeyBind("R"), set.Get(ActionCatalog.Rest, 0));
        Assert.Equal("Shift+F6", set.Get(ActionCatalog.Rest, 1)!.Display());
        // Clearing the primary slot moves the alternate up.
        set.Assign(ActionCatalog.Rest, 0, null);
        Assert.Equal(new KeyBind("F6", shift: true), set.Get(ActionCatalog.Rest, 0));
    }

    [Fact]
    public void CustomBindingsSurviveJson()
    {
        var s = new GameSettings();
        new BindingSet(s.Controls).Assign(ActionCatalog.Map, 1, new KeyBind("Tab", ctrl: true));
        var back = GameSettings.FromJson(s.ToJson());
        Assert.Equal(new KeyBind("Tab", ctrl: true), new BindingSet(back.Controls).Get(ActionCatalog.Map, 1));
    }

    [Fact]
    public void KeyNamesReadWell()
    {
        Assert.Equal("Esc", new KeyBind("Escape").Display());
        Assert.Equal("Ctrl+F5", new KeyBind("F5", ctrl: true).Display());
        Assert.Equal("1", new KeyBind("Key1").Display());
        Assert.Equal("Num Enter", new KeyBind("KpEnter").Display());
        Assert.Equal("Num *", new KeyBind("KpMultiply").Display());
        Assert.Equal("Num 5", new KeyBind("Kp5").Display());
        Assert.Equal("Page Up", new KeyBind("Pageup").Display());
        Assert.Equal("Mouse 4", new KeyBind("Mouse8").Display());
    }
}
