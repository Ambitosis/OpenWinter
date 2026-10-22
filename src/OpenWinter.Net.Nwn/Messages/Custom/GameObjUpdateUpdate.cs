using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateSharedMessages;
using ObjectType = OpenWinter.Net.Nwn.Messages.NwnExternalTypes.ObjectType;

namespace OpenWinter.Net.Nwn.Messages;

public static partial class GameObjUpdateUpdateMessages
{
    /// <summary>A facing: a bearing, or a full direction vector. Creatures also say whether they face a lock-on target.</summary>
    public sealed class OrientationUpdate : INwnStruct
    {
        [NwnField("full")]
        public bool Full { get; set; }
        [NwnField("bearing")]
        public float? Bearing { get; set; }
        [NwnField("ox")]
        public float? Ox { get; set; }
        [NwnField("oy")]
        public float? Oy { get; set; }
        [NwnField("oz")]
        public float? Oz { get; set; }
        [NwnField("lock_on")]
        public bool LockOn { get; set; }
        [NwnField("lock_target")]
        public uint? LockTarget { get; set; }

        public void Read(NwnReader r) => Read(r, creature: true);
        public void Write(NwnWriter w) => Write(w, creature: true);

        internal void Read(NwnReader r, bool creature)
        {
            Full = r.ReadBool();
            if (!Full) Bearing = r.ReadFloat(NwnFloatEncoding.Quantised(10f, 12));
            else
            {
                Ox = r.ReadFloat(NwnFloatEncoding.Ranged(-2f, 2f, 16));
                Oy = r.ReadFloat(NwnFloatEncoding.Ranged(-2f, 2f, 16));
                Oz = r.ReadFloat(NwnFloatEncoding.Ranged(-2f, 2f, 16));
            }
            if (!creature) return;
            LockOn = r.ReadBool();
            if (LockOn) LockTarget = r.ReadObjectId();
        }

        internal void Write(NwnWriter w, bool creature)
        {
            w.WriteBool(Full);
            if (!Full) w.WriteFloat(Bearing ?? 0, NwnFloatEncoding.Quantised(10f, 12));
            else
            {
                w.WriteFloat(Ox ?? 0, NwnFloatEncoding.Ranged(-2f, 2f, 16));
                w.WriteFloat(Oy ?? 0, NwnFloatEncoding.Ranged(-2f, 2f, 16));
                w.WriteFloat(Oz ?? 0, NwnFloatEncoding.Ranged(-2f, 2f, 16));
            }
            if (!creature) return;
            w.WriteBool(LockOn);
            if (LockOn) w.WriteObjectId(LockTarget ?? 0);
        }
    }

    /// <summary>A party member's name: the creature's own first and last names, or a replacement text.</summary>
    public sealed class NewPartyMemberName : INwnStruct
    {
        [NwnField("original_name")]
        public bool OriginalName { get; set; }
        /// <summary>A <see cref="NwnLocString"/> for the original name, otherwise a string.</summary>
        [NwnField("first_name")]
        public object FirstName { get; set; } = "";
        [NwnField("last_name")]
        public NwnLocString? LastName { get; set; }

        public void Read(NwnReader r)
        {
            OriginalName = r.ReadBool();
            if (OriginalName)
            {
                FirstName = r.ReadLocString();
                LastName = r.ReadLocString();
            }
            else
            {
                FirstName = r.ReadString();
                LastName = null;
            }
        }

        public void Write(NwnWriter w)
        {
            w.WriteBool(OriginalName);
            if (OriginalName)
            {
                w.WriteLocString(FirstName is NwnLocString l ? l : NwnLocString.FromText(FirstName?.ToString() ?? ""));
                w.WriteLocString(LastName ?? NwnLocString.FromText(""));
            }
            else w.WriteString(FirstName is NwnLocString l ? l.Text : FirstName?.ToString() ?? "");
        }
    }

    /// <summary>A creature's path: its waypoints and, when driven, the slide factor.</summary>
    public sealed class CreatureWaypointUpdate : INwnStruct
    {
        [NwnField("num_waypoints")]
        public ushort NumWaypoints { get; set; }
        [NwnField("drive_mode")]
        public bool DriveMode { get; set; }
        [NwnField("drive_mode_factor")]
        public float? DriveModeFactor { get; set; }
        [NwnField("waypoints")]
        public List<(float, float)> Waypoints { get; set; } = new();

