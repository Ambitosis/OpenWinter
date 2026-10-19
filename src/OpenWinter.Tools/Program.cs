using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Textures;
using OpenWinter.Content;

// Developer diagnostics against local game data.
//   openwinter-tools area-textures <area>    texture/alpha/TXI report for an area's tiles
//   openwinter-tools model <resref>          node summary for a model
var cfg = LocalConfig.Find(Directory.GetCurrentDirectory()) ?? throw new InvalidOperationException("config/local.json not found");
var paths = new GamePaths(cfg.GameRoot, cfg.UserRoot, cfg.Language);
OpenWinter.Game.Persistence.PersistenceSystem.SandboxRoot = cfg.WriteRoot;
// --module <folder or .mod/.nwm> anywhere in the arguments overrides the configured module.
int moduleArg = Array.IndexOf(args, "--module");
string modulePath = cfg.Module;
if (moduleArg >= 0 && moduleArg + 1 < args.Length)
{
    modulePath = args[moduleArg + 1];
    args = args.Where((_, i) => i != moduleArg && i != moduleArg + 1).ToArray();
}
var rm = ResMan.ForModule(paths, modulePath);
if (rm.MissingHaks.Count > 0) Console.WriteLine($"missing haks: {string.Join(", ", rm.MissingHaks)}");
var models = new ModelCache(rm);
var textures = new TextureCache(rm);
string cmd = args.Length > 0 ? args[0] : "";

