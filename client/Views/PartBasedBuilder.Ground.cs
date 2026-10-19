using System.Numerics;
using OpenWinter.Client.Rendering;
using OpenWinter.Content;
using OpenWinter.Net;

namespace OpenWinter.Client.Views;

public static partial class PartBasedBuilder
{
    /// <summary>
    /// The model an item shows lying on the ground, measured on the original (parity/visual, scene grounditems;
    /// docs/GROUND_ITEMS.md). The item's own model when the game has one, built from its parts and tinted with its
    /// colours as when it is worn or held: part-built weapons (baseitems ModelType 2) from their bottom, middle and
    /// top, one-piece items (ModelType 0 and 1: shields, helmets, throwing weapons, the torch, gold) from
    /// ItemClass_NNN. Armour is gifp's flat torso panel painted with the armour's chest texture (an approximation:
    /// docs/GROUND_ITEMS.md); cloaks are gi_cloak01, tinted with the cloak's colours. Anything else shows baseitems DefaultModel (the bag, it_bag, for most kinds; potions, scrolls, ammunition and trap
    /// kits have their own). The root part carries the ground pose: baseitems RotateOnGround 1 turns the model a
    /// quarter about its Y axis (weapons and shields lie flat), 2 about its X axis (crossbows lie flat, potions
    /// stand up), and the model is lifted so its lowest point rests on the ground.
    /// </summary>
    public static ModelData? GroundItem(ItemVisual item, ClientContent content)
    {
        var items = content.Resources.TwoDA("baseitems");
        int baseItem = item.BaseItem;
        string cls = items.Get(baseItem, "ItemClass").ToLowerInvariant();
        int type = items.GetInt(baseItem, "ModelType");
        var tinter = content.Tinter;
        var layers = Layers(new byte[4], item.Colors);
        int P(int i) => item.ModelParts.Length > i && item.ModelParts[i] > 0 ? item.ModelParts[i] : 1;

        var model = new ModelData { Name = "grounditem", Classification = ModelClassification.Item };
        model.Parts.Add(new ModelPart { Name = "grounditem" });
        bool own = false;
        if (type == 3)
        {
            int torso = item.ArmorParts.Length > 2 && item.ArmorParts[2] > 0 ? item.ArmorParts[2] : 1;
            string chest = $"{GroundArmorPrefix}_chest{torso:000}";
            if (content.Models.Get(ArmorGroundModel) is { } flat && flat.FindPart("torso_g") is var panel and >= 0
                && flat.Parts[panel].Mesh is { } mesh)
            {
                var painted = mesh.Clone();
                painted.Texture = PartTexture(chest, tinter) ?? chest;
                painted.Uvs = PanelUvs(painted.Vertices);
                Texture(painted, tinter, ArmorLayers(item), chest);
                model.Parts.Add(new ModelPart { Name = "torso_g", Parent = 0, Rotation = ArmorOnGround, Mesh = painted });
                own = true;
            }
        }
        else if (cls == "cloak")
        {
            if (content.Models.Get(GroundCloak) is { } cloak)
            {
                AttachRigid(model, cloak, "grounditem", tinter, layers, GroundCloak);
                own = true;
            }
        }
        else if (type == 2)
        {
            foreach (var (piece, n) in new[] { ("b", P(0)), ("m", P(1)), ("t", P(2)) })
                if (cls.Length > 0 && content.Models.Get($"{cls}_{piece}_{n:000}") is { } m)
                {
                    AttachRigid(model, m, "grounditem", tinter, layers, $"{cls}_{piece}_{n:000}");
                    own = true;
                }
        }
        else if (cls.Length > 0 && content.Models.Get($"{cls}_{P(0):000}") is { } m)
        {
            AttachRigid(model, m, "grounditem", tinter, layers, $"{cls}_{P(0):000}");
            own = true;
        }
        if (!own)
        {
            string name = items.Get(baseItem, "DefaultModel");
            var fallback = (name.Length > 0 ? content.Models.Get(name) : null) ?? content.Models.Get(DefaultGroundModel);
            if (fallback == null) return null;
            AttachRigid(model, fallback, "grounditem", tinter, layers, fallback.Name);
            // DefaultModels turn too (the potion bottle, it_potion_000, stands up: RotateOnGround 2); the bag stays upright.
            own = !fallback.Name.Equals(DefaultGroundModel, StringComparison.OrdinalIgnoreCase);
        }
        Pose(model, own ? items.GetInt(baseItem, "RotateOnGround", 0) : 0);
        return model;
    }

    /// <summary>The model for items without one of their own and no DefaultModel.</summary>
    public const string DefaultGroundModel = "it_bag";
    /// <summary>Cloaks on the ground, whatever their part number, tinted with the cloak's colours.</summary>
    public const string GroundCloak = "gi_cloak01";
    /// <summary>Armour on the ground (baseitems DefaultModel of armour): its torso_g mesh is a flat torso panel.</summary>
    public const string ArmorGroundModel = "gifp";
    /// <summary>Whose chest texture paints armour on the ground (the item has no wearer).</summary>
    public const string GroundArmorPrefix = "pmh0";
    /// <summary>
    /// The panel lies face up in its own space (thin along Z, waist to neck along Y). The original lays it with the
    /// neck towards the item's facing and the waist behind (scene grounditems: at facing 0 the belt is west and the
    /// collar east): a half turn about Z from the panel's own layout. gifp's torso node would stand it on its side.
    /// </summary>
    public static readonly Quaternion ArmorOnGround = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI);
    static byte[] ArmorLayers(ItemVisual item) => Layers(new byte[4], item.Colors);
    /// <summary>The quarter turn of RotateOnGround 1 about the model's Y axis (+90 degrees: shields lie face up).</summary>
    public const float RotateOnGroundYSign = 1;

    /// <summary>The ground orientation of RotateOnGround (0 none, 1 about Y, 2 about X).</summary>
    public static Quaternion GroundRotation(int rotateOnGround) => rotateOnGround switch
    {
        1 => Quaternion.CreateFromAxisAngle(Vector3.UnitY, RotateOnGroundYSign * MathF.PI / 2),
        2 => Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2),
        _ => Quaternion.Identity,
    };

    /// <summary>The whole chest texture across the panel: u across its width (x), v from waist to neck (y).</summary>
    static System.Numerics.Vector2[] PanelUvs(Vector3[] vertices)
    {
        float x0 = vertices.Min(v => v.X), x1 = vertices.Max(v => v.X), y0 = vertices.Min(v => v.Y), y1 = vertices.Max(v => v.Y);
        return vertices.Select(v => new System.Numerics.Vector2((v.X - x0) / MathF.Max(x1 - x0, 1e-4f), (v.Y - y0) / MathF.Max(y1 - y0, 1e-4f))).ToArray();
    }

    /// <summary>Turns the model's root to its ground orientation and lifts its lowest point onto the ground.</summary>
    static void Pose(ModelData model, int rotateOnGround)
    {
        var rotation = GroundRotation(rotateOnGround);
        model.Parts[0].Rotation = rotation;
        model.Parts[0].Position = Vector3.Zero;
        var globals = model.RestGlobals();
        float min = float.MaxValue;
        for (int i = 0; i < model.Parts.Count; i++)
            if (model.Parts[i].Mesh is { Render: true } mesh)
                foreach (var v in mesh.Vertices)
                    min = MathF.Min(min, Vector3.Transform(v, globals[i]).Z);
        if (min != float.MaxValue) model.Parts[0].Position = new Vector3(0, 0, -min);
    }
}
