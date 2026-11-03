using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.DungeonMaster;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Net;

namespace OpenWinter.Game;

public sealed partial class GameServer
{
    ResMan? _ownedModuleResources;

    string? FindModule(string name)
    {
        // StartNewModule accepts a module name, never an arbitrary script-supplied path.
        if (string.IsNullOrWhiteSpace(name) || name.Contains('/') || name.Contains('\\')
            || name.Contains("..") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        var paths = World.Resources.Paths;
        var folders = new[] { Path.Combine(paths.UserRoot, "modules"),
            _options.ModulePath is { } current ? Path.GetDirectoryName(Path.GetFullPath(current)) : null,
            Path.Combine(paths.GameRoot, "data", "nwm"), Path.Combine(paths.GameRoot, "data", "mod") };
        foreach (var folder in folders.OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
            foreach (var suffix in Path.HasExtension(name) ? new[] { "" } : new[] { ".mod", ".nwm" })
            {
                if (GamePaths.Find(folder, name + suffix) is { } path) return path;
            }
        return null;
    }

    /// <summary>
    /// A new world for <paramref name="resources"/>' module, loaded, with the server's settings carried over from
    /// <paramref name="old"/> before its OnModuleLoad runs.
    /// </summary>
    static World NextWorld(World old, ResMan resources)
    {
        var next = new World(resources)
        {
            GameDifficulty = old.GameDifficulty, Pvp = old.Pvp, OneParty = old.OneParty,
            PlayerPartyControl = old.PlayerPartyControl, ItemLevelRestrictions = old.ItemLevelRestrictions,
        };
        var (from, to) = (old.Limits, next.Limits);
        (to.AbilityBonus, to.AbilityPenalty, to.AttackBonus, to.DamageBonus, to.SavingThrowBonus, to.SkillBonus) =
            (from.AbilityBonus, from.AbilityPenalty, from.AttackBonus, from.DamageBonus, from.SavingThrowBonus, from.SkillBonus);
        next.System<PersistenceSystem>().RootOverride = old.System<PersistenceSystem>().RootOverride;
        next.System<ServerAdministration>().TakeOver(old.System<ServerAdministration>());
        if (old.Scripts != null)
        {
            next.EnableScripts();
            next.Scripts!.Log = old.Scripts.Log;
        }
        next.LoadModule();
        return next;
    }

    bool _reloadWhenEmpty;

    /// <summary>
    /// The last player left a server with reload-when-empty on: the module starts again from its file, as nwserver
    /// does (the server log shows the module loading again as the last player leaves).
    /// </summary>
    void ReloadModule()
    {
        if (_options.ModulePath is not { } path || _sessions.Any(s => s.Creature != null)) return;
        var old = World;
        World? next = null;
        ResMan? resources = null;
        try
        {
            resources = ResMan.ForModule(old.Resources.Paths, path);
            next = NextWorld(old, resources);
            var content = _options.ServeContent ? new Distribution.ContentServer(next) : null;
            var previousResources = _ownedModuleResources;
            ResetWorld(next);
            _ownedModuleResources = resources;
            Content = content;
            _exportTimer = 0;
            old.System<PersistenceSystem>().Dispose();
            previousResources?.Dispose();
            Console.WriteLine($"[server] module reloaded: no players left");
        }
        catch (Exception e) when (World != next)
        {
            if (next != null && World != next) next.System<PersistenceSystem>().Dispose();
            resources?.Dispose();
            Console.WriteLine($"[server] reloading the module failed: {e.Message}");
        }
    }

    /// <summary>Apply StartNewModule between ticks, after preparing a complete replacement.</summary>
    void StartModule(string name)
    {
        var old = World;
        World? next = null;
        ResMan? resources = null;
        try
        {
            var path = FindModule(name) ?? throw new FileNotFoundException("Module not found.");
            var transfers = _sessions.Where(s => s.Creature != null).Select(s =>
            {
                var actor = s.IsDM ? old.System<DungeonMasterSystem>().Avatar(s) ?? s.Creature! : s.PlayerCreature!;
                var bic = ObjectSerializer.CreatureGff(old, actor, 0xFFFFFFFF, asCharacter: true);
                // Native cross-module transfer uses character persistence: inventory locals survive,
                // while the PC's module locals, effects and action queue do not.
                bic.SetList("VarTable", []);
                return (Session: s, Bic: bic, actor.VaultFile);
            }).ToList();
            if (transfers.Count == 0) throw new InvalidOperationException("No player characters to transfer.");
            resources = ResMan.ForModule(old.Resources.Paths, path);
            next = NextWorld(old, resources);
            if (next.FindArea(next.Module.EntryArea) == null)
                throw new InvalidDataException("Module has no valid entry area.");
            var characters = transfers.Select(t => (t.Session,
                Actor: next.Creatures.FromTemplate(t.Bic), t.VaultFile)).ToList();
            var content = _options.ServeContent ? new Distribution.ContentServer(next) : null;
            // Nothing above changes connections or the live world. A failed load leaves it playable.
            foreach (var s in _sessions)
            {
                if (s.Creature is not { } pc) continue;
                foreach (var window in old.System<Players.NuiService>().Windows(pc).ToList())
                    old.Output?.Notify("nui-close", pc, new { token = window.Token }, pc);
                s.Send(new DialogMessage { Ended = true }.Write());
                old.Output?.Notify("pause", pc, new { state = 0 }, pc);
                old.Output?.Notify("cutscene", pc, new { enabled = false, leftClick = false }, pc);
            }
            var previousResources = _ownedModuleResources;
            ResetWorld(next);
            _ownedModuleResources = resources;
            Content = content;
            _options.ModulePath = path;
            _options.StartArea = null;
            _exportTimer = 0;
            foreach (var s in _sessions)
                s.Send(new WelcomeMessage
                {
                    ModuleName = next.Module.Name.Resolve(next.Resources.Talk),
                    CustomTlk = next.Module.Ifo.GetString("Mod_CustomTlk"),
                    Haks = ResMan.ModuleHaks(next.Module.Ifo).ToList(),
                    Files = Content?.Files.ToList() ?? [],
                }.Write());
            foreach (var (session, actor, vaultFile) in characters)
            {
                actor.VaultFile = vaultFile;
                SetPlayerSpeeds(actor);
                EnterWorld(session, actor);
            }
            old.System<PersistenceSystem>().Dispose();
            previousResources?.Dispose();
        }
        // Any failure before the swap (a corrupt GFF can throw IndexOutOfRangeException and the like) must not
        // escape into Tick and stop the server; the current module keeps running.
        catch (Exception e) when (World != next)
        {
            if (next != null && World != next) next.System<PersistenceSystem>().Dispose();
            resources?.Dispose();
            Console.WriteLine($"[server] StartNewModule({name}) failed: {e.Message}");
        }
    }
}
