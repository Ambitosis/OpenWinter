using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-play dir [--nui-click WINDOW:ELEMENT,...]
    /// [--nwn-replies 1,2,esc,...] [--play-seconds N]: on an NWN:EE server, follows whatever the server starts without
    /// moving the character: clicks each NUI element once its window is open, answers each conversation line (1 unless
    /// a reply is given) and takes a shot of it, and takes a shot every ten seconds, for N seconds (default 120).
    /// </summary>
    async Task CaptureNwnPlay(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && (_session.Area == null || _session.Controlled == null); i++) await Wait(0.1);
        DialogMessage? step = null;
        int steps = 0, answered = 0, shot = 0;
        _session.DialogReceived += m => { step = m; steps++; };
        var clicks = new Queue<string>(_args.GetValueOrDefault("nui-click", "").Split(',', StringSplitOptions.RemoveEmptyEntries));
        var picks = new Queue<string>(_args.GetValueOrDefault("nwn-replies", "").Split(',', StringSplitOptions.RemoveEmptyEntries));
        double seconds = double.Parse(_args.GetValueOrDefault("play-seconds", "120")), next = 10;
        var start = Time.GetTicksMsec();
        double Now() => (Time.GetTicksMsec() - start) / 1000.0;
        while (Now() < seconds)
        {
            if (clicks.Count > 0 && clicks.Peek().Split(':') is [var window, var element] && Nui.Find(window) != null)
            {
                await Wait(1.5);
                await Shot(dir, $"{++shot:00}_nui_{element.ToLowerInvariant()}");
                GD.Print($"[nwn-play] click {window}:{element}: {(Nui.Click(window, element, -1) ? "ok" : "not found")}");
                clicks.Dequeue();
                await Wait(1.5);
            }
            if (steps > answered && step is { Ended: false } m)
            {
                answered = steps;
                GD.Print($"[nwn-play] {m.SpeakerName}: {m.Text} | {string.Join(" | ", m.Replies.Select(r => r.Text))}");
                await Wait(1.5);
                await Shot(dir, $"{++shot:00}_line");
                var pick = picks.Count > 0 ? picks.Dequeue() : "1";
                await PressKey(pick == "esc" ? Key.Escape : Key.Key0 + int.Parse(pick));
                await Wait(0.5);
            }
            if (Now() >= next)
            {
                next += 10;
                await Shot(dir, $"{++shot:00}_at_{(int)Now()}s");
            }
            await Wait(0.2);
        }
        GetTree().Quit();
    }
}
