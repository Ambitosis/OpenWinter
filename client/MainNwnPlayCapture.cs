using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-play dir [--nui-click WINDOW:ELEMENT,...]
    /// [--nui-click-after S] [--nwn-replies 1,2,esc,...] [--play-levelup S,...] [--play-seconds N]: on an NWN:EE
    /// server, follows whatever the server starts without moving the character: clicks each NUI element once its
    /// window is open (from S seconds on), answers each conversation line (1 unless a reply is given) and takes a shot
    /// of it, at each --play-levelup second presses the character record's level-up button and takes the level with
    /// the recommended choices, and takes a shot every ten seconds, for N seconds (default 120).
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
        var levelUps = new Queue<double>(_args.GetValueOrDefault("play-levelup", "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(double.Parse));
        double seconds = double.Parse(_args.GetValueOrDefault("play-seconds", "120")), next = 10;
        double clickAfter = double.Parse(_args.GetValueOrDefault("nui-click-after", "0"));
        var start = Time.GetTicksMsec();
        double Now() => (Time.GetTicksMsec() - start) / 1000.0;
        while (Now() < seconds)
        {
            if (levelUps.Count > 0 && Now() >= levelUps.Peek())
            {
                levelUps.Dequeue();
                await PlayLevelUp(dir, $"{++shot:00}_levelup");
            }
            if (clicks.Count > 0 && Now() >= clickAfter && clicks.Peek().Split(':') is [var window, var element] && Nui.Find(window) != null)
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

    /// <summary>The character record's level-up button, then Recommended and OK on each step of the window it opens.</summary>
    async Task PlayLevelUp(string dir, string name)
    {
        var sheet = Find<UI.Classic.Sheets.ClassicCharacterSheet>();
        if (sheet is { Visible: false }) sheet.Toggle();
        await Wait(1.5);
        await Shot(dir, $"{name}_0_sheet");
        if (sheet?.LevelUpRequested is { } press) press();
        else Character.LevelUpRequested?.Invoke();
        await Wait(3);
        var l = UI.Classic.Creation.ClassicCreation.LevelUp;
        GD.Print($"[nwn-play] level-up window {(l is { Visible: true } ? "open" : "not open")}, input {(Gameplay.PlayerInput.Blocked ? "blocked" : "free")}");
        await Shot(dir, $"{name}_1_window");
        for (int step = 2; step <= 10 && l is { Visible: true }; step++)
        {
            if (l.CanPress("RecommendButton")) l.Press("RecommendButton");
            await Wait(0.8);
            if (!l.Press("OkButton")) break;
            await Wait(1.2);
            await Shot(dir, $"{name}_{step}_step");
        }
        await Wait(2);
        GD.Print($"[nwn-play] after the level-up: window {(l is { Visible: true } ? "open" : "closed")}, input {(Gameplay.PlayerInput.Blocked ? "blocked" : "free")}");
        await Shot(dir, $"{name}_9_after");
    }
}
