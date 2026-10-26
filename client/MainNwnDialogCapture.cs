using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-dialog dir [--talk-to NAME] [--nwn-replies 1,2,esc,...]:
    /// on an NWN:EE server, talks to the nearest creature (or the nearest whose name contains NAME), then answers each
    /// line with the next number key from the list (1 when it runs out, Esc for "esc") two seconds after it shows,
    /// with a shot of every line, until the conversation closes (at most 30 lines); then opens the journal for a shot.
    /// </summary>
    async Task CaptureNwnDialog(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && (_session.Area == null || _session.Controlled == null); i++) await Wait(0.1);
        if (_session.Controlled is not { } me)
        {
            GD.PrintErr("[nwn-dialog] no controlled character after 60 s");
            GetTree().Quit(1);
            return;
        }
        await Wait(2);
        var name = _args.GetValueOrDefault("talk-to", "");
        var npc = _session.Objects.Values
            .Where(o => o.Kind == ObjectKind.Creature && o != me && (name.Length == 0 || o.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(o => o.NetPosition.DistanceTo(me.NetPosition)).FirstOrDefault();
        if (npc == null)
        {
            GD.PrintErr($"[nwn-dialog] no creature to talk to ({name})");
            GetTree().Quit(1);
            return;
        }
        DialogMessage? step = null;
        int steps = 0;
        _session.DialogReceived += m => { step = m; steps++; };
        GD.Print($"[nwn-dialog] talking to {npc.ObjectId} {npc.DisplayName}");
        _session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = npc.ObjectId }.Write());
        var picks = new Queue<string>(_args.GetValueOrDefault("nwn-replies", "").Split(',', StringSplitOptions.RemoveEmptyEntries));
        for (int n = 1; n <= 30; n++)
        {
            int seen = steps;
            for (int i = 0; i < 300 && steps == seen; i++) await Wait(0.1);
            if (steps == seen || step is not { } m) { GD.PrintErr("[nwn-dialog] no conversation step"); break; }
            if (m.Ended) { GD.Print("[nwn-dialog] the conversation closed"); break; }
            GD.Print($"[nwn-dialog] {n}: {m.SpeakerName}: {m.Text} | {string.Join(" | ", m.Replies.Select(r => r.Text))}");
            await Wait(2);
            await Shot(dir, $"{n:00}_line");
            var pick = picks.Count > 0 ? picks.Dequeue() : "1";
            await PressKey(pick == "esc" ? Key.Escape : Key.Key0 + int.Parse(pick));
        }
        await Wait(1.5);
        await Shot(dir, "90_after");
        if (Settings.Bindings.Get(Preferences.ActionCatalog.Journal, 0) is { } bind && Preferences.InputBindings.ToEvent(bind) is InputEventKey key)
        {
            await PressKey(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode);
            await Wait(2);
            await Shot(dir, "91_journal");
        }
        GetTree().Quit();
    }
}
