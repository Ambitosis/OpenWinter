using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// Doors, waypoints, triggers, stores, encounters, sounds and areas (the combined area format, CAF) in the
/// shape the original's ObjectToJson writes them (parity suite persist_objects: the fields each type carries
/// and what JsonToObject, RetrieveCampaignObject and SqlGetObject restore from them).
/// </summary>
public static partial class ObjectSerializer
{
    static void Positioned(GffStruct s, GameObject o)
    {
        s.SetFloat("XPosition", o.Position.X);
        s.SetFloat("YPosition", o.Position.Y);
        s.SetFloat("ZPosition", o.Position.Z);
        s.SetFloat("XOrientation", MathF.Cos(o.Facing));
        s.SetFloat("YOrientation", MathF.Sin(o.Facing));
        s.SetFloat("ZOrientation", 0f);
    }

    static void Scripts(GffStruct s, GameObject o)
    {
        foreach (var (evt, script) in o.Scripts) Str(s, evt, GffType.ResRef, script);
    }

    static GffStruct FromTemplate(GffStruct t)
    {
        var s = t.DeepClone();
        s.StructType = RootStruct;
        // Fields a GIT instance or a save game adds that no serialized object carries.
        s.Remove("ObjectId");
        return s;
    }

    static void WriteTrap(GffStruct s, ITrappable t, bool flag)
    {
        if (flag) Sync(s, "TrapFlag", GffType.Byte, t.IsTrapped ? 1 : 0, 0);
        Sync(s, "TrapType", GffType.Byte, t.TrapBaseType, 0);
        Sync(s, "TrapDetectDC", GffType.Byte, t.TrapDetectDCValue, 0);
        Sync(s, "DisarmDC", GffType.Byte, t.TrapDisarmDCValue, 0);
        Sync(s, "TrapDetectable", GffType.Byte, t.Trap.Detectable ? 1 : 0, 1);
        Sync(s, "TrapDisarmable", GffType.Byte, t.Trap.Disarmable ? 1 : 0, 1);
        Sync(s, "TrapOneShot", GffType.Byte, t.Trap.OneShot ? 1 : 0, 1);
        Sync(s, "TrapRecoverable", GffType.Byte, t.Trap.Recoverable ? 1 : 0, 1);
        Sync(s, "TrapActive", GffType.Byte, t.Trap.Active ? 1 : 0, 1);
    }

    public static GffStruct DoorGff(World world, Door d)
    {
        var s = FromTemplate(d.Template);
        var t = d.Template;
        Str(s, "Tag", GffType.ExoString, d.Tag);
        Str(s, "TemplateResRef", GffType.ResRef, d.ResRef);
        if (!SameLoc(d.Name, t.GetLocString("LocName"))) s.SetLocString("LocName", d.Name.Clone());
        if (d.Description != t.GetLocString("Description").FirstString) s.SetLocString("Description", new LocString(d.Description));
        Sync(s, "Appearance", GffType.DWord, d.Appearance, 0);
        Sync(s, "GenericType_New", GffType.DWord, d.GenericType, 0);
        Sync(s, "AnimationState", GffType.Byte, d.IsOpen ? (d.OpenedSide == 2 ? 2 : 1) : 0, 0);
        Sync(s, "Locked", GffType.Byte, d.Locked ? 1 : 0, 0);
        Sync(s, "Lockable", GffType.Byte, d.Lock.Lockable ? 1 : 0, 0);
        Sync(s, "AutoRemoveKey", GffType.Byte, d.Lock.AutoRemoveKey ? 1 : 0, 0);
        Sync(s, "KeyRequired", GffType.Byte, d.KeyRequired, 0);
        Str(s, "KeyName", GffType.ExoString, d.KeyName);
        Sync(s, "OpenLockDC", GffType.Byte, d.OpenLockDC, 0);
        Sync(s, "CloseLockDC", GffType.Byte, d.CloseLockDC, 0);
        Sync(s, "Hardness", GffType.Byte, d.Hardness, 0);
        Sync(s, "HP", GffType.Short, d.HitPoints, 0);
        Sync(s, "CurrentHP", GffType.Short, d.CurrentHitPoints, 0);
        Sync(s, "Plot", GffType.Byte, d.Plot ? 1 : 0, 0);
        Str(s, "LinkedTo", GffType.ExoString, d.LinkedTo);
        if (d.LinkedToFlags != 0 || s.Has("LinkedToFlags")) Sync(s, "LinkedToFlags", GffType.Byte, d.LinkedToFlags, 0);
        WriteTrap(s, d, flag: true);
        Scripts(s, d);
        s.SetFloat("X", d.Position.X);
        s.SetFloat("Y", d.Position.Y);
        s.SetFloat("Z", d.Position.Z);
        s.SetFloat("Bearing", d.Facing);
        WriteCommon(world, s, d);
        return s;
    }

