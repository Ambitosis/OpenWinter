using System.Text.RegularExpressions;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Util: script, resource, clock and string helpers. Behaviour measured on nwserver with NWNX by the parity
/// suite nwnx_util (dawn and dusk hours outside 0 - 23 are ignored, the last created objects count a creature's
/// inventory, resource directory calls on an unknown alias fail).
/// </summary>
static class UtilPlugin
{
    const string P = "Util";

    [NwnxFunc(P, "GetCurrentScriptName")]
    static void CurrentScriptName(NwnxCall n)
    {
        int depth = n.PopInt();
        var running = n.Host?.VM.Running;
        int i = (running?.Count ?? 0) - 1 - depth;
        n.Push(running != null && i >= 0 && i < running.Count ? running[i].Program.Name : "");
    }

    /// <summary>A string whose character at index i has code i (index 0 is a placeholder space).</summary>
    [NwnxFunc(P, "GetAsciiTableString")]
    static void AsciiTable(NwnxCall n)
    {
        var bytes = new byte[256];
        bytes[0] = (byte)' ';
        for (int i = 1; i < 256; i++) bytes[i] = (byte)i;
        n.Push(OpenWinter.Aurora.Text.Windows1252.GetString(bytes));
    }

    [NwnxFunc(P, "Hash")] static void Hash(NwnxCall n) => n.Push(MiscFunctions.HashString(n.PopString()));

    static string? ModulePath(World w) => w.Resources.ModuleSource?.Description;

    [NwnxFunc(P, "GetModuleMtime")]
    static void ModuleMtime(NwnxCall n)
    {
        var path = ModulePath(n.World);
        n.Push(path != null && (File.Exists(path) || Directory.Exists(path))
            ? (int)new DateTimeOffset(File.GetLastWriteTimeUtc(path)).ToUnixTimeSeconds() : 0);
    }

    /// <summary>The module's file name without its extension.</summary>
    [NwnxFunc(P, "GetModuleFile")]
    static void ModuleFile(NwnxCall n) => n.Push(ModulePath(n.World) is { } path ? Path.GetFileNameWithoutExtension(path.TrimEnd('/', '\\')) : "");

    [NwnxFunc(P, "GetModuleTlkFile")] static void ModuleTlk(NwnxCall n) => n.Push(n.World.Module.Ifo.GetString("Mod_CustomTlk"));

    /// <summary>SetCustomToken's value for everyone (token numbers below 1 hold nothing).</summary>
    [NwnxFunc(P, "GetCustomToken")]
    static void CustomToken(NwnxCall n)
    {
        int token = n.PopInt();
        n.Push(token > 0 ? n.World.System<Conversations.ConversationService>().CustomTokens.GetValueOrDefault(token, "") : "");
    }

    /// <summary>Removes &lt;cRGB&gt; colour openers and &lt;/c&gt; closers.</summary>
    public static string StripColors(string s) => Regex.Replace(s, "<c.{3}>|</c>", "", RegexOptions.Singleline);
    [NwnxFunc(P, "StripColors")] static void Strip(NwnxCall n) => n.Push(StripColors(n.PopString()));

