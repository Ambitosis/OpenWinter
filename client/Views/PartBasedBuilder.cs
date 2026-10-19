using System.Numerics;
using System.Runtime.CompilerServices;
using OpenWinter.Client.Rendering;
using OpenWinter.Content;
using OpenWinter.Net;

namespace OpenWinter.Client.Views;

/// <summary>
/// Assembles part-based (player-style) creatures: a base skeleton model
/// ("p" + gender + race + phenotype, e.g. pmh0) plus one model per body part,
/// armour parts when armour is worn, robes and cloaks skinned to the skeleton,
/// and weapons, shields and helmets attached to their bones. PLT textures are
/// tinted with the creature's and items' colours.
/// </summary>
public static partial class PartBasedBuilder
{
    /// <summary>Body part names in Appearance.BodyParts order, with the skeleton bone each attaches to.</summary>
    static readonly (string Part, string Bone)[] Parts =
    {
        ("head", "head_g"), ("neck", "neck_g"), ("chest", "torso_g"), ("pelvis", "pelvis_g"), ("belt", "belt_g"),
        ("shor", "Rshoulder_g"), ("shol", "Lshoulder_g"), ("bicepr", "Rbicep_g"), ("bicepl", "Lbicep_g"),
        ("forer", "rforearm_g"), ("forel", "lforearm_g"), ("handr", "rhand_g"), ("handl", "lhand_g"),
        ("legr", "rthigh_g"), ("legl", "lthigh_g"), ("shinr", "rshin_g"), ("shinl", "lshin_g"),
        ("footr", "rfoot_g"), ("footl", "lfoot_g"),
    };

    const int SlotHead = 0x1, SlotChest = 0x2, SlotCloak = 0x40;
    public const int SlotRight = 0x10, SlotLeft = 0x20;

