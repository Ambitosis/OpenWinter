using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Shaders;
using OpenWinter.Content;

/// <summary>
/// shader-check [--module X] [--compile] [--show vs,fs] [--out dir]: translates every custom shader a module's
/// materials (MTR) name, and every shader program it ships, and reports which translate and which fall back to the
/// normal material (with the reason). --compile also compiles each result with Godot (headless) and reports its
/// errors; --show prints one pair's translation; --out keeps the translated shaders.
/// </summary>
static class ShaderCheck
{
    /// <summary>Shaders a later phase handles (post-processing, water): reported, not translated.</summary>
    static readonly Dictionary<string, string> Special = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fsfbpostpr"] = "the post-process pass is not supported yet",
        ["vsfbpostpr"] = "the post-process pass is not supported yet",
        ["fswater"] = "the water material is not supported yet",
        ["vswater"] = "the water material is not supported yet",
        ["vswaterc"] = "the water material is not supported yet",
    };

    public static int Run(LocalConfig cfg, ResMan rm, string[] args)
    {
        bool compile = args.Contains("--compile");
        string? show = Arg(args, "--show");
        string outDir = Arg(args, "--out") ?? Path.Combine(Repo(cfg), "out", "shader-check");
        var content = rm.Sources.Where(s => !ShaderSources.IsInstalled(rm, s)).ToList();
        var translator = new ShaderTranslator { ModuleSource = ShaderSources.Module(rm), InstalledSource = ShaderSources.Installed(rm) };

        var shaders = content.SelectMany(s => s.Keys).Where(k => k.Type == ResType.Shd).Select(k => k.ResRef).Distinct().Order().ToList();
        var materials = new List<MaterialData>();
        foreach (var key in content.SelectMany(s => s.Keys).Where(k => k.Type == ResType.Mtr).Distinct())
            if (rm.Read(key) is { } data) materials.Add(Mtr.Parse(data, key.ResRef));
        var custom = materials.Where(m => m.HasCustomShader).ToList();
        Console.WriteLine($"{shaders.Count} shader files, {materials.Count} materials ({custom.Count} with custom shaders)");

        // Each distinct program pair the materials ask for, and every shipped program not named by one.
        var pairs = new Dictionary<(string Vs, string Fs), int>();
        foreach (var m in custom)
        {
            var key = (Vs: m.VertexShader.Length > 0 ? m.VertexShader.ToLowerInvariant() : "vslit", Fs: m.FragmentShader.Length > 0 ? m.FragmentShader.ToLowerInvariant() : "fslit");
            pairs[key] = pairs.GetValueOrDefault(key) + 1;
        }
        var named = pairs.Keys.SelectMany(p => new[] { p.Vs, p.Fs }).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var name in shaders.Where(n => !named.Contains(n)))
        {
            var source = translator.ModuleSource(name) ?? "";
            int type = ShaderType(source);
            if (type == 0) continue; // an include file
            pairs.TryAdd(type == 1 ? (name, "fslit") : ("vslit", name), 0);
        }

        if (show != null)
        {
            var parts = show.Split(',');
            var t = translator.Translate(parts[0], parts.Length > 1 ? parts[1] : "fslit");
            Console.WriteLine(t.Ok ? t.Code : "fallback: " + t.Reason);
            foreach (var n in t.Notes) Console.WriteLine("note: " + n);
            return t.Ok ? 0 : 1;
        }

        var results = new List<(string Vs, string Fs, int Materials, ShaderTranslation? T, string Status)>();
        foreach (var ((vs, fs), count) in pairs.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Vs).ThenBy(p => p.Key.Fs))
        {
            if (Special.TryGetValue(vs, out var why) || Special.TryGetValue(fs, out why))
            {
                results.Add((vs, fs, count, null, "later: " + why));
                continue;
            }
            var t = translator.Translate(vs, fs);
            results.Add((vs, fs, count, t, t.Ok ? "ok" : "fallback: " + t.Reason));
        }

        if (compile)
        {
            var compiled = Compile(cfg, outDir, results.Where(r => r.T is { Ok: true }).Select(r => (Name(r.Vs, r.Fs), r.T!.Code)).ToList());
            for (int i = 0; i < results.Count; i++)
                if (results[i].T is { Ok: true } && compiled.TryGetValue(Name(results[i].Vs, results[i].Fs), out var error) && error.Length > 0)
                    results[i] = results[i] with { Status = "fallback: Godot: " + error };
        }
        else if (Arg(args, "--out") != null)
        {
            Directory.CreateDirectory(outDir);
            foreach (var r in results.Where(r => r.T is { Ok: true }))
                File.WriteAllText(Path.Combine(outDir, Name(r.Vs, r.Fs) + ".gdshader"), r.T!.Code);
        }

        foreach (var r in results)
        {
            Console.WriteLine($"{r.Vs,-12} {r.Fs,-12} {r.Materials,6} materials  {r.Status}");
            if (r.T != null)
                foreach (var note in r.T.Notes.Distinct()) Console.WriteLine($"{"",34}note: {note}");
        }
        int ok = results.Count(r => r.Status == "ok"), later = results.Count(r => r.Status.StartsWith("later")), failed = results.Count - ok - later;
        int okMaterials = results.Where(r => r.Status == "ok").Sum(r => r.Materials);
        Console.WriteLine($"programs: {ok} ok, {failed} fall back, {later} for a later phase; materials drawn with their shaders: {okMaterials} of {custom.Count}");
        return failed == 0 ? 0 : 1;
    }

    static string Name(string vs, string fs) => vs + "__" + fs;

    /// <summary>This checkout (the config may live elsewhere, as for a persistent world's own setup).</summary>
    static string Repo(LocalConfig cfg)
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "OpenWinter.slnx"))) return dir.FullName;
        return Parity.Repo(cfg);
    }

    static string? Arg(string[] args, string name)
    {
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    /// <summary>SHADER_TYPE a shader file sets (1 vertex, 2 fragment), or 0 for an include file (no main).</summary>
    static int ShaderType(string source)
    {
        var text = ShaderPreprocessor.StripComments(source);
        if (!System.Text.RegularExpressions.Regex.IsMatch(text, @"\bvoid\s+main\s*\(")) return 0;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"#\s*define\s+SHADER_TYPE\s+(\d)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 2;
    }

    /// <summary>Compiles each shader with headless Godot; the first error per shader (empty when it compiled).</summary>
    static Dictionary<string, string> Compile(LocalConfig cfg, string dir, List<(string Name, string Code)> shaders)
    {
        var result = new Dictionary<string, string>();
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        foreach (var (name, code) in shaders) File.WriteAllText(Path.Combine(dir, name + ".gdshader"), code);
        File.WriteAllText(Path.Combine(dir, "project.godot"), "config_version=5\n");
        var globals = EngineNames.Globals.Select(g => new { name = g.Name, type = g.Type }).ToList();
        File.WriteAllText(Path.Combine(dir, "check.json"), JsonSerializer.Serialize(new { globals, shaders = shaders.Select(s => s.Name).ToList() }));
        File.Copy(Path.Combine(Repo(cfg), "tools", "shaders", "validate.gd"), Path.Combine(dir, "validate.gd"), true);
        string godot = Godot();
        if (godot.Length == 0)
        {
            Console.WriteLine("Godot not found (set GODOT): shaders not compiled");
            return result;
        }
        var log = Path.Combine(dir, "godot.log");
        var psi = new ProcessStartInfo(godot) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "--headless", "--path", dir, "--log-file", log, "--script", "res://validate.gd" }) psi.ArgumentList.Add(a);
        using (var p = Process.Start(psi)!)
        {
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(300_000);
        }
        string? current = null;
        var errors = new StringBuilder();
        foreach (var line in File.Exists(log) ? File.ReadAllLines(log) : [])
        {
            if (line.StartsWith("OWBEGIN|", StringComparison.Ordinal)) { current = line[8..]; errors.Clear(); }
            else if (line.StartsWith("OWEND|", StringComparison.Ordinal) && current != null)
            {
                var parts = line.Split('|');
                result[current] = parts.Length > 2 && parts[2] == "ok" ? "" : errors.Length > 0 ? errors.ToString().Trim() : "did not compile";
                current = null;
            }
            else if (current != null && line.Contains("SHADER ERROR", StringComparison.Ordinal) && errors.Length == 0)
                errors.Append(line.Trim());
            else if (current != null && errors.Length > 0 && errors.Length < 400 && line.TrimStart().StartsWith("at:", StringComparison.Ordinal))
                errors.Append(' ').Append(line.Trim());
        }
        return result;
    }

    static string Godot()
    {
        if (Environment.GetEnvironmentVariable("GODOT") is { Length: > 0 } env) return env;
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (!Directory.Exists(root)) return "";
        return Directory.EnumerateDirectories(root, "GodotEngine.GodotEngine.Mono_*")
            .SelectMany(d => Directory.EnumerateFiles(d, "Godot_v4*_mono_win64_console.exe", SearchOption.AllDirectories)).FirstOrDefault() ?? "";
    }
}
