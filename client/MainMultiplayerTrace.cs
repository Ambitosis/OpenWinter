using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// The multiplayer sync harness (tools/multiplayer, docs/MULTIPLAYER_HARNESS.md): <c>--mp-trace dir</c> records what
    /// this client shows (Gameplay/MultiplayerTrace.cs) and <c>--mp-character file</c> picks that server-vault character
    /// as soon as the list arrives, so a scripted run needs no clicks.
    /// </summary>
    void StartMultiplayerTrace(string dir, Views.EffectPlayer effects, UI.HudParts.HudState hud)
    {
        AddChild(new Gameplay.MultiplayerTrace
        {
            Name = "MultiplayerTrace", Session = _session, Interaction = _interaction, Effects = effects, Hud = hud,
            Directory = System.IO.Path.GetFullPath(dir), Quit = () => GetTree().Quit(), Screenshots = !_args.ContainsKey("mp-no-shots"),
        });
        if (_args.TryGetValue("mp-character", out var character))
            _session.CharacterListReceived += list =>
            {
                var pick = list.Characters.FirstOrDefault(c => c.File.Equals(character, StringComparison.OrdinalIgnoreCase)) ?? list.Characters.FirstOrDefault();
                if (pick == null) { GD.PrintErr($"[mp] no character {character} in the server vault"); return; }
                _session.Send(new CharacterChoiceMessage { File = pick.File }.Write());
                GD.Print($"[mp] chose {pick.File} ({pick.Name})");
                // The selection screen opened for the same list: close it as its Play button would.
                Callable.From(() =>
                {
                    foreach (var panel in FindChildren("*", "", true, false).OfType<UI.CharacterSelectPanel>()) panel.Visible = false;
                }).CallDeferred();
            };
    }
}