    public static ModelData? Build(Appearance a, ClientContent content, int hiddenEquipmentSlots = 0)
    {
        var appearance = content.Resources.TwoDA("appearance");
        string race = appearance.Get(a.Type, "RACE").ToLowerInvariant();
        if (race.Length == 0) return null;
        string prefix = $"p{(a.Gender == 1 ? 'f' : 'm')}{race}{a.Phenotype}";
        var baseModel = content.Models.Get(prefix) ?? content.Models.Get($"p{(a.Gender == 1 ? 'f' : 'm')}{race}0");
        if (baseModel == null) return null;
        prefix = baseModel.Name.ToLowerInvariant();

        var tinter = content.Tinter;
        var model = PltTinter.ShallowCopy(baseModel);
        foreach (var p in model.Parts) p.Mesh = null; // the base model is only a skeleton

        byte[] creatureColors = a.Colors.Length >= 4 ? a.Colors : new byte[4];
        var armor = a.Equipment.FirstOrDefault(e => e.Slot == SlotChest);
        byte[] armorLayers = Layers(creatureColors, armor?.Colors);

        // Robe: replaces whichever parts parts_robe.2da hides.
        var hidden = new HashSet<string>();
        // Helmets replace the head mesh at head_g. The separate "head" node is
        // an effect attachment above the skull (pmh0: another 0.193 m higher).
        var helmet = a.Equipment.FirstOrDefault(e => e.Slot == SlotHead && (e.Slot & hiddenEquipmentSlots) == 0);
        if (helmet != null)
        {
            string cls = content.Resources.TwoDA("baseitems").Get(helmet.BaseItem, "ItemClass").ToLowerInvariant();
            int number = helmet.ModelParts.Length > 0 ? helmet.ModelParts[0] : 1;
            if (content.Models.Get($"{cls}_{number:000}") != null) hidden.Add("head");
        }
        int robe = armor != null && armor.ArmorParts.Length > 19 ? armor.ArmorParts[19] : 0;
        if (robe > 0)
        {
            var robes = content.Resources.TwoDA("parts_robe");
            foreach (var (part, _) in Parts)
                if (robes.GetInt(robe, "HIDE" + part.ToUpperInvariant()) != 0) hidden.Add(part);
            if (content.Models.Get($"{prefix}_robe{robe:000}") is { } robeModel)
                AttachSkinned(model, robeModel, tinter, armorLayers, $"{prefix}_robe{robe:000}");
        }

        for (int i = 0; i < Parts.Length; i++)
        {
            var (part, bone) = Parts[i];
            if (hidden.Contains(part)) continue;
            // Armour part 0 shows the body part underneath (bare skin parts are body parts).
            int body = i < a.BodyParts.Length ? a.BodyParts[i] : 1;
            int number = i == 0 || armor == null || i >= armor.ArmorParts.Length || armor.ArmorParts[i] == 0 ? body : armor.ArmorParts[i];
            if (number == 0) continue;
            string resref = $"{prefix}_{part}{number:000}";
            var partModel = content.Models.Get(resref);
            if (partModel == null && number != 1 && content.Models.Get($"{prefix}_{part}001") is { } plain) (partModel, resref) = (plain, $"{prefix}_{part}001");
            if (partModel == null) continue;
            var layers = i == 0 ? Layers(creatureColors, null) : armorLayers;
            AttachRigid(model, partModel, bone, tinter, layers, resref, PartTexture(resref, tinter));
        }

        // Wings and tails (wingmodel.2da / tailmodel.2da) hang off their own bones.
        foreach (var (table, index, bone) in new[] { ("wingmodel", a.Wings, "wings"), ("tailmodel", a.Tail, "tail") })
        {
            if (index <= 0) continue;
            var name = content.Resources.TwoDA(table).Get(index, "MODEL");
            if (content.Models.Get(name) is { } extra && model.FindPart(bone) >= 0)
                AttachRigid(model, extra, bone, tinter, Layers(creatureColors, null), name);
        }

        foreach (var item in a.Equipment)
        {
            if ((item.Slot & hiddenEquipmentSlots) != 0) continue;
            switch (item.Slot)
            {
                case SlotRight:
                case SlotLeft:
                    AttachHeld(model, item, content, creatureColors);
                    break;
                case SlotHead:
                    AttachItem(model, item, content, "head_g", creatureColors, appearance.GetFloat(a.Type, a.Gender == 1 ? "HELMET_SCALE_F" : "HELMET_SCALE_M", 1));
                    break;
                case SlotCloak:
                    var cloakNumber = item.ModelParts.Length > 0 ? item.ModelParts[0] : 1;
                    if (content.Models.Get($"{prefix}_cloak_{cloakNumber:000}") is { } cloak)
                        AttachSkinned(model, cloak, tinter, Layers(creatureColors, item.Colors), $"{prefix}_cloak_{cloakNumber:000}");
                    break;
            }
        }
        return model;
    }

    static bool IsShield(int baseItem, ClientContent content) =>
        content.Resources.TwoDA("baseitems").GetInt(baseItem, "BaseAC") > 0;

    /// <summary>
    /// Whole-body models (appearance.2da MODELTYPE F) hold weapons, shields and torches too: the original shows
    /// them in the hands of NPCs such as the bandits and elf mages (parity/visual, scene torch). They have the
    /// skeleton's rhand, lhand and lforearm nodes. Returns a copy when something is attached.
    /// </summary>
    public static ModelData WithHeldItems(ModelData model, Appearance a, ClientContent content, int hiddenEquipmentSlots = 0)
    {
        var held = a.Equipment.Where(e => (e.Slot is SlotRight or SlotLeft) && (e.Slot & hiddenEquipmentSlots) == 0).ToList();
        if (held.Count == 0) return model;
        var copy = PltTinter.ShallowCopy(model);
        byte[] creatureColors = a.Colors.Length >= 4 ? a.Colors : new byte[4];
        foreach (var item in held) AttachHeld(copy, item, content, creatureColors);
        return copy;
    }

    static void AttachHeld(ModelData model, ItemVisual item, ClientContent content, byte[] creatureColors) =>
        AttachItem(model, item, content, item.Slot == SlotRight ? "rhand" : IsShield(item.BaseItem, content) ? "lforearm" : "lhand", creatureColors);