    public static GffStruct WaypointGff(World world, Waypoint w)
    {
        var s = new GffStruct(RootStruct);
        s.SetString("Tag", w.Tag);
        s.SetLocString("LocalizedName", w.Name.Clone());
        s.SetByte("HasMapNote", w.HasMapNote ? 1 : 0);
        if (w.HasMapNote)
        {
            s.SetLocString("MapNote", w.MapNote.Clone());
            s.SetByte("MapNoteEnabled", w.MapNoteEnabled ? 1 : 0);
        }
        Positioned(s, w);
        WriteCommon(world, s, w);
        return s;
    }

    public static GffStruct TriggerGff(World world, Trigger tr)
    {
        var s = new GffStruct(RootStruct);
        s.SetString("Tag", tr.Tag);
        s.SetResRef("TemplateResRef", tr.ResRef);
        s.SetLocString("LocalizedName", tr.Name.Clone());
        s.SetInt("Type", tr.TriggerType);
        s.SetByte("Cursor", tr.Cursor);
        s.SetString("LinkedTo", tr.LinkedTo);
        if (tr.LinkedToFlags != 0) s.SetByte("LinkedToFlags", tr.LinkedToFlags);
        s.SetString("KeyName", tr.KeyName);
        s.SetDWord("Faction", (uint)tr.Faction);
        WriteTrap(s, tr, flag: true);
        foreach (var evt in new[] { "OnClick", "OnDisarm", "OnTrapTriggered", "ScriptHeartbeat", "ScriptOnEnter", "ScriptOnExit", "ScriptUserDefine" })
            s.SetResRef(evt, tr.Script(evt));
        Positioned(s, tr);
        s.SetList("Geometry", tr.Geometry.Select(p => new GffStruct(3)
            .SetFloat("PointX", p.X - tr.Position.X).SetFloat("PointY", p.Y - tr.Position.Y).SetFloat("PointZ", p.Z - tr.Position.Z)).ToList());
        WriteCommon(world, s, tr);
        return s;
    }

    public static GffStruct StoreGff(World world, Store st)
    {
        world.Inventory.Load(st);
        var s = new GffStruct(RootStruct);
        s.SetString("Tag", st.Tag);
        s.SetLocString("LocName", st.Name.Clone());
        s.SetInt("MarkUp", st.MarkUp);
        s.SetInt("MarkDown", st.MarkDown);
        s.SetByte("BlackMarket", st.BlackMarket ? 1 : 0);
        s.SetInt("BM_MarkDown", st.BlackMarketMarkDown);
        s.SetInt("StoreGold", st.Gold);
        s.SetInt("MaxBuyPrice", st.MaxBuyPrice);
        s.SetInt("IdentifyPrice", st.IdentifyPrice);
        s.SetResRef("OnOpenStore", st.Script("OnOpenStore"));
        s.SetResRef("OnStoreClosed", st.Script("OnStoreClosed"));
        s.SetList("WillNotBuy", st.WillNotBuy.Select(b => new GffStruct(0x17E4).SetInt("BaseItem", b)).ToList());
        s.SetList("WillOnlyBuy", st.WillOnlyBuy.Select(b => new GffStruct(0x17E4).SetInt("BaseItem", b)).ToList());
        // The stock by store panel (0 armour, 1 miscellaneous, 2 potions, 3 rings, 4 weapons), each item as it is now.
        var panels = new List<GffStruct>();
        for (int panel = 0; panel < 5; panel++)
        {
            var items = new List<GffStruct>();
            foreach (var item in st.Inventory.Where(i => !i.IsDestroyed && (i.StorePanel >= 0 ? i.StorePanel : world.Inventory.Rules.StorePanel(i.BaseItem)) == panel))
            {
                var e = ItemGff(world, item, (uint)items.Count);
                if (item.InvX >= 0 && item.InvY >= 0) { e.SetWord("Repos_PosX", item.InvX); e.SetWord("Repos_Posy", item.InvY); }
                if (item.Infinite) e.SetByte("Infinite", 1);
                items.Add(e);
            }
            panels.Add(new GffStruct((uint)panel).SetList("ItemList", items));
        }
        s.SetList("StoreList", panels);
        Positioned(s, st);
        WriteCommon(world, s, st);
        return s;
    }

