using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Game.Requests;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// Save/Load from the client (single player only, <see cref="ServerOptions.SaveGames"/>):
/// "list-saves", "save-game" {name, folder?, screenshot? (base64 TGA)}, "load-game" {folder},
/// "quicksave", "quickload". Answers with "saves-list" and "save-result" notifies (docs/NOTIFY.md).
/// Saving and loading run after the current server tick, between world steps.
/// </summary>
public static class SaveGameRequests
{
    static GameServer? Server(World world) => world.Output is GameServer { Options.SaveGames: true } s && s.World == world ? s : null;

    [ClientRequest("list-saves")]
    static void ListSaves(Request r)
    {
        if (Server(r.World) == null) return;
        SendList(r.World, r.Player);
    }

    [ClientRequest("save-game")]
    static void SaveGame(Request r)
    {
        var name = r.Str("name");
        var folder = r.Str("folder");
        byte[]? screen = null;
        if (r.Str("screenshot") is { Length: > 0 } b64)
            try { screen = Convert.FromBase64String(b64); } catch (FormatException) { }
        RequestSave(r.World, r.Player, name, SaveSlotKind.Manual, screen, folder.Length > 0 ? folder : null);
    }

    [ClientRequest("quicksave")]
    static void QuickSave(Request r)
    {
        if (Nwnx.Plugins.NwnxTweaks.Of(r.World).DisableQuicksave && (r.Player.Player?.IsDM == true || r.Player.IsDMAvatar)) return;
        byte[]? screen = null;
        if (r.Str("screenshot") is { Length: > 0 } b64)
            try { screen = Convert.FromBase64String(b64); } catch (FormatException) { }
        RequestSave(r.World, r.Player, "", SaveSlotKind.Quick, screen, null);
    }

    /// <summary>"save-screenshot" {folder}: the save's screen.tga thumbnail, answered with a "save-screenshot" notify.</summary>
    [ClientRequest("save-screenshot")]
    static void SaveScreenshot(Request r)
    {
        if (Server(r.World) == null) return;
        var folder = r.Str("folder");
        var tga = SaveGameService.ReadScreen(SaveGameService.SavesDir(r.World), folder);
        r.World.Output?.Notify("save-screenshot", r.Player, new { folder, tga = tga == null ? "" : Convert.ToBase64String(tga) }, r.Player);
    }

    [ClientRequest("load-game")]
    static void LoadGame(Request r) => RequestLoad(r.World, r.Player, r.Str("folder"));

    [ClientRequest("quickload")]
    static void QuickLoad(Request r)
    {
        if (Server(r.World) == null) return;
        if (SaveGameService.QuickSaveFolder(r.World) is { } folder) RequestLoad(r.World, r.Player, folder);
        else Result(r.World, r.Player, "load", false, "There is no quick save yet.");
    }

    /// <summary>DoSinglePlayerAutoSave: autosaves after the current tick when save games are enabled.</summary>
    public static void RequestAutoSave(World world)
    {
        if (Server(world) is not { } server) return;
        var pc = AuroraSaveResources.Players(world).FirstOrDefault();
        server.Defer(() => SaveNow(server, world, pc, "", SaveSlotKind.Auto, null, null));
    }

    static void RequestSave(World world, Creature player, string name, SaveSlotKind kind, byte[]? screen, string? folder)
    {
        if (Server(world) is not { } server) return;
        // Like the original, the player cannot save in the middle of a conversation.
        if (world.System<Conversations.ConversationService>().IsInConversation(player))
        {
            Result(world, player, "save", false, "You cannot save during a conversation.");
            return;
        }
        server.Defer(() => SaveNow(server, world, player, name, kind, screen, folder));
    }

    static void SaveNow(GameServer server, World world, Creature? player, string name, SaveSlotKind kind, byte[]? screen, string? folder)
    {
        if (server.World != world) return; // a load replaced the world first
        try
        {
            var info = SaveGameService.Save(world, name, kind, screen, folder);
            if (player != null)
            {
                Result(world, player, "save", true, kind == SaveSlotKind.Auto ? "Game autosaved." : $"Game saved: {info.Name}");
                SendList(world, player);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException or NotSupportedException)
        {
            SaveGameService.Log($"[save] failed: {e.Message}");
            if (player != null) Result(world, player, "save", false, "Saving failed: " + e.Message);
        }
    }

    static void RequestLoad(World world, Creature player, string folder)
    {
        if (Server(world) is not { } server) return;
        server.Defer(() =>
        {
            if (server.World != world) return;
            try { LoadInto(server, folder); }
            catch (Exception e)
            {
                // A save that can't be loaded must never take the running game down; the whole exception goes to
                // the log so the fault can be found (the player sees its message).
                SaveGameService.Log($"[load] failed to load {folder}: {e}");
                Result(world, player, "load", false, "Loading failed: " + e.Message);
            }
        });
    }

    /// <summary>
    /// Loads a save and makes it the server's world: each connected player takes over the saved
    /// character with their player name (or the first one not taken) and is sent its area.
    /// </summary>
    public static LoadedGame LoadInto(GameServer server, string folder)
    {
        var old = server.World;
        var loaded = SaveGameService.Load(old, folder);
        // Script UI belongs to the replaced world: close it on the clients (NUI is not saved, as in NWN).
        var nui = old.System<NuiService>();
        foreach (var s in server.Sessions)
            if (s.Creature is { } oldPc)
                foreach (var window in nui.Windows(oldPc).ToList())
                    old.Output?.Notify("nui-close", oldPc, new { token = window.Token }, oldPc);
        var unclaimed = loaded.Restored.Players.ToList();
        server.SwapWorld(loaded.World, session =>
        {
            var pick = unclaimed.FirstOrDefault(p => p.Player.Equals(session.PlayerName, StringComparison.OrdinalIgnoreCase));
            if (pick.Creature == null && unclaimed.Count > 0) pick = unclaimed[0];
            if (pick.Creature == null) return null;
            unclaimed.Remove(pick);
            return pick.Creature;
        });
        old.System<PersistenceSystem>().Dispose();
        // Later saves record the module this game now runs.
        if (loaded.Info.ModulePath.Length > 0 && loaded.World.Resources != old.Resources) server.Options.ModulePath = loaded.Info.ModulePath;
        foreach (var s in server.Sessions)
        {
            if (s.Creature is not { } pc) continue;
            loaded.World.Inventory.KeepPlayerItemFlags(pc);
            loaded.World.System<JournalService>().SendAll(pc);
            // A conversation saved in progress (an autosave from a line's script) is shown again.
            loaded.World.System<Conversations.ConversationService>().Reshow(s.PlayerCreature ?? pc);
            Result(loaded.World, pc, "load", true, $"Loaded: {loaded.Info.Name}");
        }
        return loaded;
    }

    static void SendList(World world, Creature player) =>
        world.Output?.Notify("saves-list", player, new
        {
            saves = SaveGameService.List(world).Select(s => new
            {
                folder = s.Folder, name = s.Name, kind = s.Kind.ToString().ToLowerInvariant(),
                savedAt = s.SavedAt.ToString("yyyy-MM-dd HH:mm"), module = s.Module, area = s.AreaName, player = s.PlayerName, gameTime = s.GameTime,
                played = (long)s.PlayedSeconds, screenshot = s.HasScreen,
            }).ToList(),
        }, player);

    static void Result(World world, Creature player, string action, bool ok, string message) =>
        world.Output?.Notify("save-result", player, new { action, ok, message }, player);
}