    /// <summary>
    /// The texture a body or armour part is drawn with. The original ignores the part model's own bitmap when there
    /// is a PLT named after the part: pmh0_chest012's mesh names pmh0_chest013 (a studded leather vest), yet Pavel's
    /// academy uniform (chest 12) shows chest012's blue coat. Other races and phenotypes have no part PLTs of
    /// their own (only heads) and wear the human ones of their gender: the same coat on an elf or a dwarf is the
    /// blue coat too, though their models name pmh0_chest013 (parity/visual, scene pavel). Null keeps the bitmap.
    /// </summary>
    public static string? PartTexture(string partModel, PltTinter tinter)
    {
        if (tinter.IsPalette(partModel)) return partModel;
        // p{gender}{race}{phenotype}_{part}{number} -> p{gender}h0_{part}{number}
        int us = partModel.IndexOf('_');
        if (us == 4 && partModel[0] is 'p' or 'P')
        {
            string human = partModel[..2] + "h0" + partModel[us..];
            if (tinter.IsPalette(human)) return human;
        }
        return null;
    }

    /// <summary>
    /// An item model's emitters as its "equip" animation leaves them: a torch's flame and smoke have no birthrate
    /// until it is equipped (it_torch_000: equip keys Flame02 to 9 and the smoke to 12 particles a second).
    /// </summary>
    static EmitterData? Equipped(ModelData item, ModelPart part)
    {
        if (part.Emitter is not { } e) return null;
        if (!item.Animations.TryGetValue("equip", out var equip) || !equip.Tracks.TryGetValue(part.Name, out var track) || track.EmitterKeys.Count == 0)
            return e;
        var copy = e.Clone();
        foreach (var (param, keys) in track.EmitterKeys)
            for (int c = 0; c < keys.Width && c < 3; c++) copy.Set(param, keys.Sample(equip.Length, c), c);
        return copy;
    }

    /// <summary>PLT layer colours from creature colours (skin, hair, tattoo 1, tattoo 2) and item colours (cloth 1/2, leather 1/2, metal 1/2).</summary>
    public static byte[] Layers(byte[] creature, byte[]? item)
    {
        byte C(int i) => item != null && i < item.Length ? item[i] : (byte)0;
        return new[]
        {
            creature.Length > 0 ? creature[0] : (byte)0, creature.Length > 1 ? creature[1] : (byte)0,
            C(4), C(5), C(0), C(1), C(2), C(3),
            creature.Length > 2 ? creature[2] : (byte)0, creature.Length > 3 ? creature[3] : (byte)0,
        };
    }

    static int Bone(ModelData model, string name)
    {
        int b = model.FindPart(name);
        return b >= 0 ? b : 0;
    }

    /// <summary>Adds a part model's meshes under a skeleton bone, keeping their offsets from the part model's root.</summary>
    /// <param name="texture">Replaces the palette (PLT) meshes' bitmap (body parts, <see cref="PartTexture"/>).</param>
    /// <param name="equipped">An item: its emitters as its equip animation leaves them.</param>
    static void AttachRigid(ModelData model, ModelData part, string bone, PltTinter tinter, byte[] layers, string fallbackTexture,
        string? texture = null, bool equipped = false, float scale = 1)
    {
        int parent = Bone(model, bone);
        var globals = part.RestGlobals();
        for (int i = 0; i < part.Parts.Count; i++)
        {
            var src = part.Parts[i];
            if (src.Mesh == null && src.Emitter == null) continue;
            Matrix4x4.Decompose(globals[i], out _, out var rot, out var pos);
            var mesh = src.Mesh?.Clone();
            if (mesh != null)
            {
                if (scale != 1) mesh.Vertices = mesh.Vertices.Select(v => v * scale).ToArray();
                // Only palette meshes: a part's plain-textured pieces (glows, gems) keep their own.
                if (texture != null && (mesh.Texture.Length == 0 || tinter.IsPalette(mesh.Texture))) mesh.Texture = texture;
                Texture(mesh, tinter, layers, fallbackTexture);
            }
            var emitter = equipped ? Equipped(part, src) : src.Emitter;
            model.Parts.Add(new ModelPart { Name = src.Name, Parent = parent, Position = pos * scale, Rotation = rot, Mesh = mesh, Emitter = emitter });
        }
    }

