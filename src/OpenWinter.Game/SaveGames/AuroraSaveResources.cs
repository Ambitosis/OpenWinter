using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// The NWN-shaped part of a save: module.ifo, per-area ARE/GIT with the objects' current
/// state (and their ObjectId), playerlist.ifo and the player BICs. Tools that understand
/// NWN saves can read these; OpenWinter restores from the engine snapshot (openwinter.gff), which
/// carries everything these formats have no field for.
/// </summary>
static class AuroraSaveResources
{
    public static List<ErfEntry> Build(World world, Action<string> warn)
    {
        var entries = new List<ErfEntry>
        {
            new("module", ResType.Ifo, Gff.Write(ModuleIfo(world), "IFO ")),
        };
        foreach (var area in world.Areas)
        {
            try
            {
                entries.Add(new ErfEntry(area.ResRef, ResType.Are, Gff.Write(Are(area), "ARE ")));
                entries.Add(new ErfEntry(area.ResRef, ResType.Git, Gff.Write(Git(world, area, warn), "GIT ")));
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or InvalidCastException or NullReferenceException)
            {
                warn($"area {area.ResRef}: {e.Message}");
            }
        }
        if (world.Resources.Read("module", ResType.Jrl) is { } jrl) entries.Add(new ErfEntry("module", ResType.Jrl, jrl));
        // The module database, as the original's saves keep it (module.sq3: an "SQL3" zstd buffer).
        if (world.Module.Database is { IsOpen: true } db) entries.Add(new ErfEntry("module", ResType.Sq3, CompressedBuffer.Wrap("SQL3", db.Serialize())));
        entries.Add(new ErfEntry("playerlist", ResType.Ifo, Gff.Write(PlayerList(world, warn), "IFO ")));
        return entries;
    }

    static GffStruct ModuleIfo(World world)
    {
        var ifo = world.Module.Ifo.DeepClone();
        var clock = world.Clock;
        // The current date and time, so the module resumes where it was.
        ifo.SetDWord("Mod_StartYear", (uint)clock.Year);
        ifo.SetByte("Mod_StartMonth", clock.Month);
        ifo.SetByte("Mod_StartDay", clock.Day);
        ifo.SetByte("Mod_StartHour", clock.Hour);
        ifo.SetString("Mod_Tag", world.Module.Tag);
        ifo.SetList("Mod_Area_list", world.Areas.Select(a => new GffStruct(6).SetResRef("Area_Name", a.ResRef).SetDWord("ObjectId", a.Id)).ToList());
        ObjectSerializer.WriteLocals(ifo, world.Module.Locals);
        foreach (var (evt, script) in world.Module.Scripts) ifo.SetResRef(evt, script);
        return ifo;
    }

    internal static GffStruct Are(Area area)
    {
        var are = area.Are.DeepClone();
        are.SetString("Tag", area.Tag);
        are.SetLocString("Name", area.Name.Clone());
        are.SetString("ResRef", area.ResRef);
        are.SetString("Tileset", area.Tileset);
        are.SetInt("Width", area.Width);
        are.SetInt("Height", area.Height);
        var tiles = new List<GffStruct>(area.Tiles.Length);
        foreach (var t in area.Tiles)
            tiles.Add(new GffStruct(1).SetInt("Tile_ID", t.Id).SetInt("Tile_Orientation", t.Orientation).SetInt("Tile_Height", t.Height)
                .SetByte("Tile_MainLight1", t.MainLight1).SetByte("Tile_MainLight2", t.MainLight2)
                .SetByte("Tile_SrcLight1", t.SourceLight1).SetByte("Tile_SrcLight2", t.SourceLight2)
                .SetByte("Tile_AnimLoop1", t.AnimLoops & 1).SetByte("Tile_AnimLoop2", (t.AnimLoops >> 1) & 1).SetByte("Tile_AnimLoop3", (t.AnimLoops >> 2) & 1));
        are.SetList("Tile_List", tiles);
        foreach (var (evt, script) in area.Scripts) are.SetResRef(evt, script);
        ObjectSerializer.WriteLocals(are, area.Locals);
        return are;
    }

