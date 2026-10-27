using System.IO.Compression;
using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;

namespace OpenWinter.Tests;

/// <summary>Levelling up and creating characters on an NWN:EE server through the bridge.</summary>
public class NwnBridgeProgressionTests
{
    static NwnSessionContext Context(Func<string, int> rows) => new(rows, (_, _, _) => "");

    [Fact]
    public void ThePlayersLevelUpInfoEndsAfterItsStartingPackages()
    {
        // As recorded for a first-level human fighter given the experience for a level: unspent skill points, the
        // ranks of all 28 skills, the associates (none), the starting package (4) and one more byte; nothing follows.
        var frame = Convert.FromHexString("500501044911FFFFFF7F050100060B00073200000008C20000000201050000040004000400040404000400040004000400040004040400040004000400040004000400040204000400040004000400040004000400040008000000000E0000000008000000000E0000000007040000000300");
        var list = new GameObjUpdateMessages.GameObjUpdateObjList();
        list.Read(new NwnWsReader(frame.AsMemory(3), Context(t => t == "skills" ? 28 : 0)));
        var info = Assert.IsType<GameObjUpdateObjListPlayerInfo>(Assert.Single(list.Entries));
        var levelUp = info.AcGoldWeightLevelup!.LevelUpInfo!;
        Assert.True(info.AcGoldWeightLevelup.CanLevelUp);
        Assert.Equal(28, levelUp.SkillRanks.Count);
        Assert.Equal([3, 10, 18], levelUp.SkillRanks.Select((rank, skill) => (rank, skill)).Where(x => x.rank != 0).Select(x => x.skill));
        Assert.Equal(4u, levelUp.StartingPackageNew);
    }

    sealed record Frame(bool Sent, NwnMessage Message, byte[] Bytes);

