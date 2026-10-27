using System.Diagnostics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;

/// <summary>
/// The visual parity harness's module builder. A scene is a folder under parity/visual/scenes with a
/// scene.json and NWScript: vis_main runs when the player enters the area, sets the camera, lights and
/// creatures, and logs "VISUAL|shot|name" whenever both engines should take a screenshot
/// (tools/visual/visual.py drives the original client and OpenWinter and pairs the shots).
///
/// scene.json:
///   "source":  { "module": "Chapter1", "area": "map_m1q1c" }   an area copied from an official module (data/nwm or data/mod),
///              its placeables, doors, sounds and waypoints kept, creatures, encounters, triggers and stores removed
///   or "tileset"/"tile"/"size" (and optional "tiles": [[id, orientation, height], ...] row by row,
///              "mainLight"/"sourceLight" lightcolor rows, which variants may override): a generated area, as in the parity lab
///   "pc":      [x, y, z]  and  "facing": degrees    where the player starts (the camera orbits the player)
///   "are":     { "SunAmbientColor": 6316128, "IsNight": 1, ... }   area fields to override (numbers only)
///   "variants": { "day": { "are": {...} }, "night": { "are": {...} } }   one module per variant
///   "extraAreas": [{ "source": { "module", "area" }, "onEnter": "script" }]   more areas copied from official modules
///              (doors keep their LinkedTo, so the scene can cross between them), or generated ones:
///              [{ "resref", "tag", "tileset", "tile", "size", "are": {...}, "onEnter" }] (multiplayer scenes)
///   "areaProperties": { "MusicDay": 1, "MusicBattle": 34, "AmbientSndDay": 3, "EnvAudio": 8, ... }   the GIT's music and
///              ambient sound (also in variants and generated extraAreas, which can carry "sounds" too)
///   "sounds":  sound objects (see <see cref="AddSounds"/>)
///   "triggers": [{ "tag", "position", "points": [[dx, dy], ...], "linkedTo", "linkedToFlags" }]   triggers in the
///              scene's own area (an area transition when "linkedTo" names a door or waypoint)
///   "seconds": time limit for one run
/// A scene's &lt;resref&gt;.utc.json can specify an installed "template" and exact "hitPoints" for measurements
/// (Visual.Creatures.cs lists the other fields, such as "challengeRating" or a "challengeRatings" series).
/// A scene's &lt;resref&gt;.dlg.json files become conversations in the module (see <see cref="Dialog"/>).
/// Its .mdl, .tga, .txi, .2da, .shd and .mtr files go into the scene's hak (or the module with "moduleResources").
/// </summary>
static partial class Visual
{
    static readonly string[] AreaEvents = ["OnEnter", "OnExit", "OnHeartbeat", "OnUserDefined"];

    public static string ScenesDir(LocalConfig cfg) => Path.Combine(Parity.Repo(cfg), "parity", "visual", "scenes");