    /// <summary>
    /// An area's GIT with its objects as they are now, each with its state (variables, effects, actions,
    /// transition), and with <paramref name="objectIds"/> their object ids (save games; not the combined area format).
    /// </summary>
    internal static GffStruct Git(World world, Area area, Action<string> warn, bool objectIds = true, Func<GameObject, bool>? include = null)
    {
        var git = new GffStruct();
        git.SetStruct("AreaProperties", area.Properties.DeepClone());
        var lists = new Dictionary<string, List<GffStruct>>
        {
            ["Creature List"] = new(), ["Door List"] = new(), ["Placeable List"] = new(), ["WaypointList"] = new(), ["TriggerList"] = new(),
            ["SoundList"] = new(), ["Encounter List"] = new(), ["StoreList"] = new(), ["List"] = new(), ["AreaEffectList"] = new(),
        };
        foreach (var o in area.Objects)
        {
            if (o.IsDestroyed || o is Creature c && (c.FamiliarController != null || c.IsPC && c.Player?.FamiliarMaster == null)) continue;
            if (include != null && !include(o)) continue;
            try
            {
                var (list, s) = ObjectStruct(world, o);
                if (list == null) continue;
                if (o is not AreaOfEffect) ObjectStateGff.Write(world, o, s);
                if (objectIds) s.SetDWord("ObjectId", o.Id);
                lists[list].Add(s);
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or InvalidCastException or NullReferenceException or KeyNotFoundException)
            {
                warn($"{o}: not written to {area.ResRef}.git: {e.Message}");
            }
        }
        foreach (var (name, list) in lists) git.SetList(name, list);
        return git;
    }

    static (string? List, GffStruct Struct) ObjectStruct(World world, GameObject o)
    {
        switch (o)
        {
            case Creature c:
            {
                var s = ObjectSerializer.CreatureGff(world, c, 4, asCharacter: false);
                Positioned(s, o);
                return ("Creature List", s);
            }
            case Item i:
            {
                var s = ObjectSerializer.ItemGff(world, i, 0);
                Positioned(s, o);
                return ("List", s);
            }
            case Placeable p:
            {
                var s = ObjectSerializer.PlaceableGff(world, p);
                s.StructType = 9;
                Beared(s, o);
                return ("Placeable List", s);
            }
            case Door d:
            {
                var s = Common(d.Template, o, 8);
                Beared(s, o);
                s.SetByte("OpenState", d.IsOpen ? 1 : 0);
                s.SetByte("Locked", d.Locked ? 1 : 0);
                s.SetShort("CurrentHP", d.CurrentHitPoints);
                s.SetShort("HP", d.HitPoints);
                s.SetString("LinkedTo", d.LinkedTo);
                s.SetByte("LinkedToFlags", d.LinkedToFlags);
                return ("Door List", s);
            }
            case Waypoint w:
            {
                var s = Common(null, o, 5);
                Positioned(s, o);
                s.SetByte("HasMapNote", w.HasMapNote ? 1 : 0);
                s.SetLocString("MapNote", w.MapNote.Clone());
                return ("WaypointList", s);
            }
            case Trigger t:
            {
                var s = Common(null, o, 1);
                Positioned(s, o);
                s.SetInt("Type", t.TriggerType);
                s.SetString("LinkedTo", t.LinkedTo);
                s.SetByte("LinkedToFlags", t.LinkedToFlags);
                s.SetList("Geometry", t.Geometry.Select(p => new GffStruct(3)
                    .SetFloat("PointX", p.X - o.Position.X).SetFloat("PointY", p.Y - o.Position.Y).SetFloat("PointZ", p.Z - o.Position.Z)).ToList());
                return ("TriggerList", s);
            }
            case Sound snd:
            {
                var s = Common(snd.Template, o, 6);
                Positioned(s, o);
                s.SetByte("Active", snd.Active ? 1 : 0);
                s.SetByte("Volume", snd.Volume);
                return ("SoundList", s);
            }
            case Encounter e:
            {
                var s = Common(e.Template, o, 7);
                Positioned(s, o);
                s.SetByte("Active", e.Active ? 1 : 0);
                s.SetList("Geometry", e.Geometry.Select(p => new GffStruct(1)
                    .SetFloat("X", p.X - o.Position.X).SetFloat("Y", p.Y - o.Position.Y).SetFloat("Z", p.Z - o.Position.Z)).ToList());
                return ("Encounter List", s);
            }
            case Store st:
            {
                var s = Common(st.Template, o, 11);
                Positioned(s, o);
                s.SetInt("StoreGold", st.Gold);
                return ("StoreList", s);
            }
            case AreaOfEffect aoe:
            {
                var s = Common(null, o, 13);
                Positioned(s, o);
                s.SetInt("AreaEffectId", aoe.AoeId);
                s.SetDWord("CreatorId", aoe.Creator);
                s.SetInt("SpellId", aoe.SpellId);
                s.SetFloat("Duration", aoe.ExpiresAt > 0 ? (float)Math.Max(0, aoe.ExpiresAt - world.Time) : 0f);
                return ("AreaEffectList", s);
            }
        }
        return (null, new GffStruct());
    }

