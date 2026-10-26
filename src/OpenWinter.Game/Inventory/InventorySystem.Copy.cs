using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>Copying items and objects (CopyItem, CopyItemAndModify, CopyObject) and item appearance.</summary>
public sealed partial class InventorySystem
{
    public const int ApprSimpleModel = 0, ApprWeaponColor = 1, ApprWeaponModel = 2, ApprArmorModel = 3, ApprArmorColor = 4;

    /// <summary>ITEM_APPR_ARMOR_MODEL_* order to UTI field names.</summary>
    public static readonly string[] ArmorModelFields =
    {
        "ArmorPart_RFoot", "ArmorPart_LFoot", "ArmorPart_RShin", "ArmorPart_LShin", "ArmorPart_LThigh", "ArmorPart_RThigh",
        "ArmorPart_Pelvis", "ArmorPart_Torso", "ArmorPart_Belt", "ArmorPart_Neck", "ArmorPart_RFArm", "ArmorPart_LFArm",
        "ArmorPart_RBicep", "ArmorPart_LBicep", "ArmorPart_RShoul", "ArmorPart_LShoul", "ArmorPart_RHand", "ArmorPart_LHand",
        "ArmorPart_Robe",
    };

    /// <summary>ITEM_APPR_ARMOR_COLOR_* order to UTI field names.</summary>
    public static readonly string[] ArmorColorFields = { "Leather1Color", "Leather2Color", "Cloth1Color", "Cloth2Color", "Metal1Color", "Metal2Color" };

    /// <summary>
    /// A registered, unplaced duplicate of an item (and its contents). <paramref name="asStored"/> copies it the way
    /// the original's CopyObject does, through its saved form: permanent property fields read back as stored (-1
    /// becomes 65535 or 255) and a bag's contents come back in reverse order.
    /// </summary>
    public Item Copy(Item src, bool copyVars, bool asStored = false)
    {
        var item = new Item
        {
            Tag = src.Tag,
            ResRef = src.ResRef,
            Name = src.Name.Clone(),
            Description = src.Description,
            Plot = src.Plot,
            BaseItem = src.BaseItem,
            StackSize = src.StackSize,
            Template = src.Template.DeepClone(),
            Identified = src.Identified,
            Stolen = src.Stolen,
            Cursed = src.Cursed,
            Droppable = src.Droppable,
            Pickpocketable = src.Pickpocketable,
            Charges = src.Charges,
            Cost = src.Cost,
            AddCost = src.AddCost,
            Infinite = src.Infinite,
            HiddenWhenEquipped = src.HiddenWhenEquipped,
            UseableOnGround = src.UseableOnGround,
        };
        // Temporary properties are copied as they are (the original re-adds them).
        foreach (var p in src.Properties) item.Properties.Add(asStored && p.DurationType == ItemRules.DurationPermanent ? AsStored(p) : p.Clone());
        foreach (var kv in src.PartColorOverrides) item.PartColorOverrides[kv.Key] = kv.Value;
        foreach (var kv in src.Scripts) item.Scripts[kv.Key] = kv.Value;
        if (copyVars) CopyLocals(src.Locals, item.Locals);
        _world.Register(item);
        foreach (var inner in asStored ? Enumerable.Reverse(src.Inventory) : src.Inventory)
        {
            var c = Copy(inner, copyVars, asStored);
            item.Inventory.Add(c);
            c.Possessor = item.Id;
        }
        return item;
    }

    /// <summary>An item property as it reads back from a saved item: the GFF field widths (word, byte) applied.</summary>
    public static ItemProperty AsStored(ItemProperty p)
    {
        var s = p.Clone();
        s.Type = (ushort)p.Type;
        s.SubType = (ushort)p.SubType;
        s.CostTable = (byte)p.CostTable;
        s.CostTableValue = (ushort)p.CostTableValue;
        s.Param1 = (byte)p.Param1;
        s.Param1Value = (byte)p.Param1Value;
        s.ChanceToAppear = (byte)p.ChanceToAppear;
        return s;
    }

