using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAddMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;

namespace OpenWinter.Tests;

/// <summary>
/// Conversations, the journal and cutscene mode through the NWN bridge, from frames recorded on nwserver (the Prelude's
/// Pavel, his first lines, an answer, the last line, Esc, and the journal window opening).
/// </summary>
public class NwnBridgeDialogTests
{
    const uint Pc = 0x7FFFFFFF, Pavel = 0x13;

    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 0;
        public string TwoDA(string table, int row, string column) => (table, row, column) switch
        {
            ("classes", 10, "Name") => "1",
            ("racialtypes", 1, "ConverName") => "2",
            ("portraits", 96, "BaseResRef") => "HHenchM1_",
            _ => "",
        };
        public string Text(uint strRef, byte gender) => (strRef, gender) switch
        {
            (1, _) => "Wizard",
            (2, _) => "Elven",
            (57795, 1) => "Finally, you're up.",
            (57763, 1) => "You're <FullName>, aren't you?",
            (57725, 1) => "Yes, I am <FirstName>.",
            (57723, 1) => "No, I think you have me confused with someone else.",
            (57779, 1) => "Don't forget to say hi to my twin brother Bim.",
            (57878, 0) => "Academy Training",
            (57879, 0) => "You have trained long at the Academy.",
            _ => $"#{strRef}/{gender}",
        };
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    static (NwnDialog, List<MessageReader>) Dialog()
    {
        var output = new List<MessageReader>();
        void Emit(byte[] m) => output.Add(new MessageReader(m));
        var dialog = new NwnDialog(new NwnWorldMirror(new Data(), Emit), new Data(), Emit);
        // The player list names the player's character, as at login.
        dialog.ToClient(new PlayerListMessages.PlayerListAdd
        {
            Player = new PlayerListMessages.Player
            {
                ObjectId = Pc, PlayerName = "probe", IsPrimaryPc = true,
                PrimaryPc = new PlayerListMessages.Player.PrimaryPC { ObjectId = Pc, FirstName = NwnLocString.FromText("Aluvian"), LastName = NwnLocString.FromText("Darkstar") },
            },
        });
        return (dialog, output);
    }

    /// <summary>A recorded frame without its direction byte and message numbers.</summary>
    static NwnMessage Frame(string hex, NwnMessage into)
    {
        into.Read(new NwnWsReader(Convert.FromHexString(hex[6..]), new NwnSessionContext(_ => 0, (_, _, _) => "")));
        return into;
    }

    static string Bytes(NwnMessage m)
    {
        var w = new NwnWsWriter(new NwnSessionContext(_ => 0, (_, _, _) => ""));
        m.Write(w);
        return $"70{m.Major:X2}{m.Minor:X2}" + Convert.ToHexString(w.ToArray());
    }

    // Pavel's second line: the line, its chat copy with the voice-over, and the two answers.
    const string Entry = "5014011113000000111300000011FFFFFF7F120101A3E10000";
    const string EntryChat = "50090C111300000011FFFFFF7F120101A3E100000D0F76735F6868656E63686D315F303639020011FFFFFF7F";
    const string Answers = "50140207000000000200020011FFFFFF7F070200000007000000001201017DE1000007000000001201017BE100000702000000";
    // The last line: the server offers only its end choice.
    const string LastEntry = "5014011113000000111300000011FFFFFF7F120101B3E10000";
    const string LastAnswers = "50140207100000000201020011FFFFFF7F0701000000070000000012000000000007FFFFFFFF";

    static void Step(NwnDialog dialog, string entry, string chat, string answers)
    {
        Assert.True(dialog.ToClient(Frame(entry, new DialogMessages.DialogEntry())));
        if (chat.Length > 0) Assert.True(dialog.ToClient(Frame(chat, new ChatMessages.ChatTokenTalkNoBubble())));
        Assert.True(dialog.ToClient(Frame(answers, new DialogMessages.DialogReplies())));
    }

    [Fact]
    public void ALineAndItsAnswersBecomeOneDialogStep()
    {
        var (dialog, output) = Dialog();
        Step(dialog, Entry, EntryChat, Answers);
        var chat = ChatMessage.Read(output[0]);
        Assert.Equal((ChatChannel.Dialog, Pavel, "You're Aluvian Darkstar, aren't you?"), (chat.Channel, chat.Speaker, chat.Text));
        var step = DialogMessage.Read(output[1]);
        Assert.Equal((Pavel, Pavel, false), (step.Owner, step.Speaker, step.Ended));
        // The line is the female form (the token target's gender), tokens filled in from the player list.
        Assert.Equal("You're Aluvian Darkstar, aren't you?", step.Text);
        Assert.Equal("vs_hhenchm1_069", step.Sound);
        Assert.Equal([(0, "Yes, I am Aluvian."), (1, "No, I think you have me confused with someone else.")], step.Replies);
        Assert.True(dialog.Open);
    }

