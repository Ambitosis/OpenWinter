using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI.Classic.Creation;

/// <summary>
/// Hooks the classic character creation and level-up into a game: with the classic interface style (Options →
/// Interface, or --ui classic) the character selection's New Character opens <see cref="ClassicCharGen"/> and the
/// level-up buttons open <see cref="ClassicLevelUp"/>; with the modern style the modern windows open as before. The
/// style is read when the button is pressed, so switching it in play takes effect at once.
/// </summary>
public static class ClassicCreation
{
    public static ClassicCharGen? CharGen { get; private set; }
    public static ClassicLevelUp? LevelUp { get; private set; }

    /// <summary>Adds the classic windows to the HUD and routes New Character through them in classic mode.</summary>
    public static void Wire(Hud hud, ClientSession session, CharacterSelectPanel select, CharGen.CharGenWindow modern, bool singlePlayer)
    {
        CharGen = ClassicCharGen.Create(session);
        hud.AddChild(CharGen);
        CharGen.Cancelled = () => { if (session.ControlledId == 0) select.Reopen(); };
        LevelUp = ClassicLevelUp.Create(session, singlePlayer);
        hud.AddChild(LevelUp);
        select.NewCharacterRequested = () =>
        {
            if (Settings.Current.Interface.Classic && session.Content != null) CharGen.Open();
            else modern.Open();
        };
    }

    /// <summary>Opens the level-up window of the interface in use.</summary>
    public static void OpenLevelUp(Action modern)
    {
        if (Settings.Current.Interface.Classic && LevelUp != null && GodotObject.IsInstanceValid(LevelUp)) LevelUp.Open();
        else modern();
    }
}