    public static void CopyLocals(LocalVariables from, LocalVariables to)
    {
        foreach (var kv in from.Ints) to.Ints[kv.Key] = kv.Value;
        foreach (var kv in from.Floats) to.Floats[kv.Key] = kv.Value;
        foreach (var kv in from.Strings) to.Strings[kv.Key] = kv.Value;
        foreach (var kv in from.Objects) to.Objects[kv.Key] = kv.Value;
        foreach (var kv in from.Locations) to.Locations[kv.Key] = kv.Value;
        foreach (var kv in from.Jsons) to.Jsons[kv.Key] = kv.Value;
    }

    /// <summary>Places a new item where another one is: its holder's inventory, or the ground next to it.</summary>
    Item? PlaceLike(Item copy, Item original, GameObject? target)
    {
        target ??= Owner(original) ?? Holder(original);
        if (target != null) return MoveTo(copy, target) ?? (Refused(copy) ? copy : null); // a refused copy stays, held by nothing
        if (original.Area != null && Drop(copy, original.Location)) return copy;
        _world.Destroy(copy);
        return null;
    }

    /// <summary>
    /// CopyItem: copies into a target inventory, or the item's own location. Containers must be empty. The
    /// original doesn't copy the cursed and droppable flags.
    /// </summary>
    public Item? CopyItem(Item src, GameObject? target, bool copyVars)
    {
        EnsureLoaded();
        if (src.Inventory.Count > 0) return null;
        if (target != null && !HasInventory(target)) return null;
        var copy = Copy(src, copyVars);
        copy.Cursed = false;
        // The copy is droppable and pickpocketable whatever the original was (nwserver corpse_drops).
        copy.Droppable = true;
        copy.Pickpocketable = true;
        return PlaceLike(copy, src, target);
    }

    // ----- appearance -----

    static int Wide(GffStruct t, string field) => (int)CreatureFactory.Wide(t, field);

    static void SetWide(GffStruct t, string field, int value)
    {
        t.SetByte(field, Math.Min(value, 255));
        if (value > 255 || t.Has("x" + field)) t.SetWord("x" + field, value);
    }

    /// <summary>baseitems ModelType: 0 simple, 1 layered (helmets, cloaks), 2 composite (weapons), 3 armour.</summary>
    int ModelType(int baseItem) => _world.Resources.TwoDA("baseitems").GetInt(baseItem, "ModelType", 0);

    /// <summary>parts_*.2da for each ITEM_APPR_ARMOR_MODEL_* (how many models a part has).</summary>
    static readonly string[] ArmorPartTables =
    {
        "parts_foot", "parts_foot", "parts_shin", "parts_shin", "parts_legs", "parts_legs", "parts_pelvis", "parts_chest", "parts_belt",
        "parts_neck", "parts_forearm", "parts_forearm", "parts_bicep", "parts_bicep", "parts_shoulder", "parts_shoulder", "parts_hand",
        "parts_hand", "parts_robe",
    };

    /// <summary>GetItemAppearance; -1 for invalid arguments. Layered items (helmets) answer every armour part with their model.</summary>
    public int GetAppearance(Item item, int type, int index)
    {
        var t = item.Template;
        switch (type)
        {
            case ApprSimpleModel:
                return Wide(t, "ModelPart1");
            case ApprWeaponModel when index is >= 0 and <= 2:
                return Wide(t, $"ModelPart{index + 1}") / 10;
            case ApprWeaponColor when index is >= 0 and <= 2:
                return Wide(t, $"ModelPart{index + 1}") % 10;
            case ApprArmorModel when ModelType(item.BaseItem) == 1:
                return Wide(t, "ModelPart1");
            case ApprArmorModel when index >= 0 && index < ArmorModelFields.Length:
                return Wide(t, ArmorModelFields[index]);
            case ApprArmorColor when index >= 0 && index < 6:
                return (int)t.GetInt(ArmorColorFields[index]);
            case ApprArmorColor when index >= 6:
                return item.PartColorOverrides.TryGetValue(index - 6, out var v) ? v : 255;
            default:
                return -1;
        }
    }

