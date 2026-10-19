using System.Text.Json;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// The level step screens: cg_levup_attrib (ability increase), cg_skills, cg_feats (with cg_feats_popup for a master
/// feat's choices and cg_spellinfo for a feat's text), cg_school, cg_domain, cg_spells and cg_familiar (familiar or
/// animal companion). Layouts, captions and behaviour as measured on the original (c24-c37, l04-l26).
/// </summary>
public abstract partial class CgFlow
{
    static readonly string[] AbilityTags = { "Str", "Dex", "Con", "Int", "Wis", "Cha" };
    static readonly uint[] AbilityInfoTitles = { 473, 474, 475, 477, 476, 479 };
    static readonly uint[] AbilityInfoTexts = { 459, 460, 461, 463, 462, 478 };
    static readonly uint[] AbilityNameRefs = { 135, 133, 132, 134, 136, 131 };

    /// <summary>A child button on a row at one of the row model's nodes (skill up / down, add / remove, help).</summary>
    protected CgButton RowButton(CgButton row, string node, string model, Action pressed)
    {
        var b = CgButton.Make(A, model);
        b.PlaceOrigin(row.Model?.Node(node) is { } n ? n + row.Origin : new Vector2(row.Size.X - 36, row.Size.Y - 4));
        b.Pressed += pressed;
        row.AddChild(b);
        return b;
    }

    /// <summary>The help text of an ability: the scores that make it, then its description (c20).</summary>
    protected string AbilityInfo(int a, int baseScore, int racial, int classBonus)
    {
        int final = baseScore + racial + classBonus;
        return $"{A.Tlk(61919)}: {baseScore}\n{A.Tlk(6273)}: {Signed(racial)}\n{A.Tlk(6388)}: {classBonus}\n{A.Tlk(6275)}: {final}\n{A.Tlk(6274)}: {Signed(Mod(final))}\n\n{A.Tlk(AbilityInfoTexts[a])}";
    }

    // ------------------------------------------------------------------ ability increase (every fourth level)

    protected abstract int[] AbilitiesBefore { get; }
    protected abstract int[] RacialAdjustments { get; }

    void OpenAbilityIncrease()
    {
        if (ShowScreen("cg_levup_attrib") is not { } p) return;
        SetHelp(A.Tlk(261), A.Tlk(457));
        WireStepButtons(() => Choice.Ability >= 0);
        On("RecommendButton", () =>
        {
            int primary = Model?.Classes.GetValueOrDefault(Choice.Class) is { } c ? AbilityIndex(c.Str("primary")) : -1;
            if (primary >= 0) { Choice.Ability = primary; Validate(); RefreshScreen?.Invoke(); }
        });
        var before = AbilitiesBefore;
        var racial = RacialAdjustments;
        for (int i = 0; i < 6; i++)
        {
            int a = i;
            if (p.Get<GuiButton>(AbilityTags[i] + "Label") is { } label)
                label.Pressed += () => SetHelp(A.Tlk(AbilityInfoTitles[a]), AbilityInfo(a, before[a] + (Choice.Ability == a ? 1 : 0), racial[a], 0));
            On(AbilityTags[i] + "UpButton", () => { if (Choice.Ability < 0) { Choice.Ability = a; Validate(); RefreshScreen?.Invoke(); } });
            On(AbilityTags[i] + "DownButton", () => { if (Choice.Ability == a) { Choice.Ability = -1; Validate(); RefreshScreen?.Invoke(); } });
        }
        RefreshScreen = () =>
        {
            for (int i = 0; i < 6; i++)
            {
                int score = before[i] + racial[i] + (Choice.Ability == i ? 1 : 0);
                if (p.Get<GuiLabel>(AbilityTags[i] + "Edit") is { } edit) edit.Text = $"{score} ({Signed(Mod(score))})";
            }
            if (p.Get<GuiButton>("PointsEdit") is { } points) points.Caption = (Choice.Ability >= 0 ? 0 : 1).ToString();
            Enable("OkButton", Choice.Ability >= 0);
        };
        RefreshScreen();
    }

    protected static int AbilityIndex(string label) => label.ToUpperInvariant() switch
    {
        "STR" => 0, "DEX" => 1, "CON" => 2, "INT" => 3, "WIS" => 4, "CHA" => 5, _ => -1,
    };

    // ------------------------------------------------------------------ skills

    int _skillSelected = -1;

