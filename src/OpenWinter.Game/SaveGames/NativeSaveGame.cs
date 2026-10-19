using System.Globalization;
using System.Numerics;
using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// Loads a save game made by the original game (docs/SAVEGAME.md, "The original's save games"). Its folder holds
/// <c>&lt;module&gt;.sav</c>, an ERF of type MOD: the module itself with <c>Mod_IsSaveGame</c> set, the areas' GITs holding
/// every object as it was (with its <c>ObjectId</c>, variables, effects, action queue and transition), the module's
/// variables, clock, pending DelayCommands (<c>EventQueue</c>) and players (<c>Mod_PlayerList</c>) in module.ifo, the
/// current reputations (repute.fac) and the module database (module.sq3); beside it <c>player.bic</c>,
/// <c>savenfo.txt</c> and <c>module_uuid.txt</c>. Measured with the parity scene nativesave.
/// <para>
/// The world is built from the archive like a module that runs no load, enter or spawn events. Object ids are
/// mapped from the original's (the module is 0, areas and objects their saved ids, players 0x7FFFFFFF downwards)
/// to this world's, so object variables, effect creators, transition targets and saved script stacks keep pointing at
/// the same objects.
/// </para>
/// </summary>
public static class NativeSaveGame
{
    const uint NativeModuleId = 0;

