using System.Diagnostics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;

/// <summary>
/// The parity lab: runs the same test module on the original game's dedicated server (nwserver, in a
/// sandbox user folder) and on OpenWinter, and compares what the scripts measured.
///
/// A suite is a folder under parity/suites with NWScript files and an optional suite.json:
///   { "seconds": 30, "tileset": "tde01", "tile": 121, "size": 4, "areaFlags": 0, "events": { "OnModLoad": "parity_main" } }
/// Scripts include parity_inc and report with PLog / PLogI / PLogF (a float with an optional
/// tolerance), ending with PDone(). Each line is "PARITY|key|value[|~tolerance]".
///
///   parity-build &lt;suite&gt;        builds out/parity/build/parity_&lt;suite&gt;.mod
///   parity-run &lt;suite&gt; [--only nwn|openwinter]   builds, runs both and prints the comparison
/// </summary>
static partial class Parity
{
    sealed record Suite(string Name, string Dir, int Seconds, string Tileset, int Tile, int Size, Dictionary<string, string> Events, JsonElement Json)
    {
        public int AreaFlags => Get("areaFlags") is { } f ? f.GetInt32() : 0;
        /// <summary>GAME_DIFFICULTY_* for both engines (3 unless the suite says); 0 leaves each at its own default
        /// (nwserver without -difficulty, a new OpenWinter world).</summary>
        public int Difficulty => Math.Clamp(Get("difficulty") is { } v ? v.GetInt32() : 3, 0, 4);
        public int MinPerHour => Get("minPerHour") is { } v ? v.GetInt32() : 2;
        /// <summary>"nwnx": true runs the original side on the Linux nwserver with NWNX:EE (tools/nwnws), and compiles
        /// against NWNX:EE's NWScript headers; "nwnx": "risenholm" also loads that fork's NWNX_Risenholm plugin.</summary>
        public bool Nwnx => Get("nwnx") is { } v && v.ValueKind is JsonValueKind.True or JsonValueKind.String;
        public bool NwnxRisenholm => Get("nwnx") is { ValueKind: JsonValueKind.String } v && v.GetString() == "risenholm";
        /// <summary>"nwnxEnv": { "NWNX_TWEAKS_...": "true" } sets environment variables for NWNX on both engines, after the
        /// lab's defaults (NWNX_SQL_TYPE=SQLITE, NWNX_SQL_DATABASE=nwnx_sql).</summary>
        public Dictionary<string, string> NwnxEnv()
        {
            var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["NWNX_SQL_TYPE"] = "SQLITE", ["NWNX_SQL_DATABASE"] = "nwnx_sql" };
            if (Get("nwnxEnv") is { ValueKind: JsonValueKind.Object } e)
                foreach (var p in e.EnumerateObject()) env[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString()! : p.Value.GetRawText();
            return env;
        }
        public JsonElement? Get(string key) => Json.ValueKind == JsonValueKind.Object && Json.TryGetProperty(key, out var v) ? v : null;
    }

    public static string Repo(LocalConfig cfg) => Path.GetDirectoryName(cfg.SandboxFolder!) is { } outDir ? Path.GetDirectoryName(outDir)! : throw new InvalidOperationException("repo not found");

