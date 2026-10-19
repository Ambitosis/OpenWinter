using System.Text.Json;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's item examine window (examine_item): the item's icon at the panel's "Icon" node, its name, and
/// a scrolling description (type, properties, requirements, then the module's text). Identify is available in a
/// store that identifies (store-identify) for an unidentified item of yours. Shown for "examine" replies about
/// items; creatures and objects stay with the character sheets.
/// </summary>
public partial class ClassicExamineItem : ClassicWindow
{
    public ClassicItems Items = null!;
    GuiLabel? _name;
    GuiList? _text;
    GuiButton? _identify;
    ClassicItemArt.ItemPicture _icon = null!;
    uint _item;
    bool _identified = true;

    protected override GuiPanel Build()
    {
        Anchor = LayoutPreset.Center;
        var p = GuiPanel.FromLayout(Assets, "examine_item") ?? GuiPanel.FromModel(Assets, "pnl_exam_item");
        _name = p.Get<GuiLabel>("Name");
        if (_name != null) _name.Size = new Vector2(p.Size.X - (_name.Position.X * 2), _name.Size.Y);
        _text = p.Get<GuiList>("Description");
        // As the original (examinescroll): arrows and the ridged thumb always, the thumb filling the track when it all fits.
        _text?.UseClassicScrollBar();
        _identify = p.Get<GuiButton>("IdentifyButton");
        _icon = new ClassicItemArt.ItemPicture { Assets = Assets, MouseFilter = MouseFilterEnum.Ignore, Name = "Icon" };
        p.AddChild(_icon);
        _iconCentre = p.Node("Icon") ?? new Vector2(57, 130);
        p.ButtonPressed += tag =>
        {
            switch (tag)
            {
                case "CloseButton": Close(); break;
                case "IdentifyButton":
                    if (Items.Store.CanIdentify(_item)) Items.Store.Identify(_item);
                    break;
            }
        };
        Ctx.Session.NotifyReceived += OnNotify;
        TreeExiting += () => Ctx.Session.NotifyReceived -= OnNotify;
        return p;
    }

    Vector2 _iconCentre;

    /// <summary>The item shown (0 for none).</summary>
    public uint ItemId => _item;

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "examine") return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        if ((d.TryGetProperty("kind", out var k) ? k.GetString() : "") != "item") return;
        // The modern card stays hidden for items.
        Ctx.Examine.Visible = false;
        Show(d);
    }

    void Show(JsonElement d)
    {
        _item = d.TryGetProperty("object", out var o) ? o.GetUInt32() : 0;
        string rawName = d.GetProperty("name").GetString() ?? "";
        var icon = d.GetProperty("icon").EnumerateArray().Select(i => i.GetString() ?? "").ToArray();
        var info = d.TryGetProperty("info", out var inf) ? ItemInfo.From(inf) : null;
        _identified = !d.TryGetProperty("identified", out var idf) || idf.GetBoolean();
        var item = new ItemEntry(_item, NwnText.Strip(rawName), 0, 1, icon, Identified: _identified) { Info = info, RawName = rawName };
        if (_name != null) _name.Text = item.Name;
        _icon.Item = item;
        _icon.Size = ClassicItemArt.IconSize(Assets, item);
        _icon.Position = (_iconCentre - _icon.Size / 2).Round();
        _icon.QueueRedraw();

        if (_text != null)
        {
            _text.Clear();
            void Line(string s, Color? c = null) => _text.AddText(s, font: GuiFont.DefaultName, color: c, wrap: true);
            if (info != null)
            {
                if (info.Type.Length > 0) Line(info.Type);
                if (info.Damage.Length > 0) Line($"Damage: {info.Damage} {info.DamageType}".TrimEnd());
                if (info.AC >= 0) Line($"Armor Class: {info.AC}");
                Line($"Weight: {ItemText.Weight(info.Weight).Replace(" lb", "")} lbs.");
                if (info.Value >= 0) Line($"Value: {info.Value:N0} gp");
                if (!_identified) Line("Unidentified", new Color(0.6f, 0.7f, 1f));
                else if (info.Properties.Count > 0)
                {
                    Line("");
                    Line("Properties:");
                    foreach (var p in info.Properties) Line(p.Text, p.Bad ? new Color(1f, 0.4f, 0.35f) : new Color(0.55f, 0.65f, 1f));
                }
                foreach (var r in info.Requirements) Line(r.Text, r.Met ? null : new Color(1f, 0.4f, 0.35f));
            }
            string description = (d.GetProperty("description").GetString() ?? "").Trim();
            if (description.Length > 0)
            {
                Line("");
                foreach (var para in NwnText.Strip(description).Replace("\r", "").Split('\n')) Line(para);
            }
        }
        if (_identify != null) _identify.Disabled = _identified || !Items.Store.CanIdentify(_item);
        if (Visible) Refresh(); else Open();
    }
}

