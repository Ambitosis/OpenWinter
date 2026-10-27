using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;

namespace OpenWinter.Tests;

/// <summary>
/// What a persistent world takes away, as the NWN bridge passes it on: frames recorded on a stock server running
/// parity/suites/bridge_takeaway, whose chat commands rename the player, add and destroy a henchman and a summons,
/// hide and show a map pin, make a placeable unusable and back, and polymorph the player and cancel it.
/// </summary>
public class NwnBridgeTakeAwayTests
{
    const uint Pc = 0x7FFFFFFF, Henchman = 119, Summons = 122, Barrel = 100;

    /// <summary>Feeds recorded server frames to the bridge's parts as its connection does, up to a time.</summary>
    sealed class Rig
    {
        readonly List<(long Ms, byte[] Frame)> _frames;
        readonly NwnSessionContext _context;
        int _next;
        public readonly List<byte[]> Output = [];
        public readonly NwnWorldMirror World;
        public readonly NwnCombat Combat;
        public readonly NwnPresentation Presentation;
        public readonly NwnAbilities Abilities;

        public Rig(string recording)
        {
            var data = new NwnResManData(TestData.Paths);
            _context = new NwnSessionContext(data.TwoDARowCount, data.TwoDA);
            _frames = NwnRecording.Read(Path.Combine(AppContext.BaseDirectory, "Data", recording + ".jsonl.gz"))
                .Where(f => !f.Sent).Select(f => (f.Ms, f.Frame)).ToList();
            World = new NwnWorldMirror(data, Output.Add);
            Combat = new NwnCombat(data, World, Output.Add);
            Presentation = new NwnPresentation(data, World, Output.Add);
            Abilities = new NwnAbilities(data, Output.Add, _ => { });
        }

        /// <summary>The frames recorded up to <paramref name="ms"/>; returns the notifies the client got from them.</summary>
        public List<NotifyMessage> Until(long ms)
        {
            int before = Output.Count;
            for (; _next < _frames.Count && _frames[_next].Ms <= ms; _next++)
            {
                var frame = _frames[_next].Frame;
                if (NwnMessageRegistry.Create(frame[1], frame[2], NwnDirection.ServerToClient) is not { } m) continue;
                try { m.Read(new NwnWsReader(frame.AsMemory(3), _context)); }
                catch (Exception e) when (e is not OutOfMemoryException) { continue; }
                switch (m)
                {
                    case AreaMessages.AreaClientArea area:
                        World.Area(area);
                        Presentation.Area(area);
                        Abilities.AreaChanged();
                        break;
                    case GameObjUpdateMessages.GameObjUpdateObjList list:
                        Combat.ObjectList(list, l => Presentation.ObjectList(l, World.ObjectList));
                        Abilities.ObjectList(list);
                        break;
                    case GameObjUpdateMessages.GameObjUpdateObjControl control:
                        World.Control(control.ObjectId);
                        Abilities.Player = control.ObjectId;
                        break;
                    case MapPinMessages.MapPinPinEnabled pin:
                        World.MapPin(pin);
                        break;
                    default:
                        Abilities.ToClient(m);
                        break;
                }
                Combat.Tick(0.3f);
            }
            return Notifies(before);
        }

        public List<NotifyMessage> Notifies(int from = 0) =>
            Output.Skip(from).Select(b => new MessageReader(b)).Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).ToList();

