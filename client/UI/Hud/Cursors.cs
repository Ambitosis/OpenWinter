using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>
/// Context cursors: a brass pointer normally; speech over creatures you can talk to, swords over
/// hostile targets, a hand over usable objects, a door over doors and transitions, a bag over
/// items on the ground, and a crosshair while a spell or script wants a target.
/// </summary>
public partial class Cursors : Node
{
    internal static int DoorCursor(ObjectFlags flags) => (flags & (ObjectFlags.Open | ObjectFlags.Transition))
        == (ObjectFlags.Open | ObjectFlags.Transition) ? Original.Transition : Original.Door;
    public ClientSession Session = null!;
    public Interaction Interaction = null!;
    public HudState State = null!;

    enum Kind { Pointer, Talk, Attack, Use, Door, Transition, Pickup, Target, NoWalk }
    readonly Dictionary<Kind, Texture2D> _images = new();

    const string Outline = "#0b0c10";
    static string Brass => Palette.Hex(Palette.AccentBright);

    public override void _Ready()
    {
        _images[Kind.Pointer] = Pointer();
        _images[Kind.NoWalk] = Svg($"<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'><path d='M4 4L23 23M23 4L4 23' stroke='{Outline}' stroke-width='6'/><path d='M4 4L23 23M23 4L4 23' stroke='{Brass}' stroke-width='3'/></svg>");
        _images[Kind.Talk] = Badge("chat");
        _images[Kind.Attack] = Badge("combat", Palette.Danger.Lightened(0.15f));
        _images[Kind.Use] = Badge("magic");
        _images[Kind.Door] = Badge("key");
        _images[Kind.Transition] = Badge("exit");
        _images[Kind.Pickup] = Badge("inventory");
        _images[Kind.Target] = Svg($"<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'><circle cx='16' cy='16' r='9' fill='none' stroke='{Outline}' stroke-width='4'/><circle cx='16' cy='16' r='9' fill='none' stroke='{Brass}' stroke-width='2'/><path d='M16 1v8M16 23v8M1 16h8M23 16h8' stroke='{Outline}' stroke-width='4'/><path d='M16 2v7M16 23v7M2 16h7M23 16h7' stroke='{Brass}' stroke-width='2'/><circle cx='16' cy='16' r='1.6' fill='{Brass}'/></svg>");
    }

    /// <summary>The brass arrow used everywhere (menus included; Main installs it at startup).</summary>
    public static Texture2D Pointer() => Svg($"<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'><path d='M3 2 L3 24 L9 18.5 L13 28 L17 26.3 L13 17 L21 17 Z' fill='{Brass}' stroke='{Outline}' stroke-width='1.6' stroke-linejoin='round'/><path d='M5.2 6.5 L5.2 19 L9.4 15 L16 15 Z' fill='#ffffff' fill-opacity='0.25'/></svg>");

    /// <summary>
    /// The cursor outside the game world (menus, windows): the original's gauntlet once the game data is
    /// open and Options, Interface, Original cursors is on (as by default), else the brass arrow. Buttons
    /// keep it too, as in the original.
    /// </summary>
    public static void InstallPointer()
    {
        if (Settings.Current.Interface.OriginalCursors && Original.Texture(FrontEnd.FrontEndContent.Base, Original.Default) is { } tex)
        {
            Input.SetCustomMouseCursor(tex, Input.CursorShape.Arrow, Original.Hotspot(Original.Default, tex));
            Input.SetCustomMouseCursor(tex, Input.CursorShape.PointingHand, Original.Hotspot(Original.Default, tex));
            return;
        }
        Input.SetCustomMouseCursor(Pointer(), Input.CursorShape.Arrow, new Vector2(3, 2));
        Input.SetCustomMouseCursor(null, Input.CursorShape.PointingHand);
    }

