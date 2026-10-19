using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original interface's sounds (gui_button, gui_check, gui_open ... in the game's data, so haks can replace them),
/// played on the Interface bus. A few players are kept so quick clicks don't cut each other off.
/// </summary>
public static class ClassicSounds
{
    public const string Button = "gui_button", Check = "gui_check";
    static readonly List<AudioStreamPlayer> Players = new();
    static readonly Dictionary<string, AudioStream?> Streams = new(StringComparer.OrdinalIgnoreCase);
    static Views.ClientContent? _content;

    public static void Play(ClassicAssets assets, string resref)
    {
        if (string.IsNullOrEmpty(resref) || Engine.GetMainLoop() is not SceneTree tree) return;
        // Streams come from the current content (a game's, with its haks; the base game's at the menu).
        if (!ReferenceEquals(_content, assets.Content)) { Streams.Clear(); _content = assets.Content; }
        if (!Streams.TryGetValue(resref, out var stream)) Streams[resref] = stream = assets.Content.Audio.Get(resref);
        if (stream == null) return;
        Players.RemoveAll(p => !GodotObject.IsInstanceValid(p));
        var player = Players.FirstOrDefault(p => !p.Playing);
        if (player == null)
        {
            if (Players.Count >= 4) player = Players[0];
            else
            {
                player = new AudioStreamPlayer { Name = "ClassicSound", Bus = Preferences.AudioBuses.Interface };
                tree.Root.CallDeferred(Node.MethodName.AddChild, player);
                Players.Add(player);
            }
        }
        player.Stream = stream;
        if (player.IsInsideTree()) player.Play();
        else { var p = player; p.Ready += () => p.Play(); }
    }
}
