using OpenWinter.Aurora.Resources;
using OpenWinter.Client.UI.CharGen;
using OpenWinter.Game.Progression;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>Customize: cg_appearance (head, tattoo, body, colours over the turning model) and cg_char_info (names, age, voice, deity, description).</summary>
public partial class ClassicCharGen
{
    static readonly string[] PaletteModels = { "ctl_cg_colr_skin", "ctl_cg_colr_hair", "ctl_cg_colr_tatt", "ctl_cg_colr_tatt" };
    // The palettes the colour grids draw (ctl_cg_colr_skin / _hair / _tatt).
    static readonly string[] PaletteTextures = { "gui_pal_skin", "gui_pal_hair01", "gui_pal_tattoo", "gui_pal_tattoo" };
    CreaturePreview? _preview;
    readonly Dictionary<string, List<int>> _heads = new();

    // ------------------------------------------------------------------ appearance

    void OpenAppearance()
    {
        if (ShowScreen("cg_appearance") is not { } p || Model == null) return;
        On("OkButton", OpenCharInfo);
        On("CancelButton", Abandon);
        if (_n.BodyParts.Length == 0) _n.BodyParts = CharacterFactory.DefaultBodyParts();
        // The model viewer: pnl_chr_appear's CharModel (bottom-left) to CharModelTop (top-right) (c42).
        if (p.Get<GuiModelView>("ModelViewer") is { } viewer)
        {
            var a = viewer.Node("CharModel") ?? new Vector2(3, viewer.Size.Y - 3);
            var b = viewer.Node("CharModelTop") ?? new Vector2(viewer.Size.X - 3, 3);
            var rect = new Rect2(viewer.Position + new Vector2(a.X, b.Y), new Vector2(b.X - a.X, a.Y - b.Y));
            _preview = new CreaturePreview { Content = Session.Content, Position = rect.Position, Size = rect.Size, Stretch = true, Zoom = 0 };
            p.AddChild(_preview);
        }
        foreach (var tag in new[] { "HeadButton", "TattooButton", "BodyButton", "ClothingButton", "SkinButton", "HairButton", "TattooColButton" })
            if (Btn(tag) is { } gb and not CgButton) CgButton.From(gb);
        var heads = Heads();
        void Cycle(string tag, Action<int> step)
        {
            if (Btn(tag) is not { } b) return;
            var row = b;
            Vector2 Node(string n, float fx) => row.Model?.Node(n) is { } q ? q + row.Origin : new Vector2(row.Size.X * fx, row.Size.Y - 4);
            var left = CgButton.Make(A, "ctl_cg_btn_left");
            left.PlaceOrigin(Node("button1", 0.77f));
            left.Pressed += () => { step(-1); ShowLook(); };
            row.AddChild(left);
            var right = CgButton.Make(A, "ctl_cg_btn_right");
            right.PlaceOrigin(Node("button2", 0.88f));
            right.Pressed += () => { step(+1); ShowLook(); };
            row.AddChild(right);
        }
        Cycle("HeadButton", d =>
        {
            heads = Heads();
            int i = Math.Max(0, heads.IndexOf(_n.BodyParts[0]));
            _n.BodyParts[0] = heads[(i + d + heads.Count) % heads.Count];
        });
        Cycle("TattooButton", d =>
        {
            // The tattooed body parts are the second of each limb's and the torso's models (not measured on the original).
            int t = _n.BodyParts.Length > 2 && _n.BodyParts[2] == 2 ? 1 : 2;
            foreach (int part in new[] { 2, 7, 8, 9, 10, 13, 14, 15, 16 }) if (part < _n.BodyParts.Length) _n.BodyParts[part] = t;
        });
        Cycle("BodyButton", d =>
        {
            var phen = Model.Phenotypes.Keys.OrderBy(x => x).ToList();
            if (phen.Count == 0) return;
            int i = Math.Max(0, phen.IndexOf(_n.Phenotype));
            _n.Phenotype = phen[(i + d + phen.Count) % phen.Count];
            heads = Heads();
            if (!heads.Contains(_n.BodyParts[0])) _n.BodyParts[0] = heads[0];
        });
        // Clothing: the original dresses the preview in the package's clothes and cycles them (not done here).
        Cycle("ClothingButton", _ => { });
        // Colour swatches: the wide one for skin and hair, two for the tattoo colours (c42).
        Swatch("SkinButton", "button1", "ctl_cg_btn_colw", 0);
        Swatch("HairButton", "button1", "ctl_cg_btn_colw", 1);
        Swatch("TattooColButton", "button1", "ctl_cg_btn_col", 2);
        Swatch("TattooColButton", "button2", "ctl_cg_btn_col", 3);
        RefreshScreen = () => { foreach (var s in p.FindChildren("*", "", true, false).OfType<CgSwatch>()) s.QueueRedraw(); };
        ShowLook();
    }

