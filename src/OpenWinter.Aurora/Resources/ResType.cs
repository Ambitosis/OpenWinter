namespace OpenWinter.Aurora.Resources;

/// <summary>Aurora resource type ids and their file extensions.</summary>
public static class ResType
{
    public const ushort Bmp = 1, Tga = 3, Wav = 4, Plt = 6, Ini = 7, Bmu = 8, Txt = 10,
        Mdl = 2002, Nss = 2009, Ncs = 2010, Are = 2012, Set = 2013, Ifo = 2014, Bic = 2015,
        Wok = 2016, TwoDA = 2017, Tlk = 2018, Txi = 2022, Git = 2023, Uti = 2025, Utc = 2027,
        Dlg = 2029, Itp = 2030, Utt = 2032, Dds = 2033, Uts = 2035, Ltr = 2036, Gff = 2037,
        Fac = 2038, Ute = 2040, Utd = 2042, Utp = 2044, Dft = 2045, Gic = 2046, Gui = 2047,
        Utm = 2051, Dwk = 2052, Pwk = 2053, Jrl = 2056, Utw = 2058, Ssf = 2060, Ndb = 2064,
        Ptm = 2065, Ptt = 2066, Shd = 2069, Mtr = 2072, Ktx = 2073, Ttf = 2074, Sql = 2075,
        Tml = 2076, Sq3 = 2077, Png = 2080, Jpg = 2081, Caf = 2082, Jui = 2083, Erf = 9997, Bif = 9998, Key = 9999;

    static readonly Dictionary<string, ushort> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bmp"] = Bmp, ["tga"] = Tga, ["wav"] = Wav, ["plt"] = Plt, ["ini"] = Ini, ["bmu"] = Bmu, ["txt"] = Txt,
        ["mdl"] = Mdl, ["nss"] = Nss, ["ncs"] = Ncs, ["are"] = Are, ["set"] = Set, ["ifo"] = Ifo,
        ["bic"] = Bic, ["wok"] = Wok, ["2da"] = TwoDA, ["tlk"] = Tlk, ["txi"] = Txi, ["git"] = Git,
        ["uti"] = Uti, ["utc"] = Utc, ["dlg"] = Dlg, ["itp"] = Itp, ["utt"] = Utt, ["dds"] = Dds,
        ["uts"] = Uts, ["ltr"] = Ltr, ["gff"] = Gff, ["fac"] = Fac, ["ute"] = Ute, ["utd"] = Utd,
        ["utp"] = Utp, ["dft"] = Dft, ["gic"] = Gic, ["gui"] = Gui, ["utm"] = Utm, ["dwk"] = Dwk,
        ["pwk"] = Pwk, ["jrl"] = Jrl, ["utw"] = Utw, ["ssf"] = Ssf, ["ndb"] = Ndb, ["ptm"] = Ptm,
        ["ptt"] = Ptt, ["shd"] = Shd, ["mtr"] = Mtr, ["ktx"] = Ktx, ["ttf"] = Ttf, ["sql"] = Sql,
        ["tml"] = Tml, ["sq3"] = Sq3, ["png"] = Png, ["jpg"] = Jpg, ["caf"] = Caf, ["jui"] = Jui, ["erf"] = Erf, ["bif"] = Bif, ["key"] = Key,
    };

    static readonly Dictionary<ushort, string> ById = ByExtension.ToDictionary(kv => kv.Value, kv => kv.Key.ToLowerInvariant());

    public static string Extension(ushort type) => ById.TryGetValue(type, out var ext) ? ext : "";

    public static ushort FromExtension(string ext) => ByExtension.TryGetValue(ext.TrimStart('.'), out var id) ? id : (ushort)0;
}

/// <summary>A resource name plus type, case-insensitive. Resrefs are at most 16 (or 32 in newer archives) characters.</summary>
public readonly record struct ResKey(string ResRef, ushort Type)
{
    public static ResKey Of(string resref, ushort type) => new(resref.ToLowerInvariant(), type);

    public static bool TryParse(string fileName, out ResKey key)
    {
        int dot = fileName.LastIndexOf('.');
        key = default;
        if (dot <= 0) return false;
        ushort type = ResType.FromExtension(fileName[(dot + 1)..]);
        if (type == 0) return false;
        key = Of(fileName[..dot], type);
        return true;
    }

    public override string ToString() => $"{ResRef}.{ResType.Extension(Type)}";
}
