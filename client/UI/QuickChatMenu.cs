using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Preferences;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>Shared V menu: original keyboard paths, localized labels, server-authoritative voices/orders.</summary>
public partial class QuickChatMenu : Control
{
    public ClientSession Session = null!;
    QuickChat.Group? _group;
    readonly HashSet<Key> _held = [];
    public bool CapturingKeys => Visible || _held.Count > 0;
    ClassicAssets? _assets;
    ClassicText? _font;
    Control? _text;
    float RowHeight => Settings.Current.Interface.Classic ? 14 : 24;
    StyleBoxFlat? _modernStyle;
    int Count => _group?.Entries.Length ?? QuickChat.Groups.Length;

    public override void _Ready() { Visible = false; MouseFilter = MouseFilterEnum.Stop; Session.AreaLoading += OnArea; }
    public override void _ExitTree() => Session.AreaLoading -= OnArea;
    void OnArea(OpenWinter.Net.LoadAreaMessage _) => Hide();
    public void Toggle()
    {
        if (Visible) { Cancel(); return; }
        if (Session.Content is not { } content || Session.Controlled == null) return;
        _assets = ClassicAssets.For(content);
        _font = ClassicText.For(_assets, "fnt_default", 12);
        if (_text == null) _text = _font.CreateLayer(this, DrawText);
        _text.Material = Settings.Current.Interface.Classic ? _font.Material : null;
        _group = null; Show(); Layout(); QueueRedraw(); _text.QueueRedraw();
        Session.Request("gui-event", new { type = 18 });
    }
    void Cancel()
    {
        if (!Visible) return;
        Session.Request("gui-event", new { type = 20 });
        Hide();
    }
    void Layout()
    {
        // The root ContentScaleFactor already applies the interface preference.
        var size = new Vector2(350, Count * RowHeight + 6);
        var position = new Vector2(5, GetViewportRect().Size.Y - 135 - size.Y);
        if (Size == size && Position == position) return;
        Size = size;
        Position = position;
        QueueRedraw(); _text?.QueueRedraw();
    }
    public override void _Process(double delta) { _held.RemoveWhere(k => !Input.IsKeyPressed(k)); if (Visible) Layout(); }
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: false } release) _held.Remove(release.Keycode);
        if (!Visible || PlayerInput.Blocked) { if (PlayerInput.Blocked) Hide(); return; }
        if (e is InputEventKey { Pressed: true, Echo: false } key)
        {
            _held.Add(key.Keycode);
            if (key.Keycode == Key.Escape) Cancel();
            else if (InputBindings.Pressed(e, ActionCatalog.QuickChat)) Cancel();
            else Select((char)key.Keycode);
            GetViewport().SetInputAsHandled();
        }
        else if (e is InputEventMouseButton { Pressed: true } mouse && !GetGlobalRect().HasPoint(mouse.Position)) Hide();
    }
    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mouse) return;
        int row = (int)((mouse.Position.Y - 3) / RowHeight);
        if (row < 0 || row >= Count) return;
        Select(_group == null ? QuickChat.Groups[row].Key : _group.Entries[row].Key);
        AcceptEvent();
    }
    void Select(char key)
    {
        if (_group == null)
        {
            if (QuickChat.Groups.FirstOrDefault(g => g.Key == key) is not { } group) { CancelInvalidLetter(key); return; }
            Session.Request("gui-event", new { type = 19, integer = (int)key });
            _group = group; Layout(); return;
        }
        if (_group.Entries.FirstOrDefault(e => e.Key == key) is not { } entry) { CancelInvalidLetter(key); return; }
        // Native reports each accepted hotkey, including its group. Selecting a line
        // closes the menu without a separate QUICKCHAT_CLOSE callback.
        Session.Request("gui-event", new { type = 19, integer = (int)key });
        Session.Request("voice-chat", new { id = entry.Slot });
        Hide();
    }
    void CancelInvalidLetter(char key)
    {
        // Native closes on unrecognized letter choices. Modifier-only keys are
        // not covered by that observation and must not dismiss the menu here.
        if (key is >= 'A' and <= 'Z') Cancel();
    }
    public override void _Draw()
    {
        if (_assets == null) return;
        if (Settings.Current.Interface.Classic && _assets.Model("pnl_quickchat") is { } model)
        {
            // pnl_quickchat's two-texel fill is extended procedurally by the original.
            // Its neighbouring texel supplies the one-pixel border, without repeating
            // the top edge in the middle of a taller list.
            var mesh = model.Rest.Meshes.FirstOrDefault();
            if (mesh != null && _assets.Texture(mesh.Texture) is { } texture)
            {
                float x = MathF.Round(mesh.Uvs.Min(v => v.X) * texture.GetWidth());
                var fill = new Rect2(x, 1, 1, 1);
                var edge = new Rect2(x - 1, 1, 1, 1);
                DrawTextureRectRegion(texture, new Rect2(Vector2.One, Size - Vector2.One * 2), fill);
                DrawTextureRectRegion(texture, new Rect2(0, 0, Size.X, 1), edge);
                DrawTextureRectRegion(texture, new Rect2(0, Size.Y - 1, Size.X, 1), edge);
                DrawTextureRectRegion(texture, new Rect2(0, 1, 1, Size.Y - 2), edge);
                DrawTextureRectRegion(texture, new Rect2(Size.X - 1, 1, 1, Size.Y - 2), edge);
            }
        }
        else DrawStyleBox(_modernStyle ??= Styles.Card(), new Rect2(Vector2.Zero, Size));
    }
    void DrawText(CanvasItem canvas)
    {
        if (_font == null || _assets == null) return;
        for (int i = 0; i < Count; i++)
        {
            var key = _group == null ? QuickChat.Groups[i].Key : _group.Entries[i].Key;
            uint strref = _group == null ? QuickChat.Groups[i].StringRef : _group.Entries[i].StringRef;
            string label = _assets.Tlk(strref);
            if (label.Length == 0) label = _group == null ? QuickChat.Groups[i].Label : _group.Entries[i].Label;
            bool classic = Settings.Current.Interface.Classic;
            Font font = classic ? _font.Font : Fonts.Ui;
            int size = classic ? _font.Size : Palette.FontSmall;
            float ascent = classic ? _font.Ascent : font.GetAscent(size);
            canvas.DrawString(font, new Vector2(classic ? 7 : 12, 2 + ascent + i * RowHeight), $"{key}: {label}", HorizontalAlignment.Left, -1, size, classic ? new Color(0.4f, 0.8f, 1) : Palette.Text);
        }
    }
}