    /// <summary>Adds a skinned part model (robe, cloak): its bones are copies of skeleton nodes, matched by name.</summary>
    static void AttachSkinned(ModelData model, ModelData part, PltTinter tinter, byte[] layers, string fallbackTexture)
    {
        var map = new int[part.Parts.Count];
        for (int i = 0; i < part.Parts.Count; i++)
        {
            int b = model.FindPart(part.Parts[i].Name);
            map[i] = b;
        }
        var globals = part.RestGlobals();
        for (int i = 0; i < part.Parts.Count; i++)
        {
            var src = part.Parts[i];
            if (src.Mesh is not { Render: true } || src.Mesh.Indices.Length == 0) continue;
            var mesh = src.Mesh.Clone();
            Texture(mesh, tinter, layers, fallbackTexture);
            int parent = map[i] >= 0 ? map[i] : 0;
            if (mesh.IsSkinned)
            {
                var bones = new int[mesh.BoneParts.Length];
                for (int k = 0; k < bones.Length; k++)
                    bones[k] = mesh.BoneParts[k] >= 0 && mesh.BoneParts[k] < map.Length ? map[mesh.BoneParts[k]] : -1;
                mesh.BoneParts = bones;
                // Skin vertices live in the skin node's space; place the node at its rest pose under the root.
                Matrix4x4.Decompose(globals[i], out _, out var r, out var p);
                model.Parts.Add(new ModelPart { Name = src.Name + "_skin", Parent = 0, Position = p, Rotation = r, Mesh = mesh });
            }
            else
            {
                model.Parts.Add(new ModelPart { Name = src.Name + "_part", Parent = parent, Position = Vector3.Zero, Rotation = Quaternion.Identity, Mesh = mesh });
            }
        }
    }

    static void AttachItem(ModelData model, ItemVisual item, ClientContent content, string bone, byte[] creatureColors, float scale = 1)
    {
        var items = content.Resources.TwoDA("baseitems");
        string cls = items.Get(item.BaseItem, "ItemClass").ToLowerInvariant();
        int type = items.GetInt(item.BaseItem, "ModelType");
        if (cls.Length == 0) return;
        var layers = Layers(creatureColors, item.Colors);
        int P(int i) => item.ModelParts.Length > i ? item.ModelParts[i] : 1;
        bool weapon = items.GetInt(item.BaseItem, "WeaponType", 0) > 0;
        string hand = item.Slot == SlotRight ? "R" : "L";
        if (type == 2)
        {
            foreach (var (piece, n) in new[] { ("b", P(0)), ("m", P(1)), ("t", P(2)) })
                if (content.Models.Get($"{cls}_{piece}_{n:000}") is { } m)
                    Attach(m, $"{cls}_{piece}_{n:000}", hand + piece);
        }
        else if (content.Models.Get($"{cls}_{P(0):000}") is { } m)
        {
            Attach(m, $"{cls}_{P(0):000}", hand);
        }

        void Attach(ModelData m, string resref, string tag)
        {
            // Weapons whose part models (through their supermodels) carry animations keep their node hierarchy, and
            // play the creature's clip of the same name: the morning star's and flails' chain (wblfh_t_001: 1hslashl,
            // 1hreadyr ... and default), the bows' limbs (bowshot), the crossbows' arms (xbowshot), the sling and whip.
            var clips = weapon ? AnimatedClips(m, content) : null;
            if (clips != null) AttachAnimated(model, m, bone, content.Tinter, layers, resref, scale, tag + "_", clips);
            else AttachRigid(model, m, bone, content.Tinter, layers, resref, equipped: true, scale: scale);
        }
    }

    /// <summary>An item part model's animations through its supermodel chain that key its own nodes; null when none.</summary>
    static Dictionary<string, ModelAnimation>? AnimatedClips(ModelData part, ClientContent content)
    {
        Dictionary<string, ModelAnimation>? clips = null;
        foreach (var (name, anim, _) in content.Models.AllAnimations(part))
            if (anim.Tracks.Any(t => (t.Value.RotationTimes.Length > 0 || t.Value.PositionTimes.Length > 0) && part.FindPart(t.Key) >= 0))
                (clips ??= new(StringComparer.OrdinalIgnoreCase))[name] = anim;
        return clips;
    }

    /// <summary>An animated item attached to a creature: its renamed nodes and its clips by name.</summary>
    sealed record ItemRig(string Prefix, Dictionary<string, string> Nodes, Dictionary<string, ModelAnimation> Clips, float Scale);

