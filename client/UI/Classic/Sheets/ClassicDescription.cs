using System.Text.Json;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// The original's description window for a spell, feat or skill (Examine on a quickbar slot, visual scene
/// quickbargaps): the examine panel (pnl_exam_item, centred) with the name in its title, the icon at the panel's Icon
/// node and the talk-table description in the scrolling box, and a single OK button where the item window has Close.
/// The text comes from the server's "chargen-describe" (the same rows the level-up screens read).
/// </summary>
public partial class ClassicDescription : ClassicWindow
{
    GuiLabel? _name;
    GuiList? _text;
    Control _icon = null!;
    string[] _icons = [];
    (string Kind, int Id) _showing;

    /// <summary>The window of the running classic game, made on first use.</summary>
    static ClassicDescription? _current;

    /// <summary>Opens the description of a spell, feat or skill slot; false when the slot has none to show.</summary>
    public static bool Show(ClassicContext ctx, BarSlot slot, SlotLook look)
    {
        if (slot.Kind is not ("spell" or "feat" or "skill") || slot.Id < 0) return false;
        if (_current == null || !IsInstanceValid(_current) || !_current.IsInsideTree())
        {
            _current = new ClassicDescription { Ctx = ctx, Name = "ClassicDescription" };
            ctx.Hud.AddChild(_current);
        }
        _current.Request(slot.Kind, slot.Id, look);
        return true;
    }

    protected override GuiPanel Build()
    {
        Anchor = LayoutPreset.Center;
        var p = GuiPanel.FromLayout(Assets, "examine_item") ?? GuiPanel.FromModel(Assets, "pnl_exam_item");
        _name = p.Get<GuiLabel>("Name");
        if (_name != null) _name.Size = new Vector2(p.Size.X - (_name.Position.X * 2), _name.Size.Y);
        _text = p.Get<GuiList>("Description");
        _text?.UseClassicScrollBar();
        // The text starts 73 px into the window, right of the icon (b_exam_spell: x 499 of a window at 426), where the
        // examine windows leave room for a portrait.
        _text?.MoveContentLeft(-36);
        // One OK in the Close button's place; no Identify and no X (quickbargaps b_exam_spell).
        if (p.Get<GuiButton>("IdentifyButton") is { } identify) identify.Visible = false;
        if (p.Get<GuiButton>("XButton") is { } x) x.Visible = false;
        if (p.Get<GuiButton>("CloseButton") is { } ok)
        {
            ok.Caption = "OK";
            ok.Pressed += Close;
        }
        // The icon beside the first line, 22 px in and 54 px down (b_exam_spell: Light's at x 448-480, y 191-223).
        _icon = new Control { MouseFilter = MouseFilterEnum.Ignore, Name = "Icon", Size = new Vector2(32, 32), Position = new Vector2(22, 54) };
        _icon.Draw += () =>
        {
            var content = Ctx.Session.Content;
            if (content == null || _icons.Length == 0) return;
            var look = new SlotLook("", _icons, "", -1, false, true, "", "");
            ClassicIcons.Draw(_icon, content, look, _icons, new Rect2(Vector2.Zero, _icon.Size), Colors.White);
        };
        p.AddChild(_icon);
        Ctx.Session.NotifyReceived += OnNotify;
        TreeExiting += () => Ctx.Session.NotifyReceived -= OnNotify;
        return p;
    }

    void Request(string kind, int id, SlotLook look)
    {
        _showing = (kind, id);
        _icons = look.Icons.Length > 0 ? look.Icons : TableIcon(kind, id);
        if (_name != null) _name.Text = NwnText.Strip(look.Name);
        _text?.Clear();
        _icon?.QueueRedraw();
        Ctx.Session.Request("chargen-describe", new { kind, id });
    }

    /// <summary>The row's own icon when the slot's look has none yet (spells.2da IconResRef, feat.2da ICON, skills.2da Icon).</summary>
    string[] TableIcon(string kind, int id)
    {
        var (table, column) = kind switch { "spell" => ("spells", "IconResRef"), "feat" => ("feat", "ICON"), _ => ("skills", "Icon") };
        string icon = Ctx.Session.Content?.Resources.TwoDA(table).Get(id, column) ?? "";
        return icon.Length > 0 && icon != "****" ? [icon.ToLowerInvariant()] : [];
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "chargen-describe") return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        if ((d.GetProperty("kind").GetString() ?? "", d.GetProperty("id").GetInt32()) != _showing) return;
        string name = NwnText.Strip(d.GetProperty("name").GetString() ?? "");
        if (_name != null && name.Length > 0) _name.Text = name;
        if (_text != null)
        {
            _text.Clear();
            foreach (var para in NwnText.Strip(d.GetProperty("text").GetString() ?? "").Trim().Replace("\r", "").Split('\n'))
                _text.AddText(para, font: SheetKit.Body, wrap: true);
        }
        if (Visible) { Refresh(); GetParent()?.MoveChild(this, -1); }
        else Open();
    }
}
