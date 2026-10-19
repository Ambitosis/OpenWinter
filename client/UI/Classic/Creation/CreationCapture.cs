using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// --capture-classic-chargen dir (with --ui classic, a module and no --character): walks the classic character
/// creation the way the original was measured (tools/visual/menudrive.py) and screenshots each
/// screen under the original's shot names (c01_main ... c47_main_done), enters the game with the character, then
/// takes three levels through the classic level-up (l00 ... l26; the third a cleric level, the fourth with the
/// ability increase), so the pairs can be compared with out/visual/menudrive.
/// </summary>
public static class CreationCapture
{
    public static async Task Run(string dir, CharacterSelectPanel select, Gameplay.ClientSession session, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        try { await Walk(dir, select, session, shot, wait, quit); }
        catch (Exception e) { GD.PrintErr($"[classic-chargen] {e}"); quit(); }
    }

    static async Task Walk(string dir, CharacterSelectPanel select, Gameplay.ClientSession session, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        await wait(2.5);
        if (ClassicCreation.CharGen is not { } g) { GD.PrintErr("[classic-chargen] not wired"); quit(); return; }
        async Task S(string name, double after = 0.6) { await wait(after); await shot(dir, name); }
        void Log(string what, bool ok) { if (!ok) GD.PrintErr($"[classic-chargen] could not press {what}"); }
        _wait = wait;
        // Entered with --character: only the level-ups.
        if (session.ControlledId != 0) { await LevelUps(session, S, Log, wait, quit); return; }
        select.Visible = false;
        g.Open();
        await wait(2.0);
        await S("c01_main", 0.5);
        Log("Gender", await Ready(g, "GenderButton") && g.Press("GenderButton"));
        await S("c02_gender");
        Log("Female", g.PressCaption(g.Tlk(157)));
        await S("c03_female");
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c04_main2");
        Log("Race", await Ready(g, "RaceButton") && g.Press("RaceButton"));
        await S("c05_race");
        Log("Elf", g.PressCaption(g.Tlk(24)));
        await S("c06_elf");
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        Log("Portrait", await Ready(g, "PortraitButton") && g.Press("PortraitButton"));
        await S("c07_portrait", 1.5);
        g.PickFirstPortrait();
        await S("c08_portrait_sel");
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c09_main3");
        Log("Class", await Ready(g, "ClassButton") && g.Press("ClassButton"));
        await S("c10_class");
        g.ScrollList(9);
        Log("Arcane Archer", g.PressCaption("Arcane Archer"));
        await S("c13_class_aa");
        Log("Wizard", g.PressCaption("Wizard"));
        await S("c14_class_wiz");
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c15_main4");
        Log("Alignment", await Ready(g, "AlignButton") && g.Press("AlignButton"));
        await S("c16_align");
        Log("Chaotic Good", g.Press("CGButton") || g.PressCaption("Chaotic Good"));
        await S("c18_align_cg");
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        Log("Abilities", await Ready(g, "AbilitiesButton") && g.Press("AbilitiesButton"));
        await S("c19_abil");
        Log("Intelligence", await Ready(g, "IntLabel") && g.Press("IntLabel"));
        await S("c20_abil_int");
        Log("Recommended", await Ready(g, "RecommendButton") && g.Press("RecommendButton"));
        await S("c21_abil_rec");
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c22_main5", 1.0);
        Log("Packages", await Ready(g, "PackagesButton") && g.Press("PackagesButton"));
        await S("c23_package");
        Log("Configure", await Ready(g, "ConfigurePckg") && g.Press("ConfigurePckg"));
        await S("c24_configure", 1.0);
        Log("Concentration", g.PressCaption("Concentration"));
        await S("c25_skill_conc");
        Log("Recommended", await Ready(g, "RecommendButton") && g.Press("RecommendButton"));
        await S("c26_skill_rec", 1.0);
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c27_after_skills", 1.0);
        Log("Alertness ?", g.PressRowButton("Alertness", 0));
        await S("c29_featinfo", 0.8);
        Log("Close", await Ready(g, "CloseButton") && g.Press("CloseButton"));
        Log("Recommended", await Ready(g, "RecommendButton") && g.Press("RecommendButton"));
        await S("c30_feat_rec", 1.0);
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c32_after_feats", 0.8);
        Log("Evocation", g.PressCaption("Evocation"));
        await S("c33_school_evo");
        Log("General", g.PressCaption("General"));
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c34_spells", 1.0);
        Log("Recommended", await Ready(g, "RecommendButton") && g.Press("RecommendButton"));
        await S("c35_spells_rec", 1.0);
        Log("Cantrips", await Ready(g, "SpellLevel0") && g.Press("SpellLevel0"));
        await S("c36_spells_cantrip");
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c37_after_spells", 1.5);
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c38_after_familiar", 1.0);
        g.RecordTab(1);
        await S("c39_tab2");
        g.RecordTab(3);
        await S("c41_tab4");
        g.RecordTab(0);
        Log("Customize", await Ready(g, "CustomizeButton") && g.Press("CustomizeButton"));
        await S("c42_customize", 2.0);
        Log("Hair colour", g.PressSwatch(1));
        await S("c43_haircolor");
        g.PickColour(1, 4);
        Log("Head >", g.PressRowButton(g.Tlk(123), 1));
        await S("c44_hair_head", 1.5);
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c45_charinfo", 0.8);
        Log("Deity", await Ready(g, "DeityButton") && g.Press("DeityButton"));
        await S("c46_deity");
        if (ClassicPopup.Top is { } popup) { popup.Panel.Get<GuiButton>("OkButton")?.EmitSignal(BaseButton.SignalName.Pressed); }
        Log("OK", await Ready(g, "OkButton") && g.Press("OkButton"));
        await S("c47_main_done", 1.0);
        Log("Play", await Ready(g, "PlayButton") && g.Press("PlayButton"));
        await wait(6.0);
        await shot(dir, "g00_ingame");
        await LevelUps(session, S, Log, wait, quit);
    }