    /// <summary>Opens a colour channel's grid (captures).</summary>
    public bool PressSwatch(int channel)
    {
        var s = Screen?.FindChildren("*", "", true, false).OfType<CgSwatch>().FirstOrDefault(x => x.Channel == channel);
        if (s == null) return false;
        s.EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>Picks a palette colour as a click in the open grid would (captures).</summary>
    public void PickColour(int channel, int value)
    {
        _n.Colors[channel] = value;
        ClosePopup();
        foreach (var v in GetChildren().OfType<Control>().Where(c => c.Name == "PopupVeil")) v.QueueFree();
        ShowLook();
        RefreshScreen?.Invoke();
    }

    /// <summary>Chooses the first portrait of the grid (captures).</summary>
    public void PickFirstPortrait()
    {
        if (_portraits.Count == 0) return;
        _n.PortraitId = _portraits[0].Id;
        _n.Portrait = "";
        RefreshScreen?.Invoke();
    }

    void Swatch(string tag, string node, string model, int channel)
    {
        if (Btn(tag) is not { } row) return;
        var s = new CgSwatch { Assets = A, Name = nameof(CgSwatch), Channel = channel, Texture_ = PaletteTextures[channel], Value = () => _n.Colors[channel] };
        s.Model = A.Model(model);
        s.PlaceOrigin(row.Model?.Node(node) is { } q ? q + row.Origin : new Vector2(row.Size.X - 74, row.Size.Y - 4));
        s.Pressed += () => ColourPopup(s, channel);
        row.AddChild(s);
    }

    /// <summary>The colour grid (ctl_cg_colr_*: the palette's 16 x 11 cells of 16 px), its bottom-left on the swatch's (c43).</summary>
    void ColourPopup(CgSwatch swatch, int channel)
    {
        ClosePopup();
        var veil = new Control { MouseFilter = MouseFilterEnum.Stop, Name = "PopupVeil" };
        veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(veil);
        var grid = GuiModelView.Create(A, PaletteModels[channel]);
        veil.AddChild(grid);
        var at = swatch.GetGlobalTransformWithCanvas().Origin - GetGlobalTransformWithCanvas().Origin;
        grid.Position = (at + new Vector2(0, swatch.Size.Y - grid.Size.Y)).Floor();
        grid.MouseFilter = MouseFilterEnum.Stop;
        grid.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
            int col = (int)(mb.Position.X / 16), row = (int)(mb.Position.Y / 16);
            if (col is >= 0 and < 16 && row >= 0 && row * 16 + col < 256 && mb.Position.Y < grid.Size.Y)
            {
                _n.Colors[channel] = row * 16 + col;
                ClassicSounds.Play(A, "gui_select");
                ShowLook();
                RefreshScreen?.Invoke();
            }
            ClosePopupVeil(veil);
        };
        veil.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) ClosePopupVeil(veil); };
    }

    void ClosePopupVeil(Control veil)
    {
        if (IsInstanceValid(veil)) { veil.QueueFree(); }
    }

    /// <summary>Heads with models for the race, gender and body (p&lt;g&gt;&lt;race&gt;&lt;phenotype&gt;_head&lt;n&gt;).</summary>
    List<int> Heads()
    {
        var content = Session.Content;
        if (content == null) return new List<int> { 1 };
        int appearance = _n.Appearance >= 0 ? _n.Appearance : Model?.Races.GetValueOrDefault(_n.Race)?.Int("appearance") ?? 6;
        var race = content.Resources.TwoDA("appearance").Get(appearance, "RACE").ToLowerInvariant();
        string prefix = $"p{(_n.Gender == 1 ? 'f' : 'm')}{race}{_n.Phenotype}_head";
        if (_heads.TryGetValue(prefix, out var cached)) return cached;
        var heads = content.Resources.All(ResType.Mdl).Select(k => k.ResRef.ToLowerInvariant())
            .Where(r => r.StartsWith(prefix) && int.TryParse(r[prefix.Length..], out _)).Select(r => int.Parse(r[prefix.Length..])).Distinct().OrderBy(x => x).ToList();
        if (heads.Count == 0) heads.Add(1);
        return _heads[prefix] = heads;
    }

    void ShowLook()
    {
        if (_preview == null || !IsInstanceValid(_preview) || Model == null) return;
        int appearance = _n.Appearance >= 0 ? _n.Appearance : Model.Races.GetValueOrDefault(_n.Race)?.Int("appearance") ?? 6;
        var look = new Appearance
        {
            Type = appearance, Gender = (byte)_n.Gender, Phenotype = (byte)_n.Phenotype,
            BodyParts = _n.BodyParts.Select(x => (ushort)Math.Max(0, x)).ToArray(), Colors = _n.Colors.Select(c => (byte)c).ToArray(),
            Wings = _n.Wings, Tail = _n.Tail,
        };
        // Dressed in the starting clothes, as the original shows the model (c42).
        if (Model.Raw("clothing") is { ValueKind: System.Text.Json.JsonValueKind.Object } cl)
        {
            int[] Ints(string n) => cl.TryGetProperty(n, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetInt32()).ToArray() : Array.Empty<int>();
            look.Equipment.Add(new ItemVisual
            {
                Slot = cl.GetProperty("slot").GetInt32(), BaseItem = cl.GetProperty("baseItem").GetInt32(),
                ModelParts = Ints("modelParts").Select(x => (ushort)Math.Max(0, x)).ToArray(), Colors = Ints("colors").Select(x => (byte)Math.Clamp(x, 0, 255)).ToArray(),
                ArmorParts = Ints("armorParts").Select(x => (ushort)Math.Max(0, x)).ToArray(),
            });
        }
        _preview.Show(look);
    }

    // ------------------------------------------------------------------ character info

    void OpenCharInfo()
    {
        if (ShowScreen("cg_char_info") is not { } p || Model == null) return;
        var race = Model.Races.GetValueOrDefault(_n.Race);
        // A name is generated, the race's age and biography filled in, when the screen first opens (c45).
        if (_n.FirstName.Length == 0) GenerateName();
        if (_n.Age < 0) _n.Age = race?.Int("age") ?? 20;
        if (_n.Description.Length == 0) _n.Description = race?.Str("biography") ?? "";
        LineEdit? Line(string tag, int max)
        {
            if (p.Get<GuiList>(tag) is not { } box) return null;
            box.Bar.Visible = false;
            foreach (var c in box.GetChildren().OfType<Control>()) if (c != box.Frame) c.Visible = false;
            return CgEdit.Over(A, p, box, max);
        }
        var first = Line("FirstNameEdit", 40);
        var last = Line("LastNameEdit", 40);
        var age = Line("AgeEdit", 4);
        if (first != null) { first.Text = _n.FirstName; first.TextChanged += t => _n.FirstName = t; }
        if (last != null) { last.Text = _n.LastName; last.TextChanged += t => _n.LastName = t; }
        if (age != null) { age.Text = _n.Age.ToString(); age.TextChanged += t => _n.Age = int.TryParse(t, out var v) ? v : -1; }
        if (p.Get<GuiList>("DescEdit") is { } descBox)
        {
            descBox.Bar.Visible = false;
            foreach (var c in descBox.GetChildren().OfType<Control>()) if (c != descBox.Frame) c.Visible = false;
            var desc = CgEdit.Area(A, p, descBox);
            desc.Text = _n.Description;
            desc.TextChanged += () => _n.Description = desc.Text;
        }
        // Voice sets of the character's gender in soundset.2da order (c45: "Female, Noble Scholar" first).
        var voices = CgList.Attach(p, "SoundSetEdit");
        var rows = new Dictionary<int, CgButton>();
        foreach (var s in Model.SoundSets.Values.Where(s => s.Int("gender") == _n.Gender).OrderBy(s => s.Id))
        {
            int id = s.Id;
            var row = CgButton.Make(A, "ctl_cg_btn_snds", s.Name);
            row.Pressed += () => { _n.SoundSet = id; foreach (var (k, b) in rows) b.Selected = k == id; };
            rows[id] = row;
            voices?.Add(row);
        }
        foreach (var (k, b) in rows) b.Selected = k == _n.SoundSet;
        On("RandomButton", () =>
        {
            GenerateName();
            if (first != null) first.Text = _n.FirstName;
            if (last != null) last.Text = _n.LastName;
        });
        On("DeityButton", () => ClassicPopup.Edit(this, A, A.Tlk(8156), _n.Deity, t => _n.Deity = t.Trim(), null, 64));
        On("OkButton", () =>
        {
            _n.FirstName = _n.FirstName.Trim();
            _n.LastName = _n.LastName.Trim();
            if (_n.FirstName.Length == 0) return;
            Taken(MainStep.Customize);
        });
        On("CancelButton", Abandon);
    }

    void GenerateName()
    {
        var race = Model?.Races.GetValueOrDefault(_n.Race);
        var tables = race?.Raw.TryGetProperty("nameGen", out var ng) == true ? ng.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList() : new List<string>();
        var (f, l) = CgNames.Character(A.Content, tables, _n.Gender);
        _n.FirstName = f;
        _n.LastName = l;
    }
}

