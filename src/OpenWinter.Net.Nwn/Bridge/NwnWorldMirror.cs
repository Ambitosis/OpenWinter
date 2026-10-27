using System.Numerics;
using System.Text.Json;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateDeleteMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using UpdateFlag = OpenWinter.Net.Nwn.Messages.GameObjUpdateSharedMessages.GameObjUpdateType;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>What the bridge needs from the client's game data: 2DA cells and talk table text.</summary>
public interface INwnBridgeData
{
    int TwoDARowCount(string table);
    string TwoDA(string table, int row, string column);
    string Text(uint strRef, byte gender);
    /// <summary>The server's haks and custom talk table, once it has named them.</summary>
    void UseContent(IReadOnlyList<string> haks, string customTlk);
    /// <summary>A resource's bytes (a voice set's .ssf); null when there is none.</summary>
    byte[]? Resource(string resref, string extension) => null;
    /// <summary>Whether a resource of this type (ResType) is installed.</summary>
    bool HasResource(string resref, ushort type) => false;
}

/// <summary>
/// The client's view of the server's world, rebuilt from NWN:EE messages and expressed as OpenWinter server messages:
/// areas become LoadArea, the object list becomes Spawn/Update/Appearance/Despawn, chat becomes Chat.
/// </summary>
public sealed class NwnWorldMirror(INwnBridgeData data, Action<byte[]> emit)
{
    /// <summary>NWN animation codes in object updates: standing (0, 1), walking (2), running (4).</summary>
    const ushort AnimWalk = 2, AnimRun = 4;

    /// <summary>A door's animation state: closed (22); any other state (51 after opening) is open. A door state
    /// change asks for open (21) or close (22).</summary>
    public const ushort DoorClosed = 22, DoorOpen = 21;

    // NWN armour-part order (nwscript.nss CREATURE_PART_*): right foot, left foot, right shin, left shin, left thigh,
    // right thigh, pelvis, torso, belt, neck, right forearm, left forearm, right bicep, left bicep, right shoulder,
    // left shoulder, right hand, left hand, robe. These are its indices for OpenWinter's body-part order after the head.
    static readonly int[] BodyPartFromNwn = [9, 7, 6, 8, 14, 15, 12, 13, 10, 11, 16, 17, 5, 4, 2, 3, 0, 1];
    const int NwnRobe = 18;
    const int PropertyLight = 44;

    sealed class Mirror(uint id, ObjectKind kind)
    {
        public readonly uint Id = id;
        public ObjectKind Kind = kind;
        public bool Spawned;
        public bool HasAppearance;
        public Vector3 Position;
        public float Facing;
        public string Name = "";
        public string Portrait = "";
        public Appearance Appearance = new();
        public readonly Dictionary<int, ItemVisual> Equipment = [];
        public ObjectFlags Flags;
        public float WalkRate = 2f, RunRate = 4f;
        public NwnMover? Mover;
        public MovementMode Mode;
        public bool InCombat;
        public bool AppearanceChanged, StateChanged;
    }

    readonly Dictionary<uint, Mirror> _objects = [];
    readonly HashSet<Mirror> _touched = [];
    uint? _pendingControl;
    int _minutesPerHour = 2;

    int _areaWidth, _areaHeight;
    AreaMapPin[] _pins = [];

    public uint AreaId { get; private set; }
    /// <summary>The creature the player controls (0 until the server names it).</summary>
    public uint Controlled { get; private set; }

    /// <summary>
    /// The player is driving the controlled creature, which the client moves ahead of the server: the server's own
    /// position, facing and gait for it are ignored while it only trails the client along the driven path. A
    /// position off that path (blocked by an obstacle, a jump or teleport) is taken, and driving goes on from there.
    /// </summary>
    public bool Driving
    {
        get => _driving;
        set
        {
            _driving = value;
            _driven.Clear();
        }
    }

    /// <summary>How far the server's position may be from the recently driven path and still count as its echo.</summary>
    public const float DriveTolerance = 0.6f;

    /// <summary>Driven positions kept for comparison: the server trails by up to a few tenths of a second.</summary>
    const int DrivenKept = 4;

    bool _driving;
    readonly Queue<Vector3> _driven = new();
    public uint LoadToken { get; private set; }
    /// <summary>The start of the current area, for the player's first steps.</summary>
    public Vector3 AreaStart { get; private set; }

