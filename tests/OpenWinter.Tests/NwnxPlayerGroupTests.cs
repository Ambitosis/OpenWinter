using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Conversations;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// NWNX_Player, Administration, Chat, Dialog, Feedback, Rename and Util calls that need a connected player (the NWNX
/// parity server has none), driven through a server and loopback clients.
/// </summary>
public sealed class NwnxPlayerGroupTests
{
    sealed class Client
    {
        public required LoopbackConnection Connection;
        public uint Me;
        public readonly List<ChatMessage> Chat = new();
        public readonly List<NotifyMessage> Notices = new();

        public void Pump()
        {
            while (Connection.TryReceive(out var data))
            {
                var r = new MessageReader(data);
                switch (r.Type)
                {
                    case MessageType.ControlObject: Me = ControlObjectMessage.Read(r).ObjectId; break;
                    case MessageType.Chat: Chat.Add(ChatMessage.Read(r)); break;
                    case MessageType.Notify: Notices.Add(NotifyMessage.Read(r)); break;
                }
            }
        }
    }

    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly Area Area;
        public NwnxRegistry Nwnx => World.System<NwnxRegistry>();

        public Fixture(ServerOptions? options = null)
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            World.EnableScripts();
            Area = new Area { ResRef = "nwnx", Width = 4, Height = 4 };
            World.Register(Area);
            World.Areas.Add(Area);
            Server = new GameServer(World, options ?? new ServerOptions { StartArea = "nwnx", Vault = VaultMode.Local });
        }

        public Client Join(string name, string password = "")
        {
            var (client, server) = LoopbackConnection.CreatePair();
            Server.Accept(server);
            client.Send(new HelloMessage { PlayerName = name, Password = password }.Write());
            var c = new Client { Connection = client };
            Tick();
            c.Pump();
            return c;
        }

        public void Tick(int n = 1)
        {
            for (int i = 0; i < n; i++) Server.Tick(1f / 30);
        }

        public Creature Pc(Client c) => World.Get<Creature>(c.Me)!;

        /// <summary>Calls an NWNX function with its arguments in the header's order (pushed last to first, as the wrappers do).</summary>
        public NwnxStack Call(string plugin, string function, params object[] args)
        {
            for (int i = args.Length - 1; i >= 0; i--)
                Nwnx.Stack.Push(args[i] switch
                {
                    int v => NwnxValue.Int(v),
                    ushort v => NwnxValue.Int(v),
                    float v => NwnxValue.Float(v),
                    string v => NwnxValue.String(v),
                    uint v => NwnxValue.Object(v),
                    GameObject o => NwnxValue.Object(o.Id),
                    Location l => new NwnxValue(NwnxType.Location, l),
                    Vector3 v => new NwnxValue(NwnxType.Vector, v),
                    _ => throw new ArgumentException(args[i]?.GetType().Name),
                });
            Assert.True(Nwnx.Call(plugin, function));
            return Nwnx.Stack;
        }

        public void Script(string name, string source)
        {
            var code = NssCompiler.Compile(source, name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(code != null, error);
            World.Scripts.Register(name, new NcsProgram(name, code!));
        }

        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            foreach (var source in World.Resources.Sources.OfType<IDisposable>().Distinct()) source.Dispose();
        }
    }

    // ---- Administration ----------------------------------------------------------------

    [DataFact]
    public void PlayerPasswordBansAndLevelLimitsDecideWhoGetsIn()
    {
        using var f = new Fixture();
        f.Call("Administration", "SetPlayerPassword", "secret");
        var wrong = f.Join("Alice", "nope");
        Assert.Equal(0u, wrong.Me);
        Assert.Contains(wrong.Notices, n => n.Kind == "login-refused" && n.Json.Contains("Invalid password."));
        var right = f.Join("Bob", "secret");
        Assert.NotEqual(0u, right.Me);

        f.Call("Administration", "ClearPlayerPassword");
        f.Call("Administration", "AddBannedPlayerName", "Griefer");
        Assert.Equal(0u, f.Join("Griefer").Me);
        f.Call("Administration", "RemoveBannedPlayerName", "Griefer");
        Assert.NotEqual(0u, f.Join("Griefer").Me);

        // A level 1 character can't enter a server that starts at level 2.
        f.Call("Administration", "SetMinLevel", 2);
        Assert.Equal(0u, f.Join("Carol").Me);
    }

    [DataFact]
    public void SettingsChangedBeforeTheServerExistsCarryOver()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var nwnx = world.System<NwnxRegistry>();
        // OnModuleLoad runs before the host creates its server.
        nwnx.Stack.Push(NwnxValue.String("From Script"));
        nwnx.Call("Administration", "SetServerName");
        nwnx.Stack.Push(NwnxValue.String("dm"));
        nwnx.Call("Administration", "SetDMPassword");
        var server = new GameServer(world, new ServerOptions { ServerName = "From Config" });
        Assert.Equal("From Script", server.Options.ServerName);
        Assert.Equal("dm", server.Options.DMPassword);
        nwnx.Call("Administration", "GetServerName");
        Assert.Equal("From Script", nwnx.PopAs(NwnxType.String));
    }

    [DataFact]
    public void DeletingACharacterBootsThePlayerAndKeepsABackup()
    {
        using var f = new Fixture();
        var a = f.Join("Deleter");
        var pc = f.Pc(a);
        pc.VaultFile = "deleteme";
        var dir = OpenWinter.Game.Persistence.ServerVault.VaultDir(f.World, pc);
        Directory.CreateDirectory(dir);
        var bic = Path.Combine(dir, "deleteme.bic");
        File.WriteAllBytes(bic, new byte[] { 1 });
        File.Delete(bic + ".deleted0");
        f.Call("Administration", "DeletePlayerCharacter", pc, 1, "");
        Assert.False(File.Exists(bic));
        Assert.True(File.Exists(bic + ".deleted0"));
        a.Pump();
        Assert.Contains(a.Notices, n => n.Kind == "boot" && n.Json.Contains("Delete Character"));
        Assert.False(a.Connection.IsConnected);
        File.Delete(bic + ".deleted0");
    }

    // ---- Feedback ----------------------------------------------------------------------

    [DataFact]
    public void HiddenFeedbackNeverReachesThePlayerUnlessHerOwnStateShowsIt()
    {
        using var f = new Fixture();
        var a = f.Join("Alice");
        var pc = f.Pc(a);
        a.Chat.Clear();
        f.World.Output!.SendFeedback(pc, "inventory full", FeedbackKind.Message, 12);
        f.Call("Feedback", "SetMessageHidden", GameObject.InvalidId, FeedbackPlugin.Feedback, 12, 1);
        f.World.Output!.SendFeedback(pc, "hidden", FeedbackKind.Message, 12);
        f.World.Output!.SendFeedback(pc, "other message", FeedbackKind.Message, 13);
        // Combat log kind 11 covers SendMessageToPC; her own FALSE shows it again.
        f.Call("Feedback", "SetMessageHidden", GameObject.InvalidId, FeedbackPlugin.CombatLog, FeedbackKind.Message, 1);
        f.World.Output!.SendMessage(pc, "script message");
        f.Call("Feedback", "SetMessageHidden", pc, FeedbackPlugin.CombatLog, FeedbackKind.Message, 0);
        f.World.Output!.SendMessage(pc, "shown again");
        a.Pump();
        Assert.Equal(new[] { "inventory full", "other message", "shown again" }, a.Chat.Select(c => c.Text));
        f.Call("Feedback", "GetMessageHidden", pc, FeedbackPlugin.CombatLog, FeedbackKind.Message);
        Assert.Equal(0, f.Nwnx.PopAs(NwnxType.Int));
        f.Call("Feedback", "GetMessageHidden", pc, FeedbackPlugin.Feedback, 12);
        Assert.Equal(-1, f.Nwnx.PopAs(NwnxType.Int));
    }

    // ---- Chat --------------------------------------------------------------------------

    [DataFact]
    public void ChatScriptSeesPlayerChatAndCanSkipIt()
    {
        using var f = new Fixture();
        var a = f.Join("Alice");
        f.Script("pw_chat", @"void main()
        {
            NWNXCall(""NWNX_Chat"", ""GetMessage""); string m = NWNXPopString();
            NWNXCall(""NWNX_Chat"", ""GetChannel""); int c = NWNXPopInt();
            object oMod = GetModule();
            SetLocalString(oMod, ""seen"", GetLocalString(oMod, ""seen"") + ""|"" + IntToString(c) + "":"" + m);
            if (m == ""secret"") NWNXCall(""NWNX_Chat"", ""SkipMessage"");
        }");
        f.Call("Chat", "RegisterChatScript", "pw_chat");
        a.Chat.Clear();
        a.Connection.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = "hello" }.Write());
        a.Connection.Send(new ChatSendMessage { Channel = ChatChannel.Whisper, Text = "secret" }.Write());
        f.Tick();
        a.Pump();
        Assert.Equal("|1:hello|3:secret", f.World.Module.Locals.Strings["seen"]);
        Assert.Equal(new[] { "hello" }, a.Chat.Where(c => c.Channel != ChatChannel.Server).Select(c => c.Text));
    }

    [DataFact]
    public void HearingDistanceIsPerPlayerOverTheServersOwn()
    {
        using var f = new Fixture();
        var a = f.Join("Alice");
        var b = f.Join("Bob");
        var alice = f.Pc(a);
        var bob = f.Pc(b);
        alice.Position = new Vector3(10, 10, 0);
        bob.Position = new Vector3(18, 10, 0);
        b.Chat.Clear();
        a.Connection.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = "8 m away" }.Write());
        f.Tick();
        b.Pump();
        Assert.Contains(b.Chat, c => c.Text == "8 m away");
        f.Call("Chat", "SetChatHearingDistance", 5f, bob, ChatPlugin.PlayerTalk);
        b.Chat.Clear();
        a.Connection.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = "too far" }.Write());
        f.Tick();
        b.Pump();
        Assert.DoesNotContain(b.Chat, c => c.Text == "too far");
        f.Call("Chat", "GetChatHearingDistance", bob, ChatPlugin.PlayerTalk);
        Assert.Equal(5f, f.Nwnx.PopAs(NwnxType.Float));
        f.Call("Chat", "GetChatHearingDistance", GameObject.InvalidId, ChatPlugin.PlayerTalk);
        Assert.Equal(20f, f.Nwnx.PopAs(NwnxType.Float));
    }

    // ---- Player ------------------------------------------------------------------------

    [DataFact]
    public void PersonalTokensGiveWayToSetCustomToken()
    {
        using var f = new Fixture();
        var alice = f.Pc(f.Join("Alice"));
        var bob = f.Pc(f.Join("Bob"));
        var convo = f.World.System<ConversationService>();
        convo.SetCustomToken(101, "everyone");
        f.Call("Player", "SetCustomToken", alice, 101, "only Alice");
        Assert.Equal("only Alice", convo.Tokens("<CUSTOM101>", alice, alice));
        Assert.Equal("everyone", convo.Tokens("<CUSTOM101>", bob, bob));
        convo.SetCustomToken(101, "reset");
        Assert.Equal("reset", convo.Tokens("<CUSTOM101>", alice, alice));
    }

    [DataFact]
    public void CustomJournalEntriesAreReadBackAndCompleteQuests()
    {
        using var f = new Fixture();
        var a = f.Join("Alice");
        var pc = f.Pc(a);
        f.Call("Player", "GetQuestCompleted", pc, "custom_q");
        Assert.Equal(-1, f.Nwnx.PopAs(NwnxType.Int));
        // name, text, tag, state, priority, completed, displayed, updated, day, time, silent
        f.Call("Player", "AddCustomJournalEntry", pc, "My Quest", "Find the cat.", "custom_q", 2, 1, 1, 1, 0, 100, 200, 0);
        Assert.Equal(1, f.Nwnx.PopAs(NwnxType.Int));
        f.Call("Player", "GetQuestCompleted", pc, "custom_q");
        Assert.Equal(1, f.Nwnx.PopAs(NwnxType.Int));
        f.Call("Player", "GetJournalEntry", pc, "custom_q");
        Assert.Equal(1, f.Nwnx.PopAs(NwnxType.Int)); // updated
        f.Nwnx.PopAs(NwnxType.Int); f.Nwnx.PopAs(NwnxType.Int); f.Nwnx.PopAs(NwnxType.Int);
        Assert.Equal(2, f.Nwnx.PopAs(NwnxType.Int)); // state
        Assert.Equal(200, f.Nwnx.PopAs(NwnxType.Int)); // time of day
        Assert.Equal(100, f.Nwnx.PopAs(NwnxType.Int)); // calendar day
        Assert.Equal("My Quest", f.Nwnx.PopAs(NwnxType.String));
        Assert.Equal("Find the cat.", f.Nwnx.PopAs(NwnxType.String));
        a.Pump();
        var journal = Assert.Single(a.Notices, n => n.Kind == "journal");
        Assert.True(JsonDocument.Parse(journal.Json).RootElement.GetProperty("update").GetBoolean());
        // Hidden journal lines arrive without the update flag.
        f.Call("Feedback", "SetMessageHidden", GameObject.InvalidId, FeedbackPlugin.Journal, 0, 1);
        f.Call("Player", "AddCustomJournalEntry", pc, "Other", "", "custom_r", 1, 1, 0, 1, 0, 0, 0, 0);
        Assert.Equal(2, f.Nwnx.PopAs(NwnxType.Int));
        a.Pump();
        Assert.False(JsonDocument.Parse(a.Notices.Last(n => n.Kind == "journal").Json).RootElement.GetProperty("update").GetBoolean());
    }

    [DataFact]
    public void GameObjectFollowsPossessionAndAlwaysWalkSlowsThePlayer()
    {
        using var f = new Fixture();
        var pc = f.Pc(f.Join("Alice"));
        var npc = new Creature { Tag = "npc", WalkRate = 2, RunRate = 4 };
        f.World.AddToArea(npc, f.Area, pc.Position + new Vector3(1, 0, 0), 0, exact: true);
        f.Call("Player", "GetGameObject", pc);
        Assert.Equal(pc.Id, f.Nwnx.PopAs(NwnxType.Object));
        f.Call("Player", "PossessCreature", pc, npc, 1, 0);
        Assert.Equal(1, f.Nwnx.PopAs(NwnxType.Int));
        f.Call("Player", "GetGameObject", pc);
        Assert.Equal(npc.Id, f.Nwnx.PopAs(NwnxType.Object));
        f.World.System<FamiliarPossession>().Unpossess(npc);
        f.Call("Player", "GetGameObject", pc);
        Assert.Equal(pc.Id, f.Nwnx.PopAs(NwnxType.Object));

        Movement.MaxSpeed(f.World, pc, out bool walking);
        Assert.False(walking);
        f.Call("Player", "SetAlwaysWalk", pc, 1);
        Movement.MaxSpeed(f.World, pc, out walking);
        Assert.True(walking);
    }

    [DataFact]
    public void SpawnLocationSetOnClientEnterPlacesThePlayer()
    {
        using var f = new Fixture();
        f.Script("pw_enter", @"void main()
        {
            object oPC = GetEnteringObject();
            NWNXPushLocation(Location(GetObjectByTag(""nwnx_area""), Vector(7.0, 9.0, 0.0), 0.0));
            NWNXPushObject(oPC);
            NWNXCall(""NWNX_Player"", ""SetSpawnLocation"");
        }");
        f.World.SetTag(f.Area, "nwnx_area");
        f.World.Module.Scripts["Mod_OnClientEntr"] = "pw_enter";
        var pc = f.Pc(f.Join("Alice"));
        Assert.Equal(7f, pc.Position.X, 1);
        Assert.Equal(9f, pc.Position.Y, 1);
    }

    [DataFact]
    public void PerPlayerViewsGoOnlyToThatPlayer()
    {
        using var f = new Fixture();
        var a = f.Join("Alice");
        var b = f.Join("Bob");
        var alice = f.Pc(a);
        a.Notices.Clear(); b.Notices.Clear();
        f.Call("Player", "UpdateSkyBox", alice, 3);
        f.Call("Player", "SetResManOverride", alice, ResType.Tga, "old_tex", "new_tex");
        f.Call("Player", "ShowVisualEffect", alice, 20, new Vector3(5, 5, 0), 1f, Vector3.Zero, Vector3.Zero);
        a.Pump(); b.Pump();
        Assert.Contains(a.Notices, n => n.Kind == "area-skybox" && n.Json.Contains("\"skybox\":3"));
        Assert.Contains(a.Notices, n => n.Kind == "nwnx-player" && n.Json.Contains("resman-override") && n.Json.Contains("new_tex"));
        var vfx = Assert.Single(a.Notices, n => n.Kind == "vfx");
        Assert.Contains("\"pos\":[5,5,0]", vfx.Json);
        Assert.DoesNotContain(b.Notices, n => n.Kind is "area-skybox" or "vfx" or "nwnx-player");
    }

    // ---- Rename ------------------------------------------------------------------------

    [DataFact]
    public void RenamedPlayersShowTheirOverrideToEachObserver()
    {
        using var f = new Fixture();
        var alice = f.Pc(f.Join("Alice"));
        var bob = f.Pc(f.Join("Bob"));
        var carol = f.Pc(f.Join("Carol"));
        f.Call("Rename", "SetPCNameOverride", alice, "Stranger", "", "", RenamePlugin.PlayerNameAnonymous, GameObject.InvalidId);
        f.Call("Rename", "SetPCNameOverride", alice, "Ally", "<", ">", RenamePlugin.PlayerNameDefault, bob);
        Assert.Equal(("<Ally>", "Alice"), RenamePlugin.Shown(f.World, alice, bob, "Real", "Alice"));
        Assert.Equal(("Stranger", "Someone"), RenamePlugin.Shown(f.World, alice, carol, "Real", "Alice"));
        f.Call("Rename", "GetPCNameOverride", alice, carol);
        Assert.Equal("Stranger", f.Nwnx.PopAs(NwnxType.String));
        f.Call("Rename", "ClearPCNameOverride", alice, GameObject.InvalidId, 1);
        Assert.Equal(("Real", "Alice"), RenamePlugin.Shown(f.World, alice, bob, "Real", "Alice"));
    }

    // ---- Dialog ------------------------------------------------------------------------

    static GffStruct Node(string text, string script = "", params (int Index, string Active)[] links)
    {
        var n = new GffStruct().SetLocString("Text", new LocString(text)).SetResRef("Script", script).SetString("Speaker", "");
        var list = links.Select(l => new GffStruct().SetDWord("Index", (uint)l.Index).SetResRef("Active", l.Active).SetByte("IsChild", 0)).ToList();
        return n.SetList(text.StartsWith("PC:") ? "EntriesList" : "RepliesList", list);
    }

    [DataFact]
    public void DialogScriptsReadTheirNodeAndCanRewordIt()
    {
        var dlg = new GffStruct()
            .SetResRef("EndConversation", "").SetResRef("EndConverAbort", "pw_abort")
            .SetList("EntryList", new List<GffStruct> { Node("Hello.", "pw_act", (0, ""), (1, "pw_cond")) })
            .SetList("ReplyList", new List<GffStruct> { Node("PC:First"), Node("PC:Second") })
            .SetList("StartingList", new List<GffStruct> { new GffStruct().SetDWord("Index", 0).SetResRef("Active", "") });
        var dir = Directory.CreateTempSubdirectory("openwinter-nwnx-dlg");
        File.WriteAllBytes(Path.Combine(dir.FullName, "pw_dlg.dlg"), Gff.Write(dlg, "DLG "));
        using var f = new Fixture();
        f.World.Resources.AddFirst(new DirectorySource(dir.FullName));
        const string Read = @"
            object oMod = GetModule();
            NWNXCall(""NWNX_Dialog"", ""GetCurrentNodeType""); int t = NWNXPopInt();
            NWNXCall(""NWNX_Dialog"", ""GetCurrentScriptType""); int s = NWNXPopInt();
            NWNXCall(""NWNX_Dialog"", ""GetCurrentNodeID""); int id = NWNXPopInt();
            NWNXCall(""NWNX_Dialog"", ""GetCurrentNodeIndex""); int ix = NWNXPopInt();
            NWNXPushInt(0); NWNXPushInt(0); NWNXCall(""NWNX_Dialog"", ""GetCurrentNodeText""); string text = NWNXPopString();
            SetLocalString(oMod, ""log"", GetLocalString(oMod, ""log"") + ""|"" + IntToString(t) + IntToString(s) + IntToString(id) + IntToString(ix) + "":"" + text);";
        f.Script("pw_act", "void main(){" + Read + "}");
        f.Script("pw_cond", "int StartingConditional(){" + Read + @"
            NWNXPushInt(0); NWNXPushInt(0); NWNXPushString(""Reworded""); NWNXCall(""NWNX_Dialog"", ""SetCurrentNodeText"");
            return TRUE; }");
        f.Script("pw_abort", "void main(){ SetLocalInt(GetModule(), \"aborted\", 1); }");
        var pc = f.Pc(f.Join("Alice"));
        var npc = new Creature { Tag = "npc", Name = new LocString("Barkeep"), Conversation = "pw_dlg" };
        f.World.AddToArea(npc, f.Area, pc.Position + new Vector3(1, 0, 0), 0, exact: true);
        var convo = f.World.System<ConversationService>();
        var views = new List<DialogView>();
        convo.Show += (_, v) => views.Add(v);
        Assert.True(convo.Start(npc, pc, ""));
        // The entry's action (an entry node from the starting list, action script) and the second reply's condition.
        Assert.Equal("|0200:Hello.|2111:PC:Second", f.World.Module.Locals.Strings["log"]);
        Assert.Equal(new[] { "PC:First", "Reworded" }, views[^1].Replies.Select(r => r.Text));
        f.Nwnx.Stack.Clear();
        f.Call("Dialog", "End", npc);
        Assert.Equal(1, f.World.Module.Locals.Ints["aborted"]);
        Assert.False(convo.IsInConversation(pc));
        // Outside a conversation.
        f.Call("Dialog", "GetCurrentNodeType");
        Assert.Equal(-1, f.Nwnx.PopAs(NwnxType.Int));
    }

    // ---- Util --------------------------------------------------------------------------

    [DataFact]
    public void ResourceOverridesRedirectLookups()
    {
        using var f = new Fixture();
        var rm = f.World.Resources;
        var classes = rm.Read("classes", ResType.TwoDA);
        Assert.NotNull(classes);
        f.Call("Util", "SetResourceOverride", (int)ResType.TwoDA, "pw_classes", "classes");
        Assert.Equal(classes, rm.Read("pw_classes", ResType.TwoDA));
        f.Call("Util", "GetResourceOverride", (int)ResType.TwoDA, "pw_classes");
        Assert.Equal("classes", f.Nwnx.PopAs(NwnxType.String));
        f.Call("Util", "SetResourceOverride", (int)ResType.TwoDA, "pw_classes", "");
        Assert.Null(rm.Read("pw_classes", ResType.TwoDA));
    }
}