    /// <summary>Whether an inventory icon exists for a model (the original only accepts weapon and helmet models that have one).</summary>
    bool HasIcon(string name)
    {
        var res = _world.Resources;
        foreach (var ext in new[] { "tga", "dds", "plt" })
            if (res.Contains(name, OpenWinter.Aurora.Resources.ResType.FromExtension(ext))) return true;
        return false;
    }

    /// <summary>
    /// Applies one appearance change to an item as CopyItemAndModify does; false when the original refuses the copy.
    /// Simple models take any value; weapon models need the part and an inventory icon, helmet models an icon; weapon colours outside 1-9
    /// become 1; armour parts past their parts_*.2da (negative parts are kept as a word) and colours above 175 leave the item unchanged (per-part
    /// colours above 175 clear the override); negative colours wrap to a byte.
    /// </summary>
    public bool SetAppearance(Item item, int type, int index, int value)
    {
        var t = item.Template;
        int modelType = ModelType(item.BaseItem);
        string itemClass = _world.Resources.TwoDA("baseitems").Get(item.BaseItem, "ItemClass").ToLowerInvariant();
        switch (type)
        {
            case ApprSimpleModel:
                SetWide(t, "ModelPart1", value & 0xFFFF);
                return true;
            case ApprWeaponModel when index is >= 0 and <= 2:
            {
                var field = $"ModelPart{index + 1}";
                int part = value * 10 + Wide(t, field) % 10;
                string icon = modelType == 2 ? $"i{itemClass}_{"bmt"[index]}_{part:000}" : $"i{itemClass}_{part:000}";
                // The part must exist (baseitems PartNEnvMap) on a non-armour item, and the model must have an icon.
                bool hasPart = !_world.Resources.TwoDA("baseitems").IsEmpty(item.BaseItem, $"Part{index + 1}EnvMap");
                if (modelType == 3 || !hasPart || value < 0 || !HasIcon(icon)) return false;
                SetWide(t, field, part);
                return true;
            }
            case ApprWeaponColor when index is >= 0 and <= 2:
            {
                var field = $"ModelPart{index + 1}";
                SetWide(t, field, Wide(t, field) / 10 * 10 + (value is >= 1 and <= 9 ? value : 1));
                return true;
            }
            case ApprArmorModel when modelType == 1:
                if (value < 0 || !HasIcon($"i{itemClass}_{value:000}")) return false;
                SetWide(t, "ModelPart1", value);
                return true;
            case ApprArmorModel when index >= 0 && index < ArmorModelFields.Length:
                // Past the table's rows the copy is unchanged; a negative value is kept as a word (-1 reads 65535: craft_feats cim.ranges).
                if (modelType == 3 && value >= _world.Resources.TwoDA(ArmorPartTables[index]).RowCount) return true;
                SetWide(t, ArmorModelFields[index], value & 0xFFFF);
                return true;
            case ApprArmorColor when index is >= 0 and < 6:
                if (value <= 175) t.SetByte(ArmorColorFields[index], (byte)value);
                return true;
            case ApprArmorColor when index >= 6:
                // TODO(items): per-part colours are kept in memory only; the UTI fields for them aren't written yet.
                if ((index - 6) / 6 >= ArmorModelFields.Length) return true;
                if (value > 175) item.PartColorOverrides.Remove(index - 6);
                else item.PartColorOverrides[index - 6] = (byte)value;
                return true;
            case ApprWeaponModel when index == 3:
                return true; // the original copies the item unchanged (craft_feats cim.nwn2)
            default:
                return false;
        }
    }

    /// <summary>CopyItemAndModify: a copy with one appearance change, placed with the original (equipped originals copy to the inventory).</summary>
    public Item? CopyAndModify(Item src, int type, int index, int value, bool copyVars)
    {
        EnsureLoaded();
        var copy = Copy(src, copyVars);
        copy.Cursed = false;
        copy.Droppable = true;
        if (!SetAppearance(copy, type, index, value)) { _world.Destroy(copy); return null; }
        return PlaceLike(copy, src, null);
    }