    [NwnxFunc(P, "IsValidResRef")]
    static void IsValidResRef(NwnxCall n)
    {
        var resref = n.PopString(); int type = n.PopInt();
        n.Push(resref.Length > 0 && n.World.Resources.Contains(resref, (ushort)type));
    }
    [NwnxFunc(P, "GetEnvironmentVariable")] static void EnvVar(NwnxCall n) => n.Push(Environment.GetEnvironmentVariable(n.PopString()) ?? "");
    [NwnxFunc(P, "GetMinutesPerHour")] static void GetMinutesPerHour(NwnxCall n) => n.Push(n.World.Clock.MinutesPerHour);
    [NwnxFunc(P, "SetMinutesPerHour")]
    static void SetMinutesPerHour(NwnxCall n)
    {
        int m = n.PopInt();
        if (m > 0) n.World.Clock.MinutesPerHour = m;
    }
    [NwnxFunc(P, "GetDawnHour")] static void GetDawn(NwnxCall n) => n.Push(n.World.Clock.DawnHour);
    [NwnxFunc(P, "GetDuskHour")] static void GetDusk(NwnxCall n) => n.Push(n.World.Clock.DuskHour);
    [NwnxFunc(P, "SetDawnHour")]
    static void SetDawn(NwnxCall n)
    {
        int hour = n.PopInt();
        if (hour is >= 0 and <= 23) n.World.Clock.DawnHour = hour;
    }
    [NwnxFunc(P, "SetDuskHour")]
    static void SetDusk(NwnxCall n)
    {
        int hour = n.PopInt();
        if (hour is >= 0 and <= 23) n.World.Clock.DuskHour = hour;
    }
    [NwnxFunc(P, "EncodeStringForURL")] static void EncodeUrl(NwnxCall n) => n.Push(Uri.EscapeDataString(n.PopString()));
    [NwnxFunc(P, "Get2DARowCount")] static void RowCount(NwnxCall n) => n.Push(n.World.Resources.TwoDA(n.PopString()).RowCount);
    [NwnxFunc(P, "GetServerTicksPerSecond")] static void Ticks(NwnxCall n) => n.Push((int)GameServer.TickRate);
    /// <summary>The user folder the server writes to (its databases, vault and NWNX folder; a sandbox in development).</summary>
    [NwnxFunc(P, "GetUserDirectory")] static void UserDirectory(NwnxCall n) => n.Push(n.World.System<Persistence.PersistenceSystem>().UserRoot);
    [NwnxFunc(P, "GetTTY")] static void Tty(NwnxCall n) => n.Push("");
    [NwnxFunc(P, "PluginExists")] static void PluginExists(NwnxCall n) => n.Push(NwnxRegistry.HasPlugin(n.PopString()));

    [NwnxFunc(P, "GetInstructionLimit")] static void GetInstructionLimit(NwnxCall n) => n.Push(n.Host?.VM.InstructionLimit ?? 0);
    [NwnxFunc(P, "SetInstructionLimit")]
    static void SetInstructionLimit(NwnxCall n)
    {
        int limit = n.PopInt();
        if (n.Host != null) n.Host.VM.InstructionLimit = limit > 0 ? limit : ScriptVM.DefaultInstructionLimit;
    }
    [NwnxFunc(P, "GetInstructionsExecuted")] static void InstructionsExecuted(NwnxCall n) => n.Push(n.Thread?.InstructionsExecuted ?? 0);

    /// <summary>Resets the count the instruction limit is checked against (a negative count is 0).</summary>
    [NwnxFunc(P, "SetInstructionsExecuted")]
    static void SetInstructionsExecuted(NwnxCall n)
    {
        int count = Math.Max(0, n.PopInt());
        if (n.Host == null) return;
        n.Host.VM.InstructionsUsed = count;
        if (n.Thread != null) n.Thread.InstructionsExecuted = count;
    }

    [NwnxFunc(P, "GetScriptParamIsSet")]
    static void ScriptParamIsSet(NwnxCall n)
    {
        var name = n.PopString();
        n.Push(n.Host != null && n.Host.CurrentEvent.Values.ContainsKey("param:" + name));
    }

    /// <summary>The result of the last StartingConditional script to finish.</summary>
    [NwnxFunc(P, "GetScriptReturnValue")] static void ScriptReturnValue(NwnxCall n) => n.Push(n.Host?.VM.LastConditionalResult ?? 0);

    /// <summary>The running script's event, as GetCurrentlyRunningEvent reports it from now on.</summary>
    [NwnxFunc(P, "SetCurrentlyRunningEvent")]
    static void SetCurrentlyRunningEvent(NwnxCall n)
    {
        int id = n.PopInt();
        if (n.Host?.CurrentEvent is not { } e) return;
        e.Set("EventScriptType", id);
        if (e.Values.ContainsKey("DirectEventScriptType")) e.Set("DirectEventScriptType", id);
    }

    /// <summary>The object GetItemActivator returns in the running script.</summary>
    [NwnxFunc(P, "SetItemActivator")] static void SetItemActivator(NwnxCall n) => n.Host?.CurrentEvent.Set("Activator", n.PopObjectId());

    [NwnxFunc(P, "RegisterServerConsoleCommand")]
    static void RegisterConsole(NwnxCall n)
    {
        var command = n.PopString(); var chunk = n.PopString();
        n.Registry.ConsoleCommands[command] = chunk;
        n.Push(1);
    }
    [NwnxFunc(P, "UnregisterServerConsoleCommand")] static void UnregisterConsole(NwnxCall n) => n.Registry.ConsoleCommands.Remove(n.PopString());

