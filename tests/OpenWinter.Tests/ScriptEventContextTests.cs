using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using Xunit;

namespace OpenWinter.Tests;

public sealed class ScriptEventContextTests
{
    static World Create()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        world.Register(world.Module);
        world.EnableScripts();
        return world;
    }

    static void Script(World world, string name, string source)
    {
        var bytes = NssCompiler.Compile(source, name, world.Scripts!.Definitions, _ => null, out var error);
        Assert.True(bytes != null, error);
        world.Scripts.Register(name, new NcsProgram(name, bytes!));
    }

    [DataFact]
    public void EveryRegisteredObjectHandlerStampsAnIsolatedContext()
    {
        var world = Create();
        Script(world, "record", "void main(){SetLocalInt(OBJECT_SELF,\"inherited\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"direct\",GetCurrentlyRunningEvent(FALSE));SetLocalInt(OBJECT_SELF,\"payload\",GetUserDefinedEventNumber());}");
        (GameObject Object, int First, int Last)[] groups =
        [
            (world.Module, 3000, 3021), (new Area(), 4000, 4003),
            (new Creature(), 5000, 5012), (new Trigger(), 7000, 7006),
            (new Placeable(), 9000, 9015), (new Door(), 10000, 10014),
            (new AreaOfEffect(), 11000, 11003), (new Encounter(), 13000, 13004),
            (new Store(), 14000, 14001),
        ];
        var context = new EventContext().Set("EventScriptType", 999).Set("UserDefined", 42);
        foreach (var (target, first, last) in groups)
        {
            world.Register(target);
            for (int id = first; id <= last; id++)
            {
                var field = EventScripts.Field(target, id);
                Assert.NotNull(field);
                target.Scripts[field!] = "record";
                world.RunEvent(target, field!, context);
                Assert.Equal(id, target.Locals.Ints.GetValueOrDefault("inherited"));
                Assert.Equal(id, target.Locals.Ints.GetValueOrDefault("direct"));
                Assert.Equal(42, target.Locals.Ints.GetValueOrDefault("payload"));
            }
        }
        Assert.Equal(999, context.Int("EventScriptType"));
        Assert.False(context.Values.ContainsKey("DirectEventScriptType"));
        Assert.Equal(0, world.Scripts!.CurrentEvent.Int("EventScriptType"));
        world.Module.Scripts["unknown"] = "record";
        world.RunEvent(world.Module, "unknown", context);
        Assert.Equal(0, world.Module.Locals.Ints.GetValueOrDefault("inherited"));
    }

    [DataFact]
    public void ExecuteScriptAndChunkInheritButDeferredCommandsHaveNoEvent()
    {
        var world = Create();
        Script(world, "nested", "void main(){SetLocalInt(OBJECT_SELF,\"nested\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"nestedDirect\",GetCurrentlyRunningEvent(FALSE));ExecuteScript(\"leaf\",OBJECT_SELF);}");
        Script(world, "leaf", "void main(){SetLocalInt(OBJECT_SELF,\"leaf\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"leafDirect\",GetCurrentlyRunningEvent(FALSE));}");
        Script(world, "handler", """
            void Record(string key) { SetLocalInt(OBJECT_SELF,key,GetCurrentlyRunningEvent()); SetLocalInt(OBJECT_SELF,key+"Direct",GetCurrentlyRunningEvent(FALSE)); }
            void main() {
                ExecuteScript("nested",OBJECT_SELF);
                ExecuteScriptChunk("SetLocalInt(OBJECT_SELF,\"chunk\",GetCurrentlyRunningEvent());SetLocalInt(OBJECT_SELF,\"chunkDirect\",GetCurrentlyRunningEvent(FALSE));",OBJECT_SELF);
                Record("after");
                SetLocalInt(OBJECT_SELF,"delay",-1); SetLocalInt(OBJECT_SELF,"assign",-1);
                DelayCommand(0.1,Record("delay")); AssignCommand(OBJECT_SELF,Record("assign"));
            }
            """);
        world.Module.Scripts["Mod_OnUsrDefined"] = "handler";
        world.RunEvent(world.Module, "Mod_OnUsrDefined", new EventContext());
        foreach (var key in new[] { "nested", "leaf", "chunk", "after" })
            Assert.Equal(3001, world.Module.Locals.Ints.GetValueOrDefault(key));
        foreach (var key in new[] { "nestedDirect", "leafDirect", "chunkDirect" })
            Assert.Equal(0, world.Module.Locals.Ints.GetValueOrDefault(key));
        Assert.Equal(3001, world.Module.Locals.Ints.GetValueOrDefault("afterDirect"));
        Assert.Equal(-1, world.Module.Locals.Ints.GetValueOrDefault("delay"));
        world.Step(0.2f);
        foreach (var key in new[] { "delay", "assign", "delayDirect", "assignDirect" })
            Assert.Equal(0, world.Module.Locals.Ints.GetValueOrDefault(key));
    }

    [DataFact]
    public void ConversationSignalsDispatchOnceToEachSupportedObjectHandler()
    {
        var world = Create();
        Script(world, "dialog", "void main(){SetLocalInt(OBJECT_SELF,\"count\",GetLocalInt(OBJECT_SELF,\"count\")+1);SetLocalInt(OBJECT_SELF,\"type\",GetCurrentlyRunningEvent(FALSE));}");
        Script(world, "signal", "void main(){SignalEvent(OBJECT_SELF,EventConversation());}");
        (GameObject Object, string Field, int Type)[] targets =
        [(new Creature(), "ScriptDialogue", 5007), (new Door(), "OnDialog", 10013), (new Placeable(), "OnDialog", 9014)];
        foreach (var (target, field, _) in targets)
        {
            world.Register(target);
            target.Scripts[field] = "dialog";
            world.Scripts!.Run("signal", target.Id);
            Assert.Equal(0, target.Locals.Ints.GetValueOrDefault("count"));
        }
        world.Step(0.1f);
        foreach (var (target, _, type) in targets)
        {
            Assert.Equal(1, target.Locals.Ints.GetValueOrDefault("count"));
            Assert.Equal(type, target.Locals.Ints.GetValueOrDefault("type"));
        }
    }
}
