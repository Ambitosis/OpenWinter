using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using OpenWinter.Game.Progression;
using Godot;

namespace OpenWinter.Client.UI.CharGen;

/// <summary>
/// The level steps shared by character creation and level-up: skills, feats, spells and the
/// first-level class options (domains, school, familiar, companion). Each builds its controls from
/// the server's plan and edits the <see cref="LevelChoice"/>; <see cref="Changed"/> asks the owner
/// to fetch a new plan, <see cref="Describe"/> to show an entry's description. The Recommended
/// action lives in the window's footer (<see cref="WizardFrame.Recommend"/>).
/// </summary>
public sealed class LevelSteps
{
    public required ChargenModel Model;
    public required ClientContent? Content;
    public required Func<LevelChoice> Choice;
    public required Func<LevelPlanView?> Plan;
    public Action? Changed;
    public Action<string, int>? Describe;
    /// <summary>Asks the server to fill one part ("skills", "feats", "spells") from the class package.</summary>
    public Action<string>? Recommend;

    static Control Waiting() => Ui.Text("Waiting for the server…", Ui.TextStyle.Secondary);

    /// <summary>The budget banner at the top of a step: a label, a big remaining count and a note.</summary>
    static Control Budget(string label, int left, int total, string note, bool done)
    {
        var card = new PanelContainer();
        card.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.Radius, done ? Palette.Success with { A = 0.5f } : Palette.Line, 1, 16, 10));
        var row = Ui.HBox(12);
        card.AddChild(row);
        var names = Ui.VBox(0);
        names.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        names.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        names.AddChild(Ui.Text(label.ToUpperInvariant(), Ui.TextStyle.Caption, Palette.TextSecondary));
        if (note.Length > 0)
        {
            var n = Ui.Text(note, Ui.TextStyle.Small);
            n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            n.CustomMinimumSize = new Vector2(300, 0);
            names.AddChild(n);
        }
        row.AddChild(names);
        row.AddChild(Ui.Text(left.ToString(), Ui.TextStyle.Display, done ? Palette.Success : left < 0 ? Palette.Danger : Palette.AccentBright, 32));
        var of = Ui.Text($"/ {total}", Ui.TextStyle.Secondary);
        of.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(of);
        return card;
    }

    static Button Round(string text, bool disabled, Action pressed, string tooltip)
    {
        var b = new Button { Text = text, Disabled = disabled, CustomMinimumSize = new Vector2(30, 30), FocusMode = Control.FocusModeEnum.None, TooltipText = tooltip, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        b.AddThemeStyleboxOverride("normal", Styles.Box(new Color(0, 0, 0, 0.3f), 15, Palette.LineStrong, 1, 0, 0));
        b.AddThemeStyleboxOverride("hover", Styles.Box(Palette.Bg4, 15, Palette.Accent, 1, 0, 0));
        b.AddThemeStyleboxOverride("pressed", Styles.Box(Palette.AccentDim, 15, Palette.Accent, 1, 0, 0));
        b.AddThemeStyleboxOverride("disabled", Styles.Box(Colors.Transparent, 15, Palette.Line, 1, 0, 0));
        b.AddThemeFontOverride("font", Fonts.UiBold);
        b.AddThemeFontSizeOverride("font_size", 16);
        b.Pressed += pressed;
        return b;
    }

    // ------------------------------------------------------------------ skills

    public Control Skills()
    {
        var box = Ui.VBox(10);
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var plan = Plan();
        if (plan == null) { box.AddChild(Waiting()); return box; }
        var ch = Choice();
        box.AddChild(Budget("Skill points", plan.SkillLeft, plan.SkillAvailable,
            plan.SkillCarried > 0 ? $"Includes {plan.SkillCarried} saved from earlier levels." : "Unspent points are saved for your next level.", plan.SkillLeft == 0));
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var list = Ui.VBox(2);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(Ui.Pad(list, 0, 0, 12, 0));
        ((MarginContainer)list.GetParent()).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        bool crossHeader = false;
        list.AddChild(Ui.Section("Class skills"));
        foreach (var s in plan.Skills.OrderBy(s => !s.ClassSkill).ThenBy(s => Model.SkillName(s.Id)))
        {
            if (Model.Skills.TryGetValue(s.Id, out var info) && info.Bool("hidden")) continue;
            if (!s.ClassSkill && !crossHeader) { crossHeader = true; list.AddChild(Ui.Section("Cross-class skills  ·  2 points per rank")); }
            int added = ch.SkillRanks(s.Id);
            int id = s.Id;
            var row = new TipPanel { MouseFilter = Control.MouseFilterEnum.Stop };
            var normal = Styles.Box(added > 0 ? Palette.Accent with { A = 0.07f } : Colors.Transparent, Palette.RadiusSmall, null, 0, 8, 3);
            row.AddThemeStyleboxOverride("panel", normal);
            row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", Styles.Box(new Color(1, 1, 1, 0.05f), Palette.RadiusSmall, null, 0, 8, 3));
            row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normal);
            row.GuiInput += e => { if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Describe?.Invoke("skill", id); };
            var h = Ui.HBox(10);
            h.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(h);
            var icon = UiTheme.Icon(Content!, new[] { info?.Icon ?? "" }, size: 28);
            h.AddChild(icon);
            var name = Ui.Text(Model.SkillName(s.Id), Ui.TextStyle.Body, s.ClassSkill ? Palette.Text : Palette.TextSecondary, Palette.FontSmall + 1);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            name.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            name.ClipText = true;
            h.AddChild(name);
            if (info?.Str("ability") is { Length: > 0 } ab) h.AddChild(Chip.Create(ab.ToUpperInvariant(), Palette.TextSecondary, fontSize: 10));
            var minus = Round("−", added <= 0, () => { ch.Skills[id] = Math.Max(0, ch.SkillRanks(id) - 1); if (ch.Skills[id] == 0) ch.Skills.Remove(id); Changed?.Invoke(); }, "Remove a rank");
            h.AddChild(minus);
            var ranks = Ui.Text(added > 0 ? $"{s.Current}+{added}" : s.Current.ToString(), Ui.TextStyle.Number, added > 0 ? Palette.AccentBright : Palette.Text, Palette.FontLarge);
            ranks.CustomMinimumSize = new Vector2(48, 0);
            ranks.HorizontalAlignment = HorizontalAlignment.Center;
            ranks.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            h.AddChild(ranks);
            bool atMax = s.Current + added >= s.Max;
            var plus = Round("+", atMax || plan.SkillLeft < s.Cost, () => { ch.Skills[id] = ch.SkillRanks(id) + 1; Changed?.Invoke(); },
                atMax ? $"At the maximum of {s.Max} ranks" : $"Add a rank ({s.Cost} point{(s.Cost == 1 ? "" : "s")})");
            h.AddChild(plus);
            var max = Ui.Text($"max {s.Max}", Ui.TextStyle.Caption);
            max.CustomMinimumSize = new Vector2(46, 0);
            max.HorizontalAlignment = HorizontalAlignment.Right;
            max.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            h.AddChild(max);
            list.AddChild(row);
        }
        return box;
    }

    // ------------------------------------------------------------------ feats

    public Control Feats()
    {
        var box = Ui.VBox(10);
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var plan = Plan();
        if (plan == null) { box.AddChild(Waiting()); return box; }
        var ch = Choice();
        int slots = plan.GeneralSlots + plan.BonusSlots;
        string note = $"{plan.GeneralSlots} general" + (plan.BonusSlots > 0 ? $"  ·  {plan.BonusSlots} {Model.ClassName(plan.Class)} bonus" : "");
        box.AddChild(Budget("Feats to choose", slots - ch.Feats.Count, slots, note, slots == 0 || ch.Feats.Count >= slots));
        if (plan.Granted.Count > 0)
        {
            var granted = new HFlowContainer();
            granted.AddThemeConstantOverride("h_separation", 6);
            granted.AddThemeConstantOverride("v_separation", 6);
            var cap = Ui.Text("Gained automatically", Ui.TextStyle.Caption);
            cap.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            granted.AddChild(cap);
            foreach (var f in plan.Granted)
            {
                int id = f;
                var chip = new TipPanel { MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
                chip.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.RadiusSmall, Palette.Line, 1, 6, 2));
                var h = Ui.HBox(6);
                h.MouseFilter = Control.MouseFilterEnum.Ignore;
                if (ChargenModel.Icon(Content, Model.Feats.GetValueOrDefault(f)?.Icon ?? "") is { } t)
                    h.AddChild(new TextureRect { Texture = t, CustomMinimumSize = new Vector2(20, 20), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore });
                h.AddChild(Ui.Text(Model.FeatName(f), Ui.TextStyle.Small, Palette.Text));
                chip.AddChild(h);
                chip.GuiInput += e => { if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Describe?.Invoke("feat", id); };
                granted.AddChild(chip);
            }
            box.AddChild(granted);
        }
        var filter = new LineEdit { PlaceholderText = "Search feats", ClearButtonEnabled = true };
        box.AddChild(filter);
        var lists = Ui.HBox(10);
        lists.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        box.AddChild(lists);
        var available = new ItemList { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FixedIconSize = new Vector2I(30, 30) };
        var chosen = new ItemList { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FixedIconSize = new Vector2I(30, 30) };
        foreach (var l in new[] { available, chosen }) l.AddThemeFontSizeOverride("font_size", Palette.FontSmall + 1);
        var left = Ui.VBox(6);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        left.AddChild(Ui.Section("Available"));
        left.AddChild(available);
        var mid = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        mid.AddThemeConstantOverride("separation", 8);
        var right = Ui.VBox(6);
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.AddChild(Ui.Section($"Chosen  ·  {ch.Feats.Count} of {slots}"));
        right.AddChild(chosen);
        lists.AddChild(left);
        lists.AddChild(mid);
        lists.AddChild(right);

        var options = plan.GeneralChoices.Union(plan.BonusChoices).Where(f => !ch.Feats.Contains(f)).OrderBy(Model.FeatName).ToList();
        var shown = new List<int>();
        void FillAvailable()
        {
            available.Clear();
            shown.Clear();
            string q = filter.Text.Trim();
            foreach (var f in options)
            {
                var name = Model.FeatName(f);
                if (q.Length > 0 && !name.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                bool bonusOnly = !plan.GeneralChoices.Contains(f);
                int i = available.AddItem(name + (bonusOnly ? "  · bonus" : ""), ChargenModel.Icon(Content, Model.Feats.GetValueOrDefault(f)?.Icon ?? ""));
                if (bonusOnly) available.SetItemCustomFgColor(i, Palette.Accent);
                available.SetItemTooltip(i, bonusOnly ? $"{name}: only as a {Model.ClassName(plan.Class)} bonus feat" : $"{name}: double-click to take it");
                shown.Add(f);
            }
        }
        FillAvailable();
        filter.TextChanged += _ => FillAvailable();
        foreach (var f in ch.Feats)
        {
            int i = chosen.AddItem(Model.FeatName(f), ChargenModel.Icon(Content, Model.Feats.GetValueOrDefault(f)?.Icon ?? ""));
            chosen.SetItemTooltip(i, "Double-click to remove");
        }
        void Add()
        {
            var sel = available.GetSelectedItems();
            if (sel.Length == 0 || ch.Feats.Count >= slots) return;
            ch.Feats.Add(shown[sel[0]]);
            Changed?.Invoke();
        }
        void Remove()
        {
            var sel = chosen.GetSelectedItems();
            if (sel.Length == 0) return;
            ch.Feats.RemoveAt(sel[0]);
            Changed?.Invoke();
        }
        available.ItemSelected += i => Describe?.Invoke("feat", shown[(int)i]);
        available.ItemActivated += _ => Add();
        chosen.ItemSelected += i => Describe?.Invoke("feat", ch.Feats[(int)i]);
        chosen.ItemActivated += _ => Remove();
        var add = Ui.Button("Add", Add, "", null);
        add.CustomMinimumSize = new Vector2(96, 34);
        add.TooltipText = "Take the selected feat";
        add.Disabled = ch.Feats.Count >= slots;
        mid.AddChild(add);
        var remove = Ui.Button("Remove", Remove, "GhostButton", null);
        remove.CustomMinimumSize = new Vector2(96, 34);
        remove.TooltipText = "Give back the selected chosen feat";
        mid.AddChild(remove);
        return box;
    }

    // ------------------------------------------------------------------ spells

    public Control Spells()
    {
        var box = Ui.VBox(10);
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        var plan = Plan();
        if (plan == null) { box.AddChild(Waiting()); return box; }
        var ch = Choice();
        var pick = plan.Spells.FirstOrDefault(p => p.Needed);
        if (pick == null) { box.AddChild(Ui.Text("No new spells at this level.", Ui.TextStyle.Secondary)); return box; }
        int picked = ch.Spells.Values.Sum(l => l.Count);
        int needed = pick.AnyLevel > 0 ? pick.AnyLevel : pick.PerLevel.Values.Sum();
        string note = pick.AnyLevel > 0
            ? $"Add spells of level {(pick.MaxLevel <= 0 ? "0" : $"0 to {pick.MaxLevel}")} to your spellbook."
            : string.Join("  ·  ", pick.PerLevel.Where(k => k.Value > 0).Select(k => $"{(k.Key == 0 ? "Cantrips" : $"Level {k.Key}")}: {ch.Spells.GetValueOrDefault(k.Key)?.Count ?? 0} of {k.Value}"));
        box.AddChild(Budget("Spells to learn", needed - picked, needed, note, !plan.ErrorsFor("spells").Any()));
        foreach (var (lvl, list) in pick.Granted)
        {
            var g = Ui.Text($"Learnt automatically ({(lvl == 0 ? "cantrips" : $"level {lvl}")}): " + string.Join(", ", list.Select(Model.SpellName)), Ui.TextStyle.Small);
            g.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            g.CustomMinimumSize = new Vector2(400, 0);
            box.AddChild(g);
        }
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(scroll);
        var content = Ui.VBox(10);
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(Ui.Pad(content, 2, 2, 12, 2));
        ((MarginContainer)content.GetParent()).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foreach (var (lvl, options) in pick.Choices.OrderBy(k => k.Key))
        {
            if (pick.AnyLevel == 0 && pick.PerLevel.GetValueOrDefault(lvl) == 0) continue;
            content.AddChild(Ui.Section(lvl == 0 ? "Cantrips" : $"Level {lvl}"));
            var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            grid.AddThemeConstantOverride("h_separation", 8);
            grid.AddThemeConstantOverride("v_separation", 8);
            content.AddChild(grid);
            foreach (var sp in options.OrderBy(Model.SpellName))
            {
                bool on = ch.Spells.GetValueOrDefault(lvl)?.Contains(sp) == true;
                var card = SelectCard.Create(Model.SpellName(sp), null, ChargenModel.Icon(Content, Model.Spells.GetValueOrDefault(sp)?.Icon ?? ""), 32, pad: 8);
                card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                card.Selected = on;
                if (on) card.Trailing.AddChild(Chip.Create("Learnt", Palette.Accent, solid: true, fontSize: 10));
                int level = lvl, spell = sp;
                card.MouseEntered += () => Describe?.Invoke("spell", spell);
                card.Pressed += () => { Describe?.Invoke("spell", spell); Toggle((level, spell)); };
                grid.AddChild(card);
            }
        }
        void Toggle((int Level, int Spell) r)
        {
            var l = ch.Spells.TryGetValue(r.Level, out var existing) ? existing : ch.Spells[r.Level] = new List<int>();
            if (!l.Remove(r.Spell))
            {
                int limit = pick.AnyLevel > 0 ? pick.AnyLevel - ch.Spells.Values.Sum(x => x.Count) : pick.PerLevel.GetValueOrDefault(r.Level) - l.Count;
                if (limit <= 0) return;
                l.Add(r.Spell);
            }
            if (l.Count == 0) ch.Spells.Remove(r.Level);
            Changed?.Invoke();
        }
        return box;
    }

    // ------------------------------------------------------------------ class options

    public Control Options()
    {
        var box = Ui.VBox(12);
        var plan = Plan();
        if (plan == null) return box;
        var ch = Choice();
        if (plan.PickDomains)
        {
            box.AddChild(Ui.Section("Domains  ·  choose two"));
            var row = Ui.HBox(12);
            row.AddChild(Picker(Model.Domains, ch.Domain1, v => { ch.Domain1 = v; Changed?.Invoke(); }, "domain"));
            row.AddChild(Picker(Model.Domains, ch.Domain2, v => { ch.Domain2 = v; Changed?.Invoke(); }, "domain"));
            box.AddChild(row);
        }
        if (plan.PickSchool)
        {
            box.AddChild(Ui.Section("School of magic"));
            box.AddChild(Picker(Model.Schools, ch.School, v => { ch.School = v; Changed?.Invoke(); }, "school"));
        }
        if (plan.PickFamiliar) AssociateRow(box, "Familiar", Model.Familiars, ch.Familiar, v => ch.Familiar = v, ch.FamiliarName, n => ch.FamiliarName = n);
        if (plan.PickCompanion) AssociateRow(box, "Animal companion", Model.Companions, ch.Companion, v => ch.Companion = v, ch.CompanionName, n => ch.CompanionName = n);
        if (!plan.HasOptions) box.AddChild(Ui.Text("No class options at this level.", Ui.TextStyle.Secondary));
        return box;
    }

    /// <summary>
    /// A familiar or animal companion: the choices as cards on the left, a turnable 3D preview of the
    /// chosen creature (its level-1 blueprint) on the right, and its name.
    /// </summary>
    void AssociateRow(VBoxContainer box, string title, Dictionary<int, ChargenEntry> table, int value, Action<int> set, string name, Action<string> setName)
    {
        box.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        box.AddChild(Ui.Section(title));
        var row = Ui.HBox(14);
        row.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        box.AddChild(row);

        var left = Ui.VBox(8);
        left.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(left);
        var nameRow = Ui.HBox(10);
        var nameLabel = Ui.Text("Name", Ui.TextStyle.Small);
        nameLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        nameRow.AddChild(nameLabel);
        var edit = new LineEdit { Text = name, PlaceholderText = "Optional", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MaxLength = 32 };
        edit.TextChanged += t => setName(t);
        nameRow.AddChild(edit);
        left.AddChild(nameRow);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 150) };
        left.AddChild(scroll);
        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(Ui.Pad(grid, 0, 0, 10, 0));
        ((MarginContainer)grid.GetParent()).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        foreach (var e in table.Values.OrderBy(e => e.Name))
        {
            var card = SelectCard.Create(e.Name, null, ChargenModel.Icon(Content, e.Icon), 28, pad: 10);
            card.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            card.Selected = e.Id == value;
            if (ChargenModel.AssociateLook(e) == null) card.Trailing.AddChild(Chip.Create("No model", Palette.TextMuted, fontSize: 10));
            var entry = e;
            card.Pressed += () => { set(entry.Id); Changed?.Invoke(); };
            if (e.Description.Length > 0) card.Tooltip = () => Tip.Card(entry.Name, title, Tip.Trim(NwnText.Strip(entry.Description), 420), width: 320);
            grid.AddChild(card);
        }

        // The preview.
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(250, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill, ClipContents = true };
        frame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, Palette.Radius, Palette.AccentDim with { A = 0.6f }, 1, 0));
        row.AddChild(frame);
        var chosen = table.GetValueOrDefault(value);
        var stage = PreviewStage.Create(Content);
        frame.AddChild(stage);
        if (ChargenModel.AssociateLook(chosen) is { } look)
        {
            stage.Preview.Show(look);
            stage.SetCaption(name.Trim().Length > 0 ? name.Trim() : chosen!.Name, name.Trim().Length > 0 ? chosen!.Name : title);
            edit.TextChanged += t => stage.SetCaption(t.Trim().Length > 0 ? t.Trim() : chosen!.Name, t.Trim().Length > 0 ? chosen!.Name : title);
        }
        else
        {
            var hint = Ui.Text(chosen == null ? $"Choose your {title.ToLowerInvariant()} to see it here." : "This module has no model for it.", Ui.TextStyle.Secondary);
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.CustomMinimumSize = new Vector2(200, 0);
            var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            center.AddChild(hint);
            stage.AddChild(center);
        }
    }

    OptionButton Picker(Dictionary<int, ChargenEntry> table, int value, Action<int> set, string? describeKind)
    {
        var o = new OptionButton { CustomMinimumSize = new Vector2(240, 38), FocusMode = Control.FocusModeEnum.None, FitToLongestItem = false, ClipText = true };
        o.AddItem("Choose…", -1);
        foreach (var e in table.Values.OrderBy(e => e.Name))
        {
            o.AddItem(e.Name, e.Id);
            if (ChargenModel.Icon(Content, e.Icon) is { } icon) o.SetItemIcon(o.ItemCount - 1, icon);
            if (e.Id == value) o.Select(o.ItemCount - 1);
        }
        o.AddThemeConstantOverride("icon_max_width", 24);
        o.ItemSelected += i =>
        {
            int id = o.GetItemId((int)i);
            if (describeKind != null && id >= 0) Describe?.Invoke(describeKind, id);
            set(id);
        };
        return o;
    }
}
