using OpenWinter.Client.UI.CharGen;
using OpenWinter.Game.Progression;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>The first creation steps: cg_sex, cg_race, cg_portrait, cg_class, cg_alignment, cg_attributes and cg_package.</summary>
public partial class ClassicCharGen
{
    // Alignments as the layout names them, with their talk-table name, help title ("Lawful Neutral \"Judge\"") and text.
    static readonly (string Tag, int LawChaos, int GoodEvil, uint Name, uint Title, uint Text)[] Alignments =
    {
        ("LGButton", 85, 85, 112, 468, 452), ("LNButton", 85, 50, 113, 469, 453), ("LEButton", 85, 15, 114, 467, 451),
        ("NGButton", 50, 85, 115, 471, 455), ("TNButton", 50, 50, 116, 472, 456), ("NEButton", 50, 15, 117, 470, 454),
        ("CGButton", 15, 85, 118, 465, 449), ("CNButton", 15, 50, 119, 466, 450), ("CEButton", 15, 15, 120, 464, 448),
    };
    static readonly uint[] AlignmentRefs = Alignments.Select(a => a.Name).ToArray();

    static int AlignmentIndex(int lc, int ge)
    {
        int l = lc >= 70 ? 85 : lc <= 30 ? 15 : 50, g = ge >= 70 ? 85 : ge <= 30 ? 15 : 50;
        for (int i = 0; i < Alignments.Length; i++) if (Alignments[i].LawChaos == l && Alignments[i].GoodEvil == g) return i;
        return 4;
    }

    // ------------------------------------------------------------------ gender

    void OpenGender()
    {
        if (ShowScreen("cg_sex") is not { } p) return;
        // The general text first (c02); a choice shows its own (c03).
        SetHelp(A.Tlk(203), A.Tlk(447));
        WireMain(MainStep.Gender);
        var buttons = new[] { ("MaleButton", 0, 445u, 199u), ("FemaleButton", 1, 446u, 200u) }
            .Where(x => p.Get<GuiButton>(x.Item1) != null)
            .Select(x => (Button: CgButton.From(p.Get<GuiButton>(x.Item1)!), Gender: x.Item2, Title: x.Item3, Text: x.Item4)).ToList();
        foreach (var (b, g, title, text) in buttons)
            b.Pressed += () =>
            {
                if (_n.Gender != g)
                {
                    _n.Gender = g;
                    _n.PortraitId = -1;
                    _n.Portrait = "";
                    _n.SoundSet = DefaultSoundSet();
                }
                SetHelp(A.Tlk(title), A.Tlk(text));
                RefreshScreen?.Invoke();
            };
        RefreshScreen = () => { foreach (var (b, g, _, _) in buttons) b.Selected = g == _n.Gender; };
        RefreshScreen();
    }

    // ------------------------------------------------------------------ race

    void OpenRace()
    {
        if (ShowScreen("cg_race") is not { } p || Model == null) return;
        WireMain(MainStep.Race, () => _n.Race >= 0);
        var list = CgList.Attach(p, "RaceListBox");
        var rows = new Dictionary<int, CgButton>();
        void Select(int race)
        {
            if (_n.Race != race)
            {
                _n.Race = race;
                _n.PortraitId = -1;
                _n.Portrait = "";
                _n.BodyParts[0] = 1;
            }
            if (Model!.Races.GetValueOrDefault(race) is { } r) SetHelp(r.Str("plural") is { Length: > 0 } pl ? pl : r.Name, r.Description);
            foreach (var (id, b) in rows) b.Selected = id == _n.Race;
        }
        // racialtypes row order (c05: Dwarf, Elf, Gnome, Halfling, Half-Elf, Half-Orc, Human).
        foreach (var r in Model.Races.Values.OrderBy(r => r.Id))
        {
            int id = r.Id;
            var row = CgButton.Make(A, "ctl_cg_btn_class", r.Name);
            row.Pressed += () => Select(id);
            rows[id] = row;
            list?.Add(row);
        }
        // Recommended: Human (not measured: the original's choice for this button was not captured).
        On("RecommendButton", () => { if (Model.Races.ContainsKey(6)) Select(6); });
        On("SubRaceButton", () => ClassicPopup.Edit(this, A, A.Tlk(7524), _n.Subrace, t => _n.Subrace = t.Trim(), null, 64));
        Select(_n.Race);
        if (list != null && rows.Keys.ToList().IndexOf(_n.Race) is int idx and >= 0) list.Reveal(idx);
    }

