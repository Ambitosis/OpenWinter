using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Net;

namespace OpenWinter.Game.Objects;

public sealed partial class Creature : GameObject
{
    public override ObjectType Type => ObjectType.Creature;

    public int AppearanceType;
    public byte Gender, Phenotype, Race;
    public ushort[] BodyParts = new ushort[19];
    public byte[] Colors = new byte[4];
    public int Tail, Wings;
    public string Portrait = "";
    public int PortraitId;
    public string Conversation = "";
    /// <summary>The faction; setting a different one makes the creature that faction's newest member.</summary>
    public int Faction
    {
        get => _faction;
        set
        {
            if (value != _faction || FactionJoinOrder == 0) FactionJoinOrder = Interlocked.Increment(ref s_factionJoins);
            _faction = value;
        }
    }
    int _faction;
    static long s_factionJoins;
    /// <summary>
    /// When the creature joined its faction, for the faction's membership order (nwserver, parity suite faction_bestac:
    /// a creature that joins later comes after members made after it).
    /// </summary>
    public long FactionJoinOrder { get; private set; }
    public int CurrentHitPoints = 1, MaxHitPoints = 1;
    public bool IsImmortal, IsDead;
    /// <summary>The full template (UTC/BIC fields) the creature came from; rules read from here until they get typed fields.</summary>
    public GffStruct Template = new();

    /// <summary>Equipped items by slot bit (EquipSlot).</summary>
    public readonly SortedDictionary<int, Item> Equipment = new();
    public readonly List<Item> Inventory = new();

    /// <summary>Controlled by a connected player.</summary>
    public PlayerSession? Player;
    public PlayerSession? FamiliarController;
    public uint PossessedFamiliar = InvalidId;
    public bool IsPC => Player != null;
    /// <summary>A player character at 0 to -9 hit points: lying down and uncommandable, not yet dead (docs/DEATH_AND_DYING.md).</summary>
    public bool IsDying => IsPC && !IsDead && CurrentHitPoints <= 0;

    // Movement state (server-authoritative).
    public Vector3 MoveDirection;
    public DriveInputMessage? DriveInput;
    public double DriveExpires;
    public float DriveTurnRemaining;
    public float DriveHeldTime, DriveStepRemaining;
    public bool DriveReleased;
    /// <summary>Scripted movement: the point to walk straight at (while MoveDirection is set), braking onto it when MoveArrive.</summary>
    public Vector3? MoveGoal;
    public bool MoveArrive;
    /// <summary>A short player destination reached by backing up, facing opposite its travel direction.</summary>
    public bool MoveBackward;
    public bool WalkRequested;
    public float Speed;
    public MovementMode Mode;
    public float WalkRate = 2.0f, RunRate = 4.0f;
    /// <summary>A player's load state as last seen (0 normal, 1 encumbered, 2 overloaded; -1 not read yet) and the
    /// movement stages it costs her (Inventory.EncumbranceSystem).</summary>
    public int LoadState = -1, LoadStage;
    public bool InCombat;
    /// <summary>Created, but its OnSpawn hasn't run yet (World.QueueSpawn).</summary>
    public bool SpawnPending;
}

public sealed partial class Placeable : GameObject
{
    public readonly List<Item> Inventory = new();
    public int TrapFlag, TrapType, TrapDetectDC, TrapDisarmDC, KeyRequired;
    public string KeyName = "";
    public int OpenLockDC, CloseLockDC;
    public override ObjectType Type => ObjectType.Placeable;
    public int Appearance;
    public bool Static, Useable, HasInventory, IsOpen, Locked;
    public int HitPoints, CurrentHitPoints;
    public GffStruct Template = new();
}

public sealed partial class Door : GameObject
{
    public int TrapFlag, TrapType, TrapDetectDC, TrapDisarmDC, KeyRequired;
    public string KeyName = "";
    public int OpenLockDC, CloseLockDC;
    public override ObjectType Type => ObjectType.Door;
    public int Appearance, GenericType;
    public bool IsOpen, Locked;
    /// <summary>Which way the door swung open (1 or 2, DoorService.OpenSide).</summary>
    public int OpenedSide = 1;
    public string LinkedTo = "";
    public byte LinkedToFlags;
    public int HitPoints, CurrentHitPoints;
    /// <summary>
    /// Killed (damage, EffectDeath or bashing): the door stands broken, open and without walkmesh, and does nothing
    /// by itself any more. A door with a transition stays like this for good; any other door is removed 2 s after
    /// its death (DoorService.Break, measured on nwserver: parity suite door_bashed).
    /// </summary>
    public bool Broken;
    public GffStruct Template = new();
    public int WalkmeshOwner;
    /// <summary>Walkmesh owner of the door leaf swung to side 1 or 2 (enabled while the door stands open that way).</summary>
    public int LeafOwner(int side) => WalkmeshOwner == 0 ? 0 : -(WalkmeshOwner * 2 + (side == 2 ? 1 : 0));
}