    // ----- CopyObject -----

    /// <summary>CopyObject: duplicates creatures, items, placeables, waypoints, stores, triggers and encounters.</summary>
    public GameObject? CopyObject(GameObject src, Location loc, GameObject? owner, string newTag, bool copyLocalState)
    {
        EnsureLoaded();
        Load(src);
        var area = _world.Get(loc.Area) as Area;
        float facing = loc.FacingDegrees * MathF.PI / 180f;
        GameObject? copy = null;
        switch (src)
        {
            case Item item:
            {
                // Items keep their local variables and cursed flag but not the droppable flag.
                var c = Copy(item, copyVars: true, asStored: true);
                c.Droppable = true;
                if (owner != null && HasInventory(owner)) copy = MoveTo(c, owner, merge: false) ?? (Refused(c) ? c : null);
                else if (area != null && Drop(c, loc)) copy = c;
                else _world.Destroy(c);
                break;
            }
            case Creature cr when area != null:
            {
                // Settings kept with the saved form (NWNX_POS) go with the copy, as through the original's saved form.
                var template = cr.Template;
                if (cr.Overrides is { Persisted.Count: > 0 })
                {
                    template = template.DeepClone();
                    global::OpenWinter.Game.Rules.CreatureOverridesPersistence.Save(cr, template);
                }
                var c = _world.Creatures.FromTemplate(template);
                c.ResRef = cr.ResRef;
                c.Name = cr.Name.Clone();
                c.Description = cr.Description;
                c.Tag = cr.Tag;
                // A copy of a polymorphed creature is the creature in its own form, with its own equipment.
                var poly = cr.PolymorphForm;
                c.AppearanceType = poly?.OldAppearance ?? cr.AppearanceType;
                c.Gender = cr.Gender; c.Phenotype = cr.Phenotype;
                cr.BodyParts.CopyTo(c.BodyParts, 0);
                c.Colors = (byte[])cr.Colors.Clone();
                c.Tail = cr.Tail; c.Wings = cr.Wings;
                c.Portrait = poly?.OldPortrait ?? cr.Portrait; c.PortraitId = poly?.OldPortraitId ?? cr.PortraitId;
                c.SoundSet = poly?.OldSoundSet ?? cr.SoundSet;
                c.Faction = cr.Faction;
                (c.CurrentHitPoints, c.MaxHitPoints, _) = global::OpenWinter.Game.Rules.EffectSystem.OwnHitPoints(_world, cr);
                c.Gold = cr.Gold;
                // Carried items as they are now, not as the template had them.
                foreach (var i in c.Equipment.Values.Concat(c.Inventory).ToList()) _world.Destroy(i);
                c.Equipment.Clear(); c.Inventory.Clear();
                // Like the original's copy through the saved form: the backpack comes back in reverse order.
                foreach (var kv in global::OpenWinter.Game.Rules.EffectSystem.OwnEquipment(cr)) c.Equipment[kv.Key] = Copy(kv.Value, copyVars: true, asStored: true);
                foreach (var i in Enumerable.Reverse(cr.Inventory)) c.Inventory.Add(Copy(i, copyVars: true, asStored: true));
                c.Locals.Ints.Clear(); c.Locals.Floats.Clear(); c.Locals.Strings.Clear(); c.Locals.Objects.Clear();
                if (copyLocalState) CopyLocals(cr.Locals, c.Locals);
                foreach (var kv in cr.Scripts) c.Scripts[kv.Key] = kv.Value;
                var pos = loc.Position;
                if (area.Walkmesh.GroundHeight(pos.X, pos.Y, pos.Z, 2f) is { } z) pos.Z = z;
                FireSpawnAcquires(c);
                _world.AddToArea(c, area, pos, facing);
                copy = c;
                break;
            }
            case Placeable p when area != null && _world.Loader != null:
            {
                facing = GameObject.BearingFor(facing);
                var c = _world.Loader.CreatePlaceable(area, p.Template, loc.Position, facing);
                c.ResRef = p.ResRef;
                c.Name = p.Name.Clone();
                c.InventoryLoaded = true;
                foreach (var i in Enumerable.Reverse(p.Inventory)) { var ci = Copy(i, copyVars: true, asStored: true); c.Inventory.Add(ci); ci.Possessor = c.Id; }
                if (copyLocalState) CopyLocals(p.Locals, c.Locals);
                _world.AddToArea(c, area, loc.Position, facing);
                copy = c;
                break;
            }
            case Store s when area != null:
            {
                var c = new Store
                {
                    ResRef = s.ResRef, Tag = s.Tag, Name = s.Name.Clone(), Template = s.Template, InventoryLoaded = true,
                    Gold = s.Gold, MaxBuyPrice = s.MaxBuyPrice, IdentifyPrice = s.IdentifyPrice, MarkUp = s.MarkUp, MarkDown = s.MarkDown,
                    BlackMarket = s.BlackMarket, BlackMarketMarkDown = s.BlackMarketMarkDown
                };
                c.WillNotBuy.UnionWith(s.WillNotBuy);
                c.WillOnlyBuy.UnionWith(s.WillOnlyBuy);
                foreach (var kv in s.Scripts) c.Scripts[kv.Key] = kv.Value;
                _world.Register(c);
                foreach (var i in s.Inventory) { var ci = Copy(i, copyVars: true, asStored: true); ci.StorePanel = i.StorePanel; c.Inventory.Add(ci); ci.Possessor = c.Id; }
                if (copyLocalState) CopyLocals(s.Locals, c.Locals);
                _world.AddToArea(c, area, loc.Position, facing);
                copy = c;
                break;
            }
            case Waypoint w when area != null:
            {
                var c = new Waypoint { ResRef = w.ResRef, Tag = w.Tag, Name = w.Name.Clone(), HasMapNote = w.HasMapNote, MapNote = w.MapNote.Clone() };
                if (copyLocalState) CopyLocals(w.Locals, c.Locals);
                _world.AddToArea(c, area, loc.Position, facing);
                copy = c;
                break;
            }
            case Trigger t when area != null:
            {
                var c = new Trigger { ResRef = t.ResRef, Tag = t.Tag, Name = t.Name.Clone(), TriggerType = t.TriggerType, Cursor = t.Cursor };
                if (copyLocalState) { c.LinkedTo = t.LinkedTo; c.LinkedToFlags = t.LinkedToFlags; CopyLocals(t.Locals, c.Locals); }
                foreach (var p in t.Geometry) c.Geometry.Add(p - t.Position + loc.Position);
                foreach (var kv in t.Scripts) c.Scripts[kv.Key] = kv.Value;
                _world.AddToArea(c, area, loc.Position, facing);
                copy = c;
                break;
            }
            case Encounter e when area != null:
            {
                var c = new Encounter { ResRef = e.ResRef, Tag = e.Tag, Name = e.Name.Clone(), Active = e.Active, Template = e.Template };
                foreach (var p in e.Geometry) c.Geometry.Add(p - e.Position + loc.Position);
                foreach (var kv in e.Scripts) c.Scripts[kv.Key] = kv.Value;
                if (copyLocalState) CopyLocals(e.Locals, c.Locals);
                _world.AddToArea(c, area, loc.Position, facing);
                copy = c;
                break;
            }
            // TODO(items): doors need their walkmesh registered with the area; not copied yet.
        }
        if (copy == null) return null;
        if (newTag.Length > 0) _world.SetTag(copy, newTag);
        else if (copy.Tag != src.Tag) _world.SetTag(copy, src.Tag);
        // OnSpawn runs after the copying script, like CreateObject's.
        if (copy is Creature spawned) _world.QueueSpawn(spawned);
        return copy;
    }
}
