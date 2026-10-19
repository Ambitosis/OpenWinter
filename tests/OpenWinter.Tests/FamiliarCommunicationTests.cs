using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Game.Society;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class FamiliarCommunicationTests
{
    static Creature Familiar(World w, Creature master)
    {
        var familiar = w.Creatures.FromBlueprint("nw_fm_bat01")!;
        familiar.Scripts.Clear();
        w.AddToArea(familiar, master.Area!, master.Position, 0);
        AssociateService.Add(w, master, familiar, AssociateService.Familiar);
        return familiar;
    }

    static void Hook(World w)
    {
        const string source = "void main(){object s=GetPCChatSpeaker();SetLocalObject(OBJECT_SELF,\"speaker\",s);SetLocalString(OBJECT_SELF,\"player\",GetPCPlayerName(s));SetLocalInt(OBJECT_SELF,\"possessed\",GetIsPossessedFamiliar(s));SetLocalInt(OBJECT_SELF,\"volume\",GetPCChatVolume());}";
        var code = NssCompiler.Compile(source, "fam_chat", w.Scripts!.Definitions, _ => null, out var error);
        Assert.True(code != null, error);
        w.Scripts.Register("fam_chat", new NcsProgram("fam_chat", code!));
        w.Module.Scripts["Mod_OnPlrChat"] = "fam_chat";
    }

    [DataFact]
    public void ChatAndScriptSpeakerFollowControlledCreatureAndReturnToMaster()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Account");
        var (_, observer) = f.Join("Observer");
        var w = f.Server.World; var master = session.Creature!;
        var familiar = Familiar(w, master); Hook(w);
        var possession = w.System<FamiliarPossession>();
        void Speak(Creature expected, bool possessed, ChatChannel channel, int volume)
        {
            ModuleTransitionTests.Fixture.Drain(client); ModuleTransitionTests.Fixture.Drain(observer);
            client.Send(new ChatSendMessage { Channel = channel, Text = "communication" }.Write());
            f.Server.Tick(.01f);
            foreach (var connection in new[] { client, observer })
            {
                var chat = Assert.Single(ModuleTransitionTests.Fixture.Drain(connection)
                    .Where(p => (MessageType)p[0] == MessageType.Chat).Select(p => ChatMessage.Read(new MessageReader(p))), m => m.Text == "communication");
                Assert.Equal(expected.Id, chat.Speaker);
                Assert.Equal(expected.Name.Resolve(w.Resources.Talk), chat.SpeakerName);
                Assert.Equal(channel, chat.Channel);
            }
            Assert.Equal(expected.Id, w.Module.Locals.Objects["speaker"]);
            Assert.Equal("Account", w.Module.Locals.Strings["player"]);
            Assert.Equal(possessed ? 1 : 0, w.Module.Locals.Ints["possessed"]);
            Assert.Equal(volume, w.Module.Locals.Ints["volume"]);
        }
        Speak(master, false, ChatChannel.Talk, 0);
        Assert.True(possession.Possess(session, familiar));
        Speak(familiar, true, ChatChannel.Talk, 0);
        Speak(familiar, true, ChatChannel.Whisper, 1);
        possession.Release(session);
        Speak(master, false, ChatChannel.Talk, 0);
    }

    [DataFact]
    public void QuickchatUsesControlledSoundsetAndRestoresMasterWithoutAudioPlayback()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Account");
        var w = f.Server.World; var master = session.Creature!; var familiar = Familiar(w, master);
        const int slot = 34;
        var sets = Enumerable.Range(0, w.Resources.TwoDA("soundset").RowCount)
            .Where(r => VoiceService.Resolve(w.Resources, r, slot).Sound.Length > 0)
            .DistinctBy(r => VoiceService.Resolve(w.Resources, r, slot).Sound).Take(2).ToArray();
        Assert.Equal(2, sets.Length); master.SoundSet = sets[0]; familiar.SoundSet = sets[1];
        var possession = w.System<FamiliarPossession>();
        void Speak(Creature expected)
        {
            ModuleTransitionTests.Fixture.Drain(client);
            client.Send(new ClientRequestMessage { Kind = "voice-chat", Json = "{\"id\":34,\"player\":" + master.Id + "}" }.Write());
            f.Server.Tick(.01f);
            var note = Assert.Single(ModuleTransitionTests.Fixture.Drain(client)
                .Where(p => (MessageType)p[0] == MessageType.Notify).Select(p => NotifyMessage.Read(new MessageReader(p))), m => m.Kind == "voice-chat");
            Assert.Equal(expected.Id, note.Subject);
            using var doc = JsonDocument.Parse(note.Json);
            Assert.Equal(expected.SoundSet, doc.RootElement.GetProperty("soundset").GetInt32());
            Assert.Equal(VoiceService.Resolve(w.Resources, expected.SoundSet, slot).Sound,
                doc.RootElement.GetProperty("sound").GetString());
        }
        Speak(master);
        Assert.True(possession.Possess(session, familiar)); Speak(familiar);
        possession.Release(session); Speak(master);
    }
    [DataFact]
    public void PartyChatReachesRemotePossessedMemberAndExcludesNearbyOutsider()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (speaker, outgoing) = f.Join("Speaker");
        var (member, incoming) = f.Join("Party");
        var (_, outsider) = f.Join("Outsider");
        var w = f.Server.World; var master = speaker.Creature!;
        Assert.True(PartyService.Of(w).Join(member.Creature!, master));
        var speakingFamiliar = Familiar(w, master);
        var receivingFamiliar = Familiar(w, member.Creature!);
        var possession = w.System<FamiliarPossession>();
        Assert.True(possession.Possess(speaker, speakingFamiliar));
        Assert.True(possession.Possess(member, receivingFamiliar));
        var remote = new OpenWinter.Game.Areas.Area { Tag = "remote", ResRef = "remote" };
        w.Register(remote); w.Areas.Add(remote);
        w.Jump(receivingFamiliar, new Location(remote.Id, default, 0));
        Assert.NotSame(speaker.Area, member.Area);
        void Chat(Creature expected)
        {
            foreach (var c in new[] { outgoing, incoming, outsider }) ModuleTransitionTests.Fixture.Drain(c);
            outgoing.Send(new ChatSendMessage { Channel = ChatChannel.Party, Text = "party-only" }.Write());
            f.Server.Tick(.01f);
            foreach (var c in new[] { outgoing, incoming })
            {
                var message = Assert.Single(ModuleTransitionTests.Fixture.Drain(c)
                    .Where(p => (MessageType)p[0] == MessageType.Chat)
                    .Select(p => ChatMessage.Read(new MessageReader(p))), m => m.Text == "party-only");
                Assert.Equal(expected.Id, message.Speaker);
            }
            Assert.DoesNotContain(ModuleTransitionTests.Fixture.Drain(outsider)
                .Where(p => (MessageType)p[0] == MessageType.Chat)
                .Select(p => ChatMessage.Read(new MessageReader(p))), m => m.Text == "party-only");
        }
        Chat(speakingFamiliar);
        possession.Release(speaker); Chat(master);
    }

}