/// <summary>A colour swatch (ctl_cg_btn_col / _colw): the chosen palette cell drawn into the model's gui_replace01 plane.</summary>
public partial class CgSwatch : GuiButton
{
    public int Channel;
    public string Texture_ = "";
    public Func<int> Value = () => 0;

    public override void _Draw()
    {
        if (Model == null) return;
        var pose = Model.Rest;
        GuiDraw.Model(this, Assets, pose, Origin, Colors.White, skip: m => m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase));
        if (Assets.Texture(Texture_) is not { } tex) return;
        int v = Value();
        var cell = new Rect2(v % 16 * 16, v / 16 * 16, 16, 16);
        foreach (var m in pose.Meshes)
        {
            if (!m.Texture.Equals("gui_replace01", StringComparison.OrdinalIgnoreCase)) continue;
            var r = new Rect2(m.Points[0], Vector2.Zero);
            foreach (var pt in m.Points) r = r.Expand(pt);
            DrawTextureRectRegion(tex, new Rect2(r.Position + Origin, r.Size), cell);
        }
    }
}

/// <summary>
/// The portrait grid of cg_portrait: ctl_cg_portraits with the portraits' small pictures (64 x 100, the "s" size's
/// picture part drawn at the medium size) six a row 72 px apart, rows 107 px apart from its nextrow node, a light
/// frame round the chosen one, the original's scroll bar at its right (c07, c08).
/// </summary>
public partial class CgPortraitGrid : Control
{
    public ClassicAssets Assets = null!;
    public Action<int>? Picked;
    GuiModelView _frame = null!;
    ClassicScrollBar _bar = null!;
    readonly Control _clip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
    List<(int Id, string ResRef)> _items = new();
    int _selected = -1, _topRow;
    Vector2 _first;
    const int Columns = 6;
    const float Dx = 72, Dy = 107, W = 64, H = 100;

