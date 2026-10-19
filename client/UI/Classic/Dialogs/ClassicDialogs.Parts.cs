using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The classic conversation box, game menu, message boxes and death panel. The modern conversation panel keeps
/// its logic (keys, camera, replies) but is not drawn; the modern game menu and death panel are not opened.
/// </summary>
public static partial class ClassicDialogs
{
    public static ClassicConversation? Conversation { get; private set; }
    public static ClassicGameMenu? Menu { get; private set; }
    public static ClassicDeath? Death { get; private set; }
    /// <summary>The layer the menu, popups and death panel live on (above the HUD and the conversation).</summary>
    public static CanvasLayer? Layer { get; private set; }

    static partial void AttachParts(ClassicContext ctx)
    {
        bool singlePlayer = ctx.Input.SinglePlayer;

        // Conversation: on the conversation's own layer (above the HUD and script fades, shown in cutscenes).
        foreach (var child in ctx.Dialog.GetChildren().OfType<CanvasItem>()) child.Visible = false;
        Conversation = new ClassicConversation { Ctx = ctx, Name = "ClassicConversation" };
        (ctx.Dialog.GetParent() ?? ctx.Hud).AddChild(Conversation);

        Layer = new CanvasLayer { Layer = 10, Name = "ClassicDialogs" };
        ctx.Main.AddChild(Layer);
        var host = new Control { Name = "Host", MouseFilter = Control.MouseFilterEnum.Ignore };
        host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        Layer.AddChild(host);

        Death = new ClassicDeath { Ctx = ctx, SinglePlayer = singlePlayer, Name = "ClassicDeath" };
        host.AddChild(Death);
        if (ctx.Main.GetChildren().OfType<PlayerFx>().FirstOrDefault() is { } fx) fx.DeathPanel = Death.Show;

        Menu = new ClassicGameMenu { Ctx = ctx, SinglePlayer = singlePlayer, Name = "ClassicGameMenu" };
        host.AddChild(Menu);
        ctx.Input.OpenGameMenu = Menu.Toggle;
        ctx.Hud.OpenMenu = Menu.Toggle;
        // ActivatePortal's prompt (ingame_portal) while this interface is up.
        PortalPrompt.ClassicView = () => ClassicPortal.Create(host, ctx.Assets);
    }

    static partial void DetachParts(ClassicContext ctx)
    {
        // An open game menu gives back the HUD and the keyboard first.
        if (Menu is { } m && GodotObject.IsInstanceValid(m) && m.Visible) m.Close();
        if (Conversation is { } c && GodotObject.IsInstanceValid(c)) c.QueueFree();
        if (Layer is { } l && GodotObject.IsInstanceValid(l)) l.QueueFree();
        Conversation = null; Menu = null; Death = null; Layer = null;
        PortalPrompt.ClassicView = null;
    }

    /// <summary>A message box over the game (the classic layer); null before a game.</summary>
    public static Control? PopupHost => Layer?.GetChildOrNull<Control>(0);
}