        public void Read(NwnReader r)
        {
            NumWaypoints = r.ReadWord();
            DriveMode = false;
            DriveModeFactor = null;
            if (NumWaypoints > 0)
            {
                DriveMode = r.ReadBool();
                if (DriveMode) DriveModeFactor = r.ReadFloat();
            }
            Waypoints = new(NumWaypoints);
            for (int i = 0; i < NumWaypoints; i++) Waypoints.Add((r.ReadFloat(), r.ReadFloat()));
        }

        public void Write(NwnWriter w)
        {
            w.WriteWord(NumWaypoints);
            if (NumWaypoints > 0)
            {
                w.WriteBool(DriveMode);
                if (DriveMode) w.WriteFloat(DriveModeFactor ?? 0);
            }
            foreach (var (x, y) in Waypoints)
            {
                w.WriteFloat(x);
                w.WriteFloat(y);
            }
        }
    }

    /// <summary>
    /// An update of a door, placeable, item, trigger or area of effect. The common flags come first, then the fields
    /// only some object types have (locks and traps, names, an item's hidden state).
    /// </summary>
    public class GameObjUpdateObjListUpdateGeneric : INwnStruct
    {
        const uint ItemHiddenFlag = 0x40;

        [NwnField("object_type")]
        public ObjectType ObjectType { get; set; }
        [NwnField("object_id")]
        public uint ObjectId { get; set; }
        [NwnField("flags")]
        public uint Flags { get; set; }
        [NwnField("position")]
        public (float, float, float)? Position { get; set; }
        [NwnField("orientation")]
        public OrientationUpdate? Orientation { get; set; }
        [NwnField("visual_transform")]
        public NetTypesMessages.VisualTransformData? VisualTransform { get; set; }
        [NwnField("shader_params")]
        public List<MaterialShaderParam>? ShaderParams { get; set; }
        [NwnField("portrait_id")]
        public ushort? PortraitId { get; set; }
        [NwnField("custom_portrait")]
        public string? CustomPortrait { get; set; }
        [NwnField("hilite_r")]
        public float? HiliteR { get; set; }
        [NwnField("hilite_g")]
        public float? HiliteG { get; set; }
        [NwnField("hilite_b")]
        public float? HiliteB { get; set; }
        [NwnField("cursor")]
        public int? Cursor { get; set; }
        [NwnField("useable")]
        public bool? Useable { get; set; }
        [NwnField("discovery_mask")]
        public int? DiscoveryMask { get; set; }
        [NwnField("bubble_type")]
        public int? BubbleType { get; set; }
        [NwnField("bubble_text")]
        public string? BubbleText { get; set; }
        [NwnField("textures")]
        public List<(string, string)>? Textures { get; set; }
        [NwnField("anim_replacements")]
        public List<(string, string)>? AnimReplacements { get; set; }
        [NwnField("anim_speed")]
        public float? AnimSpeed { get; set; }
        [NwnField("animation")]
        public ushort? Animation { get; set; }
        [NwnField("effects")]
        public List<VisualEffectUpdate>? Effects { get; set; }
        [NwnField("trap_hostile")]
        public bool? TrapHostile { get; set; }
        [NwnField("trapped")]
        public bool? Trapped { get; set; }
        [NwnField("locked")]
        public bool? Locked { get; set; }
        [NwnField("lockable")]
        public bool? Lockable { get; set; }
        [NwnField("recoverable")]
        public bool? Recoverable { get; set; }
        [NwnField("clickable")]
        public bool? Clickable { get; set; }
        /// <summary>A <see cref="NwnLocString"/> for the original name, a string for a replacement name.</summary>
        [NwnField("name")]
        public object? Name { get; set; }
        [NwnField("hidden")]
        public bool? Hidden { get; set; }

        public static GameObjUpdateObjListUpdateGeneric? Create(ObjectType type) => type switch
        {
            ObjectType.Door => new ObjListUpdateDoor(),
            ObjectType.Placeable => new ObjListUpdatePlaceable(),
            ObjectType.Item => new ObjListUpdateItem(),
            ObjectType.Trigger => new ObjListUpdateTrigger(),
            ObjectType.Areaofeffectobject => new ObjListUpdateAOE(),
            _ => null,
        };

        bool Has(GameObjUpdateType flag) => (Flags & (uint)flag) != 0;

