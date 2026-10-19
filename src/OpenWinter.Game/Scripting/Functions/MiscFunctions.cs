using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// 2DA access, resource manager queries, TLK overrides, random names, event
/// script handlers, module/game control, game pause and script introspection.
/// </summary>
public static class MiscFunctions
{
    [NwFunc("FeetToMeters")] static object? FeetToMeters(Call c) => c.F(0) * 0.3048f;
    [NwFunc("YardsToMeters")] static object? YardsToMeters(Call c) => c.F(0) * 0.9144f;

    // ---- 2DA ---------------------------------------------------------------

    [NwFunc("Get2DAString")] static object? Get2DAString(Call c) => c.World.Resources.TwoDA(c.S(0)).Get(c.I(2), c.S(1));
    [NwFunc("Get2DAColumn")] static object? Get2DAColumn(Call c)
    {
        // Column names come back in lower case, as in the original.
        var cols = c.World.Resources.TwoDA(c.S(0)).Columns;
        int i = c.I(1);
        return i >= 0 && i < cols.Length ? cols[i].ToLowerInvariant() : "";
    }
    [NwFunc("Get2DARowCount")] static object? Get2DARowCount(Call c) => c.World.Resources.TwoDA(c.S(0)).RowCount;

    // ---- Random names ------------------------------------------------------

    static readonly ConditionalWeakTable<ResMan, ConcurrentDictionary<string, Ltr?>> _ltrs = new();

    /// <summary>NAME_* constant to .ltr resref. Half-elves have no first-name tables and use the human ones.</summary>
    public static string NameTable(int nameType) => nameType switch
    {
        -1 => "names",
        0 => "animal",
        1 => "familiar",
        2 => "dwarfm", 3 => "dwarff", 4 => "dwarfl",
        5 => "elfm", 6 => "elff", 7 => "elfl",
        8 => "gnomem", 9 => "gnomef", 10 => "gnomel",
        11 => "humanm", 12 => "humanf", 13 => "halfelfl",
        14 => "halflingm", 15 => "halflingf", 16 => "halflingl",
        17 => "halforcm", 18 => "halforcf", 19 => "halforcl",
        20 => "humanm", 21 => "humanf", 22 => "humanl",
        _ => "",
    };

    public static Ltr? LoadLtr(ResMan rm, string resref)
    {
        var cache = _ltrs.GetValue(rm, _ => new ConcurrentDictionary<string, Ltr?>(StringComparer.OrdinalIgnoreCase));
        return cache.GetOrAdd(resref, r =>
        {
            var bytes = rm.Read(r, ResType.Ltr);
            if (bytes == null) return null;
            try { return Ltr.Parse(bytes); }
            catch (InvalidDataException) { return null; }
        });
    }

    public static string RandomName(ResMan rm, int nameType)
    {
        var table = NameTable(nameType);
        var ltr = table.Length > 0 ? LoadLtr(rm, table) : null;
        if (ltr == null && nameType == -1) ltr = LoadLtr(rm, "humanm");
        return ltr?.Generate(Random.Shared) ?? "";
    }

    [NwFunc("RandomName")] static object? RandomNameF(Call c) => RandomName(c.World.Resources, c.I(0, -1));

    // ---- Module and game ---------------------------------------------------

    [NwFunc("GetModuleName")] static object? ModuleName(Call c) => c.World.Module.Name.Resolve(c.World.Resources.Talk);
    [NwFunc("GetGameDifficulty")] static object? Difficulty(Call c) => c.World.GameDifficulty;

