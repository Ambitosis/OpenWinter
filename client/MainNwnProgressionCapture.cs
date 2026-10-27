using Godot;
using OpenWinter.Client.UI;

namespace OpenWinter.Client;

public partial class Main
{
    T? Find<T>() where T : Node => GetTree().Root.FindChildren("*", "", true, false).OfType<T>().FirstOrDefault();

    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-levelup dir [--levelup-class N] [--levelups N]: on an
    /// NWN:EE server that has given the character the experience for a level (parity/suites/bridge_progression), opens
    /// the level-up window and takes the level in class N (default wizard) with its recommended choices, with a shot at
    /// each step, N times (default 1). With --ui classic the original's window walks through its steps the same way.
    /// </summary>
    async Task CaptureNwnLevelUp(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && _session.Controlled == null; i++) await Wait(0.1);
        GD.Print($"[nwn-levelup] controlling {_session.ControlledId}");
        // The test module gives the experience a few seconds after the player enters.
        await Wait(8);
        await Shot(dir, "00_ready");
        int cls = int.TryParse(_args.GetValueOrDefault("levelup-class"), out var c) ? c : 10;
        int levels = int.TryParse(_args.GetValueOrDefault("levelups"), out var n) ? n : 1;
        for (int level = 1; level <= levels; level++)
        {
            string P(string name) => $"{level}_{name}";
            if (Settings.Current.Interface.Classic) await ClassicLevelUp(dir, P);
            else await ModernLevelUp(dir, cls, P);
            await Wait(2.5);
            await Shot(dir, P("9_after"));
        }
        Character.Toggle();
        await Wait(1.5);
        await Shot(dir, "99_sheet");
        GetTree().Quit();
    }

    async Task ModernLevelUp(string dir, int cls, Func<string, string> name)
    {
        if (Find<UI.CharGen.LevelUpWindow>() is not { } lvl) return;
        lvl.Open();
        await Wait(2);
        await Shot(dir, name("1_classes"));
        lvl.SetClass(cls);
        await Wait(1);
        lvl.Recommend("all");
        await Wait(1.5);
        int i = 2;
        foreach (var step in new[] { UI.CharGen.LevelUpWindow.Step.Ability, UI.CharGen.LevelUpWindow.Step.Options, UI.CharGen.LevelUpWindow.Step.Skills, UI.CharGen.LevelUpWindow.Step.Feats, UI.CharGen.LevelUpWindow.Step.Spells, UI.CharGen.LevelUpWindow.Step.Confirm })
        {
            lvl.GoTo(step);
            await Wait(0.8);
            if (lvl.Current == step) await Shot(dir, name($"{i++}_{step.ToString().ToLowerInvariant()}"));
        }
        lvl.Apply();
    }

    /// <summary>The classic window: the class it selects (the class taken last), then Recommended and OK on each step.</summary>
    async Task ClassicLevelUp(string dir, Func<string, string> name)
    {
        if (UI.Classic.Creation.ClassicCreation.LevelUp is not { } l) return;
        l.Open();
        await Wait(2);
        for (int step = 1; step <= 9 && l.Visible; step++)
        {
            await Shot(dir, name($"{step}_step"));
            if (l.CanPress("RecommendButton")) l.Press("RecommendButton");
            await Wait(0.8);
            if (!l.Press("OkButton")) break;
            await Wait(1.2);
        }
    }

    /// <summary>
    /// --connect-nwn ws://host:port --capture-nwn-chargen dir [--chargen-class N]: on an NWN:EE server, opens character
    /// creation from the character list and makes a human of class N (default fighter) with the recommended choices
    /// and the first portrait, a shot at each step; the server takes the character and the game starts with it.
    /// </summary>
    async Task CaptureNwnChargen(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        CharacterSelectPanel? select = null;
        for (int i = 0; i < 300 && (select = Find<CharacterSelectPanel>()) is not { Visible: true }; i++) await Wait(0.1);
        await Wait(2);
        await Shot(dir, "00_characters");
        if (Find<UI.CharGen.CharGenWindow>() is not { } gen || select == null) { GetTree().Quit(1); return; }
        select.Visible = false;
        select.NewCharacterRequested?.Invoke();
        await Wait(2);
        int cls = int.TryParse(_args.GetValueOrDefault("chargen-class"), out var c) ? c : 4;
        gen.SetGender(0);
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Race);
        gen.SetRace(6);
        await Wait(0.5);
        await Shot(dir, "01_race");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Portrait);
        for (int i = 0; i < 50 && gen.Portraits.Count == 0; i++) await Wait(0.1);
        if (gen.Portraits.Count > 0) gen.SetPortrait(gen.Portraits[0].Id);
        await Wait(0.5);
        await Shot(dir, "02_portrait");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Class);
        gen.SetClass(cls);
        await Wait(0.6);
        await Shot(dir, "03_class");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Alignment);
        gen.SetAlignment(50, 50);
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Abilities);
        gen.RecommendAbilities();
        await Wait(0.8);
        await Shot(dir, "04_abilities");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Package);
        gen.Recommend("all");
        await Wait(1.2);
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Skills);
        await Wait(0.8);
        await Shot(dir, "05_skills");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Feats);
        await Wait(0.8);
        await Shot(dir, "06_feats");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Details);
        gen.SetName("Bridget", $"Probe{DateTime.Now:HHmmss}");
        await Wait(0.4);
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Finish);
        await Wait(1.0);
        await Shot(dir, "07_finish");
        gen.Finish();
        for (int i = 0; i < 300 && _session.Controlled == null; i++) await Wait(0.1);
        await Wait(3);
        await Shot(dir, "08_entered");
        GD.Print($"[nwn-chargen] {(gen.Visible ? "creation window still open" : "created")}; controlling {_session.ControlledId}");
        GetTree().Quit();
    }
}
