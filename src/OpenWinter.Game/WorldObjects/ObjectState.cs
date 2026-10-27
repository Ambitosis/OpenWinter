using System.Numerics;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Objects;

// World-interaction state (locks, traps, transitions, encounters, sitting, visual
// overrides) added to the object classes. Filled from the GFF instance data by
// WorldObjectFields.Load, which the area loader calls for every object it reads.

/// <summary>Trap state shared by doors, placeables and triggers (beyond the flag/type/DC fields).</summary>
public sealed class TrapData
{
    public bool Detectable = true, Disarmable = true, OneShot = true, Recoverable = true, Active = true;
    /// <summary>"Flagged" traps are visible to everyone (GetTrapFlagged).</summary>
    public bool Flagged;
    public uint Creator = GameObject.InvalidId;
    public string KeyTag = "";
    public int Faction = 1; // STANDARD_FACTION_HOSTILE
    /// <summary>Creatures that have detected the trap.</summary>
    public readonly HashSet<uint> DetectedBy = new();
    public uint LastDisarmedBy = GameObject.InvalidId;
    public uint LastTriggeredBy = GameObject.InvalidId;
}

/// <summary>A door, placeable or trigger that can carry a trap.</summary>
public interface ITrappable
{
    bool IsTrapped { get; set; }
    int TrapBaseType { get; set; }
    int TrapDetectDCValue { get; set; }
    int TrapDisarmDCValue { get; set; }
    TrapData Trap { get; }
}

/// <summary>Lock state of doors and placeables beyond the fields in Objects.cs.</summary>
public sealed class LockData
{
    public bool Lockable;
    public bool AutoRemoveKey;
    /// <summary>SetKeyRequiredFeedback text ("" = the game's default message).</summary>
    public string KeyFeedback = "";
}

/// <summary>A door or placeable with a lock.</summary>
public interface ILockable
{
    bool IsLocked { get; set; }
    bool KeyRequiredFlag { get; set; }
    string LockKeyTag { get; set; }
    int UnlockDC { get; set; }
    int LockDC { get; set; }
    LockData Lock { get; }
}

public abstract partial class GameObject
{
    /// <summary>Explicit or resolved transition target (Get/SetTransitionTarget).</summary>
    public uint TransitionTargetId = InvalidId;
    /// <summary>
    /// The transition target is settled: the toolset LinkedTo tag was found (it is then not looked up again), there is
    /// no tag to look up, or SetTransitionTarget set or cleared the target. A tag that found nothing stays unresolved.
    /// </summary>
    public bool TransitionResolved;
    /// <summary>SetObjectVisibleDistance.</summary>
    public float VisibleDistance = 45f;
    /// <summary>Visual transforms by (scope, transform) (Get/SetObjectVisualTransform).</summary>
    public Dictionary<(int Scope, int Transform), WorldObjects.VisualTransform>? VisualTransforms;
    /// <summary>ReplaceObjectTexture overrides (old to new).</summary>
    public Dictionary<string, string>? TextureReplacements;
    /// <summary>ReplaceObjectAnimation overrides (old to new).</summary>
    public Dictionary<string, string>? AnimationReplacements;
    /// <summary>SetMaterialShaderUniformInt / Vec4 overrides by material (case-insensitive) and parameter.</summary>
    public Dictionary<(string Material, string Param), WorldObjects.MaterialUniform>? MaterialUniforms;
}

public sealed partial class Creature
{
    /// <summary>The last door that blocked this creature's movement (GetBlockingDoor).</summary>
    public uint LastBlockingDoor = InvalidId;
    /// <summary>The placeable this creature is sitting on (ActionSit), or invalid.</summary>
    public uint SittingOn = InvalidId;
    /// <summary>The encounter that spawned this creature (GetIsEncounterCreature), or invalid.</summary>
    public uint SpawnedByEncounter = InvalidId;
    /// <summary>The last trap this creature detected (GetLastTrapDetected).</summary>
    public uint LastTrapDetected = InvalidId;
}

