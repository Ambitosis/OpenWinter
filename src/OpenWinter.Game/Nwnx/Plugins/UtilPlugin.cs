using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting.Functions;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Util: script, resource and time helpers.</summary>
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
    [NwnxFunc(P, "GetModuleMtime")] static void ModuleMtime(NwnxCall n) => n.Push(0);

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
    [NwnxFunc(P, "SetDawnHour")] static void SetDawn(NwnxCall n) => n.World.Clock.DawnHour = Math.Clamp(n.PopInt(), 0, 23);
    [NwnxFunc(P, "SetDuskHour")] static void SetDusk(NwnxCall n) => n.World.Clock.DuskHour = Math.Clamp(n.PopInt(), 0, 23);
    [NwnxFunc(P, "EncodeStringForURL")] static void EncodeUrl(NwnxCall n) => n.Push(Uri.EscapeDataString(n.PopString()));
    [NwnxFunc(P, "Get2DARowCount")] static void RowCount(NwnxCall n) => n.Push(n.World.Resources.TwoDA(n.PopString()).RowCount);
    [NwnxFunc(P, "GetServerTicksPerSecond")] static void Ticks(NwnxCall n) => n.Push((int)GameServer.TickRate);
    [NwnxFunc(P, "GetUserDirectory")] static void UserDirectory(NwnxCall n) => n.Push(n.World.Resources.Paths.UserRoot);
    [NwnxFunc(P, "GetTTY")] static void Tty(NwnxCall n) => n.Push("");
    [NwnxFunc(P, "PluginExists")] static void PluginExists(NwnxCall n) => n.Push(NwnxRegistry.HasPlugin(n.PopString()));

    [NwnxFunc(P, "GetInstructionLimit")] static void GetInstructionLimit(NwnxCall n) => n.Push(n.Host?.VM.InstructionLimit ?? 0);
    [NwnxFunc(P, "SetInstructionLimit")]
    static void SetInstructionLimit(NwnxCall n)
    {
        int limit = n.PopInt();
        if (n.Host != null) n.Host.VM.InstructionLimit = limit > 0 ? limit : Scripting.ScriptVM.DefaultInstructionLimit;
    }
    [NwnxFunc(P, "GetInstructionsExecuted")] static void InstructionsExecuted(NwnxCall n) => n.Push(n.Thread?.InstructionsExecuted ?? 0);

    [NwnxFunc(P, "GetScriptParamIsSet")]
    static void ScriptParamIsSet(NwnxCall n)
    {
        var name = n.PopString();
        n.Push(n.Host != null && n.Host.CurrentEvent.Values.ContainsKey("param:" + name));
    }

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

    [NwnxFunc(P, "SetResourceOverride", UnsupportedReason = "override is stored but resource lookup does not consume it")]
    static void SetResourceOverride(NwnxCall n)
    {
        int type = n.PopInt(); var oldName = n.PopString(); var newName = n.PopString();
        // TODO(resources): ResMan should consult these overrides.
        if (newName.Length == 0) n.Registry.ResourceOverrides.Remove((type, oldName.ToLowerInvariant()));
        else n.Registry.ResourceOverrides[(type, oldName.ToLowerInvariant())] = newName;
    }
    [NwnxFunc(P, "GetResourceOverride")]
    static void GetResourceOverride(NwnxCall n)
    {
        int type = n.PopInt(); var name = n.PopString();
        n.Push(n.Registry.ResourceOverrides.GetValueOrDefault((type, name.ToLowerInvariant())) ?? "");
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
}