    /// <summary>A recorded level-up of a first-level human wizard given 10000 experience (parity/suites/bridge_progression).</summary>
    static List<Frame> Recording(NwnSessionContext context)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "nwn-levelup-wizard.jsonl.gz");
        using var lines = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        var frames = new List<Frame>();
        while (lines.ReadLine() is { } line)
        {
            var d = JsonDocument.Parse(line).RootElement;
            bool sent = d.GetProperty("dir").GetString() == "c2s";
            var bytes = Convert.FromHexString(d.GetProperty("hex").GetString()!);
            var m = NwnMessageRegistry.Create(bytes[1], bytes[2], sent ? NwnDirection.ClientToServer : NwnDirection.ServerToClient)!;
            m.Read(new NwnWsReader(bytes.AsMemory(3), context));
            frames.Add(new Frame(sent, m, bytes));
        }
        return frames;
    }

    [DataFact]
    public void ALevelTakenInTheWindowGoesToTheServerAsNwmainSendsIt()
    {
        var data = new NwnResManData(TestData.Paths);
        var context = new NwnSessionContext(data.TwoDARowCount, data.TwoDA);
        var notes = new List<NotifyMessage>();
        var sent = new List<NwnMessage>();
        var rules = new NwnRules(data, b => notes.Add(NotifyMessage.Read(new MessageReader(b))));
        var progression = new NwnProgression(rules, sent.Add);
        var frames = Recording(context);
        NotifyMessage Note(string kind) => Assert.Single(notes, n => n.Kind == kind);

        // The server describes the player's creature, and that a level is due.
        foreach (var f in frames.TakeWhile(f => f.Message is not LevelupMessages.LevelUpBeginS2C))
            if (f.Message is GameObjUpdateMessages.GameObjUpdateObjList list) progression.ObjectList(list);
        Assert.True(progression.CanLevelUp);

        // Opening the window asks the server for the classes it allows, and shows them when it answers.
        Assert.True(progression.FromClient("levelup-data", "{}"));
        Assert.IsType<LevelupMessages.LevelUpBegin>(Assert.Single(sent));
        Assert.Empty(notes);
        Assert.True(progression.ToClient(frames.Single(f => f.Message is LevelupMessages.LevelUpBeginS2C).Message));
        var summary = JsonDocument.Parse(Note("levelup-data").Json).RootElement;
        Assert.Equal(1, summary.GetProperty("level").GetInt32());
        Assert.Equal(0x7FFFFFFFu, notes[0].Subject);
        var offered = summary.GetProperty("classes").EnumerateArray().Where(c => c.GetProperty("available").GetBoolean()).Select(c => c.GetProperty("id").GetInt32());
        // Lawful neutral: no barbarian, bard or paladin.
        Assert.Equal([2, 3, 4, 5, 7, 8, 9, 10], offered.Where(c => c <= 10));

        // The recommended choices for a wizard level, then the level itself.
        progression.FromClient("levelup-data", """{"choice":{"class":10},"auto":true}""");
        var plan = JsonDocument.Parse(Note("levelup-plan").Json).RootElement;
        Assert.True(plan.GetProperty("ok").GetBoolean());
        progression.FromClient("levelup-apply", JsonSerializer.Serialize(new { choice = plan.GetProperty("auto") }));
        var levelUp = Assert.IsType<LevelupMessages.LevelUpNull>(sent[^1]);
        var w = new NwnWsWriter(context);
        levelUp.Write(w);
        var recorded = frames.Single(f => f.Sent && f.Message is LevelupMessages.LevelUpNull).Bytes;
        // Second level: the hit die in full; the package's skills, its two spells, the school kept.
        Assert.Equal(Convert.ToHexString(recorded[3..]), Convert.ToHexString(w.ToArray()));

        // The server's confirmation closes the window.
        Assert.True(progression.ToClient(new LevelupMessages.LevelUpConfirm()));
        var result = JsonDocument.Parse(Note("levelup-result").Json).RootElement;
        Assert.True(result.GetProperty("ok").GetBoolean());
        Assert.Equal(2, result.GetProperty("level").GetInt32());
    }

    [DataFact]
    public void ARefusedLevelSaysSo()
    {
        var data = new NwnResManData(TestData.Paths);
        var notes = new List<NotifyMessage>();
        var progression = new NwnProgression(new NwnRules(data, b => notes.Add(NotifyMessage.Read(new MessageReader(b)))), _ => { });
        Assert.True(progression.ToClient(new LevelupMessages.LevelUpDeny()));
        var result = JsonDocument.Parse(Assert.Single(notes).Json).RootElement;
        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Equal("server", result.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    static string Fighter(string name, int cls = 4) => JsonSerializer.Serialize(new
    {
        character = new
        {
            firstName = name, lastName = "Probe", gender = 0, race = 6, portraitId = 60, lawChaos = 50, goodEvil = 50,
            abilities = new[] { 14, 14, 14, 12, 12, 10 }, level = new { @class = cls, package = -1 },
        },
    });

    /// <summary>Asks for the class's recommended first level, then creates the character with it.</summary>
    static void Create(NwnCharacterCreation creation, List<NotifyMessage> notes, string name, int cls = 4)
    {
        creation.FromClient("chargen-validate", Fighter(name, cls).Replace("\"character\"", "\"auto\":true,\"character\""));
        var plan = JsonDocument.Parse(notes.Last(n => n.Kind == "chargen-plan").Json).RootElement.GetProperty("plan");
        Assert.True(plan.GetProperty("ok").GetBoolean());
        notes.Clear();
        creation.FromClient("chargen-create", Fighter(name, cls).Replace($"{{\"class\":{cls},\"package\":-1}}", plan.GetProperty("auto").GetRawText()));
    }

    [DataFact]
    public void ANewCharacterGoesToTheServerSmallEnoughForItsConnection()
    {
        var data = new NwnResManData(TestData.Paths);
        var notes = new List<NotifyMessage>();
        var sent = new List<NwnMessage>();
        var rules = new NwnRules(data, b => notes.Add(NotifyMessage.Read(new MessageReader(b))));
        var creation = new NwnCharacterCreation(rules, sent.Add);
        // The creation window's checks come from the rules, before any character is chosen.
        Create(creation, notes, "Fightr");
        var upload = Assert.IsType<LoginMessages.LoginNewServerCharacter>(Assert.Single(sent));
        Assert.InRange(upload.CharacterData.Length, 1000, NwnCharacterCreation.MaxFileBytes);
        var bic = Gff.Read(upload.CharacterData);
        Assert.Equal("Fightr", bic.GetLocString("FirstName").FirstString);
        Assert.Equal(4, bic.GetList("ClassList")[0].GetInt("Class"));
        // Skill ranks are kept, unranked ones too, up to the last ranked skill.
        var ranks = bic.GetList("SkillList");
        Assert.All(ranks, s => Assert.True(s.Has("Rank")));
        Assert.NotEqual(0, ranks[^1].GetInt("Rank"));
        Assert.Empty(notes);

        // The module loading is the server taking it.
        Assert.False(creation.ToClient(new ModuleMessages.ModuleInfo()));
        Assert.True(JsonDocument.Parse(Assert.Single(notes).Json).RootElement.GetProperty("ok").GetBoolean());
    }

    [DataFact]
    public void ACharacterTooLargeToSendIsRefusedBeforeItGoes()
    {
        var data = new NwnResManData(TestData.Paths);
        var notes = new List<NotifyMessage>();
        var sent = new List<NwnMessage>();
        var creation = new NwnCharacterCreation(new NwnRules(data, b => notes.Add(NotifyMessage.Read(new MessageReader(b)))), sent.Add);
        // A wizard's spellbook makes its file too large for the server's connection.
        Create(creation, notes, "Wizzy", 10);
        Assert.Empty(sent);
        var result = JsonDocument.Parse(Assert.Single(notes).Json).RootElement;
        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Equal("size", result.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [DataFact]
    public void ARefusedCharacterSaysWhy()
    {
        var data = new NwnResManData(TestData.Paths);
        var notes = new List<NotifyMessage>();
        var creation = new NwnCharacterCreation(new NwnRules(data, b => notes.Add(NotifyMessage.Read(new MessageReader(b)))), _ => { });
        Create(creation, notes, "Fightr");
        Assert.True(creation.ToClient(new LoginMessages.LoginCharacterFail { Reason = 66165 }));
        var result = JsonDocument.Parse(Assert.Single(notes).Json).RootElement;
        Assert.False(result.GetProperty("ok").GetBoolean());
    }
}
