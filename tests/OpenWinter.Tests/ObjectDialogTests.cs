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

public sealed class ObjectDialogTests
{
    sealed class Fixture
    {
        public readonly World World;
        public readonly Creature Pc;
        public readonly Area Area;
        public readonly ConversationService Conversations;
        public Fixture()
        {
            var memory = new MemorySource("authored object dialogue tests");
            var area = new GffStruct().SetString("Tag", "OBJECT_DIALOG").SetInt("Width", 1).SetInt("Height", 1);
            GffStruct Instance(string tag) => new GffStruct().SetString("Tag", tag).SetInt("Appearance", -1)
                .SetResRef("OnDialog", "hook").SetResRef("Conversation", "dialog");
            var git = new GffStruct().SetList("Placeable List", [Instance("PLC")]).SetList("Door List", [Instance("DOOR")]);
            memory.Set(ResKey.Of("objectdialog", ResType.Are), Gff.Write(area, "ARE "));
            memory.Set(ResKey.Of("objectdialog", ResType.Git), Gff.Write(git, "GIT "));
            var dialog = new GffStruct()
                .SetList("StartingList", [new GffStruct().SetDWord("Index", 0)])
                // An answer, so it opens a window (a first line with none is spoken as a one-liner).
                .SetList("EntryList", [new GffStruct().SetLocString("Text", new LocString("Object dialogue."))
                    .SetList("RepliesList", [new GffStruct().SetDWord("Index", 0)])])
                .SetList("ReplyList", [new GffStruct().SetLocString("Text", new LocString("Bye."))]);
            memory.Set(ResKey.Of("dialog", ResType.Dlg), Gff.Write(dialog, "DLG "));
            var resources = new ResMan(TestData.Paths); resources.Add(memory); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            Area = new AreaLoader(World).Load("objectdialog"); World.Areas.Add(Area);
            Area.Walkmesh.Add(new(0,0,0), new(40,0,0), new(40,40,0), 3, true);
            Area.Walkmesh.Add(new(0,0,0), new(40,40,0), new(0,40,0), 3, true);
            var (_, server) = LoopbackConnection.CreatePair();
            Pc = new Creature { Tag = "PC", Player = new PlayerSession { Connection = server } };
            World.AddToArea(Pc, Area, new Vector3(1,1,0), 0, exact: true);
            Conversations = World.System<ConversationService>();
            Script("hook", "void main(){SetLocalInt(OBJECT_SELF,\"count\",GetLocalInt(OBJECT_SELF,\"count\")+1);SetLocalInt(OBJECT_SELF,\"speaker\",GetIsObjectValid(GetLastSpeaker()));SetLocalInt(OBJECT_SELF,\"pcspeaker\",GetIsObjectValid(GetPCSpeaker()));if(GetLocalInt(OBJECT_SELF,\"begin\"))SetLocalInt(OBJECT_SELF,\"began\",BeginConversation());}");
        }
        public void Script(string name, string source)
        {
            var bytes = NssCompiler.Compile(source, name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(bytes != null, error); World.Scripts.Register(name, new NcsProgram(name, bytes!));
        }
        public GameObject Target(string tag) => World.ByTag(tag).Single();
    }

    [DataFact]
    public void AreaLoaderReadsEeDoorAndPlaceableDialogHandlers()
    {
        var f = new Fixture();
        Assert.Equal("hook", f.Target("PLC").Script("OnDialog"));
        Assert.Equal("hook", f.Target("DOOR").Script("OnDialog"));
    }

    [DataFact]
    public void PlayerTalkHookCanDeclineOrBeginWithoutExposingConversationSpeakerEarly()
    {
        var f = new Fixture();
        foreach (var tag in new[] { "PLC", "DOOR" })
        {
            var target = f.Target(tag);
            var action = new InteractAction { Target = target.Id, Talk = true };
            Assert.Equal(ActionStatus.Done, action.Step(f.World, f.Pc, 0.1f));
            Assert.False(f.Conversations.IsInConversation(f.Pc));
            Assert.Equal(1, target.Locals.Ints["count"]);
            Assert.Equal(0, target.Locals.Ints["speaker"]);
            Assert.Equal(0, target.Locals.Ints["pcspeaker"]);
            target.Locals.Ints["begin"] = 1;
            action.Step(f.World, f.Pc, 0.1f);
            Assert.Equal(1, target.Locals.Ints["began"]);
            Assert.Equal(target, f.Conversations.SessionOf(f.Pc)!.Owner);
            Assert.Equal("dialog", f.Conversations.SessionOf(f.Pc)!.ResRef);
            f.Conversations.Choose(f.Pc, -1);
        }
    }

    [DataFact]
    public void BeginConversationDefaultsToTheRequestedDialogAndInitiatingPlayer()
    {
        var f = new Fixture();
        var plc = (Placeable)f.Target("PLC");
        plc.Template.SetResRef("Conversation", "");
        plc.Locals.Ints["begin"] = 1;
        f.Script("request", "void main(){ActionStartConversation(GetObjectByTag(\"PLC\"),\"dialog\",FALSE,FALSE);}");
        f.World.Scripts!.Run("request", f.Pc.Id);
        for (int step = 0; step < 5; step++) f.World.Step(0.1f);
        Assert.Equal(1, plc.Locals.Ints["began"]);
        Assert.Equal("dialog", f.Conversations.SessionOf(f.Pc)!.ResRef);
        Assert.Equal(plc, f.Conversations.SessionOf(f.Pc)!.Owner);
    }

    [DataFact]
    public void ScriptedTalkUsesObjectHookEvenWithExplicitDialogAndUseStaysSeparate()
    {
        var f = new Fixture();
        f.Script("talk", "void main(){ActionStartConversation(GetObjectByTag(GetLocalString(OBJECT_SELF,\"tag\")),\"dialog\",FALSE,FALSE);}");
        foreach (var tag in new[] { "PLC", "DOOR" })
        {
            var target = f.Target(tag);
            f.Pc.Locals.Strings["tag"] = tag;
            f.World.Scripts!.Run("talk", f.Pc.Id);
            for (int step = 0; step < 5; step++) f.World.Step(0.1f);
            Assert.Equal(1, target.Locals.Ints["count"]);
            Assert.False(f.Conversations.IsInConversation(f.Pc));
        }
        var plc = (Placeable)f.Target("PLC"); plc.Useable = true;
        plc.Template.SetResRef("Conversation", "");
        f.Script("used", "void main(){SetLocalInt(OBJECT_SELF,\"used\",1);}");
        plc.Scripts["OnUsed"] = "used";
        new InteractAction { Target = plc.Id }.Step(f.World, f.Pc, 0.1f);
        Assert.Equal(1, plc.Locals.Ints["used"]);
        Assert.Equal(1, plc.Locals.Ints["count"]);
    }
}