    // ------------------------------------------------------------------ portrait

    void OpenPortrait()
    {
        if (ShowScreen("cg_portrait") is not { } p) return;
        WireMain(MainStep.Portrait, () => _n.PortraitId >= 0 || _n.Portrait.Length > 0);
        if (_portraitsFor != (_n.Race, _n.Gender))
        {
            _portraitsFor = (_n.Race, _n.Gender);
            _portraits.Clear();
            Session.Request("chargen-portraits", new { race = _n.Race, gender = _n.Gender });
        }
        // The large portrait (ctl_cg_bigportrt's PortraitPlane) shows the choice, the unknown one before (c07, c08).
        var big = p.Get<GuiModelView>("Portrait");
        var bigRect = big?.Model?.Rest.Meshes.FirstOrDefault(m => m.Node.Equals("PortraitPlane", StringComparison.OrdinalIgnoreCase)) is { } plane
            ? new Rect2(plane.Points.Min(q => q.X), plane.Points.Min(q => q.Y), plane.Points.Max(q => q.X) - plane.Points.Min(q => q.X), plane.Points.Max(q => q.Y) - plane.Points.Min(q => q.Y))
            : new Rect2(4, 4, 254, 398);
        var bigView = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        if (big != null) { bigView.Position = big.Position + big.Origin + bigRect.Position; bigView.Size = bigRect.Size; }
        p.AddChild(bigView);
        // The grid: ctl_cg_portraits on the panel model's "portraits" node, 6 a row 72 px apart, rows 107 px apart (c07).
        var grid = new CgPortraitGrid { Assets = A };
        p.AddChild(grid);
        var origin = p.Node("portraits") ?? p.ModelOrigin + new Vector2(-95, 193);
        grid.Place(origin);
        grid.Picked = id => { _n.PortraitId = id; _n.Portrait = ""; RefreshScreen?.Invoke(); };
        RefreshScreen = () =>
        {
            grid.SetPortraits(_portraits, _n.PortraitId);
            var chosen = _n.PortraitId >= 0 || _n.Portrait.Length > 0 ? PortraitResRef() : "gui_po_nwnlogo_";
            bigView.Texture = ChargenModel.Portrait(Session.Content, chosen.Length > 0 ? chosen : "gui_po_nwnlogo_", "h") ?? ChargenModel.Portrait(Session.Content, chosen, "l");
            Enable("OkButton", _n.PortraitId >= 0 || _n.Portrait.Length > 0);
        };
        RefreshScreen();
    }

    // ------------------------------------------------------------------ class

    /// <summary>Classes as the original lists them: those that can be taken by name, then the rest by name, greyed (c10-c12, l01-l03).</summary>
    IEnumerable<(ChargenEntry Class, bool Available)> ClassRows()
    {
        var m = Model!;
        return m.Classes.Values.Where(c => c.Bool("player"))
            .Select(c => (Class: c, Available: !c.Bool("prestige")))
            .OrderBy(x => x.Available ? 0 : 1).ThenBy(x => x.Class.Name, StringComparer.CurrentCultureIgnoreCase);
    }

