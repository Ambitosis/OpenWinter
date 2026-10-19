using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>
/// Under the minimap: the quest most recently updated, with the start of its latest entry.
/// Finished quests drop off; click to open the journal.
/// </summary>
public partial class QuestTracker : PanelContainer
{
    public ClientSession Session = null!;
    public Action? OpenJournal;
    Label _title = null!;
    RichTextLabel _text = null!;
    string _tag = "";
    ulong _bulkUntil;

    public override void _Ready()
    {
        Ui.Glass(this, Styles.Hud(10, Palette.Radius), 0.75f);
        MouseFilter = MouseFilterEnum.Stop;
        TooltipText = "Journal (J)";
        Visible = false;
        var col = Ui.VBox(4);
        col.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(col);
        var head = Ui.HBox(6);
        head.MouseFilter = MouseFilterEnum.Ignore;
        col.AddChild(head);
        head.AddChild(Icons.Rect("journal", 14, Palette.Accent));
        var caption = Ui.Text("CURRENT QUEST", Ui.TextStyle.Caption, Palette.AccentDim.Lightened(0.25f), 10);
        caption.AddThemeFontOverride("font", Fonts.UiBold);
        head.AddChild(caption);
        _title = Ui.Text("", Ui.TextStyle.Strong, Palette.Text, 14);
        _title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_title);
        _text = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, MouseFilter = MouseFilterEnum.Ignore };
        _text.AddThemeFontOverride("normal_font", Fonts.Reading);
        _text.AddThemeFontSizeOverride("normal_font_size", 15);
        _text.AddThemeColorOverride("default_color", Palette.TextSecondary);
        col.AddChild(_text);
        GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) OpenJournal?.Invoke(); };
        Session.NotifyReceived += OnNotify;
    }

    void OnNotify(NotifyMessage m)
    {
        // Opening the journal re-sends every quest; that isn't news, so keep tracking the current one.
        if (m.Kind == "journal-reset") { _bulkUntil = Time.GetTicksMsec() + 500; return; }
        if (m.Kind != "journal") return;
        using var doc = JsonDocument.Parse(m.Json);
        var d = doc.RootElement;
        string tag = d.GetProperty("tag").GetString() ?? "";
        bool removed = d.TryGetProperty("removed", out var r) && r.GetBoolean();
        bool end = d.TryGetProperty("end", out var e) && (e.ValueKind == JsonValueKind.True || e.ValueKind == JsonValueKind.Number && e.GetInt32() != 0);
        if (removed || end)
        {
            if (tag == _tag) { Visible = false; _tag = ""; }
            return;
        }
        if (Time.GetTicksMsec() < _bulkUntil && _tag.Length > 0) return;
        _tag = tag;
        _title.Text = NwnText.Strip(d.GetProperty("name").GetString() ?? "");
        var text = NwnText.Strip(d.GetProperty("text").GetString() ?? "").Trim();
        if (text.Length > 150) { int cut = text.LastIndexOf(' ', 147); text = text[..(cut > 60 ? cut : 147)] + "…"; }
        _text.Text = NwnText.ToBbcode(text);
        Visible = true;
    }
}