    /// <summary>The pointer with a small icon badge at its lower right.</summary>
    Texture2D Badge(string icon, Color? tint = null)
    {
        var img = new Image();
        img.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='32' height='32'><path d='M3 2 L3 20 L8 15.5 L11 22 L14 20.6 L11 14 L17.5 14 Z' fill='{Brass}' stroke='{Outline}' stroke-width='1.5' stroke-linejoin='round'/></svg>", 1);
        var path = "res://Assets/Icons/" + icon + ".svg";
        if (Godot.FileAccess.FileExists(path))
        {
            var glyph = new Image();
            // Dark disc behind the glyph so it reads on any background.
            var disc = new Image();
            disc.LoadSvgFromString($"<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18'><circle cx='9' cy='9' r='8.4' fill='{Outline}' fill-opacity='0.85' stroke='{Brass}' stroke-width='1'/></svg>", 1);
            img.BlendRect(disc, new Rect2I(0, 0, 18, 18), new Vector2I(14, 14));
            if (glyph.LoadSvgFromString(Godot.FileAccess.GetFileAsString(path), 12f / 512f) == Error.Ok)
            {
                var c = tint ?? Palette.Text;
                for (int y = 0; y < glyph.GetHeight(); y++)
                    for (int x = 0; x < glyph.GetWidth(); x++)
                    {
                        var p = glyph.GetPixel(x, y);
                        glyph.SetPixel(x, y, new Color(c.R, c.G, c.B, p.A));
                    }
                img.BlendRect(glyph, new Rect2I(0, 0, glyph.GetWidth(), glyph.GetHeight()), new Vector2I(17, 17));
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    static Texture2D Svg(string svg)
    {
        var img = new Image();
        img.LoadSvgFromString(svg, 1);
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>MOUSECURSOR_* overrides from SetObjectMouseCursor, by object id.</summary>
    readonly Dictionary<uint, int> _overrides = new();
    int _shown = -1;
    TextureRect? _capturePointer;
    public int CurrentCursorId { get; private set; } = Original.Default;
    /// <summary>While a script's EnterTargetingMode is active: the cursors for valid and invalid targets and the OBJECT_TYPE_* mask.</summary>
    public static int? InterfaceCursor;
    public static int TargetCursor = Original.Magic, BadTargetCursor = Original.NoMagic, TargetTypes = 32767;

    public override void _EnterTree() => Session.NotifyReceived += OnNotify;

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "mouse-cursor") return;
        using var doc = System.Text.Json.JsonDocument.Parse(m.Json);
        int id = doc.RootElement.TryGetProperty("cursor", out var c) ? c.GetInt32() : -1;
        if (id >= 0) _overrides[m.Subject] = id; else _overrides.Remove(m.Subject);
    }

    public override void _Process(double delta)
    {
        bool original = Settings.Current.Interface.OriginalCursors;
        int id = Original.Default;
        bool overGui = Interaction.PointerOverride == null && GetViewport().GuiGetHoveredControl() is not null;
        var h = overGui ? null : Interaction.Hovered;
        if (h != null && h.ObjectId == Session.ControlledId) h = null;
        if (Interaction.Targeting == null) (TargetCursor, BadTargetCursor, TargetTypes) = (Original.Magic, Original.NoMagic, 32767);
        // A corpse that can't be selected lights up, but a targeting click goes through it to the ground.
        else id = h == null || !Interaction.HoveredSelectable || Accepts(h) ? TargetCursor : BadTargetCursor;
        if (Interaction.Targeting != null) { }
        else if (!overGui && Interaction.DriveCursor is { } drive) id = drive;
        else if (h != null && _overrides.TryGetValue(h.ObjectId, out var custom)) id = custom;
        else if (h != null)
        {
            id = h.Kind switch
            {
                ObjectKind.Creature when (h.Flags & ObjectFlags.Dead) != 0 => Original.Default,
                // A click on a hostile creature attacks it (the server decides the same way).
                ObjectKind.Creature when Interaction.Reaction(h) == Interaction.Hostile => Original.Attack,
                ObjectKind.Creature => Original.Talk,
                ObjectKind.Door => DoorCursor(h.Flags),
                ObjectKind.Item => Original.Pickup,
                ObjectKind.Placeable => Original.Use,
                _ => Original.Default,
            };
        }
        else if (!overGui && !Interaction.Suspended && Session.Controlled != null && !Interaction.GroundWalkable) id = Original.NoWalk;
        if (InterfaceCursor is { } guiCursor) id = guiCursor;
        // The original shows each cursor's pressed version while the button is held.
        bool hasPressed = (id & 1) == 1 && (id < 59 || id > 90 && id < 293);
        if (original && hasPressed && Input.IsMouseButtonPressed(MouseButton.Left)) id++;
        // Native screenshots include its software pointer; virtual-input captures need the same
        // observable cursor. Only the harness supplies PointerOverride; the OS mouse stays untouched.
        if (Interaction.PointerOverride is { } point)
        {
            if (_capturePointer == null)
            {
                var layer = new CanvasLayer { Layer = 200 };
                AddChild(layer);
                _capturePointer = new TextureRect { MouseFilter = Control.MouseFilterEnum.Ignore };
                layer.AddChild(_capturePointer);
            }
            var texture = Original.Texture(Session.Content, id);
            _capturePointer.Texture = texture;
            _capturePointer.Size = texture?.GetSize() ?? Vector2.Zero;
            _capturePointer.Position = point - (texture == null ? Vector2.Zero : Original.Hotspot(id, texture));
            _capturePointer.Visible = true;
        }
        else if (_capturePointer != null) _capturePointer.Visible = false;
        CurrentCursorId = id;
        int key = (original ? 1000 : 0) + id;
        if (key == _shown) return;
        _shown = key;
        Show(id, original || id is >= 59 and <= 90);
    }

    /// <summary>Whether the hovered object is one the active targeting mode accepts.</summary>
    static bool Accepts(Views.ObjectView v)
    {
        int bit = v.Kind switch
        {
            ObjectKind.Creature => 1,
            ObjectKind.Item => 2,
            ObjectKind.Door => 8,
            ObjectKind.Placeable => 64,
            _ => 0,
        };
        return bit == 0 || (TargetTypes & bit) != 0;
    }

    void Show(int id, bool original)
    {
        if (original && Original.Texture(Session.Content ?? FrontEnd.FrontEndContent.Base, id) is { } tex)
        {
            Input.SetCustomMouseCursor(tex, Input.CursorShape.Arrow, Original.Hotspot(id, tex));
            return;
        }
        var kind = id switch
        {
            Original.Attack or Original.Attack + 1 => Kind.Attack,
            Original.NoWalk or Original.NoWalk + 1 => Kind.NoWalk,
            Original.Talk or Original.Talk + 1 => Kind.Talk,
            Original.Use or Original.Use + 1 => Kind.Use,
            Original.Door or Original.Door + 1 => Kind.Door,
            Original.Transition or Original.Transition + 1 => Kind.Transition,
            Original.Pickup or Original.Pickup + 1 => Kind.Pickup,
            Original.Magic or Original.Magic + 1 or Original.NoMagic or Original.NoMagic + 1 => Kind.Target,
            _ => Kind.Pointer,
        };
        Input.SetCustomMouseCursor(_images[kind], Input.CursorShape.Arrow, kind == Kind.Target ? new Vector2(16, 16) : new Vector2(3, 2));
    }

    /// <summary>
    /// The original game's cursors: the gui_mp_* textures (a module's haks can replace them), named by
    /// MOUSECURSOR_* id as nwscript.nss lists them. Odd ids are the normal cursors, the even id after
    /// each its pressed version.
    /// </summary>
    public static class Original
    {
        public const int Default = 1, Walk = 3, NoWalk = 5, Attack = 7, Talk = 11, Transition = 21, Door = 23, Use = 25, Magic = 29, NoMagic = 31, Pickup = 91;

        static readonly string[] Stems =
        {
            "default", "walk", "nowalk", "attack", "noatck", "talk", "notalk", "follow", "examine", "noexam", "trans", "door", "use", "nouse",
            "magic", "nomagic", "disarm", "nodisarm", "action", "noaction", "lock", "nolock", "pushpin", "create", "nocreat", "kill", "nokill", "heal", "noheal",
        };

        /// <summary>The texture resref of a MOUSECURSOR_* id, or null for an unknown id.</summary>
        public static string? ResRef(int id) => id switch
        {
            >= 1 and <= 58 => "gui_mp_" + Stems[(id - 1) / 2] + ((id & 1) == 1 ? "u" : "d"),
            >= 59 and <= 74 => $"gui_mp_arrun{id - 59:00}",
            >= 75 and <= 90 => $"gui_mp_arwalk{id - 75:00}",
            91 => "gui_mp_pickupu",
            92 => "gui_mp_pickupd",
            >= 93 and <= 292 => $"gui_mp_custom{(id - 93) / 2:00}" + ((id & 1) == 1 ? "u" : "d"),
            _ => null,
        };

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Views.ClientContent, Dictionary<int, Texture2D?>> Cache = new();

        /// <summary>A cursor's image, upright and scaled with the interface (null when the game data lacks it).</summary>
        public static Texture2D? Texture(Views.ClientContent? content, int id)
        {
            if (content == null || ResRef(id) is not { } resref) return null;
            var cache = Cache.GetValue(content, _ => new Dictionary<int, Texture2D?>());
            if (cache.TryGetValue(id, out var cached)) return cached;
            Texture2D? result = null;
            if (content.Textures.Get(resref) is { } e && e.Texture.GetImage() is { } image)
            {
                if (image.IsCompressed()) image.Decompress();
                image.ClearMipmaps();
                if (e.FlipV) image.FlipY();
                image.Convert(Image.Format.Rgba8);
                int size = (int)MathF.Round(image.GetWidth() * Scale);
                if (size != image.GetWidth()) image.Resize(size, size * image.GetHeight() / image.GetWidth(), Image.Interpolation.Bilinear);
                result = ImageTexture.CreateFromImage(image);
            }
            return cache[id] = result;
        }

        /// <summary>Cursor size multiplier: the interface scale (bigger on large screens).</summary>
        static float Scale
        {
            get
            {
                var tree = Engine.GetMainLoop() as SceneTree;
                int height = tree?.Root.Size.Y ?? 1080;
                return Math.Clamp(Settings.Current.Interface.EffectiveScale(height), 1f, 2f);
            }
        }

        /// <summary>The click point: every cursor's tip is at its top-left (the magic orb's spike too); the direction arrows click at their middle.</summary>
        public static Vector2 Hotspot(int id, Texture2D tex) => id is >= 59 and <= 90 ? tex.GetSize() / 2 : Vector2.Zero;
    }

    /// <summary>Writes every cursor to PNGs side by side (capture runs; screenshots don't include the cursor).</summary>
    public void SaveSheet(string path)
    {
        // Top row: the modern set; bottom row: the original's (normal and pressed default, then each kind).
        int[] originals = { 1, 2, 3, 5, 7, 8, 11, 21, 23, 25, 29, 31, 47, 91, 59, 75 };
        int columns = Math.Max(_images.Count, originals.Length);
        var sheet = Image.CreateEmpty(64 * columns, 128, false, Image.Format.Rgba8);
        sheet.Fill(new Color(0.35f, 0.4f, 0.35f));
        void Put(Texture2D tex, int column, int row)
        {
            var img = tex.GetImage();
            img.Resize(64, 64, Image.Interpolation.Nearest);
            sheet.BlendRect(img, new Rect2I(0, 0, 64, 64), new Vector2I(column * 64, row * 64));
        }
        int i = 0;
        foreach (var tex in _images.Values) Put(tex, i++, 0);
        i = 0;
        foreach (var id in originals)
        {
            if (Original.Texture(Session.Content ?? FrontEnd.FrontEndContent.Base, id) is { } tex) Put(tex, i, 1);
            i++;
        }
        sheet.SavePng(path);
    }

    public override void _ExitTree()
    {
        Session.NotifyReceived -= OnNotify;
        InstallPointer();
    }
}