    public void ModuleInfo(ModuleMessages.ModuleInfo info) => _minutesPerHour = Math.Max(1, (int)info.MinutesPerHour);

    // -- areas --------------------------------------------------------------

    /// <summary>A new area: everything known is dropped, then the area and its static objects are described.</summary>
    public void Area(AreaMessages.AreaClientArea a)
    {
        foreach (var m in _objects.Values.Where(m => m.Spawned)) emit(new DespawnMessage { ObjectId = m.Id }.Write());
        _objects.Clear();
        _touched.Clear();
        AreaId = a.ObjectId;
        (_areaWidth, _areaHeight) = (a.Width, a.Height);
        _pins = a.MapNotes.Where(n => n.Enabled)
            .Select(n => new AreaMapPin(n.ObjectId.ToString(), Text(n.Note), n.Position.Item1, n.Position.Item2, false)).ToArray();
        AreaStart = new Vector3(a.Location.Item1, a.Location.Item2, a.Location.Item3);
        LoadToken++;
        var ambient = a.AmbientSound;
        emit(new LoadAreaMessage
        {
            AreaId = a.ObjectId,
            LoadToken = LoadToken,
            ResRef = a.Resref,
            Name = a.IsDisplayName ? a.DisplayName : Text(a.Name),
            Tileset = a.Tileset,
            Width = a.Width,
            Height = a.Height,
            Tiles = a.Tiles.Select(t => new TileInfo
            {
                Id = t.Id,
                Orientation = (byte)t.Orientation,
                Height = (sbyte)t.Height,
                MainLight1 = t.MainLight1,
                MainLight2 = t.MainLight2,
                SourceLight1 = t.SourceLight1,
                SourceLight2 = t.SourceLight2,
                AnimLoops = (byte)((t.AnimLoop1 != 0 ? 1 : 0) | (t.AnimLoop2 != 0 ? 2 : 0) | (t.AnimLoop3 != 0 ? 4 : 0)),
            }).ToArray(),
            IsNight = a.IsNight,
            IsInterior = (a.Flags & AreaMessages.AreaFlag.Interior) != 0,
            IsUnderground = (a.Flags & AreaMessages.AreaFlag.Underground) != 0,
            IsNatural = (a.Flags & AreaMessages.AreaFlag.Natural) != 0,
            SunAmbient = a.SunAmbientColor,
            SunDiffuse = a.SunDiffuseColor,
            SunFogColor = a.SunFogColor,
            MoonAmbient = a.MoonAmbientColor,
            MoonDiffuse = a.MoonDiffuseColor,
            MoonFogColor = a.MoonFogColor,
            SunFogAmount = a.SunFogAmount,
            MoonFogAmount = a.MoonFogAmount,
            SunShadows = a.SunShadows,
            MoonShadows = a.MoonShadows,
            FogClipDistance = a.FogClipDistance,
            SkyBox = a.Skybox.ToString(),
            MusicDay = ambient.MusicDayTrack,
            MusicNight = ambient.MusicNightTrack,
            MusicBattle = ambient.BattleTrack,
            AmbientDay = ambient.SoundDayTrack,
            AmbientNight = ambient.SoundNightTrack,
            AmbientDayVolume = ambient.DayVolume,
            AmbientNightVolume = ambient.NightVolume,
            MusicDelay = ambient.MusicDelay,
            DayNightCycle = a.UseDaynightCycle,
            ShadowOpacity = (byte)Math.Clamp((int)a.ShadowOpacity, 0, 100),
        }.Write());
        emit(new TimeMessage { Hour = a.Hour, MinutesPerHour = _minutesPerHour, DawnHour = a.Dawn, DuskHour = a.Dusk }.Write());
        foreach (var p in a.StaticPlaceables)
        {
            var m = Get(p.ObjectId, ObjectKind.Placeable);
            m.Appearance = new Appearance { Type = p.Appearance };
            m.Position = V(p.Position);
            m.Facing = Facing(p.Orientation);
            m.Flags = ObjectFlags.Static;
            m.HasAppearance = true;
        }
        Flush();
        // The area and its static content are described: the client may build it.
        emit(new NotifyMessage { Kind = "area-snapshot-ready", Subject = a.ObjectId, Json = JsonSerializer.Serialize(new AreaSnapshotReadyNotice(LoadToken)) }.Write());
    }

    public void Time(ModuleMessages.ModuleTime t)
    {
        if (t.Hour is { } hour) emit(new TimeMessage { Hour = hour, MinutesPerHour = _minutesPerHour }.Write());
    }

