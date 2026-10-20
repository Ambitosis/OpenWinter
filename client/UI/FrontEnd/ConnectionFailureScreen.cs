using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>Connection errors cover both loading-screen styles, with an immediate way back.</summary>
public partial class ConnectionFailureScreen : CanvasLayer
{
    public IConnection Connection = null!;
    public Action? Back;
    public bool Showing { get; private set; }

    public override void _Ready() => Layer = 70;

    public override void _Process(double delta)
    {
        if (Showing || Connection is not NetClientConnection { FailureReason: { } reason } net) return;
        Showing = true;
        GD.Print($"[client] connection failed: {reason}");
        var backdrop = new ColorRect { Color = Palette.Bg0, MouseFilter = Control.MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(backdrop);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.AddChild(center);
        var card = new PanelContainer { ThemeTypeVariation = "CardPanel", CustomMinimumSize = new Vector2(640, 0) };
        center.AddChild(card);
        var column = Ui.VBox(Palette.Space6);
        card.AddChild(column);
        column.AddChild(Ui.Text(net.Joined ? "Disconnected from server" : "Unable to join server", Ui.TextStyle.Title, Palette.Accent));
        var message = Ui.Text(reason, Ui.TextStyle.Body);
        message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        message.CustomMinimumSize = new Vector2(600, 0);
        column.AddChild(message);
        var back = Ui.Primary("Back to the main menu", () => Back?.Invoke());
        back.TooltipText = "Leave this connection and return to the main menu.";
        column.AddChild(back);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Showing || !e.IsActionPressed("ui_cancel")) return;
        GetViewport().SetInputAsHandled();
        Back?.Invoke();
    }
}