    /// <summary>Builds a scene's module(s): out/visual/build/vis_&lt;scene&gt;[_&lt;variant&gt;].mod. Returns their paths.</summary>
    public static List<string> Build(LocalConfig cfg, string name)
    {
        var dir = Path.Combine(ScenesDir(cfg), name);
        if (!Directory.Exists(dir)) throw new DirectoryNotFoundException(dir);
        var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "scene.json"))).RootElement;
        var buildDir = Path.Combine(Parity.Repo(cfg), "out", "visual", "build");
        var scriptDir = Path.Combine(buildDir, "scripts_" + name);
        if (Directory.Exists(scriptDir)) Directory.Delete(scriptDir, true);
        Directory.CreateDirectory(scriptDir);
        var scripts = Compile(cfg, dir, scriptDir);
        foreach (var f in Directory.GetFiles(dir, "*.dlg.json"))
            scripts.Add(new ErfEntry(Path.GetFileName(f)[..^".dlg.json".Length], ResType.FromExtension("dlg"),
                Gff.Write(Dialog(JsonDocument.Parse(File.ReadAllText(f)).RootElement), "DLG ")));
        // Runtime copies of installed blueprints with explicit fixture deltas; no assets live in scene source.
        foreach (var f in Directory.GetFiles(dir, "*.utc.json"))
        {
            var spec = JsonDocument.Parse(File.ReadAllText(f)).RootElement;
            foreach (var (resref, actor) in CreatureFixtures(cfg, Path.GetFileName(f)[..^".utc.json".Length], spec))
                scripts.Add(new ErfEntry(resref, ResType.FromExtension("utc"), Gff.Write(actor, "UTC ")));
        }
        // Runtime store copies keep installed assets out of the authored fixtures.
        foreach (var f in Directory.GetFiles(dir, "*.utm.json"))
        {
            var spec = JsonDocument.Parse(File.ReadAllText(f)).RootElement;
            var resources = ResMan.ForClient(new GamePaths(cfg.GameRoot, cfg.UserRoot, cfg.Language), [], "");
            var store = Gff.Read(resources.Read(spec.GetProperty("template").GetString()!, ResType.Utm)!);
            if (spec.TryGetProperty("empty", out var empty) && empty.GetBoolean())
                foreach (var category in store.GetList("StoreList")) category.SetList("ItemList", []);
            // Authored store terms: {"fields": {"MarkUp": 150, "BlackMarket": 1, ...}} (BlackMarket is a byte, the rest ints).
            if (spec.TryGetProperty("fields", out var fields))
                foreach (var field in fields.EnumerateObject())
                    if (field.Name == "BlackMarket") store.SetByte(field.Name, (byte)field.Value.GetInt32());
                    else store.SetInt(field.Name, field.Value.GetInt32());
            scripts.Add(new ErfEntry(Path.GetFileName(f)[..^".utm.json".Length], ResType.Utm, Gff.Write(store, "UTM ")));
        }
        // Authored models/textures/tables normally go into a hak. An explicit moduleResources
        // control embeds them instead, exercising the single-player presentation distribution path.
        // Shaders and materials too (scene "shaders": a module's own GLSL through an MTR).
        var resourcesForHak = new[] { "*.mdl", "*.tga", "*.2da", "*.txi", "*.shd", "*.mtr" }.SelectMany(p => Directory.GetFiles(dir, p)).ToArray();
        bool embedded = json.TryGetProperty("moduleResources", out var embed) && embed.GetBoolean();
        var presentation = resourcesForHak.Select(m => new ErfEntry(Path.GetFileNameWithoutExtension(m),
            ResType.FromExtension(Path.GetExtension(m).TrimStart('.')), File.ReadAllBytes(m))).ToList();
        // Runtime-cloned tables and items, as in parity suites ("<table>.2da.json", "<item>.uti.json"): tables join the
        // hak (both clients read them from there), items the module.
        foreach (var entry in Parity.DataFixtures(cfg, dir, buildDir))
            (entry.ResType == ResType.FromExtension("2da") ? presentation : scripts).Add(entry);
        // "copyResources": {"ipf_chest300.plt": "ipf_chest186", ...}: installed resources cloned under new names into
        // the hak at build time (hak-added art without committing game assets; armouriconshak).
        if (json.TryGetProperty("copyResources", out var copies))
        {
            var installed = new ResMan(new GamePaths(cfg.GameRoot, Path.Combine(buildDir, "resource-user"), cfg.Language));
            installed.AddBaseGame(includeUserOverride: false);
            foreach (var copy in copies.EnumerateObject())
            {
                ushort type = ResType.FromExtension(Path.GetExtension(copy.Name).TrimStart('.'));
                presentation.Add(new ErfEntry(Path.GetFileNameWithoutExtension(copy.Name), type,
                    installed.Read(copy.Value.GetString()!, type) ?? throw new InvalidDataException($"Missing resource {copy.Value}")));
            }
        }
        if (embedded) scripts.AddRange(presentation);
        string hak = presentation.Count > 0 && !embedded ? "vis_" + name : "";
        if (hak.Length > 0)
            File.WriteAllBytes(Path.Combine(buildDir, hak + ".hak"),
                Erf.Write(presentation, "HAK "));

        var mods = new List<string>();
        var variants = json.TryGetProperty("variants", out var v) ? v.EnumerateObject().Select(p => (p.Name, (JsonElement?)p.Value)).ToList() : [("", null)];
        foreach (var (variant, overrides) in variants)
        {
            string modName = "vis_" + name + (variant.Length > 0 ? "_" + variant : "");
            var entries = new List<ErfEntry>(scripts);
            Parity.AddFactions(cfg, json, entries);
            var (area, are, git) = LoadArea(cfg, json, overrides);
            // "source": { ..., "moduleScripts": ["m1q0ddeadgob_9"] }: compiled scripts the kept creatures run, copied at
            // build time from the official module (the same bytecode on both engines; nothing is committed).
            var sources = new List<JsonElement>();
            if (json.TryGetProperty("source", out var mainSource)) sources.Add(mainSource);
            if (json.TryGetProperty("extraAreas", out var extraSources))
                sources.AddRange(extraSources.EnumerateArray().Where(x => x.TryGetProperty("source", out _)).Select(x => x.GetProperty("source")));
            foreach (var source in sources)
            {
                if (!source.TryGetProperty("moduleScripts", out var moduleScripts)) continue;
                string module = source.GetProperty("module").GetString()!;
                var file = new[] { "nwm", "mod" }.Select(d => Path.Combine(cfg.GameRoot, "data", d, module + "." + d)).First(File.Exists);
                using var erf = new ErfSource(file);
                foreach (var script in moduleScripts.EnumerateArray().Select(x => x.GetString()!))
                    entries.Add(new ErfEntry(script, ResType.FromExtension("ncs"), erf.Read(ResKey.Of(script, ResType.FromExtension("ncs")))
                        ?? throw new FileNotFoundException($"{script}.ncs not in {module}")));
            }
            AddTriggers(git, json);
            // Further areas copied from official modules ("extraAreas": [{ "source": {...}, "onEnter": "script" }]): their
            // doors keep their LinkedTo, so a scene can take a player and creatures from area to area.
            var extra = new List<string>();
            if (json.TryGetProperty("extraAreas", out var extraAreas))
                foreach (var spec in extraAreas.EnumerateArray())
                {
                    var (xArea, xAre, xGit) = LoadArea(cfg, spec, null);
                    ApplyOverrides(xAre, spec);
                    AddSounds(xGit, spec);
                    ApplyAreaProperties(xGit, spec);
                    foreach (var ev in AreaEvents) xAre.SetResRef(ev, "");
                    if (spec.TryGetProperty("onEnter", out var xEnter)) xAre.SetResRef("OnEnter", xEnter.GetString() ?? "");
                    entries.Add(new ErfEntry(xArea, ResType.FromExtension("are"), Gff.Write(xAre, "ARE ")));
                    entries.Add(new ErfEntry(xArea, ResType.FromExtension("git"), Gff.Write(xGit, "GIT ")));
                    var xGic = new GffStruct();
                    foreach (var list in GitLists) xGic.SetList(list, []);
                    entries.Add(new ErfEntry(xArea, ResType.FromExtension("gic"), Gff.Write(xGic, "GIC ")));
                    extra.Add(xArea);
                }
            // Doors cannot be spawned by NWScript; place installed blueprints in the area instead.
            if (json.TryGetProperty("doors", out var doors))
                foreach (var spec in doors.EnumerateArray())
                {
                    var resources = ResMan.ForClient(new GamePaths(cfg.GameRoot, cfg.UserRoot, cfg.Language), [], "");
                    var door = Gff.Read(resources.Read(spec.GetProperty("template").GetString()!, ResType.Utd)!);
                    var xyz = spec.GetProperty("position").EnumerateArray().Select(n => n.GetSingle()).ToArray();
                    door.StructType = 8;
                    door.SetString("Tag", spec.GetProperty("tag").GetString()!).SetFloat("X", xyz[0]).SetFloat("Y", xyz[1]).SetFloat("Z", xyz[2])
                        .SetFloat("Bearing", spec.GetProperty("facing").GetSingle() * MathF.PI / 180f);
                    if (spec.TryGetProperty("linkedTo", out var linked))
                        door.SetString("LinkedTo", linked.GetString()!).SetByte("LinkedToFlags", 1);
                    git.GetList("Door List").Add(door);
                }
            // Encounters: an installed blueprint ("from") with field changes, its creature list, rectangle and spawn points
            // (ModuleFixtures.PlaceEncounter): "encounters": [{ "from", "tag", "x", "y", "w", "h", "set", "creatures", "spawnPoints" }].
            if (json.TryGetProperty("encounters", out var encounters))
                foreach (var spec in encounters.EnumerateArray())
                {
                    var resources = ResMan.ForClient(new GamePaths(cfg.GameRoot, cfg.UserRoot, cfg.Language), [], "");
                    string from = spec.TryGetProperty("from", out var fv) ? fv.GetString()! : "nw_goblingrp";
                    var ute = Gff.Read(resources.Read(from, ResType.FromExtension("ute")) ?? throw new FileNotFoundException($"{from}.ute not installed"));
                    if (spec.TryGetProperty("tag", out var tag)) ute.SetString("Tag", tag.GetString()!);
                    if (spec.TryGetProperty("set", out var set)) Parity.Apply(ute, set);
                    ModuleFixtures.PlaceEncounter(ute, spec, spec.GetProperty("x").GetSingle(), spec.GetProperty("y").GetSingle());
                    git.GetList("Encounter List").Add(ute);
                }
            ApplyOverrides(are, json);
            AddSounds(git, json);
            ApplyAreaProperties(git, json);
            if (overrides is { } o) { ApplyOverrides(are, o); ApplyAreaProperties(git, o); }
            foreach (var ev in AreaEvents) are.SetResRef(ev, "");
            are.SetResRef("OnEnter", "vis_main");
            entries.Add(new ErfEntry(area, ResType.FromExtension("are"), Gff.Write(are, "ARE ")));
            entries.Add(new ErfEntry(area, ResType.FromExtension("git"), Gff.Write(git, "GIT ")));
            var gic = new GffStruct();
            foreach (var list in GitLists) gic.SetList(list, []);
            entries.Add(new ErfEntry(area, ResType.FromExtension("gic"), Gff.Write(gic, "GIC ")));
            entries.Add(new ErfEntry("module", ResType.FromExtension("ifo"), Gff.Write(Ifo(modName, area, json, hak, extra), "IFO ")));
            if (ModuleFixtures.Journal(json) is { } jrl) entries.Add(new ErfEntry("module", ResType.FromExtension("jrl"), Gff.Write(jrl, "JRL ")));
            var mod = Path.Combine(buildDir, modName + ".mod");
            File.WriteAllBytes(mod, Erf.Write(entries, "MOD "));
            VisualCharacter.Write(cfg, json, overrides, mod);
            mods.Add(mod);
        }
        return mods;
    }

    /// <summary>
    /// A conversation written for a scene (the project's own text, never game content):
    /// { "starts": [0], "entries": [{ "text", "animation", "loop", "sound", "speaker", "script", "replies": [0] }],
    ///   "replies": [{ "text", "animation", "script", "entries": [1] }], "preventZoomIn": true, "endScript", "abortScript" }.
    /// Any node can take "quest" and "questEntry" (the journal update the node makes).
    /// Links have no conditions.
    /// </summary>
    internal static GffStruct Dialog(JsonElement json)
    {
        static List<GffStruct> Links(JsonElement node, string name) =>
            node.TryGetProperty(name, out var l) ? l.EnumerateArray().Select((i, k) => new GffStruct((uint)k).SetDWord("Index", (uint)i.GetInt32()).SetResRef("Active", "").SetByte("IsChild", 0)).ToList() : [];
        static GffStruct Node(JsonElement n, bool entry, int index)
        {
            string Str(string k) => n.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
            uint U(string k, uint d) => n.TryGetProperty(k, out var v) ? (uint)v.GetInt64() : d;
            var s = new GffStruct((uint)index)
                .SetLocString("Text", new LocString(Str("text"))).SetDWord("Animation", U("animation", 0)).SetByte("AnimLoop", (int)U("loop", 1))
                .SetResRef("Script", Str("script")).SetDWord("Delay", 0xFFFFFFFF).SetString("Comment", "").SetResRef("Sound", Str("sound")).SetString("Quest", Str("quest"));
            if (n.TryGetProperty("questEntry", out var qe)) s.SetDWord("QuestEntry", (uint)qe.GetInt32());
            if (entry) s.SetString("Speaker", Str("speaker")).SetList("RepliesList", Links(n, "replies"));
            else s.SetList("EntriesList", Links(n, "entries"));
            return s;
        }
        var starts = json.GetProperty("starts").EnumerateArray().Select((i, k) => new GffStruct((uint)k).SetDWord("Index", (uint)i.GetInt32()).SetResRef("Active", "")).ToList();
        return new GffStruct()
            .SetDWord("DelayEntry", 0).SetDWord("DelayReply", 0)
            .SetResRef("EndConversation", json.TryGetProperty("endScript", out var end) ? end.GetString() ?? "" : "")
            .SetResRef("EndConverAbort", json.TryGetProperty("abortScript", out var abort) ? abort.GetString() ?? "" : "")
            .SetDWord("NumWords", 0).SetByte("PreventZoomIn", json.TryGetProperty("preventZoomIn", out var z) && z.GetBoolean() ? 1 : 0)
            .SetList("EntryList", json.GetProperty("entries").EnumerateArray().Select((e, i) => Node(e, true, i)).ToList())
            .SetList("ReplyList", json.TryGetProperty("replies", out var r) ? r.EnumerateArray().Select((e, i) => Node(e, false, i)).ToList() : [])
            .SetList("StartingList", starts);
    }

    static readonly string[] GitLists = ["Creature List", "Door List", "Encounter List", "List", "Placeable List", "SoundList", "StoreList", "TriggerList", "WaypointList"];

    /// <summary>Compiles the scene's scripts with the official compiler (the include folder first).</summary>
    static List<ErfEntry> Compile(LocalConfig cfg, string sceneDir, string outDir)
    {
        var include = Path.Combine(Parity.Repo(cfg), "parity", "visual", "include");
        var sources = Directory.GetFiles(sceneDir, "*.nss").ToList();
        var nim = Parity.NimTools(cfg);
        var psi = new ProcessStartInfo(Path.Combine(nim, "nwn_script_comp.exe")) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in new[] { "--root", cfg.GameRoot, "--dirs", include + "," + sceneDir + "," + Path.Combine(Parity.Repo(cfg), "parity", "include"), "-d", outDir, "-y", "-c" }.Concat(sources)) psi.ArgumentList.Add(a);
        using (var p = Process.Start(psi)!)
        {
            string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            var errors = output.Split('\n').Where(l => l.StartsWith("E ") || l.Contains(" errored") && !l.Contains(" 0 errored")).ToList();
            if (p.ExitCode != 0 || errors.Count > 0) throw new InvalidOperationException("script compile failed:\n" + output);
        }
        var entries = new List<ErfEntry>();
        foreach (var ncs in Directory.GetFiles(outDir, "*.ncs")) entries.Add(new ErfEntry(Path.GetFileNameWithoutExtension(ncs), ResType.FromExtension("ncs"), File.ReadAllBytes(ncs)));
        foreach (var nss in sources) entries.Add(new ErfEntry(Path.GetFileNameWithoutExtension(nss), ResType.FromExtension("nss"), File.ReadAllBytes(nss)));
        return entries;
    }

    /// <summary>
    /// Triggers, area transitions among them: "triggers": [{ "tag", "position": [x,y,z], "points": [[dx,dy], ...],
    /// "linkedTo": "DOOR_OR_WAYPOINT_TAG", "linkedToFlags": 1 (door) or 2 (waypoint) }]; no scripts.
    /// </summary>
    static void AddTriggers(GffStruct git, JsonElement json)
    {
        if (!json.TryGetProperty("triggers", out var triggers)) return;
        foreach (var spec in triggers.EnumerateArray())
        {
            var xyz = spec.GetProperty("position").EnumerateArray().Select(n => n.GetSingle()).ToArray();
            string linkedTo = spec.TryGetProperty("linkedTo", out var lt) ? lt.GetString()! : "";
            bool transition = linkedTo.Length > 0;
            var trigger = new GffStruct(1).SetString("Tag", spec.GetProperty("tag").GetString()!)
                .SetResRef("TemplateResRef", transition ? "newtransition" : "newgeneric")
                .SetString("LinkedTo", linkedTo)
                .SetByte("LinkedToFlags", spec.TryGetProperty("linkedToFlags", out var lf) ? lf.GetInt32() : transition ? 1 : 0)
                .SetInt("Type", transition ? 1 : 0).SetByte("Cursor", transition ? 1 : 0)
                .SetDWord("Faction", 1).SetFloat("HighlightHeight", 0.1f)
                .SetFloat("XPosition", xyz[0]).SetFloat("YPosition", xyz[1]).SetFloat("ZPosition", xyz[2])
                .SetList("Geometry", spec.GetProperty("points").EnumerateArray().Select(p =>
                {
                    var pt = p.EnumerateArray().Select(n => n.GetSingle()).ToArray();
                    return new GffStruct(3).SetFloat("PointX", pt[0]).SetFloat("PointY", pt[1]).SetFloat("PointZ", 0.025f);
                }).ToList());
            git.GetList("TriggerList").Add(trigger);
        }
    }

    /// <summary>The scene's area: copied from an official module, or generated from one tileset.</summary>
    static (string ResRef, GffStruct Are, GffStruct Git) LoadArea(LocalConfig cfg, JsonElement json, JsonElement? variant)
    {
        if (json.TryGetProperty("source", out var src))
        {
            string module = src.GetProperty("module").GetString()!, area = src.GetProperty("area").GetString()!;
            var file = new[] { "nwm", "mod" }.Select(d => Path.Combine(cfg.GameRoot, "data", d, module + (d == "nwm" ? ".nwm" : ".mod"))).FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException($"official module {module} not found under data/nwm or data/mod");
            var erf = new ErfSource(file);
            var are = Gff.Read(erf.Read(ResKey.Of(area, ResType.Are)) ?? throw new FileNotFoundException($"{area}.are not in {module}"));
            var git = Gff.Read(erf.Read(ResKey.Of(area, ResType.FromExtension("git"))) ?? throw new FileNotFoundException($"{area}.git not in {module}"));
            // Keep the scenery; drop everything that acts, so only the scene script decides what moves.
            bool keepCreatures = src.TryGetProperty("keepCreatures", out var kc) && kc.GetBoolean();
            foreach (var list in new[] { "Encounter List", "TriggerList", "StoreList" }.Concat(keepCreatures ? [] : new[] { "Creature List" }))
                git.SetList(list, []);
            foreach (var list in GitLists) if (!git.Has(list)) git.SetList(list, []);
            // Placeables and doors keep their looks but lose their scripts (they belong to the official module).
            foreach (var list in new[] { "Placeable List", "Door List" })
                foreach (var obj in git.GetList(list))
                    foreach (var f in obj.Fields.Where(f => f.Type == GffType.ResRef && f.Label.StartsWith("On")).ToList())
                        f.Value = "";
            return (are.GetString("ResRef", area), are, git);
        }
        // A generated extra area names itself ("resref", optional "tag"); the scene's own area is "visual".
        return json.TryGetProperty("resref", out var resref)
            ? Generate(json, variant, resref.GetString()!, json.TryGetProperty("tag", out var tag) ? tag.GetString()! : resref.GetString()!.ToUpperInvariant())
            : Generate(json, variant);
    }

    /// <summary>A flat generated area, as in the parity lab: one tile everywhere, or an explicit tile list.</summary>
    static (string, GffStruct, GffStruct) Generate(JsonElement json, JsonElement? variant, string area = "visual", string tag = "VISUAL_AREA")
    {
        // A variant's own values (say "mainLight") win over the scene's.
        int I(string k, int d) => variant is { } va && va.TryGetProperty(k, out var x) ? x.GetInt32() : json.TryGetProperty(k, out var v) ? v.GetInt32() : d;
        string S(string k, string d) => json.TryGetProperty(k, out var v) ? v.GetString()! : d;
        int size = I("size", 4), tile = I("tile", 0);
        int width = Math.Clamp(I("width", size), 1, 32), height = Math.Clamp(I("height", size), 1, 32);
        var layout = json.TryGetProperty("tiles", out var t) ? t.EnumerateArray().Select(e => e.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToList() : null;
        var tiles = new List<GffStruct>();
        for (int i = 0; i < width * height; i++)
        {
            var spec = layout != null && i < layout.Count ? layout[i] : [tile, 0, 0];
            tiles.Add(new GffStruct { StructType = 1 }
                .SetByte("Tile_AnimLoop1", 1).SetByte("Tile_AnimLoop2", 1).SetByte("Tile_AnimLoop3", 1)
                .SetInt("Tile_Height", spec.Length > 2 ? spec[2] : 0).SetInt("Tile_ID", spec[0])
                .SetByte("Tile_MainLight1", I("mainLight", 0)).SetByte("Tile_MainLight2", 0)
                .SetInt("Tile_Orientation", spec.Length > 1 ? spec[1] : 0).SetByte("Tile_SrcLight1", I("sourceLight", 0)).SetByte("Tile_SrcLight2", 0));
        }
        var are = new GffStruct()
            .SetInt("ChanceLightning", 0).SetInt("ChanceRain", 0).SetInt("ChanceSnow", 0).SetString("Comments", "").SetInt("Creator_ID", -1)
            .SetByte("DayNightCycle", 0).SetList("Expansion_List", []).SetDWord("Flags", 0).SetFloat("FogClipDist", 45)
            .SetInt("Height", height).SetInt("ID", -1).SetByte("IsNight", 0).SetByte("LightingScheme", 0).SetWord("LoadScreenID", 0)
            .SetInt("ModListenCheck", 0).SetInt("ModSpotCheck", 0).SetDWord("MoonAmbientColor", 0x202020).SetDWord("MoonDiffuseColor", 0x604040)
            .SetByte("MoonFogAmount", 0).SetDWord("MoonFogColor", 0).SetByte("MoonShadows", 0).SetLocString("Name", new LocString(area == "visual" ? "Visual" : area))
            .SetByte("NoRest", 0).SetResRef("OnEnter", "").SetResRef("OnExit", "").SetResRef("OnHeartbeat", "").SetResRef("OnUserDefined", "")
            .SetByte("PlayerVsPlayer", 3).SetResRef("ResRef", area).SetByte("ShadowOpacity", 60).SetByte("SkyBox", 0)
            .SetDWord("SunAmbientColor", 0x606060).SetDWord("SunDiffuseColor", 0xA0A0A0).SetByte("SunFogAmount", 0).SetDWord("SunFogColor", 0)
            .SetByte("SunShadows", 1).SetString("Tag", tag).SetList("Tile_List", tiles).SetResRef("Tileset", S("tileset", "ttr01"))
            .SetDWord("Version", 1).SetInt("Width", width).SetInt("WindPower", 0);
        var git = new GffStruct()
            .SetStruct("AreaProperties", new GffStruct { StructType = 100 }.SetInt("AmbientSndDay", 0).SetInt("AmbientSndNight", 0)
                .SetInt("AmbientSndDayVol", 0).SetInt("AmbientSndNitVol", 0).SetInt("EnvAudio", 0).SetInt("MusicBattle", 0)
                .SetInt("MusicDay", 0).SetInt("MusicNight", 0).SetInt("MusicDelay", 0));
        foreach (var list in GitLists) git.SetList(list, []);
        return (area, are, git);
    }

    /// <summary>
    /// Sound objects placed in an area: "sounds": [{ "tag", "sound" (or "sounds": [...]), "position": [x, y, z],
    /// "positional", "set": { any UTS field: "Looping": 1, "Interval": 2000, "PitchVariation": 0.2, ... } }].
    /// Unless "set" says otherwise a sound is inactive, plays once, at full volume, heard from 5 to 50 m.
    /// </summary>
    static void AddSounds(GffStruct git, JsonElement json)
    {
        if (!json.TryGetProperty("sounds", out var sounds)) return;
        foreach (var spec in sounds.EnumerateArray())
        {
            var xyz = spec.GetProperty("position").EnumerateArray().Select(n => n.GetSingle()).ToArray();
            var names = spec.TryGetProperty("sounds", out var list) ? list.EnumerateArray().Select(n => n.GetString()!).ToList() : [spec.GetProperty("sound").GetString()!];
            var sound = new GffStruct(6).SetString("Tag", spec.GetProperty("tag").GetString()!)
                .SetFloat("XPosition", xyz[0]).SetFloat("YPosition", xyz[1]).SetFloat("ZPosition", xyz[2])
                .SetByte("Active", 0).SetByte("Positional", (byte)(spec.TryGetProperty("positional", out var pos) && pos.GetBoolean() ? 1 : 0))
                .SetByte("Continuous", 0).SetByte("Looping", 0).SetByte("Random", 0).SetByte("RandomPosition", 0)
                .SetFloat("RandomRangeX", 0).SetFloat("RandomRangeY", 0).SetFloat("Elevation", 0)
                .SetDWord("Interval", 0).SetDWord("IntervalVrtn", 0).SetFloat("PitchVariation", 0).SetByte("Priority", 0)
                .SetDWord("Hours", 0).SetByte("Times", 3).SetByte("VolumeVrtn", 0)
                .SetByte("Volume", 127).SetFloat("MinDistance", 5).SetFloat("MaxDistance", 50)
                .SetList("Sounds", names.Select(n => new GffStruct(0).SetResRef("Sound", n)).ToList());
            if (spec.TryGetProperty("set", out var set)) Parity.Apply(sound, set);
            git.GetList("SoundList").Add(sound);
        }
    }

    /// <summary>"areaProperties": { "MusicDay": 1, "AmbientSndDayVol": 127, "EnvAudio": 8, ... }: the GIT's area sound settings.</summary>
    static void ApplyAreaProperties(GffStruct git, JsonElement json)
    {
        if (!json.TryGetProperty("areaProperties", out var props)) return;
        var target = git.GetStruct("AreaProperties");
        if (target == null) git.SetStruct("AreaProperties", target = new GffStruct { StructType = 100 });
        Parity.Apply(target, props);
    }

    /// <summary>Numeric area fields from "are": the field keeps its type; new fields are DWORDs.</summary>
    static void ApplyOverrides(GffStruct are, JsonElement json)
    {
        if (!json.TryGetProperty("are", out var o)) return;
        foreach (var p in o.EnumerateObject())
        {
            var f = are.Field(p.Name);
            if (f == null) { are.SetDWord(p.Name, (uint)p.Value.GetInt64()); continue; }
            f.Value = f.Type switch
            {
                GffType.Byte => (object)(byte)p.Value.GetInt32(),
                GffType.Char => (sbyte)p.Value.GetInt32(),
                GffType.Word => (ushort)p.Value.GetInt32(),
                GffType.Short => (short)p.Value.GetInt32(),
                GffType.DWord => (uint)p.Value.GetInt64(),
                GffType.Int => p.Value.GetInt32(),
                GffType.Float => p.Value.GetSingle(),
                _ => throw new InvalidOperationException($"area field {p.Name} is {f.Type}; only numbers can be overridden"),
            };
        }
    }

    static GffStruct Ifo(string modName, string area, JsonElement json, string hak = "", List<string>? extraAreas = null)
    {
        var pc = json.TryGetProperty("pc", out var p) ? p.EnumerateArray().Select(x => x.GetSingle()).ToArray() : [20f, 20f, 0f];
        float facing = (json.TryGetProperty("facing", out var f) ? f.GetSingle() : 90f) * MathF.PI / 180f;
        var ifo = new GffStruct()
            .SetWord("Expansion_Pack", 3)
            .SetList("Mod_Area_list", new[] { area }.Concat(extraAreas ?? []).Select(a => new GffStruct { StructType = 6 }.SetResRef("Area_Name", a)).ToList())
            .SetInt("Mod_Creator_ID", 2).SetString("Mod_CustomTlk", "")
            .SetList("Mod_CutSceneList", []).SetByte("Mod_DawnHour", 6).SetByte("Mod_DuskHour", 18)
            .SetLocString("Mod_Description", new LocString(""))
            .SetResRef("Mod_Entry_Area", area).SetFloat("Mod_Entry_Dir_X", MathF.Cos(facing)).SetFloat("Mod_Entry_Dir_Y", MathF.Sin(facing))
            .SetFloat("Mod_Entry_X", pc[0]).SetFloat("Mod_Entry_Y", pc[1]).SetFloat("Mod_Entry_Z", pc.Length > 2 ? pc[2] : 0)
            .SetList("Mod_Expan_List", []).SetList("Mod_GVar_List", []).SetList("Mod_HakList", hak.Length > 0 ? [new GffStruct { StructType = 8 }.SetString("Mod_Hak", hak)] : [])
            .Set("Mod_ID", GffType.Void, new byte[16])
            .SetByte("Mod_IsSaveGame", 0).SetString("Mod_MinGameVer", "1.86").SetByte("Mod_MinPerHour", 2)
            .SetLocString("Mod_Name", new LocString(modName))
            .SetByte("Mod_StartDay", 1).SetByte("Mod_StartHour", (byte)(json.TryGetProperty("hour", out var h) ? h.GetInt32() : 13))
            .SetByte("Mod_StartMonth", 6).SetResRef("Mod_StartMovie", "")
            .SetDWord("Mod_StartYear", 1372).SetString("Mod_Tag", "VISUAL").SetDWord("Mod_Version", 3).SetByte("Mod_XPScale", 10);
        foreach (var ev in new[] { "OnAcquirItem", "OnActvtItem", "OnClientEntr", "OnClientLeav", "OnCutsnAbort", "OnHeartbeat", "OnModLoad", "OnModStart",
                     "OnPlrChat", "OnPlrDeath", "OnPlrDying", "OnPlrEqItm", "OnPlrLvlUp", "OnPlrRest", "OnPlrUnEqItm", "OnSpawnBtnDn", "OnUnAqreItem", "OnUsrDefined" })
            ifo.SetResRef("Mod_" + ev, "");
        if (json.TryGetProperty("moduleEvents", out var moduleEvents))
            foreach (var entry in moduleEvents.EnumerateObject())
                ifo.SetResRef("Mod_" + entry.Name, entry.Value.GetString() ?? "");
        return ifo;
    }

    /// <summary>Tiles of a tileset whose four corners are one terrain and that have no crossers (handy flat ground for scenes).</summary>
    public static void FlatTiles(ResMan rm, string tileset)
    {
        var set = Ini.Parse(rm.Read(tileset, ResType.Set) ?? throw new FileNotFoundException(tileset + ".set"));
        int count = set.GetInt("TILES", "Count");
        for (int i = 0; i < count; i++)
        {
            string s = $"TILE{i}";
            var corners = new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" }.Select(c => set.Get(s, c)).Distinct().ToList();
            var heights = new[] { "TopLeftHeight", "TopRightHeight", "BottomLeftHeight", "BottomRightHeight" }.Select(c => set.GetInt(s, c)).Distinct().Count();
            var edges = new[] { "Top", "Right", "Bottom", "Left" }.Select(e => set.Get(s, e)).Where(x => x.Length > 0).ToList();
            if (corners.Count == 1 && heights == 1 && edges.Count == 0)
                Console.WriteLine($"{i,5} {set.Get(s, "Model"),-16} {corners[0],-10} doors={set.GetInt(s, "Doors")} sounds={set.GetInt(s, "Sounds")}");
        }
    }
}
