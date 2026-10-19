using OpenWinter.Client.UI.Classic.Sheets;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>The installed player timed-event panel, driven by the same rest state as the modern HUD.</summary>
public partial class ClassicRest : Control
{
    public static ClassicRest? Current { get; private set; }
    public ClassicContext Ctx = null!;
    public PlayerFx Fx = null!;
    public GuiPanel Panel { get; private set; } = null!;
    RestFill _fill = null!;
    public float Progress => Fx.RestProgress;

    public static void Attach(ClassicContext ctx)
    {
        if (ctx.Main.GetChildren().OfType<PlayerFx>().FirstOrDefault() is not { } fx) return;
        var rest = new ClassicRest { Ctx = ctx, Fx = fx, Name = "ClassicRest" };
        ctx.Hud.AddChild(rest);
        Current = rest;
        fx.UseClassicRest(true);
    }

    public static void Detach()
    {
        if (Current is { } rest && IsInstanceValid(rest))
        {
            if (IsInstanceValid(rest.Fx)) rest.Fx.UseClassicRest(false);
            rest.QueueFree();
        }
        Current = null;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Panel = GuiPanel.FromModel(Ctx.Assets, "pnl_plrtimedevnt");
        AddChild(Panel);
        Size = Panel.Size;
        var labelBox = SheetKit.NodeBox(Panel.Frame, "text0", "text1") ?? new Rect2(3, 3, 122, 34);
        SheetKit.Text(Panel, Ctx.Assets, labelBox, Ctx.Assets.Tlk(66925), alignH: .5f);
        var cancel = GuiButton.Create(Ctx.Assets, "ctl_btn_x");
        cancel.PlaceOrigin(Panel.Node("CancelButton") ?? new Vector2(109, 58));
        cancel.SetMeta(ClassicTooltip.Meta, "Cancel rest");
        cancel.Pressed += () => Ctx.Session.Request("clear-actions");
        Panel.AddChild(cancel);
        Panel.Register("CancelButton", cancel);
        var status = Panel.Node("Status") ?? new Vector2(4, 42);
        _fill = new RestFill
        {
            Assets = Ctx.Assets, Position = status,
            Size = new Vector2(cancel.Position.X - status.X, cancel.Size.Y - 3),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        Panel.AddChild(_fill);
        Visible = Fx.RestActive;
        Place();
    }

    // Native 1280x720: the model origin is screen center; its art sits at y270..330.
    void Place() => Position = (GetViewportRect().Size / 2 - Panel.ModelOrigin).Round();

    public override void _Process(double delta)
    {
        Visible = Fx.RestActive;
        if (!Visible) return;
        Place();
        _fill.Fraction = Fx.RestProgress;
        _fill.QueueRedraw();
    }

    partial class RestFill : Control
    {
        public ClassicAssets Assets = null!;
        public float Fraction;
        public override void _Draw()
        {
            if (Fraction <= 0 || Assets.Model("ctl_timedevntbar")?.Rest.Meshes.FirstOrDefault() is not { } mesh
                || Assets.Texture(mesh.Texture) is not { } texture) return;
            // The percentage model's rest geometry is collapsed. Its authored blue atlas strip
            // is stretched into the panel's Status region by the original timed-event control.
            var min = mesh.Uvs.Aggregate(new Vector2(float.MaxValue, float.MaxValue), (a, b) => a.Min(b));
            var max = mesh.Uvs.Aggregate(new Vector2(float.MinValue, float.MinValue), (a, b) => a.Max(b));
            var source = new Rect2(min * texture.GetSize(), (max - min) * texture.GetSize());
            DrawTextureRectRegion(texture, new Rect2(Vector2.Zero, new Vector2(Size.X * Fraction, Size.Y)), source);
        }
    }
}