public sealed partial class Door : ITrappable, ILockable
{
    public int Hardness;
    public readonly TrapData Trap = new();
    public readonly LockData Lock = new();

    bool ITrappable.IsTrapped { get => TrapFlag != 0; set => TrapFlag = value ? 1 : 0; }
    int ITrappable.TrapBaseType { get => TrapType; set => TrapType = value; }
    int ITrappable.TrapDetectDCValue { get => TrapDetectDC; set => TrapDetectDC = value; }
    int ITrappable.TrapDisarmDCValue { get => TrapDisarmDC; set => TrapDisarmDC = value; }
    TrapData ITrappable.Trap => Trap;

    bool ILockable.IsLocked { get => Locked; set => Locked = value; }
    bool ILockable.KeyRequiredFlag { get => KeyRequired != 0; set => KeyRequired = value ? 1 : 0; }
    string ILockable.LockKeyTag { get => KeyName; set => KeyName = value; }
    int ILockable.UnlockDC { get => OpenLockDC; set => OpenLockDC = value; }
    int ILockable.LockDC { get => CloseLockDC; set => CloseLockDC = value; }
    LockData ILockable.Lock => Lock;

    /// <summary>The door leads somewhere when clicked (toolset link or SetTransitionTarget).</summary>
    public bool IsTransition => TransitionTargetId != InvalidId || !TransitionResolved && LinkedTo.Length > 0;
}

public sealed partial class Placeable : ITrappable, ILockable
{
    public int Hardness;
    public readonly TrapData Trap = new();
    public readonly LockData Lock = new();
    /// <summary>Light on (Get/SetPlaceableIllumination). Only meaningful for placeables with a light in placeables.2da.</summary>
    public bool Illuminated = true;
    /// <summary>Creature sitting on this placeable, or invalid.</summary>
    public uint Sitter = InvalidId;

    bool ITrappable.IsTrapped { get => TrapFlag != 0; set => TrapFlag = value ? 1 : 0; }
    int ITrappable.TrapBaseType { get => TrapType; set => TrapType = value; }
    int ITrappable.TrapDetectDCValue { get => TrapDetectDC; set => TrapDetectDC = value; }
    int ITrappable.TrapDisarmDCValue { get => TrapDisarmDC; set => TrapDisarmDC = value; }
    TrapData ITrappable.Trap => Trap;

    bool ILockable.IsLocked { get => Locked; set => Locked = value; }
    bool ILockable.KeyRequiredFlag { get => KeyRequired != 0; set => KeyRequired = value ? 1 : 0; }
    string ILockable.LockKeyTag { get => KeyName; set => KeyName = value; }
    int ILockable.UnlockDC { get => OpenLockDC; set => OpenLockDC = value; }
    int ILockable.LockDC { get => CloseLockDC; set => CloseLockDC = value; }
    LockData ILockable.Lock => Lock;
}

public sealed partial class Trigger : ITrappable
{
    public int TrapFlag, TrapType, TrapDetectDC, TrapDisarmDC;
    public string KeyName = "";
    public int Faction = 1;
    public readonly TrapData Trap = new();

    bool ITrappable.IsTrapped { get => TrapFlag != 0; set => TrapFlag = value ? 1 : 0; }
    int ITrappable.TrapBaseType { get => TrapType; set => TrapType = value; }
    int ITrappable.TrapDetectDCValue { get => TrapDetectDC; set => TrapDetectDC = value; }
    int ITrappable.TrapDisarmDCValue { get => TrapDisarmDC; set => TrapDisarmDC = value; }
    TrapData ITrappable.Trap => Trap;

    /// <summary>TriggerType values (Type field in the GIT).</summary>
    public const int TypeGeneric = 0, TypeTransition = 1, TypeTrap = 2;
}

public sealed partial class Waypoint
{
    /// <summary>Map note shown on the area map (SetMapPinEnabled).</summary>
    public bool MapNoteEnabled = true;
}

