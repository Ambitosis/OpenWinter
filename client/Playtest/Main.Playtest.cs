using OpenWinter.Client.Playtest;
using OpenWinter.Client.UI;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// --playtest outdir [--module path] [--character vault-resref] [--minutes N] [--godmode]: the playtesting
/// autopilot plays the module and writes outdir/report.md, report.html and report.json (see client/Playtest).
/// </summary>
public partial class Main
{
    void StartPlaytest(Hud hud)
    {
        var dialog = FindChildren("*", nameof(Control), true, false).OfType<DialogPanel>().First(); // on its own layer since main 37ab648
        var loot = FindChildren("*", nameof(Control), true, false).OfType<ContainerPanel>().FirstOrDefault();
        var minutes = double.TryParse(_args.GetValueOrDefault("minutes"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var m) ? m : 20;
        AddChild(new Autopilot
        {
            Name = "Autopilot",
            Host = new PlaytestHost
            {
                Server = () => _server,
                Session = _session,
                Camera = _camera,
                Interaction = _interaction,
                Loading = () => _loading.Showing,
                Dialog = dialog,
                Loot = loot,
                Quit = () => GetTree().Quit(),
            },
            OutDir = System.IO.Path.GetFullPath(_args["playtest"] == "true" ? "out/playtest" : _args["playtest"]),
            Minutes = minutes,
            God = _args.ContainsKey("godmode"),
            ModulePath = _args.GetValueOrDefault("module", _cfg.Module),
            Character = _args.GetValueOrDefault("character", ""),
        });
    }
}
