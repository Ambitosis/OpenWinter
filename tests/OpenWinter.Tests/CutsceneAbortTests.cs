using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class CutsceneAbortTests
{
    sealed class Fixture
    {
        public readonly World World;
        public readonly Creature Pc;
        public Fixture()
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride:false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            Pc = Player();
            Script("toggle","void main(){SetCutsceneMode(OBJECT_SELF,GetLocalInt(OBJECT_SELF,\"enabled\"));}");
            Script("read","void main(){SetLocalObject(OBJECT_SELF,\"last\",GetLastPCToCancelCutscene());}");
            Script("abort","void main(){object p=GetLastPCToCancelCutscene();SetLocalObject(OBJECT_SELF,\"last\",p);SetLocalInt(OBJECT_SELF,\"count\",GetLocalInt(OBJECT_SELF,\"count\")+1);SetLocalInt(OBJECT_SELF,\"event\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"cutscene\",GetCutsceneMode(p));SetLocalInt(OBJECT_SELF,\"plot\",GetPlotFlag(p));if(GetLocalInt(OBJECT_SELF,\"exit\"))SetCutsceneMode(p,FALSE);}");
            World.Module.Scripts["Mod_OnCutsnAbort"]="abort";
        }
        void Script(string name,string source)
        {
            var data=NssCompiler.Compile(source,name,World.Scripts!.Definitions,_=>null,out var error);
            Assert.True(data != null,error); World.Scripts.Register(name,new NcsProgram(name,data!));
        }
        public Creature Player()
        {
            var (_,server)=LoopbackConnection.CreatePair();
            var pc=new Creature { Player=new PlayerSession { Connection=server } }; World.Register(pc); return pc;
        }
        public void Mode(bool enabled)
        {
            Pc.Locals.Ints["enabled"]=enabled?1:0; World.Scripts!.Run("toggle",Pc.Id);
        }
        public void Abort(string data="{}") => Assert.True(ClientRequests.Handle(World,Pc,"cutscene-abort",data));
    }

    [DataFact]
    public void NoOpHookCanReceiveRepeatedEscapeWithoutEndingTheCutscene()
    {
        var f=new Fixture(); f.Mode(true); f.Abort(); f.Abort();
        Assert.Equal(2,f.World.Module.Locals.Ints["count"]);
        Assert.Equal(f.Pc.Id,f.World.Module.Locals.Objects["last"]);
        Assert.Equal(3014,f.World.Module.Locals.Ints["event"]);
        Assert.Equal(1,f.World.Module.Locals.Ints["cutscene"]);
        Assert.Equal(1,f.World.Module.Locals.Ints["plot"]);
        Assert.True(f.Pc.InCutscene); Assert.True(f.Pc.Plot);
    }

    [DataFact]
    public void HookOwnsExitAndLastCancellerRemainsAvailableOutsideItsContext()
    {
        var f=new Fixture(); f.World.Scripts!.Run("read",f.Pc.Id);
        Assert.Equal(GameObject.InvalidId,f.Pc.Locals.Objects["last"]);
        f.World.Module.Locals.Ints["exit"]=1; f.Mode(true); f.Abort();
        Assert.False(f.Pc.InCutscene); Assert.False(f.Pc.Plot);
        f.World.Scripts.Run("read",f.Pc.Id);
        Assert.Equal(f.Pc.Id,f.Pc.Locals.Objects["last"]);
        f.World.Step(1); f.World.Scripts.Run("read",f.Pc.Id);
        Assert.Equal(f.Pc.Id,f.Pc.Locals.Objects["last"]);
        f.Abort(); Assert.Equal(1,f.World.Module.Locals.Ints["count"]);
    }

    [DataFact]
    public void ScriptModeChangesDoNotSignalAndRequestsCannotNameAnotherPlayer()
    {
        var f=new Fixture(); var other=f.Player(); other.InCutscene=true;
        f.Abort(); f.Mode(true); f.Mode(false);
        Assert.False(f.World.Module.Locals.Ints.ContainsKey("count"));
        f.Mode(true); f.Abort("{\"player\":"+other.Id+"}");
        Assert.Equal(f.Pc.Id,f.World.Module.LastCutsceneCanceller);
        Assert.True(other.InCutscene);
        var npc=new Creature { InCutscene=true }; f.World.Register(npc);
        ClientRequests.Handle(f.World,npc,"cutscene-abort","{}");
        Assert.Equal(1,f.World.Module.Locals.Ints["count"]);
    }
}