    [NwnxFunc(P, "GetWorldTime")]
    static void WorldTime(NwnxCall n)
    {
        float adjust = n.PopFloat();
        var k = n.World.Clock;
        long ms = k.TimeOfDayMs + (long)(adjust * 1000);
        int day = (int)k.DayNumber;
        long dayMs = 24 * k.HourMs;
        while (ms < 0) { ms += dayMs; day--; }
        while (ms >= dayMs) { ms -= dayMs; day++; }
        n.Push(day);
        n.Push((int)ms);
    }

    [NwnxFunc(P, "GetHighResTimeStamp")]
    static void HighRes(NwnxCall n)
    {
        long ticks = DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks;
        n.Push((int)(ticks / TimeSpan.TicksPerSecond));
        n.Push((int)(ticks % TimeSpan.TicksPerSecond / 10));
    }

    /// <summary>Server-side: every lookup of the old resource reads the new one (an empty name clears it).</summary>
    [NwnxFunc(P, "SetResourceOverride")]
    static void SetResourceOverride(NwnxCall n)
    {
        int type = n.PopInt(); var oldName = n.PopString(); var newName = n.PopString();
        if (oldName.Length is 0 or > 16 || newName.Length > 16) return;
        n.World.Resources.SetOverride((ushort)type, oldName, newName);
        if (type == ResType.Ncs) n.World.Scripts?.Forget(oldName);
    }
    [NwnxFunc(P, "GetResourceOverride")]
    static void GetResourceOverride(NwnxCall n)
    {
        int type = n.PopInt(); var name = n.PopString();
        n.Push(n.World.Resources.GetOverride((ushort)type, name) ?? "");
    }

