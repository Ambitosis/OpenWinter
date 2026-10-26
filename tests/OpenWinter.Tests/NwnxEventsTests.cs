using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// NWNX_Events dispatch, and the player-input events a server without players can't measure: a script subscribes
/// pev_log, which appends "EVENT=data" for the tag the test names and skips or sets a result on request.
/// </summary>
public sealed class NwnxEventsTests
{
    const string Logger = """
        object m = GetModule();
        NWNXCall("NWNX_Events", "GetCurrentEvent"); string e = NWNXPopString();
        string tag = GetLocalString(m, "TAG"); string v = "";
        if (tag != "") { NWNXPushString(tag); NWNXCall("NWNX_Events", "GetEventData"); v = NWNXPopString(); }
        SetLocalString(m, "LOG", GetLocalString(m, "LOG") + e + "=" + v + ";");
        if (GetLocalInt(m, "SKIP:" + e)) NWNXCall("NWNX_Events", "SkipEvent");
        string r = GetLocalString(m, "RESULT:" + e);
        if (r != "") { NWNXPushString(r); NWNXCall("NWNX_Events", "SetEventResult"); }
        """;

    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection Client;
        public readonly List<NotifyMessage> Notes = new();
        public Creature Player => World.ConnectedPCs.First();

        public Fixture(Action<Fixture>? beforeLogin = null)
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            var area = new Area { ResRef = "nwnxev", Width = 2, Height = 2 };
            World.Register(area); World.Areas.Add(area);
            Script("pev_log", Logger);
            beforeLogin?.Invoke(this);
            Server = new GameServer(World, new ServerOptions { StartArea = "nwnxev", Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Events tester", PlayerKey = "KEY1" }.Write());
            Tick();
        }

        public void Script(string name, string body)
        {
            var code = NssCompiler.Compile("void main(){" + body + "}", name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(code != null, error); World.Scripts.Register(name, new NcsProgram(name, code!));
        }

        public void Subscribe(string evt)
        {
            var reg = World.System<NwnxRegistry>();
            reg.Stack.Push(NwnxValue.String("pev_log"));
            reg.Stack.Push(NwnxValue.String(evt));
            Assert.True(reg.Call("Events", "SubscribeEvent"));
        }

        public void Watch(string baseName, string tag = "")
        {
            Subscribe(baseName + "_BEFORE");
            Subscribe(baseName + "_AFTER");
            World.Module.Locals.Strings["TAG"] = tag;
        }

        public void Skip(string evt) => World.Module.Locals.Ints["SKIP:" + evt] = 1;
        public void Result(string evt, string value) => World.Module.Locals.Strings["RESULT:" + evt] = value;
        public string Log => World.Module.Locals.Strings.GetValueOrDefault("LOG", "");

        public void Request(string kind, object json)
        {
            Client.Send(new ClientRequestMessage { Kind = kind, Json = System.Text.Json.JsonSerializer.Serialize(json) }.Write());
            Tick();
        }

        public void Tick()
        {
            Server.Tick(.01f);
            while (Client.TryReceive(out var data))
                if ((MessageType)data[0] == MessageType.Notify) Notes.Add(NotifyMessage.Read(new MessageReader(data)));
        }

        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            foreach (var source in World.Resources.Sources.OfType<IDisposable>().Distinct()) source.Dispose();
        }
    }

    [DataFact]
    public void ConnectingRaisesConnectThenTheAreaBeingSent()
    {
        using var f = new Fixture(f =>
        {
            f.Watch("NWNX_ON_CLIENT_CONNECT", "PLAYER_NAME");
            f.Subscribe("NWNX_ON_SERVER_SEND_AREA_BEFORE");
        });
        Assert.StartsWith("NWNX_ON_CLIENT_CONNECT_BEFORE=Events tester;NWNX_ON_CLIENT_CONNECT_AFTER=Events tester;NWNX_ON_SERVER_SEND_AREA_BEFORE=", f.Log);
    }

    [DataFact]
    public void SkippingConnectRefusesThePlayerWithTheResultAsReason()
    {
        using var f = new Fixture(f =>
        {
            f.Watch("NWNX_ON_CLIENT_CONNECT");
            f.Skip("NWNX_ON_CLIENT_CONNECT_BEFORE");
            f.Result("NWNX_ON_CLIENT_CONNECT_BEFORE", "Banned");
        });
        Assert.Empty(f.World.ConnectedPCs);
        var refused = Assert.Single(f.Notes, n => n.Kind == "login-refused");
        Assert.Contains("Banned", refused.Json);
    }

    [DataFact]
    public void ExamineCanBeSkipped()
    {
        using var f = new Fixture();
        f.Watch("NWNX_ON_EXAMINE_OBJECT", "EXAMINEE_OBJECT_ID");
        f.Request("examine", new { @object = f.Player.Id });
        Assert.Equal($"NWNX_ON_EXAMINE_OBJECT_BEFORE={f.Player.Id:x};NWNX_ON_EXAMINE_OBJECT_AFTER={f.Player.Id:x};", f.Log);
        Assert.Contains(f.Notes, n => n.Kind == "examine");
        f.Notes.Clear();
        f.Skip("NWNX_ON_EXAMINE_OBJECT_BEFORE");
        f.Request("examine", new { @object = f.Player.Id });
        Assert.DoesNotContain(f.Notes, n => n.Kind == "examine");
    }