    /// <summary>
    /// Whether the item in a hand (<paramref name="slot"/> SlotRight or SlotLeft) animates itself while held, with a
    /// pose of its own ("default"): the flails' chain, the whip's lash, the sling's strap.
    /// </summary>
    public static bool HoldsAnimatedItem(ModelData model, int slot) =>
        Rigs.TryGetValue(model, out var rigs) && rigs.Any(r => r.Prefix.StartsWith(slot == SlotRight ? "R" : "L") && r.Clips.ContainsKey("default"));

    static readonly ConditionalWeakTable<ModelData, List<ItemRig>> Rigs = new();

    /// <summary>
    /// Adds an animated item part model under a skeleton bone with its node hierarchy, its nodes renamed with
    /// <paramref name="prefix"/> (the hand and part: a dual-wielded pair of flails has the same node names twice).
    /// </summary>
    static void AttachAnimated(ModelData model, ModelData part, string bone, PltTinter tinter, byte[] layers, string fallbackTexture,
        float scale, string prefix, Dictionary<string, ModelAnimation> clips)
    {
        int hand = Bone(model, bone);
        var map = new int[part.Parts.Count];
        var nodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < part.Parts.Count; i++)
        {
            var src = part.Parts[i];
            var mesh = src.Mesh?.Clone();
            if (mesh != null)
            {
                if (scale != 1) mesh.Vertices = mesh.Vertices.Select(v => v * scale).ToArray();
                Texture(mesh, tinter, layers, fallbackTexture);
            }
            int parent = src.Parent >= 0 && src.Parent < i ? map[src.Parent] : hand;
            string name = prefix + src.Name;
            model.Parts.Add(new ModelPart { Name = name, Parent = parent, Position = src.Position * scale, Rotation = src.Rotation, Mesh = mesh, Emitter = Equipped(part, src) });
            map[i] = model.Parts.Count - 1;
            nodes[src.Name] = name;
        }
        Rigs.GetOrCreateValue(model).Add(new ItemRig(prefix, nodes, clips, scale));
    }

    /// <summary>
    /// A creature clip with the tracks of its animated held items added: each item plays its clip of the same
    /// name, else its "default" pose (the morning star's chain hangs from the haft while walking). Item positions
    /// are divided by <paramref name="positionScale"/>, which the creature's clips are built with.
    /// </summary>
    public static ModelAnimation WithItemTracks(ModelData model, string clip, ModelAnimation anim, float positionScale)
    {
        if (!Rigs.TryGetValue(model, out var rigs) || rigs.Count == 0) return anim;
        ModelAnimation? merged = null;
        foreach (var rig in rigs)
        {
            if (!rig.Clips.TryGetValue(clip, out var source) && !rig.Clips.TryGetValue("default", out source)) continue;
            if (merged == null)
            {
                merged = new ModelAnimation { Name = anim.Name, Length = anim.Length, Transition = anim.Transition, Root = anim.Root };
                merged.Events.AddRange(anim.Events);
                foreach (var kv in anim.Tracks) merged.Tracks[kv.Key] = kv.Value;
            }
            float factor = positionScale > 0 ? rig.Scale / positionScale : 1f;
            foreach (var (node, track) in source.Tracks)
                if (rig.Nodes.TryGetValue(node, out var name) && (track.RotationTimes.Length > 0 || track.PositionTimes.Length > 0))
                    merged.Tracks[name] = new AnimationTrack
                    {
                        RotationTimes = track.RotationTimes, Rotations = track.Rotations,
                        PositionTimes = track.PositionTimes,
                        Positions = factor == 1 ? track.Positions : track.Positions.Select(v => v * factor).ToArray(),
                    };
        }
        return merged ?? anim;
    }

    /// <summary>Body part meshes without a texture use the PLT named after the part model.</summary>
    static void Texture(MeshData mesh, PltTinter tinter, byte[] layers, string fallback)
    {
        if (mesh.Texture.Length == 0) mesh.Texture = fallback;
        if (tinter.IsPalette(mesh.Texture) && tinter.Tint(mesh.Texture, layers) is { } key) mesh.Texture = key;
    }

    /// <summary>The model whose animation chain a creature uses.</summary>
    public static ModelData AnimationSource(ModelData model, ClientContent content) => model;
}