    [NwnxFunc(P, "GetFirstResRef")]
    static void FirstResRef(NwnxCall n)
    {
        int type = n.PopInt(); var filter = n.PopString(); bool moduleOnly = n.PopInt() != 0;
        Regex? re = null;
        try { if (filter.Length > 0) re = new Regex(filter); }
        catch (ArgumentException) { }
        var rm = n.World.Resources;
        var list = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var s in rm.Sources)
        {
            if (moduleOnly && MiscFunctions.Classify(s, rm.Paths).IsBase) continue;
            foreach (var k in s.Keys)
                if (k.Type == type && (re == null || re.IsMatch(k.ResRef))) list.Add(k.ResRef);
        }
        n.Registry.ResRefIterator = list.ToList();
        n.Registry.ResRefIndex = 0;
        NextResRef(n);
    }
    [NwnxFunc(P, "GetNextResRef")]
    static void NextResRef(NwnxCall n)
    {
        var list = n.Registry.ResRefIterator;
        n.Push(list != null && n.Registry.ResRefIndex < list.Count ? list[n.Registry.ResRefIndex++] : "");
    }

    // ---- Objects ---------------------------------------------------------------------

    /// <summary>NWNX's object types (the engine's; NWNX_Consts_TranslateNWScriptObjectType): 0 matches any.</summary>
    static bool OfType(GameObject o, int type) => type switch
    {
        0 => o is not (Area or Objects.Module),
        5 => o is Creature,
        6 => o is Item,
        7 => o is Trigger,
        9 => o is Placeable,
        10 => o is Door,
        11 => o is AreaOfEffect,
        12 => o is Waypoint,
        13 => o is Encounter,
        14 => o is Store,
        16 => o is Sound,
        _ => false,
    };

    /// <summary>
    /// When an object was made, as the original orders them: a creature's starting inventory comes right after it,
    /// though OpenWinter registers those items first.
    /// </summary>
    static (uint, int, uint) CreationOrder(World w, GameObject o) =>
        o is Item { Possessor: var holder } && w.Get(holder) is Creature c && c.Id > o.Id ? (c.Id, 1, o.Id) : (o.Id, 0, 0);

    /// <summary>The nth newest object of a type; a creature's inventory items count, made just after it.</summary>
    [NwnxFunc(P, "GetLastCreatedObject")]
    static void LastCreatedObject(NwnxCall n)
    {
        int type = n.PopInt(); int nth = n.PopInt();
        if (nth < 1) { n.PushObject(GameObject.InvalidId); return; }
        var found = n.World.AllObjects.Where(o => !o.IsDestroyed && OfType(o, type)).OrderByDescending(o => CreationOrder(n.World, o))
            .Skip(nth - 1).FirstOrDefault();
        n.PushObject(found?.Id ?? GameObject.InvalidId);
    }

    /// <summary>Creates a door from a blueprint, with an optional new tag and doortypes.2da appearance.</summary>
    [NwnxFunc(P, "CreateDoor")]
    static void CreateDoor(NwnxCall n)
    {
        var resref = n.PopString(); var location = n.PopLocation(); var tag = n.PopString(); int appearance = n.PopInt();
        var w = n.World;
        if (w.Get(location.Area) is not Area area || w.Loader == null || w.Resources.ReadGff(resref, ResType.Utd) is not { } utd)
        {
            n.PushObject(GameObject.InvalidId);
            return;
        }
        if (appearance >= 0) utd.SetDWord("Appearance", (uint)appearance); // a fresh copy read for this door
        float bearing = GameObject.BearingFor(location.FacingDegrees * MathF.PI / 180f);
        var door = w.Loader.CreateDoor(area, utd, location.Position, bearing);
        door.ResRef = resref;
        if (tag.Length > 0) w.SetTag(door, tag);
        w.AddToArea(door, area, location.Position, bearing);
        n.PushObject(door.Id);
    }

    /// <summary>Sends every client (or one player's) the object's state again.</summary>
    [NwnxFunc(P, "UpdateClientObject")]
    static void UpdateClientObject(NwnxCall n)
    {
        var o = n.PopObject(); n.PopObjectId();
        if (o != null) n.World.MarkChanged(o);
    }

    /// <summary>Moves the module's starting point (GetStartingLocation; where new players enter).</summary>
    [NwnxFunc(P, "SetStartingLocation")]
    static void SetStartingLocation(NwnxCall n)
    {
        var resref = n.PopString(); var location = n.PopLocation(); var direction = n.PopVector();
        if (n.World.FindArea(resref) is not { } area) { n.Push(0); return; }
        var m = n.World.Module;
        m.EntryArea = area.ResRef;
        m.EntryPosition = location.Position;
        m.EntryDirection = direction;
        n.Push(1);
    }

    [NwnxFunc(P, "GetStringLevenshteinDistance")]
    static void Levenshtein(NwnxCall n)
    {
        var a = n.PopString(); var b = n.PopString();
        var row = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) row[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = row[0];
            row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int above = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = above;
            }
        }
        n.Push(row[b.Length]);
    }

    [NwnxFunc(P, "RawPrint")] static void RawPrint(NwnxCall n) => (n.Host?.Log ?? n.Registry.Log)(n.PopString());

    /// <summary>
    /// Effects and item properties share one engine type: an item property cast to an effect is a valid effect of no
    /// script type, and casts back to itself; an ordinary effect is no item property (nwnx_util).
    /// </summary>
    [NwnxFunc(P, "EffectTypeCast")]
    static void EffectTypeCast(NwnxCall n)
    {
        var v = n.PopValue();
        if (v?.Value is ItemProperty ip)
        {
            var k = EffectConstants.Of(n.World);
            n.Push(new NwnxValue(NwnxType.Effect, new Effect { Type = Effect.ItemPropertyType, DurationType = k.Permanent, Property = ip }));
        }
        else n.Push(new NwnxValue(NwnxType.ItemProperty, v?.Value is Effect { Property: { } back } ? back : new ItemProperty()));
    }

    // ---- The NWNX resource directory (user folder/nwnx) -------------------------------

    static string NwnxDir(World w) => Path.Combine(w.System<Persistence.PersistenceSystem>().UserRoot, "nwnx");

    /// <summary>The NWNX folder's resources, ahead of the module's (added the first time it is written to).</summary>
    static DirectorySource NwnxSource(World w)
    {
        var dir = NwnxDir(w);
        if (w.Resources.Sources.OfType<DirectorySource>().FirstOrDefault(s => s.Description == dir) is { } existing) return existing;
        Directory.CreateDirectory(dir);
        var source = new DirectorySource(dir);
        w.Resources.AddFirst(source);
        return source;
    }

    /// <summary>
    /// The folder an alias names for a reindex: NWNX, or another loose resource folder the server has (TEMP...).
    /// </summary>
    static DirectorySource? AliasDirectory(World w, string alias)
    {
        alias = alias.TrimEnd(':');
        if (alias.Equals("NWNX", StringComparison.OrdinalIgnoreCase)) return NwnxSource(w);
        return w.Resources.Sources.OfType<DirectorySource>()
            .FirstOrDefault(s => MiscFunctions.Classify(s, w.Resources.Paths).Alias.TrimEnd(':').Equals(alias, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The folder scripts may write to and delete from: only NWNX's own. The header's other aliases come from a
    /// custom resource directory file, which OpenWinter servers don't have; the game's folders (OVERRIDE, the module,
    /// the install) are never touched.
    /// </summary>
    static DirectorySource? WritableDirectory(World w, string alias) =>
        alias.Length == 0 || alias.TrimEnd(':').Equals("NWNX", StringComparison.OrdinalIgnoreCase) ? NwnxSource(w) : null;

    /// <summary>
    /// The file a resource name and type are kept in, inside <paramref name="dir"/>, or null when the name is not a
    /// plain resource name (1 - 16 characters, no path).
    /// </summary>
    static string? FileIn(string dir, string name, ushort type)
    {
        if (name.Length is 0 or > 16 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.IndexOfAny(['/', '\\', ':']) >= 0 || name.Trim('.').Length == 0) return null;
        var full = Path.GetFullPath(Path.Combine(dir, name.ToLowerInvariant() + "." + ResType.Extension(type)));
        return string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ? full : null;
    }

    static bool Write(World w, string name, ushort type, byte[] data)
    {
        var source = NwnxSource(w);
        if (FileIn(source.Description, name, type) is not { } path) return false;
        try { File.WriteAllBytes(path, data); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
        source.Refresh();
        return true;
    }

    /// <summary>
    /// Compiles a script into the NWNX folder, where it overrides the module's; returns "" or the compile error.
    /// sAlias is ignored: every script goes to the NWNX folder.
    /// </summary>
    [NwnxFunc(P, "AddScript")]
    static void AddScript(NwnxCall n)
    {
        var name = n.PopString(); var source = n.PopString(); bool wrap = n.PopInt() != 0; n.PopString();
        if (n.Host == null) { n.Push("no script host"); return; }
        var ncs = Scripting.Compiler.NssCompiler.Compile(wrap ? "void main() {" + source + "}" : source, name, n.Host.Definitions, n.Host.ScriptSource, out var error);
        if (ncs == null) { n.Push(error); return; }
        if (!Write(n.World, name, ResType.Ncs, ncs)) { n.Push("could not write " + name); return; }
        n.Host.Forget(name);
        n.Push("");
    }

    /// <summary>Writes a script's source into the NWNX folder; sAlias is ignored (nwserver also answered TRUE for an unknown one).</summary>
    [NwnxFunc(P, "AddNSSFile")]
    static void AddNssFile(NwnxCall n)
    {
        var name = n.PopString(); var contents = n.PopString(); n.PopString();
        n.Push(Write(n.World, name, ResType.Nss, OpenWinter.Aurora.Text.Windows1252.GetBytes(contents)));
    }

    /// <summary>Deletes one file from the NWNX folder; FALSE for any other alias, a name that isn't a resource name, or no file.</summary>
    [NwnxFunc(P, "RemoveNWNXResourceFile")]
    static void RemoveFile(NwnxCall n)
    {
        var name = n.PopString(); int type = n.PopInt(); var alias = n.PopString();
        if (WritableDirectory(n.World, alias) is not { } source || FileIn(source.Description, name, (ushort)type) is not { } path)
        {
            n.Push(0);
            return;
        }
        bool removed = false;
        try { if (File.Exists(path)) { File.Delete(path); removed = true; } }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        source.Refresh();
        if (type == ResType.Ncs) n.World.Scripts?.Forget(name);
        n.Push(removed);
    }

    /// <summary>Deletes every file of a type (0xFFFF: all) from the NWNX folder; FALSE for any other alias.</summary>
    [NwnxFunc(P, "CleanResourceDirectory")]
    static void CleanDirectory(NwnxCall n)
    {
        var alias = n.PopString(); int type = n.PopInt();
        if (alias.Length == 0 || WritableDirectory(n.World, alias) is not { } source) { n.Push(0); return; }
        foreach (var file in Directory.EnumerateFiles(source.Description))
        {
            if (!ResKey.TryParse(Path.GetFileName(file), out var key) || type != 0xFFFF && key.Type != type) continue;
            try { File.Delete(file); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            if (key.Type == ResType.Ncs) n.World.Scripts?.Forget(key.ResRef);
        }
        source.Refresh();
        n.Push(1);
    }

    [NwnxFunc(P, "UpdateResourceDirectory")]
    static void UpdateDirectory(NwnxCall n)
    {
        if (AliasDirectory(n.World, n.PopString()) is not { } source) { n.Push(0); return; }
        source.Refresh();
        n.Push(1);
    }
}
