using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Aurora.Textures;
using OpenWinter.Client.Rendering;

namespace OpenWinter.Client.Views;

/// <summary>The client's view of game content: resources plus model/texture/material caches.</summary>
public sealed class ClientContent
{
    public ResMan Resources { get; }
    public ModelCache Models { get; }
    public TextureFactory Textures { get; }
    public MaterialFactory Materials { get; }
    public PltTinter Tinter { get; }
    public Audio.AudioLoader Audio { get; }

    public ClientContent(ResMan rm)
    {
        Resources = rm;
        Models = new ModelCache(rm) { Problem = (resref, why) => WarnOnce($"model {resref} could not be read ({why})") };
        Textures = new TextureFactory(new TextureCache(rm));
        Materials = new MaterialFactory(Textures);
        Materials.Models = Models.Get;
        Materials.Shaders = new Rendering.Shaders.ModuleShaders(rm, Textures);
        Tinter = new PltTinter(Textures);
        Materials.Shaders.PaletteAlpha = Tinter.WithAlpha;
        Audio = new Audio.AudioLoader(rm);
    }

    readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _warned = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Logs a content problem (missing or unreadable model, ...) the first time it is seen.</summary>
    public void WarnOnce(string message)
    {
        if (_warned.TryAdd(message, 0)) Godot.GD.PushWarning("[content] " + message);
    }
}
