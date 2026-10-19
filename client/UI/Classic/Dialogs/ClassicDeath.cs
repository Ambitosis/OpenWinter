using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's death panel (dth_deathopts), centred: "You Are Dead", the message, and Load Game, Respawn,
/// Exit Game and Wait For Help. As the original: single player offers Load Game and no waiting for help (its
/// message starts "Choose an option below"); multiplayer offers Wait For Help. Shown for PopUpDeathGUIPanel and
/// PopUpGUIPanel(GUI_PANEL_PLAYER_DEATH); hidden again once the player is alive.
/// </summary>
public partial class ClassicDeath : Control
{
    public ClassicContext Ctx = null!;
    public GuiPanel Panel = null!;
    public bool SinglePlayer;
    string _defaultMessage = "";
    bool _sawDead;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        Panel = GuiPanel.FromLayout(Ctx.Assets, "dth_deathopts") ?? GuiPanel.FromModel(Ctx.Assets, "dth_deathopts");
        AddChild(Panel);
        _defaultMessage = Panel.Get<GuiLabel>("Message")?.Text ?? "";
        if (Panel.Get<GuiLabel>("Message") is { } msg) msg.Wrap = true;
        Resized += Center;
        Panel.ButtonPressed += tag =>
        {
            switch (tag)
            {
                case "RespawnButton": Ctx.Session.Request("respawn"); Visible = false; break;
                case "WaitButton": Visible = false; break;
                case "LoadButton": Ctx.GameMenu?.LoadGame?.Invoke(); break;
                case "ExitButton":
                    ClassicPopup.OkCancel(this, Ctx.Assets, SinglePlayer ? "Exit to the main menu? Progress since your last save will be lost." : "Exit to the main menu? You will leave the server.",
                        () => Ctx.Main.ExitToMainMenu());
                    break;
            }
        };
    }

    void Center() => ClassicPopup.CenterOn(Panel, Size);

    /// <summary>Shows the panel; <paramref name="help"/> replaces the message when given.</summary>
    public void Show(bool respawn, bool waitForHelp, string help)
    {
        string message = help.Length > 0 ? help : _defaultMessage;
        // Single player has nobody to wait for: the original's own message drops that sentence.
        if (help.Length == 0 && SinglePlayer)
        {
            const string wait = "Wait for help from another player or choose";
            if (message.StartsWith(wait, StringComparison.OrdinalIgnoreCase)) message = "Choose" + message[wait.Length..];
        }
        if (Panel.Get<GuiLabel>("Message") is { } m) m.Text = message;
        _sawDead = false;
        Button("RespawnButton", respawn);
        Button("WaitButton", waitForHelp && !SinglePlayer);
        Button("LoadButton", SinglePlayer);
        Button("ExitButton", true);
        Visible = true;
        GetParent()?.MoveChild(this, -1);
        Center();
    }

    void Button(string tag, bool shown)
    {
        if (Panel.Get<GuiButton>(tag) is { } b) b.Visible = shown;
    }

    public override void _Process(double delta)
    {
        // Gone once the player is alive again (raised or respawned); a script may show it to the living too.
        if (!Visible || Ctx.Session.Controlled is not { } me) return;
        bool dead = (me.Flags & Net.ObjectFlags.Dead) != 0;
        if (dead) _sawDead = true;
        else if (_sawDead) { Visible = false; _sawDead = false; }
    }
}
