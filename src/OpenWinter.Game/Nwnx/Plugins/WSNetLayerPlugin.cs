namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_WSNetLayer: OpenWinter's server takes no NWN websocket clients, so no player is one.</summary>
static class WSNetLayerPlugin
{
    [NwnxFunc("WSNetLayer", "IsWSClient")]
    static void IsWSClient(NwnxCall n)
    {
        n.PopObject();
        n.Push(0);
    }
}