    [Fact]
    public void AnAnswerGoesBackAsTheReplyNwmainSends()
    {
        var (dialog, _) = Dialog();
        Step(dialog, Entry, EntryChat, Answers);
        // As recorded after picking the first answer of entry 0.
        Assert.Equal("7014031113000000070000000003000700000000", Bytes(dialog.Reply(0)!));
        // The second answer shown is the server's reply 2 (reply 1 failed its condition).
        Assert.Equal("7014031113000000070200000003000700000000", Bytes(dialog.Reply(1)!));
        Assert.Null(dialog.Reply(2));
        // Esc anywhere but the last line escapes (recorded; the server closed the conversation).
        Assert.Equal("701403111300000007FFFFFFFF03010700000000", Bytes(dialog.Reply(-1)!));
    }

    [Fact]
    public void TheLastLineOffersTheEndChoiceAndEscPicksIt()
    {
        var (dialog, output) = Dialog();
        Step(dialog, LastEntry, "", LastAnswers);
        var step = DialogMessage.Read(output.Single(m => m.Type == MessageType.Dialog));
        Assert.Equal([(-1, NwnDialog.EndDialog)], step.Replies);
        // The end choice, as recorded (the server then closed the conversation).
        Assert.Equal("701403111300000007FFFFFFFF03000710000000", Bytes(dialog.Reply(-1)!));
    }

    [Fact]
    public void TheServerClosingTheConversationClosesTheWindow()
    {
        var (dialog, output) = Dialog();
        Step(dialog, Entry, EntryChat, Answers);
        output.Clear();
        // The player's empty closing line with a bubble, then the close.
        Assert.True(dialog.ToClient(Frame("50090B11FFFFFF7F11FFFFFF7F1200000000000D0002001113000000", new ChatMessages.ChatTokenTalk())));
        Assert.True(dialog.ToClient(Frame("501405", new DialogMessages.DialogClose())));
        var closed = DialogMessage.Read(Assert.Single(output));
        Assert.True(closed.Ended);
        Assert.Equal(Pavel, closed.Owner);
        Assert.False(dialog.Open);
        Assert.Null(dialog.Reply(0));
    }

    [Fact]
    public void OneLinersAreSpokenOverTheTalker()
    {
        var (dialog, output) = Dialog();
        dialog.ToClient(new CustomTokenMessages.CustomTokenSingle { TokenNumber = 100, TokenValue = "the north gate" });
        Assert.True(dialog.ToClient(new ChatMessages.ChatTokenTalk { SpeakerId = 9, TargetId = Pc, Message = NwnLocString.FromText("Meet me at <CUSTOM100>, <FirstName>.") }));
        var chat = ChatMessage.Read(Assert.Single(output));
        Assert.Equal((ChatChannel.Talk, 9u, "Meet me at the north gate, Aluvian."), (chat.Channel, chat.Speaker, chat.Text));
    }

