using OpenWinter.Aurora.Textures;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Armour icon composition as measured on nwmain (scenes armouricons*; docs/CLASSIC_UI.md "Armour icons").</summary>
public class ArmourIconTests
{
    // A chain shirt (nw_aarcl004): pelvis 6, chest 32, belt 10, shoulders 19, colours leather 10/10, cloth 11/10, metal 1/10.
    static readonly Dictionary<int, int> ChainShirt = new() { [6] = 6, [7] = 32, [8] = 10, [14] = 19, [15] = 19 };
    static readonly int[] Colours = { 10, 10, 11, 10, 1, 10 };

    static string[] Layers(bool female, Dictionary<int, int> parts, Dictionary<int, int>? overrides = null,
        Func<string, bool>? hasIcon = null, params string[] hides) =>
        ArmourIcon.Layers(female,
            part => parts.GetValueOrDefault(part),
            index => index < 6 ? Colours[index] : overrides?.GetValueOrDefault(index, 255) ?? 255,
            hasIcon ?? (_ => true),
            column => hides.Contains(column));

    static string Resref(string layer) => layer.Split(':')[1];
    static byte[] Rows(string layer) => Convert.FromHexString(layer.Split(':')[2]);

    [Fact]
    public void PartsStackPelvisChestBeltShouldersForThePlayersGender()
    {
        Assert.Equal(new[] { "ipf_pelvis006", "ipf_chest032", "ipf_belt010", "ipf_shol019", "ipf_shor019" },
            Layers(true, ChainShirt).Select(Resref));
        Assert.All(Layers(false, ChainShirt), l => Assert.StartsWith("iconplt:ipm_", l));
    }

    [Fact]
    public void ItemColoursGoToTheirPaletteLayers()
    {
        // Layers: skin, hair, metal 1, metal 2, cloth 1, cloth 2, leather 1, leather 2, tattoo 1, tattoo 2.
        Assert.Equal(new byte[] { 0, 0, 1, 10, 11, 10, 10, 10, 0, 0 }, Rows(Layers(false, ChainShirt)[1]));
    }

    [Fact]
    public void ShoulderIconsTakeTheOtherShouldersOverridesAndTheRobeNone()
    {
        var parts = new Dictionary<int, int>(ChainShirt) { [15] = 3, [14] = 22 };
        int Metal1(int part) => 6 + part * 6 + 4;
        var overrides = new Dictionary<int, int>
        {
            [Metal1(6)] = 118, [Metal1(7)] = 121, [Metal1(8)] = 124, [Metal1(14)] = 142, [Metal1(15)] = 145, [Metal1(18)] = 154,
        };
        var layers = Layers(false, parts, overrides);
        Assert.Equal(new[] { "ipm_pelvis006", "ipm_chest032", "ipm_belt010", "ipm_shol003", "ipm_shor022" }, layers.Select(Resref));
        Assert.Equal(new byte[] { 118, 121, 124, 142, 145 }, layers.Select(l => Rows(l)[2]));
        var robed = Layers(false, new Dictionary<int, int>(ChainShirt) { [18] = 5 }, overrides);
        Assert.Equal(1, Rows(robed[^1])[2]);
    }

    [Fact]
    public void ARobeHidesTheParts_robeRowHidesAndGoesOnTop()
    {
        var parts = new Dictionary<int, int>(ChainShirt) { [18] = 30 };
        Assert.Equal(new[] { "ipm_chest032", "ipm_shol019", "ipm_shor019", "ipm_robe030" },
            Layers(false, parts, hides: ["HIDEPELVIS", "HIDEBELT"]).Select(Resref));
        Assert.Equal(new[] { "ipm_robe003" },
            Layers(false, new Dictionary<int, int>(ChainShirt) { [18] = 3 }, hides: ["HIDEPELVIS", "HIDECHEST", "HIDEBELT", "HIDESHOL", "HIDESHOR"]).Select(Resref));
    }

    [Fact]
    public void PartsWithoutAModelOrAnIconAreLeftOut()
    {
        var parts = new Dictionary<int, int>(ChainShirt) { [7] = 58, [8] = 0 };
        Assert.Equal(new[] { "ipm_pelvis006", "ipm_shol019", "ipm_shor019" },
            Layers(false, parts, hasIcon: r => r != "ipm_chest058").Select(Resref));
        Assert.Empty(Layers(false, ChainShirt, hasIcon: _ => false));
    }

    [DataFact]
    public void InstalledRobesHideTheirRowsPartsAndCloaksUseTheirMaleIcons()
    {
        var world = new World(TestData.Module);
        var armor = world.Items.FromBlueprint("nw_aarcl004")!;
        world.Inventory.SetAppearance(armor, InventorySystem.ApprArmorModel, ArmourIcon.Robe, 30);
        Assert.Equal(new[] { "ipf_chest032", "ipf_shol019", "ipf_shor019", "ipf_robe030" },
            world.Inventory.Icon(armor, new Creature { Gender = 1 }).Select(Resref));
        var cloak = world.Items.FromBlueprint("x2_it_mcloak001")!;
        Assert.StartsWith("iconplt:icloak_m_003:", Assert.Single(world.Inventory.Icon(cloak, new Creature { Gender = 1 })));
    }

