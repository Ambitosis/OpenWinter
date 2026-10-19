using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// Turns live objects into GFF (UTI / UTC-BIC / UTP-shaped structs) reflecting their
/// current state, and back. Each object's template is the starting point, so fields
/// the engine does not model yet survive a round trip; fields the engine does model
/// are written from the live object. Used by Store/RetrieveCampaignObject, SqlBind/GetObject,
/// ObjectToJson/JsonToObject and character export.
/// </summary>
public static partial class ObjectSerializer
{
    const uint RootStruct = 0xFFFFFFFF;

    /// <summary>ObjectToJson and JsonToObject take every object type but modules (parity suite persist_objects).</summary>
    public static bool CanSerialize(GameObject? o) => o is Item or Creature or Placeable or Door or Waypoint or Trigger or Store or Encounter or Sound or Area;

    /// <summary>StoreCampaignObject and SqlBindObject take the same types, except areas.</summary>
    public static bool CanStore(GameObject? o) => CanSerialize(o) && o is not Area;

    /// <summary>The GFF file type an object serializes as.</summary>
    public static string FileType(GameObject o) => o switch
    {
        Item => "UTI ",
        Creature c => c.IsPC ? "BIC " : "UTC ",
        Placeable => "UTP ",
        Door => "UTD ",
        Waypoint => "UTW ",
        Trigger => "UTT ",
        Store => "UTM ",
        Encounter => "UTE ",
        Sound => "UTS ",
        Area => "CAF ",
        _ => "",
    };

    /// <summary>
    /// Serializes an object as the script functions do (ObjectToJson, SqlBindObject, StoreCampaignObject); null for
    /// types that cannot be serialized. Like the original, inventories are written newest first (the order
    /// GetFirst/NextItemInInventory gives), so a loaded copy lists them the other way round. The object's state
    /// (variables, effects, actions, transition: <see cref="ObjectStateGff"/>) is only written with
    /// <paramref name="saveObjectState"/>, except for items and areas, which always carry it.
    /// </summary>
    public static GffStruct? Serialize(World world, GameObject o, bool saveObjectState = false)
    {
        var s = o switch
        {
            Item i => ItemGff(world, i, RootStruct, newestFirst: true),
            Creature c => CreatureGff(world, c, RootStruct, asCharacter: c.IsPC, newestFirst: true),
            Placeable p => PlaceableGff(world, p, newestFirst: true),
            Door d => DoorGff(world, d),
            Waypoint w => WaypointGff(world, w),
            Trigger t => TriggerGff(world, t),
            Store st => StoreGff(world, st),
            Encounter e => EncounterGff(world, e),
            Sound snd => SoundGff(world, snd),
            Area a => AreaCaf(world, a),
            _ => null,
        };
        if (s == null || o is Area) return s;
        if (o is Item || saveObjectState) ObjectStateGff.Write(world, o, s);
        else ObjectStateGff.Strip(s);
        return s;
    }

    public static byte[]? ToBytes(World world, GameObject o, bool saveObjectState = false) =>
        Serialize(world, o, saveObjectState) is { } s ? Gff.Write(s, FileType(o)) : null;

