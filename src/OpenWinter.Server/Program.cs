using System.Diagnostics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Net;

// Dedicated server: openwinter-server [--config server.json] [--module path] [--port 5121] [--area resref] [--pvp none|party|full] [--dmpassword pw] ...
var opts = new Dictionary<string, string>();
for (int i = 0; i < args.Length; i++)
    if (args[i].StartsWith("--")) opts[args[i][2..]] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";

var cfg = LocalConfig.Find(Directory.GetCurrentDirectory()) ?? LocalConfig.Find()
    ?? LocalConfig.Detect(Path.Combine(AppContext.BaseDirectory, "userdata"))
    ?? throw new InvalidOperationException("Neverwinter Nights: Enhanced Edition was not found. Create config/local.json with its \"gameRoot\".");
var paths = new GamePaths(cfg.GameRoot, cfg.UserRoot, cfg.Language);
OpenWinter.Game.Persistence.PersistenceSystem.SandboxRoot = cfg.WriteRoot;
if (cfg.WriteRoot != null) Console.WriteLine($"[server] user data sandboxed in {cfg.WriteRoot} (set \"liveUserData\": true in config/local.json to use the real folder)");
var configPath = opts.GetValueOrDefault("config", "server.json");
var settings = ServerConfig.Load(configPath);
settings.Apply(opts);
if (!File.Exists(configPath) && !opts.ContainsKey("config"))
{
    File.WriteAllText(configPath, settings.ToJson());
    Console.WriteLine($"[server] wrote default settings to {Path.GetFullPath(configPath)}");
}
var module = settings.Module.Length > 0 ? settings.Module : cfg.Module;
int port = settings.Port;

var sw = Stopwatch.StartNew();
var world = new World(ResMan.ForModule(paths, module));
world.EnableScripts();
// --harness <dir>: the multiplayer sync harness records the server's state and relays the scene's markers (MultiplayerHarness.cs).
MultiplayerHarness? harness = null;
if (opts.TryGetValue("harness", out var harnessDir)) world.Scripts!.Log = line => { Console.WriteLine(line); harness?.OnLog(line); };
world.LoadModule();
// Game data older than the engine expects fails here rather than on the first tick.
try { _ = OpenWinter.Game.Scripting.EffectConstants.Of(world); }
catch (KeyNotFoundException e)
{
    Console.Error.WriteLine($"[server] {e.Message}");
    Environment.Exit(1);
}
world.Pvp = settings.PvpSetting;
world.GameDifficulty = settings.Difficulty;
world.ItemLevelRestrictions = settings.ItemLevelRestrictions;
world.OneParty = settings.OneParty;
world.PlayerPartyControl = settings.PlayerPartyControl;
// Like nwserver: players choose from their server vault unless the vault is "local".
var server = new GameServer(world, new ServerOptions
{
    StartArea = settings.StartArea,
    CharacterSelection = true,
    Vault = settings.Vault.Equals("local", StringComparison.OrdinalIgnoreCase) ? OpenWinter.Net.VaultMode.Local : OpenWinter.Net.VaultMode.Server,
    MaxPlayers = settings.MaxPlayers,
    ExportInterval = settings.AutoSaveMinutes * 60,
    // A DM password (settings file or --dmpassword) lets Dungeon Masters log in; without one, or with an empty one,
    // the server takes no DMs (an empty password would otherwise admit anyone as a DM).
    DMPassword = string.IsNullOrEmpty(settings.DMPassword) ? null : settings.DMPassword,
    ServeContent = settings.ServeContent,
    AllowPlayerPausing = settings.AllowPlayerPausing,
    ServerName = settings.Name,
});
if (harnessDir != null) harness = new MultiplayerHarness(world, server, harnessDir);
Console.WriteLine($"[server] {settings.Name}: PvP {settings.Pvp}{(settings.OneParty ? ", one party only" : "")}, difficulty {settings.Difficulty}, {settings.Vault} vault, max {settings.MaxPlayers} players");
Console.WriteLine($"[server] '{world.Module.Name.FirstString}': {world.Areas.Count} areas, {world.AllObjects.Count()} objects loaded in {sw.ElapsedMilliseconds} ms");

// Room for every player plus a few connections still logging in (GameServer drops those that never do).
using var net = new NetServerHost(port) { MaxConnections = settings.MaxPlayers > 0 ? settings.MaxPlayers + 8 : 0 };
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
    next += step;
    double wait = next - clock.Elapsed.TotalSeconds;
    if (wait > 0) Thread.Sleep(TimeSpan.FromSeconds(wait));
    else if (wait < -1) next = clock.Elapsed.TotalSeconds; // fell behind; don't spiral
}
harness?.Dispose();
Console.WriteLine("[server] stopped");