    // -- the object list ----------------------------------------------------

    public void ObjectList(GameObjUpdateMessages.GameObjUpdateObjList list)
    {
        foreach (var entry in list.Entries)
        {
            switch (entry)
            {
                case GameObjUpdateObjListAddCreature c:
                {
                    var m = Get(c.ObjectId, ObjectKind.Creature);
                    m.Position = V(c.Position);
                    m.Facing = Facing(c.Orientation);
                    m.Mode = MovementMode.Stand;
                    m.StateChanged = true;
                    break;
                }
                case GameObjUpdateObjListAddPlaceable p:
                {
                    var m = Get(p.ObjectId, ObjectKind.Placeable);
                    m.Name = p.OriginalName ? Text(p.LocName) : p.StrName ?? "";
                    m.Appearance = new Appearance { Type = p.Appearance };
                    m.Flags = (p.Useable ? ObjectFlags.Useable | ObjectFlags.Selectable : 0) | (p.Locked ? ObjectFlags.Locked : 0)
                        | (p.Plot ? ObjectFlags.Plot : 0) | (p.LightIsOn ? ObjectFlags.Activated : 0);
                    PlaceableAnim(m, p.Animation);
                    m.HasAppearance = true;
                    break;
                }
                case GameObjUpdateObjListAddDoor d:
                {
                    var m = Get(d.ObjectId, ObjectKind.Door);
                    m.Name = d.OriginalName ? Text(d.LocName) : d.StrName ?? "";
                    m.Appearance = new Appearance { Type = (int)d.AppearanceType, GenericDoorType = (int)(d.GenericType ?? 0) };
                    m.Flags = ObjectFlags.Selectable | (d.Locked ? ObjectFlags.Locked : 0) | (d.Plot ? ObjectFlags.Plot : 0)
                        | (d.Clickable ? ObjectFlags.Transition : 0);
                    DoorAnim(m, d.Anim);
                    m.HasAppearance = true;
                    break;
                }
                case GameObjUpdateObjListAddItem i:
                {
                    var m = Get(i.ObjectId, ObjectKind.Item);
                    m.Name = ItemName(i.Item);
                    m.Appearance = new Appearance { Type = (int)i.Item.BaseItemId, Equipment = { Visual(0, i.Item) } };
                    m.Flags = ObjectFlags.Selectable;
                    m.HasAppearance = true;
                    break;
                }
                case GameObjUpdateObjListAppearanceCreature a:
                    CreatureAppearance(a);
                    break;
                case GameObjUpdateObjListUpdateCreature u:
                    CreatureUpdate(u);
                    break;
                case GameObjUpdateObjListUpdateGeneric g:
                    GenericUpdate(g);
                    break;
                case GameObjUpdateObjListPlayerInfo { Automap: { } map }:
                    Explored(map);
                    break;
                case ObjListDeleteGeneric or ObjListDeleteCreature or ObjListDeleteDoor or ObjListDeletePlaceable
                    or ObjListDeleteItem or ObjListDeleteTrigger or ObjListDeleteAOE:
                    Delete(DeletedId(entry));
                    break;
            }
        }
        Flush();
    }

    static uint DeletedId(object entry) => entry switch
    {
        ObjListDeleteGeneric e => e.ObjectId,
        ObjListDeleteCreature e => e.ObjectId,
        ObjListDeleteDoor e => e.ObjectId,
        ObjListDeletePlaceable e => e.ObjectId,
        ObjListDeleteItem e => e.ObjectId,
        ObjListDeleteTrigger e => e.ObjectId,
        ObjListDeleteAOE e => e.ObjectId,
        _ => 0,
    };

