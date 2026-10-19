using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The modern look's text macro editor (the quickbar slot ring's Custom Text Macro, Edit Label and Edit Command): the
/// label shown on the slot and the line it sends to chat, in one window. The classic look asks with the original's two
/// prompts instead (docs/QUICKBAR_RADIAL.md).
/// </summary>
public static class MacroEditor
{
    public static UiTheme.Window Show(Control near, string label, string text, Action<string, string> save)
    {
        var editor = UiTheme.Window.Create<UiTheme.Window>("Text macro", new Vector2(460, 230), "talk");
        LineEdit Field(string caption, string value, string hint, int max)
        {
            editor.Body.AddChild(Ui.Text(caption, Ui.TextStyle.Small, Palette.TextSecondary));
            var edit = new LineEdit { Text = value, MaxLength = max, TooltipText = hint, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            editor.Body.AddChild(edit);
            return edit;
        }
        var name = Field("Label", label, "Shown on the quickbar slot.", Game.Requests.QuickbarRequests.MacroLabelLimit);
        var line = Field("Text", text, "Sent to chat when the slot is used: /p for party, /w whisper, /tk talk; otherwise it is shouted.",
            Game.Requests.QuickbarRequests.MacroTextLimit);
        var buttons = Ui.HBox(Palette.Space2);
        editor.Body.AddChild(buttons);
        var ok = new Button { Text = "Save", ThemeTypeVariation = "PrimaryButton" };
        ok.Pressed += () => { save(name.Text, line.Text); editor.Close(); };
        buttons.AddChild(ok);
        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += editor.Close;
        buttons.AddChild(cancel);
        line.TextSubmitted += _ => ok.EmitSignal(BaseButton.SignalName.Pressed);
        editor.Closed += editor.QueueFree;
        near.GetParent().AddChild(editor);
        editor.Position = ((near.GetViewportRect().Size - editor.Size) / 2).Floor();
        editor.Visible = true;
        name.GrabFocus();
        return editor;
    }
}

/// <summary>
/// The modern look's description of a spell, feat or skill (Examine on a quickbar slot): its icon, name and the
/// talk-table description from the server's "chargen-describe".
/// </summary>
public static class DescriptionWindow
{
    public static UiTheme.Window? Show(Control near, ClientSession session, BarSlot slot, SlotLook look)
    {
        if (slot.Kind is not ("spell" or "feat" or "skill")) return null;
        var window = UiTheme.Window.Create<UiTheme.Window>(Kit.NwnText.Strip(look.Name), new Vector2(440, 420), "eye");
        var head = Ui.HBox(Palette.Space3);
        window.Body.AddChild(head);
        // The row's own icon (spells.2da IconResRef, feat.2da ICON, skills.2da Icon) when the look has none yet.
        var (table, column) = slot.Kind switch { "spell" => ("spells", "IconResRef"), "feat" => ("feat", "ICON"), _ => ("skills", "Icon") };
        string iconName = look.Icons.FirstOrDefault(i => i.Length > 0) ?? session.Content?.Resources.TwoDA(table).Get(slot.Id, column) ?? "";
        if (iconName.Length > 0 && session.Content?.Textures.Get(iconName.ToLowerInvariant()) is { } icon)
            head.AddChild(new TextureRect { Texture = icon.Texture, CustomMinimumSize = new Vector2(40, 40), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var title = Ui.Text(Kit.NwnText.Strip(look.Name), Ui.TextStyle.Title);
        head.AddChild(title);
        var text = new RichTextLabel { FitContent = false, ScrollActive = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 300) };
        window.Body.AddChild(text);
        void OnNotify(NotifyMessage m)
        {
            if (m.Kind != "chargen-describe" || !GodotObject.IsInstanceValid(text)) return;
            using var doc = JsonDocument.Parse(m.Json);
            var d = doc.RootElement;
            if (d.GetProperty("kind").GetString() != slot.Kind || d.GetProperty("id").GetInt32() != slot.Id) return;
            text.Text = Kit.NwnText.Strip(d.GetProperty("text").GetString() ?? "").Trim();
            if (Kit.NwnText.Strip(d.GetProperty("name").GetString() ?? "") is { Length: > 0 } name) { title.Text = name; window.TitleLabel.Text = name; }
        }
        session.NotifyReceived += OnNotify;
        window.Closed += () => { session.NotifyReceived -= OnNotify; window.QueueFree(); };
        near.GetParent().AddChild(window);
        window.Position = ((near.GetViewportRect().Size - window.Size) / 2).Floor();
        window.Visible = true;
        session.Request("chargen-describe", new { kind = slot.Kind, id = slot.Id });
        return window;
    }
}
