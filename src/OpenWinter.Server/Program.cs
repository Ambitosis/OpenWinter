using System.Diagnostics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Net;

// Dedicated server. It takes nwserver's command line (-module name, -port, -maxclients, -servervault 1, -userdirectory
// dir, ...) and reads settings.tml and nwnplayer.ini from the user folder as nwserver does. OpenWinter's own options:
// [--config server.json] [--module path] [--area resref] [--harness dir], and the older --port, --pvp none|party|full ...
var opts = new Dictionary<string, string>();
for (int i = 0; i < args.Length; i++)
    if (args[i].StartsWith("--")) opts[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith('-') ? args[++i] : "true";

var cfg = LocalConfig.Find(Directory.GetCurrentDirectory()) ?? LocalConfig.Find()
    ?? LocalConfig.Detect(Path.Combine(AppContext.BaseDirectory, "userdata"))
    ?? throw new InvalidOperationException("Neverwinter Nights: Enhanced Edition was not found. Create config/local.json with its \"gameRoot\".");
// -userdirectory is where the server reads its settings and keeps its vault, databases and saves, as with nwserver.
// Without it the server reads the game's user folder and keeps what it writes in the sandbox unless live data is on.
var userDirectory = ServerSettings.UserDirectory(args) is { } dir ? Path.GetFullPath(dir) : null;
if (userDirectory != null) Directory.CreateDirectory(userDirectory);
var paths = new GamePaths(cfg.GameRoot, userDirectory ?? cfg.UserRoot, cfg.Language);
OpenWinter.Game.Persistence.PersistenceSystem.SandboxRoot = userDirectory != null ? null : cfg.WriteRoot;
if (userDirectory == null && cfg.WriteRoot != null) Console.WriteLine($"[server] user data sandboxed in {cfg.WriteRoot} (set \"liveUserData\": true in config/local.json to use the real folder)");

// nwserver's order: its defaults, nwnplayer.ini, settings.tml, then the command line. OpenWinter's server.json, when
// there is one, sits under the command line.
var settings = ServerSettings.Load(paths.UserRoot);
var configPath = opts.GetValueOrDefault("config", "server.json");
if (File.Exists(configPath)) settings.ApplyJson(File.ReadAllText(configPath));
else if (opts.ContainsKey("config")) Console.WriteLine($"[server] {configPath} not found");
settings.ApplyOptions(opts);
foreach (var arg in settings.ApplyFlags(args)) Console.WriteLine($"Server: {arg} not understood");

var moduleName = ServerSettings.ModuleName(args) ?? (settings.Module.Length > 0 ? settings.Module : cfg.Module);
var module = FindModule(paths, moduleName);
if (module == null)
{
    Console.Error.WriteLine($"[server] module \"{moduleName}\" not found (a .mod in {Path.Combine(paths.UserRoot, "modules")} or the game's data folders, or a path)");
    Environment.Exit(1);
}
int port = settings.Int("server.net.port");

var sw = Stopwatch.StartNew();
// A dedicated server: NWNX plugins may reach outside the game (programs, HTTP, RPC) where their own settings say so.
OpenWinter.Game.Nwnx.NwnxRegistry.DedicatedServer = true;
var world = new World(ResMan.ForModule(paths, module));
world.EnableScripts();
// --harness <dir>: the multiplayer sync harness records the server's state and relays the scene's markers (MultiplayerHarness.cs).
MultiplayerHarness? harness = null;
if (opts.TryGetValue("harness", out var harnessDir)) world.Scripts!.Log = line => { Console.WriteLine(line); harness?.OnLog(line); };
// Server settings before the module loads, so OnModuleLoad scripts (NWNX_Administration) can change them.
settings.ApplyTo(world);
world.LoadModule();
// Game data older than the engine expects fails here rather than on the first tick.
try { _ = OpenWinter.Game.Scripting.EffectConstants.Of(world); }
catch (KeyNotFoundException e)
{
    Console.Error.WriteLine($"[server] {e.Message}");
    Environment.Exit(1);
}
var options = settings.ToOptions();
options.ModulePath = module;
var server = new GameServer(world, options);
if (harnessDir != null) harness = new MultiplayerHarness(world, server, harnessDir);
Console.WriteLine($"[server] {options.ServerName}: PvP {world.Pvp}{(world.OneParty ? ", one party only" : "")}, difficulty {world.GameDifficulty}, "
    + $"{(options.Vault == VaultMode.Server ? "server" : "local")} vault, max {options.MaxPlayers} players");
Console.WriteLine($"[server] '{world.Module.Name.FirstString}': {world.Areas.Count} areas, {world.AllObjects.Count()} objects loaded in {sw.ElapsedMilliseconds} ms");

// Room for every player plus a few connections still logging in (GameServer drops those that never do).
using var net = new NetServerHost(port) { MaxConnections = options.MaxPlayers + 8 };
Console.WriteLine($"[server] OpenWinter {NetworkVersion.Current.Display}");
net.Connected += c =>
{
    server.Accept(c);
    Console.WriteLine($"[server] client {c.Id} connected");
};
Console.WriteLine($"[server] listening on UDP {net.Port}. Ctrl+C to stop.");

var running = true;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; running = false; };
float step = 1f / GameServer.TickRate;
var clock = Stopwatch.StartNew();
double next = 0;
while (running)
{
    net.Poll();
    server.Tick(step);
    if (harness != null) { harness.AfterTick(); if (harness.Finished) running = false; }
    if (server.ShutdownRequested) running = false; // NWNX_Administration_ShutdownServer
    next += step;
    double wait = next - clock.Elapsed.TotalSeconds;
    if (wait > 0) Thread.Sleep(TimeSpan.FromSeconds(wait));
    else if (wait < -1) next = clock.Elapsed.TotalSeconds; // fell behind; don't spiral
}
harness?.Dispose();
Console.WriteLine("[server] stopped");

// nwserver's -module takes a module's name, found in the user folder's modules and the game's own; OpenWinter also
// takes a path to a .mod file or module folder.
static string? FindModule(GamePaths paths, string name)
{
    if (name.Length == 0) return null;
    if (File.Exists(name) || Directory.Exists(name)) return Path.GetFullPath(name);
    foreach (var folder in new[] { Path.Combine(paths.UserRoot, "modules"), Path.Combine(paths.GameRoot, "data", "nwm"), Path.Combine(paths.GameRoot, "data", "mod") })
        foreach (var suffix in new[] { ".mod", ".nwm" })
            if (GamePaths.Find(folder, name + suffix) is { } path) return path;
    return null;
}
