using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Conversations;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// When a request to talk is refused as busy, and what a fight does to an open conversation, as measured on the
/// original (parity suite convo_busy, visual scenes convobusy to convobusy4; docs/CONVERSATION_ENDINGS.md "Busy").
/// </summary>
public sealed class ConversationBusyTests
{
    sealed class Messages : IWorldOutput
    {
        public readonly List<(uint Pc, string Text)> Sent = new();
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) => Sent.Add((pc.Id, text));
        public void FloatingText(Creature over, string text, bool toFaction) { }
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) { }
    }

    sealed class Fixture
    {
        public readonly World World;
        public readonly Area Area;
        public readonly Creature Pc, Npc, Other;
        public readonly ConversationService Convo;
        public readonly Messages Out = new();
        public string Busy => World.Resources.TlkString(ConversationService.BusyMessage);
        public string Excited => World.Resources.TlkString(ConversationService.TooExcitedMessage);

        public Fixture()
        {
            var memory = new MemorySource("authored conversation busy tests");
            memory.Set(ResKey.Of("busyarea", ResType.Are), Gff.Write(new GffStruct().SetString("Tag", "BUSY").SetInt("Width", 1).SetInt("Height", 1), "ARE "));
            memory.Set(ResKey.Of("busyarea", ResType.Git), Gff.Write(new GffStruct(), "GIT "));
            // Two lines: the first leads on (a stop there aborts), the second's only answer ends it (a stop there finishes).
            GffStruct Link(int i) => new GffStruct().SetDWord("Index", (uint)i).SetResRef("Active", "");
            var dialog = new GffStruct()
                .SetResRef("EndConversation", "busy_end").SetResRef("EndConverAbort", "busy_abort")
                .SetList("StartingList", [Link(0)])
                .SetList("EntryList", [
                    new GffStruct().SetLocString("Text", new LocString("First line.")).SetList("RepliesList", [Link(0)]),
                    new GffStruct().SetLocString("Text", new LocString("Second line.")).SetList("RepliesList", [Link(1)])])
                .SetList("ReplyList", [
                    new GffStruct().SetLocString("Text", new LocString("Keep talking.")).SetList("EntriesList", [Link(1)]),
                    new GffStruct().SetLocString("Text", new LocString("Finish."))]);
            memory.Set(ResKey.Of("busy_dlg", ResType.Dlg), Gff.Write(dialog, "DLG "));
            var resources = new ResMan(TestData.Paths); resources.Add(memory); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources) { Output = Out };
            World.Register(World.Module); World.EnableScripts();
            Area = new AreaLoader(World).Load("busyarea"); World.Areas.Add(Area);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 3, true);
            Area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 3, true);
            var (_, server) = LoopbackConnection.CreatePair();
            Pc = new Creature { Tag = "PC", Player = new PlayerSession { Connection = server } };
            Npc = new Creature { Tag = "NPC", Conversation = "busy_dlg" };
            Other = new Creature { Tag = "OTHER" };
            World.AddToArea(Pc, Area, new Vector3(10, 10, 0), 0, exact: true);
            World.AddToArea(Npc, Area, new Vector3(12, 10, 0), 0, exact: true);
            World.AddToArea(Other, Area, new Vector3(12, 12, 0), 0, exact: true);
            Convo = World.System<ConversationService>();
            // The creature's OnConversation counts, records who asked, and begins its dialog as nw_c2_default4 does.
            Script("busy_conv", "void main(){SetLocalInt(OBJECT_SELF,\"onconv\",GetLocalInt(OBJECT_SELF,\"onconv\")+1);"
                + "SetLocalString(OBJECT_SELF,\"speaker\",GetTag(GetLastSpeaker()));SetLocalInt(OBJECT_SELF,\"pcspeaker\",GetIsObjectValid(GetPCSpeaker()));"
                + "SetLocalInt(OBJECT_SELF,\"began\",BeginConversation());}");
            Npc.Scripts["ScriptDialogue"] = "busy_conv";
            const string talking = "SetLocalInt(GetModule(),\"talking\",IsInConversation(GetPCSpeaker())+2*IsInConversation(OBJECT_SELF));";
            Script("busy_end", "void main(){SetLocalInt(GetModule(),\"ended\",GetLocalInt(GetModule(),\"ended\")+1);" + talking + "}");
            Script("busy_abort", "void main(){SetLocalInt(GetModule(),\"aborted\",GetLocalInt(GetModule(),\"aborted\")+1);" + talking + "}");
            Script("busy_ask", "void main(){ActionStartConversation(GetObjectByTag(GetLocalString(OBJECT_SELF,\"to\")),GetLocalString(OBJECT_SELF,\"dlg\"),FALSE,FALSE);}");
            Script("busy_begin", "void main(){SetLocalInt(OBJECT_SELF,\"began\",BeginConversation(\"busy_dlg\",GetObjectByTag(\"PC\")));}");
        }

        public void Script(string name, string source)
        {
            var bytes = NssCompiler.Compile(source, name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(bytes != null, error); World.Scripts.Register(name, new NcsProgram(name, bytes!));
        }

        /// <summary><paramref name="who"/> runs ActionStartConversation toward <paramref name="to"/>, and the world steps until it is done.</summary>
        public void Ask(Creature who, Creature to, string dialog = "")
        {
            who.Locals.Strings["to"] = to.Tag;
            who.Locals.Strings["dlg"] = dialog;
            World.Scripts!.Run("busy_ask", who.Id);
            for (int i = 0; i < 5; i++) World.Step(0.1f);
        }

        public int Module(string key) => World.Module.Locals.Ints.GetValueOrDefault(key);
    }

    [DataFact]
    public void ClickingACreatureInCombatSaysItIsBusy()
    {
        var f = new Fixture();
        f.Npc.InCombat = true;
        Assert.Equal(ActionStatus.Done, new InteractAction { Target = f.Npc.Id, Talk = true, Type = 7 }.Step(f.World, f.Pc, 0.1f));
        Assert.Equal(0, f.Npc.Locals.Ints.GetValueOrDefault("onconv"));
        Assert.False(f.Convo.IsInConversation(f.Pc));
        Assert.Equal((f.Pc.Id, "The object is busy and can not talk to you right now."), f.Out.Sent.Single());

        // Out of combat the click runs its OnConversation, which opens the dialog.
        f.Npc.InCombat = false;
        new InteractAction { Target = f.Npc.Id, Talk = true, Type = 7 }.Step(f.World, f.Pc, 0.1f);
        Assert.Equal(1, f.Npc.Locals.Ints["onconv"]);
        Assert.Equal(1, f.Npc.Locals.Ints["began"]);
        Assert.True(f.Convo.IsInConversation(f.Pc));
    }

    [DataFact]
    public void ScriptedRequestsAreRefusedWhileTheOneAskedIsInCombatOrDead()
    {
        var f = new Fixture();
        // The player asks a fighting creature, naming its dialog or not: refused, with the busy message.
        f.Npc.InCombat = true;
        f.Ask(f.Pc, f.Npc);
        f.Ask(f.Pc, f.Npc, "busy_dlg");
        Assert.Equal(0, f.Npc.Locals.Ints.GetValueOrDefault("onconv"));
        Assert.False(f.Convo.IsInConversation(f.Pc));
        Assert.Equal(new[] { f.Busy, f.Busy }, f.Out.Sent.Select(m => m.Text));

        // The fighting creature may still ask the player itself (convobusy npc_attacked_npc_asks).
        f.Ask(f.Npc, f.Pc, "busy_dlg");
        Assert.True(f.Convo.IsInConversation(f.Pc));
        f.Convo.End(f.Pc, aborted: true);
        f.Npc.InCombat = false;

        // A player in combat: asked by a creature she is "too excited"; asking a creature works; her own private
        // conversation is busy (convobusy2 pc_combat_npc_asks, convobusy pc_attacked, convobusy4 pc_combat_self).
        f.Pc.InCombat = true;
        f.Out.Sent.Clear();
        f.Ask(f.Npc, f.Pc, "busy_dlg");
        Assert.False(f.Convo.IsInConversation(f.Pc));
        Assert.Equal((f.Pc.Id, f.Excited), f.Out.Sent.Single());
        f.Ask(f.Pc, f.Pc, "busy_dlg");
        Assert.False(f.Convo.IsInConversation(f.Pc));
        Assert.Equal(f.Busy, f.Out.Sent[^1].Text);
        f.Ask(f.Pc, f.Npc);
        Assert.Equal(1, f.Npc.Locals.Ints["onconv"]);
        Assert.True(f.Convo.IsInConversation(f.Pc));
        f.Convo.End(f.Pc, aborted: true);
        f.Pc.InCombat = false;

        // A dead creature: refused without a word (convobusy npc_dead).
        f.Out.Sent.Clear();
        f.Npc.IsDead = true;
        f.Ask(f.Pc, f.Npc);
        Assert.Equal(1, f.Npc.Locals.Ints["onconv"]);
        Assert.Empty(f.Out.Sent);
    }

    [DataFact]
    public void BeginConversationIsNeverRefused()
    {
        // convobusy npc_attacking_begin, convobusy3 pc_combat_begin.
        var f = new Fixture();
        f.Npc.InCombat = true;
        f.Pc.InCombat = true;
        f.World.Scripts!.Run("busy_begin", f.Npc.Id);
        Assert.Equal(1, f.Npc.Locals.Ints["began"]);
        Assert.True(f.Convo.IsInConversation(f.Pc));
        Assert.Empty(f.Out.Sent);
    }

    [DataFact]
    public void BetweenCreaturesARequestRunsThePartnersOnConversation()
    {
        // parity suite convo_busy: whatever dialog is named, only the partner's OnConversation runs, with the asker as
        // the last speaker and no PC speaker; a partner in combat or dead hears nothing.
        var f = new Fixture();
        f.Ask(f.Other, f.Npc, "busy_dlg");
        Assert.Equal(1, f.Npc.Locals.Ints["onconv"]);
        Assert.Equal("OTHER", f.Npc.Locals.Strings["speaker"]);
        Assert.Equal(0, f.Npc.Locals.Ints["pcspeaker"]);
        Assert.False(f.Convo.IsInConversation(f.Npc));
        f.Npc.InCombat = true;
        f.Ask(f.Other, f.Npc);
        f.Npc.InCombat = false;
        f.Npc.IsDead = true;
        f.Ask(f.Other, f.Npc);
        Assert.Equal(1, f.Npc.Locals.Ints["onconv"]);
        Assert.Empty(f.Out.Sent);
    }

    [DataFact]
    public void StartingAnAttackEndsAnOpenConversationButBeingAttackedDoesNot()
    {
        var f = new Fixture();
        // Being attacked (in combat) leaves the conversation open (convobusy open_npc_attacked, open_pc_attacked).
        Assert.True(f.Convo.Start(f.Npc, f.Pc, ""));
        f.Npc.InCombat = f.Pc.InCombat = true;
        f.Convo.Step(0.1f);
        Assert.True(f.Convo.IsInConversation(f.Pc));
        f.Npc.InCombat = f.Pc.InCombat = false;

        // The talker starts an attack at a line that leads on: aborted, and the abort script still sees both talking.
        new Game.Combat.AttackAction { Target = f.Other.Id }.Step(f.World, f.Npc, 0.1f);
        Assert.False(f.Convo.IsInConversation(f.Pc));
        Assert.Equal((0, 1, 3), (f.Module("ended"), f.Module("aborted"), f.Module("talking")));

        // At the last line the player's own attack finishes it (convobusy2 last_attack, convobusy3 open_pc_attacks).
        f.Npc.Actions.Clear();
        Assert.True(f.Convo.Start(f.Npc, f.Pc, ""));
        f.Convo.Choose(f.Pc, 0);
        new Game.Combat.AttackAction { Target = f.Other.Id }.Step(f.World, f.Pc, 0.1f);
        Assert.False(f.Convo.IsInConversation(f.Pc));
        Assert.Equal((1, 1, 3), (f.Module("ended"), f.Module("aborted"), f.Module("talking")));
    }

    [DataFact]
    public void CastingASpellEndsAnOpenConversation()
    {
        // convobusy2 open_cast, convobusy3 open_pc_casts.
        var f = new Fixture();
        f.Script("busy_cast", "void main(){ActionCastSpellAtObject(SPELL_ACID_SPLASH,OBJECT_SELF,METAMAGIC_NONE,TRUE);}");
        Assert.True(f.Convo.Start(f.Npc, f.Pc, ""));
        f.World.Scripts!.Run("busy_cast", f.Npc.Id);
        f.World.Step(0.1f);
        Assert.False(f.Convo.IsInConversation(f.Pc));
        Assert.Equal(1, f.Module("aborted"));
    }
}