    public static GffStruct SoundGff(World world, Sound snd)
    {
        var s = FromTemplate(snd.Template);
        // The original writes no blueprint resref or name for sound objects, stores and encounters.
        s.Remove("TemplateResRef");
        Str(s, "Tag", GffType.ExoString, snd.Tag);
        Sync(s, "Active", GffType.Byte, snd.Active ? 1 : 0, 0);
        Sync(s, "Continuous", GffType.Byte, snd.Continuous ? 1 : 0, 0);
        Sync(s, "Looping", GffType.Byte, snd.Looping ? 1 : 0, 0);
        Sync(s, "Positional", GffType.Byte, snd.Positional ? 1 : 0, 0);
        Sync(s, "Volume", GffType.Byte, snd.Volume, 127);
        SyncFloat(s, "MaxDistance", snd.MaxDistance, 0);
        SyncFloat(s, "MinDistance", snd.MinDistance, 0);
        s.SetList("Sounds", snd.Sounds.Select(n => new GffStruct(0).SetResRef("Sound", n)).ToList());
        s.SetFloat("XPosition", snd.Position.X);
        s.SetFloat("YPosition", snd.Position.Y);
        s.SetFloat("ZPosition", snd.Position.Z);
        WriteCommon(world, s, snd);
        return s;
    }

    public static GffStruct EncounterGff(World world, Encounter e)
    {
        var s = FromTemplate(e.Template);
        s.Remove("TemplateResRef");
        var st = e.State;
        Str(s, "Tag", GffType.ExoString, e.Tag);
        if (!SameLoc(e.Name, e.Template.GetLocString("LocalizedName"))) s.SetLocString("LocalizedName", e.Name.Clone());
        Sync(s, "Active", GffType.Byte, e.Active ? 1 : 0, 0);
        Sync(s, "DifficultyIndex", GffType.Int, st.DifficultyIndex, 2);
        Sync(s, "MaxCreatures", GffType.Int, st.MaxCreatures, 8);
        Sync(s, "RecCreatures", GffType.Int, st.RecCreatures, 1);
        Sync(s, "Respawns", GffType.Int, st.SpawnsMax, -1);
        Sync(s, "NumberSpawned", GffType.Int, st.SpawnsCurrent, 0);
        Sync(s, "Reset", GffType.Byte, st.Reset ? 1 : 0, 1);
        Sync(s, "ResetTime", GffType.Int, (int)st.ResetTime, 60);
        Sync(s, "SpawnOption", GffType.Int, st.SpawnOption, 0);
        Sync(s, "PlayerOnly", GffType.Byte, st.PlayerOnly ? 1 : 0, 1);
        Sync(s, "Faction", GffType.DWord, st.Faction, 1);
        s.SetList("CreatureList", st.Creatures.Select(c => new GffStruct(0).SetResRef("ResRef", c.ResRef).SetFloat("CR", c.CR)
            .SetByte("SingleSpawn", c.SingleSpawn ? 1 : 0)).ToList());
        s.SetList("SpawnPointList", st.SpawnPoints.Select(p => new GffStruct(0).SetFloat("X", p.Position.X).SetFloat("Y", p.Position.Y)
            .SetFloat("Z", p.Position.Z).SetFloat("Orientation", p.Facing)).ToList());
        Scripts(s, e);
        s.SetFloat("XPosition", e.Position.X);
        s.SetFloat("YPosition", e.Position.Y);
        s.SetFloat("ZPosition", e.Position.Z);
        s.SetList("Geometry", e.Geometry.Select(p => new GffStruct(1)
            .SetFloat("X", p.X - e.Position.X).SetFloat("Y", p.Y - e.Position.Y).SetFloat("Z", p.Z - e.Position.Z)).ToList());
        WriteCommon(world, s, e);
        return s;
    }