    void CreatureAppearance(GameObjUpdateObjListAppearanceCreature a)
    {
        var m = Get(a.ObjectId, ObjectKind.Creature);
        var f = a.Flags;
        bool Has(UpdateCreatureAppearanceFlags flag) => (f & flag) == flag;
        var app = m.Appearance;
        if (app.BodyParts.Length != 19) app.BodyParts = new ushort[19];
        if (app.Colors.Length != 4) app.Colors = new byte[4];
        if (Has(UpdateCreatureAppearanceFlags.Name) && a.Name?.Value is { } name)
            m.Name = name switch
            {
                OriginalCreatureName o => $"{Text(o.First)} {Text(o.Last)}".Trim(),
                DisplayCreatureName d => d.Name,
                _ => m.Name,
            };
        // The appearance update's "race" is the appearance.2da row the model comes from.
        if (Has(UpdateCreatureAppearanceFlags.Race)) app.Type = a.Race;
        if (Has(UpdateCreatureAppearanceFlags.Phenotype)) app.Phenotype = a.Phenotype;
        if (Has(UpdateCreatureAppearanceFlags.Gender)) app.Gender = a.Gender;
        if (Has(UpdateCreatureAppearanceFlags.HeadVariation)) app.BodyParts[0] = a.HeadVariation;
        if (Has(UpdateCreatureAppearanceFlags.TailVariation)) app.Tail = (int)a.TailVariation;
        if (Has(UpdateCreatureAppearanceFlags.WingVariation)) app.Wings = (int)a.WingVariation;
        if (Has(UpdateCreatureAppearanceFlags.SkinColor)) app.Colors[0] = a.SkinColor;
        if (Has(UpdateCreatureAppearanceFlags.HairColor)) app.Colors[1] = a.HairColor;
        if (Has(UpdateCreatureAppearanceFlags.Tattoo1)) app.Colors[2] = a.Tattoo1;
        if (Has(UpdateCreatureAppearanceFlags.Tattoo2)) app.Colors[3] = a.Tattoo2;
        if (Has(UpdateCreatureAppearanceFlags.PartInfo) && a.PartInfo is { } parts)
            for (int i = 0; i < BodyPartFromNwn.Length; i++) app.BodyParts[i + 1] = parts.Parts[BodyPartFromNwn[i]];
        if (Has(UpdateCreatureAppearanceFlags.ItemInfo) && a.EquippedItems is { } items)
            foreach (var tagged in items)
            {
                switch (tagged.Value)
                {
                    case EquippedItemAdd add:
                        m.Equipment[(int)add.Slot] = Visual((int)add.Slot, add.Item);
                        break;
                    case EquippedItemDelete del:
                        m.Equipment.Remove((int)del.Slot);
                        break;
                }
            }
        app.Equipment = m.Equipment.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
        m.HasAppearance = true;
        m.AppearanceChanged = true;
    }

    void CreatureUpdate(GameObjUpdateObjListUpdateCreature u)
    {
        var m = Get(u.ObjectId, ObjectKind.Creature);
        if (u.WalkRate is { } walk && walk > 0) m.WalkRate = walk / 1000f;
        if (u.RunRate is { } run && run > 0) m.RunRate = run / 1000f;
        if (Driving && u.ObjectId == Controlled && m.Spawned
            && (u.Position is not { } moved || _driven.Append(m.Position).Any(p => Vector2.Distance(new(p.X, p.Y), new(moved.Item1, moved.Item2)) < DriveTolerance)))
        {
            // The echo of the player's own driving, trailing the client: only combat state is news.
            if (u.InCombat is { } fighting && fighting != m.InCombat) (m.InCombat, m.StateChanged) = (fighting, true);
            return;
        }
        if (u.Position is { } pos)
        {
            m.Position = V(pos);
            m.Mover = null;
        }
        if (u.Orientation is { } o) m.Facing = Facing(o);
        if (u.AnimTypeData is { } anim)
        {
            m.Mode = anim.AnimType switch { AnimWalk => MovementMode.Walk, AnimRun => MovementMode.Run, _ => MovementMode.Stand };
            if (u.Waypoints is { NumWaypoints: > 0 } path && m.Mode != MovementMode.Stand)
            {
                var speed = m.Mode == MovementMode.Run ? m.RunRate : m.WalkRate;
                m.Mover = new NwnMover(m.Position, path.Waypoints.Select(w => new Vector2(w.Item1, w.Item2)).ToList(), speed);
            }
            else if (m.Mode == MovementMode.Stand) m.Mover = null;
        }
        if (u.InCombat is { } combat) m.InCombat = combat;
        if (u.PortraitId is { } portrait) SetPortrait(m, PortraitResRef(portrait, u.CustomPortrait));
        m.StateChanged = true;
    }