    public void Place(Vector2 origin)
    {
        MouseFilter = MouseFilterEnum.Pass;
        _frame = GuiModelView.Create(Assets, "ctl_cg_portraits");
        AddChild(_frame);
        Position = origin - _frame.Origin;
        Size = _frame.Size;
        var top = _frame.Node("top") ?? new Vector2(Size.X - 20, 3);
        var bottom = _frame.Node("bottom") ?? new Vector2(5, Size.Y - 3);
        _clip.Position = new Vector2(bottom.X, top.Y);
        _clip.Size = new Vector2(top.X - bottom.X, bottom.Y - top.Y);
        AddChild(_clip);
        var next = _frame.Node("nextrow") ?? new Vector2(6, 106);
        _first = new Vector2(next.X - _clip.Position.X + 1, next.Y - H - _clip.Position.Y - 2);
        var text = new Rect2(_clip.Position, _clip.Size);
        var bar = CgHelp.BarRect(Assets, _frame, text);
        _bar = new ClassicScrollBar { Assets = Assets, Position = bar.Position, Size = bar.Size, Scrolled = o => { _topRow = (int)MathF.Round(o / Dy); Layout(); } };
        AddChild(_bar);
    }

    public void SetPortraits(List<(int Id, string ResRef)> items, int selected)
    {
        bool same = items.Count == _items.Count && items.SequenceEqual(_items);
        _items = items.ToList();
        _selected = selected;
        if (!same) Rebuild();
        else foreach (var c in _clip.GetChildren().OfType<Thumb>()) c.QueueRedraw();
    }

