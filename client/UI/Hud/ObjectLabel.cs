using OpenWinter.Client.UI.Classic;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>The original's text or portrait bubble nameplate, shared by both interface styles.</summary>
public partial class ObjectLabel : Control
{
    ClassicText? _font;
    string _fontName = "";
    Control? _textLayer;
    ClientContent? _content;
    ClassicAssets? _assets;
    GuiModel? _bubble;
    Texture2D? _portrait;
    readonly List<(string Text, Color Color)> _lines = new();
    string _name = "", _player = "", _portraitName = "";
    int _damage = -2, _reaction = -2, _mode = -1;
    Vector2 _size;
    float _textCentre, _textTop, _lineHeight, _unit;
    /// <summary>Align the bottom of the text block, so adding a bubble does not move its text.</summary>
    public Vector2 AnchorOffset => new(_size.X / 2, _size.Y - (_mode == 2 ? 8 * _unit : 0));
    public ObjectLabel() { MouseFilter = MouseFilterEnum.Ignore; }

    public void Set(ClientContent content, string name, string playerName, int damageLevel, string portrait, int reaction)
    {
        bool highResolution = Settings.Current.Interface.HighResolutionFont;
        int size = Math.Max(8, (int)MathF.Round(Settings.Current.Interface.OverheadTextSize * (highResolution ? 14f : 11f) / 16));
        int mode = Settings.Current.Interface.TextBubbleMode;
        if (_content == content && _name == name && _player == playerName && _damage == damageLevel && _reaction == reaction
            && _font?.Size == size && _mode == mode && _portraitName == portrait && _fontName == ClassicText.DefaultTextFont) return;
        _content = content;
        _assets = ClassicAssets.For(content);
        _fontName = ClassicText.DefaultTextFont;
        _font = ClassicText.For(_assets, _fontName, size);
        _textLayer ??= _font.CreateLayer(this, DrawText);
        _textLayer.Material = _font.Material;
        _name = name; _player = playerName; _damage = damageLevel; _reaction = reaction; _mode = mode; _portraitName = portrait;
        _unit = Settings.Current.Interface.OverheadTextSize / 16f;
        _lineHeight = (highResolution ? 16 : 14) * _unit;
        _portrait = mode == 2 ? CharGen.ChargenModel.Portrait(content, portrait, "t") : null;
        _lines.Clear();
        var nameColor = reaction switch
        {
            2 => Color.Color8(255, 80, 64),
            1 => Color.Color8(128, 255, 128),
            0 => Color.Color8(128, 128, 255),
            _ => Colors.White,
        };
        if (playerName.Length > 0) _lines.Add((Kit.NwnText.Strip(playerName), nameColor));
        _lines.Add((Kit.NwnText.Strip(name), nameColor));
        if (damageLevel is >= 0 and <= 5)
        {
            var table = content.Resources.TwoDA("damagelevels");
            int strref = table.GetInt(damageLevel, "STRING_REF", -1);
            string health = strref < 0 ? "" : content.Resources.TlkString((uint)strref);
            if (health.Length > 0) _lines.Add((Kit.NwnText.Strip(health), HealthColor(damageLevel)));
        }
        float width = MathF.Ceiling(_lines.Max(l => _font.Measure(l.Text)));
        _size = new Vector2(width, _lineHeight * _lines.Count);
        _textTop = 0;
        _bubble = null;
        if (mode == 2)
        {
            // The installed bubble meshes have 100/150/300-pixel text widths, rounded corners and a
            // 14-pixel row pitch. Select by text width: adding the portrait here promotes
            // medium names to 300 pixels, unlike the compact native panel (namewidth).
            float textWidth = width / _unit;
            int bucket = textWidth <= 100 ? 100 : textWidth <= 150 ? 150 : 300;
            float required = textWidth + (_portrait != null ? 36 : 0);
            int rows = Math.Max(_lines.Count, _portrait != null ? 2 : 1);
            _bubble = _assets.Model($"pnl_bubble{rows}_{bucket}");
            _size = (_bubble?.Bounds.Size ?? new Vector2(bucket + 30, rows * 14 + 16)) * _unit;
            _size.X = Math.Max(_size.X, (required + 30) * _unit);
            _textTop = 8 * _unit;
        }
        _textCentre = _size.X / 2 + (_portrait != null ? 12 * _unit : 0);
        CustomMinimumSize = _size;
        Size = _size;
        QueueRedraw();
    }

    // RGB sampled from the original in the nameplates visual scene.
    static Color HealthColor(int level) => level switch
    {
        0 => Color.Color8(128, 255, 128),
        1 => Color.Color8(200, 255, 1),
        2 => Color.Color8(255, 200, 1),
        3 => Color.Color8(255, 100, 1),
        4 => Color.Color8(255, 80, 64),
        _ => Color.Color8(128, 128, 128),
    };

    public override void _Draw()
    {
        if (_font == null) return;
        if (_mode == 2)
        {
            if (_bubble is { } bubble && _assets != null && bubble.Bounds.Size.X > 0 && bubble.Bounds.Size.Y > 0)
            {
                DrawSetTransform(Vector2.Zero, 0, _size / bubble.Bounds.Size);
                GuiDraw.Model(this, _assets, bubble.Rest, -bubble.Bounds.Position, Colors.White);
                DrawSetTransform(Vector2.Zero);
            }
            if (_portrait != null)
            {
                var rect = new Rect2(new Vector2(16, 9) * _unit, new Vector2(16, 25) * _unit);
                DrawRect(rect.Grow(_unit), Colors.White);
                DrawTextureRect(_portrait, rect, false);
            }
        }
        _textLayer?.QueueRedraw();
    }

    void DrawText(CanvasItem ci)
    {
        if (_font == null) return;
        for (int i = 0; i < _lines.Count; i++)
        {
            var (text, color) = _lines[i];
            _font.DrawLine(ci, new Vector2(_textCentre - _font.Measure(text) / 2, _textTop + i * _lineHeight), text, color);
        }
    }
}