    void GenericUpdate(GameObjUpdateObjListUpdateGeneric g)
    {
        var m = Get(g.ObjectId, g.ObjectType switch
        {
            NwnExternalTypes.ObjectType.Door => ObjectKind.Door,
            NwnExternalTypes.ObjectType.Placeable => ObjectKind.Placeable,
            NwnExternalTypes.ObjectType.Item => ObjectKind.Item,
            NwnExternalTypes.ObjectType.Trigger => ObjectKind.Trigger,
            _ => ObjectKind.AreaOfEffect,
        });
        if (g.Position is { } pos) m.Position = V(pos);
        if (g.Orientation is { } o) m.Facing = Facing(o);
        if (g.Name is NwnLocString loc) m.Name = Text(loc);
        else if (g.Name is string s) m.Name = s;
        if (g.Locked is { } locked) m.Flags = locked ? m.Flags | ObjectFlags.Locked : m.Flags & ~ObjectFlags.Locked;
        if (g.Useable is { } useable)
            m.Flags = useable ? m.Flags | ObjectFlags.Useable | ObjectFlags.Selectable : m.Flags & ~ObjectFlags.Useable;
        if (g.Animation is { } anim && m.Kind == ObjectKind.Door) DoorAnim(m, anim);
        else if (g.Animation is { } state && m.Kind == ObjectKind.Placeable) PlaceableAnim(m, state);
        if (g.PortraitId is { } portrait) SetPortrait(m, PortraitResRef(portrait, g.CustomPortrait));
        m.StateChanged = true;
    }

    static void DoorAnim(Mirror m, ushort anim) =>
        m.Flags = anim == NwnAnimations.Destroyed ? m.Flags | ObjectFlags.Dead
            : anim == DoorClosed ? m.Flags & ~ObjectFlags.Open : m.Flags | ObjectFlags.Open;

    /// <summary>A placeable's state: switched on or off, open or closed, destroyed (other codes keep the state).</summary>
    static void PlaceableAnim(Mirror m, ushort anim) => m.Flags = anim switch
    {
        NwnAnimations.On => m.Flags | ObjectFlags.Activated,
        NwnAnimations.Off => m.Flags & ~ObjectFlags.Activated,
        NwnAnimations.Open => m.Flags | ObjectFlags.Open,
        NwnAnimations.Close => m.Flags & ~ObjectFlags.Open,
        NwnAnimations.Destroyed => m.Flags | ObjectFlags.Dead,
        _ => m.Flags,
    };

    /// <summary>
    /// What clicking a door sends: a closed door is asked to open, an open one to close; null when the object isn't a known door, or is an open transition that a
    /// plain use (<paramref name="use"/>) travels through.
    /// </summary>
    public InputMessages.InputChangeDoorState? DoorRequest(uint id, bool use = false) =>
        _objects.TryGetValue(id, out var m) && m.Kind == ObjectKind.Door
            && !(use && (m.Flags & (ObjectFlags.Open | ObjectFlags.Transition)) == (ObjectFlags.Open | ObjectFlags.Transition))
            ? new InputMessages.InputChangeDoorState { Door = id, Animation = (m.Flags & ObjectFlags.Open) != 0 ? DoorClosed : DoorOpen }
            : null;

    /// <summary>
    /// The player's explored tiles: the whole map, a list of explored tiles, a list of changes, or one bit per tile
    /// (row by row from the south-west corner, lowest bit first).
    /// </summary>
    void Explored(AutoMapUpdate map)
    {
        void Notify(string kind, object payload) =>
            emit(new NotifyMessage { Kind = kind, Subject = AreaId, Json = JsonSerializer.Serialize(payload) }.Write());
        if (map.Bitstream && map.RawBytes is { } bits)
        {
            var mask = new char[_areaWidth * _areaHeight];
            for (int i = 0; i < mask.Length; i++) mask[i] = i / 8 < bits.Length && ((bits[i / 8] >> (i % 8)) & 1) != 0 ? '1' : '0';
            Notify("area-map", new AreaMapNotice(AreaId, new string(mask), _pins));
            return;
        }
        if (map.ExploreAll && map.TileCoords.Count == 0)
        {
            Notify("map-explored", new MapExploredNotice(AreaId, true));
            return;
        }
        foreach (var t in map.TileCoords)
        {
            if (map.ExploreAll) Notify("tile-explored", new TileExploredNotice(AreaId, (byte)t[0]!, (byte)t[1]!, true));
            else Notify("tile-explored", new TileExploredNotice(AreaId, (byte)t[1]!, (byte)t[2]!, (bool)t[0]!));
        }
    }