    void Rebuild()
    {
        foreach (var c in _clip.GetChildren()) c.QueueFree();
        foreach (var (id, resref) in _items)
        {
            var t = new Thumb { Grid = this, Id = id, Texture_ = ChargenModel.Portrait(Assets.Content, resref, "m"), Size = new Vector2(W, H) };
            _clip.AddChild(t);
        }
        _topRow = 0;
        Layout();
    }

    void Layout()
    {
        int i = 0;
        foreach (var t in _clip.GetChildren().OfType<Thumb>())
        {
            int row = i / Columns, col = i % Columns;
            t.Position = _first + new Vector2(col * Dx, (row - _topRow) * Dy);
            t.Visible = row >= _topRow - 1 && row <= _topRow + 4;
            i++;
        }
        int rows = (_items.Count + Columns - 1) / Columns;
        _bar.Total = rows * Dy;
        _bar.Page = MathF.Floor(_clip.Size.Y / Dy) * Dy;
        _bar.Step = Dy;
        _bar.Offset = _topRow * Dy;
        _bar.QueueRedraw();
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w)
        {
            int rows = (_items.Count + Columns - 1) / Columns;
            _topRow = Math.Clamp(_topRow + (w.ButtonIndex == MouseButton.WheelUp ? -1 : 1), 0, Math.Max(0, rows - (int)(_clip.Size.Y / Dy)));
            Layout();
            AcceptEvent();
        }
    }

    partial class Thumb : Control
    {
        public CgPortraitGrid Grid = null!;
        public int Id;
        public Texture2D? Texture_;

        public override void _Ready() { MouseFilter = MouseFilterEnum.Stop; }

        public override void _Draw()
        {
            if (Texture_ != null) DrawTextureRect(Texture_, new Rect2(Vector2.Zero, Size), false);
            if (Id == Grid._selected) DrawRect(new Rect2(Vector2.Zero, Size).Grow(-0.5f), new Color(0.85f, 0.85f, 0.8f), false, 2);
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                ClassicSounds.Play(Grid.Assets, "gui_select");
                Grid._selected = Id;
                foreach (var t in Grid._clip.GetChildren().OfType<Thumb>()) t.QueueRedraw();
                Grid.Picked?.Invoke(Id);
                AcceptEvent();
            }
            else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown }) Grid._GuiInput(e);
        }
    }
}
