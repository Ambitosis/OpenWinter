using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ChatEventMutationTests
{
    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection Client;
        public Fixture()
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            var area = new Area { ResRef = "chat", Width = 1, Height = 1 };
            World.Register(area); World.Areas.Add(area);
            Server = new GameServer(World, new ServerOptions { StartArea = "chat", Vault = VaultMode.Local });
            var pair = LoopbackConnection.CreatePair(); Client = pair.Client; Server.Accept(pair.Server);
            Client.Send(new HelloMessage { PlayerName = "Chat tester" }.Write()); Server.Tick(.01f); Drain();
        }
        public void Script(string name, string body)
        {
            var code = NssCompiler.Compile("void main(){" + body + "}", name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(code != null, error); World.Scripts.Register(name, new NcsProgram(name, code!));
        }
        List<ChatMessage> Drain()
        {
            var messages = new List<ChatMessage>();
            while (Client.TryReceive(out var data))
                if ((MessageType)data[0] == MessageType.Chat) messages.Add(ChatMessage.Read(new MessageReader(data)));
            return messages;
        }
        public List<ChatMessage> Chat()
        {
            World.Module.Scripts["Mod_OnPlrChat"] = "handler";
            Client.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = "original" }.Write());
            Server.Tick(.01f); return Drain();
        }
        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            foreach (var source in World.Resources.Sources.OfType<IDisposable>().Distinct()) source.Dispose();
        }
    }

    [DataFact]
    public void DirectChatEditsAndSuppressionReachNetworkDelivery()
    {
        using var f = new Fixture();
        f.Script("handler", "SetPCChatMessage(\"edited\");SetPCChatVolume(TALKVOLUME_WHISPER);");
        var chat = Assert.Single(f.Chat()); Assert.Equal("edited", chat.Text); Assert.Equal(ChatChannel.Whisper, chat.Channel);
        f.Script("handler", "SetPCChatMessage(\"\");"); Assert.Empty(f.Chat());
    }

    [DataFact]
    public void NestedScriptAndChunkReturnChatEditsWhileKeepingTheirDirectIdentityScoped()
    {
        using var f = new Fixture();
        foreach (bool chunk in new[] { false, true })
        {
            const string edits = "SetPCChatMessage(\"nested\");SetPCChatVolume(TALKVOLUME_WHISPER);SetLocalInt(OBJECT_SELF,\"childEvent\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"childDirect\",GetCurrentlyRunningEvent(FALSE));";
            f.Script("nested", edits);
            string call = chunk ? "ExecuteScriptChunk(\"" + edits.Replace("\"", "\\\"") + "\",OBJECT_SELF);" : "ExecuteScript(\"nested\",OBJECT_SELF);";
            f.Script("handler", call + "SetLocalInt(OBJECT_SELF,\"parentDirect\",GetCurrentlyRunningEvent(FALSE));SetLocalString(OBJECT_SELF,\"readback\",GetPCChatMessage());");
            var chat = Assert.Single(f.Chat()); Assert.Equal("nested", chat.Text); Assert.Equal(ChatChannel.Whisper, chat.Channel);
            Assert.Equal("nested", f.World.Module.Locals.Strings["readback"]);
            Assert.Equal(3017, f.World.Module.Locals.Ints["childEvent"]);
            Assert.Equal(0, f.World.Module.Locals.Ints["childDirect"]);
            Assert.Equal(3017, f.World.Module.Locals.Ints["parentDirect"]);
        }
        f.Script("nested", "SetPCChatMessage(\"\");");
        f.Script("handler", "ExecuteScript(\"nested\",OBJECT_SELF);"); Assert.Empty(f.Chat());
    }

    [DataFact]
    public void PayloadReturnsWithoutOverwritingCallerIdentityOrScriptParameters()
    {
        using var f = new Fixture();
        f.Script("nested", "SetLocalString(OBJECT_SELF,\"childParam\",GetScriptParam(\"key\"));SetPCChatMessage(\"changed\");");
        f.Script("handler", "SetScriptParam(\"key\",\"child\");ExecuteScript(\"nested\",OBJECT_SELF);SetLocalString(OBJECT_SELF,\"parentParam\",GetScriptParam(\"key\"));");
        f.World.Module.Scripts["Mod_OnPlrChat"] = "handler";
        var ctx = new EventContext().Set("EventScriptType", 91).Set("DirectEventScriptType", 92)
            .Set("param:key", "caller").Set("ChatMessage", "original");
        f.World.RunEvent(f.World.Module, "Mod_OnPlrChat", ctx);
        Assert.Equal("changed", ctx.Str("ChatMessage"));
        Assert.Equal(91, ctx.Int("EventScriptType")); Assert.Equal(92, ctx.Int("DirectEventScriptType"));
        Assert.Equal("caller", ctx.Str("param:key"));
        Assert.Equal("child", f.World.Module.Locals.Strings["childParam"]);
        Assert.Equal("caller", f.World.Module.Locals.Strings["parentParam"]);
    }
}
