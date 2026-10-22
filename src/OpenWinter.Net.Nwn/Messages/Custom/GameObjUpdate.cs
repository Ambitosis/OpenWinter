using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateDeleteMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using ObjectType = OpenWinter.Net.Nwn.Messages.NwnExternalTypes.ObjectType;

namespace OpenWinter.Net.Nwn.Messages;

public static partial class GameObjUpdateMessages
{
    public const uint InvalidObjectId = 0x7F000000;
    public const int VisualEffectCow = 999;

    /// <summary>A GUI entry of the object list, with the element and operation letters it was sent with.</summary>
    public sealed record ObjListGuiEntry(char Element, char Op, INwnStruct Value);

    /// <summary>
    /// The object list: adds, updates and deletes of the objects a player knows about, appearance and player info,
    /// work-remaining progress and GUI panel contents, each entry introduced by a letter.
    /// </summary>
    public sealed partial class GameObjUpdateObjList : NwnMessage
    {
        public const byte MajorId = 0x05;
        public const byte MinorId = 0x01;
        public override byte Major => MajorId;
        public override byte Minor => MinorId;
        public override NwnDirection Direction => NwnDirection.ServerToClient;

        [NwnField("entries")]
        public List<object> Entries { get; set; } = new();

        public override void Read(NwnReader r)
        {
            Entries = new();
            while (!r.AtEnd)
            {
                var kind = (char)(byte)r.ReadChar();
                Entries.Add(kind switch
                {
                    'A' => ReadAdd(r),
                    'U' => ReadUpdate(r),
                    'D' => ReadDelete(r),
                    'P' => r.ReadStruct<GameObjUpdateObjListAppearanceCreature>(),
                    'I' => r.ReadStruct<GameObjUpdateObjListPlayerInfo>(),
                    'W' => r.ReadStruct<ObjListWorkRemaining>(),
                    'G' => ReadGui(r),
                    _ => throw new NwnWireException($"unknown object list entry '{kind}'"),
                });
            }
        }

        static INwnStruct ReadAdd(NwnReader r)
        {
            var type = (ObjectType)r.ReadByte();
            INwnStruct entry = type switch
            {
                ObjectType.Creature => r.ReadStruct<GameObjUpdateObjListAddCreature>(),
                ObjectType.Door => r.ReadStruct<GameObjUpdateObjListAddDoor>(),
                ObjectType.Placeable => r.ReadStruct<GameObjUpdateObjListAddPlaceable>(),
                ObjectType.Item => r.ReadStruct<GameObjUpdateObjListAddItem>(),
                ObjectType.Trigger => r.ReadStruct<GameObjUpdateObjListAddTrigger>(),
                ObjectType.Areaofeffectobject => r.ReadStruct<GameObjUpdateObjListAddAOE>(),
                _ => throw new NwnWireException($"unsupported added object type {type}"),
            };
            // Trigger updates are laid out differently for traps; remember which triggers are.
            if (entry is GameObjUpdateObjListAddTrigger trigger) r.Context.TriggerIsTrap[trigger.ObjectId] = trigger.IsTrap;
            return entry;
        }

        static INwnStruct ReadUpdate(NwnReader r)
        {
            var type = (ObjectType)r.ReadByte();
            if (type == ObjectType.Creature) return r.ReadStruct<GameObjUpdateObjListUpdateCreature>();
            var entry = GameObjUpdateObjListUpdateGeneric.Create(type)
                ?? throw new NwnWireException($"unsupported updated object type {type}");
            entry.Read(r);
            return entry;
        }

        static INwnStruct ReadDelete(NwnReader r)
        {
            var type = (ObjectType)r.ReadByte();
            return type switch
            {
                ObjectType.Creature => r.ReadStruct<ObjListDeleteCreature>(),
                ObjectType.Door => r.ReadStruct<ObjListDeleteDoor>(),
                ObjectType.Placeable => r.ReadStruct<ObjListDeletePlaceable>(),
                ObjectType.Item => r.ReadStruct<ObjListDeleteItem>(),
                ObjectType.Trigger => r.ReadStruct<ObjListDeleteTrigger>(),
                ObjectType.Areaofeffectobject => r.ReadStruct<ObjListDeleteAOE>(),
                _ => throw new NwnWireException($"unsupported deleted object type {type}"),
            };
        }