    /// <summary>The module archive of a folder holding one of the original's saves, or null.</summary>
    public static string? Archive(string folder)
    {
        if (!Directory.Exists(folder)) return null;
        foreach (var f in Directory.EnumerateFiles(folder, "*.sav").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            if (IsModuleArchive(f)) return f;
        return null;
    }

    static bool IsModuleArchive(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var head = new byte[8];
            return fs.Read(head, 0, 8) == 8 && Encoding.ASCII.GetString(head, 0, 4) == "MOD ";
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>A native save's listing data: the folder's name, the module, the player's area, name and the game time.</summary>
    public static SaveInfo ReadInfo(string folder)
    {
        var name = Path.GetFileName(folder);
        var archive = Archive(folder) ?? throw new FileNotFoundException($"no saved game in '{name}'");
        var kind = name.StartsWith("000000", StringComparison.Ordinal) ? SaveSlotKind.Quick
            : name.StartsWith("000001", StringComparison.Ordinal) ? SaveSlotKind.Auto : SaveSlotKind.Manual;
        using var erf = new ErfSource(archive);
        var ifo = Gff.Read(erf.Read(ResKey.Of("module", ResType.Ifo)) ?? throw new InvalidDataException("module.ifo missing"));
        string area = "", areaResRef = "", player = "";
        if (ifo.GetList("Mod_PlayerList").FirstOrDefault() is { } pc)
        {
            player = string.Join(" ", new[] { pc.GetLocString("FirstName").FirstString, pc.GetLocString("LastName").FirstString }.Where(x => x.Length > 0));
            uint areaId = (uint)pc.GetInt("AreaId", GameObject.InvalidId);
            areaResRef = ifo.GetList("Mod_Area_list").FirstOrDefault(a => (uint)a.GetInt("ObjectId", GameObject.InvalidId) == areaId)?.GetString("Area_Name") ?? "";
            if (areaResRef.Length > 0 && erf.Read(ResKey.Of(areaResRef, ResType.Are)) is { } are) area = Gff.Read(are).GetLocString("Name").FirstString;
        }
        string display = name.Length > 9 ? name[9..] : name;
        var time = $"Year {ifo.GetInt("Mod_StartYear")}, month {ifo.GetInt("Mod_StartMonth")}, day {ifo.GetInt("Mod_StartDay")}, "
            + $"{ifo.GetInt("Mod_StartHour"):D2}:{ifo.GetInt("Mod_StartMinute"):D2}";
        return new SaveInfo(name, display, kind, File.GetLastWriteTime(archive), ifo.GetLocString("Mod_Name").FirstString,
            area, areaResRef, player, time, archive, 0, File.Exists(Path.Combine(folder, "screen.tga")));
    }

    /// <summary>
    /// Builds a world from one of the original's saves. Scripts use <paramref name="current"/>'s definitions;
    /// the module's own resources come from the archive (the original keeps a .mod module's whole contents in it).
    /// Saves of modules it doesn't hold (campaign .nwm) are layered over <paramref name="modulePath"/>.
    /// </summary>
    public static LoadedGame Load(World current, string folder, string? modulePath = null)
    {
        var archive = Archive(folder) ?? throw new FileNotFoundException($"no saved game in '{Path.GetFileName(folder)}'");
        var info = ReadInfo(folder);
        var paths = current.Resources.Paths;
        ResMan rm;
        bool complete;
        using (var probe = new ErfSource(archive)) complete = probe.Keys.Any(k => k.Type == ResType.FromExtension("ncs"));
        if (complete || modulePath == null) rm = ResMan.ForModule(paths, archive);
        else
        {
            rm = ResMan.ForModule(paths, modulePath);
            rm.AddFirst(new ErfSource(archive));
        }

        var world = new World(rm) { GameDifficulty = current.GameDifficulty };
        var oldPersistence = current.System<PersistenceSystem>();
        var persistence = world.System<PersistenceSystem>();
        persistence.RootOverride = oldPersistence.RootOverride;
        persistence.Log = oldPersistence.Log;
        var structs = new Dictionary<GameObject, GffStruct>(ReferenceEqualityComparer.Instance);
        world.ObjectLoaded = (o, s) => structs[o] = s;
        world.LoadModule();
        world.ObjectLoaded = null;
        world.EnableScripts(current.Scripts?.Definitions);
        if (current.Scripts is { } oldHost && world.Scripts is { } host)
        {
            host.Log = oldHost.Log;
            host.VM.JitThreshold = oldHost.VM.JitThreshold;
            host.VM.InstructionLimit = oldHost.VM.InstructionLimit;
        }

        var restored = new WorldSnapshot.Restored();
        var warnings = new List<string>();
        var ifo = world.Module.Ifo;

        // ---- the original's object ids
        var byNative = new Dictionary<uint, GameObject> { [NativeModuleId] = world.Module };
        var areaList = ifo.GetList("Mod_Area_list");
        for (int i = 0; i < areaList.Count; i++)
            if (world.Areas.FirstOrDefault(a => a.ResRef.Equals(areaList[i].GetString("Area_Name"), StringComparison.OrdinalIgnoreCase)) is { } area
                && areaList[i].Has("ObjectId"))
                byNative[(uint)areaList[i].GetInt("ObjectId")] = area;
        foreach (var (o, s) in structs)
            if (s.Has("ObjectId")) byNative[(uint)s.GetInt("ObjectId")] = o;
        foreach (var o in world.AllObjects.ToList()) MapContents(o, byNative);
        // The saved ids belong to the original's world; the structs stay as the objects' templates.
        foreach (var s in structs.Values) s.Remove("ObjectId");

        // ---- players (their journal and every reference to them come from the save)
        var players = new List<(Creature Creature, GffStruct Struct)>();
        foreach (var p in ifo.GetList("Mod_PlayerList"))
        {
            var c = world.Creatures.FromTemplate(p);
            c.VaultFile = "";
            world.Register(c);
            if (p.Has("ObjectId")) byNative[(uint)p.GetInt("ObjectId")] = c;
            p.Remove("ObjectId");
            MapContents(c, byNative);
            players.Add((c, p));
        }
        uint Map(uint native) => native == GameObject.InvalidId ? GameObject.InvalidId
            : byNative.TryGetValue(native, out var o) ? o.Id : GameObject.InvalidId;

        foreach (var (c, p) in players)
        {
            var area = byNative.GetValueOrDefault((uint)p.GetInt("AreaId", GameObject.InvalidId)) as Area
                ?? world.FindArea(world.Module.EntryArea) ?? world.Areas.FirstOrDefault();
            if (area == null) { warnings.Add($"{c.Name.FirstString}: no area to place the player in"); continue; }
            var pos = new Vector3(p.GetFloat("XPosition"), p.GetFloat("YPosition"), p.GetFloat("ZPosition"));
            world.AddToArea(c, area, pos, MathF.Atan2(p.GetFloat("YOrientation"), p.GetFloat("XOrientation")), exact: true, enterEvent: false);
            structs[c] = p;
            restored.Players.Add((c, p.GetString("Mod_CommntyName")));
        }

        // ---- object state: effects, actions, transitions, then every object reference in variables
        foreach (var (o, s) in structs) ObjectStateGff.Read(world, o, s, warnings, Map);
        // The original's save keeps no dead flag: a corpse is a creature at -11 hit points or fewer (death leaves every
        // creature there; a dying player is at -1 to -10). The Prelude's m0q1d goblins, killed by their own spawn
        // script and kept with SetIsDestroyable(FALSE), are saved at -11 and must load as the corpses they are.
        foreach (var (o, s) in structs)
            if (o is Creature corpse && s.Has("CurrentHitPoints")) RestoreDeath(corpse);
        foreach (var o in world.AllObjects.ToList()) RemapLocals(o.Locals, Map);
        foreach (var (c, _) in players) RestoreJournal(world, c);

        // ---- pending DelayCommands
        foreach (var e in ifo.GetList("EventQueue")) Schedule(world, e, Map, warnings);

        // ---- the module database
        if (rm.Read("module", ResType.Sq3) is { Length: > 0 } sq3)
        {
            try { persistence.ModuleDb(world.Module).Load(CompressedBuffer.Unwrap("SQL3", sq3)); }
            catch (InvalidDataException e) { warnings.Add($"module database not restored: {e.Message}"); }
        }

        foreach (var w in warnings) restored.Warnings.Add(w);
        return new LoadedGame { World = world, Info = info, Restored = restored };
    }

    /// <summary>A creature saved at -11 hit points or fewer is a corpse: dead, with nothing left to do.</summary>
    public static void RestoreDeath(Creature c)
    {
        if (c.IsDead || c.CurrentHitPoints > Rules.HitPoints.PlayerDeathThreshold - 1) return;
        c.IsDead = true;
        c.Actions.Clear();
    }

    /// <summary>Maps the saved ids of everything an object carries (inventory, equipment, container contents).</summary>
    static void MapContents(GameObject o, Dictionary<uint, GameObject> byNative)
    {
        IEnumerable<Item> items = o switch
        {
            Creature c => c.Equipment.Values.Concat(c.Inventory),
            Placeable p => p.Inventory,
            Store st => st.Inventory,
            Item i => i.Inventory,
            _ => Array.Empty<Item>(),
        };
        foreach (var item in items)
        {
            if (item.Template.Has("ObjectId")) byNative[(uint)item.Template.GetInt("ObjectId")] = item;
            item.Template.Remove("ObjectId");
            MapContents(item, byNative);
        }
    }

    static void RemapLocals(LocalVariables v, Func<uint, uint> map)
    {
        foreach (var k in v.Objects.Keys.ToList()) v.Objects[k] = map(v.Objects[k]);
        foreach (var k in v.Locations.Keys.ToList())
        {
            var l = v.Locations[k];
            v.Locations[k] = new Location(map(l.Area), l.Position, l.FacingDegrees);
        }
    }

    /// <summary>
    /// The journal: the original keeps a player's quest states in the locals NW_JOURNAL_ENTRY / DATE / TIME + tag
    /// (nativesave: "NW_JOURNAL_ENTRYq_ns" 2 with the calendar day and millisecond it was reached).
    /// </summary>
    static void RestoreJournal(World world, Creature pc)
    {
        var journal = world.System<JournalService>();
        foreach (var (key, state) in pc.Locals.Ints.ToList())
        {
            if (!key.StartsWith(JournalService.LocalPrefix, StringComparison.Ordinal)) continue;
            var tag = key[JournalService.LocalPrefix.Length..];
            if (!journal.Categories.TryGetValue(tag, out var cat)) continue;
            pc.Journal[cat.Tag] = new JournalState
            {
                Tag = cat.Tag,
                State = state,
                Day = pc.Locals.Ints.GetValueOrDefault("NW_JOURNAL_DATE" + tag),
                Time = pc.Locals.Ints.GetValueOrDefault("NW_JOURNAL_TIME" + tag),
            };
        }
    }

    /// <summary>
    /// A pending DelayCommand (EventQueue entry with EventId 1): the script's code (the .ncs without its header),
    /// the instruction it resumes at (InstructionPtr, an offset into that code) and the stack it captured
    /// (BasePointer cells of globals, then the locals; cell types 3 int, 4 float, 5 string, 6 object, 16+ engine
    /// structures). It runs at its saved calendar time (Day, Time).
    /// </summary>
    static void Schedule(World world, GffStruct e, Func<uint, uint> map, List<string> warnings)
    {
        if (world.Scripts is not { } host) return;
        if (e.GetInt("EventId") != 1 || e.GetStruct("EventData") is not { } d)
        {
            warnings.Add($"saved event {e.GetInt("EventId")} is not read yet; dropped");
            return;
        }
        string name = d.GetString("Name");
        var code = d.GetBytes("Code");
        var program = host.ProgramFor(name);
        if (program == null || !program.Code.AsSpan().SequenceEqual(code))
        {
            if (code.Length == 0) { warnings.Add($"a delayed command of '{name}' has no code; dropped"); return; }
            var file = new byte[NcsProgram.HeaderSize + code.Length];
            Encoding.ASCII.GetBytes("NCS V1.0").CopyTo(file, 0);
            file[8] = 0x42;
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(file.AsSpan(9), (uint)file.Length);
            code.CopyTo(file, NcsProgram.HeaderSize);
            try { program = new NcsProgram(name, file); }
            catch (InvalidDataException ex) { warnings.Add($"a delayed command of '{name}': {ex.Message}; dropped"); return; }
        }
        var stack = d.GetStruct("Stack") ?? new GffStruct();
        var cells = stack.GetList("Stack").Select(c => Cell(world, c, map, warnings)).ToArray();
        int bp = (int)Math.Clamp(stack.GetInt("BasePointer"), 0, cells.Length);
        uint self = map((uint)e.GetInt("ObjectId", GameObject.InvalidId));
        var situation = new ScriptSituation
        {
            Program = program,
            ResumeIp = (int)d.GetInt("InstructionPtr"),
            Globals = cells[..bp],
            Locals = cells[bp..],
            Self = self,
        };
        var clock = world.Clock;
        long dayMs = 24 * clock.HourMs;
        double delay = ((long)e.GetInt("Day") * dayMs + e.GetInt("Time") - clock.TotalMs) / 1000.0;
        host.Schedule(Math.Max(0, delay), situation, self);
    }

    static Cell Cell(World world, GffStruct c, Func<uint, uint> map, List<string> warnings)
    {
        int type = (int)c.GetInt("Type");
        switch (type)
        {
            case 3: return Scripting.Cell.Int((int)c.GetInt("Value"));
            case 4: return Scripting.Cell.Float(c.GetFloat("Value"));
            case 5: return Scripting.Cell.Ref(c.GetString("Value"));
            case 6: return Scripting.Cell.Obj(map((uint)c.GetInt("Value", GameObject.InvalidId)));
        }
        if (type >= 0x10 && type < 0x10 + 8)
        {
            var nss = (NssType)((int)NssType.Effect + (type - 0x10));
            var value = c.GetStruct("GameDefinedStrct") is { } s ? EngineValue(world, nss, s, map) : null;
            return Scripting.Cell.Ref(value ?? world.Scripts!.DefaultEngineValue(nss));
        }
        warnings.Add($"a saved script stack cell of type {type} is not read yet");
        return default;
    }

    /// <summary>Engine structures on a saved stack: locations and effects; the rest start as their defaults.</summary>
    static object? EngineValue(World world, NssType type, GffStruct s, Func<uint, uint> map)
    {
        switch (type)
        {
            case NssType.Location when s.Has("PositionX"):
            {
                float deg = MathF.Atan2(s.GetFloat("OrientationY"), s.GetFloat("OrientationX")) * 180f / MathF.PI;
                if (deg < 0) deg += 360f;
                return new Location(map((uint)s.GetInt("Area", GameObject.InvalidId)),
                    new Vector3(s.GetFloat("PositionX"), s.GetFloat("PositionY"), s.GetFloat("PositionZ")), deg);
            }
            case NssType.Effect when s.Has("Type"):
                return NativeEffects.Read(world, s, out _, null, map);
            default:
                return null;
        }
    }
}
