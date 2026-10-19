using System.Text.Json;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// The original's examine window for creatures, placeables and doors (examine_basic), centred: the portrait at
/// the panel's Icon node, the name, and the description in the scrolling text box; Close or the X closes it.
/// It answers the same "examine" notify as the modern window (which stays hidden for these); items are left to
/// the inventory's examine.
/// </summary>
public partial class ClassicExamine : ClassicWindow
{
    GuiLabel? _name;
    GuiList? _text;
    TextureRect _portrait = null!;

    protected override GuiPanel Build()
    {
        var a = Assets;
        var p = GuiPanel.FromLayout(a, "examine_basic") ?? GuiPanel.FromModel(a, "pnl_exam_item");
        _name = p.Get<GuiLabel>("Name");
        if (_name != null) _name.Size = new Vector2(330, _name.Size.Y);
        _text = p.Get<GuiList>("Description");
        // As the original (examinescroll): arrows and the ridged thumb always, the thumb filling the track when it all fits.
        _text?.UseClassicScrollBar();
        if (p.Get<GuiButton>("CloseButton") is { } close) close.Pressed += Close;
        var icon = p.Node("Icon") ?? new Vector2(57, 127);
        _portrait = new TextureRect { Position = (icon - new Vector2(32, 50)).Floor(), Size = new Vector2(64, 100), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
        p.AddChild(_portrait);
        Anchor = LayoutPreset.Center;
        return p;
    }

    public override void _Ready()
    {
        base._Ready();
        Ctx.Session.NotifyReceived += OnNotify;
        TreeExiting += () => Ctx.Session.NotifyReceived -= OnNotify;
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "examine") return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        string kind = d.TryGetProperty("kind", out var k) ? k.GetString() ?? "" : "";
        if (kind == "item") return;
        // The modern window opened itself on the same notify (it listens first): this one shows instead.
        Ctx.Examine.Visible = false;
        if (_name != null) _name.Text = NwnText.Strip(d.GetProperty("name").GetString() ?? "");
        string portrait = d.TryGetProperty("portrait", out var po) ? po.GetString() ?? "" : "";
        _portrait.Texture = kind == "trap" && d.TryGetProperty("icon", out var ic) && ic.GetArrayLength() > 0
            ? Assets.Texture(ic[0].GetString() ?? "") // Examine Trap shows the trap's icon (traprules b_set_trap)
            : ChargenModel.Portrait(Ctx.Session.Content, portrait, "m");
        if (_text != null)
        {
            _text.Clear();
            string subtitle = d.TryGetProperty("subtitle", out var st) ? NwnText.Strip(st.GetString() ?? "") : "";
            if (subtitle.Length > 0) _text.AddText(subtitle, font: SheetKit.Body, color: SheetKit.Caption, wrap: true);
            string description = d.TryGetProperty("description", out var de) ? NwnText.Strip(de.GetString() ?? "").Trim() : "";
            _text.AddText(description, font: SheetKit.Body, wrap: true);
        }
        if (!Visible) Open();
        else GetParent()?.MoveChild(this, -1);
    }
}