    [NwFunc("StartNewModule")] static object? StartNewModule(Call c)
    {
        // The server stages the replacement and transfers characters after this script/tick finishes.
        c.World.PendingModule = c.S(0);
        c.World.NotifyAllPlayers("start-module", new { module = c.S(0) });
        return null;
    }
    [NwFunc("EndGame")] static object? EndGame(Call c)
    {
        c.World.NotifyAllPlayers("end-game", new { movie = c.S(0) });
        return null;
    }
    [NwFunc("ActivatePortal")] static object? ActivatePortal(Call c)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc)
            c.World.Output?.Notify("portal", pc, new { address = c.S(1), password = c.S(2), waypoint = c.S(3), seamless = c.I(4) != 0 }, pc);
        return null;
    }
    [NwFunc("SpawnScriptDebugger")] static object? Debugger(Call c) => null;
    [NwFunc("BadBadReplaceMeThisDoesNothing")] static object? BadBad(Call c) => new ItemProperty();
    [NwFunc("Reserved899")] static object? Reserved899(Call c) => null;

    [NwFunc("SetTlkOverride")] static object? SetTlkOverride(Call c)
    {
        int strref = c.I(0);
        string text = c.S(1);
        if (text.Length == 0) c.World.TlkOverrides.Remove(strref);
        else c.World.TlkOverrides[strref] = text;
        c.World.NotifyAllPlayers("tlk-override", new { strref, text });
        return null;
    }

    [NwFunc("SetGameActivePause")] static object? SetPause(Call c)
    {
        int before = c.World.PauseState;
        c.World.PauseState = c.I(0) != 0 ? before | 2 : before & ~2;
        // GameServer.Tick keeps servicing client requests but skips World.Step while paused.
        if (before != c.World.PauseState) c.World.NotifyAllPlayers("pause", new { state = c.World.PauseState });
        return null;
    }
    [NwFunc("GetGamePauseState")] static object? PauseState(Call c) => c.World.PauseState;
    [NwFunc("GetTickRate")] static object? TickRate(Call c) => (int)GameServer.TickRate;
    [NwFunc("HashString")] static object? Hash(Call c) => HashString(c.S(0));

    /// <summary>XXH32(s) ^ XXH32("") over the string's Windows-1252 bytes, as nwscript.nss specifies.</summary>
    public static int HashString(string s)
    {
        var bytes = OpenWinter.Aurora.Text.Windows1252.GetBytes(s);
        return unchecked((int)(XxHash32(bytes) ^ XxHash32(Array.Empty<byte>())));
    }

    /// <summary>xxHash32, seed 0 (public algorithm by Yann Collet).</summary>
    public static uint XxHash32(ReadOnlySpan<byte> data, uint seed = 0)
    {
        const uint P1 = 2654435761u, P2 = 2246822519u, P3 = 3266489917u, P4 = 668265263u, P5 = 374761393u;
        static uint Rotl(uint x, int r) => (x << r) | (x >> (32 - r));
        static uint Lane(ReadOnlySpan<byte> d, int i) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(d[i..]);
        unchecked
        {
            int i = 0, n = data.Length;
            uint h;
            if (n >= 16)
            {
                uint v1 = seed + P1 + P2, v2 = seed + P2, v3 = seed, v4 = seed - P1;
                for (; i + 16 <= n; i += 16)
                {
                    v1 = Rotl(v1 + Lane(data, i) * P2, 13) * P1;
                    v2 = Rotl(v2 + Lane(data, i + 4) * P2, 13) * P1;
                    v3 = Rotl(v3 + Lane(data, i + 8) * P2, 13) * P1;
                    v4 = Rotl(v4 + Lane(data, i + 12) * P2, 13) * P1;
                }
                h = Rotl(v1, 1) + Rotl(v2, 7) + Rotl(v3, 12) + Rotl(v4, 18);
            }
            else h = seed + P5;
            h += (uint)n;
            for (; i + 4 <= n; i += 4) h = Rotl(h + Lane(data, i) * P3, 17) * P4;
            for (; i < n; i++) h = Rotl(h + data[i] * P5, 11) * P1;
            h ^= h >> 15; h *= P2; h ^= h >> 13; h *= P3; h ^= h >> 16;
            return h;
        }
    }

    // ---- Resource manager --------------------------------------------------

    /// <summary>The keytable alias a source is known by, and whether it is base game data.</summary>
    public static (string Alias, bool IsBase) Classify(IResourceSource s, GamePaths paths)
    {
        var path = s.Description.Replace('\\', '/');
        var user = paths.UserRoot.Replace('\\', '/').TrimEnd('/');
        var game = paths.GameRoot.Replace('\\', '/').TrimEnd('/');
        if (path.EndsWith(".hak", StringComparison.OrdinalIgnoreCase)) return ("HAK:", false);
        if (path.Equals(user + "/development", StringComparison.OrdinalIgnoreCase)) return ("DEVELOPMENT:", false);
        if (path.Equals(user + "/override", StringComparison.OrdinalIgnoreCase)) return ("OVERRIDE:", false);
        if (s is KeyBifSource || path.StartsWith(game + "/", StringComparison.OrdinalIgnoreCase)) return ("NWNDATA:", true);
        return ("CURRENTGAME:", false);
    }

    /// <summary>
    /// Where a resource comes from, named as the original names it: "CURRENTGAME:&lt;module&gt;" for the module and
    /// "HD0INSTALL:data/nwn_base" (the key file) for base game data.
    /// </summary>
    [NwFunc("ResManGetAliasFor")] static object? AliasFor(Call c)
    {
        var rm = c.World.Resources;
        var key = ResKey.Of(c.S(0), (ushort)c.I(1));
        if (key.ResRef.Length == 0) return "";
        foreach (var s in rm.Sources)
            if (s.Contains(key))
            {
                var alias = Classify(s, rm.Paths).Alias;
                var path = s.Description.Replace('\\', '/');
                var game = rm.Paths.GameRoot.Replace('\\', '/').TrimEnd('/');
                if (alias == "NWNDATA:" && path.StartsWith(game + "/", StringComparison.OrdinalIgnoreCase))
                    return "HD0INSTALL:" + Path.ChangeExtension(path[(game.Length + 1)..], null);
                if (alias == "CURRENTGAME:") return alias + Path.GetFileNameWithoutExtension(path.TrimEnd('/'));
                return alias;
            }
        return "";
    }

    [NwFunc("ResManFindPrefix")] static object? FindPrefix(Call c)
    {
        var rm = c.World.Resources;
        string prefix = c.S(0).ToLowerInvariant();
        ushort type = (ushort)c.I(1);
        int nth = c.I(2, 1);
        bool includeBase = c.I(3) != 0;
        string only = c.S(4).TrimEnd(':');
        if (nth < 1) return "";
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var s in rm.Sources)
        {
            var (alias, isBase) = Classify(s, rm.Paths);
            if (isBase && !includeBase) continue;
            if (only.Length > 0 && !alias.TrimEnd(':').Equals(only, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var k in s.Keys)
                if (k.Type == type && k.ResRef.StartsWith(prefix, StringComparison.Ordinal)) found.Add(k.ResRef);
        }
        return found.Count >= nth ? found.ElementAt(nth - 1) : "";
    }

    [NwFunc("ResManGetFileContents")] static object? FileContents(Call c)
    {
        var bytes = c.World.Resources.Read(c.S(0), (ushort)c.I(1));
        if (bytes == null) return "";
        return c.I(2) switch
        {
            1 => Convert.ToBase64String(bytes),
            2 => Convert.ToHexString(bytes).ToLowerInvariant(),
            _ => OpenWinter.Aurora.Text.Windows1252.GetString(bytes, 0, Array.IndexOf(bytes, (byte)0) is var z and >= 0 ? z : bytes.Length),
        };
    }

    /// <summary>Compiles a script and makes it available to ExecuteScript and events for the rest of the session.</summary>
    [NwFunc("CompileScript")] static object? CompileScript(Call c)
    {
        string name = c.S(0).ToLowerInvariant();
        string source = c.I(2) != 0 ? "void main() {" + c.S(1) + "}" : c.S(1);
        var ncs = Scripting.Compiler.NssCompiler.Compile(source, name, c.Host.Definitions, c.Host.ScriptSource, out var error);
        if (ncs == null) return "ERROR: FILE NOT COMPILED SUCCESSFULLY: " + error + "\n"; // the original ends it with a newline
        c.Host.Register(name, new NcsProgram(name, ncs));
        return "";
    }

    // ---- Event script handlers --------------------------------------------

    [NwFunc("GetEventScript")] static object? GetEventScript(Call c) =>
        c.Obj(0) is { } o && EventScripts.Field(o, c.I(1)) is { } f ? o.Script(f) : "";

    [NwFunc("SetEventScript")] static object? SetEventScript(Call c)
    {
        if (c.Obj(0) is not { } o || EventScripts.Field(o, c.I(1)) is not { } f) return 0;
        string script = c.S(2);
        bool changed = !o.Script(f).Equals(script, StringComparison.OrdinalIgnoreCase);
        o.Scripts[f] = script;
        // A door's OnClick script makes it a transition for the client's cursor and click.
        if (changed && o is Door) c.World.MarkChanged(o);
        return 1;
    }

    // ---- Effects run by EffectRunScript -------------------------------------

    /// <summary>Event context keys the effects system sets when it runs an EffectRunScript script.</summary>
    public const string RunScriptEffectKey = "RunScriptEffect", RunScriptEffectTypeKey = "RunScriptEffectScriptType";

    [NwFunc("GetLastRunScriptEffect")] static object? LastRunScriptEffect(Call c) =>
        c.Event.Values.TryGetValue(RunScriptEffectKey, out var e) && e is Effect eff ? eff : c.Host.DefaultEngineValue(NssType.Effect);
    [NwFunc("GetLastRunScriptEffectScriptType")] static object? LastRunScriptEffectType(Call c) => c.Event.Int(RunScriptEffectTypeKey);

    [NwFunc("HideEffectIcon")] static object? HideEffectIcon(Call c)
    {
        var e = c.E(0).Clone();
        EffectIcons.Hide(e);
        foreach (var l in e.Linked) EffectIcons.Hide(l);
        return e;
    }

    // ---- Script introspection ---------------------------------------------

    static int Level(Call c)
    {
        int i = c.Host.VM.Running.LastIndexOf(c.Thread);
        return i >= 0 ? i : c.Host.VM.Running.Count - 1;
    }

    static ScriptThread? ThreadAt(Call c, int level)
    {
        if (level == -1) return c.Thread;
        var running = c.Host.VM.Running;
        return level >= 0 && level <= Level(c) && level < running.Count ? running[level] : null;
    }

    [NwFunc("GetScriptInstructionsRemaining")] static object? InstructionsRemaining(Call c) =>
        Math.Max(0, c.Thread.VM.InstructionLimit - c.Thread.VM.InstructionsUsed);
    [NwFunc("GetScriptRecursionLevel")] static object? RecursionLevel(Call c) => Math.Max(0, Level(c));
    [NwFunc("GetScriptName")] static object? ScriptName(Call c) => ThreadAt(c, c.I(0, -1))?.Program.Name ?? "";
    [NwFunc("GetScriptChunk")] static object? ScriptChunk(Call c) => ThreadAt(c, c.I(0, -1)) is { } t ? ScriptChunks.Of(t.Program) : "";

    [NwFunc("AbortRunningScript")] static object? Abort(Call c)
    {
        if (c.S(0).Length > 0) c.Host.Log($"[script] {c.Thread.Program.Name}: {c.S(0)}");
        c.Thread.RequestAbort();
        return null;
    }
    [NwFunc("SetJmp")] static object? SetJmp(Call c) { c.Thread.MarkJump(c.S(0)); return 0; }
    [NwFunc("LongJmp")] static object? LongJmp(Call c) { c.Thread.RequestJump(c.S(0), c.I(1, 1)); return null; }
    [NwFunc("GetIsValidJmp")] static object? IsValidJmp(Call c) => c.Thread.HasJump(c.S(0)) ? 1 : 0;

    [NwFunc("GetScriptBacktrace")] static object? Backtrace(Call c)
    {
        var t = c.Thread;
        var frames = new JsonArray { new JsonObject { ["ip"] = t.CallIp, ["bp"] = t.BasePointer, ["sp"] = t.StackPointer, ["file"] = t.Program.Name } };
        foreach (var ret in t.ReturnAddresses) frames.Add(new JsonObject { ["ip"] = ret, ["file"] = t.Program.Name });
        var result = new JsonObject { ["frames"] = frames };
        if (c.I(0, 1) != 0)
        {
            var stack = new JsonArray();
            foreach (var cell in t.StackCells)
            {
                stack.Add(cell.R switch
                {
                    null => new JsonObject { ["type"] = "int", ["data"] = cell.I },
                    string s => new JsonObject { ["type"] = "string", ["data"] = s },
                    JsonValue j => new JsonObject { ["type"] = "json", ["data"] = JsonFunctions.Dump(j) },
                    var other => new JsonObject { ["type"] = other.GetType().Name.ToLowerInvariant() },
                });
            }
            result["stack"] = stack;
        }
        return new JsonValue(result);
    }
}

/// <summary>Effects whose icon was hidden with HideEffectIcon.</summary>
/// <remarks>TODO(effects): move this onto Effect as a field so it survives Effect.Clone().</remarks>
public static class EffectIcons
{
    static readonly ConditionalWeakTable<Effect, object> _hidden = new();
    public static void Hide(Effect e) => _hidden.AddOrUpdate(e, true);
    public static bool IsHidden(Effect e) => _hidden.TryGetValue(e, out _);
}