        public void Read(NwnReader r)
        {
            ObjectId = r.ReadObjectId();
            Flags = r.ReadDword();
            if (Has(GameObjUpdateType.ObjectPositionFlag)) Position = (r.ReadFloat(), r.ReadFloat(), r.ReadFloat());
            if (Has(GameObjUpdateType.ObjectOrientationFlag))
            {
                Orientation = new OrientationUpdate();
                Orientation.Read(r, creature: false);
            }
            if (Has(GameObjUpdateType.ObjectVisualTransformFlag))
                VisualTransform = r.ReadStruct<NetTypesMessages.VisualTransformData>();
            if (Has(GameObjUpdateType.ObjectMaterialShaderParametersFlag))
            {
                int n = r.ReadWord();
                ShaderParams = new(n);
                for (int i = 0; i < n; i++) ShaderParams.Add(r.ReadStruct<MaterialShaderParam>());
            }
            if (Has(GameObjUpdateType.ObjectPortraitFlag))
            {
                PortraitId = r.ReadWord();
                if (PortraitId >= 0xFFFE) CustomPortrait = r.ReadResRef();
            }
            if (Has(GameObjUpdateType.ObjectUiFeedbackFlag))
            {
                HiliteR = r.ReadFloat();
                HiliteG = r.ReadFloat();
                HiliteB = r.ReadFloat();
                Cursor = r.ReadInt();
                Useable = r.ReadBool();
                DiscoveryMask = r.ReadInt();
                BubbleType = r.ReadInt();
                BubbleText = r.ReadString();
            }
            if (Has(GameObjUpdateType.ObjectTexturesFlag)) Textures = ReadPairs(r);
            if (Has(GameObjUpdateType.ObjectAnimationReplaceFlag)) AnimReplacements = ReadPairs(r);
            if (Has(GameObjUpdateType.ObjectAnimationFlag))
            {
                AnimSpeed = r.ReadFloat();
                Animation = r.ReadWord();
            }
            if (Has(GameObjUpdateType.ObjectVisualEffectFlag))
            {
                int n = r.ReadWord();
                Effects = new(n);
                for (int i = 0; i < n; i++) Effects.Add(r.ReadStruct<VisualEffectUpdate>());
            }
            ReadTypeSpecific(r);
        }

        static List<(string, string)> ReadPairs(NwnReader r)
        {
            var n = (int)r.ReadDword();
            var list = new List<(string, string)>(Math.Max(0, n));
            for (int i = 0; i < n; i++) list.Add((r.ReadResRef(), r.ReadResRef()));
            return list;
        }

        void ReadTypeSpecific(NwnReader r)
        {
            switch (ObjectType)
            {
                case ObjectType.Door:
                case ObjectType.Placeable:
                    if (Has(GameObjUpdateType.ObjectTrapsAndLocksFlag))
                    {
                        TrapHostile = r.ReadBool();
                        Trapped = r.ReadBool();
                        Locked = r.ReadBool();
                        Lockable = r.ReadBool();
                        Recoverable = r.ReadBool();
                        if (ObjectType == ObjectType.Door) Clickable = r.ReadBool();
                    }
                    if (Has(GameObjUpdateType.ObjectNameFlag)) Name = ReadName(r);
                    break;
                case ObjectType.Trigger:
                    if (Has(GameObjUpdateType.ObjectTrapsAndLocksFlag))
                    {
                        if (!r.Context.TriggerIsTrap.TryGetValue(ObjectId, out var isTrap))
                            throw new NwnWireException($"update of trigger {ObjectId:X} before it was added");
                        if (isTrap)
                        {
                            TrapHostile = r.ReadBool();
                            Recoverable = r.ReadBool();
                        }
                        else Clickable = r.ReadBool();
                    }
                    break;
                case ObjectType.Item:
                    if (Has(GameObjUpdateType.ObjectNameFlag)) Name = ReadName(r);
                    if ((Flags & ItemHiddenFlag) != 0) Hidden = r.ReadBool();
                    break;
            }
        }

        static object ReadName(NwnReader r) => r.ReadBool() ? r.ReadLocString() : r.ReadString();