/// <summary>
/// The original's split-stack box (inv_stacksplit): how many items go into the new stack, changed with the
/// arrows, the mouse wheel or typed digits; "Select All" takes all but one; OK sends item-split.
/// </summary>
public partial class ClassicStackSplit : ClassicWindow
{
    public ClassicItems Items = null!;
    ClassicNumberLabel _number = null!;
    ItemEntry? _item;
    Action<int>? _accept;
    int _maximum;
    int _count = 1;
    bool _typed;

    protected override GuiPanel Build()
    {
        Anchor = LayoutPreset.Center;
        var p = GuiPanel.FromLayout(Assets, "inv_stacksplit") ?? GuiPanel.FromModel(Assets, "ctl_pnl_inv_st");
        var edit = p.Get<GuiList>("NumEdit");
        var box = edit != null && edit.Frame?.Node("text0") is { } a && edit.Frame.Node("text1") is { } b
            ? new Rect2(edit.Position + a, Vector2.Zero).Expand(edit.Position + b)
            : new Rect2(edit?.Position ?? new Vector2(40, 60), edit?.Size ?? new Vector2(160, 24));
        if (edit != null) edit.Bar.Visible = false;
        _number = new ClassicNumberLabel { Assets = Assets, Text = "1", Font = Assets.Font(GuiFont.DefaultName), AlignH = .5f, AlignV = .5f };
        _number.Position = box.Position;
        _number.Size = box.Size;
        p.AddChild(_number);
        p.ButtonPressed += tag =>
        {
            switch (tag)
            {
                case "UpButton": Set(_count + 1); break;
                case "DownButton": Set(_count - 1); break;
                case "AllButton": Set(int.MaxValue); break;
                case "OkButton": Accept(); break;
                case "CancelButton": Close(); break;
            }
        };
        return p;
    }

    public int Count => _count;

    public void Show(ItemEntry item)
    {
        if (item.Stack <= 1) return;
        _item = item;
        _accept = null;
        _maximum = item.Stack - 1;
        if (Panel.Get<GuiLabel>("Title") is { } title) title.Text = "How many items?";
        _typed = false;
        Set(item.Stack / 2);
        Open();
    }

    public void ShowGold(int available, Action<int>? accept = null)
    {
        if (available <= 0) return;
        _item = null;
        _maximum = available;
        if (Panel.Get<GuiLabel>("Title") is { } title) title.Text = "How much gold?";
        // Dropping gold by default; the barter window offers it instead.
        _accept = accept ?? (count => GoldCarry.Start(this, Ctx.Session, count, Ctx.Interaction.PickGround));
        _typed = false;
        _count = 0;
        _number.Text = "";
        _number.ResetCaret();
        Open();
    }

    void Set(int v)
    {
        int max = Math.Max(1, _maximum);
        _count = Math.Clamp(v, 1, max);
        _number.Text = _count.ToString();
        _number.ResetCaret();
    }