    void OpenClass()
    {
        if (ShowScreen("cg_class", A.Tlk(61920)) is not { } p || Model == null) return;
        // The general text with an empty title until a class is clicked (c10).
        SetHelp("", A.Tlk(484));
        var rows = ClassRows().ToList();
        if (_n.Level.Class < 0 || !Model.Classes.ContainsKey(_n.Level.Class)) _n.Level.Class = rows.FirstOrDefault().Class?.Id ?? -1;
        int selected = _n.Level.Class;
        bool Available(int cls) => rows.Any(r => r.Class.Id == cls && r.Available);
        var list = CgList.Attach(p, "ClassListBox");
        var buttons = new Dictionary<int, CgButton>();
        void Select(int cls, bool describe)
        {
            selected = cls;
            if (describe && Model.Classes.GetValueOrDefault(cls) is { } c) SetHelp(c.Name, c.Description);
            foreach (var (id, b) in buttons) b.Selected = id == cls;
            // A class that can't be taken can be read about, but OK stays greyed (c13).
            Enable("OkButton", Available(cls));
        }
        foreach (var (c, available) in rows)
        {
            int id = c.Id;
            var row = CgButton.Make(A, "ctl_cg_btn_class", c.Name, ChargenModel.Icon(A.Content, c.Icon));
            row.Greyed = !available;
            row.Pressed += () => Select(id, true);
            buttons[id] = row;
            list?.Add(row);
        }
        // Recommended: the race's favoured class when it has one (inferred; the original's button was not captured).
        On("RecommendButton", () =>
        {
            int fav = Model.Races.GetValueOrDefault(_n.Race)?.Int("favored", -1) ?? -1;
            int pick = fav >= 0 && Available(fav) ? fav : rows.FirstOrDefault(r => r.Available).Class?.Id ?? -1;
            if (pick >= 0) { Select(pick, true); list?.Reveal(rows.FindIndex(r => r.Class.Id == pick)); }
        });
        On("OkButton", () =>
        {
            if (!Available(selected)) return;
            ApplyClass(selected);
            Taken(MainStep.Class);
        });
        On("CancelButton", Abandon);
        if (!Model.Recommended && Btn("RecommendButton") is { } rec) rec.Visible = false;
        Select(selected, false);
        list?.Reveal(rows.FindIndex(r => r.Class.Id == selected));
    }


    /// <summary>Takes the class chosen on the class screen (called before the step is marked taken).</summary>
    void ApplyClass(int cls)
    {
        var m = Model!;
        if (_n.Level.Class != cls || _n.Level.Package < 0)
        {
            int pkg = m.Classes.TryGetValue(cls, out var c) ? c.Int("defaultPackage", -1) : -1;
            _n.Level = new LevelChoice { Class = cls, Package = pkg };
        }
        // The class's spellcasting ability starts at 11, enough for first-level spells (c19: a wizard's Intelligence;
        // other classes not measured), within the point budget.
        int ab = CastingAbility(cls);
        if (ab >= 0 && _n.Abilities[ab] < 11)
        {
            var trial = (int[])_n.Abilities.Clone();
            trial[ab] = 11;
            if (Enumerable.Range(0, 6).Sum(i => m.CostOf(trial[i])) <= Budget()) _n.Abilities = trial;
        }
    }

    int CastingAbility(int cls) =>
        Session.Content?.Resources.TwoDA("classes") is { } t && t.GetInt(cls, "SpellCaster") == 1 ? AbilityIndex(t.Get(cls, "SpellcastingAbil")) : -1;

    int Budget() => Model?.Races.GetValueOrDefault(_n.Race)?.Int("budget", 30) ?? 30;

    // ------------------------------------------------------------------ alignment

    /// <summary>
    /// The alignment the Recommended button picks and the screen opens with: Lawful Neutral for a wizard (c16, c17);
    /// for other classes the first allowed of Lawful Neutral, True Neutral, then the rest (not measured).
    /// </summary>
    int RecommendedAlignment()
    {
        var allowed = Model?.Classes.GetValueOrDefault(_n.Level.Class)?.Bools("alignments") ?? Array.Empty<bool>();
        bool Ok(int i)
        {
            var a = Alignments[i];
            int idx = Model!.Alignments.FindIndex(x => x.LawChaos == a.LawChaos && x.GoodEvil == a.GoodEvil);
            return idx < 0 || idx >= allowed.Length || allowed[idx];
        }
        foreach (int i in new[] { 1, 4, 0, 3, 6, 7, 2, 5, 8 }) if (Ok(i)) return i;
        return 4;
    }

    bool _alignmentChosen;

