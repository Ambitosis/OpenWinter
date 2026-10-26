using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>The bridge's game data from the player's install: base game first, then the server's haks and talk table.</summary>
public sealed class NwnResManData(GamePaths paths) : INwnBridgeData
{
    ResMan _rm = ResMan.ForClient(paths, [], "");
    TalkTable? _female = Female(paths, "");

    public IReadOnlyList<string> MissingHaks => _rm.MissingHaks;
    public int TwoDARowCount(string table) => _rm.TwoDA(table).RowCount;
    public string TwoDA(string table, int row, string column) => _rm.TwoDA(table).Get(row, column);
    public byte[]? Resource(string resref, string extension) => _rm.Read(resref, ResType.FromExtension(extension));

    /// <summary>
    /// Gender 1 reads the female talk tables (dialogf.tlk, and the custom table's "f" file) where the language has
    /// them, falling back to the default text.
    /// </summary>
    public string Text(uint strRef, byte gender) =>
        gender == 1 && _female?.Get(strRef) is { Length: > 0 } female ? female : _rm.Talk.Get(strRef);

    public void UseContent(IReadOnlyList<string> haks, string customTlk)
    {
        _rm = ResMan.ForClient(paths, haks, customTlk);
        _female = Female(paths, customTlk);
    }

    static TalkTable? Female(GamePaths paths, string customTlk)
    {
        var baseFemale = Path.Combine(Path.GetDirectoryName(paths.BaseTlk) ?? "", "dialogf.tlk");
        var customFemale = string.IsNullOrEmpty(customTlk) ? null : paths.Tlk(customTlk + "f");
        if (!File.Exists(baseFemale) && customFemale == null) return null;
        return new TalkTable(File.Exists(baseFemale) ? File.ReadAllBytes(baseFemale) : null, customFemale != null ? File.ReadAllBytes(customFemale) : null);
    }
}