    public void Accept()
    {
        if (_accept != null && _count > 0) _accept(_count);
        else if (_item != null) Ctx.Session.Request("item-split", new { item = _item.Id, count = _count });
        Close();
    }

    public override void _Input(InputEvent e)
    {
        if (!Visible) return;
        if (e is InputEventKey { Pressed: true } k)
        {
            if (k.Keycode is >= Key.Key0 and <= Key.Key9 || k.Keycode is >= Key.Kp0 and <= Key.Kp9)
            {
                int digit = (int)(k.Keycode >= Key.Kp0 ? k.Keycode - Key.Kp0 : k.Keycode - Key.Key0);
                Set(_typed ? (int)Math.Min((long)_count * 10 + digit, int.MaxValue) : Math.Max(1, digit));
                _typed = true;
            }
            else if (k.Keycode == Key.Backspace) { Set(_count / 10); _typed = true; }
            else if (k.Keycode is Key.Up or Key.Right) Set(_count + 1);
            else if (k.Keycode is Key.Down or Key.Left) Set(_count - 1);
            else if (k.Keycode is Key.Enter or Key.KpEnter) Accept();
            else return;
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w && Panel.GetGlobalRect().HasPoint(w.Position))
        {
            Set(_count + (w.ButtonIndex == MouseButton.WheelUp ? 1 : -1));
            GetViewport().SetInputAsHandled();
        }
    }
}

/// <summary>The quantity field's insertion marker, aligned with its existing bitmap text.</summary>
public partial class ClassicNumberLabel : GuiLabel
{
    double _elapsed;
    bool _caretOn = true;
    public void ResetCaret() { _elapsed = 0; _caretOn = true; QueueRedraw(); }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree()) return;
        _elapsed += delta;
        bool on = _elapsed % 1.0 < .5;
        if (on != _caretOn) { _caretOn = on; QueueRedraw(); }
    }

    public override void _Draw()
    {
        base._Draw();
        if (!_caretOn) return;
        var font = Font ?? Assets.Font(GuiFont.DefaultName);
        float textWidth = font.Measure(Text);
        float x = MathF.Round((Size.X - textWidth) * AlignH + textWidth + 1);
        float y = MathF.Round((Size.Y - font.Height) * AlignV);
        var line = new Rect2(Math.Clamp(x, 1, Size.X - 2), y, 1, font.Height);
        var teal = new Color(.1f, .85f, 1f);
        DrawRect(line.Grow(2), teal with { A = .12f });
        DrawRect(line.Grow(1), teal with { A = .4f });
        DrawRect(line, new Color(.65f, 1f, 1f));
    }
}

/// <summary>
/// The original's buy / sell confirmation (buysell_confirm) as a modal <see cref="ClassicPopup"/>: the question in
/// its ItemDesc text box, OK and Cancel. Also used for dropping valuables.
/// </summary>
public sealed class ClassicConfirm
{
    public required ClassicContext Ctx;
    ClassicPopup? _open;

    public bool IsOpen => _open != null && GodotObject.IsInstanceValid(_open) && _open.IsInsideTree();

    public void Ask(string question, Action ok)
    {
        Close();
        var p = ClassicPopup.Show(Ctx.Hud, Ctx.Assets, "buysell_confirm", question, ("OkButton", ok), ("CancelButton", null))
            ?? ClassicPopup.OkCancel(Ctx.Hud, Ctx.Assets, question, ok);
        if (p?.Panel.Get<GuiList>("ItemDesc") is { } text)
        {
            text.UseClassicScrollBar();
            text.Clear();
            text.AddText(question, font: GuiFont.DefaultName, wrap: true);
        }
        _open = p;
    }

    /// <summary>Answers Cancel (captures).</summary>
    public void Close()
    {
        if (IsOpen) _open!.Cancel();
        _open = null;
    }
}