    void Delete(uint id)
    {
        if (!_objects.Remove(id, out var m)) return;
        _touched.Remove(m);
        // A corpse the server removes fades out where it lies.
        if (m.Spawned) emit(new DespawnMessage { ObjectId = id, DecayingCorpse = (m.Flags & ObjectFlags.Dead) != 0 }.Write());
    }

    public void Control(uint objectId)
    {
        Controlled = objectId;
        if (_objects.TryGetValue(objectId, out var m) && m.Spawned)
        {
            emit(new ControlObjectMessage { ObjectId = objectId }.Write());
            _pendingControl = null;
        }
        else _pendingControl = objectId;
    }

    /// <summary>A known object's name; "" when it isn't known.</summary>
    public string NameOf(uint id) => _objects.TryGetValue(id, out var m) ? m.Name : "";

    /// <summary>A portrait that changes after the spawn (SetPortraitId) reaches the client as "object-portrait".</summary>
    void SetPortrait(Mirror m, string portrait)
    {
        if (m.Portrait == portrait) return;
        m.Portrait = portrait;
        if (m.Spawned) emit(new NotifyMessage { Kind = "object-portrait", Subject = m.Id, Json = JsonSerializer.Serialize(new { portrait }) }.Write());
    }

    /// <summary>A portrait id as a texture base name: "po_" + portraits.2da BaseResRef, or the custom resref.</summary>
    string PortraitResRef(ushort id, string? custom) =>
        id >= 0xFFFE ? custom ?? "" : data.TwoDA("portraits", id, "BaseResRef") is { Length: > 0 } b ? "po_" + b.ToLowerInvariant() : "";

    /// <summary>A known creature's appearance and equipment; null when it isn't known.</summary>
    public Appearance? AppearanceOf(uint id) => _objects.TryGetValue(id, out var m) && m.HasAppearance ? m.Appearance : null;

    /// <summary>A known object's flags (dead, dying, open ...); none when it isn't known.</summary>
    public ObjectFlags FlagsOf(uint id) => _objects.TryGetValue(id, out var m) ? m.Flags : 0;

    /// <summary>Sets a creature's condition flags (dead, dying) within <paramref name="mask"/>; sent with the object list being read.</summary>
    public void SetFlags(uint id, ObjectFlags value, ObjectFlags mask)
    {
        var m = Get(id, ObjectKind.Creature);
        var flags = (m.Flags & ~mask) | (value & mask);
        if (flags == m.Flags) return;
        m.Flags = flags;
        m.StateChanged = true;
    }

    /// <summary>The kind of a known object; null when it isn't known.</summary>
    public ObjectKind? KindOf(uint id) => _objects.TryGetValue(id, out var m) ? m.Kind : null;

    /// <summary>Where a spawned object stands, its facing and its walk and run rates.</summary>
    public bool TryPose(uint id, out Vector3 position, out float facing, out float walkRate, out float runRate)
    {
        var found = _objects.TryGetValue(id, out var m) && m.Spawned;
        (position, facing, walkRate, runRate) = found ? (m!.Position, m.Facing, m.WalkRate, m.RunRate) : (default, 0, 0, 0);
        return found;
    }

    /// <summary>Moves a creature ahead of the server (the player's own driving), telling the client at once.</summary>
    public void Predict(uint id, Vector3 position, float facing, MovementMode mode)
    {
        if (!_objects.TryGetValue(id, out var m) || !m.Spawned) return;
        if (Driving && id == Controlled)
        {
            _driven.Enqueue(m.Position);
            while (_driven.Count > DrivenKept) _driven.Dequeue();
        }
        (m.Position, m.Facing, m.Mode, m.Mover) = (position, facing, mode, null);
        emit(UpdateFor(m).Write());
    }

    /// <summary>Moves creatures along their paths; called every frame.</summary>
    public void Tick(float seconds)
    {
        foreach (var m in _objects.Values)
        {
            if (m.Mover is not { } mover || !m.Spawned) continue;
            mover.Advance(seconds);
            m.Position = mover.Position;
            if (mover.Facing is { } facing) m.Facing = facing;
            if (mover.Done)
            {
                m.Mover = null;
                m.Mode = MovementMode.Stand;
            }
            emit(UpdateFor(m).Write());
        }
    }

    // -- output -------------------------------------------------------------