        static ObjListGuiEntry ReadGui(NwnReader r)
        {
            var element = (char)(byte)r.ReadChar();
            switch (element)
            {
                case 'S':
                    return new(element, '\0', r.ReadStruct<GuiCharacterSheet>());
                case 'Q':
                    return new(element, '\0', r.ReadStruct<GuiQuickbarUseCount>());
            }
            var op = (char)(byte)r.ReadChar();
            INwnStruct? value = (element, op) switch
            {
                ('I' or 'i', 'A') => r.ReadStruct<GuiInventorySlotAdd>(),
                ('I' or 'i', 'D') => r.ReadStruct<GuiInventorySlotDelete>(),
                ('I' or 'i', 'U') => r.ReadStruct<GuiInventorySlotUpdate>(),
                ('R' or 'r' or 'B' or 'A', 'A') => r.ReadStruct<GuiRepositoryItemAdd>(),
                ('R' or 'r' or 'B' or 'A', 'D') => r.ReadStruct<GuiRepositoryItemDelete>(),
                ('R' or 'r' or 'B' or 'A', 'U') => r.ReadStruct<GuiRepositoryItemUpdate>(),
                ('R' or 'r' or 'B' or 'A', 'M') => r.ReadStruct<GuiRepositoryItemMove>(),
                ('C' or 'c', 'P') => r.ReadStruct<GuiContainerPages>(),
                ('C' or 'c', 'A') => r.ReadStruct<GuiContainerItemAdd>(),
                ('C' or 'c', 'D') => r.ReadStruct<GuiContainerItemDelete>(),
                ('C' or 'c', 'U') => r.ReadStruct<GuiContainerItemUpdate>(),
                ('C' or 'c', 'M') => r.ReadStruct<GuiContainerItemMove>(),
                ('M', 'P') => r.ReadStruct<GuiStorePages>(),
                ('M', 'A') => r.ReadStruct<GuiStoreItemAdd>(),
                ('M', 'D') => r.ReadStruct<GuiStoreItemDelete>(),
                ('M', 'U') => r.ReadStruct<GuiStoreReposition>(),
                _ => null,
            };
            return new(element, op, value ?? throw new NwnWireException($"unsupported GUI entry '{element}{op}'"));
        }

        public override void Write(NwnWriter w)
        {
            foreach (var entry in Entries)
            {
                switch (entry)
                {
                    case ObjListGuiEntry gui:
                        w.WriteChar((sbyte)'G');
                        w.WriteChar((sbyte)gui.Element);
                        if (gui.Op != '\0') w.WriteChar((sbyte)gui.Op);
                        w.WriteStruct(gui.Value);
                        break;
                    case GameObjUpdateObjListAppearanceCreature:
                        WriteEntry(w, 'P', null, entry);
                        break;
                    case GameObjUpdateObjListPlayerInfo:
                        WriteEntry(w, 'I', null, entry);
                        break;
                    case ObjListWorkRemaining:
                        WriteEntry(w, 'W', null, entry);
                        break;
                    case GameObjUpdateObjListUpdateCreature:
                        WriteEntry(w, 'U', ObjectType.Creature, entry);
                        break;
                    case GameObjUpdateObjListUpdateGeneric generic:
                        WriteEntry(w, 'U', generic.ObjectType, entry);
                        break;
                    default:
                        if (AddType(entry) is { } added) WriteEntry(w, 'A', added, entry);
                        else if (DeleteType(entry) is { } deleted) WriteEntry(w, 'D', deleted, entry);
                        else throw new NwnWireException($"can't write object list entry {entry.GetType().Name}");
                        break;
                }
            }
        }

        static void WriteEntry(NwnWriter w, char kind, ObjectType? type, object entry)
        {
            w.WriteChar((sbyte)kind);
            if (type is { } t) w.WriteByte((byte)t);
            w.WriteStruct((INwnStruct)entry);
        }

