using OpenWinter.Client.Rendering.Emitters;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Rendering;

/// <summary>
/// Builds Aurora particle emitters (fire, smoke, sparkles, spell glyphs, rings, bolts) as <see cref="AuroraEmitter"/>
/// nodes. Every update, render and blend mode of the base game's emitters is handled; docs/EMITTERS.md lists each
/// emitter property, how often the game uses it and what is done with it.
/// </summary>
public static class EmitterBuilder
{
    public static AuroraEmitter? Build(EmitterData e, TextureFactory textures, EmitterOptions? options = null) =>
        AuroraEmitter.Create(e, textures, options);

    /// <summary>An "Explosion" emitter that bursts as soon as it is built (an effect's impact).</summary>
    public static AuroraEmitter? Burst(EmitterData e, TextureFactory textures) =>
        AuroraEmitter.Create(e, textures, new EmitterOptions { BurstUnkeyed = true });

    /// <summary>
    /// The options for one emitter of a model: the pool is sized for the largest birthrate and life the model's
    /// animations key, chunk emitters draw the chunk model's mesh.
    /// </summary>
    public static EmitterOptions OptionsFor(ModelPart part, IEnumerable<ModelAnimation>? animations, MaterialFactory materials,
        Func<string, ModelData?>? models, bool warm = false, bool burstUnkeyed = false) => new()
    {
        Range = EmitterRange.Of(part.Emitter!, part.Name, animations),
        Warm = warm,
        BurstUnkeyed = burstUnkeyed,
        Chunk = models == null ? null : name => ChunkMesh(name, materials, models),
    };

    static readonly Dictionary<string, (Mesh, Material?)?> Chunks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The first drawable mesh of a chunk model (debris particles).</summary>
    static (Mesh Mesh, Material? Material)? ChunkMesh(string name, MaterialFactory materials, Func<string, ModelData?> models)
    {
        if (Chunks.TryGetValue(name, out var cached)) return cached;
        (Mesh, Material?)? result = null;
        if (models(name) is { } model)
            foreach (var part in model.Parts)
            {
                if (!ModelBuilder.Drawable(part.Mesh)) continue;
                result = (ModelBuilder.MeshOf(part.Mesh!), materials.Get(part.Mesh!, MaterialKind.World));
                break;
            }
        Chunks[name] = result;
        return result;
    }
}
