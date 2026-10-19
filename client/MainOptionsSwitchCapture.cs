using System.Text.Json;
using OpenWinter.Client.Preferences;
using OpenWinter.Client.UI.FrontEnd;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    // --capture-menu-switch <dir> --options-gameplay: the Options window's Gameplay page (difficulty), in the current
    // interface style (add --ui classic for the classic one).
    async Task CaptureOptionsGameplay(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(3);
        _menu!.Open("options");
        FindOptions(_menu)!.Show(OptionsView.Page.Gameplay);
        await Wait(0.6);
        await Shot(dir, (Settings.Current.Interface.Classic ? "classic" : "modern") + "_gameplay");
        GetTree().Quit(0);
    }

    // --capture-menu-switch <dir> --options-open. Settings.Initialize locks all
    // persistence for capture runs; signals never move the OS pointer or focus.
    async Task CaptureOpenOptionsSwitch(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var readings = new List<object>();
        bool passed = true;
        void Check(string name, bool result)
        {
            readings.Add(new { name, passed = result });
            passed &= result;
            GD.Print($"[options-switch] {name}={result}");
        }
        try
        {
            await Wait(3);
            for (int direction = 0; direction < 2; direction++)
            {
                var menu = _menu!;
                bool classic = Settings.Current.Interface.Classic;
                string prefix = classic ? "classic_to_modern" : "modern_to_classic";
                menu.Open("options");
                var view = FindOptions(menu)!;
                view.Show(OptionsView.Page.Interface);
                await Wait(0.4);
                readings.Add(new { name = prefix + ".native_filter_count",
                    value = menu.FindChildren("*", nameof(OptionsView), true, false).Count });
                Button Next() => view.FindChildren("*", "Button", true, false).OfType<Button>()
                    .First(b => b.TooltipText == "Next");
                Next().EmitSignal(BaseButton.SignalName.Pressed);
                await Wait(0.6);
                bool kept = IsInstanceValid(menu) && _menu == menu && IsInstanceValid(view) && view.IsVisibleInTree();
                Check(prefix + ".preview_keeps_options", kept);
                await Shot(dir, prefix + "_preview");
                if (!kept) break;
                Check(prefix + ".preview_changed", Settings.Current.Interface.Classic != classic);
                view.RequestClose();
                await Wait(0.3);
                await Shot(dir, prefix + "_confirmation");
                var discard = GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>()
                    .First(b => b.Text == "Discard" && b.IsVisibleInTree());
                discard.EmitSignal(BaseButton.SignalName.Pressed);
                await Wait(0.8);
                Check(prefix + ".discard_keeps_menu", _menu == menu && Settings.Current.Interface.Classic == classic);

                menu.Open("options");
                view = FindOptions(menu)!;
                view.Show(OptionsView.Page.Interface);
                await Wait(0.3);
                Next().EmitSignal(BaseButton.SignalName.Pressed);
                view.Apply();
                await Wait(0.4);
                kept = IsInstanceValid(menu) && _menu == menu && IsInstanceValid(view) && view.IsVisibleInTree();
                Check(prefix + ".apply_keeps_options_until_closed", kept);
                if (!kept) break;
                view.RequestClose();
                await Wait(1.5);
                Check(prefix + ".close_rebuilds_menu", _menu != menu && Settings.Current.Interface.Classic != classic);
                await Shot(dir, prefix + "_closed");
            }
        }
        catch (Exception e)
        {
            Check("capture_completed", false);
            GD.PushError(e.ToString());
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "options-switch.json"),
            JsonSerializer.Serialize(new { passed, readings }, new JsonSerializerOptions { WriteIndented = true }));
        GetTree().Quit(passed ? 0 : 1);
    }
}