    static GffStruct Common(GffStruct? template, GameObject o, uint structType)
    {
        var s = template?.DeepClone() ?? new GffStruct();
        s.StructType = structType;
        s.SetString("Tag", o.Tag);
        s.SetResRef("TemplateResRef", o.ResRef);
        s.SetLocString("LocalizedName", o.Name.Clone());
        foreach (var (evt, script) in o.Scripts) s.SetResRef(evt, script);
        ObjectSerializer.WriteLocals(s, o.Locals);
        return s;
    }

    static void Positioned(GffStruct s, GameObject o)
    {
        s.SetFloat("XPosition", o.Position.X);
        s.SetFloat("YPosition", o.Position.Y);
        s.SetFloat("ZPosition", o.Position.Z);
        s.SetFloat("XOrientation", MathF.Cos(o.Facing));
        s.SetFloat("YOrientation", MathF.Sin(o.Facing));
    }

    static void Beared(GffStruct s, GameObject o)
    {
        s.SetFloat("X", o.Position.X);
        s.SetFloat("Y", o.Position.Y);
        s.SetFloat("Z", o.Position.Z);
        s.SetFloat("Bearing", o.Facing);
    }

    static GffStruct PlayerList(World world, Action<string> warn)
    {
        var list = new List<GffStruct>();
        foreach (var pc in Players(world))
            if (Bic(world, pc, warn) is { } bic)
            {
                bic.StructType = 0;
                list.Add(bic);
            }
        return new GffStruct().SetList("Mod_PlayerList", list);
    }

    public static IEnumerable<Creature> Players(World world) => OpenWinter.Game.Players.FamiliarPossession.Players(world);

    /// <summary>A player character as a BIC (current stats, inventory, position and ObjectId).</summary>
    public static GffStruct? Bic(World world, Creature pc, Action<string> warn)
    {
        try
        {
            var bic = ObjectSerializer.CreatureGff(world, pc, 0xFFFFFFFF, asCharacter: true);
            Positioned(bic, pc);
            if (pc.Area != null) bic.SetResRef("AreaResRef", pc.Area.ResRef);
            bic.SetDWord("ObjectId", pc.Id);
            return bic;
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or InvalidCastException or NullReferenceException or KeyNotFoundException)
        {
            warn($"{pc}: character not written: {e.Message}");
            return null;
        }
    }

}