    [Fact]
    public void TokensReadTheTargetsRaceClassAndGender()
    {
        var (dialog, _) = Dialog();
        dialog.ToClient(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListUpdateCreature
                {
                    ObjectId = Pc,
                    PrimaryAttributes = new PrimaryAttributes { Race = 1, Gender = 1, AlignmentGoodEvil = 50, AlignmentLawChaos = 15, Classes = [new CreatureClassEntry { ClassId = 10, Level = 3 }] },
                },
            },
        });
        Assert.Equal("Well met, Elven Wizard, my lady. Level 3, Chaotic neutral. [[action]][Bow][[/]]",
            dialog.Tokens.Resolve("Well met, <Race> <Class>, my <lord/lady>. Level <Level>, <Alignment>. <StartAction>[Bow]</Start>", Pc));
        Assert.Equal("[Bow] <Unknown>", dialog.Tokens.Resolve("<StartAction>[Bow]</Start> <Unknown>", Pc, markup: false));
    }

    [Fact]
    public void TheJournalArrivesWhenItsWindowOpens()
    {
        var (dialog, output) = Dialog();
        // At login: only the journal-updated line, for a quest the bridge hasn't seen yet.
        Assert.True(dialog.ToClient(Frame("501C0C12010016E2000002010200", new JournalMessages.JournalUpdated())));
        var line = NotifyMessage.Read(Assert.Single(output));
        Assert.Equal("journal", line.Kind);
        Assert.Equal("""{"tag":"","name":"Academy Training","removed":true,"update":true}""", line.Json);
        output.Clear();
        // The client asks for the journal: the quest screen opens, and the server sends the whole journal (recorded).
        Assert.Equal("701C0A", Bytes(dialog.FromClient("journal", "{}")!));
        Assert.True(dialog.ToClient(Frame(
            "501C090201020108010000000500800E110000004A4F55525F4D315130547261696E696E6712010016E2000012010017E20000074C09070007CDFB1700080A000000070000000005FFFF0200",
            new JournalMessages.JournalFullUpdate())));
        var notes = output.Select(NotifyMessage.Read).ToList();
        Assert.Equal(["journal-reset", "journal"], notes.Select(n => n.Kind));
        var quest = JsonDocument.Parse(notes[1].Json).RootElement;
        Assert.Equal("JOUR_M1Q0Training", quest.GetProperty("tag").GetString());
        Assert.Equal("Academy Training", quest.GetProperty("name").GetString());
        Assert.Equal("You have trained long at the Academy.", quest.GetProperty("text").GetString());
        Assert.Equal(10, quest.GetProperty("state").GetInt32());
        // The day stamp counts 28-day months from year 0: day 1 of month 6, 1372.
        Assert.Equal((1372 * 12 + 5) * 28, quest.GetProperty("day").GetInt32());
        Assert.False(quest.GetProperty("update").GetBoolean());
        Assert.False(quest.GetProperty("end").GetBoolean());
        output.Clear();
        // A later update of a known quest sends it again as an update (the line and the journal button's flash).
        dialog.ToClient(Frame("501C0C12010016E2000002010200", new JournalMessages.JournalUpdated()));
        var update = JsonDocument.Parse(NotifyMessage.Read(Assert.Single(output)).Json).RootElement;
        Assert.Equal("JOUR_M1Q0Training", update.GetProperty("tag").GetString());
        Assert.True(update.GetProperty("update").GetBoolean());
        output.Clear();
        dialog.ToClient(new JournalMessages.JournalRemoveQuest { PlotId = "JOUR_M1Q0Training" });
        Assert.Contains("\"removed\":true", NotifyMessage.Read(Assert.Single(output)).Json);
    }

    [Fact]
    public void CutsceneModeAndFadesReachTheClient()
    {
        var (dialog, output) = Dialog();
        dialog.ToClient(new CutsceneMessages.CutsceneStatus { Active = true, LeftMouseButtonEnabled = false });
        dialog.ToClient(new CutsceneMessages.CutsceneFadeToBlack { Speed = 0.5f });
        var notes = output.Select(NotifyMessage.Read).ToList();
        Assert.Equal(("cutscene", """{"enabled":true,"leftClick":false}"""), (notes[0].Kind, notes[0].Json));
        Assert.Equal(("fade", """{"op":"to-black","speed":0.5}"""), (notes[1].Kind, notes[1].Json));
        Assert.IsType<CutsceneMessages.CutsceneCancel>(dialog.FromClient("cutscene-abort", "{}"));
        Assert.Null(dialog.FromClient("area-ready", "{}"));
    }

    [Fact]
    public void TheSpeakersPortraitAndNameComeWithTheCreature()
    {
        var output = new List<MessageReader>();
        var mirror = new NwnWorldMirror(new Data(), m => output.Add(new MessageReader(m)));
        // As Pavel arrives: portrait 96 in the creature's first update, beside its appearance.
        mirror.ObjectList(new GameObjUpdateMessages.GameObjUpdateObjList
        {
            Entries =
            {
                new GameObjUpdateObjListAddCreature { ObjectId = Pavel, Position = (18, 8, 0) },
                new GameObjUpdateObjListAppearanceCreature
                {
                    ObjectId = Pavel, Flags = UpdateCreatureAppearanceFlags.Name | UpdateCreatureAppearanceFlags.Race,
                    Name = new NwnTagged(1, new OriginalCreatureName { First = NwnLocString.FromText("Pavel"), Last = NwnLocString.FromText("") }),
                },
                new GameObjUpdateObjListUpdateCreature { ObjectId = Pavel, PortraitId = 96 },
            },
        });
        var spawn = SpawnMessage.Read(output.Single(m => m.Type == MessageType.Spawn));
        Assert.Equal("po_hhenchm1_", spawn.Portrait);
        Assert.Equal("Pavel", mirror.NameOf(Pavel));
    }
}
