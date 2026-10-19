using System.Numerics;
using OpenWinter.Content;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Authored decoration placement, separate from the beam's endpoint geometry.</summary>
public static class BeamModelLayout
{
    public readonly record struct Decoration(EmitterData Data, Matrix4x4 Transform, bool Targeted = false);

    public static IEnumerable<Decoration> Decorations(ModelData model)
    {
        var transforms = model.RestGlobals();
        for (int i = 0; i < model.Parts.Count; i++)
            if (model.Parts[i].Emitter is { } emitter && !emitter.Is("Lightning"))
                yield return new Decoration(emitter, transforms[i], CanTarget(model, i));
    }

    /// <summary>Measured reattachable Lightning references belong at the target endpoint.</summary>
    public static IEnumerable<ModelReference> EndpointReferences(ModelData model)
    {
        foreach (var part in model.Parts)
            if (part.Reference is { Reattachable: true, Model.Length: > 0 } reference &&
                part.Parent >= 0 && part.Parent < model.Parts.Count &&
                model.Parts[part.Parent].Emitter is { } emitter && emitter.Is("Lightning"))
                yield return reference;
    }

    /// <summary>The measured zero-handle Bezier route requires a direct reattachable target.</summary>
    public static bool CanTarget(ModelData model, int part) =>
        part >= 0 && part < model.Parts.Count && model.Parts[part].Emitter is { } emitter &&
        PointToPoint.Supports(emitter) && model.Parts.Any(p => p.Parent == part &&
            p.Reference is { Reattachable: true, Model.Length: > 0 });

    // Unsupported P2P paths retain their old fallback. Suppress it only after
    // a supported source emitter was actually built and bound to the target.
    public static bool NeedsLegacyConnector(ModelData model, bool boundPointToPoint = false) =>
        model.Parts.Any(p => p.Emitter is { P2P: true }) &&
        !model.Parts.Any(p => p.Emitter is { } emitter && emitter.Is("Lightning")) &&
        (!boundPointToPoint || model.Parts.Where((p, i) => p.Emitter is { P2P: true } && !CanTarget(model, i)).Any());
}
