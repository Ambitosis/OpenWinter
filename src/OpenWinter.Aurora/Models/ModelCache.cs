using System.Collections.Concurrent;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;

namespace OpenWinter.Aurora.Models;

/// <summary>Loads and caches models by resref and resolves animations through supermodel chains. Thread-safe.</summary>
public sealed class ModelCache
{
    readonly ResMan _rm;
    readonly ConcurrentDictionary<string, ModelData?> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ModelCache(ResMan rm) { _rm = rm; }

    /// <summary>Called once per resref whose model data exists but could not be read (resref, reason).</summary>
    public Action<string, string>? Problem;

    public ModelData? Get(string resref) =>
        string.IsNullOrEmpty(resref) ? null : _cache.GetOrAdd(resref, r =>
        {
            var buf = _rm.Read(r, ResType.Mdl);
            var model = Load(buf, out var error);
            if (model == null && buf != null) Problem?.Invoke(r, error);
            return model;
        });

    /// <summary>A walkmesh-only model (PWK/DWK/WOK) by resref and type.</summary>
    public ModelData? GetWalkmesh(string resref, ushort type) =>
        string.IsNullOrEmpty(resref) ? null : _cache.GetOrAdd(resref + "." + ResType.Extension(type), _ => Load(_rm.Read(resref, type)));

    public static ModelData? Load(byte[]? buf) => Load(buf, out _);

    public static ModelData? Load(byte[]? buf, out string error)
    {
        error = "";
        if (buf == null) return null;
        if (buf.Length < 12) { error = $"only {buf.Length} bytes"; return null; }
        try
        {
            return MdlBinaryReader.IsBinary(buf) ? MdlBinaryReader.Read(buf) : MdlAsciiReader.Read(buf);
        }
        catch (Exception e)
        {
            error = $"{(MdlBinaryReader.IsBinary(buf) ? "binary" : "ascii")} model: {e.GetType().Name}: {e.Message}";
            return null;
        }
    }

    public ModelAnimation? FindAnimation(ModelData model, string name)
    {
        var current = model;
        for (int depth = 0; current != null && depth < 8; depth++)
        {
            if (current.Animations.TryGetValue(name, out var a)) return a;
            current = string.IsNullOrEmpty(current.Supermodel) ? null : Get(current.Supermodel);
        }
        return null;
    }

    /// <summary>Every animation reachable from the model with the model that defines it (nearest first).</summary>
    public IEnumerable<(string Name, ModelAnimation Animation, bool Inherited)> AllAnimations(ModelData model)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = model;
        for (int depth = 0; current != null && depth < 8; depth++)
        {
            foreach (var kv in current.Animations)
                if (seen.Add(kv.Key)) yield return (kv.Key, kv.Value, depth > 0);
            current = string.IsNullOrEmpty(current.Supermodel) ? null : Get(current.Supermodel);
        }
    }
}
