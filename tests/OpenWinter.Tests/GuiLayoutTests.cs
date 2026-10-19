using OpenWinter.Aurora.Formats;
using OpenWinter.Content;
using Xunit;

namespace OpenWinter.Tests;

public class GuiLayoutTests
{
    [Fact]
    public void ReadsPanelControlsCaptionsAndNesting()
    {
        var caption = new GffStruct(3).SetString("AurString_Font", "fnt_galahad14").SetFloat("AurString_AlignH", 0.5f).SetFloat("AurString_AlignV", 0.5f)
            .SetFloat("AurString_ColorR", 1).SetFloat("AurString_ColorG", 0.5f).SetFloat("AurString_ColorB", 0).SetFloat("AurString_ColorA", 1).SetDWord("Obj_StrRef", 66);
        var label = new GffStruct(2).SetInt("Obj_Type", 5).SetString("Obj_Tag", "Note").SetString("Obj_Parent_Hook", "BOX")
            .SetFloat("Obj_Label_X", 0.1f).SetFloat("Obj_Label_Y", 0.2f).SetFloat("Obj_Label_Width", 1.5f).SetFloat("Obj_Label_Height", 0.17f)
            .SetStruct("Obj_Caption", new GffStruct(3).SetString("AurString_Text", "Hi").SetDWord("Obj_StrRef", uint.MaxValue));
        var button = new GffStruct(2).SetInt("Obj_Type", 9).SetString("Obj_Tag", "OkButton").SetString("Obj_ResRef", "ctl_btn_pre_176")
            .SetString("Obj_Parent", "PNL").SetString("Obj_Parent_Hook", "PNL").SetInt("Obj_Layer", 4)
            .SetFloat("Obj_X", -2.49f).SetFloat("Obj_Y", -1.22f).SetFloat("Obj_Z", 0.67f).SetStruct("Obj_Caption", caption);
        var box = new GffStruct(2).SetInt("Obj_Type", 2).SetString("Obj_Tag", "Check").SetString("Obj_ResRef", "ctl_btn_xch").SetInt("Obj_Checked", 1)
            .SetFloat("Obj_X", 1).SetFloat("Obj_Y", 1).SetList("Obj_ChildList", new List<GffStruct> { label });
        var root = new GffStruct().SetInt("Obj_Type", 3).SetString("Obj_Tag", "PNL").SetString("Obj_ResRef", "pnl_util_ok")
            .SetFloat("Obj_X", 5.74f).SetFloat("Obj_Y", 1.31f).SetList("Obj_ChildList", new List<GffStruct> { button, box });

        var layout = GuiReader.Read(Gff.Write(root, "GUI "), "test");

        Assert.Equal(GuiKind.Panel, layout.Root.Kind);
        Assert.Equal("pnl_util_ok", layout.Root.ResRef);
        var ok = layout.Find("okbutton")!;
        Assert.Equal(GuiKind.TextButton, ok.Kind);
        Assert.Equal(-2.49f, ok.Position.X, 3);
        Assert.Equal(66u, ok.Caption!.StrRef);
        Assert.Equal(0.5f, ok.Caption.Color.Y, 3);
        Assert.Equal(0.5f, ok.Caption.AlignH, 3);
        var check = layout.Find("Check")!;
        Assert.True(check.Checked);
        var note = Assert.Single(check.Children);
        Assert.Equal(GuiKind.Label, note.Kind);
        Assert.True(note.HasLabel);
        Assert.False(note.HasPosition);
        Assert.Equal("Hi", note.Caption!.Text);
        Assert.Equal(uint.MaxValue, note.Caption.StrRef);
        Assert.Equal(4, layout.All().Count());
    }
}