switch (cmd)
{
    case "resource":
    {
        // resource <resref> <ext> [out] [--base]: --base reads the game's own resources (no module or haks).
        var from = args.Contains("--base") ? ResMan.ForClient(paths, Array.Empty<string>(), "") : rm;
        args = args.Where(a => a != "--base").ToArray();
        var bytes = from.Read(args[1], ResType.FromExtension(args[2])) ?? throw new FileNotFoundException(args[1]);
        if (args.Length > 3) File.WriteAllBytes(args[3], bytes);
        else Console.WriteLine(OpenWinter.Aurora.Text.Windows1252.GetString(bytes));
        break;
    }
    case "mesh-data":
    {
        var model = models.Get(args[1])!;
        foreach (var part in model.Parts.Where(p => p.Mesh != null && (args.Length < 3 || p.Name.Equals(args[2], StringComparison.OrdinalIgnoreCase))))
        {
            Console.WriteLine(part.Name);
            for (int i = 0; i < part.Mesh!.Vertices.Length; i++)
                Console.WriteLine($"{i}: {part.Mesh.Vertices[i]} uv={part.Mesh.Uvs.ElementAtOrDefault(i)}");
            Console.WriteLine(string.Join(",", part.Mesh.Indices));
            if (part.Mesh.FaceSurfaces.Length > 0) Console.WriteLine("surfaces " + string.Join(",", part.Mesh.FaceSurfaces));
        }
        break;
    }
    case "tile-geometry-audit":
        if (args.Length < 2) { Console.Error.WriteLine("usage: tile-geometry-audit <output.json>"); return 1; }
        TileGeometryAudit.Run(paths, args[1]);
        break;
    case "room-audit":
    {
        var world = new OpenWinter.Game.World(rm);
        var area = new OpenWinter.Game.Areas.AreaLoader(world).Load(args[1]);
        float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
        if (args.Length > 5) foreach (var d in area.Objects.OfType<OpenWinter.Game.Objects.Door>())
            if (System.Numerics.Vector3.Distance(d.Position, new(F(4), F(5), 0)) < 1) OpenWinter.Game.WorldObjects.DoorService.Open(world, d, d);
        var rooms = OpenWinter.Game.Areas.RoomVisibility.For(world, area);
        Console.WriteLine($"interior={area.IsInterior}");
        if (rooms == null) break;
        var mask = new string('0', area.Width * area.Height).ToCharArray();
        foreach (int i in rooms.TilesIn(area, new(F(2), F(3), 0))) mask[i] = '1';
        for (int y = 0; y < area.Height; y++) Console.WriteLine(new string(mask, y * area.Width, area.Width));
        break;
    }
    case "room-sweep":
    {
        // Every interior area's room masks, one line per origin tile (diff two runs to see what a rule change moves):
        // room-sweep [area...] --module <module>
        var world = new OpenWinter.Game.World(rm);
        var names = args.Length > 1 ? args.Skip(1) : rm.All(ResType.Are).Select(k => k.ResRef).Distinct().OrderBy(n => n);
        foreach (var name in names)
        {
            var area = new OpenWinter.Game.Areas.AreaLoader(world).Load(name);
            if (OpenWinter.Game.Areas.RoomVisibility.For(world, area) is not { } rooms) continue;
            for (int i = 0; i < area.Width * area.Height; i++)
            {
                var mask = rooms.VisibleTiles(area, new((i % area.Width + .5f) * 10, (i / area.Width + .5f) * 10, 0));
                Console.WriteLine($"{name} {i % area.Width},{i / area.Width} {string.Concat(mask.Select(b => b ? '1' : '0'))}");
            }
        }
        break;
    }
    case "set-audit":
    {
        var set = Ini.Parse(rm.Read(args[1], ResType.Set)!);
        foreach (var section in args.Skip(2).DefaultIfEmpty("GENERAL"))
            foreach (var (key, value) in set.Section(section)) Console.WriteLine($"{section}.{key}={value}");
        break;
    }
    case "door-audit":
    {
        var source = ResMan.ForClient(paths, [], "");
        var cache = new ModelCache(source);
        foreach (var name in args.Skip(1).DefaultIfEmpty("t_door01"))
        {
            var model = cache.Get(name)!;
            Console.WriteLine(name);
            Console.WriteLine("animations: " + string.Join(", ", model.Animations.Values.Select(a => $"{a.Name} ({a.Length:0.00} s)")));
            if (cache.GetWalkmesh(name, ResType.Dwk) is { } dwk)
            {
                var globals = dwk.RestGlobals();
                for (int i = 0; i < dwk.Parts.Count; i++)
                {
                    var p = dwk.Parts[i];
                    if (p.Mesh is not { } m) { Console.WriteLine($"dwk {p.Name}: point {globals[i].Translation}"); continue; }
                    var v = m.Vertices.Select(x => System.Numerics.Vector3.Transform(x, globals[i])).ToList();
                    Console.WriteLine($"dwk {p.Name}: x {v.Min(q => q.X):0.00}..{v.Max(q => q.X):0.00} y {v.Min(q => q.Y):0.00}..{v.Max(q => q.Y):0.00}");
                }
            }
            foreach (var part in model.Parts.Where(p => p.Mesh != null))
            {
                var mesh = part.Mesh!;
                Console.WriteLine($"{part.Name}: pos={part.Position} diffuse={mesh.Diffuse} ambient={mesh.Ambient} self={mesh.SelfIllumination} alpha={mesh.Alpha} hint={mesh.TransparencyHint} texture={mesh.Texture}");
                if (mesh.TransparencyHint != 0) Console.WriteLine("vertices=" + string.Join(";", mesh.Vertices));
                foreach (var anim in model.Animations.Values)
                    if (anim.Tracks.TryGetValue(part.Name, out var track) && track.Alphas.Length > 0)
                        Console.WriteLine($"  {anim.Name}: alpha=" + string.Join(",", track.Alphas));
            }
        }
        break;
    }
    case "door-pick-audit":
    {
        // Every door model (genericdoors, doortypes): its visible meshes' bounds and its invisible helper meshes
        // (alpha 0, like the stock "sam" plane across the doorway), which the closed door picks too.
        var source = ResMan.ForClient(paths, [], "");
        var cache = new ModelCache(source);
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var gd = rm.TwoDA("genericdoors"); var dt = rm.TwoDA("doortypes");
        for (int r = 0; r < gd.RowCount; r++) if (gd.Get(r, "ModelName") is { Length: > 0 } n && n != "****") names.Add(n);
        for (int r = 0; r < dt.RowCount; r++) if (dt.Get(r, "Model") is { Length: > 0 } n && n != "****") names.Add(n);
        int withHelper = 0, total = 0;
        foreach (var name in names)
        {
            if (cache.Get(name) is not { } m) continue;
            total++;
            var g = m.RestGlobals();
            System.Numerics.Vector3 vlo = new(float.MaxValue), vhi = new(float.MinValue), hlo = vlo, hhi = vhi;
            var helpers = new List<string>();
            for (int i = 0; i < m.Parts.Count; i++)
            {
                if (m.Parts[i].Mesh is not { Render: true, IsWalkmesh: false } mesh || mesh.Vertices.Length == 0) continue;
                var pts = mesh.Vertices.Select(v => System.Numerics.Vector3.Transform(v, g[i])).ToArray();
                var lo = pts.Aggregate(System.Numerics.Vector3.Min); var hi = pts.Aggregate(System.Numerics.Vector3.Max);
                if (mesh.Alpha <= 0.01f) { helpers.Add(m.Parts[i].Name); hlo = System.Numerics.Vector3.Min(hlo, lo); hhi = System.Numerics.Vector3.Max(hhi, hi); }
                else { vlo = System.Numerics.Vector3.Min(vlo, lo); vhi = System.Numerics.Vector3.Max(vhi, hi); }
            }
            if (helpers.Count > 0) withHelper++;
            bool outside = helpers.Count > 0 && (hlo.X < vlo.X - 0.3f || hhi.X > vhi.X + 0.3f || hlo.Z < vlo.Z - 0.3f || hhi.Z > vhi.Z + 0.3f);
            Console.WriteLine($"{(outside ? "!" : " ")} {name,-20} helpers=[{string.Join(",", helpers)}] visible x=[{vlo.X:0.00},{vhi.X:0.00}] z=[{vlo.Z:0.00},{vhi.Z:0.00}]"
                + (helpers.Count > 0 ? $" helpers x=[{hlo.X:0.00},{hhi.X:0.00}] z=[{hlo.Z:0.00},{hhi.Z:0.00}]" : ""));
        }
        Console.WriteLine($"{withHelper} of {total} door models have invisible helper meshes");
        break;
    }
    case "voice-audit":
        VoiceAudit.Run(args.Contains("--base") ? ResMan.ForClient(paths, Array.Empty<string>(), "") : rm);
        break;
    case "spell-presentation-survey":
        SpellPresentationSurvey.Run(args.Contains("--base") ? ResMan.ForClient(paths, Array.Empty<string>(), "") : rm,
            args.Skip(1).FirstOrDefault(a => !a.StartsWith("--")));
        break;
    case "nss-check":
    {
        // Compiles every script with a main in a folder with OpenWinter's own compiler and reports failures.
        // With an output folder, writes the .ncs files there.
        var dir = args[1];
        var outDir = args.Length > 2 ? args[2] : null;
        var defs = OpenWinter.Game.Scripting.ScriptHost.LoadDefinitions(rm);
        string? Include(string n) =>
            File.Exists(Path.Combine(dir, n + ".nss")) ? OpenWinter.Aurora.Text.Windows1252.GetString(File.ReadAllBytes(Path.Combine(dir, n + ".nss")))
            : rm.Read(n, ResType.Nss) is { } b ? OpenWinter.Aurora.Text.Windows1252.GetString(b) : null;
        int ok = 0, failed = 0, noMain = 0;
        foreach (var f in Directory.GetFiles(dir, "*.nss"))
        {
            var n = Path.GetFileNameWithoutExtension(f);
            var ncs = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(OpenWinter.Aurora.Text.Windows1252.GetString(File.ReadAllBytes(f)), n, defs, Include, out var error);
            if (ncs != null) { ok++; if (outDir != null) File.WriteAllBytes(Path.Combine(outDir, n + ".ncs"), ncs); }
            else if (error.Contains("NO FUNCTION MAIN")) noMain++;
            else { failed++; Console.WriteLine(error); }
        }
        Console.WriteLine($"{ok} compiled, {failed} failed, {noMain} without main");
        break;
    }
    case "parity-build":
        Console.WriteLine(Parity.Build(cfg, args[1]));
        break;
    case "ground-line":
    {
        // ground-line <module> <area> x0 y0 z0 x1 y1 step: every walkable and blocking surface height along a line,
        // and where a walker taking that step size ends up.
        var world = new OpenWinter.Game.World(ResMan.ForModule(paths, args[1]));
        world.LoadModule();
        var area = world.FindArea(args[2])!;
        float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
        var from = new System.Numerics.Vector3(F(3), F(4), F(5));
        var to = new System.Numerics.Vector2(F(6), F(7));
        float step = F(8);
        var pos = from;
        for (int i = 0; i < 400; i++)
        {
            var d = to - new System.Numerics.Vector2(pos.X, pos.Y);
            if (d.Length() < 1e-4f) break;
            var s = d.Length() > step ? System.Numerics.Vector2.Normalize(d) * step : d;
            var surfaces = area.Walkmesh.Triangles.Where(t => t.TryHeightAt(pos.X + s.X, pos.Y + s.Y, out _))
                .Select(t => { t.TryHeightAt(pos.X + s.X, pos.Y + s.Y, out float z); return $"{(t.Walkable ? "w" : "B")}{z:0.00}{(t.Owner != 0 ? "o" : "")}"; });
            var next = OpenWinter.Game.Movement.Step(area, pos, s);
            Console.WriteLine($"{pos.X + s.X:0.000},{pos.Y + s.Y:0.000} from z {pos.Z:0.000}: {string.Join(" ", surfaces)} -> {(next is { } n ? n.Z.ToString("0.000") : "REFUSED")}");
            if (next == null) break;
            pos = next.Value;
        }
        Console.WriteLine($"trace says {area.Walkmesh.Trace(from, new System.Numerics.Vector3(to.X, to.Y, 0), 0.9f):0.000}");
        break;
    }
    case "move-repro":
    {
        // move-repro <module> <area> x y <target tag> [appearance]: a creature walks to an object; prints its progress.
        var world = new OpenWinter.Game.World(ResMan.ForModule(paths, args[1]));
        world.LoadModule();
        var area = world.FindArea(args[2])!;
        float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
        var c = world.Creatures.FromTemplate(new GffStruct());
        c.AppearanceType = args.Length > 6 ? int.Parse(args[6]) : 6;
        world.AddToArea(c, area, new System.Numerics.Vector3(F(3), F(4), area.Walkmesh.HighestGround(F(3), F(4)) ?? 0), 0);
        var target = area.Objects.First(o => o.Tag.Equals(args[5], StringComparison.OrdinalIgnoreCase));
        c.Actions.Add(new OpenWinter.Game.MoveAction { TargetObject = target.Id, Run = true, Range = 1.8f });
        Console.WriteLine($"start {c.Position} target {target.Position}");
        for (int i = 0; i < 30 * 12; i++)
        {
            world.Step(1 / 30f);
            if (i % 15 == 0) Console.WriteLine($"{i / 30f:0.0}s {c.Position} speed={c.Speed:0.00} goal={c.MoveGoal} actions={c.Actions.Count} blocked={c.MoveBlockedBy:X}");
            if (c.Actions.Count == 0) { Console.WriteLine($"done at {c.Position}"); break; }
        }
        break;
    }
    case "path-debug":
    {
        // path-debug <module> x0 y0 x1 y1 radius [--open-south]: the path a creature of that radius would take.
        var world = new OpenWinter.Game.World(ResMan.ForModule(paths, args[1]));
        world.LoadModule();
        var area = world.Areas[0];
        if (args.Contains("--open-south"))
            foreach (var d in area.ObjectsOf<OpenWinter.Game.Objects.Door>())
            {
                var opener = new OpenWinter.Game.Objects.Waypoint { Position = d.Position - new System.Numerics.Vector3(-MathF.Sin(d.Facing), MathF.Cos(d.Facing), 0) * 2 };
                OpenWinter.Game.WorldObjects.DoorService.Open(world, d, opener);
            }
        args = args.Where(a => a != "--open-south").ToArray();
        float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
        // Then obstacle circles: x y r ...
        var obstacles = new List<(System.Numerics.Vector2, float)>();
        for (int i = 7; i + 2 < args.Length; i += 3) obstacles.Add((new System.Numerics.Vector2(F(i), F(i + 1)), F(i + 2)));
        var p = area.Pathfinder.FindPath(new System.Numerics.Vector3(F(2), F(3), 0), new System.Numerics.Vector3(F(4), F(5), 0), obstacles: obstacles, radius: F(6));
        Console.WriteLine(p == null ? "no path" : string.Join(" -> ", p.Select(v => $"({v.X:0.00},{v.Y:0.00})")));
        break;
    }
    case "bench-crowd":
    {
        // bench-crowd <module> [count]: creatures wandering in the module's first area, all perceiving each other;
        // reports the world step time (personal space checks on every step).
        var world = new OpenWinter.Game.World(ResMan.ForModule(paths, args[1]));
        world.LoadModule();
        var area = world.Areas[0];
        int count = args.Length > 2 ? int.Parse(args[2]) : 300;
        var rng = new Random(1);
        var all = new List<OpenWinter.Game.Objects.Creature>();
        for (int i = 0; i < count; i++)
        {
            var c = world.Creatures.FromBlueprint("nw_goblina")!;
            world.AddToArea(c, area, new System.Numerics.Vector3(2 + rng.NextSingle() * (area.Width * 10 - 4), 2 + rng.NextSingle() * (area.Height * 10 - 4), 0), 0);
            // Half wander, half stand busy.
            if (i % 2 == 0) c.Actions.Add(new OpenWinter.Game.RandomWalkAction());
            else c.Actions.Add(new OpenWinter.Game.WaitAction { Remaining = -1 });
            all.Add(c);
        }
        foreach (var a in all) foreach (var b in all) if (a != b) a.Society.Seen.Add(b.Id);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int steps = 30 * 20;
        for (int i = 0; i < steps; i++) world.Step(1 / 30f);
        Console.WriteLine($"{count} creatures: {sw.Elapsed.TotalMilliseconds / steps:0.000} ms per step");
        break;
    }
    case "spot-debug":
    {
        var world = new OpenWinter.Game.World(ResMan.ForModule(paths, args[1]));
        world.LoadModule();
        var area = world.Areas[0];
        float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
        var c = world.Creatures.FromBlueprint(args[4])!;
        Console.WriteLine($"fits={OpenWinter.Game.PersonalSpace.Fits(area, F(2), F(3), 0, OpenWinter.Game.PersonalSpace.Radius(world, c))} free={OpenWinter.Game.PersonalSpace.Free(world, area, c, F(2), F(3), 0)} spot={OpenWinter.Game.PersonalSpace.FindSpot(world, area, c, new System.Numerics.Vector3(F(2), F(3), 0))}");
        break;
    }
    case "walk-map":
    {
        // Walkable ground of a module's first area as a character map ('.' walkable, '#' not), north up:
        // walk-map <module> x0 y0 x1 y1 step
        var world = new OpenWinter.Game.World(ResMan.ForModule(paths, args[1]));
        world.LoadModule();
        int areaArg = Array.IndexOf(args, "--area");
        var area = areaArg > 0 ? world.FindArea(args[areaArg + 1])! : world.Areas[0];
        if (areaArg > 0) args = args.Where((_, k) => k != areaArg && k != areaArg + 1).ToArray();
        // --open: with every door open (swung away from the south/west).
        if (args.Contains("--open"))
            foreach (var d in area.ObjectsOf<OpenWinter.Game.Objects.Door>()) OpenWinter.Game.WorldObjects.DoorService.Open(world, d, null);
        args = args.Where(a => a != "--open").ToArray();
        float F(int i) => float.Parse(args[i], System.Globalization.CultureInfo.InvariantCulture);
        float x0 = F(2), y0 = F(3), x1 = F(4), y1 = F(5), step = F(6);
        for (float y = y1; y >= y0 - 1e-4f; y -= step)
        {
            var line = new System.Text.StringBuilder($"{y,6:0.00} ");
            for (float x = x0; x <= x1 + 1e-4f; x += step)
                line.Append(area.Walkmesh.GroundHeight(x, y, area.Walkmesh.HighestGround(x, y) ?? 0, 0.9f) != null ? '.' : '#');
            Console.WriteLine(line);
        }
        break;
    }
    case "visual-build":
        foreach (var mod in Visual.Build(cfg, args[1])) Console.WriteLine(mod);
        break;
    case "visual-tiles":
        Visual.FlatTiles(rm, args[1]);
        break;
    case "parity-run":
    {
        int only = Array.IndexOf(args, "--only");
        return Parity.Run(cfg, args[1], only >= 0 && only + 1 < args.Length ? args[only + 1] : null, args.Contains("--own-compiler"), args.Contains("--saved"));
    }
    case "area-textures":
    {
        var are = rm.ReadGff(args[1], ResType.Are)!;
        var set = Ini.Parse(rm.Read(are.GetString("Tileset"), ResType.Set)!);
        var seen = new Dictionary<string, (int Count, MeshData Mesh, string Model)>(StringComparer.OrdinalIgnoreCase);
        foreach (var tile in are.GetList("Tile_List"))
        {
            var model = models.Get(set.Get($"TILE{tile.GetInt("Tile_ID")}", "Model"));
            if (model == null) continue;
            foreach (var p in model.Parts)
                if (p.Mesh is { Render: true, IsWalkmesh: false } m && m.Texture.Length > 0)
                    seen[m.Texture] = seen.TryGetValue(m.Texture, out var e) ? (e.Count + 1, e.Mesh, e.Model) : (1, m, model.Name);
        }
        Console.WriteLine($"{"texture",-20} {"fmt",-5} alpha txi-blend decal env              hint  model");
        foreach (var (name, (count, mesh, modelName)) in seen.OrderBy(k => k.Key))
        {
            var t = textures.Get(name);
            Console.WriteLine($"{name,-20} {(t == null ? "----" : t.Format.ToString()),-5} {(t?.HasAlpha == true ? "yes" : "no"),-5} {(t?.Info.HasInfo == true ? t.Info.Blending : "-"),-9} {(t?.Info.Decal == true ? "y" : "-"),-5} {t?.Info.EnvironmentMap,-16} {mesh.TransparencyHint,-5} {modelName}");
        }
        Console.WriteLine($"tileset envmap: {set.Get("GENERAL", "EnvMap")}");
        break;
    }
    case "areas":
    {
        // Every area with its name, tileset and size: areas [filter]
        foreach (var key in rm.All(ResType.Are).OrderBy(k => k.ResRef))
        {
            var are = rm.ReadGff(key.ResRef, ResType.Are);
            if (are == null) continue;
            string name = are.GetString("Name");
            if (args.Length > 1 && !(name + " " + key.ResRef).Contains(args[1], StringComparison.OrdinalIgnoreCase)) continue;
            Console.WriteLine($"{key.ResRef,-20} {are.GetString("Tileset"),-10} {are.GetInt("Width")}x{are.GetInt("Height")}  {name}");
        }
        break;
    }
    case "area-tiles":
    {
        // Tile models of an area: which load, which are missing, node counts: area-tiles <area>
        var are = rm.ReadGff(args[1], ResType.Are)!;
        string tileset = are.GetString("Tileset");
        var setData = rm.Read(tileset, ResType.Set);
        Console.WriteLine($"tileset {tileset} set={(setData == null ? "MISSING" : "ok")}");
        var set = Ini.Parse(setData ?? Array.Empty<byte>());
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var where = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        int width = (int)are.GetInt("Width"), ti = 0;
        foreach (var tile in are.GetList("Tile_List"))
        {
            var name = set.Get($"TILE{tile.GetInt("Tile_ID")}", "Model");
            counts[name] = counts.GetValueOrDefault(name) + 1;
            if (!where.TryGetValue(name, out var list)) where[name] = list = new();
            list.Add($"({ti % width},{ti / width})r{tile.GetInt("Tile_Orientation")}");
            ti++;
        }
        foreach (var (name, count) in counts.OrderBy(k => k.Key))
        {
            var src = rm.Sources.FirstOrDefault(x => x.Contains(ResKey.Of(name, ResType.Mdl)))?.Description ?? "not found";
            ModelData? m = null;
            string err = "";
            try { m = models.Get(name); } catch (Exception e) { err = e.Message; }
            int meshes = m?.Parts.Count(p => p.Mesh is { Render: true, IsWalkmesh: false }) ?? 0;
            int faded = m?.Parts.Count(p => p.TileFade && p.Mesh != null) ?? 0;
            var missingTex = m?.Parts.Where(p => p.Mesh is { Render: true, IsWalkmesh: false } mm && mm.Texture.Length > 0 && textures.Get(mm.Texture) == null).Select(p => p.Mesh!.Texture).Distinct() ?? Enumerable.Empty<string>();
            Console.WriteLine($"{name,-18} x{count,-3} {(m == null ? "FAILED " + err : $"meshes={meshes} fade={faded}")} [{src}] {(missingTex.Any() ? "missing tex: " + string.Join(",", missingTex) : "")} at {string.Join(" ", where[name].Take(8))}");
        }
        break;
    }
    case "texture":
    {
        // Decoded texture summary: texture <resref>
        var t = textures.Get(args[1]);
        if (t == null) { Console.WriteLine($"{args[1]}: {textures.KindOf(args[1])}"); break; }
        Console.WriteLine($"{args[1]} {t.Width}x{t.Height} {t.Format} mips={t.MipCount} alpha={t.HasAlpha} flipV={t.FlipV} bytes={t.Data.Length} blend={t.Info.Blending} env={t.Info.EnvironmentMap}");
        Console.WriteLine("  first bytes: " + string.Join(" ", t.Data.Take(32).Select(b => b.ToString("x2"))));
        // texture <resref> <out.rgba>: raw decoded pixels (first mip) for viewing with an image tool.
        if (args.Length > 2) File.WriteAllBytes(args[2], t.Data.Take(t.Width * t.Height * 4).ToArray());
        break;
    }
    case "find-texture":
    {
        // Models whose name starts with a prefix and that use a texture matching a pattern: find-texture <model prefix> <texture substring>
        foreach (var key in rm.All(ResType.Mdl).Where(k => k.ResRef.StartsWith(args[1], StringComparison.OrdinalIgnoreCase)).OrderBy(k => k.ResRef))
        {
            ModelData? m;
            try { m = models.Get(key.ResRef); } catch (Exception e) { Console.WriteLine($"{key.ResRef}: FAILED {e.Message}"); continue; }
            if (m == null) { Console.WriteLine($"{key.ResRef}: FAILED"); continue; }
            var hits = m.Parts.Where(p => p.Mesh != null && p.Mesh.Texture.Contains(args[2], StringComparison.OrdinalIgnoreCase)).Select(p => p.Mesh!.Texture).Distinct().ToList();
            if (hits.Count > 0) Console.WriteLine($"{key.ResRef}: {string.Join(", ", hits)}");
        }
        break;
    }
    case "merged-meshes":
    {
        // Models (by name prefix) with meshes that draw more triangles than their face array holds: merged-meshes <prefix>
        int total = 0, hit = 0;
        foreach (var key in rm.All(ResType.Mdl).Where(k => k.ResRef.StartsWith(args.Length > 1 ? args[1] : "", StringComparison.OrdinalIgnoreCase)))
        {
            total++;
            ModelData? m;
            try { m = models.Get(key.ResRef); } catch { continue; }
            var merged = m?.Parts.Where(p => p.Mesh is { Render: true } mesh && mesh.Indices.Length > mesh.FaceSurfaces.Length * 3).ToList();
            if (merged is not { Count: > 0 }) continue;
            hit++;
            if (hit <= 40) Console.WriteLine($"{key.ResRef}: {string.Join(", ", merged.Select(p => $"{p.Name} {p.Mesh!.FaceSurfaces.Length}->{p.Mesh.Indices.Length / 3}"))}");
        }
        Console.WriteLine($"{hit} of {total} models have merged meshes");
        break;
    }
    case "model-bounds":
    {
        // Model-space bounds of each mesh node: model-bounds <resref>
        var m = models.Get(args[1]) ?? throw new FileNotFoundException(args[1]);
        var g = m.RestGlobals();
        for (int i = 0; i < m.Parts.Count; i++)
        {
            if (m.Parts[i].Mesh is not { } mesh || mesh.Vertices.Length == 0) continue;
            var pts = mesh.Vertices.Select(v => System.Numerics.Vector3.Transform(v, g[i])).ToArray();
            var lo = pts.Aggregate(System.Numerics.Vector3.Min); var hi = pts.Aggregate(System.Numerics.Vector3.Max);
            Console.WriteLine($"  {m.Parts[i].Name,-20} tex={mesh.Texture,-16} fade={m.Parts[i].TileFadeMode} render={(mesh.Render ? 1 : 0)} walk={(mesh.IsWalkmesh ? 1 : 0)} x=[{lo.X:0.000},{hi.X:0.000}] y=[{lo.Y:0.000},{hi.Y:0.000}] z=[{lo.Z:0.000},{hi.Z:0.000}] tris={mesh.Indices.Length / 3}");
        }
        break;
    }
    case "animation-events":
    {
        var m = models.Get(args[1]) ?? throw new FileNotFoundException(args[1]);
        foreach (var (name, anim, inherited) in models.AllAnimations(m))
            if (args.Length > 2 ? name.Equals(args[2], StringComparison.OrdinalIgnoreCase) : anim.Events.Count > 0)
                Console.WriteLine($"{name} ({anim.Length:0.###}s, transition {anim.Transition:0.###}s): " + string.Join(", ", anim.Events.Select(e => $"{e.Time:0.###}:{e.Name}")));
        break;
    }
    case "item-anims":
    {
        // Which held-item part models (baseitems.2da ItemClass, ModelType 0/2) carry animations through their
        // supermodel chain, and which nodes those animations key: item-anims [baseitem]
        var bi = rm.TwoDA("baseitems");
        var src = ResMan.ForClient(paths, Array.Empty<string>(), "");
        var all = src.All(ResType.Mdl).Select(k => k.ResRef.ToLowerInvariant()).ToHashSet();
        for (int row = 0; row < bi.RowCount; row++)
        {
            if (args.Length > 1 && row != int.Parse(args[1])) continue;
            string cls = bi.Get(row, "ItemClass").ToLowerInvariant();
            int mt = bi.GetInt(row, "ModelType", -1);
            if (cls.Length == 0 || mt is not (0 or 2) || (bi.GetInt(row, "EquipableSlots", 0) & 0x30) == 0) continue;
            var summary = new SortedDictionary<string, (int Models, string Example, string Nodes, string Clips)>();
            foreach (var name in all.Where(n => n.StartsWith(cls + "_") && !n.Contains("_fx")))
            {
                if (models.Get(name) is not { } m) continue;
                foreach (var dp in m.Parts.Where(p => p.Mesh is { IsDangly: true }))
                    Console.WriteLine($"{row} {name}: danglymesh {dp.Name}");
                var anims = models.AllAnimations(m).ToList();
                if (anims.Count == 0) continue;
                string root = anims.Select(a => a.Animation.Root).FirstOrDefault() ?? "";
                var nodes = anims.SelectMany(a => a.Animation.Tracks.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order();
                string key = root.ToLowerInvariant();
                (int Models, string Example, string Nodes, string Clips) cur = summary.GetValueOrDefault(key, (0, name, string.Join(" ", nodes), string.Join(" ", anims.Select(a => a.Name))));
                summary[key] = (cur.Models + 1, cur.Example, cur.Nodes, cur.Clips);
            }
            foreach (var (root, s) in summary)
                Console.WriteLine($"{row} {bi.Get(row, "label")} {cls}: {s.Models} models e.g. {s.Example} root={root} nodes=[{s.Nodes}]\n    clips: {s.Clips}");
        }
        break;
    }
    case "model":
    {
        var m = models.Get(args[1]) ?? throw new FileNotFoundException(args[1]);
        Console.WriteLine($"{m.Name} super={m.Supermodel} class={m.Classification} parts={m.Parts.Count} anims={m.Animations.Count}");
        Console.WriteLine("  animations: " + string.Join(", ", m.Animations.Values.Select(a => $"{a.Name}({a.Length:0.##}s)")));
        foreach (var a in m.Animations.Values)
            foreach (var (node, t) in a.Tracks.Where(t => t.Value.BirthRates.Length > 0 || t.Value.LightMultipliers.Length > 0 || t.Value.LightColors.Length > 0 || t.Value.LightRadii.Length > 0))
                Console.WriteLine($"  {a.Name}/{node}: " + (t.BirthRates.Length > 0 ? "birthrate " + string.Join(" ", t.BirthRateTimes.Zip(t.BirthRates, (x, y) => $"{x:0.##}:{y:0.##}")) : "")
                    + (t.LightMultipliers.Length > 0 ? " light " + string.Join(" ", t.LightTimes.Zip(t.LightMultipliers, (x, y) => $"{x:0.##}:{y:0.##}")) : "")
                    + (t.LightColors.Length > 0 ? " colour " + string.Join(" ", t.LightColorTimes.Zip(t.LightColors, (x, y) => $"{x:0.##}:({y.X:0.##},{y.Y:0.##},{y.Z:0.##})")) : "")
                    + (t.LightRadii.Length > 0 ? " radius " + string.Join(" ", t.LightRadiusTimes.Zip(t.LightRadii, (x, y) => $"{x:0.##}:{y:0.##}")) : ""));
        foreach (var p in m.Parts)
            Console.WriteLine($"  {p.Name,-24} parent={p.Parent,-3} pos={p.Position} rot={p.Rotation} {(p.Mesh != null ? $"mesh tex={p.Mesh.Texture} v={p.Mesh.Vertices.Length} uv={p.Mesh.Uvs.Length} uvx=[{(p.Mesh.Uvs.Length > 0 ? $"{p.Mesh.Uvs.Min(u => u.X):0.00}..{p.Mesh.Uvs.Max(u => u.X):0.00}" : "-")}] uvy=[{(p.Mesh.Uvs.Length > 0 ? $"{p.Mesh.Uvs.Min(u => u.Y):0.00}..{p.Mesh.Uvs.Max(u => u.Y):0.00}" : "-")}] tris={p.Mesh.Indices.Length / 3} render={p.Mesh.Render} walk={p.Mesh.IsWalkmesh} alpha={p.Mesh.Alpha} hint={p.Mesh.TransparencyHint} fade={p.TileFadeMode}" : "")}{(p.Light != null ? $"light r={p.Light.Radius} c={p.Light.Color} mult={p.Light.Multiplier} dyn={p.Light.IsDynamic} affdyn={p.Light.AffectsDynamic} shadow={p.Light.Shadow} prio={p.Light.Priority} pos={p.Position}" : "")}{(p.Emitter != null ? $"emitter {p.Emitter.Update}/{p.Emitter.Render}/{p.Emitter.Blend} tex={p.Emitter.Texture} rate={p.Emitter.BirthRate}" : "")}");
        break;
    }
    case "gui":
    {
        // gui <name|file.gui>: a classic interface layout as a tree (kind, tag, model, position, label box, caption).
        var data = File.Exists(args[1]) ? File.ReadAllBytes(args[1]) : rm.Read(args[1], ResType.Gui) ?? throw new FileNotFoundException(args[1]);
        var layout = GuiReader.Read(data, Path.GetFileNameWithoutExtension(args[1]));
        void Print(GuiNode n, string indent)
        {
            string caption = n.Caption is { } c ? $" caption=\"{(c.StrRef != uint.MaxValue ? rm.TlkString(c.StrRef) : c.Text)}\" font={c.Font} align=({c.AlignH},{c.AlignV}) rgba=({c.Color.X:0.##},{c.Color.Y:0.##},{c.Color.Z:0.##},{c.Color.W:0.##})" : "";
            Console.WriteLine($"{indent}{n.Kind} {n.Tag} model={n.ResRef} hook={n.ParentHook} layer={n.Layer}"
                + (n.HasPosition ? $" at=({n.Position.X:0.###},{n.Position.Y:0.###},{n.Position.Z:0.###})" : "")
                + (n.HasLabel ? $" label=({n.LabelPosition.X:0.###},{n.LabelPosition.Y:0.###} {n.LabelSize.X:0.###}x{n.LabelSize.Y:0.###})" : "")
                + (n.Checked is { } ch ? $" checked={ch}" : "") + (n.Length is { } len ? $" length={len}" : "") + caption);
            foreach (var child in n.Children) Print(child, indent + "  ");
        }
        Print(layout.Root, "");
        break;
    }
    case "gui-model":
    {
        // gui-model <resref>: interface model nodes with their model-space position and mesh rectangles (x, y, w, h).
        var m = models.Get(args[1]) ?? throw new FileNotFoundException(args[1]);
        var g = m.RestGlobals();
        Console.WriteLine($"{m.Name} anims: {string.Join(", ", m.Animations.Values.Select(a => $"{a.Name}({a.Length:0.##}s, {a.Tracks.Count} tracks)"))}");
        for (int i = 0; i < m.Parts.Count; i++)
        {
            var p = m.Parts[i];
            var at = System.Numerics.Vector3.Transform(System.Numerics.Vector3.Zero, g[i]);
            string mesh = "";
            if (p.Mesh is { Vertices.Length: > 0 } me)
            {
                var vs = me.Vertices.Select(v => System.Numerics.Vector3.Transform(v, g[i])).ToArray();
                float x0 = vs.Min(v => v.X), x1 = vs.Max(v => v.X), y0 = vs.Min(v => v.Y), y1 = vs.Max(v => v.Y);
                mesh = $" rect=({x0:0.###},{y0:0.###} {x1 - x0:0.###}x{y1 - y0:0.###}) z={vs.Average(v => v.Z):0.###} tex={me.Texture} uv=({me.Uvs.Min(u => u.X):0.###}..{me.Uvs.Max(u => u.X):0.###}, {me.Uvs.Min(u => u.Y):0.###}..{me.Uvs.Max(u => u.Y):0.###}) v={me.Vertices.Length} render={me.Render} alpha={me.Alpha}";
            }
            Console.WriteLine($"  {new string(' ', Depth(i) * 2)}{p.Name} at=({at.X:0.###},{at.Y:0.###},{at.Z:0.###}){mesh}");
        }
        foreach (var a in m.Animations.Values)
            foreach (var (node, t) in a.Tracks)
                Console.WriteLine($"  anim {a.Name}/{node}: pos {string.Join(" ", t.Positions.Select(v => $"({v.X:0.##},{v.Y:0.##},{v.Z:0.##})"))} alpha {string.Join(" ", t.Alphas.Select(v => v.ToString("0.##")))}");
        int Depth(int i) { int d = 0; while (m.Parts[i].Parent >= 0) { i = m.Parts[i].Parent; d++; } return d; }
        break;
    }
    case "walkmesh-nodes":
    {
        // Node positions of a placeable or door walkmesh (use points): walkmesh-nodes <resref> pwk|dwk
        var w = models.GetWalkmesh(args[1], ResType.FromExtension(args[2])) ?? throw new FileNotFoundException(args[1] + "." + args[2]);
        var globals = w.RestGlobals();
        for (int i = 0; i < w.Parts.Count; i++)
        {
            var mesh = w.Parts[i].Mesh;
            string box = "";
            if (mesh != null && mesh.Vertices.Length > 0)
            {
                var vs = mesh.Vertices.Select(v => System.Numerics.Vector3.Transform(v, globals[i])).ToList();
                box = $" x {vs.Min(v => v.X):0.00}..{vs.Max(v => v.X):0.00} y {vs.Min(v => v.Y):0.00}..{vs.Max(v => v.Y):0.00} z {vs.Min(v => v.Z):0.00}..{vs.Max(v => v.Z):0.00}";
            }
            Console.WriteLine($"{w.Parts[i].Name,-20} {globals[i].Translation} mesh={(mesh != null ? mesh.Vertices.Length : 0)}{box}");
        }
        break;
    }
    case "area-doors":
    {
        var git = rm.ReadGff(args[1], ResType.Git)!;
        foreach (var d in git.GetList("Door List"))
        {
            int app = (int)d.GetInt("Appearance"), gen = (int)d.GetInt("GenericType");
            string model = app > 0 ? rm.TwoDA("doortypes").Get(app, "Model") : rm.TwoDA("genericdoors").Get(gen, "ModelName");
            var m = models.Get(model);
            var texs = m?.Parts.Where(p => p.Mesh != null && p.Mesh.Render).Select(p => p.Mesh!.Texture).Distinct() ?? Enumerable.Empty<string>();
            Console.WriteLine($"{d.GetString("Tag"),-24} app={app} gen={gen} model={model} found={m != null} textures=[{string.Join(", ", texs.Select(t => $"{t}:{textures.KindOf(t)}"))}]");
        }
        break;
    }
    case "emitter-survey":
    {
        // Inventory of every emitter in the base game's models: emitter-survey [prefix]
        // Tallies each header field's values, each controller's use and range, and the controllers animations key.
        var src = ResMan.ForClient(paths, Array.Empty<string>(), "");
        string prefix = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--")) ?? "";
        var fieldValues = new Dictionary<string, Dictionary<string, (int Count, string Example)>>(StringComparer.OrdinalIgnoreCase);
        var ctrl = new Dictionary<string, (int Count, int Keyed, float[] Min, float[] Max, string Example)>(StringComparer.OrdinalIgnoreCase);
        var animated = new Dictionary<string, (int Count, int Multi, HashSet<string> Anims, string Example)>(StringComparer.OrdinalIgnoreCase);
        int modelCount = 0, withEmitters = 0, emitterCount = 0, ascii = 0, failed = 0;
        var byPrefix = new Dictionary<string, int>();
        foreach (var key in src.All(ResType.Mdl).Where(k => k.ResRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k.ResRef))
        {
            var buf = src.Read(key);
            if (buf == null || buf.Length < 12) continue;
            modelCount++;
            List<RawEmitter> list;
            try
            {
                bool bin = MdlBinaryReader.IsBinary(buf);
                if (!bin) ascii++;
                list = bin ? MdlBinaryReader.SurveyEmitters(buf) : MdlAsciiReader.SurveyEmitters(buf);
            }
            catch { failed++; continue; }
            if (list.Count == 0) continue;
            withEmitters++;
            string pre = key.ResRef.Length >= 3 ? key.ResRef[..3].ToLowerInvariant() : key.ResRef;
            byPrefix[pre] = byPrefix.GetValueOrDefault(pre) + list.Count;
            foreach (var e in list)
            {
                emitterCount++;
                string where = $"{key.ResRef}/{e.Node}";
                if (e.Fields.TryGetValue("update", out var up) && e.Fields.TryGetValue("render", out var rn))
                    e.Fields["update+render"] = up + "+" + rn + (e.Fields.TryGetValue("blend", out var bl) ? "+" + bl : "");
                foreach (var (f, v) in e.Fields)
                {
                    if (!fieldValues.TryGetValue(f, out var vals)) fieldValues[f] = vals = new();
                    var norm = f is "texture" or "chunkName" ? (v.Length == 0 || v.Equals("null", StringComparison.OrdinalIgnoreCase) ? "(none)" : "(name)") : v;
                    vals[norm] = vals.TryGetValue(norm, out var c) ? (c.Count + 1, c.Example) : (1, where);
                }
                foreach (var (c, (rows, values)) in e.Controllers)
                {
                    if (!ctrl.TryGetValue(c, out var t)) t = (0, 0, values.ToArray(), values.ToArray(), where);
                    for (int i = 0; i < values.Length && i < t.Min.Length; i++) { t.Min[i] = Math.Min(t.Min[i], values[i]); t.Max[i] = Math.Max(t.Max[i], values[i]); }
                    ctrl[c] = (t.Count + 1, t.Keyed + (rows > 1 ? 1 : 0), t.Min, t.Max, t.Example);
                }
                foreach (var (anim, c, rows, _, _) in e.Animated.DistinctBy(a => (a.Animation, a.Controller)))
                {
                    if (!animated.TryGetValue(c, out var t)) t = (0, 0, new HashSet<string>(StringComparer.OrdinalIgnoreCase), "");
                    t.Anims.Add(anim);
                    if (rows > 1 && t.Multi == 0 || t.Example.Length == 0) t.Example = $"{where} ({anim}, {rows} keys)";
                    animated[c] = (t.Count + 1, t.Multi + (rows > 1 ? 1 : 0), t.Anims, t.Example);
                }
            }
        }
        Console.WriteLine($"models {modelCount} (text {ascii}, unreadable {failed}), with emitters {withEmitters}, emitters {emitterCount}");
        Console.WriteLine("emitters by resref prefix: " + string.Join(" ", byPrefix.OrderByDescending(p => p.Value).Select(p => $"{p.Key}:{p.Value}")));
        Console.WriteLine("\n## header fields (value: count, example)");
        foreach (var (f, vals) in fieldValues.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            var top = vals.OrderByDescending(v => v.Value.Count).ToList();
            Console.WriteLine($"{f} ({top.Count} distinct): " + string.Join("; ", top.Take(14).Select(v => $"{v.Key}: {v.Value.Count} ({v.Value.Example})")) + (top.Count > 14 ? "; ..." : ""));
        }
        Console.WriteLine("\n## geometry controllers (emitters using it, keyed with >1 row, min..max per column, example)");
        foreach (var (c, t) in ctrl.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase))
            Console.WriteLine($"{c}: {t.Count}, keyed {t.Keyed}, [{string.Join(", ", t.Min.Zip(t.Max, (a, b) => $"{a:0.###}..{b:0.###}"))}] ({t.Example})");
        Console.WriteLine("\n## controllers keyed by animations (emitter-animation pairs, animations, example)");
        foreach (var (c, t) in animated.OrderByDescending(a => a.Value.Count))
            Console.WriteLine($"{c}: {t.Count} [{string.Join(",", t.Anims.Take(12))}{(t.Anims.Count > 12 ? ",..." : "")}] ({t.Example})");
        break;
    }
    case "part-textures":
    {
        // Part models whose mesh bitmap isn't the model's own name: part-textures <prefix, e.g. pmh0_>
        var src = ResMan.ForClient(paths, Array.Empty<string>(), "");
        int total = 0, differ = 0, ownPlt = 0;
        foreach (var key in src.All(ResType.Mdl).Where(k => k.ResRef.StartsWith(args[1], StringComparison.OrdinalIgnoreCase)).OrderBy(k => k.ResRef))
        {
            var m = models.Get(key.ResRef);
            var tex = m?.Parts.Where(p => p.Mesh is { Render: true }).Select(p => p.Mesh!.Texture).FirstOrDefault(t => t.Length > 0);
            if (tex == null) continue;
            total++;
            if (string.Equals(tex, key.ResRef, StringComparison.OrdinalIgnoreCase)) continue;
            differ++;
            bool own = textures.KindOf(key.ResRef) == TextureCache.Kind.Palette;
            if (own) ownPlt++;
            Console.WriteLine($"  {key.ResRef,-20} bitmap={tex,-20} own plt={(own ? "yes" : "no")}");
        }
        Console.WriteLine($"{total} part models, {differ} with another model's bitmap, {ownPlt} of those have a PLT of their own name");
        break;
    }
    case "anim-tracks":
    {
        // Which nodes an animation keys, with their key counts: anim-tracks <model> <animation>
        var m = models.Get(args[1]) ?? throw new FileNotFoundException(args[1]);
        if (!m.Animations.TryGetValue(args[2], out var a)) { Console.WriteLine($"{args[2]}: not in {m.Name}"); break; }
        Console.WriteLine($"{m.Name}/{a.Name} length={a.Length:0.###} transition={a.Transition:0.###} root={a.Root} events={string.Join(",", a.Events.Select(e => $"{e.Time:0.##}:{e.Name}"))}");
        static string R(System.Numerics.Quaternion q) => $"({q.X:0.##},{q.Y:0.##},{q.Z:0.##},{q.W:0.##})";
        foreach (var (node, t) in a.Tracks.OrderBy(t => t.Key))
            Console.WriteLine($"  {node,-20} pos={t.Positions.Length} rot={t.Rotations.Length}"
                + (t.Rotations.Length > 0 ? $" first={R(t.Rotations[0])} last={R(t.Rotations[^1])}" : "")
                + (t.Positions.Length > 0 ? " positions=" + string.Join(" ", t.PositionTimes.Zip(t.Positions,
                    (time, p) => $"{time:0.###}:({p.X:0.###},{p.Y:0.###},{p.Z:0.###})")) : ""));
        break;
    }
    case "model-scale":
    {
        // Authored scale animation tracks: model-scale [prefix]
        var src = ResMan.ForClient(paths, Array.Empty<string>(), "");
        string prefix = args.Length > 1 ? args[1] : "";
        foreach (var key in src.All(ResType.Mdl).Where(k => k.ResRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var data = src.Read(key);
            if (data == null) continue;
            var model = MdlBinaryReader.IsBinary(data) ? MdlBinaryReader.Read(data) : MdlAsciiReader.Read(data);
            foreach (var animation in model.Animations.Values)
                foreach (var (node, track) in animation.Tracks.Where(t => t.Value.Scales.Length > 0))
                    Console.WriteLine($"{key.ResRef}/{animation.Name}/{node} keys=" + string.Join(" ",
                        track.ScaleTimes.Zip(track.Scales, (time, scale) => $"{time:0.###}:{scale:0.######}")));
        }
        break;
    }
    case "mesh-illumination-survey":
    {
        // Installed model metadata only; this inventory is not visual verification.
        var src = ResMan.ForClient(paths, Array.Empty<string>(), "");
        string prefix = args.Length > 1 ? args[1] : "";
        int visited = 0, illuminated = 0, animated = 0, failures = 0;
        foreach (var key in src.All(ResType.Mdl).Where(k => k.ResRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var bytes = src.Read(key);
                if (bytes == null) continue;
                bool binary = MdlBinaryReader.IsBinary(bytes);
                var model = binary ? MdlBinaryReader.Read(bytes) : MdlAsciiReader.Read(bytes);
                visited++;
                bool keys = binary ? MdlBinaryReader.SurveyAnimatedControllers(bytes).Any(c => c.Kind == "mesh" && c.Controller == "selfillumcolor")
                    : System.Text.Encoding.ASCII.GetString(bytes).Contains("selfillumcolorkey", StringComparison.OrdinalIgnoreCase);
                if (keys) { animated++; Console.WriteLine($"ANIMATED {key.ResRef}: selfillumcolor keys require separate playback verification"); }
                foreach (var part in model.Parts.Where(p => p.Mesh is { } mesh && mesh.SelfIllumination != System.Numerics.Vector3.Zero))
                {
                    illuminated++;
                    Console.WriteLine($"{key.ResRef}/{part.Name}: texture={part.Mesh!.Texture} selfillum={part.Mesh.SelfIllumination}");
                }
            }
            catch (Exception e) { failures++; Console.WriteLine($"ERROR {key.ResRef}: {e.GetType().Name}: {e.Message}"); }
        }
        Console.WriteLine($"models={visited} self-illuminated-meshes={illuminated} animated-models={animated} failures={failures}");
        break;
    }
    case "anim-controllers":
    {
        // Which controllers animations key, by node kind, across the base game's compiled models: anim-controllers [prefix]
        var src = ResMan.ForClient(paths, Array.Empty<string>(), "");
        string prefix = args.Length > 1 ? args[1] : "";
        var tally = new Dictionary<(string, string), (int Count, int Multi, string Example)>();
        foreach (var key in src.All(ResType.Mdl).Where(k => k.ResRef.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            var buf = src.Read(key);
            if (buf == null || !MdlBinaryReader.IsBinary(buf)) continue;
            foreach (var (kind, c, rows) in MdlBinaryReader.SurveyAnimatedControllers(buf))
            {
                (int Count, int Multi, string Example) t = tally.GetValueOrDefault((kind, c), (0, 0, key.ResRef));
                tally[(kind, c)] = (t.Count + 1, t.Multi + (rows > 1 ? 1 : 0), t.Example);
            }
        }
        foreach (var ((kind, c), t) in tally.OrderBy(t => t.Key))
            Console.WriteLine($"{kind,-8} {c,-16} {t.Count,7} multi-key {t.Multi,7}  e.g. {t.Example}");
        break;
    }
    case "emitter-raw":
    {
        // Every header field, controller and animation key of each emitter in the named models.
        foreach (var name in args.Skip(1))
        {
            var buf = rm.Read(name, ResType.Mdl);
            if (buf == null) { Console.WriteLine($"{name}: missing"); continue; }
            foreach (var e in MdlBinaryReader.IsBinary(buf) ? MdlBinaryReader.SurveyEmitters(buf) : MdlAsciiReader.SurveyEmitters(buf))
            {
                Console.WriteLine($"{name}/{e.Node}: " + string.Join(" ", e.Fields.Where(f => !f.Key.StartsWith("flag") || f.Key == "flags").Select(f => $"{f.Key}={f.Value}")));
                Console.WriteLine("    " + string.Join(" ", e.Controllers.Select(c => $"{c.Key}={string.Join(",", c.Value.Values.Select(v => v.ToString("0.###")))}{(c.Value.Rows > 1 ? $"[{c.Value.Rows}k]" : "")}")));
                foreach (var g in e.Animated.GroupBy(a => a.Animation))
                    Console.WriteLine($"    anim {g.Key}: " + string.Join(" ", g.Select(a => $"{a.Controller}[" + string.Join(" ", a.Times.Zip(a.Values, (t, v) => $"{t:0.##}:{string.Join(",", v.Select(x => x.ToString("0.##")))}")) + "]")));
            }
        }
        break;
    }
    case "emitters":
    {
        foreach (var name in args.Skip(1))
        {
            var m = models.Get(name);
            if (m == null) { Console.WriteLine($"{name}: missing"); continue; }
            foreach (var p in m.Parts.Where(p => p.Emitter != null))
            {
                var e = p.Emitter!;
                var t = textures.Get(e.Texture);
                Console.WriteLine($"{name}/{p.Name}: update={e.Update} render={e.Render} blend={e.Blend} tex={e.Texture}({t?.Format},alpha={t?.HasAlpha}) rate={e.BirthRate} life={e.LifeExpectancy} vel={e.Velocity} size={e.SizeStart}->{e.SizeEnd} color={e.ColorStart}->{e.ColorEnd} alpha={e.AlphaStart}->{e.AlphaEnd} grid={e.GridX}x{e.GridY} xsize={e.XSize} spread={e.Spread} mass={e.Mass}");
            }
        }
        break;
    }
    case "area-emitters":
    {
        var git = rm.ReadGff(args[1], ResType.Git)!;
        var names = git.GetList("Placeable List").Select(p => rm.TwoDA("placeables").Get((int)p.GetInt("Appearance"), "ModelName")).Distinct();
        foreach (var n in names)
        {
            var m = models.Get(n);
            if (m != null && m.Parts.Any(p => p.Emitter != null)) Console.WriteLine(n);
        }
        break;
    }
    case "tile":
    {
        var are = rm.ReadGff(args[1], ResType.Are)!;
        var set = Ini.Parse(rm.Read(are.GetString("Tileset"), ResType.Set)!);
        int w = (int)are.GetInt("Width");
        int tx = (int)(float.Parse(args[2]) / 10), ty = (int)(float.Parse(args[3]) / 10);
        var t = are.GetList("Tile_List")[ty * w + tx];
        var model = set.Get($"TILE{t.GetInt("Tile_ID")}", "Model");
        Console.WriteLine($"tile ({tx},{ty}) id={t.GetInt("Tile_ID")} model={model} orient={t.GetInt("Tile_Orientation")} height={t.GetInt("Tile_Height")}");
        args = new[] { "model", model };
        goto case "model";
    }
    case "near":
    {
        var git = rm.ReadGff(args[1], ResType.Git)!;
        float x = float.Parse(args[2]), y = float.Parse(args[3]), r = float.Parse(args[4]);
        foreach (var (list, xf, yf, kind) in new[] { ("Placeable List", "X", "Y", "plc"), ("Door List", "X", "Y", "door") })
            foreach (var o in git.GetList(list))
            {
                float ox = o.GetFloat(xf), oy = o.GetFloat(yf);
                if ((ox - x) * (ox - x) + (oy - y) * (oy - y) > r * r) continue;
                string model = kind == "plc" ? rm.TwoDA("placeables").Get((int)o.GetInt("Appearance"), "ModelName")
                    : o.GetInt("Appearance") > 0 ? rm.TwoDA("doortypes").Get((int)o.GetInt("Appearance"), "Model") : rm.TwoDA("genericdoors").Get((int)o.GetInt("GenericType"), "ModelName");
                var m = models.Get(model);
                var texs = m?.Parts.Where(p => p.Mesh is { Render: true, IsWalkmesh: false }).Select(p => p.Mesh!.Texture).Distinct() ?? Enumerable.Empty<string>();
                Console.WriteLine($"{kind} {o.GetString("Tag"),-22} ({ox:0.0},{oy:0.0}) model={model} [{string.Join(", ", texs.Select(t => $"{t}:{textures.KindOf(t)}"))}]");
            }
        break;
    }
    case "grep-nss":
    {
        // grep-nss <regex> [--base]: the scripts whose source matches, with each matching line (which stock
        // scripts call an engine function, say). --base reads the game's own scripts only.
        var src = args.Contains("--base") ? ResMan.ForClient(paths, Array.Empty<string>(), "") : rm;
        var re = new System.Text.RegularExpressions.Regex(args[1]);
        foreach (var k in src.All(ResType.Nss).OrderBy(k => k.ResRef))
        {
            if (src.Read(k.ResRef, ResType.Nss) is not { } bytes) continue;
            foreach (var line in OpenWinter.Aurora.Text.Windows1252.GetString(bytes).Split((char)10))
                if (re.IsMatch(line)) Console.WriteLine($"{k.ResRef}: {line.Trim()}");
        }
        break;
    }
    case "gff-survey":
    {
        // gff-survey <ext> <field> [field ...] [--base] [--module-only]: how often each value of a field occurs across
        // every resource of a type (a missing field counts as "absent"). "List.Field" looks inside each struct of a
        // list ("Equip_ItemList.Dropable"). --module-only skips the base game's own copies.
        bool moduleOnly = args.Contains("--module-only");
        var src = args.Contains("--base") ? ResMan.ForClient(paths, Array.Empty<string>(), "") : rm;
        args = args.Where(a => a is not ("--base" or "--module-only")).ToArray();
        ushort type = ResType.FromExtension(args[1]);
        var counts = args.Skip(2).ToDictionary(f => f, _ => new SortedDictionary<string, int>());
        int files = 0;
        foreach (var k in src.All(type))
        {
            if (moduleOnly && src.Sources.FirstOrDefault(x => x.Contains(k))?.Description.Contains(Path.GetFileName(modulePath), StringComparison.OrdinalIgnoreCase) != true) continue;
            if (src.Read(k.ResRef, type) is not { } bytes) continue;
            GffStruct g;
            try { g = Gff.Read(bytes); } catch { continue; }
            files++;
            foreach (var (field, tally) in counts)
            {
                var parts = field.Split('.');
                IEnumerable<GffStruct> holders = [g];
                foreach (var list in parts[..^1]) holders = holders.SelectMany(h => h.GetList(list));
                foreach (var h in holders)
                {
                    string v = h.Has(parts[^1]) ? h[parts[^1]]?.ToString() ?? "" : "absent";
                    tally[v] = tally.GetValueOrDefault(v) + 1;
                }
            }
        }
        Console.WriteLine($"{files} {args[1]} resources");
        foreach (var (field, tally) in counts)
            Console.WriteLine($"{field}: " + string.Join(", ", tally.OrderByDescending(x => x.Value).Take(20).Select(x => $"{x.Key}={x.Value}")));
        break;
    }
    case "list":
    {
        // list <prefix> [ext] [--base]: --base lists the game's own resources (no module or haks).
        var src = args.Contains("--base") ? ResMan.ForClient(paths, Array.Empty<string>(), "") : rm;
        args = args.Where(a => a != "--base").ToArray();
        ushort type = ResType.FromExtension(args.Length > 2 ? args[2] : "mdl");
        foreach (var k in src.All(type).Where(k => k.ResRef.StartsWith(args[1], StringComparison.OrdinalIgnoreCase)).OrderBy(k => k.ResRef).Take(50000))
            Console.Write(k.ResRef + " ");
        Console.WriteLine();
        break;
    }
    case "tlk":
    {
        // Prints talk-table strings: tlk <strref> [strref ...]
        foreach (var arg in args.Skip(1))
            if (uint.TryParse(arg, out var strref)) Console.WriteLine($"{strref}: {rm.TlkString(strref)}");
        break;
    }
    case "tlk-find":
    {
        // Finds talk-table strings containing a text (case-insensitive): tlk-find <text> [maxStrref]
        uint max = args.Length > 2 ? uint.Parse(args[2]) : 200000;
        for (uint s = 0; s < max; s++)
            if (rm.TlkString(s) is { Length: > 0 } text && text.Contains(args[1], StringComparison.OrdinalIgnoreCase))
                Console.WriteLine($"{s}: {(text.Length > 160 ? text[..160] + "..." : text).Replace("\n", "\\n")}");
        break;
    }
    case "2da":
    {
        // Prints a 2DA as the resource manager resolves it (haks first): 2da <name> [maxRows] [--base]
        // --base reads the game's own copy (no module or haks).
        var t = (args.Contains("--base") ? ResMan.ForClient(paths, Array.Empty<string>(), "") : rm).TwoDA(args[1]);
        args = args.Where(a => a != "--base").ToArray();
        int max = args.Length > 2 ? int.Parse(args[2]) : 60;
        Console.WriteLine("row	" + string.Join("	", t.Columns));
        for (int row = 0; row < Math.Min(max, t.RowCount); row++)
            Console.WriteLine(row + "	" + string.Join("	", t.Columns.Select(c => t.Get(row, c) is { Length: > 0 } v ? v : "****")));
        break;
    }
    case "which":
    {
        var key = ResKey.Of(args[1], ResType.FromExtension(args[2]));
        foreach (var src in rm.Sources.Where(x => x.Contains(key))) Console.WriteLine(src.Description);
        if (args.Length > 3)
        {
            var data = rm.Read(key)!;
            File.WriteAllBytes(args[3], data);
            Console.WriteLine($"wrote {data.Length} bytes to {args[3]}");
        }
        break;
    }
    case "gff":
    {
        var g = Gff.Read(File.Exists(args[1]) ? File.ReadAllBytes(args[1]) : rm.Read(Path.GetFileNameWithoutExtension(args[1]), ResType.FromExtension(Path.GetExtension(args[1])))!);
        void Dump(GffStruct st, string indent, int depth)
        {
            foreach (var f in st.Fields)
            {
                switch (f.Value)
                {
                    case GffStruct child: Console.WriteLine($"{indent}{f.Label} {{struct {child.StructType}}}"); if (depth < GffDepth()) Dump(child, indent + "  ", depth + 1); break;
                    case List<GffStruct> list:
                        Console.WriteLine($"{indent}{f.Label} [list {list.Count}]");
                        if (depth < GffDepth()) foreach (var (item, i) in list.Select((x, i) => (x, i)).Take(args.Length > 2 ? int.Parse(args[2]) : 3)) { Console.WriteLine($"{indent}  #{i} struct {item.StructType}"); Dump(item, indent + "    ", depth + 1); }
                        break;
                    case LocString l: Console.WriteLine($"{indent}{f.Label} = loc({l.StrRef}) \"{l.FirstString}\""); break;
                    case byte[] b: Console.WriteLine($"{indent}{f.Label} = void[{b.Length}]"); break;
                    default: Console.WriteLine($"{indent}{f.Label} = {f.Value}"); break;
                }
            }
        }
        Dump(g, "", 0);
        int GffDepth() => Array.IndexOf(args, "--depth") is var d and >= 0 ? int.Parse(args[d + 1]) : 3;
        break;
    }
    case "containers":
    {
        // Areas with lootable placeables (inventory, useable, unlocked): containers [maxPerArea]
        foreach (var k in rm.All(ResType.FromExtension("git")).OrderBy(k => k.ResRef))
        {
            if (rm.Read(k.ResRef, ResType.FromExtension("git")) is not { } data) continue;
            var found = Gff.Read(data).GetList("Placeable List")
                .Where(p => p.GetInt("HasInventory") != 0 && p.GetInt("Useable") != 0 && p.GetInt("Locked") == 0)
                .Select(p => $"{p.GetString("Tag")}({p.GetList("ItemList").Count})").ToList();
            if (found.Count > 0) Console.WriteLine($"{k.ResRef}: {string.Join(" ", found.Take(args.Length > 1 ? int.Parse(args[1]) : 6))}");
        }
        break;
    }
    case "inventory-grid":
    {
        // Inventory grid survey: where saved characters (.bic under a folder, default the vaults) keep their
        // items (Repos_PosX/Repos_Posy) against each item's footprint (baseitems InvSlotWidth/Height).
        //   inventory-grid [folder]
        var bi = rm.TwoDA("baseitems");
        var roots = args.Length > 1 && !args[1].StartsWith("--") ? new[] { args[1] } : new[] { Path.Combine(cfg.UserRoot, "localvault"), Path.Combine(cfg.UserRoot, "servervault") };
        int maxX = 0, maxY = 0, maxRight = 0, maxBottom = 0, files = 0, overlaps = 0;
        var rows = new SortedDictionary<int, int>();
        int[] pageHeights = [4, 5, 6, 7, 8, 9, 12];
        var crossings = new SortedDictionary<int, int>();
        void Scan(List<GffStruct> list, string where, string file)
        {
            var cells = new HashSet<(int, int)>();
            foreach (var e in list)
            {
                if (!e.Has("Repos_PosX")) continue;
                int x = (int)e.GetInt("Repos_PosX"), y = (int)e.GetInt("Repos_Posy"), b = (int)e.GetInt("BaseItem");
                int w = Math.Max(1, bi.GetInt(b, "InvSlotWidth", 1)), h = Math.Max(1, bi.GetInt(b, "InvSlotHeight", 1));
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                maxRight = Math.Max(maxRight, x + w); maxBottom = Math.Max(maxBottom, y + h);
                rows[y] = rows.GetValueOrDefault(y) + 1;
                foreach (int ph in pageHeights) if (y % ph + h > ph) crossings[ph] = crossings.GetValueOrDefault(ph) + 1;
                bool clash = false;
                for (int i = 0; i < w; i++) for (int j = 0; j < h; j++) if (!cells.Add((x + i, y + j))) clash = true;
                if (clash) { overlaps++; if (overlaps <= 10) Console.WriteLine($"overlap {file} {where}: base {b} at {x},{y} ({w}x{h})"); }
            }
        }
        if (args.Contains("--blueprints"))
        {
            // The module's own blueprints and placed objects: creatures, placeables, stores (per category), bags.
            foreach (var ext in new[] { "utc", "utp", "utm", "uti", "git", "git-placeables", "git-stores" })
            {
                maxX = maxY = maxRight = maxBottom = overlaps = 0;
                rows.Clear(); crossings.Clear();
                int n = 0;
                string fileExt = ext.StartsWith("git") ? "git" : ext;
                foreach (var k in rm.All(ResType.FromExtension(fileExt)))
                {
                    if (rm.Read(k.ResRef, ResType.FromExtension(fileExt)) is not { } data) continue;
                    var g = Gff.Read(data);
                    IEnumerable<GffStruct> holders = ext switch
                    {
                        "git" => g.GetList("Creature List"),
                        "git-placeables" => g.GetList("Placeable List"),
                        "git-stores" => g.GetList("StoreList"),
                        _ => [g],
                    };
                    foreach (var o in holders)
                    {
                        if (ext is "utm" or "git-stores")
                            foreach (var cat in o.GetList("StoreList")) { n++; Scan(cat.GetList("ItemList"), "store", k.ResRef); }
                        else if (o.GetList("ItemList") is { Count: > 0 } list) { n++; Scan(list, ext, k.ResRef); }
                    }
                }
                Console.WriteLine($"{ext}: {n} inventories: max PosX {maxX}, max PosY {maxY}, right edge {maxRight}, bottom edge {maxBottom}, overlaps {overlaps}");
                Console.WriteLine("  crossing by page height: " + string.Join(" ", pageHeights.Select(ph => $"{ph}:{crossings.GetValueOrDefault(ph)}")));
            }
            break;
        }
        foreach (var root in roots.Where(Directory.Exists))
        foreach (var f in Directory.GetFiles(root, "*.bic", SearchOption.AllDirectories))
        {
            files++;
            var g = Gff.Read(File.ReadAllBytes(f));
            if (args.Contains("--bags"))
            {
                // Only what is inside container items (bags, boxes).
                foreach (var bag in g.GetList("ItemList").Where(e => e.GetList("ItemList").Count > 0)) Scan(bag.GetList("ItemList"), "bag", Path.GetFileName(f));
            }
            else Scan(g.GetList("ItemList"), "inventory", Path.GetFileName(f));
        }
        Console.WriteLine($"{files} characters: max PosX {maxX}, max PosY {maxY}, right edge {maxRight}, bottom edge {maxBottom}, overlaps {overlaps}");
        Console.WriteLine("items per row: " + string.Join(" ", rows.Select(kv => $"{kv.Key}:{kv.Value}")));
        // An item never straddles two pages, so the page height is one no item crosses.
        Console.WriteLine("items crossing a page edge, by page height: " + string.Join(" ", pageHeights.Select(ph => $"{ph}:{crossings.GetValueOrDefault(ph)}")));
        break;
    }
    case "vault-scan":
    {
        foreach (var f in Directory.GetFiles(Path.Combine(cfg.UserRoot, "localvault"), "*.bic"))
        {
            var g = Gff.Read(File.ReadAllBytes(f));
            var slots = g.GetList("Equip_ItemList").Select(e => e.StructType).ToList();
            Console.WriteLine($"{Path.GetFileNameWithoutExtension(f),-24} app={g.GetInt("Appearance_Type"),-4} race={g.GetInt("Race")} gender={g.GetInt("Gender")} pheno={g.GetInt("Phenotype")} tail={g.GetInt("Tail_New")} wings={g.GetInt("Wings_New")} slots=[{string.Join(",", slots.Select(x => "0x" + x.ToString("x")))}]");
        }
        break;
    }
    case "run-module":
    {
        // Loads the module with scripts, logs a player in, simulates, and reports.
        float seconds = args.Length > 1 ? float.Parse(args[1]) : 30;
        var errors = new List<string>();
        var world = new OpenWinter.Game.World(rm);
        if (args.Contains("--profile")) world.Profile = new();
        world.EnableScripts();
        if (world.Profile != null) world.Scripts!.VM.Stats = new();
        // Real failures are tagged "[script error]"; everything else is the module's own logging.
        var logged = new List<string>();
        world.Scripts!.Log = line => { if (line.Contains("not implemented")) return; if (line.Contains("[script error]")) errors.Add(line); else logged.Add(line); };
        var sw = System.Diagnostics.Stopwatch.StartNew();
        world.LoadModule();
        Console.WriteLine($"module loaded with scripts in {sw.ElapsedMilliseconds} ms; implemented {world.Scripts.ImplementedCount}/{world.Scripts.Definitions.Functions.Count}");
        var server = new OpenWinter.Game.GameServer(world, new OpenWinter.Game.ServerOptions { DefaultCharacter = args.Length > 2 ? args[2] : null });
        var (client, serverEnd) = OpenWinter.Net.LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new OpenWinter.Net.HelloMessage { PlayerName = "Tester" }.Write());
        sw.Restart();
        // --kill <template resref>: kills every creature made from that blueprint once loaded; the report then lists the
        // bags left where they died (what a module's creatures drop on death).
        var killed = new List<(OpenWinter.Game.Objects.Creature C, System.Numerics.Vector3 At, OpenWinter.Game.Areas.Area? Area)>();
        if (Array.IndexOf(args, "--kill") is var ki and >= 0)
        {
            server.Tick(1f / OpenWinter.Game.GameServer.TickRate);
            foreach (var c in world.AllObjects.OfType<OpenWinter.Game.Objects.Creature>().Where(c => c.ResRef.Equals(args[ki + 1], StringComparison.OrdinalIgnoreCase) && c.Area != null).ToList())
            {
                killed.Add((c, c.Position, c.Area));
                OpenWinter.Game.Rules.HitPoints.Kill(world, c, OpenWinter.Game.Objects.GameObject.InvalidId);
            }
        }
        int ticks = (int)(seconds * OpenWinter.Game.GameServer.TickRate);
        for (int i = 0; i < ticks; i++) { server.Tick(1f / OpenWinter.Game.GameServer.TickRate); while (client.TryReceive(out _)) { } }
        Console.WriteLine($"simulated {seconds}s in {sw.ElapsedMilliseconds} ms; scripts run {world.Scripts.ScriptsRun}; creatures {world.AllObjects.OfType<OpenWinter.Game.Objects.Creature>().Count()}; pending commands {world.Scripts.PendingCommands}");
        // --watch <tag>: the state of every creature with that tag at the end (ever registered ones included).
        if (Array.IndexOf(args, "--watch") is var w and >= 0)
            foreach (var c in world.AllObjects.OfType<OpenWinter.Game.Objects.Creature>().Where(c => c.Tag.Equals(args[w + 1], StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine($"{c.Tag} {c.Area?.Tag} ({c.Position.X:0.0},{c.Position.Y:0.0}) dead={c.IsDead} hp={c.CurrentHitPoints} destroyable={c.IsDestroyable} actions={c.Actions.Count}");
        foreach (var (c, at, area) in killed)
        {
            var bags = area!.Objects.OfType<OpenWinter.Game.Objects.Placeable>().Where(p => p.Tag == OpenWinter.Game.Rules.CorpseSystem.BodyBagTag && System.Numerics.Vector3.Distance(p.Position, at) < 1.5f).ToList();
            Console.WriteLine($"killed {c.ResRef} {area.ResRef} ({at.X:0.0},{at.Y:0.0}) corpse={(c.IsDestroyed ? "gone" : "stays")} bags={bags.Count} {string.Join(" ", bags.SelectMany(b => world.Inventory.Items(b)).Select(i => i.ResRef))}");
        }
        Console.WriteLine($"--- module log lines: {logged.Count} ---");
        Console.WriteLine($"--- script errors ({errors.Count}) ---");
        foreach (var e in errors.GroupBy(x => x).OrderByDescending(g => g.Count()).Take(25)) Console.WriteLine($"{g(e.Count()),5} {e.Key}");
        Console.WriteLine($"--- unimplemented functions called ({world.Scripts.MissingCalls.Count}) ---");
        foreach (var kv in world.Scripts.MissingCalls.OrderByDescending(k => k.Value)) Console.WriteLine($"{kv.Value,6} {kv.Key}");
        if (world.Profile != null)
        {
            Console.WriteLine("--- world step time by phase/system (ms) ---");
            foreach (var (name, ms) in world.Profile.Where(k => !k.Key.StartsWith("fn ")).OrderByDescending(k => k.Value).Take(12)) Console.WriteLine($"{ms,9:0} {name}");
            Console.WriteLine("--- scripts: runs, instructions, ms (inclusive) ---");
            foreach (var (name, st) in world.Scripts.VM.Stats!.OrderByDescending(k => k.Value.Ms).Take(12))
                Console.WriteLine($"{st.Ms,9:0} {st.Runs,7} runs {st.Instructions,12:N0} ins  {name}");
            Console.WriteLine("--- engine functions (ms, nested calls counted in their callers too) ---");
            foreach (var (name, ms) in world.Profile.Where(k => k.Key.StartsWith("fn ")).OrderByDescending(k => k.Value).Take(20)) Console.WriteLine($"{ms,9:0} {name[3..]}");
        }
        static string g(int n) => n.ToString();
        break;
    }
    case "native-save-watch":
    {
        // native-save-watch <save folder> <tag> [seconds]: loads one of the original's save games (read only) and prints
        // the state of every creature with that tag, then again after simulating (no player connected).
        var folder = args[1];
        var archive = OpenWinter.Game.SaveGames.NativeSaveGame.Archive(folder) ?? throw new FileNotFoundException(folder);
        var current = new OpenWinter.Game.World(rm);
        current.EnableScripts();
        current.LoadModule();
        var world = OpenWinter.Game.SaveGames.NativeSaveGame.Load(current, folder, modulePath).World;
        void Show(string when)
        {
            foreach (var c in world.AllObjects.OfType<OpenWinter.Game.Objects.Creature>().Where(c => c.Tag.Equals(args[2], StringComparison.OrdinalIgnoreCase)))
                Console.WriteLine($"{when} {c.Tag} {c.Area?.Tag} ({c.Position.X:0.0},{c.Position.Y:0.0}) dead={c.IsDead} hp={c.CurrentHitPoints} destroyable={c.IsDestroyable} decay={c.DecayTime}");
        }
        Show("loaded");
        float secs = args.Length > 3 ? float.Parse(args[3]) : 10;
        for (int i = 0; i < secs * 20; i++) world.Step(0.05f);
        Show("after");
        break;
    }
    case "unimplemented":
    {
        var w = new OpenWinter.Game.World(rm);
        w.EnableScripts();
        File.WriteAllLines(args.Length > 1 ? args[1] : "unimplemented.txt", w.Scripts!.Unimplemented.Select(n =>
        {
            var f = w.Scripts.Definitions.ByName[n];
            return $"{f.Returns.ToString().ToLowerInvariant()} {n}({string.Join(", ", f.Params.Select(p => $"{p.Type.ToString().ToLowerInvariant()} {p.Name}{(p.Default != null ? "=" + p.Default : "")}"))})";
        }));
        Console.WriteLine($"{w.Scripts.Unimplemented.Count()} unimplemented");
        break;
    }
    case "bench-script":
    {
        // Times a script interpreted vs JIT-compiled: bench-script <ncs path> [runs]
        var path = args[1];
        int runs = args.Length > 2 ? int.Parse(args[2]) : 20;
        var bw = new OpenWinter.Game.World(rm);
        bw.EnableScripts();
        bw.Register(bw.Module);
        var program = new OpenWinter.Game.Scripting.NcsProgram(Path.GetFileNameWithoutExtension(path), File.ReadAllBytes(path));
        foreach (var (label, compile) in new[] { ("interpreter", false), ("jit", true) })
        {
            if (compile) program.Compiled = OpenWinter.Game.Scripting.NcsCompiler.Compile(program);
            else program.Compiled = null;
            bw.Scripts!.VM.JitThreshold = -1;
            bw.Scripts.VM.Run(program, bw.Module.Id); // warm up
            var t = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < runs; i++) bw.Scripts.VM.Run(program, bw.Module.Id);
            Console.WriteLine($"{label,-12} {t.Elapsed.TotalMilliseconds / runs,8:0.00} ms/run  result={bw.Module.Locals.Ints.GetValueOrDefault("bench")}");
        }
        break;
    }
    case "compile-all":
    {
        // JIT-compiles every NCS the module can see and reports failures.
        int ok = 0, failed = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var key in rm.All(ResType.Ncs))
        {
            try
            {
                var program = new OpenWinter.Game.Scripting.NcsProgram(key.ResRef, rm.Read(key)!);
                OpenWinter.Game.Scripting.NcsCompiler.Compile(program);
                ok++;
            }
            catch (Exception e) { failed++; if (failed <= 20) Console.WriteLine($"{key.ResRef}: {e.Message}"); }
        }
        Console.WriteLine($"compiled {ok}, failed {failed} in {sw.ElapsedMilliseconds} ms");
        break;
    }
    default:
        Console.WriteLine("commands: areas [filter] | area-tiles <area> | area-textures <area> | area-doors <area> | emitters <model...> | area-emitters <area> | model <resref> | model-bounds <resref> | texture <resref> | find-texture <model prefix> <texture> | merged-meshes <prefix>");
        break;
}
return 0;
