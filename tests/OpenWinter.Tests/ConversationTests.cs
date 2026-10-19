using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Conversations;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class ConversationTests
{
    static GffStruct Node(string text, string script = "", params (int Index, string Active)[] links)
    {
        var n = new GffStruct().SetLocString("Text", new LocString(text)).SetResRef("Script", script).SetString("Speaker", "");
        var list = links.Select(l => new GffStruct().SetDWord("Index", (uint)l.Index).SetResRef("Active", l.Active).SetByte("IsChild", 0)).ToList();
        return n.SetList(text.StartsWith("PC:") ? "EntriesList" : "RepliesList", list);
    }

    [DataFact]
    public void GenderRaceAndClassTokensFollowTheSpeaker()
    {
        var world = new World(TestData.Module);
        var convo = world.System<ConversationService>();
        Creature Pc(int gender)
        {
            var pc = new Creature { Tag = "pc", Gender = (byte)gender, Race = 0 }; // dwarf
            pc.Classes.Add(new CreatureClass { Class = 0, Level = 3 });      // barbarian
            pc.Template.SetLocString("FirstName", new LocString("Brin"));
            world.Register(pc);
            return pc;
        }
        var he = Pc(0);
        var she = Pc(1);
        const string line = "Well met, <lad/lass>! <Lad/Lass>, <sir/madam>, <He/She>, <his/her>, <mister/missus>. A <race> <class>; <Races> and <Classes>; <Race> <Class>.";
        Assert.Equal("Well met, lad! Lad, sir, He, his, mister. A dwarven barbarian; Dwarves and Barbarians; Dwarven Barbarian.", convo.Tokens(line, he, he));
        Assert.Equal("Well met, lass! Lass, madam, She, her, missus. A dwarven barbarian; Dwarves and Barbarians; Dwarven Barbarian.", convo.Tokens(line, she, she));
        // Colour codes and unknown tags pass through; markup is dropped where it has no styling (the journal).
        Assert.Equal("<cþ  >Brin</c> <Unknown>", convo.Tokens("<cþ  ><FirstName></c> <Unknown>", he, he));
        Assert.Equal("Bash it", convo.Tokens("<StartAction>Bash it</Start>", he, he, markup: false));
    }

    [DataFact]
    public void RunsConditionsActionsAndTokens()
    {
        var dlg = new GffStruct()
            .SetResRef("EndConversation", "").SetResRef("EndConverAbort", "")
            .SetList("EntryList", new List<GffStruct>
            {
                Node("Hello <FirstName>.", "", (0, "convo_cond"), (1, "")),
                Node("Done, <CUSTOM101>."),
            })
            .SetList("ReplyList", new List<GffStruct>
            {
                Node("PC:Secret", "convo_act", (1, "")),
                Node("PC:Bye"),
            })
            .SetList("StartingList", new List<GffStruct> { new GffStruct().SetDWord("Index", 0).SetResRef("Active", "") });
        var dir = Directory.CreateTempSubdirectory("openwinter-dlg");
        File.WriteAllBytes(Path.Combine(dir.FullName, "test_dlg.dlg"), Gff.Write(dlg, "DLG "));

        var scripts = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(scripts.FullName, "tests", "scripts"))) scripts = scripts.Parent!;
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(dir.FullName));
        rm.Add(new DirectorySource(Path.Combine(scripts.FullName, "tests", "scripts")));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);

        var npc = new Creature { Tag = "npc", Name = new LocString("Barkeep"), Conversation = "test_dlg" };
        var pc = new Creature { Tag = "pc" };
        pc.Template.SetLocString("FirstName", new LocString("Abigail"));
        var (_, serverEnd) = LoopbackConnection.CreatePair();
        pc.Player = new PlayerSession { Connection = serverEnd };
        world.Register(npc);
        world.Register(pc);

        var convo = world.System<ConversationService>();
        var views = new List<DialogView>();
        convo.Show += (_, v) => views.Add(v);

        // Without the flag the "Secret" reply is hidden.
        Assert.True(convo.Start(npc, pc, ""));
        Assert.Equal("Hello Abigail.", views[^1].Text);
        Assert.Equal(new[] { "PC:Bye" }, views[^1].Replies.Select(r => r.Text));
        convo.End(pc, aborted: true);

        pc.Locals.Ints["flag"] = 1;
        convo.Start(npc, pc, "");
        Assert.Equal(2, views[^1].Replies.Count);
        convo.Choose(pc, 0);
        Assert.Equal(1, pc.Locals.Ints["acted"]);
        Assert.Equal("Done, friend.", views[^1].Text);
        Assert.Equal("[End Dialog]", views[^1].Replies[0].Text);
        convo.Choose(pc, -1);
        Assert.True(views[^1].Ended);
        Assert.False(convo.IsInConversation(pc));
    }

    [DataFact]
    public void DelayedResumeFindsTheConversationWithoutTheDialogueEvent()
    {
        // Found by the playtest autopilot (HotU, the Yawning Portal adventurers): a node script pauses the
        // conversation and resumes it from DelayCommand, where GetPCSpeaker's event is gone. The resume never
        // reached the conversation and it stayed paused for good.
        var dlg = new GffStruct()
            .SetResRef("EndConversation", "").SetResRef("EndConverAbort", "")
            .SetList("EntryList", new List<GffStruct> { Node("Hello.", "", (0, "")), Node("After.") })
            .SetList("ReplyList", new List<GffStruct> { Node("PC:Next", "convo_resume", (1, "")) })
            .SetList("StartingList", new List<GffStruct> { new GffStruct().SetDWord("Index", 0).SetResRef("Active", "") });
        var dir = Directory.CreateTempSubdirectory("openwinter-dlg");
        File.WriteAllBytes(Path.Combine(dir.FullName, "test_pause.dlg"), Gff.Write(dlg, "DLG "));
        var scripts = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(scripts.FullName, "tests", "scripts"))) scripts = scripts.Parent!;
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(dir.FullName));
        rm.Add(new DirectorySource(Path.Combine(scripts.FullName, "tests", "scripts")));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        var npc = new Creature { Tag = "npc", Name = new LocString("Daelan"), Conversation = "test_pause" };
        var pc = new Creature { Tag = "pc" };
        var (_, serverEnd) = LoopbackConnection.CreatePair();
        pc.Player = new PlayerSession { Connection = serverEnd };
        world.Register(npc);
        world.Register(pc);

        var convo = world.System<ConversationService>();
        Assert.True(convo.Start(npc, pc, ""));
        convo.Choose(pc, 0);
        Assert.True(convo.SessionOf(pc)!.Paused);
        for (int i = 0; i < 30; i++) world.Step(1f / 30);
        Assert.False(convo.SessionOf(pc)!.Paused);
        Assert.Equal(1, pc.Locals.Ints.GetValueOrDefault("delayed_speaker"));
    }

    sealed class AnimationLog : IWorldOutput
    {
        public readonly List<(uint Object, int Animation, float Duration)> Played = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) => Played.Add((o.Id, animation, duration));
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) { }
    }

    [DataFact]
    public void PartnersTurnToEachOtherAndLinesPlayTheirAnimations()
    {
        // Measured on the original (parity/visual scenes talk, talkanim): both turn at once, scripts see the turn
        // only when the conversation ends; Animation 38 is the looping talk-normal, held until a line without one.
        var dlg = new GffStruct()
            .SetResRef("EndConversation", "").SetResRef("EndConverAbort", "")
            .SetList("EntryList", new List<GffStruct> {
                Node("Hello.", "", (0, "")).SetDWord("Animation", 38).SetResRef("Sound", "explicit_voice"),
                Node("Bye.", "", (1, "")).SetLocString("Text", new LocString { StrRef = 57763 }) })
            .SetList("ReplyList", new List<GffStruct> { Node("PC:Ha!", "", (1, "")).SetDWord("Animation", 40), Node("PC:Bye") })
            .SetList("StartingList", new List<GffStruct> { new GffStruct().SetDWord("Index", 0).SetResRef("Active", "") });
        var dir = Directory.CreateTempSubdirectory("openwinter-dlg");
        File.WriteAllBytes(Path.Combine(dir.FullName, "test_turn.dlg"), Gff.Write(dlg, "DLG "));
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(dir.FullName));
        rm.AddBaseGame();
        var world = new World(rm);
        var log = new AnimationLog();
        world.Output = log;
        var npc = new Creature { Tag = "npc", Conversation = "test_turn", Position = new System.Numerics.Vector3(0, 3, 0), Facing = 0 };
        var pc = new Creature { Tag = "pc", Facing = 0 };
        var (_, serverEnd) = LoopbackConnection.CreatePair();
        pc.Player = new PlayerSession { Connection = serverEnd };
        world.Register(npc);
        world.Register(pc);
        var convo = world.System<ConversationService>();

        var views = new List<DialogView>();
        convo.Show += (_, view) => views.Add(view);
        Assert.True(convo.Start(npc, pc, ""));
        Assert.Equal("explicit_voice", views[^1].Sound);
        Assert.Equal(-MathF.PI / 2, convo.ShownFacing(npc)!.Value, 3);
        Assert.Equal(MathF.PI / 2, convo.ShownFacing(pc)!.Value, 3);
        Assert.Equal(0f, npc.Facing);
        Assert.Equal((npc.Id, 8), (log.Played[^1].Object, log.Played[^1].Animation));
        Assert.True(log.Played[^1].Duration > 1000);

        convo.Choose(pc, 0);
        // The reply's forceful talk on the player; the next line has no animation, so the speaker's talk stops.
        Assert.Equal("vs_hhenchm1_069", views[^1].Sound); // installed TLK voice when DLG Sound is empty
        Assert.Contains(log.Played, p => p.Object == pc.Id && p.Animation == 10);
        Assert.Equal((npc.Id, 0), (log.Played[^1].Object, log.Played[^1].Animation));

        // A moving player keeps their movement facing; the stationary speaker follows their position.
        pc.Position = new System.Numerics.Vector3(3, 3, 0);
        pc.Facing = 0.7f;
        convo.Step(1f / 30);
        Assert.Null(convo.ShownFacing(pc));
        Assert.Equal(0, convo.ShownFacing(npc)!.Value, 3);
        Assert.Equal(0f, npc.Facing); // still only a visual facing until the conversation ends

        // Shoved aside by the walking player (PersonalSpace pushes idle creatures), the speaker still follows.
        npc.Position = new System.Numerics.Vector3(0, 3.3f, 0);
        pc.Position = new System.Numerics.Vector3(3, 0.3f, 0);
        convo.Step(1f / 30);
        Assert.Equal(-MathF.PI / 4, convo.ShownFacing(npc)!.Value, 3);
        pc.Position = new System.Numerics.Vector3(3, 3.3f, 0);
        convo.Step(1f / 30);
        Assert.Equal(0, convo.ShownFacing(npc)!.Value, 3);

        convo.Choose(pc, 0);
        Assert.False(convo.IsInConversation(pc));
        Assert.Equal((pc.Id, 0), (log.Played[^1].Object, log.Played[^1].Animation));
        Assert.Null(convo.ShownFacing(npc));
        Assert.Equal(0, npc.Facing, 3);
        Assert.Equal(0.7f, pc.Facing, 3);
    }

    [Fact]
    public void ConversationLineAnimationsAsMeasured()
    {
        Assert.Equal((-1, false), Animations.ConversationLine(0));
        Assert.Equal((8, true), Animations.ConversationLine(38));
        Assert.Equal((9, true), Animations.ConversationLine(39));
        Assert.Equal((10, true), Animations.ConversationLine(40));
        Assert.Equal((11, true), Animations.ConversationLine(41));
        Assert.Equal((107, false), Animations.ConversationLine(29));
        Assert.Equal((113, false), Animations.ConversationLine(70));
        Assert.Equal((-1, false), Animations.ConversationLine(88));
    }

    /// <summary>A world with one conversation (end script convo_end, abort script convo_abort) and an NPC 3 m from a player.</summary>
    static (World World, ConversationService Convo, Creature Npc, Creature Pc) Talk(string name, List<GffStruct> entries, List<GffStruct> replies)
    {
        var dlg = new GffStruct()
            .SetResRef("EndConversation", "convo_end").SetResRef("EndConverAbort", "convo_abort")
            .SetList("EntryList", entries).SetList("ReplyList", replies)
            .SetList("StartingList", new List<GffStruct> { new GffStruct().SetDWord("Index", 0).SetResRef("Active", "") });
        var dir = Directory.CreateTempSubdirectory("openwinter-dlg");
        File.WriteAllBytes(Path.Combine(dir.FullName, name + ".dlg"), Gff.Write(dlg, "DLG "));
        var scripts = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(scripts.FullName, "tests", "scripts"))) scripts = scripts.Parent!;
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(dir.FullName));
        rm.Add(new DirectorySource(Path.Combine(scripts.FullName, "tests", "scripts")));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        var npc = new Creature { Tag = "npc", Name = new LocString("Bandit"), Conversation = name, Position = new System.Numerics.Vector3(3, 0, 0) };
        var pc = new Creature { Tag = "pc", Name = new LocString("Aluvian") };
        var (_, serverEnd) = LoopbackConnection.CreatePair();
        pc.Player = new PlayerSession { Connection = serverEnd };
        world.Register(npc);
        world.Register(pc);
        return (world, world.System<ConversationService>(), npc, pc);
    }

    static int Ended(Creature pc) => pc.Locals.Ints.GetValueOrDefault("ended");
    static int Aborted(Creature pc) => pc.Locals.Ints.GetValueOrDefault("aborted");

    [DataFact]
    public void AStopAtTheLastLineFinishesTheConversationAndAnywhereElseAbortsIt()
    {
        // convoend, convoend2: 10 m apart stops it; at a line whose only answer leads nowhere that counts as finished.
        var (_, convo, npc, pc) = Talk("test_stop",
            [Node("First line.", "", (0, "")), Node("Second line.", "", (1, ""))],
            [Node("PC:Keep talking.", "", (1, "")), Node("PC:Finish.")]);
        Assert.True(convo.Start(npc, pc, ""));
        pc.Position = new System.Numerics.Vector3(-6, 0, 0); // 9 m: still talking
        convo.Step(0.1f);
        Assert.True(convo.IsInConversation(pc));
        pc.Position = new System.Numerics.Vector3(-7, 0, 0); // 10 m
        convo.Step(0.1f);
        Assert.False(convo.IsInConversation(pc));
        Assert.Equal((0, 1), (Ended(pc), Aborted(pc)));

        pc.Position = default;
        Assert.True(convo.Start(npc, pc, ""));
        convo.Choose(pc, 0);
        npc.Position = new System.Numerics.Vector3(30, 0, 0);
        convo.Step(0.1f);
        Assert.Equal((1, 1), (Ended(pc), Aborted(pc)));

        // A new conversation started over one at its last line finishes the old one, elsewhere aborts it.
        npc.Position = new System.Numerics.Vector3(3, 0, 0);
        convo.Start(npc, pc, "");
        convo.Start(npc, pc, "");
        Assert.Equal((1, 2), (Ended(pc), Aborted(pc)));
        convo.Choose(pc, 0);
        convo.Start(npc, pc, "");
        Assert.Equal((2, 2), (Ended(pc), Aborted(pc)));
    }

    [DataFact]
    public void ClosingTheWindowChoosesTheOnlyEndingAnswerAndPostsEveryLineToChat()
    {
        var (_, convo, npc, pc) = Talk("test_close",
            [Node("First line.", "", (0, "")), Node("Second line.", "", (1, ""))],
            [Node("PC:Keep talking.", "", (1, "")), Node("PC:Finish.")]);
        var chat = new List<(uint, string)>();
        convo.Line += (_, speaker, text) => chat.Add((speaker.Id, text));
        // Esc at a line that leads on aborts.
        convo.Start(npc, pc, "");
        convo.Dismiss(pc);
        Assert.Equal((0, 1), (Ended(pc), Aborted(pc)));
        // At a line whose only answer ends it, Esc chooses that answer (posted to chat) and finishes.
        convo.Start(npc, pc, "");
        convo.Choose(pc, 0);
        convo.Dismiss(pc);
        Assert.Equal((1, 1), (Ended(pc), Aborted(pc)));
        Assert.Equal(new[] { (npc.Id, "First line."), (npc.Id, "First line."), (pc.Id, "PC:Keep talking."), (npc.Id, "Second line."), (pc.Id, "PC:Finish.") }, chat);
    }

    [DataFact]
    public void AFirstLineWithNoAnswersIsSpokenAndFinishes()
    {
        // store_noreply: no window, the end script runs at once.
        var (_, convo, npc, pc) = Talk("test_oneliner", [Node("Here is what I sell.")], []);
        var views = new List<DialogView>();
        convo.Show += (_, v) => views.Add(v);
        Assert.True(convo.Start(npc, pc, ""));
        Assert.False(convo.IsInConversation(pc));
        Assert.Empty(views);
        Assert.Equal((1, 0), (Ended(pc), Aborted(pc)));
    }

    [DataFact]
    public void ADestroyedTalkerRunsNeitherScript()
    {
        var (world, convo, npc, pc) = Talk("test_gone", [Node("First line.", "", (0, ""))], [Node("PC:Finish.")]);
        convo.Start(npc, pc, "");
        world.Destroy(npc);
        convo.Step(0.1f);
        Assert.False(convo.IsInConversation(pc));
        Assert.Equal((0, 0), (Ended(pc), Aborted(pc)));
    }

    [DataFact]
    public void AStoreOpenedOverALineClosesItButOneOpenedByAReplyLetsItGoOn()
    {
        var (_, convo, npc, pc) = Talk("test_store",
            [Node("Want to trade?", "", (0, "")), Node("Anything else?", "", (1, ""))],
            [Node("PC:Show me your wares.", "", (1, "")), Node("PC:No.")]);
        // While the first line shows (its answer leads on): aborted, like Esc.
        convo.Start(npc, pc, "");
        convo.StoreOpened(pc);
        Assert.Equal((0, 1), (Ended(pc), Aborted(pc)));
        // During a reply's action (InReply) the conversation goes on.
        convo.Start(npc, pc, "");
        convo.SessionOf(pc)!.InReply = true;
        convo.StoreOpened(pc);
        Assert.True(convo.IsInConversation(pc));
    }
}
