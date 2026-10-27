using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.ResmanMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The server's changes to the player's game data, as OpenWinter's own server sends them: SetTextureOverride (a
/// resource override of the texture type) becomes "texture-override", NWNX_Player_SetResManOverride (any other type)
/// the "nwnx-player" resman-override, and SetTlkOverride "tlk-override", which the bridge's own text follows too.
/// SetPanelButtonFlash becomes "panel-flash".
/// </summary>
static class NwnResourceOverrides
{
    /// <summary>RESTYPE_TEX: whichever texture format the name resolves to.</summary>
    const short TextureType = 2001;

    public static bool ToClient(NwnMessage message, uint player, INwnBridgeData data, Action<byte[]> emit)
    {
        void Notify(string kind, JsonObject json) =>
            emit(new NotifyMessage { Kind = kind, Subject = player, Json = json.ToJsonString() }.Write());
        void Text(uint strRef, string text)
        {
            data.SetText(strRef, text);
            Notify("tlk-override", new JsonObject { ["strref"] = (int)strRef, ["text"] = text });
        }
        switch (message)
        {
            case ResmanOverride m:
            {
                string to = m.Action == ResmanOverrideAction.Store ? m.NewName : "";
                if (m.Restype == TextureType) Notify("texture-override", new JsonObject { ["old"] = m.Name, ["new"] = to });
                else Notify("nwnx-player", new JsonObject { ["op"] = "resman-override", ["type"] = (int)m.Restype, ["from"] = m.Name, ["to"] = to });
                return true;
            }
            case ResmanTlkOverride m:
                Text(m.Strref, m.Value);
                return true;
            case ResmanTlkOverrideList m:
                foreach (var (strRef, text) in m.Overrides) Text(strRef, text);
                return true;
            case GuiPartyBarMessages.GuiPartyBarPanelButtonFlash m:
                Notify("panel-flash", new JsonObject { ["button"] = (int)m.Button, ["flash"] = m.EnableFlash });
                return true;
        }
        return false;
    }
}