    void OpenSkills()
    {
        if (ShowScreen("cg_skills", Title(A.Tlk(396))) is not { } p) return;
        SetHelp(A.Tlk(58250), A.Tlk(58251));
        _skillSelected = -1;
        WireStepButtons();
        On("RecommendButton", () => Recommend("skills"));
        var list = CgList.Attach(p, "SkillsButtonBox");
        var remaining = p.Get<GuiLabel>("PtsRemainingBox");
        var cost = p.Get<GuiLabel>("CostBox");
        var rows = new Dictionary<int, (CgButton Row, GuiLabel Ranks)>();
        void Build()
        {
            if (list == null || Model == null) return;
            int top = list.Top;
            list.Clear();
            rows.Clear();
            var plan = Plan;
            foreach (var sk in Model.Skills.Values.Where(s => !s.Bool("hidden")).OrderBy(s => s.Name))
            {
                var opt = plan?.Skills.FirstOrDefault(o => o.Id == sk.Id);
                bool canBuy = opt is { Id: >= 0 } && plan!.Skills.Any(o => o.Id == sk.Id);
                bool classSkill = canBuy && opt!.Value.ClassSkill;
                int id = sk.Id;
                var row = CgButton.Make(A, "ctl_cg_btn_skill", sk.Name + (classSkill ? " " + A.Tlk(52951) : ""), ChargenModel.Icon(A.Content, sk.Icon));
                row.Greyed = !canBuy;
                row.Selected = id == _skillSelected;
                row.Pressed += () => SelectSkill(id);
                // The rank box (ctl_cg_numbox3, 34 px) on the row model's skillrank node, the number centred in it (c24).
                var at = row.Model?.Node("skillrank") is { } sr ? sr + row.Origin : new Vector2(266, 41);
                var box = GuiModelView.Create(A, "ctl_cg_numbox3");
                box.PlaceOrigin(at);
                row.AddChild(box);
                var ranks = GuiLabel.Create(A, "", CgKit.Font, Colors.White, 0.5f, 0.5f);
                ranks.Position = box.Position;
                ranks.Size = box.Size;
                row.AddChild(ranks);
                RowButton(row, "skillup", "ctl_cg_btn_skup", () => ChangeSkill(id, +1));
                RowButton(row, "skilldown", "ctl_cg_btn_skdn", () => ChangeSkill(id, -1));
                list.Add(row);
                rows[id] = (row, ranks);
            }
            list.ScrollTo(top);
        }
        void SelectSkill(int id)
        {
            _skillSelected = id;
            foreach (var (k, r) in rows) r.Row.Selected = k == id;
            ShowSkillHelp(id);
            Update();
        }
        void ChangeSkill(int id, int delta)
        {
            var plan = Plan;
            var opt = plan?.Skills.FirstOrDefault(o => o.Id == id);
            if (plan == null || opt is not { } o || !plan.Skills.Any(x => x.Id == id)) return;
            int added = Choice.SkillRanks(id);
            if (delta > 0 && (o.Current + added >= o.Max || SkillLeft() < o.Cost)) return;
            if (delta < 0 && added <= 0) return;
            Choice.Skills[id] = added + delta;
            if (Choice.Skills[id] == 0) Choice.Skills.Remove(id);
            if (_skillSelected != id) SelectSkill(id);
            else ShowSkillHelp(id);
            Update();
            Validate();
        }
        void Update()
        {
            var plan = Plan;
            if (remaining != null) remaining.Text = SkillLeft().ToString();
            var opt = plan?.Skills.FirstOrDefault(o => o.Id == _skillSelected);
            if (cost != null) cost.Text = _skillSelected >= 0 && plan != null && plan.Skills.Any(o => o.Id == _skillSelected) ? opt!.Value.Cost.ToString() : "";
            foreach (var (id, r) in rows)
            {
                var o = plan?.Skills.FirstOrDefault(x => x.Id == id);
                int current = plan != null && plan.Skills.Any(x => x.Id == id) ? o!.Value.Current : CurrentRanks(id);
                r.Ranks.Text = (current + Choice.SkillRanks(id)).ToString();
            }
        }
        Build();
        Update();
        int planCount = Plan?.Skills.Count ?? -1;
        RefreshScreen = () =>
        {
            if ((Plan?.Skills.Count ?? -1) != planCount || rows.Count == 0) { planCount = Plan?.Skills.Count ?? -1; Build(); }
            Update();
        };
    }