    static Func<double, Task>? _wait;

    /// <summary>Waits up to 4 s for a button to be pressable (the server's plan may still be on its way).</summary>
    static async Task<bool> Ready(CgFlow f, string tag)
    {
        for (int i = 0; i < 40 && !f.CanPress(tag) && _wait != null; i++) await _wait(0.1);
        return true;
    }

    /// <summary>Three level-ups (the scene gives the experience for level 4): wizard, cleric, wizard with the ability increase.</summary>
    static T? Find<T>(Node root, Func<T, bool> match) where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T t && match(t)) return t;
            if (Find(child, match) is { } found) return found;
        }
        return null;
    }

    /// <summary>Rests a posted pointer on a control's middle (the real pointer stays where it is).</summary>
    static async Task Hover(Control? c, Func<double, Task> wait)
    {
        if (c == null) { GD.PrintErr("[classic-chargen] nothing to hover"); return; }
        var p = c.GetGlobalRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        await wait(0.1);
    }

    static async Task LevelUps(Gameplay.ClientSession session, Func<string, double, Task> S, Action<string, bool> Log, Func<double, Task> wait, Action quit)
    {
        _wait = wait;
        await wait(1.5);
        if (ClassicCreation.LevelUp is not { } l) { quit(); return; }
        l.Open();
        await S("l00_levelup", 1.5);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l04_after_class", 1.0);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await S("l05_skills_rec", 0.8);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l06_after_skills", 1.0);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await wait(0.8);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l07_after_spells", 1.5);
        // Hover on the familiar step (the original with the real pointer): the row under the pointer shows
        // its ?, which opens the description; the record's tabs have hover text. A posted pointer, not the real one.
        var pixie = Find<CgButton>(l, b => b.Row && b.Caption == "Pixie");
        await Hover(pixie, wait);
        await S("l07b_hover_row", 0.8);
        await Hover(Find<Control>(l, c => c.Name == "ctl_chr_btn_tab3"), wait);
        await S("l07c_hover_tab", 1.0);
        Log("?", pixie?.GetChildren().OfType<CgButton>().FirstOrDefault(b => b.Visible && b.Name == "ctl_cg_btn_help") is { } help
            && help.EmitSignal(BaseButton.SignalName.Pressed) == Error.Ok);
        await S("l07d_familiar_help", 0.8);
        ClassicPopup.Top?.Panel.Get<GuiButton>("OkButton")?.EmitSignal(BaseButton.SignalName.Pressed);
        await wait(0.5);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l08_summary", 1.0);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await wait(2.0);
        // A cleric level: domains.
        l.Open();
        await wait(1.5);
        l.ScrollList(0);
        Log("Cleric", l.PressCaption("Cleric"));
        await S("l12_cleric", 0.6);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l13_after_cleric", 1.0);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await wait(0.8);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l14_feats", 1.0);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await wait(0.8);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l15_after_feats", 1.0);
        Log("Air", l.PressCaption("Air"));
        await S("l16_domain_air", 0.6);
        Log("Air >", l.PressRowButton("Air", 0));
        await S("l17_domain_added", 0.6);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await S("l18_domain_rec", 1.0);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l19_after_domain", 1.0);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await wait(2.0);
        // The fourth level: the ability increase.
        l.Open();
        await S("l20_lvl4_class", 1.5);
        Log("Wizard", l.PressCaption("Wizard"));
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l22_ability", 1.0);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await S("l23_ability_rec", 0.8);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await wait(0.8);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await wait(0.8);
        Log("OK", await Ready(l, "OkButton") && l.Press("OkButton"));
        await S("l24_spells_l4", 1.0);
        Log("Recommended", await Ready(l, "RecommendButton") && l.Press("RecommendButton"));
        await S("l26_spells_rec", 1.0);
        Log("Cancel", await Ready(l, "CancelButton") && l.Press("CancelButton"));
        await S("l27_cancel", 1.0);
        quit();
    }
}
