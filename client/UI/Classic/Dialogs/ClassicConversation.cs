using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's conversation box (pnl_dialog): a translucent box in the top-left corner with the speaker's
/// portrait, their name and line, and the numbered replies below (click one, or press 1-9; Esc ends the
/// conversation). Places come from the model's nodes (portraitback, name0/name1, entrytext, repliestext and
/// the ctl_dialog_reply index box); the box's colours from gui_boxes_dialog.
/// The modern <see cref="DialogPanel"/> keeps running underneath, unseen: it owns the conversation's keys,
/// camera framing and cutscene behaviour, and sends the replies, so both interfaces converse the same way.
/// </summary>
public partial class ClassicConversation : Control
{
    public ClassicContext Ctx = null!;
    ClassicAssets Assets => Ctx.Assets;

    // Name and reply colours measured from the original (1280x720); NPC text is white.
    public static readonly Color NameColor = Color.Color8(102, 178, 255), ReplyColor = Color.Color8(102, 178, 255), TextColor = Colors.White;
    /// <summary>The box's height at its smallest (measured): it grows when the conversation needs more.</summary>
    public const float MinHeight = 254;
    static float LinePitch => Settings.Current.Interface.HighResolutionFont ? 16 : 14;

    DialogMessage? _m;
    Texture2D? _portrait;
    Rect2 _box, _portraitRect, _nameRect;
    Vector2 _entryAt, _repliesAt;
    float _entryWidth, _repliesWidth, _indexLeft, _indexRight;
    Color _fill = new(0, 0, 0, 0.65f), _edge = new(1, 1, 1, 0.65f);
    ClassicText _font = null!, _replyFont = null!, _nameFont = null!;
    Control _textLayer = null!, _nameLayer = null!;
    List<ClassicText.Piece> _name = new(), _entry = new();
    readonly List<(int Index, float Top, float Bottom, List<ClassicText.Piece> Pieces)> _replies = new();
    int _hover = -1;
    float _entryLines;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        Measure();
        _textLayer = _font.CreateLayer(this, DrawText);
        _nameLayer = _nameFont.CreateLayer(this, ci =>
        {
            if (_m == null) return;
            float nameTop = MathF.Round(_nameRect.Position.Y + (_nameRect.Size.Y - _nameFont.LineHeight) / 2) + 1;
            _nameFont.Draw(ci, new Vector2(_nameRect.Position.X, nameTop), _name);
        });
        Ctx.Session.DialogReceived += OnStep;
        TreeExiting += () => { Ctx.Session.DialogReceived -= OnStep; ClassicWindow.CloseConversation(this); };
        // The modern panel decides when the conversation ends (its Esc, a script); follow it.
        Ctx.Dialog.VisibilityChanged += OnDialogVisibility;
        TreeExiting += () => Ctx.Dialog.VisibilityChanged -= OnDialogVisibility;
        GetViewport().SizeChanged += () => { if (_m != null) Build(); };
    }

    /// <summary>Positions from pnl_dialog and ctl_dialog_reply; colours from the box texture.</summary>
    void Measure()
    {
        _font = ClassicText.For(Assets);
        _replyFont = Settings.Current.Interface.HighResolutionFont ? ClassicText.For(Assets, size: 14) : _font;
        _nameFont = ClassicText.For(Assets, ClassicText.NameFont, 16);
        var model = Assets.Model("pnl_dialog");
        var rest = model?.Rest;
        Rect2? Mesh(string node)
        {
            var m = rest?.Meshes.FirstOrDefault(x => x.Node.Equals(node, StringComparison.OrdinalIgnoreCase));
            if (m == null) return null;
            var r = new Rect2(m.Points[0], Vector2.Zero);
            foreach (var p in m.Points) r = r.Expand(p);
            return r;
        }
        Vector2 Node(string name, Vector2 fallback) => rest?.Node(name) ?? fallback;
        // Model origin = screen top-left; y down in pixels.
        var top = Mesh("Plane12") ?? new Rect2(5, 4, 350, 16);
        _box = new Rect2(top.Position, new Vector2(top.Size.X, MinHeight));
        _portraitRect = Mesh("portraitback") ?? new Rect2(8, 7, 64, 100);
        var n0 = Node("name0", new Vector2(76, 13));
        var n1 = Node("name1", new Vector2(353, 33));
        _nameRect = new Rect2(n0, Vector2.Zero).Expand(n1);
        _entryAt = Node("entrytext", new Vector2(76, 35));
        if (!Settings.Current.Interface.HighResolutionFont) _entryAt.Y -= 1;
        // Text wraps in the entry and reply controls' text0..text1 boxes (the rest of the viewport is their scroll bars).
        var entry = Assets.Model("ctl_dialog_entry")?.Rest;
        _entryWidth = entry?.Node("text1") is { } e1 && entry.Node("text0") is { } e0 ? e1.X - e0.X : 261;
        _repliesAt = Node("repliestext", new Vector2(26, 112));
        if (!Settings.Current.Interface.HighResolutionFont) _repliesAt.Y -= 1;
        // The reply number sits in ctl_dialog_reply's indextext0..indextext1 box, left of the reply's text0.
        var reply = Assets.Model("ctl_dialog_reply")?.Rest;
        _repliesWidth = reply?.Node("text1") is { } r1 && reply.Node("text0") is { } r0 ? r1.X - r0.X : 311;
        float i0 = reply?.Node("indextext0")?.X ?? -20, i1 = reply?.Node("indextext1")?.X ?? -4, t0 = reply?.Node("text0")?.X ?? 0;
        _indexLeft = _repliesAt.X + (i0 - t0);
        _indexRight = _repliesAt.X + (i1 - t0);
        // gui_boxes_dialog: the planes sample a 2-texel fill whose neighbours are the border.
        var plane = rest?.Meshes.FirstOrDefault(x => x.Node.Equals("Plane12", StringComparison.OrdinalIgnoreCase));
        if (plane != null && Assets.Texture(plane.Texture) is { } tex && tex.GetImage() is { } img)
        {
            if (img.IsCompressed()) img.Decompress();
            float u0 = plane.Uvs.Min(p => p.X);
            int x = Math.Clamp((int)MathF.Round(u0 * img.GetWidth()), 1, img.GetWidth() - 1);
            int y = img.GetHeight() / 2;
            _fill = img.GetPixel(x, y);
            _edge = img.GetPixel(x - 1, y);
        }
    }

    void OnStep(DialogMessage m)
    {
        if (m.Ended) { Close(); return; }
        if (_m == null) ClassicWindow.OpenConversation(this);
        _m = m;
        _portrait = Portrait(m);
        _hover = -1;
        Build();
        Visible = true;
        GetParent()?.MoveChild(this, -1);
    }

    void OnDialogVisibility() { if (!Ctx.Dialog.Visible) Close(); }

    public void Close()
    {
        _m = null;
        Visible = false;
        ClassicWindow.CloseConversation(this);
    }

    /// <summary>Lays the line and the replies out; the box grows to hold them.</summary>
    void Build()
    {
        if (_m is not { } m) return;
        (_name, _) = _nameFont.Layout(ClassicText.Runs(Kit.NwnText.Strip(m.SpeakerName), NameColor), float.MaxValue);
        int lines;
        (_entry, lines) = _font.Layout(ClassicText.Runs(m.Text.Trim(), TextColor), _entryWidth);
        _entryLines = lines;
        float entryBottom = _entryAt.Y + lines * LinePitch;
        float y = Math.Max(_repliesAt.Y, entryBottom + 4);
        _replies.Clear();
        foreach (var (index, text) in m.Replies)
        {
            var (pieces, n) = _replyFont.Layout(ClassicText.Runs(text.Trim(), ReplyColor), _repliesWidth);
            float h = Math.Max(1, n) * LinePitch;
            _replies.Add((index, y, y + h, pieces));
            y += h;
        }
        float view = GetViewportRect().Size.Y;
        float height = Math.Clamp(y + 8 - _box.Position.Y, MinHeight, Math.Max(MinHeight, view - _box.Position.Y - 160));
        _box = new Rect2(_box.Position, new Vector2(_box.Size.X, height));
        Position = ClassicWindow.PanelPosition(this, 360);
        Size = _box.End;
        QueueRedraw();
    }

    /// <summary>The speaker's medium portrait, cropped to the picture (the top 100 of 128 rows) as the model's UVs crop it.</summary>
    Texture2D? Portrait(DialogMessage m)
    {
        if (Ctx.Session.Objects.GetValueOrDefault(m.Speaker) is not { Portrait.Length: > 0 } speaker || Ctx.Session.Content is not { } content) return null;
        foreach (var size in new[] { "m", "l", "h", "s" })
        {
            if (content.Textures.Get(speaker.Portrait + size) is not { } tex) continue;
            var s = tex.Texture.GetSize();
            float h = s.Y * 100f / 128f;
            return new AtlasTexture { Atlas = tex.Texture, Region = new Rect2(0, tex.FlipV ? s.Y - h : 0, s.X, h) };
        }
        return null;
    }

    public override void _Draw()
    {
        if (_m == null) return;
        // The box: a 1-pixel border round a translucent fill.
        DrawRect(_box.Grow(-1), _fill);
        DrawRect(_box, _edge, false, 1);
        if (_portrait != null) DrawTextureRect(_portrait, _portraitRect, false);
        _textLayer.QueueRedraw();
        _nameLayer.QueueRedraw();
    }

    void DrawText(CanvasItem ci)
    {
        if (_m == null) return;
        _font.Draw(ci, _entryAt, _entry, lineHeight: LinePitch);
        for (int i = 0; i < _replies.Count; i++)
        {
            var r = _replies[i];
            var tint = i == _hover ? new Color(1.35f, 1.25f, 1.1f) : (Color?)null;
            string number = (i + 1) + ".";
            float w = _replyFont.Measure(number);
            var (pieces, _) = _replyFont.Layout(ClassicText.Runs(number, ReplyColor), float.MaxValue);
            _replyFont.Draw(ci, new Vector2(_indexRight - w, r.Top), pieces, tint, lineHeight: LinePitch);
            _replyFont.Draw(ci, new Vector2(_repliesAt.X, r.Top), r.Pieces, tint, lineHeight: LinePitch);
        }
    }

    int ReplyAt(Vector2 p)
    {
        for (int i = 0; i < _replies.Count; i++)
            if (p.Y >= _replies[i].Top && p.Y < _replies[i].Bottom && p.X >= _indexLeft && p.X < _box.End.X) return i;
        return -1;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
        {
            int h = ReplyAt(mm.Position);
            if (h != _hover) { _hover = h; QueueRedraw(); }
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb)
        {
            int i = ReplyAt(mb.Position);
            if (i >= 0) Choose(i);
            AcceptEvent();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && _hover >= 0) { _hover = -1; QueueRedraw(); }
    }

    /// <summary>Picks reply <paramref name="i"/> (0-based, as shown): the same request the modern panel sends.</summary>
    public void Choose(int i)
    {
        if (i < 0 || i >= _replies.Count) return;
        Ctx.Dialog.Reply?.Invoke(_replies[i].Index);
    }

    /// <summary>The middle of reply <paramref name="i"/> on screen, if shown (scripted captures).</summary>
    public Vector2? ReplyPosition(int i) => i >= 0 && i < _replies.Count ? GetGlobalTransform() * new Vector2((_repliesAt.X + _box.End.X) / 2, (_replies[i].Top + _replies[i].Bottom) / 2) : null;
}
