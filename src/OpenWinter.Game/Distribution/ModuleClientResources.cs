using System.Security.Cryptography;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;

namespace OpenWinter.Game.Distribution;

/// <summary>Packages module presentation data without exposing module state, scripts or object templates.</summary>
public static class ModuleClientResources
{
    public static bool IsClientResource(ushort type) => type is
        ResType.Bmp or ResType.Tga or ResType.Wav or ResType.Plt or ResType.Bmu or
        ResType.Mdl or ResType.Set or ResType.Wok or ResType.TwoDA or ResType.Txi or
        ResType.Dds or ResType.Dft or ResType.Gui or ResType.Dwk or ResType.Pwk or
        ResType.Jui or ResType.Ssf or ResType.Mtr or ResType.Ktx or ResType.Ttf or ResType.Png or ResType.Jpg;

    /// <summary>Returns a deterministic presentation ERF in the server cache, or null for an empty module.</summary>
    public static string? Package(ResMan resources, string cache)
    {
        if (resources.ModuleSource is not { } module) return null;
        var entries = module.Keys.Where(k => IsClientResource(k.Type))
            .OrderBy(k => k.ResRef, StringComparer.Ordinal).ThenBy(k => k.Type)
            .Select(k => new ErfEntry(k.ResRef, k.Type, module.Read(k)
                ?? throw new IOException($"Module resource vanished: {k}"))).ToList();
        if (entries.Count == 0) return null;
        // Stable header date makes unchanged modules reuse the same verified client cache.
        var data = Erf.Write(entries, "ERF ", buildDate: new DateTime(2000, 1, 1));
        var hash = Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
        Directory.CreateDirectory(cache);
        var path = Path.Combine(cache, hash + ".erf");
        if (Matches(path, hash)) return path;
        // A truncated or stale file under the hash name is replaced. Writing a private temp file and moving it
        // into place keeps readers (and other servers sharing the cache) from ever seeing a partial package.
        var temp = Path.Combine(cache, $"{hash}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temp, data);
            try { File.Move(temp, path, overwrite: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException && Matches(path, hash)) { } // another process won
        }
        finally
        {
            try { File.Delete(temp); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return path;
    }

    static bool Matches(string path, string hash)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA1.HashData(stream)).Equals(hash, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }
}