    /// <summary>Skill points left by the latest plan and the picks made since.</summary>
    protected int SkillLeft()
    {
        var plan = Plan;
        if (plan == null) return 0;
        int spent = Choice.Skills.Sum(k => k.Value * (plan.Skills.FirstOrDefault(o => o.Id == k.Key) is { Id: >= 0 } o && plan.Skills.Any(x => x.Id == k.Key) ? o.Cost : 2));
        return plan.SkillAvailable - spent;
    }

    /// <summary>Ranks the character already has in a skill (for skills this class can't raise).</summary>
    protected virtual int CurrentRanks(int skill) => 0;

    void ShowSkillHelp(int id)
    {
        if (Model?.Skills.GetValueOrDefault(id) is not { } sk) return;
        var plan = Plan;
        var opt = plan?.Skills.FirstOrDefault(o => o.Id == id);
        int ranks = (plan != null && plan.Skills.Any(o => o.Id == id) ? opt!.Value.Current : CurrentRanks(id)) + Choice.SkillRanks(id);
        int mod = SkillModifier(id, sk);
        Describe("skill", id, (name, text) => SetHelp(name, $"{A.Tlk(6597)}: {ranks}\n{A.Tlk(6596)}: {mod}\n{A.Tlk(6598)}: {ranks + mod}\n{text}"));
    }

    /// <summary>
    /// A skill's modifier: the record's total less its ranks when there is a record (feats and racial bonuses count),
    /// else the key ability's modifier (c25: Concentration with Constitution 12 shows 1).
    /// </summary>
    int SkillModifier(int id, ChargenEntry sk)
    {
        if (Record is { } r && r.TryGetProperty("sheet", out var sheet) && sheet.TryGetProperty("skills", out var skills))
            foreach (var e in skills.EnumerateArray())
                if (e.GetProperty("id").GetInt32() == id) return e.GetProperty("total").GetInt32() - e.GetProperty("ranks").GetInt32();
        int a = AbilityIndex(sk.Str("ability"));
        var ab = AbilitiesAfter;
        return a >= 0 && ab.Length == 6 ? Mod(ab[a] + RacialAdjustments[a]) : 0;
    }

    // ------------------------------------------------------------------ feats