    [DataFact]
    public void QuickbarButtonsReportTheirSlotAndType()
    {
        using var f = new Fixture();
        f.Watch("NWNX_ON_QUICKBAR_SET_BUTTON", "TYPE");
        f.Request("quickbar-set", new { index = 3, slot = new { Kind = "feat", Id = 28 } });
        Assert.Equal("NWNX_ON_QUICKBAR_SET_BUTTON_BEFORE=4;NWNX_ON_QUICKBAR_SET_BUTTON_AFTER=4;", f.Log);
        Assert.NotNull(f.Player.QuickbarEdits[3]);
        f.Skip("NWNX_ON_QUICKBAR_SET_BUTTON_BEFORE");
        f.Request("quickbar-set", new { index = 4, slot = new { Kind = "feat", Id = 28 } });
        Assert.False(f.Player.QuickbarEdits.ContainsKey(4) && f.Player.QuickbarEdits[4] != null);
    }

    [DataFact]
    public void MapPinsAddChangeAndDestroy()
    {
        using var f = new Fixture();
        f.Watch("NWNX_ON_MAP_PIN_ADD_PIN", "PIN_NOTE");
        f.Watch("NWNX_ON_MAP_PIN_DESTROY_PIN", "PIN_NOTE");
        uint area = f.Player.Area!.Id;
        f.Request("map-pin-add", new { area, x = 3f, y = 4f, note = "camp" });
        Assert.Equal("NWNX_ON_MAP_PIN_ADD_PIN_BEFORE=camp;NWNX_ON_MAP_PIN_ADD_PIN_AFTER=camp;", f.Log);
        var pins = OpenWinter.Game.WorldObjects.PersonalMapPins.Read(f.World, f.Player.Area!, f.Player);
        var pin = Assert.Single(pins);
        f.World.Module.Locals.Strings["TAG"] = "PIN_ID";
        f.Skip("NWNX_ON_MAP_PIN_DESTROY_PIN_BEFORE");
        f.Request("map-pin-remove", new { area, id = pin.Id });
        Assert.EndsWith("NWNX_ON_MAP_PIN_DESTROY_PIN_BEFORE=1;NWNX_ON_MAP_PIN_DESTROY_PIN_AFTER=1;", f.Log);
        Assert.Single(OpenWinter.Game.WorldObjects.PersonalMapPins.Read(f.World, f.Player.Area!, f.Player));
    }

    [DataFact]
    public void PauseReportsTheNewState()
    {
        using var f = new Fixture();
        f.Watch("NWNX_ON_INPUT_TOGGLE_PAUSE", "PAUSE_STATE");
        f.Server.Options.AllowPlayerPausing = true;
        f.Request("pause", new { state = 1 });
        Assert.Equal("NWNX_ON_INPUT_TOGGLE_PAUSE_BEFORE=1;NWNX_ON_INPUT_TOGGLE_PAUSE_AFTER=1;", f.Log);
    }

    [DataFact]
    public void ClickingToWalkCanBeRefused()
    {
        using var f = new Fixture();
        f.Watch("NWNX_ON_INPUT_WALK_TO_WAYPOINT", "RUN_TO_POINT");
        f.Skip("NWNX_ON_INPUT_WALK_TO_WAYPOINT_BEFORE");
        f.Client.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = f.Player.Position + new Vector3(3, 0, 0), Run = false }.Write());
        f.Tick();
        Assert.Equal("NWNX_ON_INPUT_WALK_TO_WAYPOINT_BEFORE=0;NWNX_ON_INPUT_WALK_TO_WAYPOINT_AFTER=0;", f.Log);
        Assert.Empty(f.Player.Actions);
    }

    [DataFact]
    public void ScriptChunksDispatchListsAndWhitelistsGateSubscribers()
    {
        using var f = new Fixture();
        var reg = f.World.System<NwnxRegistry>();
        void Call(string fn, params NwnxValue[] args)
        {
            foreach (var a in args.Reverse()) reg.Stack.Push(a);
            Assert.True(reg.Call("Events", fn));
        }
        Call("SubscribeEventScriptChunk", NwnxValue.String("EV"), NwnxValue.String("SetLocalInt(GetModule(), \"CHUNK\", 1);"), NwnxValue.Int(1));
        EventsPlugin.Signal(f.World, "EV", f.World.Module);
        Assert.Equal(1, f.World.Module.Locals.Ints["CHUNK"]);

        f.Subscribe("EV2");
        Call("ToggleDispatchListMode", NwnxValue.String("EV2"), NwnxValue.String("pev_log"), NwnxValue.Int(1));
        EventsPlugin.Signal(f.World, "EV2", f.Player);
        Assert.Equal("", f.Log);
        Call("AddObjectToDispatchList", NwnxValue.String("EV2"), NwnxValue.String("pev_log"), NwnxValue.Object(f.Player.Id));
        EventsPlugin.Signal(f.World, "EV2", f.Player);
        Assert.Equal("EV2=;", f.Log);

        Assert.True(EventsPlugin.Allows(f.World, "NWNX_ON_CAST_SPELL", 5));
        Assert.False(EventsPlugin.Allows(f.World, "NWNX_ON_HAS_FEAT", 5)); // on by default
        Call("AddIDToWhitelist", NwnxValue.String("NWNX_ON_HAS_FEAT"), NwnxValue.Int(5));
        Assert.True(EventsPlugin.Allows(f.World, "NWNX_ON_HAS_FEAT", 5));
        Call("ToggleIDWhitelist", NwnxValue.String("NWNX_ON_CAST_SPELL"), NwnxValue.Int(1));
        Assert.False(EventsPlugin.Allows(f.World, "NWNX_ON_CAST_SPELL", 5));

        Call("UnsubscribeAllStartingWith", NwnxValue.String("pev"));
        Assert.Equal(0, EventsPlugin.SubscriberCount(f.World, "EV2"));
        Assert.Equal(1, EventsPlugin.SubscriberCount(f.World, "EV"));
    }
}