    /// <summary>
    /// A hak's armour art, as the original reads it (scene armouriconshak): part icons at numbers beyond the base
    /// game's (300, and 1000 looked up as ip?_chest1000), a parts_robe.2da row of its own and a new base item with
    /// ModelType 3.
    /// </summary>
    [DataFact]
    public void HakPartIconsRobeRowsAndBaseItemsBuildTheIcon()
    {
        var resources = new OpenWinter.Aurora.Resources.ResMan(TestData.Paths);
        resources.AddBaseGame();
        var hak = new OpenWinter.Game.Persistence.MemorySource("hak");
        void Copy(string name, string from) => hak.Set(OpenWinter.Aurora.Resources.ResKey.Of(name, OpenWinter.Aurora.Resources.ResType.Plt),
            resources.Read(from, OpenWinter.Aurora.Resources.ResType.Plt)!);
        Copy("ipf_chest300", "ipf_chest186");
        Copy("ipf_chest1000", "ipf_chest016");
        Copy("ipf_robe050", "ipf_robe030");
        Table("parts_chest", 1000, 186);
        Table("parts_robe", 50, 38, ("HIDEPELVIS", "0"), ("HIDECHEST", "1"), ("HIDEBELT", "0"), ("HIDESHOL", "0"), ("HIDESHOR", "0"));
        Table("baseitems", 200, 16, ("label", "ArmourProbe"));
        resources.AddFirst(hak);
        var world = new World(resources);
        var viewer = new Creature { Gender = 1 };
        string[] Icon(int part, int model, int baseItem = 16)
        {
            var armor = world.Items.FromBlueprint("nw_aarcl004")!;
            armor.BaseItem = baseItem;
            if (part >= 0) Assert.True(world.Inventory.SetAppearance(armor, InventorySystem.ApprArmorModel, part, model));
            return world.Inventory.Icon(armor, viewer).Select(Resref).ToArray();
        }
        Assert.Contains("ipf_chest300", Icon(ArmourIcon.Chest, 300));
        Assert.Contains("ipf_chest1000", Icon(ArmourIcon.Chest, 1000));
        Assert.Equal(new[] { "ipf_pelvis006", "ipf_belt010", "ipf_shol019", "ipf_shor019", "ipf_robe050" }, Icon(ArmourIcon.Robe, 50));
        Assert.Equal(new[] { "ipf_pelvis006", "ipf_chest032", "ipf_belt010", "ipf_shol019", "ipf_shor019" }, Icon(-1, 0, 200));

        void Table(string name, int row, int source, params (string Column, string Value)[] values)
        {
            var table = OpenWinter.Aurora.Formats.TwoDA.Parse(resources.Read(name, OpenWinter.Aurora.Resources.ResType.TwoDA)!);
            var text = new System.Text.StringBuilder("2DA V2.0\n\n").AppendLine(string.Join(" ", table.Columns));
            for (int index = 0; index < Math.Max(table.RowCount, row + 1); index++)
            {
                var cells = table.Columns.Select(c => index < table.RowCount || index == row ? table.Get(index == row ? source : index, c) : "").ToArray();
                if (index == row) foreach (var (column, value) in values) cells[table.ColumnIndex(column)] = value;
                text.Append(index).Append(' ').AppendLine(string.Join(" ", cells.Select(c => string.IsNullOrEmpty(c) ? "****" : c.Contains(' ') ? "\"" + c + "\"" : c)));
            }
            hak.Set(OpenWinter.Aurora.Resources.ResKey.Of(name, OpenWinter.Aurora.Resources.ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(text.ToString()));
        }
    }

    [Fact]
    public void PaletteIconsShowTheirTop64x96OrTheWholeHelmet()
    {
        Assert.Equal((64, 96), PltIcon.Canvas(64, 128));
        Assert.Equal((64, 64), PltIcon.Canvas(64, 64));
    }

    [Fact]
    public void IconPixelsTakeThePaletteColourAndAlphaAndBothMetalsUseArmor02()
    {
        // 2x1 PLT: a metal 1 pixel at shade 128 and a transparent canvas pixel (shade 255).
        var plt = new PaletteTextureData { Width = 2, Height = 1, Shades = new byte[] { 128, 255 }, Layers = new byte[] { 2, 2 } };
        var palettes = new Dictionary<string, PltIcon.Palette>
        {
            // 256 x 2 palettes: row 1, column 128 holds a distinct colour.
            ["pal_armor01"] = Pal(1, 128, 9, 9, 9, 255),
            ["pal_armor02"] = Pal(1, 128, 200, 100, 50, 180),
        };
        var rgba = PltIcon.Paint(plt, new byte[] { 0, 0, 1, 0, 0, 0, 0, 0, 0, 0 }, n => palettes.GetValueOrDefault(n));
        Assert.Equal(new byte[] { 200, 100, 50, 180, 0, 0, 0, 0 }, rgba);

        static PltIcon.Palette Pal(int row, int col, byte r, byte g, byte b, byte a)
        {
            var data = new byte[256 * 2 * 4];
            int i = (row * 256 + col) * 4;
            data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = a;
            return new PltIcon.Palette(data, 256, 2);
        }
    }
}