    static Suite Load(LocalConfig cfg, string name)
    {
        var dir = Path.Combine(Repo(cfg), "parity", "suites", name);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException(dir);
        var json = File.Exists(Path.Combine(dir, "suite.json")) ? JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "suite.json"))).RootElement : default;
        int I(string k, int d) => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(k, out var v) ? v.GetInt32() : d;
        string S(string k, string d) => json.ValueKind == JsonValueKind.Object && json.TryGetProperty(k, out var v) ? v.GetString()! : d;
        var events = new Dictionary<string, string> { ["Mod_OnModLoad"] = "parity_main" };
        if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("events", out var ev))
            foreach (var p in ev.EnumerateObject()) events["Mod_" + p.Name.Replace("Mod_", "")] = p.Value.GetString()!;
        return new Suite(name, dir, I("seconds", 30), S("tileset", "tde01"), I("tile", 121), I("size", 4), events, json.ValueKind == JsonValueKind.Object ? json.Clone() : default);
    }

    static string ModuleName(Suite s) => "parity_" + s.Name;
    static string SandboxUser(LocalConfig cfg, Suite s) => Path.Combine(Repo(cfg), "out", "parity", "user", s.Name);

    /// <summary>neverwinter.nim's tools (the official script compiler), used at development time only.</summary>
    public static string NimTools(LocalConfig cfg)
    {
        foreach (var root in new[] { cfg.UserRoot, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/Neverwinter Nights" })
        {
            var tools = Path.Combine(root, "tools");
            if (!Directory.Exists(tools)) continue;
            var nim = Directory.GetDirectories(tools, "neverwinter.nim*").OrderByDescending(d => d).FirstOrDefault();
            if (nim != null && File.Exists(Path.Combine(nim, "nwn_script_comp.exe"))) return nim;
        }
        throw new FileNotFoundException("neverwinter.nim tools (nwn_script_comp.exe) not found under <userRoot>/tools");
    }

    public static string Build(LocalConfig cfg, string name, bool ownCompiler = false)
    {
        var s = Load(cfg, name);
        var buildDir = Path.Combine(Repo(cfg), "out", "parity", "build", s.Name);
        if (Directory.Exists(buildDir)) Directory.Delete(buildDir, true);
        Directory.CreateDirectory(buildDir);
        var include = Path.Combine(Repo(cfg), "parity", "include");
        if (s.Nwnx) include += "," + NwnxScripts(cfg, s.NwnxRisenholm);
        var sources = Directory.GetFiles(s.Dir, "*.nss").ToList();

        // Compile with the official compiler, so both engines run the same bytecode.
        var nim = NimTools(cfg);
        var psi = new ProcessStartInfo(Path.Combine(nim, "nwn_script_comp.exe")) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "--root", cfg.GameRoot, "--dirs", include + "," + s.Dir, "-d", buildDir, "-y", "-c" }.Concat(sources)) psi.ArgumentList.Add(a);
        using (var p = Process.Start(psi)!)
        {
            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            var errors = output.Split('\n').Where(l => l.StartsWith("E ") || l.Contains(" errored") && !l.Contains(" 0 errored")).ToList();
            if (p.ExitCode != 0 || errors.Count > 0) throw new InvalidOperationException("script compile failed:\n" + output);
        }
        if (ownCompiler)
        {
            // Replace the official compiler's output with OpenWinter's own (ExecuteScriptChunk's compiler).
            var rm = new ResMan(new GamePaths(cfg.GameRoot, SandboxUser(cfg, s), cfg.Language));
            rm.AddBaseGame(includeUserOverride: false);
            var defs = OpenWinter.Game.Scripting.ScriptHost.LoadDefinitions(rm);
            string? Include(string n)
            {
                foreach (var dir in new[] { s.Dir, include })
                    if (File.Exists(Path.Combine(dir, n + ".nss"))) return OpenWinter.Aurora.Text.Windows1252.GetString(File.ReadAllBytes(Path.Combine(dir, n + ".nss")));
                return rm.Read(n, ResType.Nss) is { } b ? OpenWinter.Aurora.Text.Windows1252.GetString(b) : null;
            }
            foreach (var nss in sources)
            {
                var n = Path.GetFileNameWithoutExtension(nss);
                var ncs = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(OpenWinter.Aurora.Text.Windows1252.GetString(File.ReadAllBytes(nss)), n, defs, Include, out var error);
                if (ncs == null)
                {
                    if (error.Contains("NO FUNCTION MAIN")) continue;
                    throw new InvalidOperationException("own compiler failed: " + error);
                }
                File.WriteAllBytes(Path.Combine(buildDir, n + ".ncs"), ncs);
            }
        }

        var entries = new List<ErfEntry>();
        AddFactions(cfg, s.Json, entries);
        void Add(string resref, string ext, byte[] data) => entries.Add(new ErfEntry(resref, ResType.FromExtension(ext), data));
        foreach (var ncs in Directory.GetFiles(buildDir, "*.ncs")) Add(Path.GetFileNameWithoutExtension(ncs), "ncs", File.ReadAllBytes(ncs));
        foreach (var nss in sources) Add(Path.GetFileNameWithoutExtension(nss), "nss", File.ReadAllBytes(nss));
        // Small, authored tables let suites vary one engine parameter without copying installed assets.
        foreach (var table in Directory.GetFiles(s.Dir, "*.2da")) Add(Path.GetFileNameWithoutExtension(table), "2da", File.ReadAllBytes(table));
        entries.AddRange(DataFixtures(cfg, s.Dir, buildDir));
        // Conversations written for the suite (<resref>.dlg.json, the format visual scenes use; see Visual.Dialog).
        foreach (var f in Directory.GetFiles(s.Dir, "*.dlg.json"))
            Add(Path.GetFileName(f)[..^".dlg.json".Length], "dlg", Gff.Write(Visual.Dialog(JsonDocument.Parse(File.ReadAllText(f)).RootElement), "DLG "));

        var creatureFixtures = new Dictionary<string, (string Type, GffStruct Gff)>();
        foreach (var file in Directory.GetFiles(s.Dir, "*.utc.json"))
        {
            var spec = JsonDocument.Parse(File.ReadAllText(file)).RootElement;
            string resref = Path.GetFileName(file)[..^".utc.json".Length];
            foreach (var (fixture, creature) in Visual.CreatureFixtures(cfg, resref, spec))
            {
                creatureFixtures.Add(fixture, ("utc", creature));
                Add(fixture, "utc", Gff.Write(creature, "UTC "));
            }
        }

        // One flat area, or several ("areas": [{ "resref", "tag", "size", "tile", "instances", "areaEvents" }]; the
        // first is the entry area) for suites that need area transitions.
        var areaSpecs = AreaSpecs(s);
        var area = areaSpecs[0].ResRef;
        float centre = areaSpecs[0].Size * 10f / 2f;
        var ifo = new GffStruct()
            .SetWord("Expansion_Pack", 3)
            .SetList("Mod_Area_list", areaSpecs.Select(a => new GffStruct { StructType = 6 }.SetResRef("Area_Name", a.ResRef)).ToList())
            .SetInt("Mod_Creator_ID", 2).SetString("Mod_CustomTlk", "")
            .SetList("Mod_CutSceneList", []).SetByte("Mod_DawnHour", 6).SetByte("Mod_DuskHour", 18)
            .SetLocString("Mod_Description", new LocString(""))
            .SetResRef("Mod_Entry_Area", area).SetFloat("Mod_Entry_Dir_X", 0).SetFloat("Mod_Entry_Dir_Y", 1)
            .SetFloat("Mod_Entry_X", centre).SetFloat("Mod_Entry_Y", centre).SetFloat("Mod_Entry_Z", 0)
            .SetList("Mod_Expan_List", []).SetList("Mod_GVar_List", []).SetList("Mod_HakList", [])
            .Set("Mod_ID", GffType.Void, new byte[16])
            .SetByte("Mod_IsSaveGame", 0).SetString("Mod_MinGameVer", "1.86").SetByte("Mod_MinPerHour", s.MinPerHour)
            .SetLocString("Mod_Name", new LocString(ModuleName(s)))
            .SetByte("Mod_StartDay", 1).SetByte("Mod_StartHour", 13).SetByte("Mod_StartMonth", 6).SetResRef("Mod_StartMovie", "")
            .SetDWord("Mod_StartYear", 1372).SetString("Mod_Tag", "PARITY").SetDWord("Mod_Version", 3).SetByte("Mod_XPScale", 10);
        foreach (var ev in new[] { "OnAcquirItem", "OnActvtItem", "OnClientEntr", "OnClientLeav", "OnCutsnAbort", "OnHeartbeat", "OnModLoad", "OnModStart",
                     "OnPlrChat", "OnPlrDeath", "OnPlrDying", "OnPlrEqItm", "OnPlrLvlUp", "OnPlrRest", "OnPlrUnEqItm", "OnSpawnBtnDn", "OnUnAqreItem", "OnUsrDefined" })
            ifo.SetResRef("Mod_" + ev, s.Events.GetValueOrDefault("Mod_" + ev, ""));
        Add("module", "ifo", Gff.Write(ifo, "IFO "));
        if (ModuleFixtures.Journal(s.Json) is { } jrl) Add("module", "jrl", Gff.Write(jrl, "JRL "));

        var blueprints = Blueprints(cfg, s, Add);
        foreach (var fixture in creatureFixtures) blueprints.Add(fixture.Key, fixture.Value);
        foreach (var a in areaSpecs)
        {
            var tiles = new List<GffStruct>();
            for (int i = 0; i < a.Size * a.Size; i++)
                tiles.Add(new GffStruct { StructType = 1 }
                    .SetByte("Tile_AnimLoop1", 1).SetByte("Tile_AnimLoop2", 1).SetByte("Tile_AnimLoop3", 1)
                    .SetInt("Tile_Height", 0).SetInt("Tile_ID", a.Tile).SetByte("Tile_MainLight1", 16).SetByte("Tile_MainLight2", 0)
                    .SetInt("Tile_Orientation", 0).SetByte("Tile_SrcLight1", 0).SetByte("Tile_SrcLight2", 0));
            string AreaEv(string name) => a.Events is { } ev && ev.TryGetProperty(name, out var v) ? v.GetString()! : "";
            var are = new GffStruct()
                .SetInt("ChanceLightning", 0).SetInt("ChanceRain", 0).SetInt("ChanceSnow", 0).SetString("Comments", "").SetInt("Creator_ID", -1)
                .SetByte("DayNightCycle", 0).SetList("Expansion_List", []).SetDWord("Flags", (uint)s.AreaFlags).SetFloat("FogClipDist", 45)
                .SetInt("Height", a.Size).SetInt("ID", -1).SetByte("IsNight", 0).SetByte("LightingScheme", 0).SetWord("LoadScreenID", 0)
                .SetInt("ModListenCheck", 0).SetInt("ModSpotCheck", 0).SetDWord("MoonAmbientColor", 0).SetDWord("MoonDiffuseColor", 0)
                .SetByte("MoonFogAmount", 0).SetDWord("MoonFogColor", 0).SetByte("MoonShadows", 0).SetLocString("Name", new LocString(a.Name))
                .SetByte("NoRest", 0).SetResRef("OnEnter", AreaEv("OnEnter")).SetResRef("OnExit", AreaEv("OnExit"))
                .SetResRef("OnHeartbeat", AreaEv("OnHeartbeat")).SetResRef("OnUserDefined", AreaEv("OnUserDefined"))
                .SetByte("PlayerVsPlayer", 3).SetResRef("ResRef", a.ResRef).SetByte("ShadowOpacity", 60).SetByte("SkyBox", 0)
                .SetDWord("SunAmbientColor", 0x606060).SetDWord("SunDiffuseColor", 0xA0A0A0).SetByte("SunFogAmount", 0).SetDWord("SunFogColor", 0)
                .SetByte("SunShadows", 1).SetString("Tag", a.Tag).SetList("Tile_List", tiles).SetResRef("Tileset", a.Tileset)
                .SetDWord("Version", 1).SetInt("Width", a.Size).SetInt("WindPower", 0);
            // "are": { "IsNight": 1, "ModSpotCheck": -5, ... } changes area fields (lighting, skill modifiers).
            if (a.Are is { } areSet) Apply(are, areSet);
            Add(a.ResRef, "are", Gff.Write(are, "ARE "));
            var git = new GffStruct()
                .SetStruct("AreaProperties", new GffStruct { StructType = 100 }.SetInt("AmbientSndDay", 0).SetInt("AmbientSndNight", 0)
                    .SetInt("AmbientSndDayVol", 0).SetInt("AmbientSndNitVol", 0).SetInt("EnvAudio", 0).SetInt("MusicBattle", 0)
                    .SetInt("MusicDay", 0).SetInt("MusicNight", 0).SetInt("MusicDelay", 0));
            // "areaProperties": { "MusicDay": 5, "AmbientSndDayVol": 100, ... } sets the area's music and ambient sound.
            if (a.AreaProperties is { } props) Apply(git.GetStruct("AreaProperties")!, props);
            foreach (var list in new[] { "Creature List", "Door List", "Encounter List", "List", "Placeable List", "SoundList", "StoreList", "TriggerList", "WaypointList" })
                git.SetList(list, []);
            Instances(a.Instances, git, blueprints);
            Add(a.ResRef, "git", Gff.Write(git, "GIT "));
            var gic = new GffStruct();
            foreach (var list in new[] { "Creature List", "Door List", "Encounter List", "List", "Placeable List", "SoundList", "StoreList", "TriggerList", "WaypointList" })
                gic.SetList(list, []);
            Add(a.ResRef, "gic", Gff.Write(gic, "GIC "));
        }

        var modDir = Path.Combine(Repo(cfg), "out", "parity", "build", ownCompiler ? "own" : "");
        Directory.CreateDirectory(modDir);
        var mod = Path.Combine(modDir, ModuleName(s) + ".mod");
        File.WriteAllBytes(mod, Erf.Write(entries, "MOD "));
        return mod;
    }

    sealed record AreaSpec(string ResRef, string Tag, string Name, int Size, int Tile, string Tileset, JsonElement? Events, JsonElement? Instances, JsonElement? Are = null, JsonElement? AreaProperties = null);

    /// <summary>The suite's areas: "areas" if given (the first is the entry area), else the one flat "parity" area.</summary>
    static List<AreaSpec> AreaSpecs(Suite s)
    {
        if (s.Get("areas") is not { } areas)
            return [new AreaSpec("parity", "PARITY_AREA", "Parity", s.Size, s.Tile, s.Tileset, s.Get("areaEvents"), s.Get("instances"), s.Get("are"), s.Get("areaProperties"))];
        var list = new List<AreaSpec>();
        foreach (var a in areas.EnumerateArray())
        {
            JsonElement? P(string k) => a.TryGetProperty(k, out var v) ? v : null;
            string resref = P("resref")!.Value.GetString()!;
            list.Add(new AreaSpec(resref, P("tag")?.GetString() ?? resref.ToUpperInvariant(), P("name")?.GetString() ?? resref,
                P("size")?.GetInt32() ?? s.Size, P("tile")?.GetInt32() ?? s.Tile, P("tileset")?.GetString() ?? s.Tileset,
                P("areaEvents") ?? s.Get("areaEvents"), P("instances"), P("are"), P("areaProperties")));
        }
        return list;
    }

    // Optional installed campaign factions for research fixtures. Without repute.fac the original
    // collapses NPC factions, so changing their faction cannot test collision relationships.
    internal static void AddFactions(LocalConfig cfg, JsonElement json, List<ErfEntry> entries)
    {
        if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("factions", out var source)) return;
        string module = source.GetString()!;
        var file = new[] { "nwm", "mod" }.Select(d => Path.Combine(cfg.GameRoot, "data", d, module + "." + d)).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException($"faction source {module} not in installed campaign modules");
        var key = ResKey.Of("repute", ResType.FromExtension("fac"));
        var data = new ErfSource(file).Read(key) ?? throw new FileNotFoundException($"repute.fac not in {module}");
        entries.Add(new ErfEntry("repute", key.Type, data));
    }

    /// <summary>
    /// Derived blueprints: "templates": { "p_door": { "from": "nw_door_metal", "type": "utd", "set": { "OnOpen": "p_open" } } }.
    /// A copy of a base-game blueprint with some fields changed, written into the module.
    /// </summary>
    static Dictionary<string, (string Type, GffStruct Gff)> Blueprints(LocalConfig cfg, Suite s, Action<string, string, byte[]> add)
    {
        var made = new Dictionary<string, (string Type, GffStruct Gff)>(StringComparer.OrdinalIgnoreCase);
        if (s.Get("templates") is not { } templates) return made;
        var baseGame = ResMan.ForClient(new GamePaths(cfg.GameRoot, SandboxUser(cfg, s), cfg.Language), Array.Empty<string>(), "");
        foreach (var t in templates.EnumerateObject())
        {
            string type = t.Value.GetProperty("type").GetString()!, from = t.Value.GetProperty("from").GetString()!;
            var gff = made.TryGetValue(from, out var m) ? m.Gff.DeepClone()
                : baseGame.ReadGff(from, ResType.FromExtension(type)) ?? throw new FileNotFoundException($"{from}.{type} not in the base game");
            gff.SetResRef("TemplateResRef", t.Name);
            if (t.Value.TryGetProperty("set", out var set)) Apply(gff, set);
            if (type == "ute") ModuleFixtures.Creatures(gff, t.Value);
            made[t.Name] = (type, gff);
            add(t.Name, type, Gff.Write(gff, type.ToUpperInvariant().PadRight(4)));
        }
        return made;
    }

    /// <summary>Sets fields from JSON, keeping each existing field's GFF type.</summary>
    internal static void Apply(GffStruct gff, JsonElement set)
    {
        foreach (var f in set.EnumerateObject())
        {
            var v = f.Value;
            // New EE fields can be absent from an installed legacy blueprint. Explicit
            // ResRef values must not become ExoString merely because no prior type exists.
            if (v.ValueKind == JsonValueKind.Object && v.TryGetProperty("resref", out var reference))
            {
                gff.SetResRef(f.Name, reference.GetString()!);
                continue;
            }
            // { "byte": 1 }, { "word": 1 }, { "dword": 0 }: a field the blueprint lacks, with the type the original reads.
            if (v.ValueKind == JsonValueKind.Object && v.EnumerateObject().FirstOrDefault() is { Name: "byte" or "word" or "dword" } typed)
            {
                _ = typed.Name switch
                {
                    "byte" => gff.SetByte(f.Name, typed.Value.GetInt32()),
                    "word" => gff.SetWord(f.Name, typed.Value.GetInt32()),
                    _ => gff.SetDWord(f.Name, typed.Value.GetUInt32()),
                };
                continue;
            }
            switch (gff.Field(f.Name)?.Type)
            {
                case GffType.Byte: gff.SetByte(f.Name, v.GetInt32()); break;
                case GffType.Word: gff.SetWord(f.Name, v.GetInt32()); break;
                case GffType.Short: gff.SetShort(f.Name, v.GetInt32()); break;
                case GffType.DWord: gff.SetDWord(f.Name, v.GetUInt32()); break;
                case GffType.Int: gff.SetInt(f.Name, v.GetInt32()); break;
                case GffType.Float: gff.SetFloat(f.Name, v.GetSingle()); break;
                case GffType.ResRef: gff.SetResRef(f.Name, v.GetString()!); break;
                case GffType.LocString: gff.SetLocString(f.Name, new LocString(v.GetString()!)); break;
                case GffType.ExoString: gff.SetString(f.Name, v.GetString()!); break;
                case null when v.ValueKind == JsonValueKind.String: gff.SetString(f.Name, v.GetString()!); break;
                case null when v.TryGetInt32(out int i): gff.SetInt(f.Name, i); break;
                case null: gff.SetFloat(f.Name, v.GetSingle()); break;
                default: throw new InvalidOperationException($"can't set {f.Name} ({gff.Field(f.Name)!.Type})");
            }
        }
    }

    /// <summary>
    /// Objects placed in the area: "instances": [{ "type": "utd", "from": "p_door", "x": 25, "y": 20, "facing": 90, "set": {...} }].
    /// "from" names a blueprint under "templates". Doors can't be created by script, so they go here; items
    /// ("uti") lie on the ground; sound objects ("uts") can't be created by script either. Encounters ("ute") take
    /// "w", "h", "spawnPoints" and "creatures" (ModuleFixtures).
    /// </summary>
    static void Instances(JsonElement? instances, GffStruct git, Dictionary<string, (string Type, GffStruct Gff)> blueprints)
    {
        if (instances is not { } list) return;
        foreach (var inst in list.EnumerateArray())
        {
            string from = inst.GetProperty("from").GetString()!;
            if (!blueprints.TryGetValue(from, out var bp)) throw new InvalidOperationException($"instance {from}: declare it under templates");
            var gff = bp.Gff.DeepClone();
            if (inst.TryGetProperty("set", out var set)) Apply(gff, set);
            float x = inst.GetProperty("x").GetSingle(), y = inst.GetProperty("y").GetSingle();
            float facing = (inst.TryGetProperty("facing", out var fv) ? fv.GetSingle() : 0) * MathF.PI / 180f;
            if (bp.Type == "ute")
            {
                ModuleFixtures.PlaceEncounter(gff, inst, x, y);
                git.GetList("Encounter List").Add(gff);
                continue;
            }
            if (bp.Type is "utd" or "utp")
                gff.SetFloat("X", x).SetFloat("Y", y).SetFloat("Z", 0).SetFloat("Bearing", facing);
            else if (bp.Type == "utt")
            {
                // A trigger: a w x h rectangle centred on (x, y), its points relative to the position; or "points":
                // [[dx, dy], ...] relative to it (a trigger whose position is a corner, say).
                float hw = (inst.TryGetProperty("w", out var wv) ? wv.GetSingle() : 2) / 2, hh = (inst.TryGetProperty("h", out var hv) ? hv.GetSingle() : 2) / 2;
                var points = inst.TryGetProperty("points", out var pv)
                    ? pv.EnumerateArray().Select(p => (p[0].GetSingle(), p[1].GetSingle())).ToArray()
                    : new[] { (-hw, -hh), (hw, -hh), (hw, hh), (-hw, hh) };
                gff.SetFloat("XPosition", x).SetFloat("YPosition", y).SetFloat("ZPosition", 0)
                    .SetFloat("XOrientation", 0).SetFloat("YOrientation", 0).SetFloat("ZOrientation", 0)
                    .SetList("Geometry", points
                        .Select(p => new GffStruct { StructType = 3 }.SetFloat("PointX", p.Item1).SetFloat("PointY", p.Item2).SetFloat("PointZ", 0)).ToList());
            }
            else
                gff.SetFloat("XPosition", x).SetFloat("YPosition", y).SetFloat("ZPosition", 0)
                    .SetFloat("XOrientation", MathF.Cos(facing)).SetFloat("YOrientation", MathF.Sin(facing));
            (gff.StructType, string listName) = bp.Type switch
            {
                "utd" => (8u, "Door List"),
                "utp" => (9u, "Placeable List"),
                "utc" => (4u, "Creature List"),
                "utw" => (5u, "WaypointList"),
                "utt" => (1u, "TriggerList"),
                "uts" => (6u, "SoundList"),
                "uti" => (0u, "List"),
                _ => throw new InvalidOperationException("can't place a " + bp.Type),
            };
            git.GetList(listName).Add(gff);
        }
    }

    /// <summary>Uncompared notes a suite logs as "RAW|key|text" (stock AI decision traces), kept beside the readings.</summary>
    static void WriteRaw(string path, IEnumerable<string> lines)
    {
        var raw = lines.Select(l => l.IndexOf("RAW|", StringComparison.Ordinal) is var at and >= 0 ? l[(at + 4)..].TrimEnd('\r') : null)
            .OfType<string>().ToList();
        if (raw.Count > 0) File.WriteAllLines(path, raw);
        else if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>One measured value: "PARITY|key|value[|~tolerance]".</summary>
    sealed record Reading(string Key, string Value, double Tolerance);

    /// <summary>A reading as the result files keep it (with its tolerance, so a saved run can be compared again).</summary>
    static string Line(Reading r) => r.Tolerance > 0
        ? $"{r.Key}|{r.Value}|~{r.Tolerance.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : $"{r.Key}|{r.Value}";

    static List<Reading> Parse(IEnumerable<string> lines)
    {
        var list = new List<Reading>();
        foreach (var line in lines)
        {
            int at = line.IndexOf("PARITY|", StringComparison.Ordinal);
            if (at < 0) continue;
            // The value may itself contain '|'; only a trailing "|~number" is a tolerance.
            var text = line[(at + 7)..].TrimEnd('\r', '\n');
            int bar = text.IndexOf('|');
            if (bar < 0) continue;
            string key = text[..bar], value = text[(bar + 1)..];
            double tol = 0;
            int tilde = value.LastIndexOf("|~", StringComparison.Ordinal);
            if (tilde >= 0 && double.TryParse(value[(tilde + 2)..], System.Globalization.CultureInfo.InvariantCulture, out var t)) { tol = t; value = value[..tilde]; }
            list.Add(new Reading(key, value, tol));
        }
        return list;
    }

    /// <summary>NWNX:EE's NWScript headers, copied from the WSL build once (tools/nwnws/nwnws.py scripts).</summary>
    static string NwnxScripts(LocalConfig cfg, bool risenholm = false)
    {
        var dir = Path.Combine(Repo(cfg), "out", "nwnx", "scripts");
        if (!File.Exists(Path.Combine(dir, "nwnx_core.nss"))) Nwnws(cfg, "scripts", "--dest", dir);
        if (risenholm && !File.Exists(Path.Combine(dir, "nwnx_risenholm.nss"))) Nwnws(cfg, "scripts", "--dest", dir, "--risenholm");
        if (!File.Exists(Path.Combine(dir, "nwnx_core.nss"))) throw new FileNotFoundException("NWNX:EE scripts not found; run tools/nwnws/nwnws.py setup");
        return dir;
    }

    static void Nwnws(LocalConfig cfg, params string[] args)
    {
        var psi = new ProcessStartInfo("python") { UseShellExecute = false };
        psi.ArgumentList.Add(Path.Combine(Repo(cfg), "tools", "nwnws", "nwnws.py"));
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"nwnws.py {args[0]} failed");
    }

    /// <summary>Runs the module on the Linux nwserver with NWNX:EE and returns the parity lines.</summary>
    static List<string> RunNwnx(LocalConfig cfg, Suite s, string mod)
    {
        var output = Path.Combine(Repo(cfg), "out", "parity", "results", s.Name, "nwnx.log");
        var args = new List<string> { "run-parity", "--module", mod, "--name", s.Name, "--out", output,
            "--port", (5200 + (int)((uint)StableHash(s.Name + Repo(cfg)) % 700)).ToString(), "--seconds", s.Seconds.ToString(),
            "--difficulty", (s.Difficulty > 0 ? s.Difficulty : 3).ToString() };
        foreach (var table in Directory.GetFiles(s.Dir, "*.2da").Concat(Directory.GetFiles(Path.Combine(Repo(cfg), "out", "parity", "build", s.Name), "*.2da")))
        {
            args.Add("--override");
            args.Add(table);
        }
        if (s.NwnxRisenholm) args.Add("--risenholm");
        if (s.Get("nwnxEnv") is { ValueKind: JsonValueKind.Object } env)
            foreach (var p in env.EnumerateObject())
            {
                args.Add("--env");
                args.Add(p.Name + "=" + (p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText()));
            }
        string exports = Path.Combine(Repo(cfg), "out", "parity", "results", s.Name, "nwn.nwnx");
        if (Exports(s) is not null) { args.Add("--collect"); args.Add(exports); }
        if (File.Exists(output)) File.Delete(output);
        Nwnws(cfg, [.. args]);
        var lines = File.Exists(output) ? File.ReadAllLines(output, OpenWinter.Aurora.Text.Windows1252).ToList() : [];
        if (!lines.Any(l => l.Contains("PARITY|")))
            Console.WriteLine("[nwn] no parity output; server log tail:\n  " + string.Join("\n  ", lines.TakeLast(15)));
        if (Exports(s) is { } files) lines.AddRange(ParityExports.Readings(exports, files));
        return lines.Concat(lines.Select(ErrorReading).OfType<string>()).ToList();
    }

    /// <summary>Runs the module on the original game's dedicated server and returns the parity lines.</summary>
    static List<string> RunNwn(LocalConfig cfg, Suite s, string mod)
    {
        if (s.Nwnx) return RunNwnx(cfg, s, mod);
        if (s.Get("saveReload") is { } save && save.GetBoolean())
        {
            string output = Path.Combine(Repo(cfg), "out", "parity", "results", s.Name, "native-reload.log");
            var saveRun = new ProcessStartInfo("python") { UseShellExecute = false };
            foreach (string arg in new[] { Path.Combine(Repo(cfg), "tools", "parity", "save_control.py"),
                mod, SandboxUser(cfg, s), output, "--port",
                (5200 + (int)((uint)StableHash(s.Name + Repo(cfg)) % 700)).ToString(),
                "--difficulty", s.Difficulty.ToString(), "--seconds", (s.Seconds + 30).ToString() })
                saveRun.ArgumentList.Add(arg);
            using var saveProcess = Process.Start(saveRun)!;
            saveProcess.WaitForExit();
            if (saveProcess.ExitCode != 0) throw new InvalidOperationException("Native save/reload harness failed");
            return File.ReadAllLines(output).SelectMany(line => ErrorReading(line) is { } error
                ? new[] { line, error } : new[] { line }).ToList();
        }
        // A sandbox user folder and port per suite, so suites (and separate checkouts) can run side by side.
        var user = SandboxUser(cfg, s);
        Directory.CreateDirectory(Path.Combine(user, "modules"));
        File.Copy(mod, Path.Combine(user, "modules", Path.GetFileName(mod)), true);
        // Native tables are resolved outside module resources. Restrict overrides to this suite's sandbox.
        var overrides = Path.Combine(user, "override");
        Directory.CreateDirectory(overrides);
        foreach (var old in Directory.GetFiles(overrides, "*.2da")) File.Delete(old);
        foreach (var table in Directory.GetFiles(s.Dir, "*.2da")) File.Copy(table, Path.Combine(overrides, Path.GetFileName(table)), true);
        foreach (var table in Directory.GetFiles(Path.Combine(Repo(cfg), "out", "parity", "build", s.Name), "*.2da"))
            File.Copy(table, Path.Combine(overrides, Path.GetFileName(table)), true);
        var log = Path.Combine(user, "logs", "nwserverLog1.txt");
        if (File.Exists(log)) File.Delete(log);
        var engineLog = Path.Combine(user, "logs", "nwengineLog.txt");
        if (File.Exists(engineLog)) File.Delete(engineLog);
        var exe = Path.Combine(cfg.GameRoot, "bin", "win32", "nwserver.exe");
        // nwserver.exe is a GUI program: CreateNoWindow does nothing for it, so ask for its window hidden (SW_HIDE).
        var psi = new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe)!, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "-userdirectory", user, "-module", ModuleName(s), "-port", (5200 + (int)((uint)StableHash(s.Name + Repo(cfg)) % 700)).ToString(), "-publicserver", "0", "-servername", "openwinter-parity", "-maxclients", "1" })
            psi.ArgumentList.Add(a);
        if (s.Difficulty > 0) { psi.ArgumentList.Add("-difficulty"); psi.ArgumentList.Add(s.Difficulty.ToString()); }
        using var p = Process.Start(psi)!;
        DevDisplay.Follow(p, cfg.DevDisplay);
        p.OutputDataReceived += (_, _) => { };
        p.ErrorDataReceived += (_, _) => { };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        var sw = Stopwatch.StartNew();
        List<string> lines = new();
        while (sw.Elapsed.TotalSeconds < s.Seconds + 60)
        {
            Thread.Sleep(500);
            if (p.HasExited) break;
            if (!File.Exists(log)) continue;
            try
            {
                using var fs = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                lines = new StreamReader(fs, OpenWinter.Aurora.Text.Windows1252).ReadToEnd().Split('\n').ToList();
            }
            catch (IOException) { continue; }
            if (lines.Any(ParityResultGuard.IsComplete)) break;
        }
        if (!p.HasExited) p.Kill(true);
        if (!lines.Any(l => l.Contains("PARITY|")))
            Console.WriteLine($"[nwn] no parity output; server log tail:\n  " + string.Join("\n  ", lines.TakeLast(15)));
        // Script errors go to the engine log.
        if (File.Exists(engineLog))
            using (var fs = new FileStream(engineLog, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                lines.AddRange(new StreamReader(fs, OpenWinter.Aurora.Text.Windows1252).ReadToEnd().Split('\n').Select(ErrorReading).OfType<string>());
        return lines;
    }

    /// <summary>"Script error: name OID: ..., ERROR: text" (both engines log this) as a reading "error.name|text".</summary>
    static string? ErrorReading(string line)
    {
        int at = line.IndexOf("Script error: ", StringComparison.Ordinal);
        if (at < 0) return null;
        var rest = line[(at + 14)..];
        int space = rest.IndexOf(' '), error = rest.IndexOf("ERROR: ", StringComparison.Ordinal);
        return space < 0 || error < 0 ? null : $"PARITY|error.{rest[..space]}|{rest[(error + 7)..].TrimEnd()}";
    }

    static int StableHash(string text)
    {
        unchecked
        {
            int h = 17;
            foreach (char ch in text) h = h * 31 + ch;
            return h;
        }
    }

    /// <summary>Runs the module headless on OpenWinter (no players) and returns the parity lines.</summary>
    /// <summary>The files a suite's "exports" names (written to the user directory's nwnx folder), or null.</summary>
    static List<string>? Exports(Suite s) => s.Get("exports") is { } e ? e.EnumerateArray().Select(f => f.GetString()!).ToList() : null;

    static List<string> RunOpenWinter(LocalConfig cfg, Suite s, string mod)
    {
        var paths = new GamePaths(cfg.GameRoot, SandboxUser(cfg, s), cfg.Language);
        var rm = ResMan.ForModule(paths, mod);
        var world = new OpenWinter.Game.World(rm);
        // A suite comparing exported files gets a fresh user directory of its own, like the original's sandbox.
        string? exportRoot = null;
        if (Exports(s) is not null)
        {
            exportRoot = Path.Combine(Repo(cfg), "out", "parity", "results", s.Name, "openwinter.user");
            if (Directory.Exists(exportRoot)) Directory.Delete(exportRoot, true);
            world.System<OpenWinter.Game.Persistence.PersistenceSystem>().RootOverride = exportRoot;
        }
        if (s.Difficulty > 0) world.GameDifficulty = s.Difficulty;
        world.EnableScripts();
        if (s.Nwnx)
        {
            // NWNX reads its settings from the environment; give OpenWinter's plugins the suite's, as the NWNX side has.
            var env = s.NwnxEnv();
            world.System<OpenWinter.Game.Nwnx.NwnxRegistry>().EnvironmentVariable = name =>
                env.TryGetValue(name, out var v) ? v : name.StartsWith("NWNX_", StringComparison.Ordinal) ? null : Environment.GetEnvironmentVariable(name);
        }
        var lines = new List<string>();
        world.Scripts!.Log = line =>
        {
            if (line.Contains("[script error]") || line.Contains("not implemented")) Console.WriteLine("[openwinter] " + line);
            lines.AddRange(line.Split('\n')); // as the server log file has it
        };
        world.LoadModule();
        var server = new OpenWinter.Game.GameServer(world, new OpenWinter.Game.ServerOptions());
        int ticks = (int)((s.Seconds + 30) * OpenWinter.Game.GameServer.TickRate);
        bool reloaded = false;
        for (int i = 0; i < ticks && !lines.Any(ParityResultGuard.IsComplete); i++)
        {
            server.Tick(1f / OpenWinter.Game.GameServer.TickRate);
            if (!reloaded && s.Get("saveReload") is { } save && save.GetBoolean()
                && lines.Any(l => l.Contains("PARITY|__save__|1")))
            {
                var messages = new List<string>();
                var originalLog = OpenWinter.Game.SaveGames.SaveGameService.Log;
                OpenWinter.Game.SaveGames.SaveGameService.Log = messages.Add;
                try
                {
                    var info = OpenWinter.Game.SaveGames.SaveGameService.Save(server.World, "Parity summon save");
                    OpenWinter.Game.SaveGames.SaveGameRequests.LoadInto(server, info.Folder);
                }
                finally { OpenWinter.Game.SaveGames.SaveGameService.Log = originalLog; }
                var beforeFailures = ParityResultGuard.Failures(lines).Where(f => !f.StartsWith("missing PDone"));
                var failures = beforeFailures.ToList();
                lines.Clear();
                lines.Add("PARITY|__reloaded__|1");
                lines.AddRange(failures.Select(f => "PARITY|error.before-save|" + f));
                foreach (string message in messages)
                {
                    Console.WriteLine(message);
                    if (!message.StartsWith("[save] saved '") && !message.StartsWith("[load] loaded '"))
                        lines.Add("PARITY|error.restore|" + message);
                }
                reloaded = true;
            }
        }
        if (s.Get("saveReload") is { } expected && expected.GetBoolean() && !reloaded)
            lines.Add("PARITY|error.restore|save marker was not reached");
        if (exportRoot != null) lines.AddRange(ParityExports.Readings(Path.Combine(exportRoot, "nwnx"), Exports(s)!));
        lines.AddRange(lines.Select(ErrorReading).OfType<string>().ToList());
        return lines;
    }

    /// <param name="saved">With --only openwinter: compare against the original's readings saved by its last run.</param>
    public static int Run(LocalConfig cfg, string name, string? only, bool ownCompiler = false, bool saved = false)
    {
        var s = Load(cfg, name);
        var mod = Build(cfg, name);
        var openwinterMod = ownCompiler ? Build(cfg, name, ownCompiler: true) : mod;
        Console.WriteLine($"built {mod}");
        var results = Path.Combine(Repo(cfg), "out", "parity", "results", s.Name);
        Directory.CreateDirectory(results);
        List<Reading>? nwn = null, ely = null;
        bool valid = true;
        void Validate(string engine, List<string> lines)
        {
            var failures = ParityResultGuard.Failures(lines);
            if (failures.Count == 0) return;
            valid = false;
            foreach (string failure in failures) Console.WriteLine($"  INVALID {engine}: {failure}");
        }
        if (only != "openwinter")
        {
            var lines = RunNwn(cfg, s, mod);
            Validate("nwn", lines);
            nwn = Parse(lines);
            File.WriteAllLines(Path.Combine(results, "nwn.txt"), nwn.Select(Line));
            WriteRaw(Path.Combine(results, "nwn.raw.txt"), lines);
        }
        if (only != "nwn")
        {
            var lines = RunOpenWinter(cfg, s, openwinterMod);
            Validate("openwinter", lines);
            ely = Parse(lines);
            File.WriteAllLines(Path.Combine(results, "openwinter.txt"), ely.Select(Line));
            WriteRaw(Path.Combine(results, "openwinter.raw.txt"), lines);
        }
        string savedNwn = Path.Combine(results, "nwn.txt");
        if (saved && nwn == null && File.Exists(savedNwn)) nwn = Parse(File.ReadAllLines(savedNwn).Select(l => "PARITY|" + l));
        if (nwn == null || ely == null)
        {
            foreach (var r in (nwn ?? ely)!) Console.WriteLine($"{r.Key} = {r.Value}");
            return valid ? 0 : 1;
        }
        int comparison = Compare(nwn, ely);
        if (!valid) Console.WriteLine("parity: INVALID RUN (matching partial readings do not establish parity)");
        return valid ? comparison : 1;
    }

    static int Compare(List<Reading> nwn, List<Reading> ely)
    {
        // Keys can repeat (a series); compare occurrence by occurrence.
        var a = nwn.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.ToList());
        var b = ely.GroupBy(r => r.Key).ToDictionary(g => g.Key, g => g.ToList());
        int same = 0, differ = 0, missing = 0;
        foreach (var key in a.Keys.Concat(b.Keys).Distinct())
        {
            if (key == "__done__") continue;
            var x = a.GetValueOrDefault(key) ?? new();
            var y = b.GetValueOrDefault(key) ?? new();
            for (int i = 0; i < Math.Max(x.Count, y.Count); i++)
            {
                var n = i < x.Count ? x[i] : null;
                var e = i < y.Count ? y[i] : null;
                string label = x.Count > 1 || y.Count > 1 ? $"{key}[{i}]" : key;
                if (n == null || e == null) { missing++; Console.WriteLine($"  MISSING {label}: nwn={n?.Value ?? "-"} openwinter={e?.Value ?? "-"}"); continue; }
                double tol = Math.Max(n.Tolerance, e.Tolerance);
                bool ok = n.Value == e.Value
                    || tol > 0 && double.TryParse(n.Value, System.Globalization.CultureInfo.InvariantCulture, out var nv)
                       && double.TryParse(e.Value, System.Globalization.CultureInfo.InvariantCulture, out var ev) && Math.Abs(nv - ev) <= tol;
                if (ok) same++;
                else { differ++; Console.WriteLine($"  DIFF    {label}: nwn={n.Value} openwinter={e.Value}{(tol > 0 ? $" (±{tol})" : "")}"); }
            }
        }
        Console.WriteLine($"parity: {same} match, {differ} differ, {missing} missing");
        return differ + missing == 0 ? 0 : 1;
    }
}