    void OpenAlignment()
    {
        if (ShowScreen("cg_alignment") is not { } p || Model == null) return;
        var allowed = Model.Classes.GetValueOrDefault(_n.Level.Class)?.Bools("alignments") ?? Array.Empty<bool>();
        bool Ok(int i)
        {
            var a = Alignments[i];
            int idx = Model.Alignments.FindIndex(x => x.LawChaos == a.LawChaos && x.GoodEvil == a.GoodEvil);
            return idx < 0 || idx >= allowed.Length || allowed[idx];
        }
        int selected = _alignmentChosen && Ok(AlignmentIndex(_n.LawChaos, _n.GoodEvil)) ? AlignmentIndex(_n.LawChaos, _n.GoodEvil) : RecommendedAlignment();
        var buttons = new List<CgButton?>();
        void Select(int i)
        {
            selected = i;
            SetHelp(A.Tlk(Alignments[i].Title), A.Tlk(Alignments[i].Text));
            for (int k = 0; k < buttons.Count; k++) if (buttons[k] is { } b) b.Selected = k == i;
        }
        for (int i = 0; i < Alignments.Length; i++)
        {
            int index = i;
            if (p.Get<GuiButton>(Alignments[i].Tag) is not { } gb) { buttons.Add(null); continue; }
            var b = CgButton.From(gb);
            b.Disabled = !Ok(i);
            b.Pressed += () => Select(index);
            buttons.Add(b);
        }
        On("RecommendButton", () => Select(RecommendedAlignment()));
        On("OkButton", () =>
        {
            _n.LawChaos = Alignments[selected].LawChaos;
            _n.GoodEvil = Alignments[selected].GoodEvil;
            _alignmentChosen = true;
            Taken(MainStep.Alignment);
        });
        On("CancelButton", Abandon);
        if (!Model.Recommended && Btn("RecommendButton") is { } r) r.Visible = false;
        Select(selected);
    }

    // ------------------------------------------------------------------ abilities (point buy)

    static readonly string[] AbilityTagsCg = { "Str", "Dex", "Con", "Int", "Wis", "Cha" };
    static readonly uint[] AbilityTitles = { 473, 474, 475, 477, 476, 479 };

    int _abilitySelected = -1;

    void OpenAbilities()
    {
        if (ShowScreen("cg_attributes") is not { } p || Model == null) return;
        var m = Model;
        SetHelp(A.Tlk(261), A.Tlk(457));
        _abilitySelected = -1;
        int Spent() => Enumerable.Range(0, 6).Sum(i => m.CostOf(_n.Abilities[i]));
        int Left() => Budget() - Spent();
        int NextCost(int a) => m.CostOf(_n.Abilities[a] + 1) - m.CostOf(_n.Abilities[a]);
        // OK opens once every point is spent (c19: greyed with 27 left; c21: open at 0).
        WireMain(MainStep.Abilities, () => Left() == 0);
        var racial = RacialAdjustments;
        void Info(int a) => SetHelp(A.Tlk(AbilityTitles[a]), AbilityInfo(a, _n.Abilities[a], racial[a], 0));
        for (int i = 0; i < 6; i++)
        {
            int a = i;
            if (p.Get<GuiButton>(AbilityTagsCg[i] + "Label") is { } label) label.Pressed += () => { _abilitySelected = a; Info(a); RefreshScreen?.Invoke(); };
            On(AbilityTagsCg[i] + "UpButton", () =>
            {
                if (_n.Abilities[a] >= m.AbilityMax || NextCost(a) > Left()) return;
                _n.Abilities[a]++;
                _abilitySelected = a;
                ResetPicks();
                Validate();
                RefreshScreen?.Invoke();
            });
            On(AbilityTagsCg[i] + "DownButton", () =>
            {
                if (_n.Abilities[a] <= m.AbilityMin) return;
                _n.Abilities[a]--;
                _abilitySelected = a;
                ResetPicks();
                Validate();
                RefreshScreen?.Invoke();
            });
        }
        On("RecommendButton", () => { RecommendAbilities(); RefreshScreen?.Invoke(); });
        RefreshScreen = () =>
        {
            for (int i = 0; i < 6; i++)
            {
                int score = _n.Abilities[i] + racial[i];
                if (p.Get<GuiLabel>(AbilityTagsCg[i] + "Edit") is { } edit) edit.Text = $"{score} ({Signed(Mod(score))})";
            }
            if (p.Get<GuiButton>("PointsEdit") is { } pts) pts.Caption = Left().ToString();
            // The cost of raising the ability last pressed (c20: 1); empty before (c19). Not refreshed by Recommended (c21).
            if (p.Get<GuiButton>("CostEdit") is { } cost) cost.Caption = _abilitySelected >= 0 ? NextCost(_abilitySelected).ToString() : "";
            Enable("OkButton", Left() == 0);
        };
        RefreshScreen();
    }