public sealed partial class Waypoint : GameObject
{
    public override ObjectType Type => ObjectType.Waypoint;
    public bool HasMapNote;
    public LocString MapNote = new();
}

public sealed partial class Trigger : GameObject
{
    public override ObjectType Type => ObjectType.Trigger;
    /// <summary>Polygon vertices in area space.</summary>
    public List<Vector3> Geometry = new();
    public int TriggerType;
    public string LinkedTo = "";
    public byte LinkedToFlags;
    public int Cursor;
    public readonly HashSet<uint> Inside = new();

    Vector2 _min, _max;
    int _boundsCount = -1;

    /// <summary>Recomputes the polygon's bounding box (geometry is set by the loader or SetTriggerGeometry-style changes).</summary>
    public void UpdateBounds()
    {
        _boundsCount = Geometry.Count;
        _min = new Vector2(float.MaxValue);
        _max = new Vector2(float.MinValue);
        foreach (var v in Geometry) { _min = Vector2.Min(_min, new Vector2(v.X, v.Y)); _max = Vector2.Max(_max, new Vector2(v.X, v.Y)); }
    }

    /// <summary>The flat distance from a point to the nearest point of the polygon's outline.</summary>
    public float OutlineDistance(Vector3 p)
    {
        var q = new Vector2(p.X, p.Y);
        float best = float.MaxValue;
        for (int i = 0, j = Geometry.Count - 1; i < Geometry.Count; j = i++)
        {
            var a = new Vector2(Geometry[j].X, Geometry[j].Y);
            var b = new Vector2(Geometry[i].X, Geometry[i].Y);
            var ab = b - a;
            float t = ab.LengthSquared() > 1e-8f ? Math.Clamp(Vector2.Dot(q - a, ab) / ab.LengthSquared(), 0, 1) : 0;
            best = MathF.Min(best, Vector2.Distance(q, a + ab * t));
        }
        return best;
    }

    public bool Contains(Vector3 p)
    {
        // Bounding box first: most triggers are nowhere near the creature.
        if (_boundsCount != Geometry.Count) UpdateBounds();
        if (p.X < _min.X || p.X > _max.X || p.Y < _min.Y || p.Y > _max.Y) return false;
        bool inside = false;
        for (int i = 0, j = Geometry.Count - 1; i < Geometry.Count; j = i++)
        {
            var a = Geometry[i]; var b = Geometry[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y + 1e-12f) + a.X) inside = !inside;
        }
        return inside;
    }
}

public sealed partial class Sound : GameObject
{
    public override ObjectType Type => ObjectType.Sound;
    public List<string> Sounds = new();
    public bool Active, Continuous, Looping, Positional;
    public float MaxDistance, MinDistance;
    public int Volume;
    public OpenWinter.Aurora.Formats.GffStruct Template = new();
}

public sealed partial class Encounter : GameObject
{
    public override ObjectType Type => ObjectType.Encounter;
    public List<Vector3> Geometry = new();
    /// <summary>The active value as set (SetEncounterActive(5) reads back 5 in the original).</summary>
    public int ActiveValue;
    public bool Active { get => ActiveValue != 0; set => ActiveValue = value ? 1 : 0; }
    public GffStruct Template = new();
}

public sealed partial class Store : GameObject
{
    public readonly List<Item> Inventory = new();
    public int Gold = -1, MaxBuyPrice = -1, IdentifyPrice = 100, MarkUp = 100, MarkDown = 100;
    public readonly HashSet<int> WillNotBuy = new();
    public readonly HashSet<int> WillOnlyBuy = new();
    public override ObjectType Type => ObjectType.Store;
    public GffStruct Template = new();
}

public sealed partial class Item : GameObject
{
    public readonly List<Scripting.ItemProperty> Properties = new();
    public readonly List<Item> Inventory = new(); // containers
    public bool Identified = true, Stolen, Cursed, Droppable = true, Pickpocketable = true;
    public int Charges, Cost, AddCost;
    public override ObjectType Type => ObjectType.Item;
    public int BaseItem;
    public int StackSize = 1;
    /// <summary>Top-left cell in the holder's inventory grid (Repos_PosX / Repos_Posy); -1 when equipped, on the ground or not placed yet.</summary>
    public int InvX = -1, InvY = -1;
    public GffStruct Template = new();
    public uint Possessor = InvalidId;
    /// <summary>The last potion or scroll of a stack used up: it goes without "Lost Item".</summary>
    public bool UsedUp;
}

public sealed partial class Module : GameObject
{
    public override ObjectType Type => ObjectType.Module;
    public GffStruct Ifo = new();
    public string EntryArea = "";
    public Vector3 EntryPosition;
    public Vector3 EntryDirection;
}
