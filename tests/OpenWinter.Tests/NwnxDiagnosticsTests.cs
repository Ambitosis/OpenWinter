using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Nwnx;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using Xunit;

namespace OpenWinter.Tests;

public class NwnxDiagnosticsTests
{
    static (World World, NwnxRegistry Registry, List<string> Log) Setup()
    {
        var world = new World(new ResMan(TestData.Paths));
        var registry = world.System<NwnxRegistry>();
        var log = new List<string>();
        registry.Log = log.Add;
        return (world, registry, log);
    }

    [DataFact]
    public void UnknownLegacyAndNativeCallsShareOneWarningWithoutBreakingStoredValues()
    {
        var (_, registry, log) = Setup();
        registry.Stack.Push(NwnxValue.Int(42));
        registry.Stack.Push(NwnxValue.String("key"));
        Assert.True(registry.Call("NWNX_Custom", "SetExample"));
        registry.Stack.Push(NwnxValue.String("key"));
        Assert.True(registry.Call("custom", "GetExample"));
        Assert.Equal(42, registry.PopAs(NwnxType.Int));
        registry.Stack.Push(NwnxValue.Int(43));
        registry.Stack.Push(NwnxValue.String("key"));
        Assert.True(registry.TryLegacyCall("NWNXEE!ABIv2!nwnx_CUSTOM!SetExample!CALL"));
        Assert.Equal(2, registry.MissingCalls["NWNX_custom_SetExample"]);
        Assert.Equal(2, log.Count); // one setter warning and one getter warning
        Assert.All(log, text => Assert.Contains("no engine behavior", text));
        Assert.False(registry.Call("custom", "ExecuteExample"));
        Assert.False(registry.Call("CUSTOM", "executeexample"));
        Assert.Equal(3, log.Count);
        Assert.Equal(0, registry.Stack.Count);
    }

    [DataFact]
    public void KnownPlaceholdersAreReportedOnceEachAndStillConsumeTheirArguments()
    {
        var (world, registry, log) = Setup();
        var item = new Item();
        world.Register(item);
        var calls = new (string Plugin, string Function)[]
        {
            ("Player", "GetTURD"), ("Player", "RemoveEffectFromTURD"),
        };
        foreach (var (plugin, function) in calls)
        {
            for (int i = 0; i < 2; i++)
            {
                Assert.True(registry.Call(plugin, function));
                registry.Stack.Clear(); // GetTURD retains its existing result.
            }
            Assert.Equal(2, registry.MissingCalls[$"NWNX_{plugin}_{function}"]);
        }
        Assert.Equal(calls.Length, log.Count);
        registry.Stack.Push(NwnxValue.Int(23));
        registry.Stack.Push(NwnxValue.Object(item.Id));
        Assert.True(registry.Call("Item", "SetWeight"));
        Assert.Equal(23, item.WeightOverride);
        Assert.Equal(calls.Length, log.Count);
        registry.Stack.Push(NwnxValue.Object(item.Id));
        Assert.True(registry.Call("Item", "GetBaseGoldPieceValue"));
        Assert.Equal((int)world.Inventory.Rules.UnitValue(item, item.Identified), registry.PopAs(NwnxType.Int));
        Assert.Equal(calls.Length, registry.MissingCalls.Count);
    }

    [DataFact]
    public void UnemittedEngineSubscriptionsWarnButRemainAvailableToExplicitSignals()
    {
        var (world, registry, log) = Setup();
        foreach (var evt in new[] { "CUSTOM_EVENT", "NWNX_ON_DM_POSSESS_BEFORE", "NWNX_ON_UNKNOWN_A_BEFORE", "NWNX_ON_UNKNOWN_A_BEFORE", "NWNX_ON_UNKNOWN_B_BEFORE" })
        {
            registry.Stack.Push(NwnxValue.String("callback"));
            registry.Stack.Push(NwnxValue.String(evt));
            Assert.True(registry.Call("Events", "SubscribeEvent"));
            Assert.Equal(1, EventsPlugin.SubscriberCount(world, evt));
        }
        // One warning per unemitted event, naming it.
        Assert.Equal(2, log.Count);
        Assert.All(log, text => Assert.Contains("explicit SignalEvent still works", text));
        Assert.Contains("NWNX_ON_UNKNOWN_A_BEFORE", log[0]);
        Assert.Contains("NWNX_ON_UNKNOWN_B_BEFORE", log[1]);
        Assert.Equal(3, registry.MissingCalls["NWNX_events_SubscribeEvent"]);
    }
}
