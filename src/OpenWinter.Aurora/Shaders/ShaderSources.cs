using OpenWinter.Aurora.Resources;

namespace OpenWinter.Aurora.Shaders;

/// <summary>
/// Where shader sources come from: a module's own (module, haks, development and override folders), and the
/// player's installed game (its shader library and standard programs, read at run time like any other game data).
/// </summary>
public static class ShaderSources
{
    /// <summary>Whether a resource source belongs to the installed game rather than to the module or the player.</summary>
    public static bool IsInstalled(ResMan resources, IResourceSource source)
    {
        if (source is KeyBifSource) return true;
        var gameRoot = Path.GetFullPath(resources.Paths.GameRoot);
        string? path = source switch
        {
            DirectorySource d => d.Description,
            ErfSource e => e.Description,
            _ => null,
        };
        return path != null && Path.GetFullPath(path).StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A module's own shader by name (module, haks, development and override folders), or null.</summary>
    public static Func<string, string?> Module(ResMan resources) => Reader(resources, resources.Sources.Where(s => !IsInstalled(resources, s)).ToList());

    /// <summary>A shader of the installed game by name, or null.</summary>
    public static Func<string, string?> Installed(ResMan resources) => Reader(resources, resources.Sources.Where(s => IsInstalled(resources, s)).ToList());

    static Func<string, string?> Reader(ResMan resources, List<IResourceSource> sources) => name =>
    {
        var key = ResKey.Of(name, ResType.Shd);
        foreach (var s in sources)
            if (s.Read(key) is { } data) return Text.Windows1252.GetString(data);
        return null;
    };
}