        public void Write(NwnWriter w)
        {
            w.WriteObjectId(ObjectId);
            w.WriteDword(Flags);
            if (Has(GameObjUpdateType.ObjectPositionFlag))
            {
                var (x, y, z) = Position.GetValueOrDefault();
                w.WriteFloat(x);
                w.WriteFloat(y);
                w.WriteFloat(z);
            }
            if (Has(GameObjUpdateType.ObjectOrientationFlag)) (Orientation ?? new()).Write(w, creature: false);
            if (Has(GameObjUpdateType.ObjectVisualTransformFlag)) w.WriteStruct(VisualTransform ?? new());
            if (Has(GameObjUpdateType.ObjectMaterialShaderParametersFlag))
            {
                var list = ShaderParams ?? [];
                w.WriteWord((ushort)list.Count);
                foreach (var p in list) w.WriteStruct(p);
            }
            if (Has(GameObjUpdateType.ObjectPortraitFlag))
            {
                w.WriteWord(PortraitId ?? 0);
                if (PortraitId >= 0xFFFE) w.WriteResRef(CustomPortrait ?? "");
            }
            if (Has(GameObjUpdateType.ObjectUiFeedbackFlag))
            {
                w.WriteFloat(HiliteR ?? 0);
                w.WriteFloat(HiliteG ?? 0);
                w.WriteFloat(HiliteB ?? 0);
                w.WriteInt(Cursor ?? 0);
                w.WriteBool(Useable ?? false);
                w.WriteInt(DiscoveryMask ?? 0);
                w.WriteInt(BubbleType ?? 0);
                w.WriteString(BubbleText ?? "");
            }
            if (Has(GameObjUpdateType.ObjectTexturesFlag)) WritePairs(w, Textures);
            if (Has(GameObjUpdateType.ObjectAnimationReplaceFlag)) WritePairs(w, AnimReplacements);
            if (Has(GameObjUpdateType.ObjectAnimationFlag))
            {
                w.WriteFloat(AnimSpeed ?? 0);
                w.WriteWord(Animation ?? 0);
            }
            if (Has(GameObjUpdateType.ObjectVisualEffectFlag))
            {
                var list = Effects ?? [];
                w.WriteWord((ushort)list.Count);
                foreach (var e in list) w.WriteStruct(e);
            }
            WriteTypeSpecific(w);
        }

        static void WritePairs(NwnWriter w, List<(string, string)>? pairs)
        {
            var list = pairs ?? [];
            w.WriteDword((uint)list.Count);
            foreach (var (from, to) in list)
            {
                w.WriteResRef(from);
                w.WriteResRef(to);
            }
        }

        void WriteTypeSpecific(NwnWriter w)
        {
            switch (ObjectType)
            {
                case ObjectType.Door:
                case ObjectType.Placeable:
                    if (Has(GameObjUpdateType.ObjectTrapsAndLocksFlag))
                    {
                        w.WriteBool(TrapHostile ?? false);
                        w.WriteBool(Trapped ?? false);
                        w.WriteBool(Locked ?? false);
                        w.WriteBool(Lockable ?? false);
                        w.WriteBool(Recoverable ?? false);
                        if (ObjectType == ObjectType.Door) w.WriteBool(Clickable ?? false);
                    }
                    if (Has(GameObjUpdateType.ObjectNameFlag)) WriteName(w);
                    break;
                case ObjectType.Trigger:
                    if (Has(GameObjUpdateType.ObjectTrapsAndLocksFlag))
                    {
                        if (Clickable is { } clickable) w.WriteBool(clickable);
                        else
                        {
                            w.WriteBool(TrapHostile ?? false);
                            w.WriteBool(Recoverable ?? false);
                        }
                    }
                    break;
                case ObjectType.Item:
                    if (Has(GameObjUpdateType.ObjectNameFlag)) WriteName(w);
                    if ((Flags & ItemHiddenFlag) != 0) w.WriteBool(Hidden ?? false);
                    break;
            }
        }

        void WriteName(NwnWriter w)
        {
            if (Name is NwnLocString loc)
            {
                w.WriteBool(true);
                w.WriteLocString(loc);
            }
            else
            {
                w.WriteBool(false);
                w.WriteString(Name?.ToString() ?? "");
            }
        }
    }

    public sealed class ObjListUpdateDoor : GameObjUpdateObjListUpdateGeneric
    {
        public ObjListUpdateDoor() => ObjectType = ObjectType.Door;
    }

    public sealed class ObjListUpdatePlaceable : GameObjUpdateObjListUpdateGeneric
    {
        public ObjListUpdatePlaceable() => ObjectType = ObjectType.Placeable;
    }

    public sealed class ObjListUpdateItem : GameObjUpdateObjListUpdateGeneric
    {
        public ObjListUpdateItem() => ObjectType = ObjectType.Item;
    }

    public sealed class ObjListUpdateTrigger : GameObjUpdateObjListUpdateGeneric
    {
        public ObjListUpdateTrigger() => ObjectType = ObjectType.Trigger;
    }

    public sealed class ObjListUpdateAOE : GameObjUpdateObjListUpdateGeneric
    {
        public ObjListUpdateAOE() => ObjectType = ObjectType.Areaofeffectobject;
    }
}
