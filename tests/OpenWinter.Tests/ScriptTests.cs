using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Scripting;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

public class ScriptTests
{
    readonly ITestOutputHelper _out;
    public ScriptTests(ITestOutputHelper output) { _out = output; }

    static string ScriptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "tests", "scripts"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "tests", "scripts");
    }

    static World ScriptWorld()
    {
        var rm = new ResMan(TestData.Paths);
        rm.Add(new DirectorySource(ScriptsDir()));
        rm.AddBaseGame();
        var world = new World(rm);
        world.EnableScripts();
        world.Register(world.Module);
        world.Module.Tag = "test_module";
        return world;
    }

    [DataFact]
    public void ParsesAllEngineFunctions()
    {
        var defs = ScriptHost.LoadDefinitions(TestData.Module);
        Assert.True(defs.Functions.Count > 1100);
        Assert.Equal(0, defs.ByName["Random"].Index);
        Assert.Equal(NssType.Action, defs.ByName["DelayCommand"].Params[1].Type);
        Assert.Equal("json", defs.EngineStructures[7]);
        Assert.Equal(1, defs.Int("OBJECT_TYPE_CREATURE"));
        var world = new World(TestData.Module);
        world.EnableScripts(defs);
        Assert.True(world.Scripts!.DuplicateImplementations.Count == 0, string.Join("; ", world.Scripts.DuplicateImplementations));
    }

    /// <summary>Same assertions for the interpreter (-1) and the JIT compiling on first run (0).</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void VmMatchesCompilerSemantics(int jitThreshold)
    {
        if (TestData.Config == null) return;
        var world = ScriptWorld();
        var host = world.Scripts!;
        host.VM.JitThreshold = jitThreshold;
        host.VM.BackgroundCompile = false; // compile synchronously so the compiled path is what runs
        var logs = new List<string>();
        host.Log = logs.Add;
        host.Run("vm_basic", world.Module.Id);
        foreach (var l in logs) _out.WriteLine(l);
        var v = world.Module.Locals;

        Assert.Equal(1, v.Ints["done"]);
        Assert.Equal(20, v.Ints["add"]);
        Assert.Equal(-2, v.Ints["mod"]);
        Assert.Equal(1043, v.Ints["shift"]);
        Assert.Equal(5.5f, v.Floats["float"]);
        Assert.Equal("WORLD2", v.Strings["str"]);
        Assert.Equal(2116, v.Ints["loop"]);
        Assert.Equal(35, v.Ints["while"]);
        Assert.Equal(199, v.Ints["switch"]);
        Assert.Equal("p4/6.0/12.0", v.Strings["struct"]);
        Assert.Equal(610, v.Ints["fib"]);
        Assert.Equal(16, v.Ints["logic"]);
        Assert.Equal(1, v.Ints["valid"]);
        Assert.Equal("glob10", v.Strings["global"]);
        Assert.Equal(90f, v.Floats["locfacing"]);

        // AssignCommand runs after the current script; DelayCommand after its delay.
        Assert.False(v.Strings.ContainsKey("assigned"));
        world.Step(0.1f);
        Assert.Equal("test_module", v.Strings["assigned"]);
        Assert.False(v.Ints.ContainsKey("delayed"));
        for (int i = 0; i < 12; i++) world.Step(0.1f);
        Assert.Equal(42, v.Ints["delayed"]);
        Assert.Equal(15, v.Ints["bumped"]);

        Assert.Equal(1, host.Run("vm_cond", world.Module.Id));
        v.Ints["add"] = 3;
        Assert.Equal(0, host.Run("vm_cond", world.Module.Id));
        if (jitThreshold == 0) Assert.True(host.VM.CompiledPrograms >= 2);
    }
}
