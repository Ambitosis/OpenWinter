using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --capture-cutscene dir: plays a module's opening as a player would: answers the first conversation (the reply
/// containing --reply, default "Automatic", else the first one), then screenshots the cinematic that follows every
/// few seconds until cutscene mode ends and control returns (Hordes of the Underdark chapter 1's level-up and dream).
/// --line-shots also shoots every line of that first conversation.
/// </summary>
public partial class Main
{
    async Task CaptureCutscene(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        string want = _args.GetValueOrDefault("reply", "Automatic");
        float limit = float.TryParse(_args.GetValueOrDefault("seconds"), out var l) ? l : 240;
        DialogMessage? dialog = null;
        bool cutscene = false, sawCutscene = false;
        string fade = "none";
        double clock = 0;
        _session.DialogReceived += m => { dialog = m; GD.Print($"[cutscene-capture] {clock,6:0.0}s dialog ended={m.Ended} replies={m.Replies.Count}"); };
        _session.NotifyReceived += n =>
        {
            if (n.Kind == "fade") fade = n.Json;
            if (n.Kind == "cutscene") { cutscene = n.Json.Contains("\"enabled\":true"); sawCutscene |= cutscene; }
            if (n.Kind is "fade" or "cutscene") GD.Print($"[cutscene-capture] {clock,6:0.0}s {n.Kind} {n.Json}");
        };
        _session.AreaLoaded += () => GD.Print($"[cutscene-capture] {clock,6:0.0}s area built: {_session.Area?.Name}");
        async Task Tick(double s) { await Wait(s); clock += s; }

        for (int i = 0; i < 120 && dialog is not { Ended: false }; i++) await Tick(0.5);
        await Tick(1.5);
        await Shot(dir, "c01_dialog");
        if (_args.ContainsKey("line-shots")) { await Tick(0.8); await Shot(dir, "c01_dialog_b"); }
        int shot = 2;
        for (int guard = 0; guard < 12 && dialog is { Ended: false } d; guard++)
        {
            var pick = d.Replies.FirstOrDefault(r => r.Text.Contains(want), d.Replies[0]);
            if (pick.Text.Contains(want)) await Shot(dir, $"c{shot++:00}_choice");
            dialog = null;
            _session.Send(new DialogReplyMessage { Choice = pick.Index }.Write());
            await Tick(1.5);
            // --line-shots: every line of the conversation too (the speakers' facing and talk animations).
            if (_args.ContainsKey("line-shots") && dialog is { Ended: false }) await Shot(dir, $"c{shot++:00}_line");
        }
        await Tick(0.5);
        await Shot(dir, $"c{shot++:00}_after_choice");

        // The cinematic: a frame every few seconds until it hands control back.
        double next = clock + 4;
        while (clock < limit)
        {
            await Tick(0.5);
            if (clock >= next)
            {
                next = clock + 9;
                await Shot(dir, $"c{shot++:00}_t{(int)clock:000}{(cutscene ? "_cutscene" : "")}");
            }
            if (sawCutscene && !cutscene && dialog is { Ended: false }) break;
        }
        await Tick(2.5);
        await Shot(dir, $"c{shot++:00}_control_returned");
        GD.Print($"[cutscene-capture] done at {clock:0.0}s: cutscene seen={sawCutscene}, in cutscene={cutscene}, last fade {fade}, dialog open={dialog is { Ended: false }}");
        // Close the wake-up conversation and show the HUD back.
        while (dialog is { Ended: false } open)
        {
            dialog = null;
            _session.Send(new DialogReplyMessage { Choice = open.Replies.Count > 0 ? open.Replies[0].Index : -1 }.Write());
            await Tick(1.0);
        }
        await Tick(2.0);
        await Shot(dir, $"c{shot++:00}_playing");
        GetTree().Quit();
    }
}
