using Godot;
using OpenWinter.Net;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-combat dir: on an NWN:EE server running the
    /// bridgecombat scene (parity/visual/scenes/bridgecombat), follows the fight its chat markers ("BC goblins" ...)
    /// announce: clicks the first hostile goblin as a player would (the bridge turns that into an attack) and selects
    /// it, takes shots mid-fight, with the lasting effects, during the caster's missiles, while dying, at the death
    /// panel, and after pressing respawn.
    /// </summary>
    async Task CaptureNwnCombat(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var marks = new List<string>();
        _session.ChatReceived += chat => { if (chat.Text.StartsWith("BC ", StringComparison.Ordinal)) lock (marks) marks.Add(chat.Text[3..]); };
        async Task<bool> Mark(string step, double seconds)
        {
            for (int i = 0; i < seconds * 10; i++)
            {
                lock (marks) if (marks.Any(m => m.StartsWith(step, StringComparison.Ordinal))) return true;
                await Wait(0.1);
            }
            GD.PrintErr($"[nwn-combat] no \"BC {step}\" after {seconds} s");
            return false;
        }
        for (int i = 0; i < 600 && (_session.Area == null || _session.Controlled == null); i++) await Wait(0.1);
        if (_session.Area == null || _session.Controlled == null)
        {
            GD.PrintErr("[nwn-combat] no area or controlled character after 60 s");
            GetTree().Quit(1);
            return;
        }
        await Wait(1);
        await Shot(dir, "01_arrived");
        if (await Mark("goblins", 30))
        {
            await Wait(0.6);
            var here = _session.Controlled.NetPosition;
            if (_session.Objects.Values.Where(o => o.Kind == ObjectKind.Creature && o.ObjectId != _session.ControlledId && o.DisplayName.Contains("Goblin"))
                .OrderBy(o => o.NetPosition.DistanceTo(here)).FirstOrDefault() is { } goblin)
            {
                GD.Print($"[nwn-combat] clicking {goblin.DisplayName} {goblin.ObjectId}");
                _input.HudState?.Select(goblin.ObjectId);
                _session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = goblin.ObjectId }.Write());
            }
            await Wait(1.6);
            await Shot(dir, "02_fight");
            await Wait(1.2);
            await Shot(dir, "03_fight_later");
        }
        if (await Mark("saves", 30))
        {
            await Wait(0.8);
            await Shot(dir, "04_effects_and_saves");
        }
        if (await Mark("caster", 20))
        {
            await Wait(5.8);
            await Shot(dir, "05_missiles");
        }
        if (await Mark("dying", 40))
        {
            await Wait(1.5);
            await Shot(dir, "06_dying");
        }
        if (await Mark("lethal", 30))
        {
            await Wait(3.5);
            await Shot(dir, "07_death_panel");
            GD.Print($"[nwn-combat] dead: {(_session.Controlled.Flags & ObjectFlags.Dead) != 0}");
            _session.Request("respawn");
            await Wait(2.5);
            await Shot(dir, "08_respawned");
            GD.Print($"[nwn-combat] dead after respawn: {(_session.Controlled.Flags & ObjectFlags.Dead) != 0}");
        }
        GetTree().Quit();
    }
}