        static ObjectType? AddType(object entry) => entry switch
        {
            GameObjUpdateObjListAddCreature => ObjectType.Creature,
            GameObjUpdateObjListAddDoor => ObjectType.Door,
            GameObjUpdateObjListAddPlaceable => ObjectType.Placeable,
            GameObjUpdateObjListAddItem => ObjectType.Item,
            GameObjUpdateObjListAddTrigger => ObjectType.Trigger,
            GameObjUpdateObjListAddAOE => ObjectType.Areaofeffectobject,
            _ => null,
        };

        static ObjectType? DeleteType(object entry) => entry switch
        {
            ObjListDeleteCreature => ObjectType.Creature,
            ObjListDeleteDoor => ObjectType.Door,
            ObjListDeletePlaceable => ObjectType.Placeable,
            ObjListDeleteItem => ObjectType.Item,
            ObjListDeleteTrigger => ObjectType.Trigger,
            ObjListDeleteAOE => ObjectType.Areaofeffectobject,
            _ => null,
        };
    }

    /// <summary>A visual effect on an object or location. Beams and projectiles (visualeffects.2da Type_FD P or B) also
    /// carry a source.</summary>
    public sealed partial class GameObjUpdateVisEffect : NwnMessage
    {
        public const byte MajorId = 0x05;
        public const byte MinorId = 0x03;
        public override byte Major => MajorId;
        public override byte Minor => MinorId;
        public override NwnDirection Direction => NwnDirection.ServerToClient;

        [NwnField("target_id")]
        public uint TargetId { get; set; }
        [NwnField("visual_effect_id")]
        public ushort VisualEffectId { get; set; }
        [NwnField("target_location")]
        public (float, float, float) TargetLocation { get; set; }
        [NwnField("source_id")]
        public uint SourceId { get; set; } = InvalidObjectId;
        [NwnField("source_node")]
        public byte SourceNode { get; set; }
        [NwnField("target_node")]
        public byte TargetNode { get; set; }
        [NwnField("source_location")]
        public (float, float, float) SourceLocation { get; set; }
        [NwnField("duration")]
        public float Duration { get; set; }
        [NwnField("vt")]
        public NetTypesMessages.VisualTransformData? Vt { get; set; }

        static bool UsesSource(int effect, INwnContext context)
        {
            if (effect == VisualEffectCow) return true;
            try
            {
                return context.TwoDA("visualeffects", effect, "Type_FD") is "P" or "B";
            }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentException or KeyNotFoundException)
            {
                return false;
            }
        }

        public override void Read(NwnReader r)
        {
            TargetId = r.ReadObjectId();
            VisualEffectId = r.ReadWord();
            TargetLocation = (r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            if (UsesSource(VisualEffectId, r.Context))
            {
                SourceId = r.ReadObjectId();
                SourceNode = r.ReadByte();
                TargetNode = r.ReadByte();
                SourceLocation = (r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
                if (TargetId == InvalidObjectId) Duration = r.ReadFloat();
            }
            Vt = r.ReadStruct<NetTypesMessages.VisualTransformData>();
        }

        public override void Write(NwnWriter w)
        {
            w.WriteObjectId(TargetId);
            w.WriteWord(VisualEffectId);
            w.WriteFloat(TargetLocation.Item1);
            w.WriteFloat(TargetLocation.Item2);
            w.WriteFloat(TargetLocation.Item3);
            if (UsesSource(VisualEffectId, w.Context))
            {
                w.WriteObjectId(SourceId);
                w.WriteByte(SourceNode);
                w.WriteByte(TargetNode);
                w.WriteFloat(SourceLocation.Item1);
                w.WriteFloat(SourceLocation.Item2);
                w.WriteFloat(SourceLocation.Item3);
                if (TargetId == InvalidObjectId) w.WriteFloat(Duration);
            }
            w.WriteStruct(Vt ?? new NetTypesMessages.VisualTransformData());
        }
    }
}