public sealed partial class Encounter
{
    public readonly WorldObjects.EncounterState State = new();
}

/// <summary>Reads the world-interaction fields of a GIT/blueprint struct into an object.</summary>
public static class WorldObjectFields
{
    public static void Load(GameObject o, GffStruct s)
    {
        switch (o)
        {
            case Door d:
                d.KeyRequired = (int)s.GetInt("KeyRequired");
                d.KeyName = s.GetString("KeyName");
                d.OpenLockDC = (int)s.GetInt("OpenLockDC");
                d.CloseLockDC = (int)s.GetInt("CloseLockDC");
                d.Hardness = (int)s.GetInt("Hardness");
                LoadLock(d.Lock, s);
                d.TrapFlag = (int)s.GetInt("TrapFlag");
                d.TrapType = (int)s.GetInt("TrapType");
                d.TrapDetectDC = (int)s.GetInt("TrapDetectDC");
                d.TrapDisarmDC = (int)s.GetInt("DisarmDC");
                LoadTrap(d.Trap, s);
                d.IsOpen = s.GetInt("AnimationState") is 1 or 2;
                d.OpenedSide = s.GetInt("AnimationState") == 2 ? 2 : 1;
                break;
            case Placeable p:
                p.KeyRequired = (int)s.GetInt("KeyRequired");
                p.KeyName = s.GetString("KeyName");
                p.OpenLockDC = (int)s.GetInt("OpenLockDC");
                p.CloseLockDC = (int)s.GetInt("CloseLockDC");
                p.Hardness = (int)s.GetInt("Hardness");
                LoadLock(p.Lock, s);
                p.TrapFlag = (int)s.GetInt("TrapFlag");
                p.TrapType = (int)s.GetInt("TrapType");
                p.TrapDetectDC = (int)s.GetInt("TrapDetectDC");
                p.TrapDisarmDC = (int)s.GetInt("DisarmDC");
                LoadTrap(p.Trap, s);
                // AnimationState: 0 default, 1 open, 2 closed, 3 destroyed, 4 activated, 5 deactivated.
                long anim = s.GetInt("AnimationState");
                p.IsOpen = anim == 1;
                p.Illuminated = anim != 5;
                break;
            case Trigger t:
                t.KeyName = s.GetString("KeyName");
                t.Faction = (int)s.GetInt("Faction", 1);
                t.TrapFlag = (int)s.GetInt("TrapFlag");
                t.TrapType = (int)s.GetInt("TrapType");
                t.TrapDetectDC = (int)s.GetInt("TrapDetectDC");
                t.TrapDisarmDC = (int)s.GetInt("DisarmDC");
                LoadTrap(t.Trap, s);
                break;
            case Waypoint w:
                w.MapNoteEnabled = s.GetInt("MapNoteEnabled", 1) != 0;
                break;
            case Encounter e:
                e.State.Load(s);
                break;
        }
    }

    static void LoadLock(LockData l, GffStruct s)
    {
        l.Lockable = s.GetInt("Lockable") != 0;
        l.AutoRemoveKey = s.GetInt("AutoRemoveKey") != 0;
        l.KeyFeedback = s.Has("KeyReqFeedback") ? s.GetString("KeyReqFeedback") : "";
    }

    static void LoadTrap(TrapData t, GffStruct s)
    {
        t.Detectable = s.GetInt("TrapDetectable", 1) != 0;
        t.Disarmable = s.GetInt("TrapDisarmable", 1) != 0;
        t.OneShot = s.GetInt("TrapOneShot", 1) != 0;
        // The original reads no recoverable or active flag from the blueprint or the area: a toolset trap starts
        // recoverable and active (trap_api git*: TrapRecoverable 0 and TrapActive 0 still read back 1).
        t.Recoverable = true;
        t.Active = true;
        t.Faction = (int)s.GetInt("Faction", 1);
    }
}
