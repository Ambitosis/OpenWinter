using System.Numerics;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Formats;

/// <summary>
/// Reads the base game's interface layouts (".gui", a GFF of Obj_* structs: the panel at the top level,
/// controls in nested Obj_ChildList lists). Field names were read from the files themselves.
/// </summary>
public static class GuiReader
{
    public static GuiLayout Read(byte[] data, string name = "")
    {
        var root = Gff.Read(data);
        return new GuiLayout { Name = name, Root = Node(root, 0) };
    }

    static GuiNode Node(GffStruct s, int depth)
    {
        var n = new GuiNode
        {
            Tag = s.GetString("Obj_Tag"),
            Kind = (GuiKind)s.GetInt("Obj_Type"),
            ResRef = s.GetString("Obj_ResRef"),
            Parent = s.GetString("Obj_Parent"),
            ParentHook = s.GetString("Obj_Parent_Hook"),
            Layer = (int)s.GetInt("Obj_Layer"),
            Locked = s.GetInt("Obj_Locked") != 0,
            HasPosition = s.Has("Obj_X"),
            Position = new Vector3(s.GetFloat("Obj_X"), s.GetFloat("Obj_Y"), s.GetFloat("Obj_Z")),
            HasLabel = s.Has("Obj_Label_X"),
            LabelPosition = new Vector3(s.GetFloat("Obj_Label_X"), s.GetFloat("Obj_Label_Y"), s.GetFloat("Obj_Label_Z")),
            LabelSize = new Vector2(s.GetFloat("Obj_Label_Width"), s.GetFloat("Obj_Label_Height")),
        };
        if (s.Has("Obj_Checked")) n.Checked = s.GetInt("Obj_Checked") != 0;
        if (s.Has("Obj_Length")) n.Length = (int)s.GetInt("Obj_Length");
        if (s.GetStruct("Obj_Caption") is { } c)
        {
            n.Caption = new GuiCaption
            {
                StrRef = (uint)c.GetInt("Obj_StrRef", uint.MaxValue),
                Text = c.GetString("AurString_Text"),
                Font = c.GetString("AurString_Font"),
                Color = new Vector4(c.GetFloat("AurString_ColorR", 1), c.GetFloat("AurString_ColorG", 1), c.GetFloat("AurString_ColorB", 1), c.GetFloat("AurString_ColorA", 1)),
                AlignH = c.GetFloat("AurString_AlignH"),
                AlignV = c.GetFloat("AurString_AlignV"),
            };
        }
        if (depth < 32)
            foreach (var child in s.GetList("Obj_ChildList"))
                n.Children.Add(Node(child, depth + 1));
        return n;
    }
}
