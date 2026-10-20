using OpenWinter.Client.UI.FrontEnd;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's loading screen (load_screen on pnl_loadscreen), drawn over the modern loader while it is up:
/// black, the area's loading art as the original lays it out (its 512 x 512 texture's two halves side by side,
/// by the panel's planes and their texture coordinates), the title above, the status below, the progress bar
/// (ctl_loadstatus, its bar stretched to the progress) and a hint. The modern <see cref="LoadingScreen"/> keeps
/// deciding what is loading and when it is done.
/// </summary>
public partial class ClassicLoading : Control
{
    public LoadingScreen Screen = null!;
    ClassicAssets? _assets;
    GuiPanel? _panel;
    GuiModel? _model, _bar;
    string _artKey = "";
    Texture2D? _art;
    bool _artFlip;
    float _shown, _since;
    bool _built;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
    }

    /// <summary>Built once the game's data is open (it opens in the background after the loader exists).</summary>
    void Build(Views.ClientContent content)
    {
        _built = true;
        _assets = ClassicAssets.For(content);
        _model = _assets.Model("pnl_loadscreen");
        _bar = _assets.Model("ctl_loadstatus");
        _panel = GuiPanel.FromLayout(_assets, "load_screen");
        if (_panel == null || _model == null) { _assets = null; return; }
        // The frame and the bar are drawn here (the art replaces gui_replace01; the bar follows the progress).
        _panel.Frame.Visible = false;
        if (_panel.Get("LoadBar") is { } bar) bar.Visible = false;
        _panel.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var l in new[] { "TitleLabel", "DescLabel", "TipLabel" })
            if (_panel.Get<GuiLabel>(l) is { } label) label.Wrap = l == "TipLabel";
        AddChild(_panel);
    }

    public override void _Process(double delta)
    {
        if (!_built && FrontEndContent.Base is { } content) Build(content);
        bool show = _assets != null && Screen.Showing && Settings.Current.Interface.Classic; // follows a live style switch
        if (show != Visible)
        {
            Visible = show;
            if (show) { _shown = 0; _since = 0; }
            // The modern loader's own pieces stay underneath, covered.
        }
        if (!show || _panel == null) return;
        _since += (float)delta;
        ClassicPopup.CenterOn(_panel, Size);
        Label("TitleLabel", Screen.Title);
        Label("DescLabel", Screen.Status);
        Label("TipLabel", Screen.Hint);
        string key = Screen.ArtResRef;
        if (key != _artKey)
        {
            _artKey = key;
            _art = null;
            if (key.Length > 0 && Screen.ArtContent?.Textures.Get(key) is { } t) { _art = t.Texture; _artFlip = t.FlipV; }
        }
        // Unknown progress creeps towards the end; known progress is followed smoothly.
        float target = Screen.Progress >= 0 ? Screen.Progress : 0.9f * (1 - MathF.Exp(-_since / 6f));
        _shown = Mathf.Lerp(_shown, target, Math.Clamp((float)delta * 6, 0, 1));
        QueueRedraw();
    }

    void Label(string tag, string text)
    {
        if (_panel?.Get<GuiLabel>(tag) is { } l) l.Text = text;
    }

    public override void _Draw()
    {
        if (_panel == null || _model == null || _assets == null) return;
        DrawRect(new Rect2(Vector2.Zero, Size), Colors.Black);
        var origin = _panel.Position + _panel.ModelOrigin;
        var rid = GetCanvasItem();
        foreach (var m in _model.Rest.Meshes)
        {
            // The art planes use gui_replace01's place in the texture; the rest (the black backing) stays black.
            if (!m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase) || _art == null) continue;
            var pts = m.Points.Select(p => p + origin).ToArray();
            var uvs = _artFlip ? m.Uvs.Select(u => new Vector2(u.X, 1 - u.Y)).ToArray() : m.Uvs;
            var cols = Enumerable.Repeat(Colors.White, pts.Length).ToArray();
            RenderingServer.CanvasItemAddTriangleArray(rid, m.Indices, pts, cols, uvs, texture: _art.GetRid());
        }
        if (_bar != null && _panel.Get("LoadBar") is { } bar)
        {
            var barOrigin = _panel.Position + _panel.OriginOf(bar);
            var pose = _bar.Rest;
            GuiDraw.Model(this, _assets, pose, barOrigin, Colors.White, skip: m => m.Node.Equals("ResizedBar", StringComparison.OrdinalIgnoreCase));
            // The bar runs from its own left edge to the background's inner right edge (as far in as it starts).
            var fill = pose.Meshes.FirstOrDefault(m => m.Node.Equals("ResizedBar", StringComparison.OrdinalIgnoreCase));
            var back = pose.Meshes.FirstOrDefault(m => m.Node.Equals("Backgnd", StringComparison.OrdinalIgnoreCase));
            if (fill != null && back != null)
            {
                float left = fill.Points.Min(p => p.X), right = back.Points.Max(p => p.X) - (left - back.Points.Min(p => p.X));
                float end = fill.Points.Max(p => p.X), middle = (left + end) / 2;
                float extra = Math.Max(0, right - left - (end - left)) * Math.Clamp(_shown, 0, 1);
                // Three authored quads: fixed-width end caps surround a nearly zero-width
                // centre. Move only the right half; scaling the whole mesh inflates the caps.
                var stretched = fill with
                {
                    Points = fill.Points.Select(p =>
                    new Vector2(p.X > middle ? p.X + extra : p.X, p.Y)).ToArray()
                };
                GuiDraw.Model(this, _assets, new GuiModel.Pose([stretched], new(), new Rect2()), barOrigin, Colors.White);
            }
        }
    }
}