    public static GameObject? FromBytes(World world, byte[] data, Location at, GameObject? owner, bool loadObjectState = false)
    {
        if (data.Length < 56) return null;
        GffStruct s;
        string type;
        try { s = Gff.Read(data, out type); }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException or InvalidDataException) { return null; }
        return Deserialize(world, s, type, at, owner, loadObjectState);
    }

    // ---- Writing ---------------------------------------------------------------------------

    public static GffStruct ItemGff(World world, Item it, uint structType, bool newestFirst = false)
    {
        var t = it.Template;
        var s = t.DeepClone();
        s.StructType = structType;
        s.Remove("EquippedRes");
        s.Remove("InventoryRes");
        Str(s, "Tag", GffType.ExoString, it.Tag);
        Str(s, "TemplateResRef", GffType.ResRef, it.ResRef);
        if (!SameLoc(it.Name, t.GetLocString("LocalizedName"))) s.SetLocString("LocalizedName", it.Name.Clone());
        if (it.Description != t.GetLocString("DescIdentified").FirstString) s.SetLocString("DescIdentified", new LocString(it.Description));
        Sync(s, "BaseItem", GffType.Int, it.BaseItem, 0);
        Sync(s, "StackSize", GffType.Word, it.StackSize, 1);
        Sync(s, "Charges", GffType.Byte, it.Charges, 0);
        Sync(s, "Cost", GffType.DWord, it.Cost, 0);
        Sync(s, "AddCost", GffType.DWord, it.AddCost, 0);
        Sync(s, "Identified", GffType.Byte, it.Identified ? 1 : 0, 1);
        Sync(s, "Stolen", GffType.Byte, it.Stolen ? 1 : 0, 0);
        Sync(s, "Cursed", GffType.Byte, it.Cursed ? 1 : 0, 0);
        Sync(s, "Plot", GffType.Byte, it.Plot ? 1 : 0, 0);
        // Read as 0 when absent (ItemFactory). A player's items always carry both, as in the original's saves: files
        // OpenWinter wrote before left them out when 1, so a missing field on a player's item means such a file
        // (InventorySystem.KeepPlayerItemFlags). Other items write them only when set, so an unchanged blueprint item
        // still serializes as its UTI.
        long flagDefault = world.Inventory.Owner(it) is Creature { IsPC: true } ? -1 : 0;
        Sync(s, "Dropable", GffType.Byte, it.Droppable ? 1 : 0, flagDefault);
        Sync(s, "Pickpocketable", GffType.Byte, it.Pickpocketable ? 1 : 0, flagDefault);

        if (it.Properties.Count > 0 || t.Has("PropertiesList"))
        {
            var old = t.GetList("PropertiesList");
            var list = new List<GffStruct>();
            // The original writes cast-spell properties first, then the rest, each in their own order.
            var ordered = it.Properties.Where(p => p.Type == 15).Concat(it.Properties.Where(p => p.Type != 15)).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var p = ordered[i];
                var e = i < old.Count && old[i].GetInt("PropertyName", -1) == p.Type ? old[i].DeepClone() : new GffStruct(0);
                // Every field, as the original writes them (struct ids count up).
                e.StructType = (uint)i;
                Sync(e, "PropertyName", GffType.Word, p.Type, long.MinValue);
                Sync(e, "Subtype", GffType.Word, p.SubType, long.MinValue);
                Sync(e, "CostTable", GffType.Byte, p.CostTable, long.MinValue);
                Sync(e, "CostValue", GffType.Word, p.CostTableValue, long.MinValue);
                Sync(e, "Param1", GffType.Byte, p.Param1, long.MinValue);
                Sync(e, "Param1Value", GffType.Byte, p.Param1Value, long.MinValue);
                Sync(e, "ChanceAppear", GffType.Byte, p.ChanceToAppear, long.MinValue);
                Sync(e, "Useable", GffType.Byte, p.Useable ? 1 : 0, long.MinValue);
                Sync(e, "UsesPerDay", GffType.Byte, p.UsesPerDay, long.MinValue);
                if (e.Has("Tag")) Str(e, "Tag", GffType.ExoString, p.Tag);
                else if (!e.Has("CustomTag")) e.Fields.Add(new GffField("CustomTag", GffType.ExoString, p.Tag));
                else Str(e, "CustomTag", GffType.ExoString, p.Tag);
                list.Add(e);
            }
            s.SetList("PropertiesList", list);
        }
        if (it.Inventory.Count > 0 || t.Has("ItemList")) s.SetList("ItemList", InventoryList(world, it.Inventory, newestFirst));
        // An item on the ground carries where it lies (in an inventory its grid position is written by the list).
        if (it.Area != null && it.Possessor == GameObject.InvalidId)
        {
            s.SetFloat("XPosition", it.Position.X);
            s.SetFloat("YPosition", it.Position.Y);
            s.SetFloat("ZPosition", it.Position.Z);
            s.SetFloat("XOrientation", MathF.Cos(it.Facing));
            s.SetFloat("YOrientation", MathF.Sin(it.Facing));
            s.SetFloat("ZOrientation", 0f);
        }
        WriteCommon(s, it);
        return s;
    }

    /// <summary>An inventory list, in the order the items went in, or newest first (<see cref="Serialize"/>).</summary>
    static List<GffStruct> InventoryList(World world, List<Item> items, bool newestFirst)
    {
        var list = new List<GffStruct>();
        int i = 0;
        foreach (var item in newestFirst ? Enumerable.Reverse(items) : items)
        {
            if (item.IsDestroyed) continue;
            // Keep the struct id an item had in a stored inventory list; new entries are numbered.
            var e = ItemGff(world, item, item.Template.Has("Repos_PosX") ? item.Template.StructType : (uint)i, newestFirst);
            // Inventory grid position: where the item sits now; items never placed keep a stored one or get rows.
            if (item.InvX >= 0 && item.InvY >= 0)
            {
                e.SetWord("Repos_PosX", item.InvX);
                e.SetWord("Repos_Posy", item.InvY);
            }
            if (!e.Has("Repos_PosX")) e.SetWord("Repos_PosX", i % 10);
            if (!e.Has("Repos_Posy")) e.SetWord("Repos_Posy", i / 10);
            list.Add(e);
            i++;
        }
        return list;
    }

    public static GffStruct CreatureGff(World world, Creature c, uint structType, bool asCharacter, bool newestFirst = false)
    {
        var t = c.Template;
        var s = t.DeepClone();
        s.StructType = structType;
        var tlk = world.Resources.Talk;

        Str(s, "Tag", GffType.ExoString, c.Tag);
        Str(s, "TemplateResRef", GffType.ResRef, c.ResRef);
        string templateName = string.Join(" ", new[] { t.GetLocString("FirstName").Resolve(tlk), t.GetLocString("LastName").Resolve(tlk) }.Where(x => x.Length > 0));
        if (c.Name.Resolve(tlk) != templateName)
        {
            s.SetLocString("FirstName", new LocString(c.Name.Resolve(tlk)));
            s.SetLocString("LastName", new LocString());
        }
        if (c.Description != t.GetLocString("Description").FirstString) s.SetLocString("Description", new LocString(c.Description));

        // Effects aren't saved, so a polymorphed creature is saved in its own form.
        var poly = Rules.EffectSystem.Polymorph(c);
        Sync(s, "Appearance_Type", GffType.Word, poly?.OldAppearance ?? c.AppearanceType, 0);
        Sync(s, "Gender", GffType.Byte, c.Gender, 0);
        Sync(s, "Phenotype", GffType.Int, c.Phenotype, 0);
        Sync(s, "Race", GffType.Byte, poly?.OldRace ?? c.Race, 0);
        Str(s, "Portrait", GffType.ResRef, poly?.OldPortrait ?? c.Portrait);
        Sync(s, "PortraitId", GffType.Word, poly?.OldPortraitId ?? c.PortraitId, 0);
        Str(s, "Conversation", GffType.ResRef, c.Conversation);
        Sync(s, "FactionID", GffType.Word, c.Faction, 0);
        // A polymorphed creature's hit points as they will be in its own form.
        var (currentHp, maxHp, bonus) = Rules.EffectSystem.OwnHitPoints(world, c, bonus: asCharacter);
        if (asCharacter)
        {
            // Character files keep current hit points without Constitution/feat bonus HP, and the real value in
            // PregameCurrent (which loading prefers); the rolled hit dice total goes in HitPoints.
            s.Set("CurrentHitPoints", GffType.Short, (short)Math.Clamp(currentHp - bonus, short.MinValue, short.MaxValue));
            s.Set("PregameCurrent", GffType.Short, (short)Math.Clamp(currentHp, short.MinValue, short.MaxValue));
        }
        else
        {
            Sync(s, "CurrentHitPoints", GffType.Short, currentHp, 1);
            // Template current hit points get the Constitution bonus added when the rules first touch the
            // creature; once that happened, PregameCurrent keeps the real value so loading doesn't add it twice.
            if (c.RulesInitialised) s.Set("PregameCurrent", GffType.Short, (short)Math.Clamp(currentHp, short.MinValue, short.MaxValue));
        }
        // The rolled hit dice total: loading takes the maximum from it (a levelled henchman keeps its new dice).
        if (c.RulesInitialised && c.BaseHitPoints > 0) s.Set("HitPoints", GffType.Short, (short)Math.Clamp(c.BaseHitPoints, 0, short.MaxValue));
        Sync(s, "MaxHitPoints", GffType.Short, maxHp, 1);
        Sync(s, "Plot", GffType.Byte, c.Plot ? 1 : 0, 0);
        Sync(s, "IsImmortal", GffType.Byte, c.IsImmortal ? 1 : 0, 0);
        Sync(s, "Tail_New", GffType.DWord, c.Tail, t.GetInt("Tail"));
        Sync(s, "Wings_New", GffType.DWord, c.Wings, t.GetInt("Wings"));

        for (int i = 0; i < CreatureFactory.BodyPartFields.Length; i++)
        {
            var label = CreatureFactory.BodyPartFields[i];
            if (i == 17 && !t.Has("BodyPart_RFoot") && t.Has("ArmorPart_RFoot")) label = "ArmorPart_RFoot";
            Wide(s, label, c.BodyParts[i], 1);
        }
        string[] colors = { "Color_Skin", "Color_Hair", "Color_Tattoo1", "Color_Tattoo2" };
        for (int i = 0; i < colors.Length && i < c.Colors.Length; i++) Sync(s, colors[i], GffType.Byte, c.Colors[i], 0);
        foreach (var (evt, script) in c.Scripts) Str(s, evt, GffType.ResRef, script);

        WriteStats(s, t, c);
        QuickbarPersistence.Write(world, c, s);

        // A polymorphed creature is saved in its own form: its own equipment, not the form's items.
        var equipment = Rules.EffectSystem.OwnEquipment(c);
        if (equipment.Count > 0 || t.Has("Equip_ItemList"))
            s.SetList("Equip_ItemList", equipment.Select(kv => ItemGff(world, kv.Value, (uint)kv.Key, newestFirst)).ToList());
        if (c.Inventory.Count > 0 || t.Has("ItemList")) s.SetList("ItemList", InventoryList(world, c.Inventory, newestFirst));

        if (c.Area != null)
        {
            s.SetFloat("XPosition", c.Position.X);
            s.SetFloat("YPosition", c.Position.Y);
            s.SetFloat("ZPosition", c.Position.Z);
            s.SetFloat("XOrientation", MathF.Cos(c.Facing));
            s.SetFloat("YOrientation", MathF.Sin(c.Facing));
        }
        if (asCharacter)
        {
            Sync(s, "IsPC", GffType.Byte, 1, 0);
            if (PersistenceSystem.CharacterDbStruct(c) is { } db) s.SetStruct("SQLite", db);
        }
        WriteCommon(s, c);
        return s;
    }

    static void WriteStats(GffStruct s, GffStruct t, Creature c)
    {
        string[] abilities = { "Str", "Dex", "Con", "Int", "Wis", "Cha" };
        for (int i = 0; i < 6; i++) Sync(s, abilities[i], GffType.Byte, c.Abilities[i], 10);

        // Class list: keep the stored list when nothing changed (it carries fields the engine doesn't model).
        var oldClasses = t.GetList("ClassList");
        var classes = new List<GffStruct>();
        for (int i = 0; i < c.Classes.Count; i++)
        {
            var cc = c.Classes[i];
            var e = i < oldClasses.Count && oldClasses[i].GetInt("Class") == cc.Class ? oldClasses[i].DeepClone() : new GffStruct(2);
            Sync(e, "Class", GffType.Int, cc.Class, long.MinValue);
            Sync(e, "ClassLevel", GffType.Short, cc.Level, long.MinValue);
            Sync(e, "Domain1", GffType.Byte, cc.Domain1, -1);
            Sync(e, "Domain2", GffType.Byte, cc.Domain2, -1);
            Sync(e, "School", GffType.Byte, cc.School, -1);
            for (int lvl = 0; lvl < 10; lvl++)
            {
                var knownLabel = $"KnownList{lvl}";
                var stored = e.GetList(knownLabel).Select(k => (int)k.GetInt("Spell")).ToList();
                if (!stored.SequenceEqual(cc.KnownSpells[lvl]))
                {
                    if (cc.KnownSpells[lvl].Count == 0) e.Remove(knownLabel);
                    else e.SetList(knownLabel, cc.KnownSpells[lvl].Select(sp => new GffStruct(3).SetWord("Spell", sp)).ToList());
                }
                var memLabel = $"MemorizedList{lvl}";
                var storedMem = e.GetList(memLabel).Select(m => ((int)m.GetInt("Spell"), (int)m.GetInt("SpellMetaMagic"), m.GetInt("Ready", 1) != 0)).ToList();
                if (!storedMem.SequenceEqual(cc.Memorized[lvl]))
                {
                    if (cc.Memorized[lvl].Count == 0) e.Remove(memLabel);
                    else e.SetList(memLabel, cc.Memorized[lvl].Select(m => new GffStruct(3).SetWord("Spell", m.Spell)
                        .SetByte("SpellFlags", 1).SetByte("SpellMetaMagic", m.Meta).SetInt("Ready", m.Ready ? 1 : 0)).ToList());
                }
            }
            classes.Add(e);
        }
        if (classes.Count > 0 || t.Has("ClassList")) s.SetList("ClassList", classes);

        var oldSkills = t.GetList("SkillList");
        int lastSkill = Array.FindLastIndex(c.Skills, r => r != 0);
        int skillCount = oldSkills.Count > 0 ? Math.Max(oldSkills.Count, lastSkill + 1) : lastSkill + 1;
        if (skillCount > 0 || t.Has("SkillList"))
            s.SetList("SkillList", Enumerable.Range(0, skillCount).Select(i =>
            {
                var e = i < oldSkills.Count ? oldSkills[i].DeepClone() : new GffStruct(0);
                Sync(e, "Rank", GffType.Byte, (byte)(i < c.Skills.Length ? c.Skills[i] : 0), long.MinValue);
                return e;
            }).ToList());

        var oldFeats = t.GetList("FeatList");
        bool sameFeats = oldFeats.Select(f => (int)f.GetInt("Feat")).SequenceEqual(c.Feats);
        if (!sameFeats || c.FeatUses.Count > 0)
            s.SetList("FeatList", c.Feats.Select((f, i) =>
            {
                var e = sameFeats ? oldFeats[i].DeepClone() : new GffStruct(1);
                Sync(e, "Feat", GffType.Word, f, long.MinValue);
                if (c.FeatUses.TryGetValue(f, out var uses)) Sync(e, "Uses", GffType.Byte, uses, long.MinValue);
                return e;
            }).ToList());

        Sync(s, "LawfulChaotic", GffType.Byte, c.LawfulChaotic, 50);
        Sync(s, "GoodEvil", GffType.Byte, c.GoodEvil, 50);
        Sync(s, "Gold", GffType.DWord, c.Gold, 0);
        Sync(s, "Experience", GffType.DWord, c.Experience, 0);
        Sync(s, "NaturalAC", GffType.Byte, c.NaturalAC, 0);
        Sync(s, "fortbonus", GffType.Short, c.FortBonus, 0);
        Sync(s, "refbonus", GffType.Short, c.RefBonus, 0);
        Sync(s, "willbonus", GffType.Short, c.WillBonus, 0);
        Sync(s, "Age", GffType.Int, c.Age, 0);
        Str(s, "Subrace", GffType.ExoString, c.Subrace);
        Str(s, "Deity", GffType.ExoString, c.Deity);
        Sync(s, "SoundSetFile", GffType.Word, Rules.EffectSystem.Polymorph(c)?.OldSoundSet ?? c.SoundSet, 0);
        Sync(s, "CreatureSize", GffType.Int, Rules.EffectSystem.Polymorph(c)?.OldSize ?? c.Size, 3);
        SyncFloat(s, "ChallengeRating", c.ChallengeRating, 0);
        Sync(s, "Disarmable", GffType.Byte, c.Disarmable ? 1 : 0, 1);
        Sync(s, "Lootable", GffType.Byte, c.Lootable ? 1 : 0, 0);
        Sync(s, "IsDestroyable", GffType.Byte, c.IsDestroyable ? 1 : 0, 1);
        Sync(s, "IsRaiseable", GffType.Byte, c.IsRaiseable ? 1 : 0, 1);
        Sync(s, "NoPermDeath", GffType.Byte, c.NoPermDeath ? 1 : 0, 0);
        Sync(s, "Interruptable", GffType.Byte, c.Interruptable ? 1 : 0, 1);
        Sync(s, "DecayTime", GffType.DWord, c.DecayTime, 5000);
        Sync(s, "BodyBag", GffType.Byte, c.BodyBag, 0);
        Sync(s, "PerceptionRange", GffType.Byte, c.PerceptionRange, 11);
        if (c.BaseAttackOverride >= 0)
        {
            Sync(s, "OverrideBAB", GffType.Byte, 1, long.MinValue);
            Sync(s, "BaseAttackBonus", GffType.Byte, c.BaseAttackOverride, long.MinValue);
        }
        else Sync(s, "OverrideBAB", GffType.Byte, 0, 0);
        Sync(s, "StealthMode", GffType.Byte, c.StealthMode ? 1 : 0, 0);
        Sync(s, "DetectMode", GffType.Byte, c.DetectMode ? 1 : 0, 0);
    }

    public static GffStruct PlaceableGff(World world, Placeable p, bool newestFirst = false)
    {
        var t = p.Template;
        var s = t.DeepClone();
        s.StructType = RootStruct;
        Str(s, "Tag", GffType.ExoString, p.Tag);
        Str(s, "TemplateResRef", GffType.ResRef, p.ResRef);
        if (!SameLoc(p.Name, t.GetLocString("LocName"))) s.SetLocString("LocName", p.Name.Clone());
        if (p.Description != t.GetLocString("Description").FirstString) s.SetLocString("Description", new LocString(p.Description));
        Sync(s, "Appearance", GffType.DWord, p.Appearance, 0);
        Sync(s, "Static", GffType.Byte, p.Static ? 1 : 0, 0);
        Sync(s, "Useable", GffType.Byte, p.Useable ? 1 : 0, 0);
        Sync(s, "HasInventory", GffType.Byte, p.HasInventory ? 1 : 0, 0);
        Sync(s, "Plot", GffType.Byte, p.Plot ? 1 : 0, 0);
        Sync(s, "Locked", GffType.Byte, p.Locked ? 1 : 0, 0);
        Sync(s, "OpenLockDC", GffType.Byte, p.OpenLockDC, t.GetInt("OpenLockDC"));
        Sync(s, "CloseLockDC", GffType.Byte, p.CloseLockDC, t.GetInt("CloseLockDC"));
        Str(s, "KeyName", GffType.ExoString, p.KeyName);
        Sync(s, "KeyRequired", GffType.Byte, p.KeyRequired, t.GetInt("KeyRequired"));
        Sync(s, "TrapFlag", GffType.Byte, p.TrapFlag, t.GetInt("TrapFlag"));
        Sync(s, "TrapType", GffType.Byte, p.TrapType, t.GetInt("TrapType"));
        Sync(s, "TrapDetectDC", GffType.Byte, p.TrapDetectDC, t.GetInt("TrapDetectDC"));
        Sync(s, "TrapDisarmDC", GffType.Byte, p.TrapDisarmDC, t.GetInt("TrapDisarmDC"));
        Sync(s, "HP", GffType.Short, p.HitPoints, 0);
        Sync(s, "CurrentHP", GffType.Short, p.CurrentHitPoints, 0);
        long anim = t.GetInt("AnimationState");
        Sync(s, "AnimationState", GffType.Byte, p.IsOpen ? 1 : anim == 1 ? 2 : anim, anim);
        foreach (var (evt, script) in p.Scripts) Str(s, evt, GffType.ResRef, script);
        if (p.Inventory.Count > 0 || t.Has("ItemList")) s.SetList("ItemList", InventoryList(world, p.Inventory, newestFirst));
        if (p.Area != null || t.Has("X") || t.Has("Bearing"))
        {
            s.SetFloat("X", p.Position.X);
            s.SetFloat("Y", p.Position.Y);
            s.SetFloat("Z", p.Position.Z);
            s.SetFloat("Bearing", p.Facing);
        }
        WriteCommon(s, p);
        return s;
    }

    static void WriteCommon(GffStruct s, GameObject o)
    {
        WriteLocals(s, o.Locals);
        if (o.Uuid != null) s.SetString("UUID", o.Uuid);
    }

    /// <summary>
    /// A VarTable as the original writes it: 1 int, 2 float, 3 string, 4 object, 5 location (struct), 7 json (a struct
    /// with the text in "JSON"; measured in the original's save games, parity scene nativesave).
    /// </summary>
    public static void WriteLocals(GffStruct s, LocalVariables v)
    {
        int count = v.Ints.Count + v.Floats.Count + v.Strings.Count + v.Objects.Count + v.Locations.Count + v.Jsons.Count;
        if (count == 0)
        {
            if (s.Has("VarTable")) s.SetList("VarTable", new List<GffStruct>());
            return;
        }
        var list = new List<GffStruct>();
        GffStruct Entry(string name, int type) => new GffStruct(0).SetString("Name", name).SetDWord("Type", (uint)type);
        foreach (var (k, x) in v.Ints) list.Add(Entry(k, 1).SetInt("Value", x));
        foreach (var (k, x) in v.Floats) list.Add(Entry(k, 2).SetFloat("Value", x));
        foreach (var (k, x) in v.Strings) list.Add(Entry(k, 3).SetString("Value", x));
        foreach (var (k, x) in v.Objects) list.Add(Entry(k, 4).SetDWord("Value", x));
        foreach (var (k, x) in v.Locations)
        {
            float rad = x.FacingDegrees * MathF.PI / 180f;
            list.Add(Entry(k, 5).SetStruct("Value", new GffStruct(1).SetDWord("Area", x.Area)
                .SetFloat("PositionX", x.Position.X).SetFloat("PositionY", x.Position.Y).SetFloat("PositionZ", x.Position.Z)
                .SetFloat("OrientationX", MathF.Cos(rad)).SetFloat("OrientationY", MathF.Sin(rad)).SetFloat("OrientationZ", 0)));
        }
        foreach (var (k, x) in v.Jsons) list.Add(Entry(k, 7).SetStruct("Value", new GffStruct(3).SetString("JSON", x)));
        s.SetList("VarTable", list);
    }

    static void ClearLocals(LocalVariables v)
    {
        v.Ints.Clear(); v.Floats.Clear(); v.Strings.Clear(); v.Objects.Clear(); v.Locations.Clear(); v.Jsons.Clear();
    }

    /// <summary>Replaces the variables with a VarTable's (<see cref="LocalVariables.LoadFrom"/>).</summary>
    public static void ReadLocals(GffStruct s, LocalVariables v)
    {
        ClearLocals(v);
        v.LoadFrom(s);
    }

    // ---- Reading ---------------------------------------------------------------------------

    /// <summary>
    /// Creates an object from serialized GFF. Items go into <paramref name="owner"/>'s inventory
    /// when it can hold them, otherwise to <paramref name="at"/>; everything else needs a valid location.
    /// Items always keep their state; anything else only reads it with <paramref name="loadObjectState"/>.
    /// A creature fires OnAcquireItem for what it carries and then OnSpawn, but no area OnEnter (as in the original).
    /// A combined-format area becomes a new area instance (the location is ignored, as in the original).
    /// </summary>
    public static GameObject? Deserialize(World world, GffStruct s, string fileType, Location at, GameObject? owner, bool loadObjectState = false)
    {
        string type = fileType.Trim().ToUpperInvariant();
        if (type is "GFF" or "") type = s.Has("BaseItem") ? "UTI" : s.Has("Appearance_Type") ? "UTC" : s.Has("Appearance") && s.Has("Static") ? "UTP" : type;
        if (type == "CAF") return DeserializeArea(world, s);
        var o = DeserializeObject(world, s, type, at, owner, loadObjectState);
        if (o != null && (loadObjectState || o is Item)) ObjectStateGff.Read(world, o, s);
        return o;
    }

    static GameObject? DeserializeObject(World world, GffStruct s, string type, Location at, GameObject? owner, bool loadObjectState)
    {
        var area = world.Get(at.Area) as Area;
        float facing = at.FacingDegrees * MathF.PI / 180f;
        switch (type)
        {
            case "UTI":
            {
                // The original refuses items whose base item isn't a baseitems.2da row.
                if (s.GetInt("BaseItem", -1) is var baseItem && (baseItem < 0 || baseItem >= world.Resources.TwoDA("baseitems").RowCount)) return null;
                var item = world.Items.FromTemplate(s);
                FixItem(world, item);
                world.Register(item);
                if (owner != null && GiveItem(world, owner, item))
                {
                    world.Inventory.Acquired(owner, item);
                    return item;
                }
                if (area == null) { DestroyItem(world, item); return null; }
                world.AddToArea(item, area, at.Position, facing);
                return item;
            }
            case "UTC" or "BIC":
            {
                if (area == null) return null;
                var c = world.Creatures.FromTemplate(s);
                foreach (var item in c.Equipment.Values.Concat(c.Inventory)) { FixItem(world, item); item.Possessor = c.Id; }
                if (loadObjectState) ReadLocals(s, c.Locals);
                else ClearLocals(c.Locals);
                world.Register(c);
                foreach (var item in c.Equipment.Values.Concat(c.Inventory)) item.Possessor = c.Id;
                ClaimUuid(world, c, s);
                var pos = at.Position;
                if (area.Walkmesh.GroundHeight(pos.X, pos.Y, pos.Z, 2f) is { } z) pos.Z = z;
                world.Inventory.FireSpawnAcquires(c);
                world.AddToArea(c, area, pos, facing, enterEvent: false);
                world.QueueSpawn(c);
                return c;
            }
            case "UTP":
            {
                if (area == null || world.Loader == null) return null;
                var p = world.Loader.CreatePlaceable(area, s, at.Position, GameObject.BearingFor(facing));
                p.OpenLockDC = (int)s.GetInt("OpenLockDC");
                p.CloseLockDC = (int)s.GetInt("CloseLockDC");
                p.KeyName = s.GetString("KeyName");
                p.KeyRequired = (int)s.GetInt("KeyRequired");
                p.TrapFlag = (int)s.GetInt("TrapFlag");
                p.TrapType = (int)s.GetInt("TrapType");
                p.TrapDetectDC = (int)s.GetInt("TrapDetectDC");
                p.TrapDisarmDC = (int)s.GetInt("TrapDisarmDC");
                p.IsOpen = s.GetInt("AnimationState") == 1;
                if (loadObjectState) ReadLocals(s, p.Locals);
                else ClearLocals(p.Locals);
                // The serialized contents replace the blueprint's (which would otherwise be added when first opened).
                p.InventoryLoaded = true;
                if (p.Inventory.Count == 0)
                    foreach (var entry in s.GetList("ItemList"))
                        if (world.Items.FromListEntry(entry) is { } item)
                        {
                            FixItem(world, item);
                            world.Register(item);
                            item.Possessor = p.Id;
                            p.Inventory.Add(item);
                        }
                ClaimUuid(world, p, s);
                world.AddToArea(p, area, at.Position, facing);
                return p;
            }
            default:
                return area == null ? null : DeserializePlaced(world, s, type, area, at.Position, facing, loadObjectState);
        }
    }

    /// <summary>Completes what ItemFactory leaves out: all local types, property tags, UUIDs (recursively for containers).</summary>
    static void FixItem(World world, Item item)
    {
        var s = item.Template;
        ReadLocals(s, item.Locals);
        var props = s.GetList("PropertiesList");
        for (int i = 0; i < item.Properties.Count && i < props.Count; i++)
            if (props[i].GetString("CustomTag") is { Length: > 0 } tag) item.Properties[i].Tag = tag;
        world.Register(item);
        ClaimUuid(world, item, s);
        foreach (var inner in item.Inventory) { FixItem(world, inner); inner.Possessor = item.Id; }
    }

    static void ClaimUuid(World world, GameObject o, GffStruct s)
    {
        if (s.GetString("UUID") is { Length: > 0 } uuid) world.System<PersistenceSystem>().TryClaim(o, uuid);
    }

    /// <summary>Puts an item into a creature's, placeable's, store's or container's inventory. False if the owner cannot hold items.</summary>
    public static bool GiveItem(World world, GameObject owner, Item item)
    {
        switch (owner)
        {
            case Creature c: c.Inventory.Add(item); break;
            case Placeable p when p.HasInventory: p.Inventory.Add(item); break;
            case Store st: st.Inventory.Add(item); break;
            case Item container when container != item && world.Resources.TwoDA("baseitems").GetInt(container.BaseItem, "Container") != 0:
                container.Inventory.Add(item);
                break;
            default: return false;
        }
        item.Possessor = owner.Id;
        world.MarkChanged(owner);
        return true;
    }

    static void DestroyItem(World world, Item item)
    {
        foreach (var inner in item.Inventory.ToList()) DestroyItem(world, inner);
        world.Destroy(item);
    }

    // ---- Field helpers ---------------------------------------------------------------------

    static bool IsInteger(GffType t) => t is GffType.Byte or GffType.Char or GffType.Word or GffType.Short or GffType.DWord or GffType.Int or GffType.DWord64 or GffType.Int64;

    static object Cast(GffType t, long v) => t switch
    {
        GffType.Byte => unchecked((byte)v),
        GffType.Char => unchecked((sbyte)v),
        GffType.Word => unchecked((ushort)v),
        GffType.Short => unchecked((short)v),
        GffType.DWord => unchecked((uint)v),
        GffType.Int => unchecked((int)v),
        GffType.DWord64 => unchecked((ulong)v),
        GffType.Int64 => v,
        GffType.Float => (float)v,
        GffType.Double => (double)v,
        _ => unchecked((int)v),
    };

    /// <summary>
    /// Writes an integer field, keeping the stored field's width. When the field is absent and
    /// the value equals what a reader would assume (<paramref name="readDefault"/>), nothing is added,
    /// so unchanged objects serialize to exactly their template.
    /// </summary>
    static void Sync(GffStruct s, string label, GffType type, long value, long readDefault)
    {
        var f = s.Field(label);
        if (f == null)
        {
            if (value == readDefault) return;
            s.Fields.Add(new GffField(label, type, Cast(type, value)));
            return;
        }
        if (IsInteger(f.Type) || f.Type is GffType.Float or GffType.Double) type = f.Type;
        if (f.Value is IConvertible && Convert.ToInt64(f.Value) == value && f.Type == type) return;
        f.Type = type;
        f.Value = Cast(type, value);
    }

    static void SyncFloat(GffStruct s, string label, float value, float readDefault)
    {
        var f = s.Field(label);
        if (f == null) { if (value != readDefault) s.SetFloat(label, value); return; }
        if (f.Type == GffType.Double) f.Value = (double)value;
        else { f.Type = GffType.Float; f.Value = value; }
    }

    static void Str(GffStruct s, string label, GffType type, string value)
    {
        var f = s.Field(label);
        if (f == null) { if (value.Length > 0) s.Fields.Add(new GffField(label, type, value)); return; }
        if (f.Value is LocString) { if (f.Value is LocString l && l.FirstString != value) f.Value = new LocString(value); return; }
        if (f.Type is not (GffType.ExoString or GffType.ResRef)) f.Type = type;
        f.Value = value;
    }

    /// <summary>Body part numbers: EE keeps values above 255 in an "x"-prefixed word field.</summary>
    static void Wide(GffStruct s, string label, int value, int readDefault)
    {
        if (value > 255 || s.Has("x" + label))
        {
            Sync(s, "x" + label, GffType.Word, value, long.MinValue);
            Sync(s, label, GffType.Byte, Math.Min(value, 255), long.MinValue);
        }
        else Sync(s, label, GffType.Byte, value, readDefault);
    }

    static bool SameLoc(LocString a, LocString b) =>
        ReferenceEquals(a, b) || (a.StrRef == b.StrRef && a.Strings.Count == b.Strings.Count && a.Strings.All(kv => b.Strings.TryGetValue(kv.Key, out var v) && v == kv.Value));
}
