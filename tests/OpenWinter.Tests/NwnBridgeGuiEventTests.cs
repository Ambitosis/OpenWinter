using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Built-in GUI panels and events over the NWN bridge, from frames recorded against nwserver running the bridge_gui
/// test module (parity/suites/bridge_gui).
/// </summary>
public class NwnBridgeGuiEventTests
{
    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 0;
        public string TwoDA(string table, int row, string column) => (table, row, column) == ("portraits", 129, "BaseResRef") ? "hu_m_99_" : "";
        public string Text(uint strRef, byte gender) => strRef == 14594 ? "Bound in iron." : "";
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    static readonly NwnSessionContext Context = new(_ => 0, (_, _, _) => "");

    static T Decode<T>(string hex) where T : NwnMessage, new()
    {
        var m = new T();
        m.Read(new NwnWsReader(Convert.FromHexString(hex), Context));
        return m;
    }

    static string Encode(NwnMessage m)
    {
        var w = new NwnWsWriter(Context);
        m.Write(w);
        return Convert.ToHexString(w.ToArray());
    }

    static List<NotifyMessage> Notices(Func<Action<byte[]>, bool> translate, bool used = true)
    {
        var notes = new List<NotifyMessage>();
        Assert.Equal(used, translate(b => notes.Add(NotifyMessage.Read(new MessageReader(b)))));
        return notes;
    }

    [Fact]
    public void DisabledPanelsReachTheClientWithTheirTarget()
    {
        // SetGuiPanelDisabled(oPC, GUI_PANEL_CHARACTERSHEET, TRUE, oPC), then FALSE: the player is the client's own id.
        var on = Decode<GuiEventMessages.GuiEventDisable>("0808000000020111FFFFFF7F");
        var off = Decode<GuiEventMessages.GuiEventDisable>("0808000000020011FFFFFF7F");
        var notes = Notices(emit => NwnGuiEvents.ToClient(on, emit) & NwnGuiEvents.ToClient(off, emit));
        Assert.All(notes, n => Assert.Equal("gui-disable", n.Kind));
        Assert.Equal("""{"panel":8,"disabled":true,"target":2147483647}""", notes[0].Json);
        Assert.Equal("""{"panel":8,"disabled":false,"target":2147483647}""", notes[1].Json);
        // Without a target the server sends OBJECT_INVALID.
        var all = new GuiEventMessages.GuiEventDisable { GuiElement = GuiEventMessages.GuiPanel.Inventory, Disable = true };
        Assert.Equal("""{"panel":4,"disabled":true,"target":2130706432}""", Assert.Single(Notices(emit => NwnGuiEvents.ToClient(all, emit))).Json);
    }

    [Fact]
    public void PopUpGuiPanelForcesThePanelOpen()
    {
        // PopUpGUIPanel(oPC, GUI_PANEL_INVENTORY).
        var popup = Decode<PopupGuiPanelMessages.PopUpGUIPanelPopUpOtherPlayerPanel>("0804000000");
        var note = Assert.Single(Notices(emit => NwnGuiEvents.ToClient(popup, emit)));
        Assert.Equal(("gui-panel", """{"panel":4}"""), (note.Kind, note.Json));
        Notices(emit => NwnGuiEvents.ToClient(new ChatMessages.ChatTalkC2S(), emit), used: false);
    }

    [Fact]
    public void GuiEventsGoBackAsTheMessageTheServerRaisesOnPlayerGuiEventFrom()
    {
        // The frame the server raised GUIEVENT_DISABLED_PANEL_ATTEMPT_OPEN from, with 8 and the player.
        var attempt = NwnGuiEvents.FromClient("gui-event", """{"type":14,"integer":8,"object":2147483647,"vector":[0,0,0]}""");
        Assert.Equal("050E0005080011FFFFFF7F0B000000000B000000000B00000000", Encode(attempt!));
        // A ground radial: no object, the point it opened on.
        var radial = Assert.IsType<GuiEventMessages.GuiEventNotify>(NwnGuiEvents.FromClient("gui-event", """{"type":26,"integer":1,"vector":[22.75,15.5,0]}"""));
        Assert.Equal((26, 1, GameObjUpdateMessages.InvalidObjectId, (22.75f, 15.5f, 0f)),
            ((int)radial.GuiEvent, (int)radial.GuiEventInteger, radial.GuiEventObject, radial.GuiEventPosition));
        Assert.Null(NwnGuiEvents.FromClient("gui-panel", "{}"));
    }

    [Fact]
    public void ExaminedCreaturesPlaceablesAndDoorsReachTheClient()
    {
        var data = new Data();
        var world = new NwnWorldMirror(data, _ => { });
        // ActionExamine on a commoner named "Npc One", then InputExamine answers for a chest and a door.
        var creature = Decode<GuiInfoPopupMessages.GuiInfoPopupExamineCreatureData>(
            "110B0000000200120000000000020102000E070000004E7063204F6E650581000300030002010B0000803E02010500000200");
        var chest = Decode<GuiInfoPopupMessages.GuiInfoPopupExaminePlaceableData>("110F000000020012010002390000");
        var notes = Notices(emit => NwnExamine.ToClient(creature, world, data, emit) & NwnExamine.ToClient(chest, world, data, emit));
        Assert.All(notes, n => Assert.Equal("examine", n.Kind));
        Assert.Equal("""{"object":11,"kind":"creature","name":"Npc One","description":"","portrait":"po_hu_m_99_","icon":[],"properties":[],"subtitle":"","info":null,"identified":true}""", notes[0].Json);
        Assert.Contains("\"kind\":\"placeable\"", notes[1].Json);
        Assert.Contains("\"description\":\"Bound in iron.\"", notes[1].Json);
        // The request for a world object goes as the examine input the server answered.
        Assert.Equal("110B000000", Encode(NwnExamine.FromClient("examine", """{"object":11}""")!));
        Assert.Null(NwnExamine.FromClient("examine", """{"object":2130706432}"""));
    }
}