    void ResetPicks()
    {
        _n.Level.Skills.Clear();
        _n.Level.Feats.Clear();
        _n.Level.Spells.Clear();
    }

    /// <summary>The class's suggested scores (classes.2da Str..Cha), fitted to the race's points (c21: a wizard's 10/14/14/12/16/10 base).</summary>
    public void RecommendAbilities()
    {
        var m = Model!;
        var c = m.Classes.GetValueOrDefault(_n.Level.Class);
        var rec = c?.Ints("recommended") is { Length: 6 } r ? r : Enumerable.Repeat(10, 6).ToArray();
        var scores = rec.Select(v => Math.Clamp(v, m.AbilityMin, m.AbilityMax)).ToArray();
        int budget = Budget();
        while (scores.Sum(m.CostOf) > budget)
        {
            int hi = Array.IndexOf(scores, scores.Max());
            scores[hi]--;
        }
        _n.Abilities = scores;
        ResetPicks();
        Validate();
    }

    // ------------------------------------------------------------------ packages

    void OpenPackages()
    {
        if (ShowScreen("cg_package") is not { } p || Model == null) return;
        var m = Model;
        SetHelp(A.Tlk(483), A.Tlk(487));
        var packages = m.Packages.Values.Where(x => x.Int("class", -1) == _n.Level.Class).OrderBy(x => x.Id).ToList();
        int def = m.Classes.GetValueOrDefault(_n.Level.Class)?.Int("defaultPackage", -1) ?? -1;
        int selected = packages.Any(x => x.Id == _n.Level.Package) ? _n.Level.Package : def >= 0 ? def : packages.FirstOrDefault()?.Id ?? -1;
        var list = CgList.Attach(p, "ClassListBox");
        var rows = new Dictionary<int, CgButton>();
        void Select(int id, bool describe)
        {
            selected = id;
            if (describe && m.Packages.GetValueOrDefault(id) is { } pk) SetHelp(pk.Name, pk.Description);
            foreach (var (k, b) in rows) b.Selected = k == id;
        }
        foreach (var pk in packages)
        {
            int id = pk.Id;
            var row = CgButton.Make(A, "ctl_cg_btn_class", pk.Name);
            row.Pressed += () => Select(id, true);
            rows[id] = row;
            list?.Add(row);
        }
        On("RecommendButton", () => { if (def >= 0) Select(def, true); });
        // OK takes the package as it is: its skills, feats, spells and options (c50: straight back to the main screen).
        On("OkButton", () =>
        {
            if (selected < 0) return;
            ChoosePackage(selected);
            _afterAuto = () => Taken(MainStep.Packages);
            Recommend("all");
        });
        // Configure Packages walks through the choices, the package's as the starting point (c24).
        On("ConfigurePckg", () =>
        {
            if (selected < 0) return;
            ChoosePackage(selected);
            GoStep(LevelStep.Skills);
        });
        On("CancelButton", Abandon);
        if (!m.Recommended && Btn("RecommendButton") is { } r) r.Visible = false;
        Select(selected, false);
    }

    void ChoosePackage(int package)
    {
        if (_n.Level.Package != package)
        {
            int cls = _n.Level.Class;
            _n.Level = new LevelChoice { Class = cls, Package = package };
        }
        Validate();
    }
}