        public List<UpdateMessage> Updates(int from = 0) =>
            Output.Skip(from).Select(b => new MessageReader(b)).Where(r => r.Type == MessageType.Update).Select(UpdateMessage.Read).ToList();
    }

    static JsonElement LastHud(IEnumerable<NotifyMessage> notes) => JsonDocument.Parse(notes.Last(n => n.Kind == "hud").Json).RootElement;

    [DataFact]
    public void ARenameAndItsUndoReachTheHud()
    {
        var rig = new Rig("nwn-bridge-takeaway");
        rig.Until(17000);
        // SetName(oPC, "Bob"): the label over the player and the HUD's own name both change.
        var renamed = rig.Until(19000);
        Assert.Equal("Bob", LastHud(renamed).GetProperty("self").GetProperty("name").GetString());
        // SetName(oPC, ""): the original name comes back in both.
        var restored = rig.Until(22000);
        Assert.Equal("Clergy Clericson", LastHud(restored).GetProperty("self").GetProperty("name").GetString());
        Assert.Contains(restored, n => n.Kind == "creature-labels" && n.Json.Contains("Clergy Clericson"));
    }

    [DataFact]
    public void ADestroyedHenchmanAndADismissedSummonsLeaveTheHudsParty()
    {
        var rig = new Rig("nwn-bridge-takeaway");
        rig.Until(23000);
        uint[] Party(List<NotifyMessage> notes) =>
            [.. LastHud(notes).GetProperty("party").EnumerateArray().Select(p => p.GetProperty("id").GetUInt32())];
        Assert.Equal([Henchman], Party(rig.Until(26000)));
        // DestroyObject on the henchman: the server deletes the creature and says nothing else about the party.
        Assert.Empty(Party(rig.Until(28000)));
        Assert.Equal([Summons], Party(rig.Until(32000)));
        Assert.Empty(Party(rig.Until(34000)));
    }

    [DataFact]
    public void AMapPinTheServerHidesLeavesTheMapAndComesBack()
    {
        var rig = new Rig("nwn-bridge-takeaway");
        var start = rig.Until(35000);
        var map = JsonDocument.Parse(start.Last(n => n.Kind == "area-map").Json).RootElement;
        string explored = map.GetProperty("explored").GetString()!;
        Assert.Equal("Test pin", Assert.Single(map.GetProperty("pins").EnumerateArray()).GetProperty("note").GetString());
        // SetMapPinEnabled(FALSE), then TRUE: the map is described again each time, its explored tiles kept.
        var hidden = JsonDocument.Parse(rig.Until(38000).Single(n => n.Kind == "area-map").Json).RootElement;
        Assert.Empty(hidden.GetProperty("pins").EnumerateArray());
        Assert.Equal(explored, hidden.GetProperty("explored").GetString());
        var shown = JsonDocument.Parse(rig.Until(41000).Single(n => n.Kind == "area-map").Json).RootElement;
        Assert.Single(shown.GetProperty("pins").EnumerateArray());
        // The client's own map asks again when it opens: the same map.
        int before = rig.Output.Count;
        rig.World.SendMap();
        Assert.Equal(shown.ToString(), JsonDocument.Parse(rig.Notifies(before).Single().Json).RootElement.ToString());
    }

    [DataFact]
    public void APlaceableMadeUnusableCanNoLongerBeSelected()
    {
        var rig = new Rig("nwn-bridge-takeaway");
        rig.Until(42000);
        int before = rig.Output.Count;
        rig.Until(44000);
        var off = rig.Updates(before).Last(u => u.ObjectId == Barrel);
        Assert.Equal((ObjectFlags)0, off.Flags & (ObjectFlags.Useable | ObjectFlags.Selectable));
        before = rig.Output.Count;
        rig.Until(47000);
        var on = rig.Updates(before).Last(u => u.ObjectId == Barrel);
        Assert.Equal(ObjectFlags.Useable | ObjectFlags.Selectable, on.Flags & (ObjectFlags.Useable | ObjectFlags.Selectable));
    }

    [DataFact]
    public void AFormWithoutAbilitiesCanBeCancelledUntilItEnds()
    {
        // A Polymorph Self troll (no abilities of its own), cancelled by the player.
        var rig = new Rig("nwn-bridge-polymorph");
        rig.Abilities.FromClient("abilities", "{}");
        bool CanCancel()
        {
            int before = rig.Output.Count;
            rig.Abilities.FromClient("abilities", $"{{\"target\":{Pc}}}");
            var target = JsonDocument.Parse(rig.Notifies(before).Last(n => n.Kind == "abilities").Json).RootElement.GetProperty("target");
            return target.GetProperty("canCancelPolymorph").GetBoolean();
        }
        bool Polymorphed(List<NotifyMessage> notes) =>
            JsonDocument.Parse(notes.Last(n => n.Kind == "spellbook").Json).RootElement.GetProperty("polymorphed").GetBoolean();
        rig.Until(8000);
        Assert.False(CanCancel());
        Assert.True(Polymorphed(rig.Until(8500)));
        Assert.True(CanCancel());
        Assert.False(Polymorphed(rig.Until(13500)));
        Assert.False(CanCancel());
    }

    [Fact]
    public void ASheetOpenedAgainBringsTheOtherPanelUpToDate()
    {
        // The server fills only the sheet panel the client has active. Skills lowered while the sheet was shut came
        // with the skills panel only, so opening the sheet again shows that panel at once rather than after a while.
        double now = 0;
        var sent = new List<NwnMessage>();
        var abilities = new NwnAbilities(new FakeData(), _ => { }, sent.Add, () => now) { Player = Pc };
        sbyte[] Panels() => [.. sent.OfType<GuiCharacterSheetMessages.GuiCharacterSheetStatus>().Select(s => s.ActivePanel)];
        void MainFigures() => abilities.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries = { new GameObjUpdateMessages.ObjListGuiEntry('S', ' ', new GuiCharacterSheet { ObjectId = Pc, Flags = CharacterSheetFlags.Abilities }) },
        });
        abilities.FromClient("character", "{}");
        MainFigures();
        Assert.Equal([0, 1], Panels());
        // Polled for a while: the panels alternate, ending on the main one.
        for (int i = 0; i < 7; i++)
        {
            now += 0.5;
            abilities.FromClient("character", "{\"version\":\"1\"}");
        }
        Assert.Equal([0, 1, 0], Panels());
        // Shut, then opened again: the skills panel straight away, its figures being the stale ones.
        now += 10;
        abilities.FromClient("character", "{}");
        Assert.Equal([0, 1, 0, 1], Panels());
    }

    sealed class FakeData : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 0;
        public string TwoDA(string table, int row, string column) => "";
        public string Text(uint strRef, byte gender) => "";
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }
}
