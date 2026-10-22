using OpenWinter.Aurora.Resources;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>The bridge's game data from the player's install: base game first, then the server's haks and talk table.</summary>
public sealed class NwnResManData(GamePaths paths) : INwnBridgeData
{
    ResMan _rm = ResMan.ForClient(paths, [], "");

    public IReadOnlyList<string> MissingHaks => _rm.MissingHaks;
    public int TwoDARowCount(string table) => _rm.TwoDA(table).RowCount;
    public string TwoDA(string table, int row, string column) => _rm.TwoDA(table).Get(row, column);
    public string Text(uint strRef, byte gender) => _rm.Talk.Get(strRef);
    public void UseContent(IReadOnlyList<string> haks, string customTlk) => _rm = ResMan.ForClient(paths, haks, customTlk);
}