    /// <summary>
    /// An area in the combined area format: <c>{ARE, GIT}</c>, the ARE with its current tiles, name and scripts and
    /// the GIT with every object but the players in its state form (no object ids) and the area's variables.
    /// </summary>
    public static GffStruct AreaCaf(World world, Area area)
    {
        var are = AuroraSaveResources.Are(area);
        are.Remove("VarTable");
        are.StructType = 0;
        var git = AuroraSaveResources.Git(world, area, _ => { }, objectIds: false);
        WriteLocals(git, area.Locals);
        if (!git.Has("VarTable")) git.SetList("VarTable", new List<GffStruct>());
        git.StructType = 0;
        return new GffStruct(RootStruct).SetStruct("ARE", are).SetStruct("GIT", git);
    }

    // ---- Reading ----------------------------------------------------------------------------

    /// <summary>Doors, waypoints, triggers, stores, encounters and sounds at a location; null for other types.</summary>
    static GameObject? DeserializePlaced(World world, GffStruct s, string type, Area area, Vector3 position, float facing, bool loadState)
    {
        var loader = world.Loader ?? new AreaLoader(world);
        GameObject? o;
        switch (type)
        {
            case "UTD":
            {
                var d = loader.CreateDoor(area, s, position, GameObject.BearingFor(facing));
                d.Locked = s.GetInt("Locked") != 0;
                // The walkmesh was built when the area loaded: rebuild it with the new door.
                WalkmeshBuilder.Rebuild(world, area);
                area.ResetPathfinder();
                DoorService.SyncWalkmesh(area);
                world.AddToArea(d, area, position, d.Facing);
                o = d;
                break;
            }
            case "UTW":
                o = loader.CreateWaypoint(area, s, position, facing);
                world.AddToArea(o, area, position, facing);
                break;
            case "UTT":
                o = loader.CreateTrigger(area, s, position);
                world.AddToArea(o, area, position, facing);
                break;
            case "UTM":
            {
                var st = loader.CreateStore(area, s, position, facing);
                world.AddToArea(st, area, position, facing);
                o = st;
                break;
            }
            case "UTS":
                o = loader.CreateSound(area, s, position);
                world.AddToArea(o, area, position, 0);
                break;
            case "UTE":
                o = loader.CreateEncounter(area, s, position);
                world.AddToArea(o, area, position, 0);
                break;
            default:
                return null;
        }
        if (!loadState) ClearLocals(o.Locals);
        ClaimUuid(world, o, s);
        return o;
    }

    /// <summary>JsonToObject of a combined-format area: a new area instance ("nw_N") with everything in it.</summary>
    static Area? DeserializeArea(World world, GffStruct s)
    {
        if (s.GetStruct("ARE") is not { } are) return null;
        var git = s.GetStruct("GIT") ?? new GffStruct();
        var source = are.GetString("ResRef");
        var loader = world.Loader ?? new AreaLoader(world);
        Area area;
        try { area = loader.Load(source, are, git); }
        catch (Exception e) when (e is FileNotFoundException or InvalidDataException or KeyNotFoundException) { return null; }
        area.SourceResRef = source;
        // The objects' state (effects, actions, transitions) was written with them.
        foreach (var o in area.Objects.ToList())
            if (TemplateAccess.TemplateOf(o) is { } t) ObjectStateGff.Read(world, o, t);
        return AreaInstancing.Adopt(world, area);
    }
}