    void Flush()
    {
        foreach (var m in _touched)
        {
            // Placeables and doors are added before their position arrives in the same list; creatures wait for
            // their appearance, which the server sends with the add.
            if (!m.Spawned)
            {
                if (!m.HasAppearance || m.Kind is ObjectKind.Trigger or ObjectKind.AreaOfEffect) continue;
                emit(new SpawnMessage
                {
                    ObjectId = m.Id,
                    Kind = m.Kind,
                    Name = m.Name,
                    Position = m.Position,
                    Facing = m.Facing,
                    Flags = m.Flags | (m.Kind == ObjectKind.Creature ? ObjectFlags.Selectable : 0),
                    Appearance = m.Appearance,
                    WalkSpeed = m.WalkRate,
                    RunSpeed = m.RunRate,
                    Portrait = m.Portrait,
                }.Write());
                m.Spawned = true;
                m.AppearanceChanged = m.StateChanged = false;
                if (_pendingControl == m.Id) Control(m.Id);
                continue;
            }
            if (m.AppearanceChanged) emit(new AppearanceMessage { ObjectId = m.Id, Appearance = m.Appearance }.Write());
            if (m.StateChanged) emit(UpdateFor(m).Write());
            m.AppearanceChanged = m.StateChanged = false;
        }
        _touched.Clear();
    }

    UpdateMessage UpdateFor(Mirror m) => new()
    {
        ObjectId = m.Id,
        Position = m.Position,
        Facing = m.Facing,
        Speed = m.Mode switch { MovementMode.Run => m.RunRate, MovementMode.Walk => m.WalkRate, _ => 0 },
        Mode = m.Mode,
        Flags = m.Flags | (m.Kind == ObjectKind.Creature ? ObjectFlags.Selectable : 0),
        InCombat = m.InCombat,
    };

    Mirror Get(uint id, ObjectKind kind)
    {
        if (!_objects.TryGetValue(id, out var m)) _objects[id] = m = new Mirror(id, kind);
        _touched.Add(m);
        return m;
    }

    // -- conversions --------------------------------------------------------

    static Vector3 V((float, float, float) v) => new(v.Item1, v.Item2, v.Item3);

    /// <summary>A direction vector as a facing: radians from +X.</summary>
    static float Facing((float, float, float) v) => MathF.Atan2(v.Item2, v.Item1);

    static float Wrap(float radians) => MathF.IEEERemainder(radians, 2 * MathF.PI);

    /// <summary>An update's facing: a bearing in degrees counter-clockwise from +Y (measured against the legs of
    /// walked paths), or a direction vector.</summary>
    static float Facing(OrientationUpdate o) => o.Full
        ? MathF.Atan2(o.Oy ?? 0, o.Ox ?? 1)
        : Wrap(((o.Bearing ?? 0) + 90f) * MathF.PI / 180f);

    string Text(NwnLocString? s) => s is not { } l ? "" : l.IsStrRef ? data.Text(l.StrRef, l.Gender) : l.Text;

    string ItemName(ItemAppearanceAndProps item) => item.Name?.Value switch
    {
        OriginalItemName o => Text(o.Name),
        DisplayItemName d => d.Name,
        _ => "",
    };

    /// <summary>An item's look as OpenWinter describes it: model parts, colours, armour parts and its light.</summary>
    ItemVisual Visual(int slot, ItemAppearanceAndProps item)
    {
        var v = new ItemVisual { Slot = slot, BaseItem = (int)item.BaseItemId, ModelParts = new ushort[3], Colors = new byte[6], ArmorParts = new ushort[20] };
        switch (item.ModelData?.Value)
        {
            case SimpleModel s:
                v.ModelParts[0] = s.Part1;
                break;
            case LayeredModel l:
                v.ModelParts[0] = l.Part1;
                v.Colors = l.Palette.Take(6).ToArray();
                break;
            case CompositeModel c:
                v.ModelParts = [c.Part1, c.Part2, c.Part3];
                break;
            case ArmorModel a:
                v.Colors = a.Palette.Take(6).ToArray();
                for (int i = 0; i < BodyPartFromNwn.Length; i++) v.ArmorParts[i + 1] = a.Parts[BodyPartFromNwn[i]];
                v.ArmorParts[19] = a.Parts[NwnRobe];
                break;
        }
        var light = item.Properties.FirstOrDefault(p => p.Name == PropertyLight && p.Param1 < ItemVisual.NoLight);
        if (light != null)
        {
            v.LightColor = light.Param1;
            v.LightLevel = (byte)Math.Clamp((int)light.CostTableValue, 0, 254);
        }
        return v;
    }
}