    void OpenFeats()
    {
        if (ShowScreen("cg_feats", Title(A.Tlk(397))) is not { } p) return;
        var available = CgList.Attach(p, "AvailBox");
        var known = CgList.Attach(p, "KnownBox");
        var remaining = p.Get<GuiLabel>("RemainLabel");
        On("OkButton", () => { if (FeatsDone()) NextStep(); });
        On("CancelButton", LevelStepsCancelled);
        On("RecommendButton", () => Recommend("feats"));
        On("ResetButton", () => { Choice.Feats.Clear(); Validate(); RefreshScreen?.Invoke(); });
        string Icon(int f) => Model?.Feats.GetValueOrDefault(f)?.Icon ?? "";
        string selected = "";
        CgButton FeatRow(int f, string name, string icon, bool add, bool greyed, Action arrow, Action info)
        {
            var row = CgButton.Make(A, "ctl_cg_btn_feat", name, ChargenModel.Icon(A.Content, icon));
            row.Greyed = greyed;
            string key = (add ? "+" : "-") + name;
            // A clicked row is selected (blue) and shows its help button (c28); its arrow takes or gives back the feat.
            var help = RowButton(row, "helpbutton", "ctl_cg_btn_help", info);
            help.Visible = row.Selected = key == selected;
            row.Pressed += () =>
            {
                selected = key;
                foreach (var l in new[] { available, known })
                    foreach (var r in l?.Rows.OfType<CgButton>() ?? Enumerable.Empty<CgButton>())
                    {
                        r.Selected = r == row;
                        foreach (var h in r.GetChildren().OfType<CgButton>().Where(c => c.Name == "ctl_cg_btn_help")) h.Visible = r == row;
                    }
            };
            var a = RowButton(row, "addremovebutton", add ? "ctl_cg_btn_right" : "ctl_cg_btn_left", arrow);
            a.Disabled = greyed;
            return row;
        }
        void Build()
        {
            if (Model == null || available == null || known == null) return;
            var plan = Plan;
            int left = FeatsLeft();
            if (remaining != null) remaining.Text = left.ToString();
            int topA = available.Top, topK = known.Top;
            available.Clear();
            known.Clear();
            if (plan != null && left > 0)
            {
                // Choosable feats not yet taken, by name; a master feat's choices share one row (its popup lists them).
                var options = plan.GeneralChoices.Union(plan.BonusChoices).Where(f => !Choice.Feats.Contains(f)).ToList();
                var groups = options.GroupBy(f => Model.Feats.GetValueOrDefault(f)?.Int("master", -1) ?? -1).ToList();
                var entries = new List<(string Name, Action Add, Action Info, string Icon)>();
                foreach (var g in groups)
                {
                    if (g.Key >= 0 && Model.MasterFeats.TryGetValue(g.Key, out var master) && g.Count() > 1)
                    {
                        var subs = g.ToList();
                        entries.Add((master.Name, () => MasterPopup(master, subs), () => ShowInfo("masterfeat", master.Id, master.Icon), master.Icon));
                    }
                    else foreach (var f in g)
                        {
                            int id = f;
                            entries.Add((Model.FeatName(f), () => { Choice.Feats.Add(id); Validate(); Build(); }, () => ShowInfo("feat", id, Icon(id)), Icon(f)));
                        }
                }
                foreach (var (name, add, info, icon) in entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase))
                    available.Add(FeatRow(-1, name, icon, true, false, add, info));
            }
            // Gained automatically (greyed, by name), then the chosen ones in the order taken (c31).
            if (plan != null)
                foreach (var f in plan.Granted.OrderBy(Model.FeatName))
                {
                    int id = f;
                    known.Add(FeatRow(f, Model.FeatName(f), Icon(f), false, true, () => { }, () => ShowInfo("feat", id, Icon(id))));
                }
            foreach (var f in Choice.Feats)
            {
                int id = f;
                known.Add(FeatRow(f, Model.FeatName(f), Icon(f), false, false, () => { Choice.Feats.Remove(id); Validate(); Build(); }, () => ShowInfo("feat", id, Icon(id))));
            }
            available.ScrollTo(topA);
            known.ScrollTo(topK);
            Enable("OkButton", FeatsDone());
        }
        Build();
        RefreshScreen = Build;
    }

    /// <summary>A master feat's choices (cg_feats_popup): pick one, OK takes it.</summary>
    void MasterPopup(ChargenEntry master, List<int> subs)
    {
        var p = ShowPopup("cg_feats_popup");
        if (p == null || Model == null) return;
        if (p.Get<GuiLabel>("TGuiLabel") is { } t) t.Text = master.Name;
        var list = CgList.Attach(p, "FeatsList");
        int picked = -1;
        var rows = new List<CgButton>();
        foreach (var f in subs.OrderBy(Model.FeatName))
        {
            int id = f;
            var row = CgButton.Make(A, "ctl_cg_btn_feat", Model.FeatName(f), ChargenModel.Icon(A.Content, Model.Feats.GetValueOrDefault(f)?.Icon ?? ""));
            row.Pressed += () => { picked = id; foreach (var r in rows) r.Selected = r == row; };
            rows.Add(row);
            list?.Add(row);
        }
        if (p.Get<GuiButton>("OkButton") is { } ok) ok.Pressed += () =>
        {
            if (picked < 0) return;
            ClosePopup();
            Choice.Feats.Add(picked);
            Validate();
            RefreshScreen?.Invoke();
        };
        if (p.Get<GuiButton>("CancelButton") is { } cancel) cancel.Pressed += ClosePopup;
    }

    protected int FeatsLeft()
    {
        var plan = Plan;
        return plan == null ? 0 : Math.Max(0, plan.GeneralSlots + plan.BonusSlots - Choice.Feats.Count);
    }

    /// <summary>OK is open once every feat slot is used, or nothing is left to take (c27: greyed with one feat left; c30: open).</summary>
    protected bool FeatsDone()
    {
        var plan = Plan;
        if (plan == null) return false;
        if (FeatsLeft() == 0) return !plan.ErrorsFor("feats", "feat-prereq").Any();
        return !plan.GeneralChoices.Union(plan.BonusChoices).Any(f => !Choice.Feats.Contains(f));
    }

    // ------------------------------------------------------------------ school of magic

    static readonly string[] SchoolTags = { "General", "Abjuration", "Conjuration", "Divination", "Enchantment", "Evocation", "Illusion", "Necromancy", "Transmutation" };

    void OpenSchool()
    {
        if (ShowScreen("cg_school", Title(A.Tlk(381))) is not { } p) return;
        SetHelp(A.Tlk(381), A.Tlk(381));
        WireStepButtons(() => Choice.School >= 0);
        On("RecommendButton", () => Recommend("school"));
        var buttons = new Dictionary<int, CgButton>();
        for (int i = 0; i < SchoolTags.Length; i++)
        {
            if (p.Get<GuiButton>(SchoolTags[i]) is not { } b) continue;
            int id = i;
            var c = CgButton.From(b);
            if (Model?.Schools.GetValueOrDefault(id) is { } s) c.Caption = s.Name;
            c.Pressed += () =>
            {
                Choice.School = id;
                if (Model?.Schools.GetValueOrDefault(id) is { } sc) SetHelp(sc.Name, sc.Description);
                Validate();
                RefreshScreen?.Invoke();
            };
            buttons[id] = c;
        }
        // The package's school is chosen when the screen opens (c32: General for the generalist).
        if (Choice.School < 0) Choice.School = PackageSchool();
        RefreshScreen = () => { foreach (var (id, b) in buttons) b.Selected = id == Choice.School; Enable("OkButton", Choice.School >= 0); };
        RefreshScreen();
        Validate();
    }

    /// <summary>The school the class package names (packages.2da School), else General.</summary>
    protected virtual int PackageSchool() => 0;

    // ------------------------------------------------------------------ domains

    void OpenDomains()
    {
        if (ShowScreen("cg_domain", Title(A.Tlk(5982))) is not { } p) return;
        WireStepButtons(() => Choice.Domain1 >= 0 && Choice.Domain2 >= 0);
        On("RecommendButton", () => Recommend("domains"));
        var list = CgList.Attach(p, "DomainListBox");
        // The description box: its name label is the help title, the text in text0..text1, the two chosen domains at
        // its domain1 / domain2 nodes (l16, l17).
        CgText? body = null;
        GuiLabel? name = p.Get<GuiLabel>("DomainName");
        if (name != null) name.Text = "";
        Vector2 slot1 = default, slot2 = default;
        var chosenHost = new Control { MouseFilter = MouseFilterEnum.Pass };
        p.AddChild(chosenHost);
        if (p.Get<GuiList>("DomainDesc") is { Frame: { } frame } desc)
        {
            desc.Bar.Visible = false;
            foreach (var c in desc.GetChildren().OfType<Control>()) if (c != frame) c.Visible = false;
            var text = frame.Node("text0") is { } t0 && frame.Node("text1") is { } t1 ? new Rect2(t0, Vector2.Zero).Expand(t1) : new Rect2(8, 10, 320, 260);
            var bar = CgHelp.BarRect(A, frame, text);
            body = CgText.Create(A, new Rect2(desc.Position + text.Position, text.Size), new Rect2(desc.Position + bar.Position, bar.Size), p);
            body.Justify = false;
            slot1 = desc.Position + (frame.Node("domain1") ?? new Vector2(17, 352));
            slot2 = desc.Position + (frame.Node("domain2") ?? new Vector2(17, 396));
        }
        int shown = -1;
        void ShowDomain(int id)
        {
            shown = id;
            if (Model?.Domains.GetValueOrDefault(id) is not { } d) return;
            if (name != null) name.Text = d.Name;
            body?.Let(b => b.Text = d.Description);
        }
        CgButton Row(int id, bool add)
        {
            var d = Model!.Domains[id];
            var row = CgButton.Make(A, "ctl_cg_btn_dom", d.Name, ChargenModel.Icon(A.Content, d.Icon));
            row.Selected = id == shown;
            row.Pressed += () => { ShowDomain(id); RefreshScreen?.Invoke(); };
            RowButton(row, "addremovebutton", add ? "ctl_cg_btn_right" : "ctl_cg_btn_left", () =>
            {
                if (add)
                {
                    if (Choice.Domain1 < 0) Choice.Domain1 = id;
                    else if (Choice.Domain2 < 0) Choice.Domain2 = id;
                    else return;
                }
                else if (Choice.Domain1 == id) { Choice.Domain1 = Choice.Domain2; Choice.Domain2 = -1; }
                else if (Choice.Domain2 == id) Choice.Domain2 = -1;
                ShowDomain(id);
                Validate();
                RefreshScreen?.Invoke();
            });
            return row;
        }
        RefreshScreen = () =>
        {
            if (Model == null || list == null) return;
            int top = list.Top;
            list.Clear();
            foreach (var d in Model.Domains.Values.Where(d => d.Id != Choice.Domain1 && d.Id != Choice.Domain2).OrderBy(d => d.Name))
                list.Add(Row(d.Id, true));
            list.ScrollTo(top);
            foreach (var c in chosenHost.GetChildren()) c.QueueFree();
            foreach (var (id, at) in new[] { (Choice.Domain1, slot1), (Choice.Domain2, slot2) })
            {
                if (id < 0 || !Model.Domains.ContainsKey(id)) continue;
                var row = Row(id, false);
                row.PlaceOrigin(at);
                chosenHost.AddChild(row);
            }
            Enable("OkButton", Choice.Domain1 >= 0 && Choice.Domain2 >= 0);
        };
        RefreshScreen();
    }

    // ------------------------------------------------------------------ spells

    int _spellLevel = -1;

    void OpenSpells()
    {
        if (ShowScreen("cg_spells", Title(A.Tlk(368))) is not { } p) return;
        WireStepButtons(() => Plan != null && !Plan.ErrorsFor("spells").Any());
        On("RecommendButton", () => Recommend("spells"));
        On("ResetButton", () => { Choice.Spells.Clear(); Validate(); RefreshScreen?.Invoke(); });
        var available = CgList.Attach(p, "AvailBox");
        var known = CgList.Attach(p, "KnownBox");
        var remaining = p.Get<GuiLabel>("RemainLabel");
        var empty = p.Get<GuiButton>("EmptySpellListLabel");
        var cannot = p.Get<GuiButton>("CannotLearn");
        // The labels hang inside the lists (hooked to their models); keep them over the rows, their two and three lines
        // centred (c36).
        if (empty != null) empty = CgButton.From(empty);
        if (cannot != null) cannot = CgButton.From(cannot);
        GuiLabel? levelName = null;
        if (empty != null)
        {
            empty.MouseFilter = MouseFilterEnum.Ignore;
            empty.GetParent().MoveChild(empty, -1);
            levelName = GuiLabel.Create(A, "", CgKit.Font, Colors.White, 0, 0.5f);
            levelName.Position = (available?.Position ?? Vector2.Zero) + new Vector2(50, 78);
            levelName.Size = new Vector2(200, 20);
            p.AddChild(levelName);
        }
        if (cannot != null) { cannot.MouseFilter = MouseFilterEnum.Ignore; cannot.GetParent().MoveChild(cannot, -1); }
        var tabs = new CgButton?[10];
        for (int l = 0; l < 10; l++)
        {
            if (p.Get<GuiButton>("SpellLevel" + l) is not { } b) continue;
            int lvl = l;
            var c = CgButton.From(b);
            c.IconSize = new Vector2(32, 32);
            c.ClickSound = "gui_select";
            c.Pressed += () => { if (Pick() is { } pk && lvl <= TopSpellLevel(pk)) { _spellLevel = lvl; RefreshScreen?.Invoke(); } };
            tabs[l] = c;
        }
        string Icon(int sp) => Model?.Spells.GetValueOrDefault(sp)?.Icon ?? "";
        CgButton SpellRow(int sp, bool add, bool greyed, Action arrow)
        {
            var row = CgButton.Make(A, "ctl_cg_btn_feat", Model!.SpellName(sp), ChargenModel.Icon(A.Content, Icon(sp)));
            row.Greyed = greyed;
            var help = RowButton(row, "helpbutton", "ctl_cg_btn_help", () => ShowInfo("spell", sp, Icon(sp)));
            help.Visible = false;
            row.MouseEntered += () => help.Visible = true;
            row.MouseExited += () => help.Visible = help.IsHovered();
            help.MouseExited += () => help.Visible = row.IsHovered();
            RowButton(row, "addremovebutton", add ? "ctl_cg_btn_right" : "ctl_cg_btn_left", arrow).Disabled = greyed;
            return row;
        }
        RefreshScreen = () =>
        {
            var pick = Pick();
            if (Model == null || available == null || known == null) return;
            if (pick == null) return;
            // The tab opened first is the highest level with spells to learn (l24: II).
            if (_spellLevel < 0 || !pick.Choices.ContainsKey(_spellLevel) && !pick.Granted.ContainsKey(_spellLevel))
                _spellLevel = Enumerable.Range(0, 10).Where(l => pick.PerLevel.GetValueOrDefault(l) > 0 || pick.AnyLevel > 0 && pick.Choices.ContainsKey(l)).DefaultIfEmpty(pick.Choices.Keys.DefaultIfEmpty(0).Max()).Max();
            // A tab for every level the class can cast, cantrips included, whether or not it has spells to learn
            // (l24: 0, I and II for a wizard 3).
            int top = TopSpellLevel(pick);
            for (int l = 0; l < 10; l++)
            {
                if (tabs[l] is not { } t) continue;
                bool has = l <= top;
                t.Icon = has ? A.Texture(l == 0 ? "ir_cantrips" : l <= 6 ? "ir_level" + l : "ir_level789") : null;
                t.Selected = false;
                t.Modulate = has && l != _spellLevel ? new Color(0.62f, 0.62f, 0.62f) : Colors.White;
            }
            int picked = Choice.Spells.Values.Sum(x => x.Count);
            int needed = pick.AnyLevel > 0 ? pick.AnyLevel : pick.PerLevel.Values.Sum();
            int leftTotal = Math.Max(0, needed - picked);
            int leftHere = pick.AnyLevel > 0 ? leftTotal : Math.Max(0, pick.PerLevel.GetValueOrDefault(_spellLevel) - (Choice.Spells.GetValueOrDefault(_spellLevel)?.Count ?? 0));
            if (remaining != null) remaining.Text = leftTotal.ToString();
            int topA = available.Top, topK = known.Top;
            available.Clear();
            known.Clear();
            var chosenHere = Choice.Spells.GetValueOrDefault(_spellLevel) ?? new List<int>();
            // The spells still to be had stay listed when none are left to choose (c35); their arrows then do nothing.
            foreach (var sp in (pick.Choices.GetValueOrDefault(_spellLevel) ?? new List<int>()).Where(s => !chosenHere.Contains(s)).OrderBy(Model.SpellName))
            {
                int id = sp, lvl = _spellLevel;
                available.Add(SpellRow(sp, true, false, () =>
                {
                    int room = pick.AnyLevel > 0 ? pick.AnyLevel - Choice.Spells.Values.Sum(x => x.Count) : pick.PerLevel.GetValueOrDefault(lvl) - (Choice.Spells.GetValueOrDefault(lvl)?.Count ?? 0);
                    if (room <= 0) return;
                    var l = Choice.Spells.TryGetValue(lvl, out var e) ? e : Choice.Spells[lvl] = new List<int>();
                    if (!l.Contains(id)) l.Add(id);
                    Validate();
                    RefreshScreen?.Invoke();
                }));
            }
            bool none = available.Rows.Count == 0;
            if (empty != null) empty.Visible = none;
            if (levelName != null) { levelName.Visible = none; levelName.Text = _spellLevel == 0 ? A.Tlk(691) : A.Tlk((uint)(61924 + _spellLevel - 1)); }
            foreach (var sp in (pick.Granted.GetValueOrDefault(_spellLevel) ?? new List<int>()).OrderBy(Model.SpellName))
                known.Add(SpellRow(sp, false, true, () => { }));
            // Chosen spells by name (c35).
            foreach (var sp in chosenHere.OrderBy(Model.SpellName))
            {
                int id = sp, lvl = _spellLevel;
                known.Add(SpellRow(sp, false, false, () => { Choice.Spells.GetValueOrDefault(lvl)?.Remove(id); Validate(); RefreshScreen?.Invoke(); }));
            }
            available.ScrollTo(topA);
            known.ScrollTo(topK);
            if (cannot != null) cannot.Visible = false;
            Enable("OkButton", Plan != null && !Plan.ErrorsFor("spells").Any() && leftTotal == 0);
        };
        _spellLevel = -1;
        RefreshScreen();
    }

    LevelPlanView.SpellPickView? Pick() => Plan?.Spells.FirstOrDefault(s => s.Needed || s.Granted.Count > 0);

    static int TopSpellLevel(LevelPlanView.SpellPickView pick) =>
        new[] { pick.MaxLevel }.Concat(pick.Choices.Keys).Concat(pick.Granted.Keys).Concat(pick.PerLevel.Keys).DefaultIfEmpty(0).Max();

    // ------------------------------------------------------------------ familiar / animal companion

    void OpenAssociate(bool companion)
    {
        string table = companion ? "hen_companion" : "hen_familiar";
        if (ShowScreen("cg_familiar", Title(companion ? A.Tlk(1933) : A.Tlk(5607))) is not { } p || Model == null) return;
        var entries = companion ? Model.Companions : Model.Familiars;
        WireStepButtons();
        var list = CgList.Attach(p, "FamListBox");
        // The record's combat box ends 197 px below the panel's origin, 10 above OK (l04).
        var record = CgRecord.Create(A, p.Node("CharacterSheet") is { } at ? at : p.ModelOrigin + new Vector2(9, -221), p.ModelOrigin.Y + 197);
        record.ShowCombat = true;
        // The server sends the original's fixed saves for a familiar (-1 each, no class saves: FamiliarRecordTests).
        record.CreationSaves = true;
        p.AddChild(record);
        // The name box: an edit over the layout's box (the original generates a name when the screen opens: c37).
        LineEdit? edit = null;
        if (p.Get<GuiList>("NameEditBox") is { } box)
        {
            box.Bar.Visible = false;
            foreach (var c in box.GetChildren().OfType<Control>()) if (c != box.Frame) c.Visible = false;
            edit = CgEdit.Over(A, p, box, 32);
            edit.TextChanged += t => { if (companion) Choice.CompanionName = t; else Choice.FamiliarName = t; };
        }
        string Generated() => CgNames.Random(A.Content, companion ? "animal" : "familiar");
        On("GenNameButton", () => { if (edit != null) { edit.Text = Generated(); if (companion) Choice.CompanionName = edit.Text; else Choice.FamiliarName = edit.Text; } });
        int Current() => companion ? Choice.Companion : Choice.Familiar;
        void Set(int id)
        {
            if (companion) Choice.Companion = id; else Choice.Familiar = id;
            Session.Request("chargen-associate", new { table, id, level = AssociateLevel });
            Validate();
            RefreshScreen?.Invoke();
        }
        if (Current() < 0) { if (companion) Choice.Companion = DefaultAssociate(true); else Choice.Familiar = DefaultAssociate(false); }
        string name = companion ? Choice.CompanionName : Choice.FamiliarName;
        if (name.Length == 0) { name = CurrentAssociateName(companion); if (name.Length == 0) name = Generated(); }
        if (edit != null) edit.Text = name;
        if (companion) Choice.CompanionName = name; else Choice.FamiliarName = name;
        var rows = new Dictionary<int, CgButton>();
        var helps = new List<CgButton>();
        // hen_familiar / hen_companion row order (c37: Bat, Panther, Hell Hound, Imp ...).
        foreach (var e in entries.Values.OrderBy(e => e.Id))
        {
            int id = e.Id;
            string description = e.Description;
            var row = CgButton.Make(A, "ctl_cg_btn_famil", e.Name);
            row.Pressed += () => Set(id);
            // The row under the pointer shows its ? (helpbutton), which stays on the last row hovered; pressing it selects
            // the row and opens the creature's description in a scrolling message box (level-up).
            var help = RowButton(row, "helpbutton", "ctl_cg_btn_help", () =>
            {
                Set(id);
                ClassicPopup.OkScroll(this, A, CgKit.Clean(description));
            });
            help.Visible = false;
            helps.Add(help);
            row.MouseEntered += () => { foreach (var h in helps) h.Visible = h == help; };
            rows[id] = row;
            list?.Add(row);
        }
        Session.Request("chargen-associate", new { table, id = Current(), level = AssociateLevel });
        RefreshScreen = () =>
        {
            foreach (var (id, r) in rows) r.Selected = id == Current();
            var rec = AssociateRecord(table, Current(), AssociateLevel);
            record.Identity = rec is { } x && x.TryGetProperty("sheet", out var sh) ? AssociateIdentity(sh) : Array.Empty<string>();
            record.Fill(rec, "");
        };
        RefreshScreen();
    }

    static string[] AssociateIdentity(JsonElement sheet)
    {
        string S(string n) => sheet.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? NwnText.Strip(v.GetString() ?? "") : "";
        var lines = new List<string> { $"{S("race")}, {S("alignment")}" };
        if (sheet.TryGetProperty("classes", out var cls))
            foreach (var c in cls.EnumerateArray()) lines.Add($"{NwnText.Strip(c.GetProperty("name").GetString() ?? "")} ({c.GetProperty("level").GetInt32()})");
        return lines.ToArray();
    }

    /// <summary>The associate a screen opens with: the one the character has, else the package's.</summary>
    protected abstract int DefaultAssociate(bool companion);
    protected virtual string CurrentAssociateName(bool companion) => "";
}

static class ObjectLet
{
    public static void Let<T>(this T o, Action<T> a) where T : class => a(o);
}
